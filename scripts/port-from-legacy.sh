#!/usr/bin/env bash
#
# 009_sheet-pile-quaywall — レガシー 3 リポジトリからの Core 層移植スクリプト
#
# docs/implementation-plan.md §11(ファイル移行マッピング)の機械的実行。
#
# 設計意図:
#   1. 移植元をコミットハッシュで時点固定し、`git show` で取り出す。
#      移植元リポジトリの作業ツリー状態に一切影響されない
#      (006 は 6a777b1 で継手・控え杭を削除済みのため、この方式が必須)。
#   2. namespace 置換は sed による機械置換。CLAUDE.PRIVATE.md §2.1 の
#      using 禁止規約により全型が完全修飾で書かれており、手作業では
#      転記ミスが避けられないため。
#   3. 冪等。出力先を上書きするので何度実行しても同じ結果になる。
#      実行後に `git diff` が空であれば、移植先は移植元と同期している。
#
# 対象フェーズ: フェーズ 1(移植のみで green になる Core)
#   フェーズ 2 で移植する 006@6d6d8cf 由来の 3 モジュール
#   (PieceAssignment / FrontWallPlacement / AnchorAlignment)は
#   AutoCAD 型からのロジック抽出を伴う書き直しであり、機械置換では
#   移植できないため本スクリプトの対象外。
#
# 使い方:
#   scripts/port-from-legacy.sh [レガシーリポジトリの親ディレクトリ]

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
LEGACY_ROOT="${1:-$(dirname "$REPO_ROOT")}"

# ── 移植元の時点固定(docs/implementation-plan.md §11)────────────────────
SRC_007="$LEGACY_ROOT/007_steel-pipe-sheet-pile"
SRC_008="$LEGACY_ROOT/008_tairod"
COMMIT_007="b12b188"
COMMIT_008="ff3a986"

# ── namespace 置換規則 ──────────────────────────────────────────────────
NS_FRONTWALL="SheetPileQuayWall.Core.FrontWall"   # ← SteelPipeSheetPile.Data
NS_TIEROD="SheetPileQuayWall.Core.TieRod"         # ← TaiRod.Core
NS_TESTS="SheetPileQuayWall.Core.Tests"           # ← SteelPipeSheetPile.Tests / TaiRod.Core.Tests

CORE_DIR="$REPO_ROOT/src/SheetPileQuayWall.Core"
TESTS_DIR="$REPO_ROOT/tests/SheetPileQuayWall.Core.Tests"

die() { echo "エラー: $*" >&2; exit 1; }

require_commit() {
    local repo="$1" commit="$2"
    [ -d "$repo/.git" ] || die "移植元リポジトリが見つかりません: $repo"
    git -C "$repo" cat-file -e "${commit}^{commit}" 2>/dev/null \
        || die "移植元コミットが見つかりません: $repo@$commit"
}

require_commit "$SRC_007" "$COMMIT_007"
require_commit "$SRC_008" "$COMMIT_008"

mkdir -p "$CORE_DIR/FrontWall" "$CORE_DIR/TieRod" "$TESTS_DIR/fixtures"

# ── 前壁 Core: 007@b12b188 src/Data/*.cs → Core/FrontWall/ ──────────────
# JointShapes.cs も含め、決定 3 のとおり内容は変更しない(namespace 置換のみ)。
for f in DriveEstimate InputValidator JointCatalog JointGeometry \
         JointParameters JointPlacement JointShapes SectionProperties; do
    git -C "$SRC_007" show "$COMMIT_007:src/Data/$f.cs" \
        | sed -e "s/SteelPipeSheetPile\.Data/$NS_FRONTWALL/g" \
        > "$CORE_DIR/FrontWall/$f.cs"
done

# JointShapes.cs のヘッダのみ、009 に存在しない生成スクリプトへの参照と
# 紛らわしい旧リポジトリ名を、移植元を指す記述へ置き換える(決定 3 の運用注記)。
sed -i \
    -e "s|再生成: python3 scripts/generate_joint_shapes\.py|009 では再生成しない (docs/implementation-plan.md §1 決定3)|" \
    -e "s|// 元データ: 008_steel-pipe-sheet-pile (DXF抽出+カタログ構築、詳細は同リポジトリREADME)|// 移植元: 007_steel-pipe-sheet-pile@$COMMIT_007 src/Data/JointShapes.cs (DXF抽出+カタログ構築。生成スクリプトと DXF 原本は 007 側にある)|" \
    "$CORE_DIR/FrontWall/JointShapes.cs"

# ── タイロッド Core: 008@ff3a986 src/TaiRod.Core/*.cs → Core/TieRod/ ────
for f in Enums TieRodCalculator TieRodCatalog TieRodParameters TieRodResult; do
    git -C "$SRC_008" show "$COMMIT_008:src/TaiRod.Core/$f.cs" \
        | sed -e "s/TaiRod\.Core/$NS_TIEROD/g" \
        > "$CORE_DIR/TieRod/$f.cs"
done

# ── テスト: 007 の 8 ファイル + 008 の 4 ファイル → 単一テストプロジェクト ──
for f in DriveEstimateTests InputValidatorTests JointCatalogTests JointGeometryTests \
         JointParametersTests JointPlacementTests JointShapesTests SectionPropertiesTests; do
    git -C "$SRC_007" show "$COMMIT_007:tests/SteelPipeSheetPile.Tests/$f.cs" \
        | sed -e "s/SteelPipeSheetPile\.Tests/$NS_TESTS/g" \
              -e "s/SteelPipeSheetPile\.Data/$NS_FRONTWALL/g" \
        > "$TESTS_DIR/$f.cs"
done

# 置換順に依存: TaiRod.Core.Tests を先に処理しないと TaiRod.Core が先に食う。
for f in CalculatorTests CatalogJsonTests CatalogTests ValidationTests; do
    git -C "$SRC_008" show "$COMMIT_008:tests/TaiRod.Core.Tests/$f.cs" \
        | sed -e "s/TaiRod\.Core\.Tests/$NS_TESTS/g" \
              -e "s/TaiRod\.Core/$NS_TIEROD/g" \
        > "$TESTS_DIR/$f.cs"
done

# ── テスト用フィクスチャ ────────────────────────────────────────────────
# ランタイムからは参照されない照合専用データのため data/ ではなく tests 配下に置く。
# CatalogJsonTests は AppContext.BaseDirectory\data\ から読むため、
# 出力先へのリンクはテスト csproj 側で data\ に張っている。
git -C "$SRC_008" show "$COMMIT_008:data/tairod_catalog_dimensions.json" \
    > "$TESTS_DIR/fixtures/tairod_catalog_dimensions.json"

echo "移植完了:"
echo "  前壁 Core    : $(ls -1 "$CORE_DIR/FrontWall"/*.cs | wc -l) ファイル  ← 007@$COMMIT_007"
echo "  タイロッド Core: $(ls -1 "$CORE_DIR/TieRod"/*.cs | wc -l) ファイル  ← 008@$COMMIT_008"
echo "  テスト        : $(ls -1 "$TESTS_DIR"/*.cs | wc -l) ファイル  ← 007@$COMMIT_007 + 008@$COMMIT_008"
echo
echo "検証: dotnet test tests/SheetPileQuayWall.Core.Tests"
