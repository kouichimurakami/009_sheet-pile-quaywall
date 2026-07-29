# Dynamo ノード リファレンス

[← README に戻る](../README.md)

> 文中の `§N.M` は、分割前の README 一括版のセクション番号を引き継いだ相互参照です。対応表: §1・§2・§8・§10 は [README](../README.md) 本体、§3 → [commands.md](commands.md)、§4 → [dynamo-nodes.md](dynamo-nodes.md)、§5 → [parameters.md](parameters.md)、§6 → [calculations.md](calculations.md)、§7 → [build.md](build.md)、§9 → [known-issues.md](known-issues.md)。


Civil 3D 2025 同梱の Dynamo 3.3 で使う Zero Touch Node。計算専用の `SpqwNodes`(**7 ノード**、§4.2〜4.10)と、ソリッド生成の `SpqwGeometryNodes`(**3 ノード**、§4.11、実験的・未検証)の計 **10 ノード**。**`SheetPileQuayWall.Dynamo.dll` という独立した DLL**に実装されており、AutoCAD コマンドの `SheetPileQuayWall.Plugin.dll` とは別に Import Library する(§7「Dynamo への登録」参照)。

### 4.1 共通仕様

- **カテゴリ**: ノード検索で `SpqwNodes.` と入力すると `SheetPileQuayWall.Dynamo > SpqwNodes` 配下に 7 ノードが表示される。
- **独立プロジェクトの理由**: 当初は `SheetPileQuayWall.Plugin.dll`(AutoCAD コマンドと同じアセンブリ)に同居させていたが、実機で Dynamo の Import Library が `Dynamo.Exceptions.LibraryLoadFailedException` で読み込み全体を拒否する不具合が判明した。原因は、AcCoreMgd/AcDbMgd/AcMgd を `<Private>False</Private>` で参照するアセンブリは .NET 8 の `deps.json` にその参照が載らず、Dynamo 側の依存解決(`AssemblyDependencyResolver` ベースと推定)が解決できないためと考えられる。AutoCAD の `NETLOAD` は影響を受けなかった(AutoCAD 独自の読み込み経路のため)。`SpqwNodes.cs` はもともと AutoCAD 非依存の純計算として書かれていたため、AutoCAD 参照を持たない別プロジェクト `SheetPileQuayWall.Dynamo` へ移し、Core と `DynamoServices.dll` のみを参照する構成にした(§10 参照)。
- **入力**: メソッドの各引数がそのまま入力ポートになる。`Number` / `String` / `Boolean` / `Code Block` ノードから配線する。**未配線のポートはデフォルト値で実行される**ため、既定値のまま出力を確かめてから実データに置き換えられる(例外: `CalcWeightedN` はファイルパス必須のため既定値では例外)。
- **出力**: 戻り値は `[MultiReturn]` の辞書で、**日本語の辞書キーがそのまま出力ポート名**になる(CLAUDE.PRIVATE.md §2.1)。必要な項目だけを下流(`Watch` / `Data.ExportToCSV` / `Excel.WriteToFile` 等)へ配線すればよい。
- **list-level 自動反復**: 入力に単一値の代わりにリストを渡すと、Dynamo が 1 要素ずつ自動で関数を呼び出し、結果をリストで返す(§4.9 の例 1)。
- **単位の境界**: 入力は実務の呼び径慣行に合わせ mm 呼称(`D_mm` 等)で受け、ノード内部で直ちに m へ変換する(決定 7)。内部計算・Core 層はすべて m。
- **エラー動作**: 入力不正・基準の規格表範囲外はすべて `ArgumentException` を投げてノードを警告状態(黄色)にする。不正値のまま計算を続けることはない。打設歩掛系の 4 ノード(§4.5〜4.8)は、対応する AutoCAD コマンドが「エラーメッセージを表示して中断」する箇所を、この規約に合わせて例外に置き換えている。
- **XData を経由しない**: AutoCAD コマンドが選択済みエンティティの XData から読む値(外径・肉厚・全長・傾斜角など)は、ノードでは明示的な引数として受け取る。
- 本節(§4.2〜4.10)はジオメトリを扱わない純計算のため `ProtoGeometry.dll` を使わない。ソリッド生成ノードは `SpqwGeometryNodes`(§4.11、実験的)として別クラスに分けている。

### 4.2 `SpqwNodes.CalcSection` — 前壁鋼管矢板の断面性能

前壁鋼管矢板 1 本分の断面性能(K011)・有効幅・継手質量を返す。候補径の比較検討や、`SPQW_FRONTWALL_Create` へ入力する前の諸元確認に使う。

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

**入力**

| 英語名 | 日本語名 | 単位 | デフォルト値 | 範囲 |
|---|---|---|---|---|
| `D_mm` | 外径 D | mm | 800.0 | 500〜2000(K011 製造範囲。`InputValidator.ValidateD`) |
| `t_mm` | 肉厚 t | mm | 12.0 | 9〜25、かつ内径 > 1 mm(`ValidateT`) |
| `L_m` | 全長 L | m | 20.0 | 1〜80(`ValidateL`) |
| `jointType` | 継手形式 | − | `"LT75"` | `LT65` / `LT75` / `LT100` / `PP` / `PT`(不明コードは例外) |

**出力**(9 ポート)

| 出力ポート | 説明 | 信頼度 |
|---|---|---|
| 断面積 A [cm2] | K011 | 確定 |
| 断面係数 Z [cm3] | K011 | 確定 |
| 断面2次モーメント I [cm4] | K011 | 確定 |
| 単位重量 W [kg/m] | K011 | 確定 |
| 本管質量 [kg] | W × L | 確定 |
| 断面2次半径 i [cm] | K011 | 確定 |
| 内径 d [mm] | D − 2t | 確定 |
| 有効幅 B [mm] | D + 継手有効間隔 J(§6.1) | 確定(LT100 のみ**推定**) |
| 継手質量 (1接続) [kg/m] | 側別質量の合計(`JointMass`。007 のバグを修正済み。§9.4) | 確定 |

### 4.3 `SpqwNodes.CalcQuayWallQuantity` — 施設 1 件分の鋼材質量集計

前壁・タイロッド・控え杭の諸元と本数から、岸壁 1 施設分の鋼材質量を集計する。**図面はまだ無く諸元だけが決まっている検討段階で、AutoCAD を開かずに概算したいときに使う**(AutoCAD コマンド `SPQW_QUAYWALL_Estimate` の Dynamo 版に相当するが、こちらは図面・XData を参照せず入力値だけで計算する)。

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

**入力**

| 英語名 | 日本語名 | 単位 | デフォルト値 | 範囲 |
|---|---|---|---|---|
| `frontD_mm` | 前壁 外径 | mm | 800.0 | 範囲チェックなし(K011 製造範囲 500〜2000 を目安) |
| `frontT_mm` | 前壁 肉厚 | mm | 12.0 | 範囲チェックなし(同 9〜25 を目安) |
| `frontL_m` | 前壁 全長 | m | 20.0 | 範囲チェックなし(同 1〜80 を目安) |
| `jointType` | 前壁 継手形式 | − | `"LT75"` | `LT65` / `LT75` / `LT100` / `PP` / `PT`(不明コードは例外) |
| `frontPieceCount` | 前壁 総本数 | 本 | 10 | 範囲チェックなし |
| `tieRodSetCount` | タイロッド組数 | 組 | 5 | 範囲チェックなし(0 で計上なし) |
| `tieRodMassPerSet_kg` | タイロッド 1 組当り質量 | kg | 150.0 | 範囲チェックなし(008 の `RodMass` 相当。付属品は含まない) |
| `anchorPileCount` | 控え杭 本数 | 本 | 5 | 範囲チェックなし(0 で計上なし) |
| `anchorD_mm` | 控え杭 外径 | mm | 800.0 | 範囲チェックなし(JIS A 5525 へのスナップも行わない) |
| `anchorT_mm` | 控え杭 肉厚 | mm | 12.0 | 範囲チェックなし |
| `anchorL_m` | 控え杭 全長 | m | 18.0 | 範囲チェックなし |
| `anchorClosedTip` | 控え杭 先端形状 | − | `false`(開端) | `true` = 閉端(底板質量を加算) |

> **範囲チェックなし**の入力は AutoCAD コマンド側と異なり検証されない(継手コードのみ例外を投げる)。検討段階の道具という位置づけのため、製造範囲外の値もそのまま計算される。確定諸元は §5.1 / §5.4 の範囲に収めること。

**出力**(7 ポート)

| 出力ポート | 説明 | 信頼度 |
|---|---|---|
| 施設延長 [m] | 有効幅 B × 前壁本数 | 確定 |
| 継手接続数 [箇所] | 前壁本数 − 1 | 確定 |
| 前壁 本管質量 [kg] | K011 単位重量 × 全長 × 本数 | 確定 |
| 前壁 継手質量 [kg] | 施工順位ごとの側別質量 × 全長の合計(= 接続数 × 1 接続あたり質量) | 確定 |
| タイロッド質量 [kg] | 1 組当り質量 × 組数 | 確定 |
| 控え杭 質量 [kg] | 本管質量 + 閉端時の底板質量 | 確定(底板は**概算**) |
| 合計質量 [kg] | 上記の総和 | 確定 |

### 4.4 `SpqwNodes.CalcWeightedN` — 柱状図 CSV から加重平均N値

柱状図 CSV(§5.6 の 9 列形式)から、打設歩掛積算コマンドが個別に尋ねる **加重平均N値**(R 用・Sb 用・土質区分別)と、岩盤層の**加重平均一軸圧縮強度**を算出する。ジオメトリ・AutoCAD トランザクションを伴わない純計算のため AutoCAD コマンドではなく Dynamo ノードとし、`File Path` ノード等からファイルパスを直接配線できる。

```
 [File Path] csvPath ── SpqwNodes.CalcWeightedN ──┬── 加重平均N値 (R用、N=0連続除外)
                                                  ├── 根入れ長 (R用) [m]
                                                  ├── 加重平均N値 (Sb用、N≦5連続除外)
                                                  ├── 根入れ長 (Sb用) [m]
                                                  ├── 加重平均N値 (砂質土等)
                                                  ├── 加重平均N値 (粘性土)
                                                  ├── 加重平均N値 (玉石混りレキ)
                                                  ├── 加重平均N値 (固結土)
                                                  ├── 加重平均一軸圧縮強度 (岩盤) [N/mm2]
                                                  └── 岩盤層の除外本数 (R/Sb計算から除外)
```

**入力**

| 英語名 | 日本語名 | 単位 | デフォルト値 | 範囲 |
|---|---|---|---|---|
| `csvPath` | 柱状図 CSV のファイルパス | − | `""`(空文字) | 存在する UTF-8 の CSV ファイル(§5.6 形式)。空文字・ファイル不存在・読取不可はいずれも例外 |

> 他の 2 ノードと違い、**既定値のままでは実行できない**(空文字で即例外にする設計)。Dynamo 標準の `File Path` ノード(ファイル選択ダイアログ付き)を繋ぐのが簡単。

**出力**(10 ポート)

| 出力ポート | 説明 | 信頼度 |
|---|---|---|
| 加重平均N値 (R用、N=0連続除外) | 貫入抵抗値 R の N̄。表層から連続する N=0 の層のみ除外(3-4.5-14 等) | 確定 |
| 根入れ長 (R用) [m] | R 用の集計対象層厚の合計 | 確定 |
| 加重平均N値 (Sb用、N≦5連続除外) | 打撃速度 Sb の N̄。表層から連続する N≦5 の層を除外(R 用より広い除外) | 確定 |
| 根入れ長 (Sb用) [m] | Sb 用の集計対象層厚の合計 | 確定 |
| 加重平均N値 (砂質土等/粘性土/玉石混りレキ/固結土) | ジェット併用 γ 用の土質区分別加重平均(除外なし。§9.1 の 6) | 確定 |
| 加重平均一軸圧縮強度 (岩盤) [N/mm2] | 岩盤層のみの層厚加重平均(γ₄・A₀ 用) | 確定 |
| 岩盤層の除外本数 | R/Sb 計算から除外した岩盤層の数(§9.1 の 6。009 独自判断) | − |

- 該当層が無く算出できない出力(例: 柱状図に岩盤層が無い場合の qu、粘性土が無い場合の粘性土 N̄)は**空文字**を返す。
- N>50 の打止め行は `打撃回数法`・`貫入量` 列から換算N値(分子 1500/1800/2100/2400 ÷ 貫入量 cm)へ自動変換して集計する。
- **行に 1 件でも不備があれば例外を投げて計算全体を止める**(「◯行目: 内容」を `; ` で連結した集約メッセージ)。帳票 CSV 取り込みコマンドの「1 行の不備で全体を止めない」方針とは逆で、部分的な値のまま地盤条件の計算を進めると設計判断を誤るため、あえて全件成功を必須にしている。
- 算出した値は、対応する AutoCAD コマンドの「加重平均N値」欄へ**手入力で転記**する(自動連携は無い)。

### 4.5 `SpqwNodes.CalcFrontWallDriveEstimate` — 前壁・打撃工法の打設歩掛積算

`SPQW_FRONTWALL_Estimate`(4節 3-4.5)の対話フローを 1:1 で移植。杭打機・杭打船・付帯船舶(§3「打撃工法の杭打機・杭打船」)を含む。

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

**入力**

| 英語名 | 日本語名 | 単位 | デフォルト値 | 範囲 |
|---|---|---|---|---|
| `D_mm` / `t_mm` / `L_m` | 外径 / 肉厚 / 全長 | mm / mm / m | 800.0 / 12.0 / 20.0 | チェックなし(選択済み前壁の XData に相当する値を渡す想定) |
| `isOffshore` | 施工区分(海上か) | − | `true` | `true`=海上 / `false`=陸上 |
| `penetration_m` | 根入れ長 | m | 10.0 | チェックなし |
| `pileCount` | 打設本数 | 本 | 10 | チェックなし |
| `nTip` / `nAvg` | 先端 N 値 / 加重平均 N 値 | − | 50 / 20 | チェックなし |
| `jointCountPerPile` | 継杭の継手個所数 | 箇所 | 0 | チェックなし |
| `isSevereSea` | 海象が悪いか(海上のみ有効) | − | `false` | − |
| `hasObstacle` | 障害の有無 | − | `false` | − |
| `needCrawlerCrane` | クローラクレーンの計上(陸上のみ有効) | − | `false` | − |
| `needTugBoat` | 引船の計上(海上のみ有効。現場条件により杭打船の移動が必要な場合。3-4.5-15 注1) | − | `false` | − |
| `needDiverVessel` | 潜水士船の計上(海上のみ有効) | − | `false` | − |

**出力**(23 ポート): 単位重量 W [kg/m]・1本当り質量 [kg]・合計質量 [t]・貫入抵抗値 R [kN]・推奨ハンマ・クローラ式杭打機・クローラクレーン・杭打船・台船・引船・揚錨船・潜水士船・打撃速度 Sb [m/分]・準備時間 Tp・打撃時間 Tb・溶接時間 Tw・打設時間 Tc(いずれも分/本)・日当り打設 Q [本/日]・打設日数 [日]・世話役/とび工/普通作業員/溶接工。陸上時は杭打船・台船・引船・揚錨船・潜水士船が空文字、海上時はクローラ式杭打機・クローラクレーンが空文字になる。引船は `needTugBoat`、潜水士船は `needDiverVessel` が `true` のときのみ規格を返す(「現場条件による追加船団」のため)。1 日当り打設本数が 0 以下になる入力条件は例外。

### 4.6 `SpqwNodes.CalcVibroEstimate` — 前壁・振動工法(バイブロ単独)の打設歩掛積算

`SPQW_FRONTWALL_VibroEstimate`(16節 3-2、海上打設のみ)の対話フローを 1:1 で移植。

```
 [Number]  D_mm/t_mm/L_m ────────┐
 [String]  jointType ────────────┤
 [Number]  pieceIndex/pieceCount ┤
 [Number]  driveLength_m ────────┤── SpqwNodes.CalcVibroEstimate ──┬── バイブロハンマ規格
 [Number]  pileCount ────────────┤                                 ├── 起重機船・杭打船
 [Number]  nTip/nAvg ────────────┤                                 ├── 台船/引船/揚錨船/潜水士船
 [Number]  jointCountPerPile ────┤                                 ├── 打設時間 Tc [分/本]
 [Boolean] isSevereSea ──────────┤                                 ├── 日当り打設 Q [本/日]
 [Boolean] hasObstacle ──────────┤                                 └── 世話役/とび工/普通作業員/
 [Boolean] needDiverVessel ──────┘                                    特殊作業員/溶接工 …(26 ポート)
```

**入力**

| 英語名 | 日本語名 | 単位 | デフォルト値 | 範囲 |
|---|---|---|---|---|
| `D_mm` / `t_mm` / `L_m` | 外径 / 肉厚 / 全長 | mm / mm / m | 800.0 / 12.0 / 20.0 | チェックなし |
| `jointType` | 継手形式 | − | `"LT75"` | `LT65`/`LT75`/`LT100`/`PP`/`PT`(不明コードは例外) |
| `pieceIndex` / `pieceCount` | 施工順位 / 総本数(継手金物質量の算定用) | − | 1 / 10 | `PieceAssignment.Validate` で検証(範囲外は例外) |
| `driveLength_m` | 打設長 Lb(表層の連続 N=0 区間は除く) | m | 20.0 | チェックなし |
| `pileCount` | 打設本数 | 本 | 10 | チェックなし |
| `nTip` / `nAvg` | 先端地盤 N 値 / 周辺地盤の加重平均 N 値 | − | 50 / 20 | チェックなし |
| `jointCountPerPile` | 継杭の継手個所数 | 箇所 | 0 | チェックなし |
| `isSevereSea` | 海象が悪いか | − | `false` | − |
| `hasObstacle` | 障害の有無 | − | `false` | − |
| `needDiverVessel` | 潜水士船の計上(`hasObstacle` とは別の判断軸) | − | `false` | − |

**出力**(26 ポート): 本管質量 [kg]・継手金物質量 [kg]・1本当り合計質量 [t]・本管貫入抵抗 R1・継手貫入抵抗 Rj・合計貫入抵抗 R(いずれも kN)・バイブロハンマ規格・発動発電機・起重機船・杭打船・継手溶接機械台数・継手溶接発電機・台船・引船・揚錨船・潜水士船・準備時間 Tp・打込時間 Tb・溶接時間 Tw・打設時間 Tc(分/本)・日当り打設 Q [本/日]・打設日数 [日]・世話役/とび工/普通作業員/特殊作業員/溶接工。

### 4.7 `SpqwNodes.CalcVibroJetEstimate` — 前壁・振動工法(ジェット併用)の打設歩掛積算

`SPQW_FRONTWALL_VibroJetEstimate`(16節 3-1、陸上/海上とも)の対話フローを 1:1 で移植。7 ノード中もっとも入力が多い(26 個)。

```
 [Number]  D_mm/t_mm/L_m/jointType/pieceIndex/pieceCount ─┐
 [Boolean] isOffshore ─────────────────────────────────────┤
 [Number]  operatingHours(陸上のみ) ────────────────────────┤
 [Number]  driveLength_m/liftLength_m/liftCount/pileCount ─┤
 [String]  soilType ────────────────────────────────────────┤── SpqwNodes ──┬── バイブロハンマ規格
 [Number]  nAvg/maxCobble_mm(玉石のみ)/qu(岩盤のみ) ─────────┤ .CalcVibroJet ├── 必要偏心モーメント K0
 [Boolean] hasChuck ────────────────────────────────────────┤  Estimate     ├── クレーン吊上げ荷重 Cf
 [Number]  jointLength_m/jetCount/nozzleCount ──────────────┤               ├── 打設時間 Tc [分/本]
 [Boolean] needWaterSupply ──────────────────────────────────┤               ├── 日当り打設 Q [本/日]
 [Number]  jointCountPerPile ─────────────────────────────────┤               └── 世話役/とび工/…
 [Boolean] isSevereSea/hasObstacle/needDiverVessel ──────────┤                  (36 ポート)
 [Number]  vibroMass_t ──────────────────────────────────────┘
```

**入力**(26 個。既定値は `SPQW_FRONTWALL_VibroJetEstimate` のプロンプト既定値と同じ)

| 英語名 | 日本語名 | 単位 | デフォルト値 | 範囲 |
|---|---|---|---|---|
| `D_mm` / `t_mm` / `L_m` | 外径 / 肉厚 / 全長 | mm / mm / m | 800.0 / 12.0 / 20.0 | `ValidateJetApplicability`: D≦1,500mm・L≦40m を超えると例外(3-1-3 注3) |
| `jointType` | 継手形式 | − | `"LT75"` | 5 種(不明コードは例外) |
| `pieceIndex` / `pieceCount` | 施工順位 / 総本数 | − | 1 / 10 | `PieceAssignment.Validate` で検証 |
| `isOffshore` | 施工区分(海上か) | − | `true` | − |
| `operatingHours` | クローラクレーンの運転時間 T(陸上のみ有効。海上は 6h/日固定) | h/日 | 8.0 | チェックなし |
| `driveLength_m` | 打込長 ℓ | m | 20.0 | チェックなし |
| `liftLength_m` | 吊込 1 回ごとの杭長 L0 | m | 20.0 | チェックなし |
| `liftCount` | 杭の吊込み回数 ns | 回 | 1 | チェックなし |
| `pileCount` | 打設本数 | 本 | 10 | チェックなし |
| `soilType` | 土質 | − | `"SG"` | `SG`(砂質土等)/`CL`(粘性土)/`CG`(玉石混りレキ)/`CE`(固結土)/`RK`(岩盤) |
| `nAvg` | 加重平均 N 値 | − | 30 | 土質と N 値の組合せが基本振幅係数表(3-16-15)に無いと例外 |
| `maxCobble_mm` | 最大玉石径(`soilType="CG"` のみ使用) | mm | 100.0 | 200mm 超は例外(η未定義) |
| `qu` | 加重平均一軸圧縮強度(`soilType="RK"` のみ使用) | N/mm² | 10.0 | 組合せが表に無いと例外 |
| `hasChuck` | 鋼管チャックの装備 | − | `true` | `false` で A0 を 1.3 で除す |
| `jointLength_m` | 継手の長さ ℓj(ε 算定用) | m | 20.0 | チェックなし |
| `jetCount` | ジェット使用台数(基準 3-16-16 の表は要確認) | 台 | 2 | 1〜4(範囲外は例外) |
| `nozzleCount` | 噴射ノズル数(基準 3-16-16 の表による) | 個 | 6 | チェックなし |
| `needWaterSupply` | 水中ポンプ・水槽の計上 | − | `false` | − |
| `jointCountPerPile` | 継杭の継手個所数 | 箇所 | 0 | チェックなし |
| `isSevereSea` | 海象が悪いか(海上のみ有効) | − | `false` | − |
| `hasObstacle` | 障害の有無 | − | `false` | − |
| `needDiverVessel` | 潜水士船の計上(海上のみ有効) | − | `false` | − |
| `vibroMass_t` | バイブロハンマ質量 Wv(鋼管チャック込み) | t | 10.0 | チェックなし |

**出力**(36 ポート): 本管質量・継手金物質量・杭1本当り質量 Wp・基本振幅係数 A0・必要偏心モーメント K0・バイブロハンマ規格・発動発電機(バイブロ用)・クレーン吊上げ荷重 Cf・台船・引船・揚錨船・潜水士船・ジェット使用台数・噴射ノズル数(いずれも入力値のエコー)・発動発電機(ジェット用)・水中ポンプ関連 6 項目・γ/β/δ/ε・準備時間 Tp・打込時間 Tb・溶接時間 Tw・打設時間 Tc・日当り打設 Q・打設日数・世話役/とび工/普通作業員/特殊作業員/溶接工。陸上時は台船・引船・揚錨船・潜水士船が空文字。バイブロ規格・係数β・係数δがいずれも表の範囲外の場合はそれぞれ例外。

### 4.8 `SpqwNodes.CalcAnchorPileDriveEstimate` — 控え杭・打撃工法の打設歩掛積算

`SPQW_ANCHORPILE_Estimate`(4節 3-4.6、陸上打設のみ)の対話フローを 1:1 で移植。

```
 [Number]  D_mm/t_mm/L_m ────────┐
 [Number]  inclDeg ──────────────┤
 [Number]  penetration_m ────────┤── SpqwNodes ──┬── 貫入抵抗値 R [kN]
 [Number]  pileCount ────────────┤ .CalcAnchorPile├── 推奨ハンマ
 [Number]  nTip/nAvg ────────────┤  DriveEstimate ├── クローラ式杭打機/クローラクレーン
 [Number]  jointCountPerPile ────┤               ├── 打設時間 Tc [分/本]
 [Boolean] hasObstacle ──────────┤               ├── 日当り打設 Q [本/日]
 [Boolean] needCrawlerCrane ─────┘               └── 世話役/とび工/普通作業員/溶接工(18 ポート)
```

**入力**

| 英語名 | 日本語名 | 単位 | デフォルト値 | 範囲 |
|---|---|---|---|---|
| `D_mm` / `t_mm` / `L_m` | 外径 / 肉厚 / 全長 | mm / mm / m | 800.0 / 12.0 / 20.0 | チェックなし |
| `inclDeg` | 傾斜角 θ(斜杭判定。`AnchorDriveEstimate.InclinationTolerance_deg` 超で斜杭 K=1.2) | deg | 0.0 | チェックなし |
| `penetration_m` | 根入れ長 | m | 10.0 | チェックなし |
| `pileCount` | 打設本数 | 本 | 1 | チェックなし |
| `nTip` / `nAvg` | 先端 N 値 / 加重平均 N 値 | − | 50 / 20 | チェックなし |
| `jointCountPerPile` | 継杭の継手個所数 | 箇所 | 0 | チェックなし |
| `hasObstacle` | 障害の有無 | − | `false` | − |
| `needCrawlerCrane` | クローラクレーンの計上 | − | `false` | − |

**出力**(18 ポート): 単位重量 W [kg/m]・1本当り質量 [kg]・合計質量 [t]・貫入抵抗値 R [kN]・推奨ハンマ・クローラ式杭打機・クローラクレーン・打撃速度 Sb・準備時間 Tp・打撃時間 Tb・溶接時間 Tw・打設時間 Tc(分/本)・日当り打設 Q [本/日]・打設日数 [日]・世話役/とび工/普通作業員/溶接工。

> **打設歩掛系 4 ノード(§4.5〜4.8)共通の注意**: 杭打機・杭打船のランク対応(`FrontWall.DriveEquipment`)は原文表のセル結合により信頼度**推定**(§9.1)。D/t/L 等の寸法は AutoCAD コマンドの「Estimate」系と同様に範囲チェックを行わない(選択済み XData を信頼する設計を、明示引数に置き換えても踏襲している)。算出根拠・端数処理・信頼度ラベルは §6.2〜6.5 の該当表を参照。

### 4.9 グラフ配線例(推奨ノード例)

いずれも Dynamo の標準機能(list-level 自動反復・既存の I/O ノード)との組合せだけで、`SpqwNodes` 側に特別な対応は要らない。この開発環境には Dynamo 実行系が無いため**グラフとしての動作は未検証**であり、数値は Core 層(`SectionProperties` / `QuayWallEstimate` / `BoringLogAnalysis`)を直接呼び出して算出した参考値である。

**例 1. 候補径の比較検討** — 候補外径を `Code Block` に列挙すると、list-level 自動反復で断面性能を横並び比較できる。

```
 [Code Block]
   D_mm = {700,800,900,1000,1100};  ────┐
 [Number] t_mm = 12.0 ───────────────────┤
 [Number] L_m = 20.0 ────────────────────┤── SpqwNodes.CalcSection ──┬── 単位重量 W [kg/m]
 [String] jointType = "LT75" ────────────┘                           ├── 有効幅 B [mm]
                                                                     └── 本管質量 [kg]
```

| D [mm] | A [cm²] | W [kg/m] | 本管質量 [kg] | i [cm] | B [mm] |
|---|---|---|---|---|---|
| 700 | 259.37 | 203.59 | 4,072 | 24.33 | 773.7 |
| 800 | 297.07 | 233.18 | 4,664 | 27.86 | 875.2 |
| 900 | 334.77 | 262.78 | 5,256 | 31.40 | 976.4 |
| 1000 | 372.47 | 292.37 | 5,847 | 34.93 | 1,077.3 |
| 1100 | 410.17 | 321.96 | 6,439 | 38.47 | 1,178.1 |

外径 700 → 1100 mm で単位重量はほぼ線形に増える。継手質量(1 接続あたり 32.60 kg/m、LT75)は外径に依らず一定。

**例 2. 施工順位ごとの継手質量** — 継手の要否・雌雄は施工順位から一意に決まる(`PieceAssignment`)。`List.Create` / `List.Count` で 3 パターンを作り `CalcSection` と組み合わせる。

```
 [Number] pieceCount = 10;  ── List.Count → 総本数
                                        │
    piece 1  ── 継手 +Y 側のみ(先頭)──┤
    piece 2〜9 ── 継手 両側 ───────────┼── 継手質量(1本あたり)= 側別質量の合計 × L_m
    piece 10 ── 継手 −Y 側のみ(末尾)──┘
```

「継手接続数 = 総本数 − 1」の等式が例 3 と独立に成立するため、整合性チェックに使える。

**例 3. 施設 1 件分の概算 → Excel へ書き出す** — `CalcQuayWallQuantity` を既定値のまま実行し、出力を `Data.ExportToCSV` / `Excel.WriteToFile` へ配線すると、AutoCAD を開かずに概算数量書を作れる。既定値での出力:

| 項目 | 値 |
|---|---|
| 施設延長 [m] | 8.752 |
| 継手接続数 [箇所] | 9 |
| 前壁 本管質量 [kg] | 46,637 |
| 前壁 継手質量 [kg] | 5,868 |
| タイロッド質量 [kg] | 750 |
| 控え杭 質量 [kg] | 20,987 |
| **合計質量 [kg]** | **74,242** |

施設延長 8.752 m は「有効幅 875.2 mm(例 1 の D=800 行と一致)× 10 本」、継手接続数 9 は「総本数 10 − 1」に一致する。

**例 4. 柱状図から加重平均N値を算出する** — `File Path` ノードで §5.6 の例 CSV(5 層: 埋土・粘土・砂質土・打止め層・岩盤)を渡した場合の出力:

| 出力 | 値 |
|---|---|
| 加重平均N値(R用) | 160.848(根入れ長 15.0 m) |
| 加重平均N値(Sb用) | 185.133(根入れ長 13.0 m) |
| 加重平均N値(砂質土等) | 199.061 |
| 加重平均N値(粘性土) | 8.000 |
| 加重平均一軸圧縮強度(岩盤) | 4.2 N/mm² |
| 岩盤層の除外本数 | 1 |

R 用と Sb 用が異なるのは、表層の埋土(N=3)が Sb の除外しきい値(N≦5)には該当して除外されるが、R の除外しきい値(N=0)には該当しないため。打止め層(N=55、50回法・貫入量 3.3 cm)は換算N値 1500÷3.3≒454.5 に置き換えて集計している。

### 4.10 CSV を Dynamo から使う

**ノードが直接読める CSV は柱状図 CSV(§5.6)だけ**である。帳票 CSV(§5.5)は AutoCAD コマンド `SPQW_*_ImportCsv` 専用で、`SpqwNodes` 側に読み込み口を持たない。

| CSV | 読み込み先 | ノードから直接読めるか |
|---|---|---|
| 柱状図 CSV | `CalcWeightedN` の `csvPath` 入力 | ○ 専用ポートあり(経路 A) |
| 帳票 CSV | `SPQW_FRONTWALL_ImportCsv` 等 | × Dynamo 標準ノードで読んで配線する(経路 B) |

**経路 A. 柱状図 CSV → `CalcWeightedN` → 積算ノード**

```
 [File Path]                [SpqwNodes.CalcWeightedN]        [Math.Round]
  Browse... ──── string ──▶ csvPath                              ▲
  boringlog.csv                加重平均N値 (R用) ─────────────────┘
                               根入れ長 (R用) [m] ──────┐         │
                               加重平均N値 (Sb用)       │         │
                               … ほか 7 ポート ──▶ [Watch]        │
                                                        │         │
              [SpqwNodes.CalcFrontWallDriveEstimate] ◀───┘         │
                 penetration_m                                     │
                 nAvg ◀────────────────────────────────────────────┘
                 D_mm / t_mm / L_m ◀── [Number]
                        貫入抵抗値 R [kN] ──▶ [Watch]
                        推奨ハンマ ─────────▶ [Watch]
                        … ──▶ [List.Create] ──▶ [Data.ExportToCSV]
```

1. `File Path` ノードを置き、Browse... で `docs/samples/boringlog.csv` を選ぶ(文字列としてパスを出力する)
2. `SpqwNodes.CalcWeightedN` の `csvPath` へ配線する。**このノードだけは未配線だと例外**になる(他の 6 ノードは既定値で動く)
3. 使う出力ポートに `Watch` を繋ぐ。10 ポート全部を繋ぐ必要はない
4. 積算ノードへ渡す場合、`nAvg` は **int 型**のため `Math.Round` を挟む。`CalcWeightedN` は小数を返す
5. 書き出しは `List.Create` で束ねてから `Data.ExportToCSV` へ

該当層が無い出力(玉石混りレキ・固結土など)は `null` ではなく**空文字**を返すため、下流で数値演算するとエラーになる。

積算ノードは R 用・Sb 用を区別せず単一の `nAvg` を両方に使う(§9.2 の 11)。上表の例では 160.848 と 185.133 で 24 の差があり、どちらを採るかは利用者判断。

**経路 B. 帳票 CSV → Dynamo 標準ノードで読んで配線**

```
 [File Path]        [Data.ImportCSV]     [List.DropItems]    [List.Transpose]
  frontwall_ ──▶ filePath            ──▶ amount ◀─ 1     ──▶
  import_minimal.csv transpose ◀─ false   (ヘッダー行を除去)      │
                                                                 ▼
                                                    [List.GetItemAtIndex]
                                                      index ◀── 0(外径列)
                                                                 │
                        [SpqwNodes.CalcSection] ◀────────────────┘ D_mm
                           t_mm ◀── 列1 / L_m ◀── 列2
                           jointType ◀── [String] "LT75"
                                断面積 A [cm2] ──▶ [Watch]  ← 10 要素のリスト
                                有効幅 B [mm]  ──▶ [Watch]
```

- **list-level 自動反復**が効くため、列のリストを渡せば結果もリストで返る(§4.1)
- **列名による対応付けはされない**。AutoCAD コマンド側の別名解決(`outer_d_mm` / `外径` / `D` を自動判別)は Core の各インポータが行うもので、この経路では列の位置を自分で指定する。CSV の列順を変えるとグラフが壊れる
- `Data.ImportCSV` が値を文字列で返す場合は `String.ToNumber` を挟む
- **単位変換も行われない**。`CalcSection` の `D_mm` は mm 呼称なので前壁 CSV の `800` をそのまま渡せるが、タイロッド帳票 CSV は全て m のため他ノードと単位が合わない

いずれの経路も**文字コードは UTF-8 のみ**(Excel からは「CSV UTF-8 形式で保存」)。柱状図 CSV は 1 行でも不備があれば計算全体を止める(帳票 CSV の「1 行の不備で全体を止めない」方針とは逆。§4.4)。

> **未検証**: 本節のグラフ配線は実機の Dynamo で動作確認していない(§4.9 冒頭と同じ制約)。検証済みなのは柱状図 CSV のパースと 10 出力ポートの値(Core 層を直接呼び出し、例 4 の表と一致)、および帳票 CSV 5 ファイルのパースと部材間整合まで。`File Path` / `Data.ImportCSV` / `List.Transpose` の挙動と、`[MultiReturn]` の日本語キーの表示は実機確認が必要。

### 4.11 `SpqwGeometryNodes` — Dynamo ネイティブジオメトリでのソリッド生成(実験的・未検証)

Dynamo 自身のジオメトリカーネル(`Autodesk.DesignScript.Geometry` / `ProtoGeometry.dll`)でソリッドを生成し、グラフ実行時に Dynamo が図面へ焼き込む。`SpqwNodes`(§4.1〜4.10)とは性質が異なるため別クラスに分けている。

- **カテゴリ**: ノード検索で `SpqwGeometryNodes.` と入力すると `SheetPileQuayWall.Dynamo > SpqwGeometryNodes` 配下に 3 ノードが表示される。
- **XData を持たない**: Dynamo が焼き込んだ後のエンティティに、ノード側から XData を後付けする手段が無いため(戻り値の `Geometry` を Dynamo が消費した後は触れない)、本ノードが生成するソリッドは AutoCAD コマンド版と異なり `_Action` での再生成に対応しない。パラメトリック性は Dynamo 自身のグラフ再実行(入力値を変えるとグラフ全体が再評価され、焼き込み済みジオメトリも自動で置き換わる)に委ねる。
- **前壁は本体円筒のみ**: `CreateFrontWallPileSolid` は継手(LT65/75/100・PP/PT の実形状)を持たない単純円筒。継手の複製(`SolidBuilder.JointMember` 相当)は未実装。
- **挿入点は `Point` 型(控え杭・前壁のみ)**: `headPoint`(杭上端、D.L. 基準)は既定値を持たない必須入力。`Point.ByCoordinates` 等で配線する。`CreateTieRodSolid` は前壁を選択しないため位置も `baseX_m`/`positionY_m` の数値で受け取る(下記)。
- **`CreateTieRodSolid` は前壁選択を行わない**: AutoCAD コマンド版(`SPQW_TIEROD_Create`)は選択した前壁の XData から海側鋼管矢板径・矢板ピッチ・海側取付点 X を自動代入するが、本ノードは `SpqwNodes` と同じ「XData を経由しない」規約に合わせ、これらを明示引数(`pileDiameter_m`/`pilePitch_m`/`baseX_m`)で直接受け取る。鋼種・設計基準・荷重状態・腹起し高さ・定着プレート/ワッシャー厚・取付点反力は `TieRodParameters` の既定値(_Create のプロンプト廃止方針と同じ)を固定で使う。`tieCount` 組ぶんの `Solid` を配列で返す。
- **全面未検証**: `ProtoGeometry.dll` を Dynamo の Import Library へ読み込めるか自体が未確認(AcCoreMgd 等とは異なり `<Private>False</Private>` でも Dynamo 本体が使う DLL のため §4.1 の deps.json 問題は起きない想定だが未実機検証)。`Point`/`Circle`/`Solid` の各メソッドシグネチャも一般的な Dynamo 3.x API 知識による推測であり、実際の `ProtoGeometry.dll` と一致するかは実機ビルドまで不明(WSL はスタブによる構文検証のみ)。

| ノード | 内容 | 入力 | 既定値 |
|---|---|---|---|
| `CreateAnchorPileSolid` | 控え杭(本管 + 閉端時は底板、傾斜角対応) | `headPoint`, `D_mm`, `t_mm`, `L_m`, `inclDeg`, `closedTip` | 800.0 / 12.0 / 20.0 / 0.0 / false |
| `CreateFrontWallPileSolid` | 前壁本体円筒(θ=0 固定、継手なし) | `headPoint`, `D_mm`, `t_mm`, `L_m` | 800.0 / 12.0 / 20.0 |
| `CreateTieRodSolid` | タイロッド(直杭、法線直角方向の軸線に沿った円柱を組数ぶん) | `baseX_m`, `positionY_m`, `rodDiameter_m`, `spanLength_m`, `pileDiameter_m`, `pilePitch_m`, `tieSpacing_m`, `tieCount`, `hwl_m`, `tieElevation_m` | 0.0 / 0.0 / 0.048 / 10.000 / 1.000 / 1.200 / 2.400 / 1 / 2.000 / 2.500 |

控え杭・前壁の杭先端への変換は `PileGeometry.TipFromHead`(Core、既存・テスト済み)をそのまま再利用し、配置順序(回転 → 平行移動)は AutoCAD コマンド版の `BuildSolid` と一致させている。タイロッドの派生量(全長・海側/陸側端 X・各組 Y 座標)は `TieRodCalculator.Compute`(Core、既存・テスト済み)をそのまま呼び出し、整合性チェック(取付間隔が矢板ピッチの整数倍であること等)も同じ `TieRodParameters.Validate()` が実行する。
