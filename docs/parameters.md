# 入力パラメータ

[← README に戻る](../README.md)

> 文中の `§N.M` は、分割前の README 一括版のセクション番号を引き継いだ相互参照です。対応表: §1・§2・§8・§10 は [README](../README.md) 本体、§3 → [commands.md](commands.md)、§4 → [dynamo-nodes.md](dynamo-nodes.md)、§5 → [parameters.md](parameters.md)、§6 → [calculations.md](calculations.md)、§7 → [build.md](build.md)、§9 → [known-issues.md](known-issues.md)。


**単位はメートル統一**。外径・肉厚のみ、対話プロンプトの表示・入力に限り mm 呼称を許容し、取得直後に m へ変換する(内部処理・XData・派生量はすべて m。決定 7)。

### 5.1 前壁鋼管矢板(モデル生成)

| 英語名 | 日本語名 | 単位 | デフォルト値 | 範囲 |
|---|---|---|---|---|
| `outerDiameter` | 外径 D | mm(入力時呼称) | 800 | 500〜2000 |
| `wallThickness` | 肉厚 t | mm(入力時呼称) | 12 | 9〜25、かつ内径 > 0 |
| `length` | 全長 L | m | 20.0 | 1〜80 |
| `jointType` | 継手形式 | − | LT75 | LT65 / LT75 / LT100 / PP / PT |
| `grade` | 鋼種 | − | SKY400 | SKY400 / SKY490 |
| `wallLength` | 施設全長 | m | 100.000 | 0.1〜1000(`_Create` のみ。**外径 D より先に入力する**) |
| `effectiveWidth` | 鋼管矢板 有効幅 B(継手考慮) | m | 外径・継手形式から自動算出 | 0.5〜2.5(`_Create` のみ) |
| `pieceCount` | 総本数 | 本 | **`_Create` では自動算出** | 1〜500(`_Action` のみ入力) |
| `pieceIndex` | 施工順位 | 本目 | **`_Create` では 1 始まりで自動採番** | 1〜`pieceCount`(`_Action` のみ入力) |
| `colorIndex` | 本管の色 | ACI | 8 | 1〜255 |
| `headElevation` | 杭上端(杭頭)標高 Z_head | m(D.L.) | −18.0(既定 Z_tip)+ 20.0(既定 L)= **2.000** | 変換後の Z_tip が −80〜10 に収まること |
| `planPoint` | 始点(1 本目の杭中心) | m | − | UCS ピック → WCS 変換(Z は使わない) |

**傾斜角 θ は入力しない(直杭のみ)**。`FrontWallRecord.InclDeg` フィールド自体は残っているが、`SPQW_FRONTWALL_Create` / `_Action` は常に `0.0` を書き込む(既存の傾斜杭図面を `_Action` で再生成すると直杭になる)。`SPQW_FRONTWALL_ImportCsv`(帳票 CSV 取り込み)は `incl_deg` 列を指定すれば引き続き傾斜杭を取り込める。

**杭上端標高 Z_head は前壁の内部表現そのもの**であり、入力値をそのまま `FrontWallRecord.HeadPoint` / XData(`head_x/_y/_z`)へ格納する。杭先端標高 Z_tip は `PileGeometry.TipFromHead(Z_head, L, θ)`(直杭のため `Z_tip = Z_head − L` の単純計算)によるソリッド生成・表示専用の計算値になった。範囲チェックはこの変換後の Z_tip に対して行う。旧図面(`tip_x/_y/_z` のみを持つ)は読み込み時に Z_head へ自動変換する。

**`_Create` の壁一括生成**(`FrontWall.WallLayout`)

| 派生量 | 計算式 | 既定値での結果 |
|---|---|---|
| 本数 | `ceil((施設全長 − 0.001) ÷ 有効幅)` | 10.000 ÷ 0.8752 = 11.426 → **12 本**(既定の施設全長 100m では 115 本) |
| 実延長 | `本数 × 有効幅` | 12 × 0.8752 = **10.502 m**(+0.502 m 超過) |
| 各本の Y | `始点Y + (施工順位 − 1) × 有効幅` | 0.000, 0.875, …, 9.628 |

端数は**切り上げ**(施設全長を必ずカバーするが終点は行き過ぎる)。誤差許容 1mm を差し引いてから割るため、ちょうど整数倍のとき(8.752 ÷ 0.8752 = 10)に浮動小数誤差で 1 本増えることはない。本数が 500 本(`PieceAssignment.PieceCount_Max`)を超える組合せは**エラー停止**する(施設全長を分割して生成する)。

有効幅 B は入力値を優先する。外径・継手形式から算出される値と 1mm を超えて食い違う場合は**警告を表示して続行**する(エラー停止しない)。**実際に確定した B は XData(`effective_width`)に記録され、`SPQW_TIEROD_Create` / `SPQW_ANCHORPILE_Create` / `SPQW_QUAYWALL_Estimate` はこの値を使う**(`FrontWallRef.ResolveEffectiveWidth`)ため、カスタム B のままタイロッド・控え杭・施設積算まで一貫して整合する。旧図面(`effective_width` キーが無い)は算出値にフォールバックする。

`SPQW_FRONTWALL_Action` は 1 本の再生成であり、総本数・施工順位を明示入力する(施設全長・有効幅の入力は無いため対象外)。

### 5.2 打設歩掛積算

外径・肉厚・全長・継手形式・総本数は選択した前壁の XData から読むため入力不要。杭 1 本当り質量は本管 + 継手金物として自動算出する。

**打撃工法(`SPQW_FRONTWALL_Estimate`、4節 3-4.5)**

| 英語名 | 日本語名 | 単位 | デフォルト値 | 範囲 |
|---|---|---|---|---|
| `site` | 施工区分 | − | 海上 | 陸上 / 海上 |
| `penetration` | 根入れ長 | m | 全長 × 0.5 | 0.1〜全長 |
| `pileCount` | 打設本数 | 本 | 総本数 | 1〜500 |
| `nTip` / `nAvg` | 先端 N 値 / 加重平均 N 値 | − | 50 / 20 | 1〜100 |
| `jointCountPerPile` | 継杭の継手個所数 | 箇所 | 0 | 0〜5 |
| `seaCondition` | 海象条件(海上のみ) | − | 普通 | 普通 / 悪い |
| `obstacle` | 障害の有無 | − | なし | なし / あり |
| `needCrawlerCrane` | クローラクレーン(小運搬用)の計上(陸上のみ) | − | しない | する / しない |
| `needTugBoat` | 引船の計上(海上のみ。現場条件により杭打船の移動が必要な場合。3-4.5-15 注1) | − | しない | する / しない |
| `needDiverVessel` | 潜水士船の計上(海上のみ、`obstacle` とは別の判断軸) | − | しない | する / しない |

**振動工法・バイブロ単独(`SPQW_FRONTWALL_VibroEstimate`、16節 3-2)**

施工区分は尋ねない(適用範囲が海上打設のみのため)。

| 英語名 | 日本語名 | 単位 | デフォルト値 | 範囲 |
|---|---|---|---|---|
| `driveLength` | 打設長 Lb | m | 全長 | 1〜80(表層から連続する N=0 区間は除く) |
| `pileCount` | 打設本数 | 本 | 総本数 | 1〜500 |
| `nTip` / `nAvg` | 先端地盤 N 値 / 周辺地盤の加重平均 N 値 | − | 50 / 20 | 1〜100 |
| `jointCountPerPile` | 継杭の継手個所数 | 箇所 | 0 | 0〜5 |
| `seaCondition` | 海象条件 | − | 普通 | 普通 / 悪い |
| `obstacle` | 障害の有無 | − | なし | なし / あり |
| `needDiverVessel` | 潜水士船の計上(打設個所の障害物・打設後異常の調査作業) | − | しない | する / しない(`obstacle` とは別の判断軸) |

**振動工法・ジェット併用(`SPQW_FRONTWALL_VibroJetEstimate`、16節 3-1)**

| 英語名 | 日本語名 | 単位 | デフォルト値 | 範囲 |
|---|---|---|---|---|
| `site` | 施工区分 | − | 海上 | 陸上 / 海上 |
| `operatingHours` | 1 日当り運転時間 T | h/日 | 陸上 8.0 / 海上 6.0(固定) | 1〜24 |
| `driveLength` | 打込長 ℓ | m | 全長 | 1〜80 |
| `liftLength` | 吊込 1 回ごとの杭長 L₀ | m | 全長 | 1〜80 |
| `liftCount` | 杭の吊込み回数 nₛ | 回 | 1 | 1〜10 |
| `pileCount` | 打設本数 | 本 | 総本数 | 1〜500 |
| `soilType` | 土質 | − | 砂質土･レキ質土 | 砂質土･レキ質土 / 粘性土 / 玉石混りレキ / 固結土 / 岩盤 |
| `nAvg` | 加重平均 N 値 | − | 30 | 1〜100 |
| `maxCobble` | 最大玉石径(玉石混りレキのみ) | mm | 100 | 76〜200 |
| `qu` | 加重平均一軸圧縮強度(岩盤のみ) | N/mm² | 10.0 | 0.1〜29.4 |
| `hasChuck` | 鋼管チャックの装備 | − | あり | あり / なし(なしは A₀ を 1.3 で除す) |
| `jointLength` | 継手の長さ ℓj(ε 算定用) | m | 全長 | 0〜80 |
| `jetCount` | **ジェット使用台数** | 台 | 2 | 1〜4(**§9-1 参照。基準の表は要確認**) |
| `nozzleCount` | **噴射ノズル数** | 個 | 6 | 1〜20(**§9-1 参照。同上**) |
| `needWaterSupply` | 水中ポンプ・水槽の計上 | − | しない | する / しない |
| `jointCountPerPile` | 継杭の継手個所数 | 箇所 | 0 | 0〜5 |
| `seaCondition` | 海象条件(海上のみ) | − | 普通 | 普通 / 悪い |
| `obstacle` | 障害の有無 | − | なし | なし / あり |
| `needDiverVessel` | 潜水士船の計上(海上のみ、`obstacle` とは別の判断軸) | − | しない | する / しない |
| `vibroMass` | バイブロハンマ質量 Wv(鋼管チャック込み) | t | 10.0 | 0.1〜100 |

**控え杭・打撃工法・陸上打設(`SPQW_ANCHORPILE_Estimate`、4節 3-4.6)**

外径・肉厚・全長・傾斜角は選択した控え杭の XData から読むため入力不要。施工区分は尋ねない(本コマンドは陸上打設のみ)。

| 英語名 | 日本語名 | 単位 | デフォルト値 | 範囲 |
|---|---|---|---|---|
| `penetration` | 根入れ長 | m | 全長 × 0.5 | 0.1〜全長 |
| `pileCount` | 打設本数 | 本 | 1 | 1〜500 |
| `nTip` / `nAvg` | 先端 N 値 / 加重平均 N 値 | − | 50 / 20 | 1〜100 |
| `jointCountPerPile` | 継杭の継手個所数 | 箇所 | 0 | 0〜5 |
| `obstacle` | 障害の有無 | − | なし | なし / あり |
| `needCrawlerCrane` | クローラクレーン(小運搬用)の計上 | − | しない | する / しない |

### 5.3 タイロッド

008 の 18 項目のうち、鋼種・設計基準・荷重状態・腹起し溝形鋼高さ・定着プレート厚・定着ワッシャー厚・ナット高さ・調節長・取付点反力 Ap の 9 項目はプロンプトを廃止した。海側鋼管矢板径・矢板ピッチは選択した前壁から既定値を提示する。

| 英語名 | 日本語名 | 単位 | デフォルト値 | 範囲 |
|---|---|---|---|---|
| `frontWallSelection` | 基準とする前壁 | − | − | `SPQW_FRONTWALL` XData を持つ Solid3d |
| `rodDiameter` | タイロッド径 | m | 0.048 | 0.020〜0.100(カタログ規格径 φ25〜φ90 の 19 種のみ可) |
| `spanLength` | 法線直角方向延長 span | m | 10.000 | 3.000〜40.000 |
| `pileDiameter` | 海側鋼管矢板径 | m | **前壁から自動代入(入力を求めない)** | 前壁の外径と一致 |
| `pilePitch` | 鋼管矢板ピッチ | m | **前壁から自動代入(入力を求めない)** | 前壁が壁一括生成で実際に使った有効幅 B(`FrontWallRef.ResolveEffectiveWidth`)と一致。旧図面は外径・継手形式からの算出値にフォールバック |
| `everyNPiles` | タイロッド取付間隔(矢板何本ごと) | 本 | 1 | 1〜50、かつ間隔が 0.600〜20.000 m |
| `tieSpacing` | タイロッド取付間隔 | m | **`pilePitch × everyNPiles` で自動算出** | 派生量(ピッチの整数倍が構造的に保証される) |
| `tieCount` | 組数 | 組 | **前壁の総本数と `everyNPiles` から自動算定(入力を求めない)** | `TieRodPitch.CountFor(pieceCount, everyNPiles)` = `(pieceCount-1)/everyNPiles + 1`(1 本目に配置し以降 n 本ごと) |
| `hwl` | H.W.L. 標高 | m(D.L.) | 2.000 | 0.000〜5.000 |
| `tieElevation` | タイロッド軸心標高 | m(D.L.) | `hwl` + 0.500 | −5.000〜10.000 |
| `layerColor` | 色 | ACI | 8 | 1〜255 |
| `positionY` | 1 組目の位置 Y | m | − | UCS ピック(**X は前壁から自動計算**。`_Action` では保存値を保持) |

`span_length` は「前壁矢板中心 〜 陸側定着面」の水平距離(積算基準 3-4.5-(13))。定着金物はこの面より陸側へ張り出す。

**プロンプト廃止後も内部の計算式(全長・質量・張力照査)は変更していない**。廃止した 9 項目は `TieRodParameters` のプロパティとしては残り、`SPQW_TIEROD_Create` では以下の固定値、`SPQW_TIEROD_Action` では前回保存値(XData)がそのまま計算に使われる。

| 項目 | 固定値(`_Create`) | 備考 |
|---|---|---|
| 鋼種 | HT690 | 張力照査(`AllowableTension`)に使用 |
| 設計基準 | PartialFactor(部分係数法) | 同上 |
| 荷重状態 | Normal(常時/永続) | 同上 |
| 腹起し溝形鋼高さ h | 0.300 m | 全長算出式(積算基準 3-4.5-(13))に使用 |
| 定着プレート厚 t2 | 0.025 m | 同上 |
| 定着ワッシャー厚 t1 | 0.006 m | 同上 |
| ナット高さ | 積算基準表(φ38〜φ65)から自動設定。表外径は 0.055 m | 同上。`ApplyStandardNutHeight()` は従来どおり動作 |
| 調節長 | 同上 | 同上 |
| 取付点反力 Ap | 0.0 kN/m(張力照査なし) | 張力照査要否を切り替える値。`_Create` では常に 0.0 |

### 5.4 控え杭

`SPQW_ANCHORPILE_Create` は前壁選択に加え、**代表となるタイロッドの選択**を求める。タイロッド軸心標高・配置間隔・本数はこのタイロッドの XData(`TieElevation`/`TieSpacing`/`TieCount`)からそのまま自動設定され、対応する入力プロンプトは廃止した。

| 英語名 | 日本語名 | 単位 | デフォルト値 | 範囲 |
|---|---|---|---|---|
| `frontWallSelection` | 基準とする前壁 | − | − | `SPQW_FRONTWALL` XData を持つ Solid3d |
| `tieRodSelection` | 配置間隔・本数・軸心標高の基準とするタイロッド | − | − | `SPQW_TIEROD` XData を持つ Solid3d(`_Create` のみ) |
| `outerDiameter` | 外径 D | mm(入力時呼称) | 800 | 318.5〜2500(JIS A 5525 標準径へスナップ) |
| `wallThickness` | 肉厚 t | mm(入力時呼称) | 12 | 外径別の K011 製造範囲 |
| `length` | 全長 L | m | 20.0 | 1〜80 |
| `inclinationDeg` | 傾斜角 θ | deg | 0.0 | 0〜15 |
| `closedTip` | 先端形状 | − | 開端 | 開端 / 閉端 |
| `span` | 法線直角方向延長 | m | 10.0 | 3.0〜40.0 |
| `tieElevation` | タイロッド軸心標高 Z_tr | m(D.L.) | **タイロッドから自動代入(入力を求めない)** | 選択したタイロッドの `TieElevation` と一致(`_Action` は前回保存値を保持) |
| `headElevation` | 杭上端(杭頭)標高 Z_head | m(D.L.) | **前壁の杭上端標高をそのまま使用**(控え杭自身の全長・傾斜角では算出しない) | 内部の Z_tip 換算値が −80〜10 |
| `colorIndex` | 本管の色 | ACI | 8 | 1〜255 |
| `everyNPiles` / `pileCount` | 控え杭 配置間隔・本数 | − | − | **タイロッドから自動代入(入力を求めない)**。選択したタイロッドの `TieSpacing`/`TieCount` をそのまま使う(`_Create` のみ) |
| `positionY` | 位置 Y | m | **図面内の全前壁のうち杭中心 Y が最小のもの(壁の 1 本目)に自動整列** | 派生量。2 本目以降は `始点Y + i × 配置間隔`(`_Action` は保存値を保持) |

控え杭は前壁と異なり**傾斜角プロンプトを維持**している(斜杭の需要があるため)。杭上端標高 Z_head 入力後の内部 Z_tip への変換は控え杭自身の全長・傾斜角を使う。

**タイロッド軸心標高 Z_tr は入力パラメータとして削除**。整列計算(`AnchorAlignment.Compute`/`Validate`)は引き続き Z_tr を使うため、`AnchorInput.TieElevM` フィールド自体は残っている。`_Create` は選択したタイロッドの `TieElevation` を自動代入し、`_Action` は再選択せず前回保存値(XData)をそのまま使う。

### 5.5 帳票 CSV 取り込み(列名は未確定。§9.1 参照)

対話入力の代わりに CSV の 1 行が上記パラメータ 1 件分に対応する。列名は別名リストで解決し、大文字小文字を無視する。

**前壁**(`SPQW_FRONTWALL_ImportCsv`。個別列が無い場合は「規格」列からの正規表現抽出にフォールバック)

| 列(別名) | 対応パラメータ | 必須 |
|---|---|---|
| `outer_d_mm` / `外径` | outerDiameter [mm] | ○(または規格列の `φNNN`) |
| `wall_t_mm` / `肉厚` | wallThickness [mm] | ○(または規格列の `×NN`) |
| `length_m` / `全長` / `L` | length [m] | ○(または規格列の `L=NN.N`) |
| `joint` / `継手形式` | jointType | −(既定 LT75。または規格列の LT65/LT75/LT100/PP/PT) |
| `grade` / `鋼種` | grade | −(既定 SKY400) |
| `incl_deg` / `傾斜角` | inclinationDeg | − |
| `piece_count` / `総本数`、`piece_index` / `施工順位` | pieceCount, pieceIndex | −(**両方無ければ CSV の総行数・出現順で自動採番**。壁一括生成の既定動作) |
| `color` / `色` | colorIndex | − |
| `tip_z` / `杭先端標高` | tipElevation [m] | ○(取り込み時に杭上端標高へ内部変換する) |

平面位置は CSV に持たせず、コマンド実行時に「1 本目の位置」を 1 回だけピックし、以降は各行の外径・継手から有効幅 B を計算して +Y へ自動配置する。

**タイロッド**(`SPQW_TIEROD_ImportCsv`。008 の 18 項目 + `pos_y` がすべて必須列)

| 列 | 対応パラメータ |
|---|---|
| `rod_d`, `grade`, `code`, `state`, `span_length`, `pile_d`, `pile_pitch`, `tie_spacing`, `tie_count`, `hwl`, `tie_elev`, `waling_h`, `plate_t`, `washer_t`, `nut_h`, `adjust_l`, `anchor_reaction`, `color` | §5.3 の同名パラメータ(単位は m。mm 境界は無い) |
| `pos_y` / `Y` / `位置y` | positionY [m](**必須**。前壁からの相対位置は自動計算できないため) |

**控え杭**(`SPQW_ANCHORPILE_ImportCsv`)

| 列(別名) | 対応パラメータ |
|---|---|
| `outer_d_mm` / `外径`、`wall_t_mm` / `肉厚`、`length_m` / `全長`、`incl_deg` / `傾斜角`、`closed_tip` / `先端形状`、`span` / `法線直角方向延長`、`tie_elev` / `タイロッド軸心標高`、`tip_elev` / `杭先端標高`、`color` / `色` | §5.4 の同名パラメータ |
| `pos_y` / `Y` / `位置y` | positionY [m](**必須**。省略すると全行が同一座標に重なるため) |

`closed_tip` は `1` / `閉端` / `true` / `closed` のいずれかを閉端と解釈する。前壁との整合(span の干渉チェック等)は前壁選択後にのみ検証できるため、取り込み時点では外径・肉厚・全長の単体範囲チェックのみ行う。

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

許容誤差は既定で比率 1%(`QuantityReconciliation.DefaultToleranceRatio`)。帳票値が 0 の項目は絶対差で判定する。

**サンプル CSV**([`docs/samples/`](docs/samples/))

各インポータ(`FrontWallCsvImporter` / `TieRodCsvImporter` / `AnchorPileCsvImporter`)を直接呼び出してエラー 0 件を確認済み。前壁 → タイロッド → 控え杭を**通しで使える組合せ**にしてある(前壁 D=800mm・LT75 → 有効幅 B=875.2mm、杭頭標高 +3.0m > タイロッド軸心標高 Z_tr=2.5m)。

| ファイル | 用途 | 検証結果 |
|---|---|---|
| `frontwall_import_minimal.csv` | 前壁・必須 4 列のみ。総本数/施工順位を省略し自動採番させる例 | 10 行、施設延長 8.752m、継手 1本目 +Y のみ / 2〜9本目 両側 / 10本目 −Y のみ |
| `frontwall_import_full.csv` | 前壁・全 10 列明示。肉厚・全長・杭先端標高が途中で変わる遷移区間の例 | 10 行、杭頭標高を +3.0m で揃え、先端を −18/−19/−20m と変化させる |
| `frontwall_import_spec.csv` | 前壁・「規格」列フォールバック(`φ800×12 L=21.0m LT75`) | 5 行。**杭先端標高だけは規格列から抽出できず別列が必須** |
| `tierod_import.csv` | タイロッド・必須 16 列 + 列挙型 3 列 | 3 組、Y=0 / 2.6256 / 5.2512m(取付間隔 = B×3)、前壁整合 OK |
| `anchorpile_import.csv` | 控え杭・必須 7 列(`pos_y` 含む)+ 任意 3 列 | 4 本、Y=0 / 2.6256 / 5.2512 / 7.8768m、杭面間浄距離 8.800m、前壁整合 OK |
| `boringlog.csv` | 柱状図(§5.6。Dynamo `CalcWeightedN` 用) | 5 層、出力は §4.9 例 4 の表と一致 |

**単位が部材ごとに異なる点に注意**。前壁・控え杭は外径・肉厚のみ mm で他は m、**タイロッドは全て m**(`rod_d` は `48` ではなく `0.048`)。タイロッドで mm 値を書くと `TieRodParameters.Validate` が「単位はメートルです(mm 値の混入に注意)」で停止する。

タイロッドの `pile_d` / `pile_pitch` は前壁の外径 / 有効幅 B と一致しなければ `CrossMemberValidator` で行ごと弾かれる(誤差許容 1mm)。`tie_spacing` は `pile_pitch` の整数倍であること。

控え杭の `pos_y` 列は**必須**。省略すると全行が同一座標に重なるため、エラー停止する。

### 5.6 柱状図 CSV(加重平均N値の算出、`SpqwNodes.CalcWeightedN`)

前壁・バイブロ単独・控え杭の各打設歩掛積算コマンドが個別に尋ねる `nAvg`(加重平均N値)を、柱状図データから算出するための入力形式。1 行 = 1 層。

| # | 列名(別名) | 日本語名 | 単位 | 必須 | 説明 |
|---|---|---|---|---|---|
| 1 | `layer_name` / `土層名` | 土層名 | − | 任意 | 記述名。表示用ラベルで計算には使わない |
| 2 | `soil_type` / `土質区分` | 土質区分 | − | ○ | `砂質土等` / `粘性土` / `玉石混りレキ` / `固結土` / `岩盤`(5区分、`FrontWall.JetLayerType` と一致) |
| 3 | `elevation_top` / `標高上端` | 標高上端 | m(D.L.) | ○ | Z軸鉛直上向きのため標高上端 > 標高下端 |
| 4 | `elevation_bottom` / `標高下端` | 標高下端 | m(D.L.) | ○ | |
| 5 | `thickness_m` / `層厚` | 層厚 | m | ○ | 標高上端−標高下端と一致することを検証(誤差許容1mm) |
| 6 | `n_value` / `N値` | N 値 | − | `岩盤`以外は○ | `岩盤`行では空欄可(一軸圧縮強度を使うため) |
| 7 | `blow_count` / `打撃回数法` | 打撃回数法 | 回 | 任意 | 50/60/70/80。N>50 の打止め行のみ。`penetration_cm` とセット必須 |
| 8 | `penetration_cm` / `貫入量` | 貫入量 | cm | 任意 | 換算N値 = 分子(50回法1500/60回法1800/70回法2100/80回法2400)÷ 貫入量。この値が生の `n_value` を上書きする |
| 9 | `qu_value` / `一軸圧縮強度` | 一軸圧縮強度 | N/mm² | `岩盤`のみ○ | `岩盤`以外の行では指定不可(土質区分の誤り検出) |

```csv
土層名,土質区分,標高上端,標高下端,層厚,N値,打撃回数法,貫入量,一軸圧縮強度
埋土,砂質土等,0.0,-2.0,2.0,3,,,
沖積粘土層,粘性土,-2.0,-5.0,3.0,8,,,
洪積砂質土層,砂質土等,-5.0,-10.0,5.0,22,,,
洪積砂礫層,砂質土等,-10.0,-15.0,5.0,55,50,3.3,
軟岩層,岩盤,-15.0,-20.0,5.0,,,,4.2
```

CSV の行順は問わない(標高上端の降順に自動で並べ替えたうえで、地表からの連続性を検証する)。

上記の例をそのまま [`docs/samples/boringlog.csv`](docs/samples/boringlog.csv) に置いてある(出力は §4.9 例 4 の表)。Dynamo からの使い方は §4.10 を参照。
