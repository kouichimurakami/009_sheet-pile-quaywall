# 009_sheet-pile-quaywall

**鋼管矢板式岸壁**(前壁鋼管矢板 + タイロッド + 控え杭)を、単一の DLL で完結してパラメトリック 3D モデル生成・積算(施工歩掛計算)できる AutoCAD 2025 / Civil 3D 2025 プラグイン。

> ⚠️ **実機動作は未検証。**使用前に [docs/known-issues.md](docs/known-issues.md) を読むこと。積算基準の一部は OCR から復元できていない。

## 概要

- 対象構造物: 前壁鋼管矢板(直杭)+ タイロッド + 控え杭(傾斜可)
- 環境: C# / .NET 8.0(`net8.0-windows`、x64)/ AutoCAD 2025 .NET / Civil 3D 2025 / Dynamo 3.3(Zero Touch Node)
- 構成: **Core(AutoCAD 非依存の計算層)/ Plugin(AutoCAD 依存層)/ Dynamo(Zero Touch Node 層)** の 3 プロジェクト分割。Core は BCL のみ参照し、WSL / Linux でもテストできる(**xUnit 674 ケース green**)
- `006_steel-pipe-pile` / `007_steel-pipe-sheet-pile` / `008_tairod` の後継・統合版。**009 単独でビルド・実行できる**(3 リポジトリへの参照は持たない)

## クイックスタート

```bash
# Core のビルド・テストだけなら AutoCAD 不要(674 件 green)
dotnet test tests/SheetPileQuayWall.Core.Tests -c Release
```

AutoCAD / Civil 3D への NETLOAD、Dynamo への Import Library、実機ビルドの手順は [docs/build.md](docs/build.md) を参照。

## 概略図

### 側面(X–Z)

```
      海側 −X                     X = 0                               陸側 +X
                                    ┃
   Z_head ┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┓┃
   (入力・内部表現の基準点)        ┃┃      腹起し(矢板半割部)   定着プレート・ワッシャー・ナット
   Z_tr ┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┃┣━━━━━━━━━━━━━━━━━━━━━━━━━━━━┫┓┄┄┄ タイロッド軸心
  (= H.W.L. + 0.5)                ┃┃                            ┃┃
   D.L.±0.000 ━━━━━━━━━━━━━━━━━━━━╋╋━━━━━━━━━━━━━━━━━━━━━━━━━━━━╋╋━━━ 基本水準面 Z = 0
                                  ┃┃                            ┃┃
                   前壁鋼管矢板 → ┃┃                            ┃┃ ← 控え杭
                   (直杭のみ)     ┃┃                            ┃┃   (傾斜角 θ_a 可)
                                  ┗┛                            ┗┛
   Z_tip ┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄ 杭先端(ソリッド生成用の計算値)
                                    │←────────── span ─────────→│
                                       (前壁矢板中心 〜 控え杭陸側定着面)
```

前壁は `Z_head = Z_tip + L`(直杭のみのため三角関数を使わない単純計算)。控え杭は今も傾斜角 θ_a を持つため、内部は従来どおり杭先端標高 Z_tip 基準。

### 平面(X–Y)

```
      +Y(施設延長方向)
       ↑
       │  前壁鋼管矢板(ピッチ = 有効幅 B、継手で連結)
       │   ●
       │   ●━━━━━━━━━━━━━━━━━━━○ ← タイロッド + 控え杭
       │   ●                        (取付間隔 = B の整数倍。
       │   ●━━━━━━━━━━━━━━━━━━━○    矢板中央を横断する)
       │   ●
       └──────────────────────────→ +X(陸側)
              ←────── span ─────→

   施工順位 1 本目から +Y 方向へ打設。継手は −Y 側(先行矢板と嵌合)/ +Y 側(後続を受ける)
```

### 座標系

| 軸 | 方向 | 備考 |
|---|---|---|
| X | 陸側 → +X、海側 → −X | 法線直角方向 |
| Y | 施設延長方向 | 法線平行方向 |
| Z | 鉛直上向き | **Z = 0 を D.L.(基本水準面)に統一**。3 部材共通 |

標高パラメータ(前壁の杭上端標高 Z_head、控え杭の杭先端標高 Z_tip、タイロッド軸心標高 Z_tr 等)はすべて D.L. 基準の数値がそのまま Z 座標になる。

## 参照アセンブリ

| アセンブリ | 用途 | 対象プロジェクト | Copy Local |
|---|---|---|---|
| `AcCoreMgd.dll` | AutoCAD コア | Plugin | `False` |
| `AcDbMgd.dll` | Database / Solid3d / XData | Plugin | `False` |
| `AcMgd.dll` | Application / Document | Plugin | `False` |
| `DynamoServices.dll` | `MultiReturn` / `IsVisibleInDynamoLibrary` 属性 | Dynamo | `False` |
| `ProtoGeometry.dll` | Dynamo ネイティブジオメトリ(`SpqwGeometryNodes`、実験的) | Dynamo | `False` |

**Core は BCL のみ**を参照する。**Dynamo は AutoCAD 本体 DLL を一切参照しない**(独立プロジェクトに分離した理由は [docs/dynamo-nodes.md](docs/dynamo-nodes.md) を参照)。**参照 DLL バージョンは未検証**(開発機に AutoCAD が無いため。`scripts/verify-dll-versions.ps1` が実機で exit 0 になるまで配布しないこと。CLAUDE.PRIVATE.md §9)。

## できること

- **AutoCAD / Civil 3D コマンド 19 個**(`SPQW_*`): 前壁・タイロッド・控え杭の生成 / 再生成 / 照会、3 工法(打撃・バイブロ単独・ジェット併用)の打設歩掛積算、施設全体の数量集計、帳票 CSV 一括取込み。→ [docs/commands.md](docs/commands.md)
- **Dynamo ノード 10 個**: 計算専用 `SpqwNodes` 7 個(断面性能・数量集計・柱状図解析・打設歩掛積算 4 系統)+ ソリッド生成 `SpqwGeometryNodes` 3 個(控え杭・前壁本体円筒・タイロッド。実験的・未検証、XData なし)。→ [docs/dynamo-nodes.md](docs/dynamo-nodes.md)

## もっと詳しく

| ドキュメント | 内容 |
|---|---|
| [docs/commands.md](docs/commands.md) | AutoCAD コマンド全 19 種の詳細・ワークフロー・XData 設計 |
| [docs/dynamo-nodes.md](docs/dynamo-nodes.md) | Dynamo ノード全 10 種の入出力・配線例 |
| [docs/parameters.md](docs/parameters.md) | 入力パラメータ表(英語名 / 日本語名 / 単位 / デフォルト値 / 範囲) |
| [docs/calculations.md](docs/calculations.md) | 自動算出される計算値と信頼度(確定 / 概算 / 推定) |
| [docs/build.md](docs/build.md) | ビルド方法・NETLOAD / Import Library 手順・プロジェクト構成 |
| [docs/known-issues.md](docs/known-issues.md) | 積算基準の OCR 復元限界・実装範囲の限定・既知の不整合 |
| [docs/implementation-plan.md](docs/implementation-plan.md) | 設計判断の経緯・フェーズ計画・実機検証項目 |
| [docs/features.html](docs/features.html) | 機能概要(図表中心、ブラウザで開く) |

## 規約・制約

- **`using` ディレクティブを使わない**。型は完全修飾名で書く(暗黙 using も無効化)。
- **単位はメートル統一**。mm は対話プロンプト・Dynamo 入力の呼称のみで、取得直後に m へ変換する。
- **Z 軸は上向き、Z = 0 が D.L.**。下向き座標は使わない。
- 部材 1 本につき **Solid3d 1 個**に集約する(`BoolUnite` / `BoolSubtract`)。
- 参照 DLL は `<Private>False</Private>`(Copy Local = False)。AutoCAD 本体 DLL を配布物に同梱しない。
- **006 / 007 / 008 へのプロジェクト参照・アセンブリ参照を追加しない**。共通ロジックは 009 内にコードとして移植する。
- 整合性チェックの誤差許容は **1 mm = 0.001 m**。不一致時はエラー停止し、自動補正も再生成もしない(外径の JIS / カタログスナップのみ例外)。
- **基準の表で「−」のセルは `null` として扱い、0 に潰さない**。0 として計算に混入すると打込み時間が消える等の誤りにつながるため、該当する組合せではエラー停止する。
- 旧 RegApp(`STEELPIPEPILE` / `SPSP` / `TAIROD_PARAM` / `ANCHORPILE`)で作成した既存図面との**互換は持たない**。旧図面は旧プラグインで扱うか 009 で再作成する。009 自身の旧バージョン(`tip_x/_y/_z` 形式の前壁 XData 等)とは自動変換で互換を保つ([docs/commands.md](docs/commands.md) の XData 設計)。

**部材間の整合性チェック**(`CrossMemberValidator`。統合版として、同じ量を 2 部材が別々に入力している箇所を突き合わせる):

| # | 突き合わせ |
|---|---|
| 1 | タイロッドの海側鋼管矢板径 ⟺ 前壁の外径 |
| 2 | タイロッドの矢板ピッチ ⟺ 前壁の有効幅 B |
| 3 | タイロッドの軸心標高 ⟺ 控え杭の Z_tr |
| 4 | タイロッドの `span_length` ⟺ 控え杭の `span` |

## 変更履歴(要約)

**過去のすべての変更点は `git log` で追える。** 本節は現在の設計を理解するうえで背景を知っておく価値がある、アーキテクチャ上の主要な決定だけを新しい順に要約する(詳細な経緯・テスト件数の推移は `git log` と [`docs/implementation-plan.md`](docs/implementation-plan.md) の改訂履歴を参照)。

1. **`SpqwGeometryNodes` を新設**(`ProtoGeometry.dll` 参照、実験的・未検証)。控え杭・前壁本体円筒・タイロッドの 3 ノード。Dynamo が焼き込んだジオメトリに XData を後付けする手段が無いため、`_Action` 相当には対応せず、パラメトリック性は Dynamo 自身のグラフ再実行に委ねる設計にした。
2. **PP形継手の3Dモデル幾何整合性を検証**(`JointFit.PpPipeCenterDistance`)。2本の継手鋼管の中心間距離が外径に依らず一定(≈パイプ半径)であることを確認し回帰テスト化した。
3. **Dynamo ノードを独立プロジェクト `SheetPileQuayWall.Dynamo` へ分離**。実機で `Dynamo.Exceptions.LibraryLoadFailedException` が発生し、原因は `<Private>False</Private>` 参照(AcCoreMgd 等)が `deps.json` に載らず Dynamo 側の依存解決が失敗すること。AutoCAD 参照を持たない別プロジェクトへ分離して解決した。**最初に試した `IsVisibleInDynamoLibrary(false)` の付与(型走査除外)は効果が無く、この対処で置き換えた**(型走査が原因という仮説自体は反証済み)。
4. **前壁の内部表現を杭先端標高 Z_tip 基準から杭上端標高 Z_head 基準へ刷新**(`FrontWallRef.HeadPoint` が正の記録、XData キーも `head_x/_y/_z` に変更)。Z_tip は `PileGeometry.TipFromHead` による表示専用の計算値になった。旧図面(`tip_x/_y/_z` のみ)は読み込み時に自動変換する。
5. **前壁の傾斜角パラメータを廃止**(直杭のみに簡略化。Z_head→Z_tip 変換も単純減算に)。**控え杭の杭上端標高の既定値を前壁と同じ値に変更**(控え杭自身の式による逆算をやめた)。
6. **タイロッド組数を前壁総本数から自動算定**(`TieRodPitch.CountFor`)し、鋼種・設計基準・荷重状態・腹起し高さ・定着プレート/ワッシャー厚・ナット高さ・調節長・取付点反力の 9 項目のプロンプトを廃止(計算式・XData は維持、既定値固定または前回保存値を使用)。
7. **控え杭の生成をタイロッド選択方式に変更**(軸心標高・配置間隔・本数を選択したタイロッドから自動設定)。位置 Y は図面内の全前壁のうち最小 Y(壁の 1 本目)に自動整列するようにした。
8. **カスタム有効幅 B を使った際の部材間ズレを修正**(`FrontWallRef.EffectiveWidthM` 新設)。壁一括生成で確定した B を XData に保存し、タイロッド・控え杭・施設積算がすべて同じ値を参照するようにした。
9. **継手 3D モデルの幾何整合性を検証**(`JointFit.Overlaps`/`MinClearance`)。LT65/75/100 は径によらず非干渉であることを確認。あわせて実機で発覚した `Region.CreateFromCurves` の `eInvalidInput` クラッシュ(DXF 抽出データの縮退辺・共線点が原因)を `PolygonCleanup` で解消した。
10. **帳票 CSV 取り込み・柱状図解析ノード・打設歩掛積算(打撃/バイブロ単独/ジェット併用/控え杭)・付帯船舶・杭打機/杭打船選定を追加**。フェーズ 5 以降に順次拡張した主要機能群。
11. **007/008 からの Core 移植・部材間整合チェック(`CrossMemberValidator`)の新設**(フェーズ 1〜3)。プロジェクト立ち上げ時の基礎工事にあたる部分。
