# 009_sheet-pile-quaywall

**鋼管矢板式岸壁**(前壁鋼管矢板 + タイロッド + 控え杭)を、単一の DLL で完結してパラメトリック 3D モデル生成・積算(施工歩掛計算)できる AutoCAD 2025 / Civil 3D 2025 プラグイン。

- ターゲット: C# / .NET 8.0、`net8.0-windows`、x64、AutoCAD 2025 .NET / Civil 3D 2025 / Dynamo 3.3(Zero Touch Node)
- 構成: **Core(AutoCAD 非依存の計算層)/ Plugin(AutoCAD 依存層)** の 2 プロジェクト分割。Core は BCL のみを参照し、WSL / Linux でもテストできる(**xUnit 335 ケース green**)
- `006_steel-pipe-pile` / `007_steel-pipe-sheet-pile` / `008_tairod` の後継・統合版であり、**009 単独でビルド・実行できる**(3 リポジトリへのプロジェクト参照・アセンブリ参照は持たない)

設計判断の経緯(決定 1〜11)・フェーズ計画・実機検証項目は [`docs/implementation-plan.md`](docs/implementation-plan.md) を参照。

---

## 1. 概略図

### 側面(X–Z)

```
      海側 −X                     X = 0                               陸側 +X
                                    ┃
   杭頭標高 ┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┓┃
                                  ┃┃      腹起し(矢板半割部)   定着プレート・ワッシャー・ナット
   Z_tr ┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┃┣━━━━━━━━━━━━━━━━━━━━━━━━━━━━┫┓┄┄┄ タイロッド軸心
  (= H.W.L. + 0.5)                ┃┃                            ┃┃
   D.L.±0.000 ━━━━━━━━━━━━━━━━━━━━╋╋━━━━━━━━━━━━━━━━━━━━━━━━━━━━╋╋━━━ 基本水準面 Z = 0
                                  ┃┃                            ┃┃
                   前壁鋼管矢板 → ┃┃                            ┃┃ ← 控え杭
                   (傾斜角 θ 可)  ┃┃                            ┃┃   (傾斜角 θ_a 可)
                                  ┗┛                            ┗┛
   Z_tip ┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄ 杭先端(挿入点)
                                    │←────────── span ─────────→│
                                       (前壁矢板中心 〜 控え杭陸側定着面)
```

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

標高パラメータ(杭先端標高 Z_tip・タイロッド軸心標高 Z_tr 等)はすべて D.L. 基準の数値がそのまま Z 座標になる。

---

## 2. 参照アセンブリ

| アセンブリ | 用途 | 対象プロジェクト | Copy Local |
|---|---|---|---|
| `AcCoreMgd.dll` | AutoCAD コア | Plugin | `False` |
| `AcDbMgd.dll` | Database / Solid3d / XData | Plugin | `False` |
| `AcMgd.dll` | Application / Document | Plugin | `False` |
| `DynamoServices.dll` | `MultiReturn` 属性 | Plugin(`ExcludeDynamo=true` で除外可) | `False` |

**Core は BCL のみ**を参照し、上記を一切参照しない。`ProtoGeometry.dll` は参照しない(Dynamo ノードはジオメトリを扱わない純計算のため)。

> **参照 DLL バージョン: 未検証**
> 開発機に AutoCAD 2025 / Civil 3D 2025 が未インストールのため、実 DLL のバージョンを実測できていない(`scripts/verify-dll-versions.ps1` → exit 2)。バージョン不一致は `TypeLoadException` / `MissingMethodException` としてランタイムでのみ顕在化するため、**実機で同スクリプトが exit 0 になることを確認するまで配布しないこと**(CLAUDE.PRIVATE.md §9)。

---

## 3. AutoCAD / Civil 3D コマンド

全 **12 コマンド**。接頭辞は `SPQW`(Sheet Pile Quay Wall)。命名は次のパターンに従う。

| パターン | 役割 |
|---|---|
| `<STRUCT>_Create` | パラメータ入力 → Solid3d 生成 → XData 記録 |
| `<STRUCT>_Action` | 既存エンティティ選択 → パラメータ再入力 → 同位置再生成 |
| `<STRUCT>_Query` | XData 読取 → 諸元・派生量・積算数量を出力(図形は変更しない) |

### 前壁鋼管矢板(レイヤー「前壁鋼管矢板」)

| コマンド | 説明 |
|---|---|
| `SPQW_FRONTWALL_Create` | 対話入力 → Solid3d 生成(実形状継手 + 傾斜角対応)→ XData 記録 |
| `SPQW_FRONTWALL_Action` | 既存選択 → 諸元再入力 → 同位置に再生成(**MOVE 後は移動先に追随**) |
| `SPQW_FRONTWALL_Query` | 諸元・断面性能(K011)・継手要否・質量を出力 |
| `SPQW_FRONTWALL_Estimate` | 打設歩掛積算(貫入抵抗 R・ハンマ選定・打設日数・労務編成。積算基準 3-4.5) |

### タイロッド(レイヤー「タイ材」)

| コマンド | 説明 |
|---|---|
| `SPQW_TIEROD_Create` | **前壁選択** → 入力 → 組数分の Solid3d 生成。海側取付 X は前壁の傾斜角から自動計算 |
| `SPQW_TIEROD_Action` | 選択 1 本を、前壁の現在の位置・θ・Z_tip に基づき再計算して再生成(Y は保存値を保持) |
| `SPQW_TIEROD_Query` | 諸元・張力照査(鋼種・設計基準・荷重状態)・受杭数量を出力 |
| `SPQW_TIEROD_Color` | 色番号のみ変更 |

### 控え杭(レイヤー「控え杭」)

| コマンド | 説明 |
|---|---|
| `SPQW_ANCHORPILE_Create` | **前壁選択** → タイロッド軸線に整列した控え杭を生成 |
| `SPQW_ANCHORPILE_Action` | 前壁基準の整列位置に再生成(MOVE していても整列位置へ戻る) |
| `SPQW_ANCHORPILE_Query` | 諸元・整列座標・杭面間浄距離・積算数量(1 本あたり)を出力 |

### 施設全体

| コマンド | 説明 |
|---|---|
| `SPQW_QUAYWALL_Estimate` | 代表部材を選択し、岸壁 1 施設分の鋼材質量を 3 部材まとめて集計 |

### 基本ワークフロー

1. `SPQW_FRONTWALL_Create` — 矢板 1 本ごとに施工順位(`pieceIndex`)を指定して生成(継手の要否・雌雄は順位から自動判定)
2. `SPQW_TIEROD_Create` — 前壁を選択。海側取付点 X は前壁の θ・Z_tip から自動算出、位置は Y のみピック
3. `SPQW_ANCHORPILE_Create` — 前壁を選択。タイロッド軸線に整列した位置へ自動配置
4. `SPQW_QUAYWALL_Estimate` / `SPQW_FRONTWALL_Estimate` — 数量集計・打設歩掛積算

タイロッド・控え杭の入力時には前壁との整合(§8 の部材間チェック)を検査し、不一致はエラー停止する。

### XData 設計

| 部材 | RegApp 名 | 主なキー |
|---|---|---|
| 前壁 | `SPQW_FRONTWALL` | `fmt`, `outer_d`, `wall_t`, `length`, `joint`, `grade`, `incl_deg`, `piece_index`, `piece_count`, `color`, `tip_x`/`tip_y`/`tip_z` + **World 座標点(DxfCode 1011)** |
| タイロッド | `SPQW_TIEROD` | `fmt` + 008 の 18 項目 + `front_handle`, `pos_y`, `rod_index` |
| 控え杭 | `SPQW_ANCHORPILE` | `fmt`, `outer_d`, `wall_t`, `length`, `incl_deg`, `closed_tip`, `span`, `tie_elev`, `tip_elev`, `color`, `front_handle` |

- エンコードは **「キー=値」の ASCII 文字列 + 形式バージョン `fmt=1`**(008 方式)。数値は `InvariantCulture` で書式化する。
- **前壁の挿入点は 1011 を併記保存し、読み側は 1011 を優先**する。1011(World space position)は MOVE 等の変換に AutoCAD が自動追随させるため、MOVE 後の `_Action`・`_Query` は移動先の位置で動作する。`tip_x/_y/_z` は人間可読なフォールバック。
- **タイロッド・控え杭は平面 X を保存しない**。前壁 Handle(`front_handle`)と `span` / `tie_elev` から `_Action` のたびに再計算するため、前壁を MOVE したり傾斜角 θ を変更しても整列位置に追随する。

---

## 4. Dynamo ノード

Civil 3D 2025 同梱の Dynamo 3.3。カテゴリ `SheetPileQuayWall.Plugin > Dynamo`。戻り値は `[MultiReturn]` で辞書キーは日本語。

### `SpqwNodes.CalcSection`

前壁鋼管矢板の断面性能・有効幅・継手質量を返す。

```
 [Number] D_mm ──────┐
 [Number] t_mm ──────┤
 [Number] L_m  ──────┤── SpqwNodes.CalcSection ──┬── 断面積 A [cm2]
 [String] jointType ─┘                           ├── 断面係数 Z [cm3]
                                                 ├── 断面2次モーメント I [cm4]
                                                 ├── 単位重量 W [kg/m]
                                                 ├── 本管質量 [kg]
                                                 ├── 断面2次半径 i [cm]
                                                 ├── 内径 d [mm]
                                                 ├── 有効幅 B [mm]
                                                 └── 継手質量 (1接続) [kg/m]
```

| 入力 | 単位 | デフォルト値 |
|---|---|---|
| `D_mm` | mm | 800.0 |
| `t_mm` | mm | 12.0 |
| `L_m` | m | 20.0 |
| `jointType` | − | `"LT75"` |

### `SpqwNodes.CalcQuayWallQuantity`

岸壁 1 施設分の鋼材質量を集計する。

```
 [Number] frontD_mm ────────────┐
 [Number] frontT_mm ────────────┤
 [Number] frontL_m ─────────────┤
 [String] jointType ────────────┤
 [Number] frontPieceCount ──────┤
 [Number] tieRodSetCount ───────┤── SpqwNodes ──┬── 施設延長 [m]
 [Number] tieRodMassPerSet_kg ──┤ .CalcQuayWall ├── 継手接続数 [箇所]
 [Number] anchorPileCount ──────┤   Quantity    ├── 前壁 本管質量 [kg]
 [Number] anchorD_mm ───────────┤               ├── 前壁 継手質量 [kg]
 [Number] anchorT_mm ───────────┤               ├── タイロッド質量 [kg]
 [Number] anchorL_m ────────────┤               ├── 控え杭 質量 [kg]
 [Boolean] anchorClosedTip ─────┘               └── 合計質量 [kg]
```

| 入力 | 単位 | デフォルト値 |
|---|---|---|
| `frontD_mm` / `frontT_mm` / `frontL_m` | mm / mm / m | 800.0 / 12.0 / 20.0 |
| `jointType` | − | `"LT75"` |
| `frontPieceCount` | 本 | 10 |
| `tieRodSetCount` / `tieRodMassPerSet_kg` | 組 / kg | 5 / 150.0 |
| `anchorPileCount` | 本 | 5 |
| `anchorD_mm` / `anchorT_mm` / `anchorL_m` | mm / mm / m | 800.0 / 12.0 / 18.0 |
| `anchorClosedTip` | − | `false` |

ジオメトリ生成ノード(007 `SpspNodes.CreateSolid` 相当)は移植していない。AutoCAD のトランザクションを伴い実機でしか検証できないため(docs §12 項目 3 の決定)。

---

## 5. 入力パラメータ

**単位はメートル統一**。外径・肉厚のみ、対話プロンプトの表示・入力に限り mm 呼称を許容し、取得直後に m へ変換する(内部処理・XData・派生量はすべて m。決定 7)。

### 5.1 前壁鋼管矢板

| 英語名 | 日本語名 | 単位 | デフォルト値 | 範囲 |
|---|---|---|---|---|
| `outerDiameter` | 外径 D | mm(入力時呼称) | 800 | 500〜2000 |
| `wallThickness` | 肉厚 t | mm(入力時呼称) | 12 | 9〜25、かつ内径 > 0 |
| `length` | 全長 L | m | 20.0 | 1〜80 |
| `jointType` | 継手形式 | − | LT75 | LT65 / LT75 / LT100 / PP / PT |
| `grade` | 鋼種 | − | SKY400 | SKY400 / SKY490 |
| `inclinationDeg` | 傾斜角 θ | deg | 0.0 | 0〜15(Y 軸周り) |
| `pieceCount` | 総本数 | 本 | 1 | 1〜500 |
| `pieceIndex` | 施工順位 | 本目 | 1 | 1〜`pieceCount` |
| `colorIndex` | 本管の色 | ACI | 8 | 1〜255 |
| `tipElevation` | 杭先端標高 Z_tip | m(D.L.) | −18.0 | −80〜10 |
| `planPoint` | 平面位置 (X, Y) | m | − | UCS ピック → WCS 変換(Z は使わない) |

### 5.2 タイロッド

008 の 18 項目を踏襲。海側鋼管矢板径・矢板ピッチは選択した前壁から既定値を提示する。

| 英語名 | 日本語名 | 単位 | デフォルト値 | 範囲 |
|---|---|---|---|---|
| `frontWallSelection` | 基準とする前壁 | − | − | `SPQW_FRONTWALL` XData を持つ Solid3d |
| `steelGrade` | 鋼種 | − | HT690 | HT690 / HT740 / SS400 / SS490(部分係数法は HT690・SS400 のみ) |
| `designCode` | 設計基準 | − | PartialFactor | Allowable(許容応力度法)/ PartialFactor(部分係数法) |
| `loadState` | 荷重状態 | − | Normal | Normal(常時/永続)/ Seismic(地震時/変動) |
| `rodDiameter` | タイロッド径 | m | 0.048 | 0.020〜0.100(カタログ規格径 φ25〜φ90 の 19 種のみ可) |
| `spanLength` | 法線直角方向延長 span | m | 10.000 | 3.000〜40.000 |
| `pileDiameter` | 海側鋼管矢板径 | m | 前壁の外径 | 0.600〜1.600 |
| `pilePitch` | 鋼管矢板ピッチ | m | 前壁の有効幅 B | 0.600〜2.000 |
| `tieSpacing` | タイロッド取付間隔 | m | 2.400 | 0.600〜20.000、ピッチの整数倍 |
| `tieCount` | 組数 | 組 | 1 | 1〜200 |
| `hwl` | H.W.L. 標高 | m(D.L.) | 2.000 | 0.000〜5.000 |
| `tieElevation` | タイロッド軸心標高 | m(D.L.) | `hwl` + 0.500 | −5.000〜10.000 |
| `walingHeight` | 腹起し溝形鋼高さ h | m | 0.300 | 0 不可、≦ `pileDiameter` |
| `plateThickness` | 定着プレート厚 t2 | m | 0.025 | 0.001〜0.100 |
| `washerThickness` | 定着ワッシャー厚 t1 | m | 0.006 | 0.001〜0.100 |
| `nutHeight` | ナット高さ | m | 0.055 | 0.001〜0.200(φ38〜φ65 は積算基準表値を自動設定し入力省略) |
| `adjustLength` | 調節長 | m | 0.055 | 同上 |
| `anchorReaction` | 取付点反力 Ap | kN/m | 0.0 | 0〜10000。0 で張力照査なし |
| `layerColor` | 色 | ACI | 30 | 1〜255 |
| `positionY` | 1 組目の位置 Y | m | − | UCS ピック(**X は前壁から自動計算**。`_Action` では保存値を保持) |

`span_length` は「前壁矢板中心 〜 陸側定着面」の水平距離(積算基準 3-4.5-(13))。定着金物はこの面より陸側へ張り出す。

### 5.3 控え杭

| 英語名 | 日本語名 | 単位 | デフォルト値 | 範囲 |
|---|---|---|---|---|
| `frontWallSelection` | 基準とする前壁 | − | − | `SPQW_FRONTWALL` XData を持つ Solid3d |
| `outerDiameter` | 外径 D | mm(入力時呼称) | 800 | 318.5〜2500(JIS A 5525 標準径へスナップ) |
| `wallThickness` | 肉厚 t | mm(入力時呼称) | 12 | 外径別の K011 製造範囲 |
| `length` | 全長 L | m | 20.0 | 1〜80 |
| `inclinationDeg` | 傾斜角 θ | deg | 0.0 | 0〜15 |
| `closedTip` | 先端形状 | − | 開端 | 開端 / 閉端 |
| `span` | 法線直角方向延長 | m | 10.0 | 3.0〜40.0 |
| `tieElevation` | タイロッド軸心標高 Z_tr | m(D.L.) | 2.5 | −5.0〜10.0 |
| `tipElevation` | 杭先端標高 Z_tip | m(D.L.) | 前壁の Z_tip | −80〜10 |
| `colorIndex` | 本管の色 | ACI | 8 | 1〜255 |

---

## 6. 計算値(自動算出)

信頼度ラベル: **確定**(出典のある式)/ **概算** / **推定**(カタログ式が無く代替値)。

### 6.1 前壁

| 派生量 | 計算式・出典 | 信頼度 |
|---|---|---|
| 断面積 A / 断面 2 次モーメント I / 断面係数 Z / 単位重量 W / 断面 2 次半径 i | 日本製鉄 K011 | 確定 |
| 有効幅 B(= 矢板ピッチ) | B = D + 継手有効間隔 J(K011)。LT65/LT75 は √式、PP は J=0.2478、PT は J=0.180 | 確定 |
| 有効幅 B(LT100) | カタログ式が無く D + 0.100 | **推定** |
| 継手の要否・雌雄 | 施工順位から一意(`pieceIndex > 1` で −Y 側、`pieceIndex < pieceCount` で +Y 側) | 確定 |
| 継手質量(側別) | A 側(+Y): LT = 山形鋼×2 / PP・PT = 鋼管。B 側(−Y): LT・PT = T 形鋼 / PP = 鋼管 | 確定 |
| 杭頭標高 | Z_tip + L·cos θ | 確定 |
| 貫入抵抗 R / ハンマ規格 / 打撃速度 Sb / 打設時間 Tc / 日当り打設 Q / 労務編成 | 積算基準 3-4.5 | 確定 |

### 6.2 タイロッド

| 派生量 | 計算式・出典 | 信頼度 |
|---|---|---|
| 全長 | span + (t1 + t2 + ナット高さ + 調節長) × 2 + h(積算基準 3-4.5-(13)) | 確定 |
| 海側取付点 X | 前壁の杭先端 X + (`tie_elevation` − Z_tip)·tan θ | 確定 |
| 断面積 / 体積 / 質量 | カタログ規格径へスナップした呼び径による | 確定 |
| 本体本数 / ターンバックル / リングジョイント | 継手方法表 | 確定 |
| 受杭箇所数 | 積算基準 3-4.5-(14)。法線方向は「タイロッド 1 本おき」で組数の切上げ半数 | 確定 |
| 許容張力 / 張力照査 | 鋼種・設計基準・荷重状態による | 確定 |

### 6.3 控え杭・施設全体

| 派生量 | 計算式 | 信頼度 |
|---|---|---|
| 控え杭軸 X(Z_tr) | 前壁軸 X(Z_tr) + span − D_a/2 | 確定 |
| 杭先端(挿入点) | 控え杭軸 X(Z_tr) − (Z_tr − Z_tip)·tan θ_a、Y は前壁と同一 | 確定 |
| 軸間水平距離 | span − D_a/2 | 確定 |
| 杭面間浄距離 | 軸間水平距離 − D_f/2 − D_a/2(負値は干渉) | 確定 |
| 控え杭 本管質量(1 本) | K011 単位重量 × L | 確定 |
| 控え杭 閉端底板質量 | π/4 · D² · t · 7.85 g/cm³ | **概算** |
| 施設延長 | 有効幅 B × 前壁本数 | 確定 |
| 継手接続数 | 前壁本数 − 1 | 確定 |
| 前壁 継手金物質量(施設分) | 接続数 × 1 接続あたり側別質量 × 全長 | 確定 |

---

## 7. ビルド方法

Core 層は AutoCAD 非依存のため WSL / Linux でもビルド・テストできる。Plugin 層は AutoCAD が必要で、無い環境ではスタブで構文検証まで行う。

```bash
# Core + テスト(AutoCAD 不要。335 件が green であること)
dotnet test tests/SheetPileQuayWall.Core.Tests -c Release

# Plugin の構文検証(AutoCAD 不要。スタブとリンクする。配布不可)
dotnet build src/SheetPileQuayWall.Plugin/SheetPileQuayWall.Plugin.csproj -c Release -p:UseAutoCadStubs=true

# Dynamo 抜きで検証する場合
dotnet build src/SheetPileQuayWall.Plugin/SheetPileQuayWall.Plugin.csproj -c Release -p:UseAutoCadStubs=true -p:ExcludeDynamo=true

# Plugin の実機ビルド(AutoCAD 必須)
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/verify-dll-versions.ps1   # exit 0 を確認
dotnet build src/SheetPileQuayWall.Plugin/SheetPileQuayWall.Plugin.csproj -c Release
```

AutoCAD が既定パス以外にある場合は `-p:AcadRoot="..."` を指定する。

### プロジェクト構成

```
009_sheet-pile-quaywall/
├── src/
│   ├── SheetPileQuayWall.Core/          AutoCAD 非依存の計算層(BCL のみ参照)
│   │   ├── Point3.cs / PileGeometry.cs / FrontWallRef.cs
│   │   ├── CrossMemberValidator.cs      部材間整合チェック(§8 の 4 組)
│   │   ├── QuayWallEstimate.cs          施設 1 件分の数量集計
│   │   ├── FrontWall/                   007 移植 8 + 新規 3(PieceAssignment・FrontWallPlacement・JointMass)
│   │   ├── TieRod/                      008 移植 5 + 新規 1(TieRodPlacement)
│   │   └── AnchorPile/                  006@6d6d8cf 由来(書き直し)4 ファイル
│   └── SheetPileQuayWall.Plugin/        AutoCAD 依存層
│       ├── Commands/                    SPQW_* 12 コマンド(部材別 4 ファイル)
│       ├── XData/                       XDataStore(キー=値 + 1011)+ 3 部材のレコード
│       ├── Dynamo/SpqwNodes.cs          Zero Touch Node 2 個
│       └── DrawingHelper.cs / Prompt.cs / SolidBuilder.cs
├── stubs/                               AutoCAD API スタブ(構文検証専用。配布禁止)
├── tests/SheetPileQuayWall.Core.Tests/  xUnit 335 ケース + fixtures/
├── scripts/
│   ├── port-from-legacy.sh              007/008 からの冪等移植
│   └── verify-dll-versions.ps1          参照 DLL バージョン実測(CLAUDE.PRIVATE.md §9)
└── docs/implementation-plan.md          設計決定 1〜11・フェーズ計画・実機検証項目(§13.5)
```

### レガシー 3 リポジトリからの移植

Core の一部は 006/007/008 から移植したもので、`scripts/port-from-legacy.sh` が再現する。移植元は `git show <commit>:<path>` で取り出すためレガシー側の作業ツリー状態に影響されず、冪等(再実行後に `git diff` が空なら同期済み)。

| 移植元 | 移植先 namespace |
|---|---|
| `SteelPipeSheetPile.Data`(007@`b12b188`) | `SheetPileQuayWall.Core.FrontWall` |
| `TaiRod.Core`(008@`ff3a986`) | `SheetPileQuayWall.Core.TieRod` |
| `006@6d6d8cf` の継手判定・整列計算 | 手作業で抽出・書き直し(スクリプト対象外) |

---

## 8. 規約・制約

- **`using` ディレクティブを使わない**。型は完全修飾名で書く(暗黙 using も無効化)。
- **単位はメートル統一**。mm は対話プロンプト・Dynamo 入力の呼称のみで、取得直後に m へ変換する。
- **Z 軸は上向き、Z = 0 が D.L.**。下向き座標は使わない。
- 部材 1 本につき **Solid3d 1 個**に集約する(`BoolUnite` / `BoolSubtract`)。
- 参照 DLL は `<Private>False</Private>`(Copy Local = False)。AutoCAD 本体 DLL を配布物に同梱しない。
- **006 / 007 / 008 へのプロジェクト参照・アセンブリ参照を追加しない**。共通ロジックは 009 内にコードとして移植する。
- 整合性チェックの誤差許容は **1 mm = 0.001 m**。不一致時はエラー停止し、自動補正も再生成もしない(外径の JIS / カタログスナップのみ例外)。
- 旧 RegApp(`STEELPIPEPILE` / `SPSP` / `TAIROD_PARAM` / `ANCHORPILE`)で作成した既存図面との**互換は持たない**。旧図面は旧プラグインで扱うか 009 で再作成する。

### 部材間の整合性チェック

統合版として、同じ量を 2 部材が別々に入力している箇所を突き合わせる(`CrossMemberValidator`)。

| # | 突き合わせ |
|---|---|
| 1 | タイロッドの海側鋼管矢板径 ⟺ 前壁の外径 |
| 2 | タイロッドの矢板ピッチ ⟺ 前壁の有効幅 B |
| 3 | タイロッドの軸心標高 ⟺ 控え杭の Z_tr |
| 4 | タイロッドの `span_length` ⟺ 控え杭の `span` |

---

## 9. 既知の課題

- **実機動作確認は未実施**。開発機に AutoCAD 2025 が無いため、Plugin 層はスタブによる構文・型検証までしか行えていない。実機での検証項目は `docs/implementation-plan.md` §13.5 を参照。
- **移植元 007 の継手質量に不整合**。`JointCatalog.JointMassPerM` は P-P 形で鋼管を 1 本分(34.7 kg/m)しか数えないが、`JointShapes` の実形状は両側とも鋼管であり、正しくは 69.4 kg/m。009 では `JointMass`(側別質量)を新設して積算に使っており、移植元のファイルは変更していない(`port-from-legacy.sh` の再実行で失われるため)。007 側の修正は別途必要。
- **前壁の壁一括生成が無い**。矢板 1 本ごとに平面位置をピックする方式(006 / 007 から踏襲)のため、100 本の壁では 100 回の操作になる。ピッチ B での自動配置は未実装。
- **前壁と控え杭で外径の規則が異なる**。前壁は K011(D 0.500〜2.000 m、肉厚一律、スナップなし)、控え杭は JIS A 5525(D 0.3185〜2.500 m、径別肉厚範囲、スナップあり)。控え杭は継手を持たない単独杭のため規則が違うこと自体は妥当だが、同一図面内で非対称になる。
- **工種体系へのマッピングは未実装**。`SPQW_QUAYWALL_Estimate` は鋼材質量の集計までで、『港湾工事工種体系ツリー.md』のレベル体系への対応付けは行っていない。

---

## 10. 旧版 README からの変更点

全面書き換え(CLAUDE.PRIVATE.md §7 の 9 項目構成は維持)。記載している数値・仕様は旧版(コミット `db2d478` 時点)と同一で、変更はすべて構成の再編と増補。

- **§1 概略図**: 側面図に腹起し・定着金物・傾斜角の位置関係を追記し、平面図を「タイロッド + 控え杭の整列」が読み取れる形に描き直した。座標系を表形式で明示。
- **§3 コマンド**: コマンド命名パターン(`_Create` / `_Action` / `_Query`)、**基本ワークフロー**(4 ステップ)、**XData 設計**(RegApp 一覧・キー=値 + `fmt`・1011 併記・位置情報の持ち方)の各節を新設。旧版の「運用上の注意」の内容は XData 設計節に統合。
- **§5.2**: タイロッド径の範囲に「φ25〜φ90 の 19 種」、取付間隔・取付点反力の数値範囲を明記(コードの実挙動どおり。仕様変更なし)。
- **§6.3**: 控え杭の本管質量(1 本あたり)の行を追加(`SPQW_ANCHORPILE_Query` の出力に対応)。
- **§7 ビルド方法**: **プロジェクト構成**(ディレクトリツリーと各層の役割)を新設。
- **§9 既知の課題**: 変更なし(章番号も docs からの参照を維持するため据え置き)。
- **§10(本節)**: 新設(§7 の項目 9「既存の README.md が存在する場合、変更点を示す」に対応)。
