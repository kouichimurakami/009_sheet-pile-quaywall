# 009_sheet-pile-quaywall

**鋼管矢板式岸壁**(前壁鋼管矢板 + タイロッド + 控え杭)を、単一の DLL で完結してパラメトリック 3D モデル生成・積算(施工歩掛計算)できる AutoCAD 2025 / Civil 3D 2025 プラグイン。

> ⚠️ **実機動作は未検証。**何が検証済みで何が未検証かは [§9.5 検証状態マトリクス](#95-検証状態マトリクス)にまとめてある。使用前に必ず読むこと。

| 項目 | 内容 |
|---|---|
| 対象構造物 | 前壁鋼管矢板(直杭)+ タイロッド + 控え杭(傾斜可) |
| 環境 | C# / .NET 8.0(`net8.0-windows`、x64)/ AutoCAD 2025 .NET / Civil 3D 2025 / Dynamo 3.3 |
| 構成 | Core(計算層)/ Plugin(AutoCAD 層)/ Dynamo(Zero Touch Node 層)の 3 プロジェクト |
| 規模 | AutoCAD コマンド **19** / Dynamo ノード **10** / Core テスト **678 件 green** |
| 由来 | `006_steel-pipe-pile` / `007_steel-pipe-sheet-pile` / `008_tairod` の後継・統合版。**009 単独でビルド・実行できる** |

関連ドキュメント:

| ファイル | 内容 |
|---|---|
| [`docs/implementation-plan.md`](docs/implementation-plan.md) | 設計決定 1〜11・フェーズ計画・実機検証項目 |
| [`docs/features.html`](docs/features.html) | 機能概要(図表中心。ブラウザで開く) |
| [`docs/references/アライズ計算書-009パラメータ対応表.md`](docs/references/) | 設計計算書のセル参照と 009 パラメータの対応 |

---

## 目次

| 章 | 内容 |
|---|---|
| [1. 概略図](#1-概略図) | データフロー・断面図・座標系 |
| [2. 参照アセンブリ](#2-参照アセンブリ) | DLL 一覧・プロジェクト依存関係 |
| [3. AutoCAD / Civil 3D コマンド](#3-autocad--civil-3d-コマンド) | 19 コマンド・ワークフロー・打設工法の選択・XData |
| [4. Dynamo ノード](#4-dynamo-ノード) | 10 ノード・入力 3 経路・グラフ配線 |
| [5. 入力パラメータ](#5-入力パラメータ) | 部材別の入力表・CSV / JSON 形式 |
| [6. 計算値(自動算出)](#6-計算値自動算出) | 派生量と信頼度ラベル |
| [7. ビルド方法](#7-ビルド方法) | ビルド・登録・トラブルシュート |
| [8. 規約・制約](#8-規約制約) | **単位・座標系・整合性の規則(唯一の定義箇所)** |
| [9. 注意点・既知の課題](#9-注意点既知の課題) | 復元限界・実装範囲・検証状態 |
| [10. 設計変更の経緯](#10-設計変更の経緯) | アーキテクチャ上の主要な決定 |
| [11. 本 README の変更点](#11-本-readme-の変更点) | 旧版からの差分 |

---

## 1. 概略図

### 1.1 データフロー(全体像)

入力は 3 経路あり、いずれも Core(AutoCAD 非依存の計算層)を通る。

```
  入力                          処理                        出力
────────────────────────────────────────────────────────────────────────
 ① 対話入力
   SPQW_*_Create ────────┐
   (AutoCAD コマンドライン)│
                          │    ┌────────────────────┐   ┌──────────────────────┐
 ② 帳票 CSV               ├───▶│ SheetPileQuayWall  │──▶│ Solid3d + XData      │
   SPQW_*_ImportCsv ──────┤    │ .Core              │   │ (AutoCAD、_Action可) │
   (積算ソフト出力)        │    │                    │   └──────────────────────┘
                          │    │ AutoCAD 非依存      │   ┌──────────────────────┐
 ③ 設計計算書 JSON         │    │ BCL のみ参照        │──▶│ 諸元・数量・歩掛      │
   arise_design_input ────┤    │ 678 テスト green    │   │ (コマンドライン出力)  │
   (Dynamo 標準ノード)     │    └────────────────────┘   └──────────────────────┘
                          │                              ┌──────────────────────┐
 ④ 柱状図 CSV             ─┘                          ──▶│ Geometry(Dynamo 焼込)│
   CalcWeightedN                                          │ ※ XData を持たない   │
                                                          └──────────────────────┘
```

### 1.2 側面(X–Z)

```
      海側 −X                     X = 0                               陸側 +X
                                    ┃
   Z_head ┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┓┃   ← 前壁の入力・内部表現の基準点
                                  ┃┃      腹起し(矢板半割部)   定着プレート・ワッシャー・ナット
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

**標高の基準が部材ごとに違う**点が設計上の要点。

| 部材 | 内部表現の基準 | 理由 |
|---|---|---|
| 前壁 | **杭上端 Z_head** | 直杭のみのため `Z_tip = Z_head − L` の単純計算。現場で測りやすい杭頭側に揃えた |
| 控え杭 | 杭先端 Z_tip | 傾斜角 θ_a を持つため、変換に全長・傾斜角が要る |
| タイロッド | 軸心 Z_tr | 標高そのものが設計値 |

### 1.3 平面(X–Y)

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

### 1.4 座標系

```
          +Z(鉛直上向き)
           ↑
           │        Z = 0 ── D.L.(基本水準面)。3 部材共通の原点
           │
   海側 ───┼───▶ +X(陸側)
        −X │
           │
           └──▶ +Y(施設延長方向)
```

| 軸 | 方向 | 備考 |
|---|---|---|
| X | 陸側 → +X、海側 → −X | 法線直角方向 |
| Y | 施設延長方向 | 法線平行方向 |
| Z | 鉛直上向き | **Z = 0 を D.L.(基本水準面)に統一** |

標高パラメータ(Z_head / Z_tip / Z_tr 等)はすべて D.L. 基準の数値がそのまま Z 座標になる。単位・座標系の規則は [§8](#8-規約制約) に一元化してある。

---

## 2. 参照アセンブリ

### 2.1 プロジェクト依存関係

```
              ┌───────────────────────────────────┐
              │  SheetPileQuayWall.Core           │  参照: BCL のみ
              │  計算層(AutoCAD 非依存)           │  → WSL / Linux でテスト可
              │  678 テスト green                  │
              └──────┬──────────────────┬─────────┘
                     │                  │
        ┌────────────▼───────┐   ┌──────▼──────────────────────┐
        │ .Plugin            │   │ .Dynamo                     │
        │ AcCoreMgd / AcDbMgd│   │ DynamoServices              │
        │ AcMgd              │   │ ProtoGeometry               │
        │                    │   │ ※ AutoCAD 本体 DLL を参照   │
        │                    │   │    しない(§10 の 4)         │
        └────────┬───────────┘   └──────┬──────────────────────┘
                 │                      │
            NETLOAD で登録         Import Library で登録
            SPQW_* 19 コマンド     SpqwNodes / SpqwGeometryNodes 10 ノード
```

### 2.2 参照 DLL

| アセンブリ | 用途 | 対象プロジェクト | Copy Local |
|---|---|---|---|
| `AcCoreMgd.dll` | AutoCAD コア | Plugin | `False` |
| `AcDbMgd.dll` | Database / Solid3d / XData | Plugin | `False` |
| `AcMgd.dll` | Application / Document | Plugin | `False` |
| `DynamoServices.dll` | `MultiReturn` 属性 | Dynamo | `False` |
| `ProtoGeometry.dll` | Dynamo ネイティブジオメトリ | Dynamo | `False` |

**参照 DLL バージョンは未検証**(開発機に AutoCAD が無い)。`scripts/verify-dll-versions.ps1` が実機で exit 0 になるまで配布しないこと(CLAUDE.PRIVATE.md §9)。

---

## 3. AutoCAD / Civil 3D コマンド

全 **19 コマンド**。接頭辞は `SPQW`(Sheet Pile Quay Wall)。

| パターン | 役割 |
|---|---|
| `<STRUCT>_Create` | パラメータ入力 → Solid3d 生成 → XData 記録 |
| `<STRUCT>_Action` | 既存エンティティ選択 → パラメータ再入力 → 同位置再生成 |
| `<STRUCT>_Query` | XData 読取 → 諸元・派生量・積算数量を出力(図形は変更しない) |
| `<STRUCT>_Estimate` | 施工歩掛積算(図形は変更しない) |
| `<STRUCT>_ImportCsv` | 帳票 CSV から一括生成 |

### 3.1 コマンド一覧

**前壁鋼管矢板**(レイヤー「前壁鋼管矢板」)

| コマンド | 説明 |
|---|---|
| `SPQW_FRONTWALL_Create` | **施設全長と有効幅 B から本数を自動算出**し、始点から +Y 方向へ壁を一括生成(実形状継手。直杭のみ) |
| `SPQW_FRONTWALL_Action` | 既存選択 → 諸元再入力 → 同位置に再生成(**MOVE 後は移動先に追随**) |
| `SPQW_FRONTWALL_Query` | 諸元・断面性能(K011)・継手要否・質量を出力 |
| `SPQW_FRONTWALL_Estimate` | **打撃工法**の打設歩掛積算(4節 3-4.5) |
| `SPQW_FRONTWALL_VibroEstimate` | **振動工法・バイブロ単独**の打設歩掛積算(16節 3-2、海上打設のみ) |
| `SPQW_FRONTWALL_VibroJetEstimate` | **振動工法・ジェット併用**の打設歩掛積算(16節 3-1、陸上/海上とも) |
| `SPQW_FRONTWALL_ImportCsv` | CSV 帳票 → 前壁を一括生成 |

**タイロッド**(レイヤー「タイ材」)

| コマンド | 説明 |
|---|---|
| `SPQW_TIEROD_Create` | **前壁は自動選択**(図面内で杭中心 Y が最小の矢板 = 壁の 1 本目)→ 組数分の Solid3d 生成。矢板径・ピッチ・海側取付 X は前壁から自動代入 |
| `SPQW_TIEROD_Action` | 選択 1 本を前壁の現在位置に基づき再生成(Y は保存値を保持) |
| `SPQW_TIEROD_Query` | 諸元・張力照査・受杭数量を出力 |
| `SPQW_TIEROD_Color` | 色番号のみ変更 |
| `SPQW_TIEROD_ImportCsv` | **前壁選択** → CSV 帳票 → 一括生成 |

**控え杭**(レイヤー「控え杭」)

| コマンド | 説明 |
|---|---|
| `SPQW_ANCHORPILE_Create` | **タイロッド・前壁とも自動選択**(位置 Y が最小のタイロッド → その参照先の前壁)→ 配置間隔・本数・軸心標高を自動設定して一括生成 |
| `SPQW_ANCHORPILE_Action` | 前壁基準の整列位置に再生成(MOVE していても整列位置へ戻る) |
| `SPQW_ANCHORPILE_Query` | 諸元・整列座標・杭面間浄距離・積算数量を出力 |
| `SPQW_ANCHORPILE_Estimate` | **打撃工法**の打設歩掛積算(4節 3-4.6、**陸上打設のみ**) |
| `SPQW_ANCHORPILE_ImportCsv` | **前壁選択** → CSV 帳票 → 一括生成 |

**施設全体**

| コマンド | 説明 |
|---|---|
| `SPQW_QUAYWALL_Estimate` | 代表部材を選択し、岸壁 1 施設分の鋼材質量を 3 部材まとめて集計 |
| `SPQW_QUAYWALL_ReconcileCsv` | 帳票の数量・質量と 009 の計算値を突合し、許容誤差(既定 1%)超過を検出 |

### 3.2 生成コマンドの依存関係

**後段のコマンドが前段の XData を読む**ため、この順序で実行する。**②③は基準部材の選択操作が無い**(2026-07-31。図面を走査して自動で決める)。

```
 ① SPQW_FRONTWALL_Create
      施設全長 ÷ 有効幅 B → 本数を自動算出、+Y へ一括生成
      XData: outer_d, effective_width, piece_count, head_x/_y/_z …
                │
                │ ▼自動選択: 杭中心 Y が最小の矢板(壁の 1 本目)
                │   └─ 外径・有効幅 B・総本数を自動代入
                ▼
 ② SPQW_TIEROD_Create
      「矢板何本ごと」の整数入力のみ。取付間隔 = B × n、組数は総本数から自動算定
      XData: tie_spacing, tie_count, tie_elev, front_handle, pos_y …
                │
                │ ▼自動選択: 位置 Y が最小のタイロッド(1 組目)
                │   └─ 間隔・本数・軸心標高を自動代入
                │ ▼自動解決: 前壁 = そのタイロッドの front_handle の参照先
                ▼
 ③ SPQW_ANCHORPILE_Create
      天端 = タイ材中心 + 0.5 m(既定値。変更可)
      位置 Y は図面内の全前壁のうち最小 Y(壁の 1 本目)に自動整列
      XData: span, tie_elev, tip_elev, front_handle, pos_y …
                │
                ▼
 ④ SPQW_QUAYWALL_Estimate ＋ 打設歩掛積算コマンド 1 つ(§3.3 で選ぶ)
```

各段で前壁との整合([§8.2](#82-部材間の整合性チェック))を検査し、不一致はエラー停止する。

自動選択で対象が見つからない場合はエラー停止する(②は前壁が 1 本も無いとき、③はタイロッドが 1 組も無いとき)。③でタイロッドの `front_handle` が失効している図面(前壁を削除した等)に限り、`_Action` と同じく前壁の選択にフォールバックする。

### 3.3 打設工法の選択

3 つの打設積算コマンドは積算基準上の**別節・別歩掛**であり、規格選定の基礎も労務編成も基準作業能力係数も異なる。**混用してはならない。**

```
                  対象部材は?
                       │
        ┌──────────────┴──────────────┐
     控え杭                         前壁
        │                             │
        ▼                             ▼
 SPQW_ANCHORPILE_Estimate      振動への配慮が必要?
 (4節 3-4.6、陸上のみ)              │
                          ┌─────────┴─────────┐
                        YES                   NO
                          │                    │
                          ▼                    ▼
                  VibroJetEstimate    支持層へ打込む/中間層を打抜く?
                  (ジェット併用)              │
                                     ┌───────┴───────┐
                                   YES              NO
                                     │               │
                                     ▼               ▼
                        バイブロ単独は標準適用外   海上打設?
                        → 打撃 or ジェット併用      │
                                            ┌──────┴──────┐
                                           NO            YES
                                            │             │
                                            ▼             ▼
                                   FRONTWALL_Estimate  騒音・油飛散への配慮?
                                   (打撃・陸上)         YES→ VibroEstimate
                                                       NO → Estimate(打撃)
```

| 工法 | コマンド | 出典 | 規格選定の基礎 | 施工区分 | ei |
|---|---|---|---|---|---|
| 打撃(ディーゼル/油圧ハンマ) | `..._Estimate` | 4節 3-4.5 | 鋼材質量 + 貫入抵抗値 R | 陸上 / 海上 | 陸 0.90 / 海 0.50 |
| 振動・バイブロ単独 | `..._VibroEstimate` | 16節 3-2 | 鋼材質量 + R(継手 Rj を加算) | **海上のみ** | 海 0.70 |
| 振動・ジェット併用 | `..._VibroJetEstimate` | 16節 3-1 | **必要偏心モーメント K₀** | 陸上 / 海上 | 陸 0.80 / 海 0.70 |

基準の適用工法表(3-1-3)による標準適用の目安:

| 条件 | ディーゼル | 油圧 | バイブロ | ジェット併用 |
|---|---|---|---|---|
| 騒音への配慮が必要 | − | ○ | ○ | ○ |
| 振動への配慮が必要 | − | − | − | ○ |
| 油飛散等への配慮が必要 | − | ○ | ○ | ○ |
| 支持層へ打込む/中間層を打抜く | ○ | ○ | **−** | ○ |

ジェット併用の適用範囲は外径 1,500 mm 以下・全長 40 m 以下(3-1-3 注3)。4節 3-4.5 には「バイブロハンマによる場合は現場条件により『16節 仮設工』を適用できる」という注記があり、本体工の前壁に 16節の振動工法歩掛を適用することは基準自身が認めている。

**控え杭は前壁と節が違う。** 継手を持たない単独の鋼管杭であるため 4節 **3-4.6**(鋼杭式)に基づく。貫入抵抗値 R・ハンマ規格決定図・打撃速度 Sb 表・溶接時間表・準備時間 Tp・基準作業能力係数は 3-4.5 と数値まで完全一致するため共有実装を再利用しているが、**1 本当り打撃時間 Tb の係数 K が異なる**(3-4.5 は直杭 K=1.0 のみ、3-4.6 は直杭 1.0・斜杭 1.2)。控え杭は傾斜角を持つため斜杭補正が必須で、`AnchorPile.AnchorDriveEstimate` に新規実装した。

### 3.4 XData 設計

```
   Solid3d(前壁)                     Solid3d(タイロッド)        Solid3d(控え杭)
   ┌───────────────────┐              ┌──────────────────┐      ┌──────────────────┐
   │ RegApp:           │              │ RegApp:          │      │ RegApp:          │
   │  SPQW_FRONTWALL   │◀──front_handle──  SPQW_TIEROD   │      │ SPQW_ANCHORPILE  │
   │                   │◀────────────────────front_handle────────┤                  │
   │ head_x/_y/_z      │              │ pos_y            │      │ pos_y            │
   │ + DxfCode 1011 ★  │              │ ※ X は保存しない  │      │ ※ X は保存しない  │
   └───────────────────┘              └──────────────────┘      └──────────────────┘
     ★ MOVE に AutoCAD が                 _Action のたびに前壁 Handle + span から
       自動追随。読み側は 1011 優先         X を再計算 → 前壁を動かしても整列に追随
```

| 部材 | RegApp 名 | 主なキー |
|---|---|---|
| 前壁 | `SPQW_FRONTWALL` | `fmt`, `outer_d`, `wall_t`, `length`, `joint`, `grade`, `incl_deg`, `piece_index`, `piece_count`, `effective_width`, `color`, `head_x`/`head_y`/`head_z` + **World 座標点(DxfCode 1011)** |
| タイロッド | `SPQW_TIEROD` | `fmt` + 008 の 18 項目 + `front_handle`, `pos_y`, `rod_index` |
| 控え杭 | `SPQW_ANCHORPILE` | `fmt`, `outer_d`, `wall_t`, `length`, `incl_deg`, `closed_tip`, `span`, `tie_elev`, `tip_elev`, `color`, `pos_y`, `front_handle` |

- エンコードは **「キー=値」の ASCII 文字列 + 形式バージョン `fmt=1`**(008 方式)。数値は `InvariantCulture` で書式化する
- **前壁の内部表現は杭上端標高 Z_head 基準**。杭先端 Z_tip は `PileGeometry.TipFromHead` によるソリッド生成・表示専用の計算値
- **旧図面(`tip_x/_y/_z` のみ)は読み込み時に全長・傾斜角から自動変換する**
- 控え杭の `pos_y` は後から追加したキーのため、欠落する旧図面は `_Action` 時に前壁の Y へ整列させる(その旨を表示する)
- 積算コマンドは XData を書き換えない(図形・パラメータとも不変)

---

## 4. Dynamo ノード

Civil 3D 2025 同梱の Dynamo 3.3 で使う Zero Touch Node。**`SheetPileQuayWall.Dynamo.dll`** という独立した DLL に実装されており、AutoCAD コマンドの `SheetPileQuayWall.Plugin.dll` とは別に Import Library する([§7.4](#74-dynamo-への登録import-library))。

| クラス | ノード数 | 性質 | 節 |
|---|---|---|---|
| `SpqwNodes` | 7 | 純計算(ジオメトリを扱わない) | §4.2〜4.5 |
| `SpqwGeometryNodes` | 3 | ソリッド生成(実験的・未検証) | §4.6 |

### 4.1 共通仕様

| 項目 | 仕様 |
|---|---|
| カテゴリ | ノード検索で `SpqwNodes.` / `SpqwGeometryNodes.` と入力すると `SheetPileQuayWall.Dynamo` 配下に表示される |
| 入力 | メソッドの各引数がそのまま入力ポート。**未配線のポートは既定値で実行される**(例外: `CalcWeightedN` はパス必須) |
| 出力 | `[MultiReturn]` の辞書で、**日本語の辞書キーがそのまま出力ポート名**になる |
| list-level 自動反復 | 入力にリストを渡すと 1 要素ずつ自動で呼び出し、結果をリストで返す |
| 単位の境界 | 入力は mm 呼称(`D_mm` 等)で受け、ノード内部で直ちに m へ変換する([§8.1](#81-単位)) |
| エラー動作 | 入力不正・規格表範囲外はすべて `ArgumentException` を投げてノードを警告状態(黄色)にする |
| XData を経由しない | AutoCAD コマンドが XData から読む値も、ノードでは明示的な引数として受け取る |

### 4.2 ノード一覧

| ノード | 内容 | 入力数 | 出力数 |
|---|---|---|---|
| `CalcSection` | 前壁 1 本分の断面性能(K011)・有効幅・継手質量 | 4 | 9 |
| `CalcQuayWallQuantity` | 施設 1 件分の鋼材質量集計(図面不要の概算) | 12 | 7 |
| `CalcWeightedN` | 柱状図 CSV から加重平均N値・一軸圧縮強度 | 1 | 10 |
| `CalcFrontWallDriveEstimate` | 前壁・打撃工法の打設歩掛(4節 3-4.5) | 13 | 23 |
| `CalcVibroEstimate` | 前壁・バイブロ単独の打設歩掛(16節 3-2) | 13 | 26 |
| `CalcVibroJetEstimate` | 前壁・ジェット併用の打設歩掛(16節 3-1) | 26 | 36 |
| `CalcAnchorPileDriveEstimate` | 控え杭・打撃工法の打設歩掛(4節 3-4.6) | 10 | 18 |
| `CreateAnchorPileSolid` | 控え杭ソリッド(本管 + 閉端時は底板、傾斜角対応) | 6 | 1 |
| `CreateFrontWallPileSolid` | 前壁本体円筒(θ=0 固定、継手なし) | 4 | 1 |
| `CreateTieRodSolid` | タイロッド(組数ぶんの配列を返す) | 10 | N |

### 4.3 `CalcSection` — 前壁の断面性能

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

| 英語名 | 日本語名 | 単位 | 既定値 | 範囲 |
|---|---|---|---|---|
| `D_mm` | 外径 D | mm | 800.0 | 500〜2000(K011 製造範囲) |
| `t_mm` | 肉厚 t | mm | 12.0 | 9〜25、かつ内径 > 1 mm |
| `L_m` | 全長 L | m | 20.0 | 1〜80 |
| `jointType` | 継手形式 | − | `"LT75"` | `LT65`/`LT75`/`LT100`/`PP`/`PT` |

出力の信頼度はすべて**確定**(K011 出典)。ただし有効幅 B は LT100 のみ**推定**([§9.1](#91-積算基準データの復元限界要原本確認) の 4)。

**候補径の比較検討**(list-level 自動反復。`Code Block` に `D_mm = {700,800,900,1000,1100};` と書く):

| D [mm] | A [cm²] | W [kg/m] | 本管質量 [kg] | i [cm] | B [mm] |
|---|---|---|---|---|---|
| 700 | 259.37 | 203.59 | 4,072 | 24.33 | 773.7 |
| 800 | 297.07 | 233.18 | 4,664 | 27.86 | 875.2 |
| 900 | 334.77 | 262.78 | 5,256 | 31.40 | 976.4 |
| 1000 | 372.47 | 292.37 | 5,847 | 34.93 | 1,077.3 |
| 1100 | 410.17 | 321.96 | 6,439 | 38.47 | 1,178.1 |

継手質量(1 接続あたり 32.60 kg/m、LT75)は外径に依らず一定。

### 4.4 `CalcQuayWallQuantity` — 施設 1 件分の質量集計

図面がまだ無く諸元だけが決まっている検討段階で、AutoCAD を開かずに概算する。

| 英語名 | 日本語名 | 単位 | 既定値 |
|---|---|---|---|
| `frontD_mm` / `frontT_mm` / `frontL_m` | 前壁 外径 / 肉厚 / 全長 | mm / mm / m | 800.0 / 12.0 / 20.0 |
| `jointType` | 前壁 継手形式 | − | `"LT75"` |
| `frontPieceCount` | 前壁 総本数 | 本 | 10 |
| `tieRodSetCount` / `tieRodMassPerSet_kg` | タイロッド 組数 / 1 組当り質量 | 組 / kg | 5 / 150.0 |
| `anchorPileCount` | 控え杭 本数 | 本 | 5 |
| `anchorD_mm` / `anchorT_mm` / `anchorL_m` | 控え杭 外径 / 肉厚 / 全長 | mm / mm / m | 800.0 / 12.0 / 18.0 |
| `anchorClosedTip` | 控え杭 先端形状 | − | `false`(開端) |

> **範囲チェックを行わない**(継手コードのみ例外を投げる)。検討段階の道具という位置づけのため製造範囲外の値もそのまま計算される。確定諸元は §5 の範囲に収めること。

既定値のままの出力:

| 項目 | 値 | 検算 |
|---|---|---|
| 施設延長 [m] | 8.752 | 有効幅 875.2 mm × 10 本 |
| 継手接続数 [箇所] | 9 | 総本数 10 − 1 |
| 前壁 本管質量 [kg] | 46,637 | |
| 前壁 継手質量 [kg] | 5,868 | |
| タイロッド質量 [kg] | 750 | |
| 控え杭 質量 [kg] | 20,987 | |
| **合計質量 [kg]** | **74,242** | |

### 4.5 `CalcWeightedN` — 柱状図から加重平均N値

柱状図 CSV([§5.6](#56-柱状図-csv))から、打設歩掛積算コマンドが尋ねる **加重平均N値**(R 用・Sb 用・土質区分別)と岩盤層の**加重平均一軸圧縮強度**を算出する。

```
 [File Path] csvPath ── SpqwNodes.CalcWeightedN ──┬── 加重平均N値 (R用、N=0連続除外)
                                                  ├── 根入れ長 (R用) [m]
                                                  ├── 加重平均N値 (Sb用、N≦5連続除外)
                                                  ├── 根入れ長 (Sb用) [m]
                                                  ├── 加重平均N値 (砂質土等/粘性土/
                                                  │              玉石混りレキ/固結土)
                                                  ├── 加重平均一軸圧縮強度 (岩盤) [N/mm2]
                                                  └── 岩盤層の除外本数
```

`docs/samples/boringlog.csv`(5 層)を渡した場合の出力:

| 出力 | 値 |
|---|---|
| 加重平均N値(R用) | 160.848(根入れ長 15.0 m) |
| 加重平均N値(Sb用) | 185.133(根入れ長 13.0 m) |
| 加重平均N値(砂質土等) | 199.061 |
| 加重平均N値(粘性土) | 8.000 |
| 加重平均一軸圧縮強度(岩盤) | 4.2 N/mm² |
| 岩盤層の除外本数 | 1 |

R 用と Sb 用が異なるのは、表層の埋土(N=3)が Sb の除外しきい値(N≦5)には該当するが R の除外しきい値(N=0)には該当しないため。打止め層(N=55、50回法・貫入量 3.3 cm)は換算N値 1500÷3.3≒454.5 に置き換えて集計している。

- **このノードだけは既定値のままでは実行できない**(空文字で即例外)。`File Path` ノードを繋ぐ
- 該当層が無い出力は `null` ではなく**空文字**を返すため、下流で数値演算するとエラーになる
- **行に 1 件でも不備があれば例外を投げて計算全体を止める**(帳票 CSV の「1 行の不備で全体を止めない」方針とは逆。部分的な値で地盤条件を進めると設計判断を誤るため)
- 算出値は積算コマンドの `nAvg` 欄へ**手入力で転記**する(自動連携は無い。[§9.2](#92-実装範囲の限定) の 11)

### 4.6 打設歩掛積算ノード(4 種)

対応する AutoCAD コマンドの対話フローを 1:1 で移植したもの。入力の意味・既定値・範囲は [§5.2](#52-打設歩掛積算) の各表と同じ(**ただし D/t/L 等の寸法は範囲チェックを行わない**)。算出根拠は [§6.2〜6.5](#62-打設歩掛--打撃工法4節-3-45) を参照。

| ノード | 対応コマンド | 特記 |
|---|---|---|
| `CalcFrontWallDriveEstimate` | `SPQW_FRONTWALL_Estimate` | 陸上時は船舶が空文字、海上時は杭打機が空文字 |
| `CalcVibroEstimate` | `SPQW_FRONTWALL_VibroEstimate` | 海上打設固定 |
| `CalcVibroJetEstimate` | `SPQW_FRONTWALL_VibroJetEstimate` | 入力 26 個で最多。`ValidateJetApplicability`(D≦1,500mm・L≦40m)で例外 |
| `CalcAnchorPileDriveEstimate` | `SPQW_ANCHORPILE_Estimate` | `inclDeg` で斜杭 K=1.2 を判定 |

```
 [Number]  D_mm/t_mm/L_m ────────┐
 [Boolean] isOffshore ───────────┤
 [Number]  penetration_m ────────┤
 [Number]  pileCount ────────────┤── SpqwNodes ──┬── 貫入抵抗値 R [kN]
 [Number]  nTip/nAvg ────────────┤ .CalcFrontWall├── 推奨ハンマ
 [Number]  jointCountPerPile ────┤  DriveEstimate├── 打設時間 Tc [分/本]
 [Boolean] isSevereSea ──────────┤               ├── 日当り打設 Q [本/日]
 [Boolean] hasObstacle ──────────┤               ├── 世話役/とび工/普通作業員/溶接工
 [Boolean] needCrawlerCrane ─────┤               └── クローラ式杭打機/杭打船/台船/引船/
 [Boolean] needTugBoat ──────────┤                   揚錨船/潜水士船 …ほか(23 ポート)
 [Boolean] needDiverVessel ──────┘
```

### 4.7 `SpqwGeometryNodes` — ソリッド生成(実験的・未検証)

Dynamo 自身のジオメトリカーネル(`ProtoGeometry.dll`)でソリッドを生成し、グラフ実行時に Dynamo が図面へ焼き込む。

| ノード | 入力 | 既定値 |
|---|---|---|
| `CreateAnchorPileSolid` | `headPoint`, `D_mm`, `t_mm`, `L_m`, `inclDeg`, `closedTip` | − / 800.0 / 12.0 / 20.0 / 0.0 / false |
| `CreateFrontWallPileSolid` | `headPoint`, `D_mm`, `t_mm`, `L_m` | − / 800.0 / 12.0 / 20.0 |
| `CreateTieRodSolid` | `baseX_m`, `positionY_m`, `rodDiameter_m`, `spanLength_m`, `pileDiameter_m`, `pilePitch_m`, `tieSpacing_m`, `tieCount`, `hwl_m`, `tieElevation_m` | 0.0 / 0.0 / 0.048 / 10.000 / 1.000 / 1.200 / 2.400 / 1 / 1.000 / 1.500 |

**AutoCAD コマンド版との違い:**

| 観点 | AutoCAD コマンド | `SpqwGeometryNodes` |
|---|---|---|
| XData | 記録する(`_Action` で再生成可) | **持たない**。焼き込み後のエンティティに後付けする手段が無い |
| パラメトリック性 | XData 保存 → `_Action` | Dynamo のグラフ再実行(入力値を変えると全体が再評価される) |
| 前壁の継手 | 実形状(LT65/75/100・PP/PT) | **未実装**(本体円筒のみ) |
| 前壁の選択 | XData から径・ピッチを自動代入 | 明示引数(`pileDiameter_m` 等)で受け取る |

杭先端への変換は `PileGeometry.TipFromHead`(Core、テスト済み)を再利用し、配置順序(回転 → 平行移動)は AutoCAD コマンド版の `BuildSolid` と一致させている。タイロッドの派生量は `TieRodCalculator.Compute` をそのまま呼び出すため、整合性チェック([§8.2](#82-部材間の整合性チェック))も同じ `TieRodParameters.Validate()` が実行する。

### 4.8 入力データの 3 経路

Dynamo へデータを渡す方法は 3 つあり、**読める CSV が経路ごとに違う**。

```
 経路A ── 柱状図 CSV ──▶ [CalcWeightedN] csvPath ──▶ 積算ノード
          専用ポートあり。ノードが直接パースする

 経路B ── 帳票 CSV ────▶ [Data.ImportCSV] ──▶ [List.Transpose] ──▶ 各ノード
          Dynamo 標準ノードで読む。列名解決・単位変換は行われない

 経路C ── 設計計算書 ───▶ [Data.ParseJSON] ──▶ [SpqwGeometryNodes]
          xlsx → 中間 JSON(§4.9)
```

| CSV / JSON | 読み込み先 | ノードから直接読めるか |
|---|---|---|
| 柱状図 CSV([§5.6](#56-柱状図-csv)) | `CalcWeightedN` の `csvPath` | ○ 専用ポートあり |
| 帳票 CSV([§5.5](#55-帳票-csv-取り込み)) | `SPQW_*_ImportCsv`(AutoCAD 専用) | × 標準ノードで読んで配線する |
| 設計計算書 JSON([§4.9](#49-設計計算書アライズを-dynamo-の入力にする)) | 標準ノード `Data.ParseJSON` | × 標準ノードで読んで配線する |

**経路 B の注意:**

```
 [File Path]        [Data.ImportCSV]     [List.DropItems]    [List.Transpose]
  frontwall_ ──▶ filePath            ──▶ amount ◀─ 1     ──▶
  import_minimal.csv transpose ◀─ false   (ヘッダー行を除去)      │
                                                                 ▼
                                                    [List.GetItemAtIndex]
                                                      index ◀── 0(外径列)
                                                                 │
                        [SpqwNodes.CalcSection] ◀────────────────┘ D_mm
                                断面積 A [cm2] ──▶ [Watch]  ← 10 要素のリスト
```

- **列名による対応付けはされない**。AutoCAD コマンド側の別名解決(`outer_d_mm` / `外径` / `D` を自動判別)は Core の各インポータが行うもので、この経路では列の位置を自分で指定する。CSV の列順を変えるとグラフが壊れる
- `Data.ImportCSV` が値を文字列で返す場合は `String.ToNumber` を挟む
- **単位変換も行われない**。タイロッド帳票 CSV は全て m のため他ノードと単位が合わない
- 積算ノードへ渡す `nAvg` は **int 型**のため `Math.Round` を挟む

いずれの経路も**文字コードは UTF-8 のみ**(Excel からは「CSV UTF-8 形式で保存」)。

### 4.9 設計計算書(アライズ)を Dynamo の入力にする

市販ソフト「アライズ」が出力した設計計算書(`docs/references/アライズ鋼管矢板岸壁.xlsx`)から §4.7 のジオメトリノードへ入力を渡す経路。**C# 側の追加はなく、Dynamo 標準ノードだけで配線する。**

```
xlsx ──(scripts/build-design-input.py)──▶ 中間 JSON ──(人がレビュー・補完)──▶ Dynamo
                                          docs/samples/arise_design_input.json
```

```bash
# 1. 計算書から中間 JSON を生成(既存ファイルは --overwrite 無しでは上書きしない)
python3 scripts/build-design-input.py docs/references/アライズ鋼管矢板岸壁.xlsx \
    --out docs/samples/arise_design_input.json

# 2. JSON を開き、計算書から決まらない 3 項目を埋める(下表)

# 3. 再検証。validation.errors が空になるまで繰り返す
python3 scripts/build-design-input.py --check docs/samples/arise_design_input.json
```

`--check` は入力値から `derived` と `validation` を計算し直して JSON に書き戻す(手編集した入力値は保持する)。終了コードは 0 = エラーなし / 1 = エラーあり / 2 = 入出力エラー。

**自動で入るのは 15 項目。残り 3 項目は計算書に情報が無いため、埋めるまで `validation.errors` が消えない。**

| JSON キー | なぜ自動で決まらないか | 記入例 |
|---|---|---|
| `tie_rod.rod_d` | 計算書はタイブルを型番 `F270T` で管理し、呼び径の記載が無い。メーカーカタログで確認する | `0.075` |
| `span.definition` | 「前面矢板-控え工間距離」が中心間か陸側定着面までか本文から判別できない。**誤ると控え杭位置が外径の半分ずれる**。原本 xlsx の間距離定義図(EMF)で確認する | `"center"` |
| `site.wall_length` | 計算書は 1 断面の計算のため施設延長を含まない | `100.0` |

**派生量(`derived`、スクリプトが算出)**

| キー | 計算式 | 例 |
|---|---|---|
| `front_wall_length` | `front_wall.head_z − front_wall.tip_z` | 30.5 m |
| `anchor_head_z` | `anchor_pile.tip_z + anchor_pile.length` | +2.0 m |
| `span_009` | `definition=="center"` なら `center_to_center + anchor_pile.outer_d / 2`、`"land_face"` ならそのまま | 20.0 m |
| `pile_count` | `ceil(site.wall_length / front_wall.pile_pitch)` | 85 本 |
| `tie_count` | `floor(site.wall_length / tie_rod.tie_spacing)` | 42 組 |

**検証の分担** — スクリプトが検査するのは**ノード側で検査できない項目だけ**である。径・肉厚・全長の範囲、取付間隔が矢板ピッチの整数倍であること等は各ノードが Core の `Validate()` で検査する。

| スクリプトの検査 | 内容 |
|---|---|
| 未確定 3 項目 | 上表の 3 キーが埋まっているか |
| タイ材と矢板天端 | `tie_elev < front_wall.head_z` |
| 控え杭天端とタイ材 | `anchor_head_z − tie_elev == anchor_pile.head_to_tie` |
| ピッチと継手式(警告) | 計算書の `pile_pitch` が `JointGeometry` の有効幅と一致するか。この計算書では 1.17809 m ⇔ 1.178086 m で**一致**(差 0.004 mm) |

**グラフ配線**

```
[File Path] ──▶ [File.FromPath] ──▶ [FileSystem.ReadText] ──▶ [Data.ParseJSON]
 arise_design_input.json                                              │ Dictionary
                                                                      ▼
                          ┌────────────── [Dictionary.ValueAtKey] ────┴─────────────┐
                          │ "validation" ──▶ "errors" ──▶ [List.Count] ──▶ [Watch]  │ ← 0 であること
                          │ "front_wall" / "tie_rod" / "anchor_pile" / "site" / "derived"
                          └──────────────────────────────────────────────────────────┘

前壁:  [Sequence] start ◀ site.origin_y / amount ◀ derived.pile_count / step ◀ front_wall.pile_pitch
         └──▶ [Point.ByCoordinates] x ◀ site.origin_x / z ◀ front_wall.head_z
                └──▶ [CreateFrontWallPileSolid]
                       D_mm ◀ front_wall.outer_d × 1000
                       t_mm ◀ front_wall.wall_t  × 1000
                       L_m  ◀ derived.front_wall_length

控え杭: [Sequence] start ◀ site.origin_y / amount ◀ derived.tie_count / step ◀ tie_rod.tie_spacing
         └──▶ [Point.ByCoordinates]
                x ◀ site.origin_x + derived.span_009 − anchor_pile.outer_d ÷ 2
                z ◀ derived.anchor_head_z
                └──▶ [CreateAnchorPileSolid]
                       D_mm ◀ anchor_pile.outer_d × 1000 / t_mm ◀ anchor_pile.wall_t × 1000
                       L_m ◀ anchor_pile.length / inclDeg ◀ anchor_pile.incl_deg

タイロッド: [CreateTieRodSolid]   ← 全入力が m。×1000 は不要
              baseX_m ◀ site.origin_x        positionY_m ◀ site.origin_y
              rodDiameter_m ◀ tie_rod.rod_d  spanLength_m ◀ derived.span_009
              pileDiameter_m ◀ front_wall.outer_d   pilePitch_m ◀ front_wall.pile_pitch
              tieSpacing_m ◀ tie_rod.tie_spacing    tieCount ◀ derived.tie_count
              hwl_m ◀ tie_rod.hwl                   tieElevation_m ◀ tie_rod.tie_elev
```

- **単位に注意**。JSON は全て m だが、`CreateFrontWallPileSolid` / `CreateAnchorPileSolid` の `D_mm` / `t_mm` は mm 呼称ポートのため **×1000 が必要**。`CreateTieRodSolid` は全ポートが m
- 控え杭の X は `AnchorAlignment.cs` の `anchorAxisX = frontAxisX + span − D/2` と同じ式を組む。`definition="center"` なら `19.500 + 0.5 − 0.5 = 19.500` と元の中心間距離に戻る(検算に使える)
- `validation.errors` は必ず `Watch` に出し、**空であることを目視してからソリッドを焼き込む**。Dynamo 側にエラーで停止する仕組みは無い
- **JSON がパラメータの唯一の記録になる**。Dynamo 生成ソリッドは XData を持たないため、JSON を図面と一緒に保管する

---

## 5. 入力パラメータ

単位・座標系の規則は [§8.1](#81-単位) に一元化してある。**外径・肉厚のみ対話プロンプト・Dynamo 入力で mm 呼称を許容**し、取得直後に m へ変換する。

### 5.1 前壁鋼管矢板(モデル生成)

| 英語名 | 日本語名 | 単位 | 既定値 | 範囲 |
|---|---|---|---|---|
| `outerDiameter` | 外径 D | mm(呼称) | 800 | 500〜2000 |
| `wallThickness` | 肉厚 t | mm(呼称) | 12 | 9〜25、かつ内径 > 0 |
| `length` | 全長 L | m | 20.0 | 1〜80 |
| `jointType` | 継手形式 | − | LT75 | LT65 / LT75 / LT100 / PP / PT |
| `grade` | 鋼種 | − | SKY400 | SKY400 / SKY490 |
| `wallLength` | 施設全長 | m | 100.000 | 0.1〜1000(`_Create` のみ。**外径 D より先に入力する**) |
| `effectiveWidth` | 有効幅 B(継手考慮) | m | 外径・継手形式から自動算出 | 0.5〜2.5(`_Create` のみ) |
| `pieceCount` | 総本数 | 本 | **`_Create` では自動算出** | 1〜500(`_Action` のみ入力) |
| `pieceIndex` | 施工順位 | 本目 | **`_Create` では 1 始まりで自動採番** | 1〜`pieceCount`(`_Action` のみ) |
| `colorIndex` | 本管の色 | ACI | 8 | 1〜255 |
| `headElevation` | 杭上端標高 Z_head | m(D.L.) | 2.000 | 変換後の Z_tip が −80〜10 |
| `planPoint` | 始点(1 本目の杭中心) | m | − | UCS ピック → WCS 変換(Z は使わない) |

**傾斜角 θ は入力しない(直杭のみ)**。`FrontWallRecord.InclDeg` フィールドは残っているが `_Create` / `_Action` は常に `0.0` を書き込む(既存の傾斜杭図面を `_Action` で再生成すると直杭になる)。`_ImportCsv` は `incl_deg` 列を指定すれば引き続き傾斜杭を取り込める。

**`_Create` の壁一括生成**(`FrontWall.WallLayout`)

| 派生量 | 計算式 | 施設全長 10 m・B=0.8752 m の場合 |
|---|---|---|
| 本数 | `ceil((施設全長 − 0.001) ÷ 有効幅)` | 11.426 → **12 本** |
| 実延長 | `本数 × 有効幅` | **10.502 m**(+0.502 m 超過) |
| 各本の Y | `始点Y + (施工順位 − 1) × 有効幅` | 0.000, 0.875, …, 9.628 |

端数は**切り上げ**(施設全長を必ずカバーするが終点は行き過ぎる)。誤差許容 1mm を差し引いてから割るため、ちょうど整数倍のときに浮動小数誤差で 1 本増えることはない。本数が 500 本を超える組合せは**エラー停止**する。

有効幅 B は入力値を優先し、算出値と 1mm を超えて食い違う場合は**警告を表示して続行**する。**確定した B は XData(`effective_width`)に記録され、タイロッド・控え杭・施設積算はこの値を使う**ため、カスタム B のままでも一貫して整合する。旧図面(キーが無い)は算出値にフォールバックする。

### 5.2 打設歩掛積算

外径・肉厚・全長・継手形式・総本数は選択した部材の XData から読むため入力不要。

**打撃工法**(`SPQW_FRONTWALL_Estimate`、4節 3-4.5)

| 英語名 | 日本語名 | 単位 | 既定値 | 範囲 |
|---|---|---|---|---|
| `site` | 施工区分 | − | 海上 | 陸上 / 海上 |
| `penetration` | 根入れ長 | m | 全長 × 0.5 | 0.1〜全長 |
| `pileCount` | 打設本数 | 本 | 総本数 | 1〜500 |
| `nTip` / `nAvg` | 先端 N 値 / 加重平均 N 値 | − | 50 / 20 | 1〜100 |
| `jointCountPerPile` | 継杭の継手個所数 | 箇所 | 0 | 0〜5 |
| `seaCondition` | 海象条件(海上のみ) | − | 普通 | 普通 / 悪い |
| `obstacle` | 障害の有無 | − | なし | なし / あり |
| `needCrawlerCrane` | クローラクレーンの計上(陸上のみ) | − | しない | する / しない |
| `needTugBoat` | 引船の計上(海上のみ。3-4.5-15 注1) | − | しない | する / しない |
| `needDiverVessel` | 潜水士船の計上(海上のみ、`obstacle` とは別軸) | − | しない | する / しない |

**振動工法・バイブロ単独**(`SPQW_FRONTWALL_VibroEstimate`、16節 3-2)— 施工区分は尋ねない(海上打設のみ)

| 英語名 | 日本語名 | 単位 | 既定値 | 範囲 |
|---|---|---|---|---|
| `driveLength` | 打設長 Lb | m | 全長 | 1〜80(表層の連続 N=0 区間は除く) |
| `pileCount` | 打設本数 | 本 | 総本数 | 1〜500 |
| `nTip` / `nAvg` | 先端地盤 N 値 / 周辺地盤の加重平均 N 値 | − | 50 / 20 | 1〜100 |
| `jointCountPerPile` | 継杭の継手個所数 | 箇所 | 0 | 0〜5 |
| `seaCondition` / `obstacle` / `needDiverVessel` | 海象条件 / 障害 / 潜水士船 | − | 普通 / なし / しない | − |

**振動工法・ジェット併用**(`SPQW_FRONTWALL_VibroJetEstimate`、16節 3-1)

| 英語名 | 日本語名 | 単位 | 既定値 | 範囲 |
|---|---|---|---|---|
| `site` | 施工区分 | − | 海上 | 陸上 / 海上 |
| `operatingHours` | 1 日当り運転時間 T | h/日 | 陸上 8.0 / 海上 6.0(固定) | 1〜24 |
| `driveLength` | 打込長 ℓ | m | 全長 | 1〜80 |
| `liftLength` | 吊込 1 回ごとの杭長 L₀ | m | 全長 | 1〜80 |
| `liftCount` | 杭の吊込み回数 nₛ | 回 | 1 | 1〜10 |
| `pileCount` | 打設本数 | 本 | 総本数 | 1〜500 |
| `soilType` | 土質 | − | 砂質土･レキ質土 | 5 区分(砂質土･レキ質土 / 粘性土 / 玉石混りレキ / 固結土 / 岩盤) |
| `nAvg` | 加重平均 N 値 | − | 30 | 1〜100 |
| `maxCobble` | 最大玉石径(玉石混りレキのみ) | mm | 100 | 76〜200 |
| `qu` | 加重平均一軸圧縮強度(岩盤のみ) | N/mm² | 10.0 | 0.1〜29.4 |
| `hasChuck` | 鋼管チャックの装備 | − | あり | なしは A₀ を 1.3 で除す |
| `jointLength` | 継手の長さ ℓj(ε 算定用) | m | 全長 | 0〜80 |
| `jetCount` | **ジェット使用台数** | 台 | 2 | 1〜4(**§9.1 の 1 参照**) |
| `nozzleCount` | **噴射ノズル数** | 個 | 6 | 1〜20(**同上**) |
| `needWaterSupply` | 水中ポンプ・水槽の計上 | − | しない | する / しない |
| `jointCountPerPile` | 継杭の継手個所数 | 箇所 | 0 | 0〜5 |
| `seaCondition` / `obstacle` / `needDiverVessel` | 海象条件 / 障害 / 潜水士船 | − | 普通 / なし / しない | 海上のみ |
| `vibroMass` | バイブロハンマ質量 Wv(チャック込み) | t | 10.0 | 0.1〜100 |

**控え杭・打撃工法**(`SPQW_ANCHORPILE_Estimate`、4節 3-4.6)— 施工区分は尋ねない(陸上打設のみ)

| 英語名 | 日本語名 | 単位 | 既定値 | 範囲 |
|---|---|---|---|---|
| `penetration` | 根入れ長 | m | 全長 × 0.5 | 0.1〜全長 |
| `pileCount` | 打設本数 | 本 | 1 | 1〜500 |
| `nTip` / `nAvg` | 先端 N 値 / 加重平均 N 値 | − | 50 / 20 | 1〜100 |
| `jointCountPerPile` | 継杭の継手個所数 | 箇所 | 0 | 0〜5 |
| `obstacle` / `needCrawlerCrane` | 障害 / クローラクレーン | − | なし / しない | − |

### 5.3 タイロッド

008 の 18 項目のうち 9 項目はプロンプトを廃止した(下表)。**基準の前壁は選択させず、図面内で杭中心 Y が最小の矢板(壁の 1 本目)を自動選択する**(2026-07-31)。海側鋼管矢板径・矢板ピッチはその前壁から自動代入する。

| 英語名 | 日本語名 | 単位 | 既定値 | 範囲 |
|---|---|---|---|---|
| `frontWallSelection` | 基準とする前壁 | − | **自動選択(杭中心 Y が最小の矢板)** | 図面に 1 本も無ければエラー停止 |
| `rodDiameter` | タイロッド径 | m | 0.048 | カタログ規格径 φ25〜φ90 の 19 種のみ |
| `spanLength` | 法線直角方向延長 span | m | 10.000 | 3.000〜40.000 |
| `pileDiameter` | 海側鋼管矢板径 | m | **前壁から自動代入** | 前壁の外径と一致 |
| `pilePitch` | 鋼管矢板ピッチ | m | **前壁から自動代入** | 前壁の有効幅 B と一致 |
| `everyNPiles` | 取付間隔(矢板何本ごと) | 本 | 1 | 1〜50、かつ間隔が 0.600〜20.000 m |
| `tieSpacing` | タイロッド取付間隔 | m | **`pilePitch × everyNPiles`** | 派生量(ピッチの整数倍が構造的に保証される) |
| `tieCount` | 組数 | 組 | **前壁総本数と `everyNPiles` から自動算定** | `(pieceCount−1)/everyNPiles + 1` |
| `hwl` | H.W.L. 標高 | m(D.L.) | 1.000 | 0.000〜5.000 |
| `tieElevation` | タイロッド軸心標高 | m(D.L.) | 1.500(H.W.L. 既定値 + 0.5 m の固定値) | −5.000〜10.000 |
| `layerColor` | 色 | ACI | 8 | 1〜255 |
| `positionY` | 1 組目の位置 Y | m | − | UCS ピック(**X は前壁から自動計算**) |

`span_length` は「前壁矢板中心 〜 陸側定着面」の水平距離(積算基準 3-4.5-(13))。定着金物はこの面より陸側へ張り出す。

**プロンプトを廃止した 9 項目**(計算式・XData は維持。`_Create` は固定値、`_Action` は前回保存値を使う):

| 項目 | 固定値(`_Create`) | 用途 |
|---|---|---|
| 鋼種 / 設計基準 / 荷重状態 | HT690 / PartialFactor / Normal | 張力照査 |
| 腹起し溝形鋼高さ h | 0.300 m | 全長算出式 |
| 定着プレート厚 t2 / ワッシャー厚 t1 | 0.025 m / 0.006 m | 同上 |
| ナット高さ / 調節長 | 積算基準表(φ38〜φ65)から自動設定。表外径は 0.055 m | 同上 |
| 取付点反力 Ap | 0.0 kN/m(張力照査なし) | 照査要否の切替 |

### 5.4 控え杭

`SPQW_ANCHORPILE_Create` は**基準部材の選択操作を持たない**(2026-07-31)。位置 Y が最小のタイロッド(1 組目)を自動選択して軸心標高・配置間隔・本数を取得し、前壁はそのタイロッドの `front_handle` から自動解決する。

| 英語名 | 日本語名 | 単位 | 既定値 | 範囲 |
|---|---|---|---|---|
| `frontWallSelection` | 基準とする前壁 | − | **自動解決(タイロッドの `front_handle`)** | 失効時のみ選択にフォールバック |
| `tieRodSelection` | 基準とするタイロッド | − | **自動選択(位置 Y が最小のタイロッド)** | 図面に 1 組も無ければエラー停止(`_Create` のみ) |
| `outerDiameter` | 外径 D | mm(呼称) | 800 | 318.5〜2500(JIS A 5525 標準径へスナップ) |
| `wallThickness` | 肉厚 t | mm(呼称) | 12 | 外径別の K011 製造範囲 |
| `length` | 全長 L | m | 20.0 | 1〜80 |
| `inclinationDeg` | 傾斜角 θ | deg | 0.0 | 0〜15 |
| `closedTip` | 先端形状 | − | 開端 | 開端 / 閉端 |
| `span` | 法線直角方向延長 | m | 10.0 | 3.0〜40.0 |
| `tieElevation` | タイロッド軸心標高 Z_tr | m(D.L.) | **タイロッドから自動代入** | 選択したタイロッドと一致 |
| `headElevation` | 杭上端標高 Z_head | m(D.L.) | **タイロッド軸心標高 + 0.5 m**(`AnchorAlignment.HeadAboveTie_m`) | 内部 Z_tip 換算値が −80〜10 |
| `colorIndex` | 本管の色 | ACI | 8 | 1〜255 |
| `everyNPiles` / `pileCount` | 配置間隔 / 本数 | − | **タイロッドから自動代入** | `_Create` のみ |
| `positionY` | 位置 Y | m | **図面内の全前壁のうち最小 Y に自動整列** | 2 本目以降は `始点Y + i × 配置間隔` |

控え杭は前壁と異なり**傾斜角プロンプトを維持**している(斜杭の需要があるため)。Z_head → 内部 Z_tip の変換は控え杭自身の全長・傾斜角を使う。

**杭上端標高 Z_head の既定値はタイ材中心の 0.5 m 上**(2026-07-31。従来は前壁の杭上端標高をそのまま使っていたが、控え杭の天端はタイ材の取り付け位置で決まるため相対で定める)。`_Create` と `_Action` の両方に適用し、**既定値として提示するだけで入力は従来どおり可能**。Z_tr は `_Create` では自動選択したタイロッドから、`_Action` では前回保存値から入る。タイロッドの既定軸心標高 1.500 m では天端 2.000 m となり、従来の既定値(先端 −18.0 + 全長 20.0)と一致する(テスト T1311)。

### 5.5 帳票 CSV 取り込み

積算ソフトの出力を **CSV UTF-8 形式で保存**したものを読み込み、対話入力を省いて一括生成する。1 行の不備は取り込み全体を止めず、行番号付きで一覧表示したうえで残りの行だけを生成する。**列名は未確定**([§9.1](#91-積算基準データの復元限界要原本確認) の 5)。

**前壁**(`SPQW_FRONTWALL_ImportCsv`。個別列が無い場合は「規格」列からの正規表現抽出にフォールバック)

| 列(別名) | 対応パラメータ | 必須 |
|---|---|---|
| `outer_d_mm` / `外径` | outerDiameter [mm] | ○(または規格列の `φNNN`) |
| `wall_t_mm` / `肉厚` | wallThickness [mm] | ○(または規格列の `×NN`) |
| `length_m` / `全長` / `L` | length [m] | ○(または規格列の `L=NN.N`) |
| `joint` / `継手形式` | jointType | −(既定 LT75) |
| `grade` / `鋼種` | grade | −(既定 SKY400) |
| `incl_deg` / `傾斜角` | inclinationDeg | − |
| `piece_count` / `piece_index` | pieceCount / pieceIndex | −(**両方無ければ総行数・出現順で自動採番**) |
| `color` / `色` | colorIndex | − |
| `tip_z` / `杭先端標高` | tipElevation [m] | ○(取り込み時に杭上端標高へ内部変換) |

平面位置は CSV に持たせず、実行時に「1 本目の位置」を 1 回だけピックし、以降は各行の有効幅 B で +Y へ自動配置する。

**タイロッド**(`SPQW_TIEROD_ImportCsv`)

| 列 | 対応パラメータ |
|---|---|
| `rod_d`, `grade`, `code`, `state`, `span_length`, `pile_d`, `pile_pitch`, `tie_spacing`, `tie_count`, `hwl`, `tie_elev`, `waling_h`, `plate_t`, `washer_t`, `nut_h`, `adjust_l`, `anchor_reaction`, `color` | §5.3 の同名パラメータ(**単位は全て m**) |
| `pos_y` / `Y` / `位置y` | positionY [m](**必須**) |

**控え杭**(`SPQW_ANCHORPILE_ImportCsv`)

| 列(別名) | 対応パラメータ |
|---|---|
| `outer_d_mm` / `外径`、`wall_t_mm` / `肉厚`、`length_m` / `全長`、`incl_deg` / `傾斜角`、`closed_tip` / `先端形状`、`span`、`tie_elev`、`tip_elev`、`color` | §5.4 の同名パラメータ |
| `pos_y` / `Y` / `位置y` | positionY [m](**必須**。省略すると全行が同一座標に重なる) |

`closed_tip` は `1` / `閉端` / `true` / `closed` を閉端と解釈する。

**突合検証**(`SPQW_QUAYWALL_ReconcileCsv`。「項目,数量」の 2 列 CSV)

| 項目ラベル(別名) | 対応する 009 計算値 |
|---|---|
| `施設延長` / `wall_length_m` | 施設延長 [m] |
| `継手接続数` / `joint_count` | 継手接続数 [箇所] |
| `前壁本管質量` / `front_body_kg` | 前壁 本管質量 [kg] |
| `前壁継手質量` / `front_joint_kg` | 前壁 継手質量 [kg] |
| `タイロッド質量` / `tie_rod_kg` | タイロッド質量 [kg] |
| `控え杭質量` / `anchor_kg` | 控え杭質量(本管+底板)[kg] |
| `合計質量` / `total_kg` | 合計質量 [kg] |

許容誤差は既定で比率 1%。帳票値が 0 の項目は絶対差で判定する。

**サンプル CSV**([`docs/samples/`](docs/samples/))— 前壁 → タイロッド → 控え杭を**通しで使える組合せ**にしてある(前壁 D=800mm・LT75 → B=875.2mm、杭頭標高 +3.0m > Z_tr=1.5m)。

| ファイル | 用途 | 検証結果 |
|---|---|---|
| `frontwall_import_minimal.csv` | 必須 4 列のみ。自動採番の例 | 10 行、施設延長 8.752m |
| `frontwall_import_full.csv` | 全 10 列明示。諸元が途中で変わる遷移区間の例 | 10 行、先端を −18/−19/−20m と変化 |
| `frontwall_import_spec.csv` | 「規格」列フォールバック(`φ800×12 L=21.0m LT75`) | 5 行。**杭先端標高だけは別列が必須** |
| `tierod_import.csv` | 必須 16 列 + 列挙型 3 列 | 3 組、Y=0 / 2.6256 / 5.2512m、前壁整合 OK |
| `anchorpile_import.csv` | 必須 7 列 + 任意 3 列 | 4 本、杭面間浄距離 8.800m、前壁整合 OK |
| `boringlog.csv` | 柱状図(§5.6) | 5 層、出力は §4.5 の表と一致 |
| `arise_design_input.json` | 設計計算書の中間 JSON(§4.9) | 未確定 3 項目でエラー 3 件(意図どおり) |

**単位が部材ごとに異なる**。前壁・控え杭は外径・肉厚のみ mm で他は m、**タイロッドは全て m**(`rod_d` は `48` ではなく `0.048`)。タイロッドで mm 値を書くと `Validate` が「単位はメートルです」で停止する。

### 5.6 柱状図 CSV

`SpqwNodes.CalcWeightedN`(§4.5)の入力形式。1 行 = 1 層。

| # | 列名(別名) | 単位 | 必須 | 説明 |
|---|---|---|---|---|
| 1 | `layer_name` / `土層名` | − | 任意 | 表示用ラベル。計算には使わない |
| 2 | `soil_type` / `土質区分` | − | ○ | `砂質土等` / `粘性土` / `玉石混りレキ` / `固結土` / `岩盤` |
| 3 | `elevation_top` / `標高上端` | m(D.L.) | ○ | Z 軸上向きのため 標高上端 > 標高下端 |
| 4 | `elevation_bottom` / `標高下端` | m(D.L.) | ○ | |
| 5 | `thickness_m` / `層厚` | m | ○ | 標高差と一致することを検証(誤差許容 1mm) |
| 6 | `n_value` / `N値` | − | `岩盤`以外は○ | `岩盤`行では空欄可 |
| 7 | `blow_count` / `打撃回数法` | 回 | 任意 | 50/60/70/80。N>50 の打止め行のみ |
| 8 | `penetration_cm` / `貫入量` | cm | 任意 | 換算N値 = 分子(1500/1800/2100/2400)÷ 貫入量 |
| 9 | `qu_value` / `一軸圧縮強度` | N/mm² | `岩盤`のみ○ | `岩盤`以外の行では指定不可 |

```csv
土層名,土質区分,標高上端,標高下端,層厚,N値,打撃回数法,貫入量,一軸圧縮強度
埋土,砂質土等,0.0,-2.0,2.0,3,,,
沖積粘土層,粘性土,-2.0,-5.0,3.0,8,,,
洪積砂質土層,砂質土等,-5.0,-10.0,5.0,22,,,
洪積砂礫層,砂質土等,-10.0,-15.0,5.0,55,50,3.3,
軟岩層,岩盤,-15.0,-20.0,5.0,,,,4.2
```

行順は問わない(標高上端の降順に並べ替えたうえで地表からの連続性を検証する)。

---

## 6. 計算値(自動算出)

信頼度ラベル: **確定**(出典のある式)/ **概算** / **推定**(カタログ式が無く代替値)。

### 6.1 前壁(諸元・断面性能)

| 派生量 | 計算式・出典 | 信頼度 |
|---|---|---|
| 断面積 A / 断面 2 次モーメント I / 断面係数 Z / 単位重量 W / 断面 2 次半径 i | 日本製鉄 K011 | 確定 |
| 有効幅 B(= 矢板ピッチ) | B = D + 継手有効間隔 J(K011)。LT65/LT75 は √式、PP は J=0.2478、PT は J=0.180 | 確定 |
| 有効幅 B(LT100) | カタログ式が無く D + 0.100 | **推定** |
| 継手の要否・雌雄 | 施工順位から一意(`pieceIndex > 1` で −Y 側、`< pieceCount` で +Y 側) | 確定 |
| 継手質量(側別) | A 側(+Y): LT = 山形鋼×2 / PP・PT = 鋼管。B 側(−Y): LT・PT = T 形鋼 / PP = 鋼管 | 確定 |
| 杭先端標高 Z_tip | `PileGeometry.TipFromHead(Z_head, L, θ)`。直杭のため `Z_head − L` | 確定 |

**継手 3D モデルの幾何整合性**(回帰テスト化済み)

| 継手形式 | 検証内容 | 結果 |
|---|---|---|
| LT65 / LT75 / LT100 | `JointFit.Overlaps` / `MinClearance` による多角形交差判定 | D=500〜2000mm で干渉せず、最小離隔は径によらず約 5.5mm で一定 |
| PP | `JointFit.PpPipeCenterDistance`(2 本の継手鋼管の中心間距離) | 外径 D に依らず一定(約 82.7mm)= 鋼管自身の半径(82.6mm)にほぼ一致 |
| PT | − | **対象外**(B 側が T 形鋼で非対称。別途の評価が必要) |

PP で中心間距離がパイプ直径(165.2mm)より短い、つまり **2D 断面で重なる**のは、「差し込んで隙間なく収まる」LT 型とは異なり「2 本の鋼管が絡み合う」構造のため正常。**多角形交差判定を PP/PT に使えない理由そのもの**である。

### 6.2 打設歩掛 — 打撃工法(4節 3-4.5)

| 派生量 | 計算式・出典 | 信頼度 |
|---|---|---|
| 貫入抵抗値 R | 300·N·Ap + 2·N̄·L·As(3-4.5-14)。**継手項は無い** | 確定 |
| ハンマ規格 | 鋼材質量と R の両方が収まる最小規格。4〜4.5t(4.56t/5,700kN)〜 15.0t(28.2t/35,100kN) | 確定 |
| 打撃速度 Sb / 溶接時間 Tw / 打設時間 Tc / 日当り打設 Q / 労務編成 | 3-4.5-15〜17 | 確定 |
| **クローラ式杭打機・クローラクレーン(陸上)** | ハンマ規格→3ランク(4〜4.5t / 6.5〜8t / 10〜12.5t)。**ハンマ 15.0t は陸上打設の表に行が無く表外** | **推定** |
| **杭打船(海上)** | ハンマ規格 5 ランク→3 ランク(H-65/H-125/H-150) | **推定** |
| **台船・揚錨船(海上)** | 16節 3-2 と同じ規格表(3-4.5-15 注3)。台船は杭長で選定、揚錨船は固定 | 確定 |
| **引船・潜水士船(海上)** | 「現場条件による追加船団」。引船は杭打船の移動が必要な場合、潜水士船は調査作業が伴う場合のみ計上 | 確定 |

### 6.3 打設歩掛 — 振動工法・バイブロ単独(16節 3-2、海上打設のみ)

| 派生量 | 計算式・出典 | 信頼度 |
|---|---|---|
| 本管の貫入抵抗 R1 | 300·N·Ap + 2·N̄·Lb·As(3-16-29)。打撃工法の R と同一形 | 確定 |
| 継手の貫入抵抗 Rj | R1 × 10⁻¹。**鋼管矢板のみ**加算(鋼管杭は 0)。振動工法固有 | 確定 |
| バイブロハンマ規格 | 鋼材質量と R の**両方**が収まる最小規格。90kW(2t/2,000kN)〜 240kW(20t/28,000kN) | 確定 |
| 発動発電機・起重機船 | バイブロ規格から一意(90kW→300kVA・80t吊 〜 240kW→800kVA・200t吊) | 確定 |
| **台船・引船** | 積載物の長さ(= 杭の全長)で選定(28m 未満→鋼300t積 / 鋼D450PS型 〜 39〜44m 未満→鋼1,000t積 / 鋼D600PS型)。44m 以上は基準に規定が無く別途選定 | 確定 |
| **揚錨船・潜水士船** | 揚錨船は鋼D 5t吊で固定。潜水士船は D270PS型 3〜5t吊、調査作業が伴う場合のみ | 確定 |
| 準備時間 Tp | 24 + 0.6·(Lb − 25) [分/本] | 確定 |
| 打込時間 Tb | Lb ÷ Lo。Lo = 鋼管矢板 0.75 / 鋼管杭 0.90 m/分 | 確定 |
| 溶接時間 Tw | 4節 3-4.5 の表を適用(3-16-31 注5) | 確定 |
| 日当り打設 Q | T·60/Tc ×(ei + E1 + E2 + E3)。ei = 0.70、T = 6 h/日 | 確定 |
| 労務編成 | 打設長 25 m 境界で とび工 3→5 人(鋼管矢板) | 確定 |
| 継手溶接機械 | φ800mm 未満: 500A×1 + 100kVA / 以上: 500A×2 + 125kVA | 確定 |

### 6.4 打設歩掛 — 振動工法・ジェット併用(16節 3-1)

| 派生量 | 計算式・出典 | 信頼度 |
|---|---|---|
| 基本振幅係数 A₀ | 土質 × N 値(または qu)の表(3-16-15)。チャック非装備なら 1.3 で除す | 確定 |
| 必要偏心モーメント K₀ | A₀ × Wp × 98 [N·m] | 確定 |
| バイブロハンマ・発動発電機規格 | K₀ から 7 ランク(≦200→45kW/150kVA 〜 ≦2,900→240kW/800kVA)。2,900 超は別途検討 | 確定 |
| ジェット規格 | エンジン式 243kW / 吐出圧力 14.7MPa / 吐出流量 895 ℓ/min | 確定 |
| ジェット用発動発電機 | 使用台数 1〜4 → 10 / 20 / 35 / 45 kVA | 確定 |
| 水中ポンプ・水槽 | 使用台数 1〜4 → φ150 10.6kW / φ200 15.5kW × 1〜2 台、水槽 20 / 30 m³ × 1〜2 基 | 確定 |
| 1m 当り打込み時間 γ | γ₁ = 0.02N+0.5(砂・砂質土・レキ質土)/ γ₂ = γ₁+η(玉石混りレキ)/ γ₃ = 0.04N+0.6(粘性土・固結土)/ γ₄ = 0.82qu+3(岩盤) | 確定 |
| 玉石補正係数 η | 最大玉石径 75〜100mm → 2 / 〜150mm → 2.5 / 〜200mm → 3 | 確定 |
| 係数 β・δ | β は外径 × 板厚、δ はバイブロ規格 × 外径の表。「−」の組合せは適用対象外 | 確定 |
| 加算時間 ε | 0.3 × 継手長(鋼管矢板のみ) | 確定 |
| 準備時間 Tp | (0.3·L₀ + 11) × nₛ + 5 [分/本](小数1位切上げ) | 確定 |
| 打込時間 Tb | γ·β·δ·ℓ + ε [分/本](小数1位切上げ) | 確定 |
| 日当り打設 Q | T·60/Tc ×(ei + E1 + E2 + E3)。ei = 陸上 0.80 / 海上 0.70 | 確定 |
| クレーン最大吊上げ荷重 | Cf =(Wv + Wp)× 6 [t] | 確定 |
| **台船・引船・揚錨船・潜水士船(海上のみ)** | 16節 3-2 と同じ規格表を参照(3-16-18 注2)。台船・引船は積載物の長さ(杭の全長)で選定 | 確定 |
| 労務編成 | 陸上は 20m、海上は 25m で区分が変わる(16節 3-2 とは別表) | 確定 |

**土質区分の注意**: A₀ 表(3-16-15)は「砂質土･レキ質土･**粘性土**」でひとくくりにするが、γ 表(3-16-20)は「砂・砂質土・レキ質土(γ₁)」と「**粘性土**・固結土(γ₃)」に分かれる。粘性土を一方の区分で通すと誤った係数になるため、Core では `JetLayerType`(5 区分)で受けて両表へ振り分けている。

### 6.5 打設歩掛 — 控え杭・打撃工法(4節 3-4.6、陸上打設のみ)

| 派生量 | 計算式・出典 | 信頼度 |
|---|---|---|
| 貫入抵抗値 R | 300·N·Ap + 2·N̄·L·As(3-4.6-12)。**4節 3-4.5 と同一式**のため共有実装を再利用 | 確定 |
| ハンマ規格・打撃速度 Sb・溶接時間 Tw・準備時間 Tp・ei/E1〜E3 | 4節 3-4.5 と数値まで完全一致するため共有実装を再利用 | 確定 |
| **打撃時間 Tb** | **Tb = K × L ÷ Sb(小数1位切上げ)。K = 直杭 1.0 / 斜杭 1.2**(3-4.6-14) | 確定 |
| 労務編成 | 杭長 20m 境界でとび工 2→3 人・普通作業員 1→2 人(3-4.6-15) | 確定 |
| **クローラ式杭打機・クローラクレーン** | 前壁と同じ選定(3-4.6-12) | **推定** |

### 6.6 タイロッド

| 派生量 | 計算式・出典 | 信頼度 |
|---|---|---|
| 全長 | span + (t1 + t2 + ナット高さ + 調節長) × 2 + h(積算基準 3-4.5-(13)) | 確定 |
| 海側取付点 X | 前壁の杭先端 X + (`tie_elevation` − Z_tip)·tan θ(前壁 θ は常に 0 のため実質は前壁の X) | 確定 |
| 断面積 / 体積 / 質量 | カタログ規格径へスナップした呼び径による | 確定 |
| 本体本数 / ターンバックル / リングジョイント | 継手方法表 | 確定 |
| 受杭箇所数 | 積算基準 3-4.5-(14)。法線方向は「タイロッド 1 本おき」で組数の切上げ半数 | 確定 |
| 許容張力 / 張力照査 | 鋼種・設計基準・荷重状態による | 確定 |

### 6.7 控え杭・施設全体

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

### 6.8 柱状図由来の加重平均N値・一軸圧縮強度

| 派生量 | 計算式・出典 | 信頼度 |
|---|---|---|
| 加重平均N値(R用) | 層厚加重平均。表層から連続する N=0 の層のみ除外(3-4.5-14, 3-4.6-12, 3-16-29)。岩盤層は常に除外し除外本数を別途返す | 確定 |
| 加重平均N値(Sb用) | 層厚加重平均。表層から連続する N≦5 の層を除外(3-4.5-16, 3-4.6-14。R 用より広い) | 確定 |
| 加重平均N値(土質区分別、γ用) | 土質区分ごとに層厚加重平均。除外ルールの明記が無いため除外を適用しない | 確定 |
| 加重平均一軸圧縮強度(岩盤) | 岩盤層のみ層厚加重平均(γ₄・A₀ 用) | 確定 |
| 換算N値(N>50 の打止め値) | 分子(50回法1500/60回法1800/70回法2100/80回法2400)÷ 貫入量(3-16-19 注3、3-16-6 注2) | 確定 |

---

## 7. ビルド方法

### 7.1 ビルドコマンド

Core 層は AutoCAD 非依存のため WSL / Linux でもビルド・テストできる。Plugin / Dynamo 層は本体 DLL が無い環境ではスタブで構文検証まで行う。

```bash
# Core + テスト(AutoCAD 不要。678 件が green であること)
dotnet test tests/SheetPileQuayWall.Core.Tests -c Release

# Plugin(AutoCAD コマンド)の構文検証(AutoCAD 不要。スタブとリンクする。配布不可)
dotnet build src/SheetPileQuayWall.Plugin/SheetPileQuayWall.Plugin.csproj -c Release -p:UseAutoCadStubs=true

# Dynamo(Zero Touch Node)の構文検証(AutoCAD/Dynamo 不要。スタブとリンクする。配布不可)
dotnet build src/SheetPileQuayWall.Dynamo/SheetPileQuayWall.Dynamo.csproj -c Release -p:UseAutoCadStubs=true

# Plugin の実機ビルド(AutoCAD 必須)
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/verify-dll-versions.ps1   # exit 0 を確認
dotnet build src/SheetPileQuayWall.Plugin/SheetPileQuayWall.Plugin.csproj -c Release

# Dynamo の実機ビルド(Civil 3D 同梱の Dynamo 3.3 必須)
dotnet build src/SheetPileQuayWall.Dynamo/SheetPileQuayWall.Dynamo.csproj -c Release
```

AutoCAD / Civil 3D が既定パス以外にある場合は `-p:AcadRoot="..."` を指定する。Dynamo プロジェクトは `$(AcadRoot)\C3D\Dynamo\Core\DynamoServices.dll` を既定の参照元とする(`-p:DynamoRoot="..."` で個別に上書き可)。

### 7.2 プロジェクト構成

```
009_sheet-pile-quaywall/
├── src/
│   ├── SheetPileQuayWall.Core/          AutoCAD 非依存の計算層(BCL のみ参照)
│   │   ├── Point3.cs / PileGeometry.cs / FrontWallRef.cs
│   │   ├── CrossMemberValidator.cs      部材間整合チェック(§8.2 の 4 組)
│   │   ├── QuayWallEstimate.cs          施設 1 件分の数量集計
│   │   ├── FrontWall/                   007 移植 8 + 新規 6
│   │   │                                 PieceAssignment / FrontWallPlacement / JointMass
│   │   │                                 WallLayout(施設全長→本数の壁一括レイアウト)
│   │   │                                 DriveEstimate(打撃)/ VibroEstimate(バイブロ単独)
│   │   │                                 VibroJetEstimate(ジェット併用)
│   │   │                                 DriveEquipment(打撃工法の杭打機・杭打船選定)
│   │   ├── TieRod/                      008 移植 5 + 新規 2(TieRodPlacement / TieRodPitch)
│   │   ├── AnchorPile/                  006@6d6d8cf 由来(書き直し)4 + 新規 1
│   │   │                                 (AnchorDriveEstimate。4節3-4.6 陸上打設)
│   │   ├── Import/                      帳票 CSV 取り込み 6 ファイル(CsvTable・各部材の
│   │   │                                 Importer・SpecTextParser・QuantityReconciliation)
│   │   └── Geotech/                     柱状図解析(BoringLog.cs)
│   ├── SheetPileQuayWall.Plugin/        AutoCAD 依存層
│   │   ├── Commands/                    SPQW_* 19 コマンド(8 ファイル)
│   │   ├── XData/                       XDataStore + 3 部材のレコード
│   │   └── DrawingHelper.cs / Prompt.cs / SolidBuilder.cs
│   └── SheetPileQuayWall.Dynamo/        Zero Touch Node 層(AutoCAD 本体 DLL を参照しない)
│       ├── SpqwNodes.cs                  7 ノード(純計算)
│       └── SpqwGeometryNodes.cs          3 ノード(ソリッド生成。実験的・未検証)
├── stubs/                               AutoCAD/Dynamo API スタブ(構文検証専用。配布禁止)
├── tests/SheetPileQuayWall.Core.Tests/  xUnit 678 ケース + fixtures/
├── scripts/
│   ├── port-from-legacy.sh              007/008 からの冪等移植
│   ├── verify-dll-versions.ps1          参照 DLL バージョン実測
│   ├── extract-xlsx-text.py             xlsx → テキスト / JSON 抽出(標準ライブラリのみ)
│   ├── build-design-input.py            設計計算書 → Dynamo 入力 JSON(§4.9)
│   ├── update-project.bat               ZIP ドラッグ&ドロップで固定フォルダへ展開・ビルド
│   └── fix-restore.bat                  .vs / obj / bin 削除 → restore → build
└── docs/
    ├── implementation-plan.md           設計決定 1〜11・フェーズ計画・実機検証項目
    ├── features.html                    機能概要(図表中心)
    ├── references/                      設計計算書(アライズ)と 009 パラメータ対応表
    └── samples/                          帳票 CSV 5 種 + 柱状図 CSV + 設計入力 JSON
```

### 7.3 AutoCAD / Civil 3D への登録(NETLOAD)

ビルドした Plugin DLL は自動ロード設定を持たないため、起動のたびに手動でロードする。

1. AutoCAD 2025 または Civil 3D 2025 を起動する
2. コマンドラインに `NETLOAD` と入力し Enter
3. `src\SheetPileQuayWall.Plugin\bin\Release\net8.0-windows\SheetPileQuayWall.Plugin.dll` を選択する
4. ロード完了後、[§3](#3-autocad--civil-3d-コマンド) の全 19 コマンドが実行可能になる

> スタブビルド(`-p:UseAutoCadStubs=true`)の出力は `AutoCadStubs.dll` を含むため **NETLOAD しないこと**(配布不可)。

### 7.4 Dynamo への登録(Import Library)

Dynamo ノードは AutoCAD コマンドとは**別の DLL** にあり、Dynamo 側の設定に永続登録されないため、グラフを開くたびに手動でインポートする。

1. Civil 3D 2025 で `DYNAMO` コマンドを実行し Dynamo を起動する
2. ライブラリペイン下部の「Import Library...」から `src\SheetPileQuayWall.Dynamo\bin\Release\net8.0-windows\SheetPileQuayWall.Dynamo.dll` を選択する(**`SheetPileQuayWall.Plugin.dll` ではない**)
3. ノード検索に `SpqwNodes.` / `SpqwGeometryNodes.` と入力すると計 10 ノードが表示される

### 7.5 トラブルシュート

**プロジェクトの置き場所を固定する(推奨。フォルダ乱立の根本対策)**

社内ルールで `git clone` / `git pull` が使えず GitHub の ZIP を都度ダウンロードする運用のため、そのままだと展開のたびに「新しいフォルダー (N)」が増え続け、NU1105 / CS0006 が繰り返し発生する。

[`scripts/update-project.bat`](scripts/update-project.bat) を**プロジェクトフォルダの外**(デスクトップなど)へ 1 回コピーしておくと、以後は新しい ZIP をこのファイルへ**ドラッグ&ドロップするだけ**で、固定フォルダへの上書き展開・キャッシュ削除・`dotnet restore`・`dotnet build` まで自動実行する。フォルダが増えないため NU1105 自体が起きなくなる。

> プロジェクトフォルダの**中**に置くと、更新のたびに自分自身が上書き対象に巻き込まれるため、必ず外に置くこと。

**NU1105 / CS0006 が出た場合**

`obj` / `.vs` に前のフォルダ名を含む古いキャッシュが残っている。[`scripts/fix-restore.bat`](scripts/fix-restore.bat) をダブルクリックすると `.vs` / `obj` / `bin` の削除、`dotnet restore`、`dotnet build`(Core → Plugin の順)を自動実行する。実行後は Visual Studio を再起動すること。

> `-p:Platform=x64` を手動ビルドコマンドに付けないこと。`Core.csproj` には `<Platforms>` の指定が無く、`Platform` を強制すると出力先が `bin\x64\...` という Visual Studio が期待しない場所になり CS0006 の原因になる(実機で確認済み)。

### 7.6 レガシー 3 リポジトリからの移植

Core の一部は 006/007/008 からの移植で、`scripts/port-from-legacy.sh` が再現する。移植元は `git show <commit>:<path>` で取り出すためレガシー側の作業ツリー状態に影響されず、冪等(再実行後に `git diff` が空なら同期済み)。

| 移植元 | 移植先 namespace |
|---|---|
| `SteelPipeSheetPile.Data`(007@`b12b188`) | `SheetPileQuayWall.Core.FrontWall` |
| `TaiRod.Core`(008@`ff3a986`) | `SheetPileQuayWall.Core.TieRod` |
| `006@6d6d8cf` の継手判定・整列計算 | 手作業で抽出・書き直し(スクリプト対象外) |

---

## 8. 規約・制約

**本章が単位・座標系・整合性の唯一の定義箇所である。**他章はここを参照する。

### 8.1 単位

| 規則 | 内容 |
|---|---|
| 基本 | **メートル統一**。内部処理・XData・派生量・テストはすべて m |
| 例外 | 外径・肉厚のみ、**対話プロンプトと Dynamo 入力ポートの呼称**に限り mm を許容する。取得直後に m へ変換する |
| 角度 | 度(deg) |
| 部材別の例外 | **タイロッドの帳票 CSV は全て m**(`rod_d` は `48` ではなく `0.048`)。mm 値を書くと `Validate` が停止する |
| 外部データ | 設計計算書は mm / cm / m が混在する。[§4.9](#49-設計計算書アライズを-dynamo-の入力にする) の変換表を参照 |

### 8.2 部材間の整合性チェック

`CrossMemberValidator` が、同じ量を 2 部材が別々に入力している箇所を突き合わせる。誤差許容は **1 mm = 0.001 m**。

```
       前壁                     タイロッド                  控え杭
   ┌──────────┐             ┌──────────────┐          ┌──────────────┐
   │ outer_d  │◀──── 1 ────▶│ pile_d       │          │              │
   │ 有効幅 B  │◀──── 2 ────▶│ pile_pitch   │          │              │
   │          │             │ tie_elev     │◀── 3 ───▶│ tie_elev     │
   │          │             │ span_length  │◀── 4 ───▶│ span         │
   └──────────┘             └──────────────┘          └──────────────┘
```

| # | 突き合わせ |
|---|---|
| 1 | タイロッドの海側鋼管矢板径 ⟺ 前壁の外径 |
| 2 | タイロッドの矢板ピッチ ⟺ 前壁の有効幅 B |
| 3 | タイロッドの軸心標高 ⟺ 控え杭の Z_tr |
| 4 | タイロッドの `span_length` ⟺ 控え杭の `span` |

このほか `TieRodParameters.Validate()` が単体で検査する項目(取付間隔が矢板ピッチの整数倍であること、腹起し高さ ≦ 矢板径、径がカタログ規格値であること等)がある。

### 8.3 コーディング規約

- **`using` ディレクティブを使わない**。型は完全修飾名で書く(暗黙 using も無効化)
- **Z 軸は上向き、Z = 0 が D.L.**。下向き座標は使わない
- 部材 1 本につき **Solid3d 1 個**に集約する(`BoolUnite` / `BoolSubtract`)
- 参照 DLL は `<Private>False</Private>`。AutoCAD 本体 DLL を配布物に同梱しない
- **006 / 007 / 008 へのプロジェクト参照・アセンブリ参照を追加しない**。共通ロジックは 009 内にコードとして移植する

### 8.4 エラー処理の方針

| 場面 | 方針 |
|---|---|
| 整合性チェック不一致 | **エラー停止**。自動補正も再生成もしない(外径の JIS / カタログスナップのみ例外) |
| 基準の表で「−」のセル | `null` として扱い **0 に潰さない**。0 に混入すると打込み時間が消える等の誤りになるため、該当する組合せではエラー停止する |
| 帳票 CSV の行の不備 | **全体を止めない**。行番号付きで一覧表示し、残りの行だけを生成する |
| 柱状図 CSV の行の不備 | **全体を止める**(上と逆)。部分的な値で地盤条件を進めると設計判断を誤るため |
| Dynamo ノードの入力不正 | `ArgumentException` を投げてノードを警告状態にする |

### 8.5 旧図面との互換

旧 RegApp(`STEELPIPEPILE` / `SPSP` / `TAIROD_PARAM` / `ANCHORPILE`)で作成した既存図面との**互換は持たない**。旧図面は旧プラグインで扱うか 009 で再作成する。009 自身の旧バージョン(`tip_x/_y/_z` 形式の前壁 XData 等)とは自動変換で互換を保つ([§3.4](#34-xdata-設計))。

---

## 9. 注意点・既知の課題

### 9.1 積算基準データの復元限界(要・原本確認)

参照ドキュメント『港湾土木請負工事積算基準 令和7年度改訂版.md』は OCR テキストであり、一部の表が復元できていない。**推測で値を埋めることはしていない**(CLAUDE.PRIVATE.md §9)。

| # | 対象 | 状態と対処 |
|---|---|---|
| 1 | **噴射ノズル数・ジェット使用台数の表**(3-16-16) | セル結合により OCR が崩壊し判読不能。原文は「500 / 3 4 / 4 4 1 / 600 1 2 …」のような形でどの数値がどのセルか特定できない。**`VibroJetEstimate` では利用者入力**として受け取る。台数が決まれば発動発電機・水中ポンプ・水槽は自動決定される。**原本の表を確認のうえ入力すること** |
| 2 | 施工規模区分 E3(16節 3-2、3-16-30) | 原文が「鋼管杭 50 本未満 −0.05 / 鋼管矢板 50 本以上 0」と読める形に崩れている。打撃工法(3-4.5)と同じ「50 本未満 −0.05 / 50 本以上 0」と解釈して実装した |
| 3 | 配管系部材取付のクレーン規格表(16節 3-1) | 表がどの項に属するか OCR 上判然としないため実装対象から外した |
| 4 | 前壁の有効幅 B(LT100) | 原本にカタログ式が無く D + 0.100 とした(信頼度**推定**) |
| 5 | **帳票 CSV の列名・レイアウト** | サーチマス等の実際のエクスポート形式(列名・並び順・エンコード)を未確認のまま、業界の一般的な慣行を仮定して別名リストを設計した。実データが手に入り次第 `Core.Import` 配下の各インポータに列名を追加すれば対応できるが、**現状のデフォルト別名は未検証**。対応エンコードも UTF-8 限定(Shift-JIS 等は事前に UTF-8 で保存し直す必要がある) |
| 6 | 土質区分別・岩盤の加重平均N値/qu の除外ルール | R・Sb 用には表層除外ルール(N=0 / N≦5)が基準に明記されているが、**ジェット併用γ用の土質区分別加重平均N値には明記が無い**ため除外を適用しない実装とした。岩盤層を含む地盤で R・Sb を計算する場合の扱いも基準に明記が無く 009 独自の判断(除外・件数報告に留める) |
| 7 | **打撃工法の杭打機・杭打船選定表**(3-4.5-14〜15、3-4.6-12〜13) | セル結合で崩れているが結合位置から対応を読み取って実装した。クローラ式杭打機は 3 ランク「4〜4.5t / 6.5〜8t(結合セル) / 10〜12.5t」で、**陸上打設の表にはハンマ 15.0t の行が無く表外**。杭打船は「4〜4.5t・6.5t→H-65 / 7〜8t・10〜12.5t→H-125 / 15.0t→H-150」。いずれも信頼度**推定**であり、**境界の最終確認は原本の表で行うこと** |

### 9.2 実装範囲の限定

| # | 内容 |
|---|---|
| 1 | **ジェット併用の γ は代表 1 層で算定**。基準 3-16-19 は 4 土質の打込み長による加重平均を定めており Core に `WeightedGamma` を用意してあるが、コマンドは対話入力の負担を考えて代表 1 層のみを受け取る。互層の現場では Core API を直接使うか加重平均済みの値を入力すること |
| 2 | **ジェット併用の配管系部材・導材・拘束費は未実装**。配管系部材の材料費・取付費(3-16-22)、導材(3-1-8、4節の準用)、作業船の拘束費(3-16-23)はいずれも打設とは別の代価表 |
| 3 | **振動工法の陸上打設(バイブロ単独)は基準に鋼管矢板の歩掛が無い**。16節 3-2 の適用範囲は海上打設に限られ、陸上のバイブロ歩掛(16節 2-1)は鋼矢板・H 形鋼杭が対象。そのため `VibroEstimate` は施工区分を尋ねず海上固定としている |
| 4 | 前壁の壁一括生成は `_Create`(施設全長 ÷ 有効幅)と `_ImportCsv`(帳票 CSV)の 2 系統。**直線配置のみ・全数同一諸元(`_Create` の場合)**を想定しており、平面線形が曲がる岸壁には対応しない。`_ImportCsv` は各矢板の Y を「1 つ前の矢板自身の有効幅」で加算するため、諸元が変化する遷移区間では概算になる。**1 本ずつ諸元を変えたい場合は `_ImportCsv`、1 本だけ直したい場合は `_Action`** |
| 5 | **工種体系へのマッピングは未実装**。`SPQW_QUAYWALL_Estimate` は鋼材質量の集計までで、『港湾工事工種体系ツリー.md』のレベル体系への対応付けは行っていない |
| 6 | 打設歩掛の Dynamo ノードは D/t/L 等の寸法チェックを行わないため、AutoCAD コマンド側より入力ミスに気付きにくい |
| 7 | **タイロッド・控え杭の帳票 CSV は `pos_y` が必須列**。前壁 CSV のような自動配置は行わない |
| 8 | **控え杭の帳票 CSV は前壁との整合チェックを取り込み時に行えない**。単体範囲チェックのみを行い、span 干渉チェックは前壁選択後に行う |
| 9 | **控え杭の打設歩掛は陸上打設のみ**。4節 3-4.6 には海上打設の船団構成・労務編成もあるが未実装 |
| 10 | **ジェット併用のメイン船はトン数ランクまで選定していない**。必要吊上げ荷重 Cf の数値を示すのみで、具体的なランクへ変換する表が 3-1 節側に見当たらない。付帯船舶は両工法とも実装済み |
| 11 | **`CalcWeightedN` と積算コマンドの自動連携は無い**。算出値は `nAvg` プロンプトへ手入力で転記する。また積算コマンドは R 用・Sb 用を区別せず単一の `nAvg` を両方に使うため、両者が大きく異なる地盤では使い分けを利用者が判断すること(§4.5 の例では 160.848 と 185.133 で 24 の差) |

### 9.3 モジュール間で挙動が揃っていない点

| # | 内容 |
|---|---|
| 1 | **鋼材質量の算定基礎が打撃工法だけ異なる**。`_Estimate`(打撃)はハンマ選定に**本管質量のみ**を使う(移植元 007 の挙動を維持)のに対し、振動工法の 2 コマンドは**本管 + 継手金物**を使う。積算基準はどちらも「鋼材質量」としか書いておらず継手を含めるかを明示していない。振動側は内訳を画面出力して差が追えるようにしてある。統一するかは要判断 |
| 2 | **端数処理が既存モジュールと異なる**。`VibroEstimate` / `VibroJetEstimate` は基準の「四捨五入」に合わせ `MidpointRounding.AwayFromZero` を使うが、`DriveEstimate` は `Math.Round` の既定(銀行丸め)のまま。ちょうど中間値のときのみ最終桁が 1 違う |
| 3 | **前壁と控え杭で外径の規則が異なる**。前壁は K011(D 0.500〜2.000 m、肉厚一律、スナップなし)、控え杭は JIS A 5525(D 0.3185〜2.500 m、径別肉厚範囲、スナップあり)。継手を持たない単独杭のため規則が違うこと自体は妥当だが、同一図面内で非対称になる |
| 4 | 溶接時間表は 16節 3-1・3-2 と 4節 3-4.5 で**同一データ**であることを確認済みのため共有実装を再利用している(3-16-31 注5 の指示とも一致) |
| 5 | **既存 `FrontWall.DriveEstimate.GetLabor` に労務編成のバグがある**(前壁の打撃工法に既存)。4節 3-4.5・3-4.6 とも実際の表は「陸上 20m 未満/以上・海上 25m 未満/以上」の 4 段階(とび工 2/3/4/5、普通作業員 1/2/2/2)だが、実装は**陸上側を杭長によらず一律 とび工2・普通作業員1 に固定**し、海上側のしきい値も 20m/25m の 3 分岐になっている(正しくは 25m の 2 分岐)。控え杭側(`AnchorPile.AnchorDriveEstimate.GetLabor`)は正しい表で新規実装したため同一条件で値が食い違う(**テスト T1208 でこれを検出**)。前壁側の修正は本件のスコープ外としたため未着手 |

### 9.4 移植元リポジトリの不整合

- **007 の継手質量にバグがある**。`JointCatalog.JointMassPerM` は P-P 形で鋼管を 1 本分(34.7 kg/m)しか数えないが、実形状は両側とも φ165.2×9 の鋼管であり正しくは 69.4 kg/m(約 50% の過小評価)。009 では `JointMass`(側別質量)を新設して積算に使っており、移植元ファイルは変更していない(`port-from-legacy.sh` の再実行で失われるため)。**007 側の修正は別途必要**
- **008 の `TieRodParameters.SpanLength` の XML コメントが誤っている**。「控工中心まで」とあるが、008 の README 図・算定式・006 の定義はいずれも「前壁矢板中心〜陸側定着面」を指す。009 では正しい定義で記述している
- **007 の `JointShapes`(継手断面、DXF 抽出データ)に極端に短い辺・極薄のノッチが含まれる**。実機で `Region.CreateFromCurves` が `eInvalidInput` で失敗する形で顕在化した(2 回)。(1) 極端に短い辺(実測 最小 0.0023mm)、(2) 隣接 3 点が完全に同一直線上にあり進行方向が反転する極薄のノッチ(35.7mm 進んだ直後に 0.177mm 逆戻り)。`JointShapes.cs` は手編集禁止のため、`PolygonCleanup.RemoveDegenerateVertices` → `RemoveNearCollinearVertices` の順で押し出し直前に除去する対処とした。副次的に PP/PT の自己交差も解消した。**007 側の DXF 再抽出は別途検討が必要**

### 9.5 検証状態マトリクス

| 対象 | 検証状態 | 根拠・残作業 |
|---|---|---|
| Core 層の計算ロジック | ✅ **検証済み** | xUnit 678 ケース green(WSL) |
| 帳票 CSV のパース | ✅ 検証済み | サンプル 5 ファイルをインポータ直接呼び出しでエラー 0 件 |
| 柱状図 CSV のパース・10 出力ポートの値 | ✅ 検証済み | Core 直接呼び出し。§4.5 の表と一致 |
| 設計計算書の抽出・中間 JSON 生成 | ✅ 検証済み | 正常系 0 件 / 異常系 5 ケース(WSL の Python) |
| 継手 3D モデルの幾何整合性 | ✅ 検証済み | `JointFit`(LT65/75/100 は非干渉、PP は中心間距離一定)。**PT は対象外** |
| Plugin 層(19 コマンド) | ⚠️ **未検証** | スタブによる構文・型検証のみ。NETLOAD・図面生成・MOVE 追随・旧図面変換は実機待ち |
| Dynamo ノード(10 ノード) | ⚠️ **未検証** | 同上。`[MultiReturn]` の日本語キー表示、Import Library の可否とも実機待ち |
| `SpqwGeometryNodes` の API シグネチャ | ⚠️ **未検証** | `Point`/`Circle`/`Solid` の各メソッドは一般的な Dynamo 3.x API 知識による**推測**。`ProtoGeometry.dll` を読み込めるか自体が未確認 |
| Dynamo グラフ配線(§4.8・§4.9) | ⚠️ 未検証 | `File Path` / `Data.ImportCSV` / `Data.ParseJSON` の挙動は実機待ち。`ParseJSON` が使えない場合は JSON 読込ノードを追加する |
| 参照 DLL バージョン | ⚠️ 未検証 | `verify-dll-versions.ps1` → exit 2(AutoCAD 未検出)。**exit 0 になるまで配布しない** |
| 帳票 CSV の列名 | ⚠️ 未検証 | 実際のエクスポート形式が未確認(§9.1 の 5) |
| 杭打機・杭打船の選定境界 | ⚠️ 推定 | OCR のセル結合による(§9.1 の 7)。原本確認が必要 |

実機での検証項目は [`docs/implementation-plan.md`](docs/implementation-plan.md) §13.5 を参照。

---

## 10. 設計変更の経緯

**過去のすべての変更点は `git log` で追える。** 本節は現在の設計を理解するうえで背景を知っておく価値がある、アーキテクチャ上の主要な決定だけを新しい順に要約する。

1. **基準部材の選択操作を廃止し、控え杭天端の決め方を変更**(2026-07-31)。`SPQW_TIEROD_Create` は前壁を選択させず杭中心 Y が最小の矢板(壁の 1 本目)を自動選択、`SPQW_ANCHORPILE_Create` は位置 Y が最小のタイロッドを自動選択してそこから前壁も解決する(選択操作 3 回 → 0 回)。あわせて控え杭の杭上端標高の既定値を「前壁の杭上端標高」から「**タイ材中心 + 0.5 m**」(`AnchorAlignment.HeadAboveTie_m`)へ変更した。控え杭の天端は前壁の施工基面ではなくタイ材の取り付け位置で決まるため。既定条件では従来と同じ +2.000 m になる(675→678 ケース)。
2. **別セッションの批判的レビュー指摘を反映**(`SpqwGeometryNodes` 中心に 8 件)。①タイロッドが全長の半分(+約5.3m)陸側にずれる配置バグを修正(`ExtrudeAsSolid` は片側押し出しなのに AutoCAD 版 `CreateFrustum`(原点中心)と同じ中点移動を使っていた)、②控え杭ノードの検証を前壁用 `InputValidator`(K011)からテスト済みの `AnchorPileSteel`(JIS A 5525)へ差し替え、③`Vector.ZAxis()` の Dispose 漏れ 2 箇所、④HWL 既定値変更の波及漏れ、⑤「TieElevation 既定値 = Hwl 既定値 + 0.5m」を固定するテストを追加(674→675 ケース)
3. **`SpqwGeometryNodes` を新設**(`ProtoGeometry.dll` 参照、実験的・未検証)。Dynamo が焼き込んだジオメトリに XData を後付けする手段が無いため `_Action` 相当には対応せず、パラメトリック性は Dynamo のグラフ再実行に委ねる設計にした
4. **PP形継手の3Dモデル幾何整合性を検証**(`JointFit.PpPipeCenterDistance`)。2 本の継手鋼管の中心間距離が外径に依らず一定であることを確認し回帰テスト化した
5. **Dynamo ノードを独立プロジェクトへ分離**。実機で `Dynamo.Exceptions.LibraryLoadFailedException` が発生し、原因は `<Private>False</Private>` 参照(AcCoreMgd 等)が `deps.json` に載らず Dynamo 側の依存解決(`AssemblyDependencyResolver` ベースと推定)が失敗すること。AutoCAD の `NETLOAD` は独自の読み込み経路のため影響を受けなかった。**最初に試した `IsVisibleInDynamoLibrary(false)` の付与は効果が無く**、AutoCAD 参照を持たない別プロジェクトへの分離で解決した
6. **前壁の内部表現を Z_tip 基準から Z_head 基準へ刷新**(XData キーも `head_x/_y/_z` に変更)。Z_tip は表示専用の計算値になった。旧図面は読み込み時に自動変換する
7. **前壁の傾斜角パラメータを廃止**(直杭のみに簡略化)。**控え杭の杭上端標高の既定値を前壁と同じ値に変更**
8. **タイロッド組数を前壁総本数から自動算定**し、9 項目のプロンプトを廃止(計算式・XData は維持)
9. **控え杭の生成をタイロッド選択方式に変更**(軸心標高・配置間隔・本数を自動設定)。位置 Y は図面内の全前壁のうち最小 Y に自動整列
10. **カスタム有効幅 B を使った際の部材間ズレを修正**(`FrontWallRef.EffectiveWidthM` 新設)
11. **継手 3D モデルの幾何整合性を検証**(`JointFit`)。あわせて実機で発覚した `Region.CreateFromCurves` のクラッシュを `PolygonCleanup` で解消した
12. **帳票 CSV 取り込み・柱状図解析・打設歩掛積算 4 系統・付帯船舶・杭打機/杭打船選定を追加**(フェーズ 5 以降)
13. **007/008 からの Core 移植・部材間整合チェックの新設**(フェーズ 1〜3)

---

## 11. 本 README の変更点

旧版(1,362 行)を白紙から再執筆した。**内容の削除はしておらず、重複の集約と図解の追加が変更の中心である。**

### 11.1 重複の解消

| 項目 | 旧版 | 新版 |
|---|---|---|
| 単位規則(m 統一・mm 呼称) | §2.1 / §4.1 / §5 冒頭 / §5.5 / §8 の 5 箇所 | **§8.1 に一元化**、他章は参照 |
| 座標系 | §1 / §5 に分散 | **§1.4 に図 + 表で一元化** |
| 部材間整合 | §5.5 / §8 に分散 | **§8.2 に図 + 表で一元化** |
| 「未検証」の注記 | §2 / §4.9 / §4.10 / §4.11 / §4.12 / §7 / §9.5 の 7 箇所に散在 | **§9.5 の検証状態マトリクスに集約**(11 項目を ✅/⚠️ で一覧化) |
| エラー処理の方針 | §4.4 / §5.5 / §9 に分散 | **§8.4 に表で一元化** |
| Dynamo ノードの入力表 | 打設歩掛 4 ノードで §4.5〜4.8 に全項目を再掲(§5.2 と重複) | **§4.6 に集約し §5.2 を参照**(4 ノードの相違点のみ表で示す) |

### 11.2 図解の追加(7 点)

| 図 | 節 | 内容 |
|---|---|---|
| データフロー全体像 | §1.1 | 入力 4 経路 → Core → 出力 4 種 |
| プロジェクト依存関係 | §2.1 | Core / Plugin / Dynamo と参照 DLL・登録方法 |
| 座標系 | §1.4 | 3 軸の向きと D.L. の位置 |
| 生成コマンドの依存関係 | §3.2 | `_Create` 3 段の実行順序と XData の受け渡し |
| 打設工法の選択フロー | §3.3 | 分岐条件からコマンドを決めるフローチャート |
| XData の相互参照 | §3.4 | `front_handle` と DxfCode 1011 による追随の仕組み |
| 部材間整合性チェック | §8.2 | 4 組の突き合わせを図示 |

### 11.3 構成の変更

- **目次を追加**(旧版は入口が無く 1,362 行を頭から読む必要があった)
- 冒頭に**サマリ表**を追加(対象・環境・構成・規模・由来を 5 行で把握できる)
- §4 の Dynamo ノードを**一覧表(§4.2)で俯瞰できるように**した(旧版は §4.2〜4.11 を順に読まないと全体像が掴めなかった)
- 継手の幾何整合性(旧 §6.1 の脚注 2 件、1 セルに 400 字超)を**表に分解**した
- §9.5 を「実機動作確認(3 行)」から**検証状態マトリクス(11 項目)**へ拡張した
- 旧 §10「変更履歴」を §10「設計変更の経緯」とし、README 自身の変更点を §11 に分けた(CLAUDE.PRIVATE.md §7-9)

### 11.4 事実の再確認

再執筆にあたり、以下を実装から再確認した(いずれも旧版の記載どおりで、**訂正は無い**)。

| 項目 | 確認方法 | 結果 |
|---|---|---|
| AutoCAD コマンド 19 個 | `grep CommandMethod src/**/Commands/*.cs` | 19 個、名称一致 |
| Dynamo ノード 7 + 3 個 | `SpqwNodes.cs` / `SpqwGeometryNodes.cs` の public static | 一致 |
| Core テスト 678 件 | `dotnet test` | 678 passed / 0 failed |
| 参照 DLL 5 個と Copy Local | 各 `.csproj` | 一致(全て `<Private>False</Private>`) |
| 有効幅 B の算定式 | `JointGeometry.EffectiveWidth(1.1, LT75)` | 1.178086 m(設計計算書の 1.17809 m と一致) |
