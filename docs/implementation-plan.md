# 009_sheet-pile-quaywall 実装計画

本ドキュメントは提案・設計計画であり、**実装(C# コード生成)は行わない**。実装に着手する際はこの計画を土台に CLAUDE.PRIVATE.md §6 の固定順(計画→テスト→参照アセンブリ/コマンド→パラメータ表→派生量表→整合性チェック→コード→注意点→.gitignore影響)で進める。

> **改訂履歴**
> - 2026-07-25 初版
> - 2026-07-25 第2版: 批判的レビューの指摘を反映 — ① 006 の鋼管杭純化(`6a777b1`)に伴う移植元コミット固定(決定 5 改訂・§0・§11)、② 継手 DXF/JSON 非コピーの徹底(決定 3 改訂・§3・§11 の矛盾解消)、③ 単位入出力方針の明確化(決定 7 新設・§7.1)、④ `JointParameters.cs` の補完(§3・§10・§11)、⑤ 旧 RegApp 図面互換を §12 に追加。
> - 2026-07-25 第3版: 実装フェーズ計画を §13 として新設(旧 §13 注意点は §14 へ)。開発環境の実測(WSL に .NET SDK 10.0.302 あり / AutoCAD なし)に基づきフェーズ順を決定し、フェーズ 0(骨格)・1(移植 Core、275 テスト green)を完了。§12 に未解決事項 6(θ 付き前壁とタイロッドの幾何整合)を追加。

## 0. 目的とスコープ

港湾構造物の**鋼管矢板式岸壁**(前壁鋼管矢板 + タイロッド + 控え杭)を、**009 単独で(006 / 007 / 008 を参照・依存せずに)** ビルド・実行できる統合パラメトリックモデルとして構築する。

統合元:

| リポジトリ | 提供する機能 | 009 での位置づけ |
|---|---|---|
| `006_steel-pipe-pile`(移植元は **`006@6d6d8cf`** に時点固定) | 前壁の傾斜角 θ・XData 挿入点追随(1011)・施工順位からの継手自動判定、**控え杭**(タイロッド軸線整列) | ロジックを抽出して移植。ファイルはそのまま流用しない(007 側の構造に合わせて書き直す)。006 本体はその後 `6a777b1` で継手・控え杭を削除し鋼管杭単独に純化済みのため、**これらのコードは 006 の HEAD に存在しない** |
| `007_steel-pipe-sheet-pile` | 前壁の実形状継手(LT65/75/100・PP・PT、DXF 抽出)・断面性能・打設歩掛積算・Dynamo | Core 層(データクラス)をそのまま移植。`JointShapes.cs` は自動生成ファイルのまま複製(決定 3) |
| `008_tairod` | タイロッド(Core/Plugin 分割、172 テストで検証済みの計算層)、D.L. 基準の標高入力・UCS→WCS 変換 | Core 層をそのまま移植。標高入力パターンを前壁・控え杭にも一般化する |

007 / 008 は**現状維持**し、個別リポジトリとしての開発を継続可能とする。006 は 2026-07-25 のコミット `6a777b1` で継手・控え杭を削除し**鋼管杭(単独杭)専用に純化済み**のため、施工順位→継手判定・控え杭のロジックについては **009 が唯一の後継**となる(移植元は `006@6d6d8cf` に時点固定)。009 はそれらを踏まえた独立の統合成果物という位置づけ。

## 1. 今回確定した設計決定

| # | 決定事項 | 内容 | 根拠 |
|---|---|---|---|
| 1 | 座標系の統一 | **Z = 0 を鋼管矢板上部工の法線標高 D.L.(基本水準面)に統一**。前壁・タイロッド・控え杭すべてで共通の鉛直基準とする | 008 は既に D.L. 基準だが、007(矢板底面中心)・006(挿入点=杭先端、D.L. との関係は運用者の申し合わせ任せ)は不統一だった。1 図面に 3 部材を混在させる以上、鉛直基準の統一は必須 |
| 2 | コマンド名の全面刷新 | 006/007/008 の既存コマンド名(`STEELPIPEPILE_*` / `SPSP_*` / `TAIROD_*` / `ANCHORPILE_*`)を継承せず、新体系 `SPQW_<部材>_<動詞>` に統一する | 009 は新しい名前空間の独立プロジェクトであり、旧名を引き継ぐ理由がない。1 台の AutoCAD に複数プラグインを NETLOAD する運用でもコマンド名衝突が起きない |
| 3 | `JointShapes.cs` の移植方法(第2版改訂) | 007 の自動生成ファイル(DXF 抽出+カタログ構築)を**そのままファイルコピー**する。`scripts/generate_joint_shapes.py`・DXF 原本に加え、**中間 JSON(`007/継手/json`)も一切持ち込まない**(初版 §3/§11 の「data/ へ DXF/JSON をコピー」という記述は本決定と矛盾していたため第2版で撤回) | 継手の実形状データは 007 で生成・検証済みであり、009 側で再生成する必要がない。007 のランタイムは JSON/DXF を参照しておらず、実形状の唯一のソースはコード化済みの `JointShapes.cs` である。再生成が要る場合は 007 側で行い、生成物を再コピーする運用とする |
| 4 | プロジェクト構成 | 008 と同じ Core(AutoCAD 非依存)/ Plugin(AutoCAD 依存)分割 | 3 部材の整合性ロジック(整列計算・継手判定・歩掛式)を AutoCAD 無しで xUnit 検証できる。008 で 172 テストの実績あり |
| 5 | 既存リポジトリの扱い(第2版改訂) | 007 / 008 は現状維持。006 は `6a777b1` で継手・控え杭を削除し鋼管杭専用に純化済みのため「現状維持」の前提を改め、**009 を施工順位→継手判定・控え杭ロジックの正式な後継**とする(移植元は `006@6d6d8cf` に時点固定) | 個別プロジェクトの継続開発を妨げない。006 の純化は 007 と役割重複した継手実装の解消が目的(006 コミット `6a777b1` のメッセージ参照) |
| 6 | GitHub リポジトリ | `kouichimurakami/009_sheet-pile-quaywall` を作成し push 済み | 006/007/008 と同じ運用(public リポジトリ) |
| 7 | 単位の入出力方針(第2版新設) | `outerDiameter`・`wallThickness` は**対話プロンプトの表示・入力に限り mm 呼称を許容**し、取得直後に `/1000.0` で m へ変換する。内部処理・XData・派生量・テストはすべて m。実装着手時に CLAUDE.PRIVATE.md §2.1 の「mm 混入禁止」へこの例外(UI 境界のみ mm 呼称可)を明文化する | 007 `SPSP_Create.cs` の実装実績(mm 入力→即 m 変換)と実務の呼び径慣行(φ800 等)。規約の mm 禁止は内部表現に対する規定として維持。008 の `pile_diameter` は m 入力のままであり、前壁 `outerDiameter` との突き合わせは内部の m 同士で行う |

---

## 2. 座標系設計の詳細(決定 1: D.L. 統一)

### 2.1 現状(統一前)との差分

| 構造物 | 旧原点 / Z 基準 | 新原点 / Z 基準(009) |
|---|---|---|
| 前壁(007 由来) | 矢板底面中心(Z=0 が矢板底面、D.L. との対応は図面側の申し合わせ) | **Z=0 = D.L.**。杭先端標高(D.L. 基準の数値)を入力し、ローカル形状(Z=0 が矢板底面)をその標高へ平行移動する |
| 控え杭(006 由来) | 挿入点(杭先端)を `GetPoint` でそのまま取得、Z は picked 点の Z | **変更なし(呼称の明確化のみ)**。杭先端標高は既に D.L. 基準の数値として扱われていたため、009 でもそのまま「杭先端標高 Z_tip(D.L. 基準)」と明記する |
| タイロッド(008 由来) | 既に D.L. 基準(`tie_elevation`)。平面位置は UCS ピック→WCS 変換、Z は picked 点を使わず入力値をそのまま採用 | 変更なし。009 の他 2 部材をこのパターンに合わせる |

### 2.2 前壁の入力方式の変更(008 パターンへの統一)

旧(006 / 007): `GetPoint` で取得した 3 次元点をそのまま挿入点とする。平面位置と標高が 1 回のピックに混在し、標高の桁数管理が暗黙的(クリック時の Z 値に依存)。

新(009):

1. 平面位置(X, Y)は UCS でクリック取得し、`ed.CurrentUserCoordinateSystem` で WCS へ変換する(008 `TAIROD_Create` と同一手順)。Z は使用しない。
2. 標高は「杭先端標高 Z_tip(D.L. 基準)」を別途数値入力する。
3. 配置変換は `Rotation(θ, Y軸, 原点) → Displacement(X, Y, Z_tip)` の順(006 `BuildPileSolid` の変換順序を踏襲)。

この分離により、「平面位置は目視ピック、標高は正確な数値入力」という実務上の作業分担に対応する。タイロッド・控え杭は既にこの分離ができている。

### 2.3 各構造物の Z 入力一覧(統一後)

| 構造物 | Z 入力パラメータ | 意味 | 既定値(暫定) |
|---|---|---|---|
| 前壁 | `tipElevation` Z_tip | 杭先端標高(D.L. 基準) | −18.0 m |
| タイロッド | `tie_elevation` | タイロッド軸心標高(D.L. 基準) | `hwl` + 0.5 m |
| 控え杭 | `tipElevation` Z_tip | 杭先端標高(D.L. 基準) | 前壁の Z_tip を既定値として提示 |

### 2.4 整合性チェックへの影響

控え杭の整合性チェック(006 由来、§8 に再掲)は「Z_tr が前壁/控え杭の杭体範囲内」を検証するが、この判定は**前壁・控え杭が同一の D.L. 基準に乗っていること**を前提とする。統一により、この前提が 009 全体で常に成立するようになる(006 単体では 008 との併用時にユーザーが図面の Z=0 を D.L. に手動で合わせる必要があったが、009 では設計上不要になる)。

---

## 3. ディレクトリ構成(確定版)

```
009_sheet-pile-quaywall/
├── CLAUDE.md / CLAUDE.PRIVATE.md          (作成済み)
├── docs/
│   └── implementation-plan.md              (本ファイル)
├── src/
│   ├── SheetPileQuayWall.Core/            ← AutoCAD 非依存。xUnit で全網羅
│   │   ├── FrontWall/                       前壁鋼管矢板(007 Core 移植 + 006 ロジック抽出)
│   │   │   ├── JointCatalog.cs              継手部材諸元(007 そのまま移植)
│   │   │   ├── JointParameters.cs           JointType enum・有効幅ディスパッチ(007 そのまま移植。第2版で補完 — 他の FrontWall 全ファイルがこの enum に依存し、これ無しではコンパイル不能)
│   │   │   ├── JointGeometry.cs             継手有効間隔 J・有効幅(007 そのまま移植)
│   │   │   ├── JointShapes.cs               実形状断面(007 ファイルをそのままコピー、決定3)
│   │   │   ├── JointPlacement.cs            配置変換(007 そのまま移植)
│   │   │   ├── SectionProperties.cs         断面性能(007 そのまま移植)
│   │   │   ├── DriveEstimate.cs             打設歩掛積算(007 そのまま移植)
│   │   │   ├── PieceAssignment.cs           【新規】施工順位→継手要否(006 ロジック抽出)
│   │   │   ├── FrontWallPlacement.cs        【新規】傾斜角θ・D.L.標高→配置変換(006 ロジック抽出 + §2.2 反映)
│   │   │   └── InputValidator.cs            統合入力検証(007 拡張 + 006/mm混入検出)
│   │   ├── TieRod/                          タイロッド(008 TaiRod.Core そのまま移植)
│   │   │   ├── TieRodCatalog.cs
│   │   │   ├── TieRodParameters.cs
│   │   │   ├── TieRodCalculator.cs
│   │   │   ├── TieRodResult.cs
│   │   │   └── Enums.cs
│   │   ├── AnchorPile/                      控え杭(006 ANCHORPILE ロジック抽出)
│   │   │   ├── AnchorAlignment.cs           整列計算(前壁軸 + span → 控え杭軸、§2.2 のD.L.統一を反映)
│   │   │   ├── AnchorInput.cs
│   │   │   └── AnchorResult.cs
│   │   └── SheetPileQuayWall.Core.csproj    net8.0、AutoCAD 参照なし(CLAUDE.PRIVATE.md §9 対象外)
│   │
│   └── SheetPileQuayWall.Plugin/          ← AutoCAD / Civil3D / Dynamo 依存
│       ├── Commands/
│       │   ├── FrontWall_Create.cs / _Action.cs / _Query.cs / _Estimate.cs / _JointModel.cs
│       │   ├── TieRod_Create.cs / _Action.cs / _Query.cs / _Color.cs
│       │   └── AnchorPile_Create.cs / _Action.cs / _Query.cs
│       ├── XData/
│       │   ├── FrontWallXData.cs / TieRodXData.cs / AnchorPileXData.cs
│       ├── Dynamo/
│       │   └── SpqwNodes.cs
│       └── SheetPileQuayWall.Plugin.csproj  net8.0-windows、Core を ProjectReference
│
├── tests/SheetPileQuayWall.Core.Tests/     007(8ファイル・103件)+ 008(4ファイル・172件)相当を移植・再編 + 新規モジュール分
│   └── fixtures/
│       └── tairod_catalog_dimensions.json   カタログ照合テスト専用(008 由来。ランタイム未使用のため data/ ではなく tests 配下に置く)
├── stubs/                                   AutoCAD API スタブ(008 のパターン踏襲、構文検証専用)
└── scripts/verify-dll-versions.ps1
```

※ 第2版: 初版にあった `data/` ディレクトリは廃止した。継手 DXF/JSON は 009 に持ち込まず(決定 3)、タイロッドカタログ JSON はテスト専用フィクスチャとして tests 配下へ移した。

---

## 4. コマンド一覧(新体系、旧名からの対応表)

| 新コマンド(009) | 相当する旧コマンド(移行元) | 説明 |
|---|---|---|
| `SPQW_FRONTWALL_Create` | `SPSP_Create`(007)+ 傾斜角・継手自動判定(006) | 対話入力 → Solid3d 生成(実形状継手 + 傾斜角対応) → XData 記録 |
| `SPQW_FRONTWALL_Action` | `SPSP_Action`(007)+ XData 位置追随(006) | 既存選択 → 再入力 → 同位置(MOVE 後は追随)に再生成 |
| `SPQW_FRONTWALL_Query` | `SPSP_Query`(007) | 諸元・断面性能・積算数量を出力 |
| `SPQW_FRONTWALL_Estimate` | `SPSP_Estimate`(007) | 打設歩掛積算(貫入抵抗・ハンマ選定・労務編成) |
| `SPQW_FRONTWALL_JointModel` | `SPSP_JointModel`(007) | 継手嵌合モデル(隣接2本+継手部材)生成 |
| `SPQW_TIEROD_Create` | `TAIROD_Create`(008) | 組数分の Solid3d 生成、XData 記録 |
| `SPQW_TIEROD_Action` | `TAIROD_Action`(008) | 選択1本のみ再生成 |
| `SPQW_TIEROD_Query` | `TAIROD_Query`(008) | 諸元・張力照査・受杭数量を出力 |
| `SPQW_TIEROD_Color` | `TAIROD_Color`(008) | 色番号のみ変更 |
| `SPQW_ANCHORPILE_Create` | `ANCHORPILE_Create`(006) | 前壁選択 → タイロッド軸線に整列した控え杭を生成 |
| `SPQW_ANCHORPILE_Action` | `ANCHORPILE_Action`(006) | 前壁基準の整列位置に再生成 |
| `SPQW_ANCHORPILE_Query` | `ANCHORPILE_Query`(006) | 諸元・整列座標・積算数量を出力 |

Dynamo ノード(暫定、`SpqwNodes` クラス): `CalcSection`・`CreateSolid`(007 `SpspNodes` 移植)。タイロッド・控え杭のノードは 006/008 でも未実装のため 009 でもフェーズ2以降とする(§11)。

---

## 5. 参照アセンブリ(暫定)

| アセンブリ | 用途 | 対象 |
|---|---|---|
| `AcCoreMgd.dll` | AutoCAD コア | Plugin |
| `AcDbMgd.dll` | Database / Solid3d / XData | Plugin |
| `AcMgd.dll` | Application | Plugin |
| `DynamoServices.dll` | `MultiReturn` / 警告ログ | Plugin(`ExcludeDynamo` で除外可、007 の方式踏襲) |
| `ProtoGeometry.dll` | Dynamo ジオメトリ | Plugin(同上) |

すべて `<Private>False</Private>`。**Core は BCL のみ参照し、上記アセンブリを一切参照しない**(CLAUDE.PRIVATE.md §9 の検証対象外)。バージョンは 006/007/008 と同様に**現時点で未検証**(開発機に AutoCAD 未インストール)。

---

## 6. XData 設計(新 RegApp 名)

| 部材 | RegApp 名(新) | 参考: 旧 RegApp 名 | 主なフィールド(暫定) |
|---|---|---|---|
| 前壁 | `SPQW_FRONTWALL` | `SPSP`(007)/ `STEELPIPEPILE`(006) | D, t, L, 継手コード, 鋼種(007)+ 傾斜角θ・施工順位・総本数・色(006)+ 挿入点1011(平面位置+D.L.標高) |
| タイロッド | `SPQW_TIEROD` | `TAIROD_PARAM`(008) | 008 の18項目 + 配置位置(base_x/base_y)+ rod_index(そのまま踏襲) |
| 控え杭 | `SPQW_ANCHORPILE` | `ANCHORPILE`(006) | D, t, L, θ, span, Z_tr, 先端形状, 色, 前壁Handle参照, 挿入点1011 |

XData のインデックス順は各部材で新規に確定し、007 の「順序変更禁止」規約(§8)を 009 でも踏襲する(いったん確定したら以後変更しない)。

---

## 7. 入力パラメータ表(統合案、D.L. 統一を反映)

### 7.1 前壁(`SPQW_FRONTWALL_Create`)

| 英語名 | 日本語名 | 単位 | デフォルト値 | 範囲 | 由来 |
|---|---|---|---|---|---|
| `outerDiameter` | 外径 D | mm(入力時呼称のみ) | 800 | 500〜2000(007 準拠) | 007 |
| `wallThickness` | 肉厚 t | mm(入力時呼称のみ) | 12 | 9〜25 かつ d>0 | 007 |
| `length` | 全長 L | m | 20.0 | 1〜80 | 007/006 |
| `jointType` | 継手形式 | − | LT75 | LT65/LT75/LT100/PP/PT | 007 |
| `grade` | 鋼種 | − | SKY400 | SKY400/SKY490 | 007 |
| `inclinationDeg` | 傾斜角 θ | deg | 0.0 | 0〜15(Y軸周り) | 006(新規移植) |
| `pieceIndex` | 施工順位 | 本 | 1 | 1〜pieceCount | 006(新規移植) |
| `pieceCount` | 総本数 | 本 | 1 | 1〜500 | 006(新規移植) |
| `planPoint` | 平面位置 [x, y] | m | − | UCSピック→WCS変換(§2.2) | 008パターンへ統一(新規) |
| `tipElevation` | 杭先端標高 Z_tip | m(D.L.) | −18.0(仮) | −80〜10 | 006由来、D.L.基準に明確化 |
| `colorIndex` | 本管の色 | ACI | 8 | 1〜255 | 006/007共通 |

**単位の扱い(決定 7)**: 上表の mm は対話プロンプト上の呼称のみ。取得直後に m へ変換し、内部処理・XData・派生量・テストはすべて m とする(007 `SPSP_Create.cs` の方式を踏襲)。タイロッド側の `pile_diameter`(§7.2、m 入力)と前壁 `outerDiameter` の整合確認は内部の m 同士で行う。

### 7.2 タイロッド(`SPQW_TIEROD_Create`)

008 の 18 項目をそのまま踏襲(README 008 §5 参照)。`tie_elevation` は既に D.L. 基準のため変更なし。

### 7.3 控え杭(`SPQW_ANCHORPILE_Create`)

006 の 9 項目(前壁選択 + D/t/L/θ/先端形状/span/Z_tr/Z_tip/色)をそのまま踏襲。呼称のみ「杭先端標高 Z_tip(D.L. 基準)」に統一。

---

## 8. 派生量表(信頼度ラベル、統合案)

| モジュール | 主な派生量 | 信頼度 |
|---|---|---|
| 前壁 断面性能(007) | A, I, Z, i, W | 確定 |
| 前壁 有効幅 B(007) | LT65/LT75: 確定、LT100: 推定、PP/PT: 確定 | §5(007 README)参照 |
| 前壁 打設歩掛(007) | R, Q, 打設日数, 労務編成 | 確定(積算基準3-4.5節) |
| 前壁 継手判定(006由来) | 継手有無・雌雄(pieceIndex/pieceCount から) | 確定 |
| タイロッド(008) | 全長, 断面積, 質量, 継手構成, 受杭数, 許容張力 | 確定(一部概算、008 README §6 参照) |
| 控え杭 整列(006由来) | 挿入点, 取付点, 軸間水平距離, 杭面間浄距離 | 確定〜概算(006 README §6 参照) |

---

## 9. 整合性チェック一覧(統合、D.L. 基準に合わせて文言更新)

| 部材 | チェック内容 | 由来 |
|---|---|---|
| 前壁 | 内径>0、肉厚が jointType 別範囲内、傾斜角0〜15°、施工順位1〜総本数 | 007+006統合 |
| タイロッド | カタログ規格径一致、waling_height条件、pile_pitch条件、tie_spacing整数倍、nut_height/adjust_length表値一致 等9項目 | 008(README §5そのまま) |
| 控え杭 | 内径>0、肉厚範囲内、**Z_tr が前壁/控え杭の杭体範囲内(D.L.基準、§2.4)**、干渉なし(span条件)、前壁XData必須 | 006(README §5そのまま、D.L.統一により前提強化) |

すべて誤差許容 1 mm = 0.001 m、不一致時はエラー停止・再生成しない(自動補正しない。前壁外径の JIS/カタログスナップのみ例外)。

---

## 10. テスト計画

| モジュール | 移植元 | 追加が必要なテスト |
|---|---|---|
| JointParameters / JointCatalog / JointGeometry / JointShapes / JointPlacement | 007(テスト 8 ファイル・103 ケースの該当分をそのまま) | 移植のみ(既存テストをそのまま流用。`JointParametersTests` 9 ケースを含む — 第2版で補完) |
| SectionProperties / DriveEstimate | 007 | 移植のみ |
| **PieceAssignment**(新規) | 006 ロジック抽出 | 施工順位 1 本目/最終本/中間の継手有無判定 3 ケース |
| **FrontWallPlacement**(新規) | 006 ロジック抽出 + §2.2 | 傾斜角 θ=0/10° での平面位置+標高→挿入点変換、XData 位置追随(MOVE 後の再生成一致) |
| TieRod 一式 | 008(172件そのまま) | 移植のみ |
| **AnchorAlignment**(新規) | 006 ANCHORPILE ロジック抽出 | 整列座標(直杭/傾斜杭)、整合性チェック(D.L. 統一後の前壁/控え杭範囲判定含む)、干渉判定 |
| 統合 InputValidator | 007+006+008 統合 | 3 部材共通の mm 混入検出・範囲外エラー |

---

## 11. ファイル移行マッピング

移植元はすべて**コミットハッシュで時点固定**する(第2版): **006@`6d6d8cf`**(006 はその後 `6a777b1` で継手・控え杭を削除済みのため、該当コードは HEAD から取得不能)、**007@`b12b188`**・**008@`ff3a986`**(いずれも 2026-07-25 時点の HEAD)。

| 009 のファイル | 由来 | 種別 |
|---|---|---|
| `Core/FrontWall/JointCatalog.cs`・`JointParameters.cs` ほか計 7 ファイル(Data 全 8 のうち `JointShapes.cs` を除く) | `007@b12b188` の `src/Data/*.cs` | そのまま移植(namespace のみ変更) |
| `Core/FrontWall/JointShapes.cs` | `007@b12b188` の `src/Data/JointShapes.cs` | **ファイルそのままコピー**(決定3。自動生成ファイルの再生成は行わない) |
| `Core/FrontWall/PieceAssignment.cs` | `006@6d6d8cf` の `src/SteelPipePile.cs` 継手判定部分(**006 HEAD では削除済み**) | ロジック抽出・書き直し(AutoCAD 型を除去) |
| `Core/FrontWall/FrontWallPlacement.cs` | `006@6d6d8cf` の `src/SteelPipePile.cs` `BuildPileSolid` 変換部分(同メソッドは 006 HEAD にも残るが、時点固定のため同一コミットを参照) | ロジック抽出・書き直し(§2.2 の D.L. 統一を反映) |
| `Core/TieRod/*.cs` | `008@ff3a986` の `src/TaiRod.Core/*.cs` | そのまま移植(namespace のみ変更) |
| `Core/AnchorPile/AnchorAlignment.cs` | `006@6d6d8cf` の `src/AnchorPile.cs` 整列計算部分(**006 HEAD では削除済み**) | ロジック抽出・書き直し |
| (継手 DXF/JSON) | − | **コピーしない**(第2版で撤回。決定 3 のとおり持ち込まない。初版記載の `007/data/` は実在せず、`007/継手/json` は 007 ランタイム未参照の生成中間物) |
| `tests/SheetPileQuayWall.Core.Tests/fixtures/tairod_catalog_dimensions.json` | `008@ff3a986` の `data/tairod_catalog_dimensions.json` | そのままコピー(ランタイム未使用。カタログ照合テスト専用フィクスチャ) |
| `Plugin/Commands/*.cs` | 006/007/008 の Commands 相当 | 新規書き直し(コマンド名刷新・Core 呼び出しへの置換) |
| `Plugin/XData/*.cs` | 006/007/008 の XData 相当 | 新規書き直し(RegApp 名刷新、§6) |

---

## 12. 未解決事項(次のステップで決めること)

1. **タイロッドの前壁参照方式**: 控え杭(006)は前壁ソリッドを選択して XData から読み取るが、タイロッド(008)は前壁中心点を目視クリックするだけで XData 連携がない。009 で統一するか(タイロッドも前壁選択方式にする)、現状の使い分けを維持するかは未決定。
2. **`SPQW_FRONTWALL_JointModel` の要否**: 007 由来のこの機能(隣接2本+継手部材の別ソリッド生成)を 009 に残すか、`_Create` の継手一体化(BoolUnite)で代替可能として削るか。
3. **Dynamo ノードの対象範囲**: 前壁(007 由来)のみ移植する案が現実的だが、タイロッド・控え杭を含めるかはフェーズ分けの判断が必要。
4. **プロジェクト名 `SPQW`**: コマンド接頭辞としての適否(他候補: `KSMY`〈鋼矢板護岸〉等)。ユーザー側で語感の確認が必要であれば別案を検討する。フェーズ 4 の全コマンド名・全 RegApp 名に波及するため、フェーズ 3 までに確定が必要。
5. **旧 RegApp 図面の互換方針**(第2版追加): 006 は `6a777b1` で XData を 11→8 要素に変更し「旧図面は再作成が必要」と割り切った。この結果、`ANCHORPILE` RegApp を持つ既存図面は現在どのプラグインからも Action/Query できない。007(`SPSP`)・008(`TAIROD_PARAM`)で作成した既存図面も、RegApp 名を刷新する 009 の `SPQW_*` からはそのままでは読めない。009 に旧 XData 読取→`SPQW_*` 変換の救済コマンドを設けるか、006 と同様「旧図面は旧プラグインで扱う/再作成」で割り切るかを実装着手前に決める。
6. **θ 付き前壁とタイロッドの幾何整合**(第3版追加): 008 の計算層は「X=0 が海側鋼管矢板の中心軸」を前提としており、**前壁が鉛直であることを暗黙に仮定**している。一方 009 の前壁は傾斜角 θ(0〜15°)を持つ。θ=15°・水深 20 m ならタイロッド取付点の水平ずれは約 5 m に達するため無視できない。タイロッド側に θ 補正を導入するか、タイロッド併用時は θ=0 に制限するかを決める(フェーズ 3 のブロッカー)。

## 13. 実装フェーズ計画

### 13.1 方針の根拠(実測)

着手前に開発環境を実測し、当初の前提を 1 点訂正した。**開発機の WSL に .NET SDK 10.0.302 がインストール済みで、net8.0 のビルドとテスト実行ができる**(CLAUDE.md §5 は「SDK 未インストールなら構文レビューに留める」としていたが、Core 層には当てはまらない)。一方、AutoCAD は未インストールで `/mnt/c` 自体が未マウントのため、**Plugin 層はスタブによる構文検証までが限界**であり、実機確認とDLLバージョン検証(§9)はユーザー環境に依存する。

この非対称性(WSL で検証できることは安く、実機確認は高い)から、**WSL で検証できる範囲を先に消化するボトムアップ順**を採用する。骨格構築のみ最初に置き、実機貫通の前倒し(Walking Skeleton)は採らない。

### 13.2 フェーズ構成

| フェーズ | 内容 | 検証基準 | §12 未解決の影響 | 状態 |
|---|---|---|---|---|
| **0. 骨格** | プロジェクト 4 個(Core / Plugin / tests / stubs)、スタブ移植、`scripts/verify-dll-versions.ps1` 配置 | Core・tests がビルド成功。スタブ経由で Plugin がビルド成功 | なし | **完了 2026-07-25** |
| **1. 移植のみの Core** | 007 `src/Data` 8 ファイル → `Core/FrontWall`、008 `TaiRod.Core` 5 ファイル → `Core/TieRod`、テスト 12 ファイル | `dotnet test` で **275/275 pass** | なし | **完了 2026-07-25** |
| **2. 006 由来の新規 Core** | `PieceAssignment` / `FrontWallPlacement` / `AnchorAlignment` を `006@6d6d8cf` から抽出。`ed.WriteMessage` 依存を Result 型へ、`Point3d` を独自 struct へ置換 | 新規テスト pass かつ 275 を維持 | なし | 未着手 |
| **3. 部材間整合** | `CrossMemberValidator` 新設(§9 の単体チェックに対する横断チェック)。θ 付き前壁のタイロッド取付点補正 | 整合エラー/正常の両ケースが期待通り | 1・**6(新規)** | 未着手 |
| **4. Plugin** | XData 3 種、コマンド 12 個。前壁 → タイロッド → 控え杭の順 | スタブビルド成功 + XData 保存順/復元順の対応レビュー。実機確認は別途 | 1・2・4・5 | 未着手 |
| **5. 仕上げ** | Dynamo ノード、岸壁 1 施設分の統合積算、README(§7 の 9 章構成) | Dynamo の MultiReturn キー数一致 | 3 | 未着手 |

フェーズ 0〜2 は §12 の未解決事項の影響を受けない(未解決 5 件はいずれもコマンド名・XData・部材間参照方式に関わり、Core の数値ロジックには波及しないため)。

### 13.3 追加が必要なテスト(移植 275 件に対する新規 28 件)

| 対象 | ケース | 件数 | フェーズ |
|---|---|---|---|
| `PieceAssignment` | 1 本目/中間/最終本の継手有無、`pieceCount=1`、`pieceIndex` 範囲外 | 5 | 2 |
| `FrontWallPlacement` | θ=0/10/15° の挿入点変換、Z_tip 負値、平面位置の WCS 変換値、変換行列の往復一致 | 6 | 2 |
| `AnchorAlignment` | 直杭、前壁 θ>0 の傾斜杭、Z_tr 杭体範囲外、干渉 span、span 境界値(誤差 1 mm) | 6 | 2 |
| `CrossMemberValidator` | 径一致 / `pile_pitch` ⟺ 有効幅 B / `tie_elevation` ⟺ Z_tr / span 整合 の各正常・異常 | 8 | 3 |
| 単位変換 | mm→m 変換の境界(800→0.800)、mm 値の内部混入検出 | 3 | 2 |

「XData 位置追随(MOVE 後の再生成一致)」は AutoCAD ランタイムの挙動であり Core では検証できないため、§10 のテスト計画から外し、フェーズ 4 の実機手動検証項目とする。

### 13.4 移植の再現手順

移植は `scripts/port-from-legacy.sh` に集約した。移植元を `git show <commit>:<path>` で取り出すため、移植元リポジトリの作業ツリー状態に影響されない。冪等であり、再実行後に `git diff` が空であれば移植先は移植元と同期している。namespace の対応は次のとおり。

| 移植元 | 移植先 |
|---|---|
| `SteelPipeSheetPile.Data`(007@`b12b188`) | `SheetPileQuayWall.Core.FrontWall` |
| `TaiRod.Core`(008@`ff3a986`) | `SheetPileQuayWall.Core.TieRod` |
| `SteelPipeSheetPile.Tests` / `TaiRod.Core.Tests` | `SheetPileQuayWall.Core.Tests` |

`JointShapes.cs` は決定 3 のとおり内容を変更しないが、ヘッダの 2 行(009 に存在しない生成スクリプトへの参照と、紛らわしい旧リポジトリ名)のみスクリプトが移植元を指す記述に置換する。

## 14. 注意点

- DLL バージョンは 006/007/008 と同様に**未検証のまま**進める(開発機に AutoCAD 未インストール)。実装時はソース先頭コメントと出力冒頭に「未検証」を明記する(CLAUDE.PRIVATE.md §9.3)。
- Core 層のテストは WSL の .NET SDK で実行できる(§13.1)。Plugin 層は `-p:UseAutoCadStubs=true` による構文検証のみ可能で、実機動作確認はユーザー環境で行う。
- §12 の未解決事項は該当フェーズの着手前に確認する(フェーズ 0〜2 はブロックされない)。
- CLAUDE.PRIVATE.md 側の追随修正(①§1 由来表の 006 行を `006@6d6d8cf` 時点固定に更新、②§2.1 へ単位例外(決定 7)を明文化)は完了済み。
