# 009_sheet-pile-quaywall 実装計画

本ドキュメントは提案・設計計画であり、**実装(C# コード生成)は行わない**。実装に着手する際はこの計画を土台に CLAUDE.PRIVATE.md §6 の固定順(計画→テスト→参照アセンブリ/コマンド→パラメータ表→派生量表→整合性チェック→コード→注意点→.gitignore影響)で進める。

> **改訂履歴**
> - 2026-07-25 初版
> - 2026-07-25 第2版: 批判的レビューの指摘を反映 — ① 006 の鋼管杭純化(`6a777b1`)に伴う移植元コミット固定(決定 5 改訂・§0・§11)、② 継手 DXF/JSON 非コピーの徹底(決定 3 改訂・§3・§11 の矛盾解消)、③ 単位入出力方針の明確化(決定 7 新設・§7.1)、④ `JointParameters.cs` の補完(§3・§10・§11)、⑤ 旧 RegApp 図面互換を §12 に追加。
> - 2026-07-25 第3版: 実装フェーズ計画を §13 として新設(旧 §13 注意点は §14 へ)。開発環境の実測(WSL に .NET SDK 10.0.302 あり / AutoCAD なし)に基づきフェーズ順を決定し、フェーズ 0(骨格)・1(移植 Core、275 テスト green)を完了。§12 に未解決事項 6(θ 付き前壁とタイロッドの幾何整合)を追加。
> - 2026-07-25 第3版 (b): フェーズ 2(006@6d6d8cf 由来の新規 Core)完了、311 テスト green。§3 に `Point3.cs`・`PileGeometry.cs`・`AnchorPileSteel.cs` を追加。§12 に未解決事項 7(継手質量の側別配分)・8(前壁と控え杭で外径規則が異なる)を追加。
> - 2026-07-26 第3版 (c): §12 未解決事項 1・6(タイロッドの前壁参照方式、θ 付き前壁との幾何整合)を決定8として解決。タイロッドを目視クリックから前壁選択方式へ変更し、海側取付点の X 座標を `PileGeometry.AxisXAt` で自動計算する(§1・§4・§6・§7.2 を更新)。フェーズ3のブロッカーが解消し、残る未解決は項目2〜5・7・8のみ。
> - 2026-07-26 第3版 (d): フェーズ 3(部材間整合)完了、323 テスト green。`CrossMemberValidator`(§9 に横断チェック 4 組を追加)と `TieRodPlacement`(決定8)を新設。`FrontWallRef` を Core ルートへ移動し継手形式を追加。008 `SpanLength` の XML コメント(「控工中心まで」)が README の図・算定式・006 の定義と矛盾することを §9 に記録。
> - 2026-07-26 第3版 (e): フェーズ 4 のブロッカーを一括解決 — 決定9(XData を 008 の キー=値 + `fmt` 方式に統一。§6.1 新設、位置依存とする旧記載を差し替え)、決定10(`_JointModel` は移植しない。コマンド 12→11 個)、決定11(旧 RegApp 図面との互換は持たない)。§12 の項目 2・4・5 が解決し、フェーズ 4 のブロッカーは無くなった。残る未解決はフェーズ 5 の項目 3・7 と、記録のみの項目 8。
> - 2026-07-26 第3版 (f): フェーズ 4(Plugin)完了。XData 3 種(決定9 のキー=値方式)+ コマンド 11 個 + 共通ヘルパー 3 種を実装し、スタブビルドがエラー 0・警告 0。スタブに Polyline/Circle/Region/DBObjectCollection/Point2d/Handle/BooleanOperationType 等を追加。§3・§6 を実装に合わせて更新し、§13.5 に実機手動検証項目 8 件を追加。
> - 2026-07-26 第3版 (g): フェーズ 5(仕上げ)完了、335 テスト green。§12 項目3(Dynamo 範囲)を決定、項目7(継手質量の側別配分)を `JointShapes` の実形状から解決し、**移植元 007 `JointCatalog.JointMassPerM` が P-P 形で鋼管を 1 本分しか数えないバグを発見**。`FrontWall.JointMass`・`QuayWallEstimate`・`SPQW_QUAYWALL_Estimate`・Dynamo ノード 2 個・README(9 章構成)を追加。残る未解決は記録のみの項目 8。
> - 2026-07-26 第3版 (o): **打撃工法の杭打機・杭打船選定と、打設歩掛積算 4 系統の Dynamo ノード化を追加**、603 テスト green。①基準原文 3-4.5-14〜15/3-4.6-12〜13「作業船・機械の選定」を確認し、振動工法には実装済みだった主船選定が打撃工法に無いことが判明したため `Core.FrontWall.DriveEquipment` を新設(`DriveEstimate.cs` は port-from-legacy.sh の同期対象のため直接変更しない回避パターン)。陸上=クローラ式杭打機 3 ランク(4〜4.5t/6.5〜8t/10〜12.5t。**ハンマ15.0tは陸上打設の表に行が無く表外**)+条件計上のクローラクレーン(50t吊)、海上=杭打船(H-65/H-125/H-150)+付帯船舶(`VibroEstimate` の表を再利用。原文注記で同一表と確認)。対応はセル結合位置からの読み取りで信頼度は推定(README §9.1 の 7)。**引船は「現場条件による追加船団」**(注1「杭打船の移動が必要な場合は計上」)のため `needTugBoat` の独立プロンプトで分離。ラベル転記の乖離リスクは `GetHammerClass` 実戻り値を通す連結テスト T1259 で検出する。②打設歩掛 4 系統(前壁の打撃/バイブロ単独/ジェット併用、控え杭の打撃)を Dynamo ノード化(`CalcFrontWallDriveEstimate`/`CalcVibroEstimate`/`CalcVibroJetEstimate`/`CalcAnchorPileDriveEstimate`。ノード数 3→**7**。§12 項目 3 の旧決定「2 個」を拡張)。コマンドの対話フローを移植し、XData 由来の値は明示引数・エラー中断は ArgumentException に置換。③別セッションの批判的レビュー指摘を反映(杭打機 3 ランク化・引船条件計上・nozzleCount 未使用の解消・質量キーの単位統一・`SPQW_FRONTWALL_Estimate` の Q≦0 ガード追加・jetCount 範囲例外・プロンプト文言統一)。
> - 2026-07-26 第3版 (n): **柱状図から加重平均N値を算出する Dynamo ノードを追加**、585 テスト green。前壁(打撃・バイブロ単独)・控え杭(打撃)・ジェット併用(γ)の各打設歩掛コマンドが個別に尋ねる「加重平均N値」を、柱状図 CSV(標高上端/標高下端/層厚/N値/土質区分5区分/打止め換算用の打撃回数法・貫入量/岩盤の一軸圧縮強度)から算出できるようにした。基準原文の突き合わせにより、貫入抵抗値R用のN_avgは「表層から連続するN=0の区間のみ」除外、打撃速度Sb用のN_avgは「表層から連続するN≦5の区間」除外と、**同じ「加重平均N値」でも式によって除外ルールが異なる**ことを確認し、除外しきい値をパラメータ化した単一の計算関数(`Geotech.BoringLogAnalysis.CalcWeightedN`)で両方に対応した。土質区分は既存の `FrontWall.JetLayerType`(5区分)をそのまま流用し、ジェット併用のγ・A0計算に必要な土質区分別の加重平均N値・岩盤の加重平均一軸圧縮強度も同じ柱状図から算出できる。ジオメトリ・AutoCADトランザクションを伴わない純計算のため、AutoCADコマンドではなくDynamoノード(`SpqwNodes.CalcWeightedN`、ノード数2→3)として実装し、新規 `Core.Geotech` 名前空間を新設した。1行の不備でも計算全体を止める設計(既存の帳票CSV取り込みコマンドの部分許容方針とはあえて変えた。地盤入力で部分的な値のまま計算を進めると設計判断を誤るため)。岩盤層はR/Sb用の加重平均からは常に除外し(基準に明記が無いため推測せず除外・件数報告に留めた)、土質区分別の加重平均には除外ルールを適用しない(同じく基準に明記が無いため)。
> - 2026-07-26 第3版 (m): **振動工法(バイブロ単独・ジェット併用)の付帯船舶を追加**、556 テスト green。従来 `SPQW_FRONTWALL_VibroEstimate` は起重機船・杭打船のトン数までしか選定しておらず、台船・引船・揚錨船・潜水士船は固定文字列の注記に留まっていた。基準原文(4節 3-4.6-6「２－３－２－２ 作業船・機械の組合せ」)を確認し、台船・引船を積載物の長さ(杭の全長)から選定する `FrontWall.VibroEstimate.GetBargeAndTug` を新設(28m未満→鋼300t積/鋼D450PS型 〜 39〜44m未満→鋼1,000t積/鋼D600PS型、44m以上は基準に規定なし)。揚錨船(鋼D 5t吊)・潜水士船(D270PS型 3〜5t吊)は規格が固定であることを確認し定数化。潜水士船の計上要否は既存の `obstacle`(障害区分能力補正係数)とは別の判断軸(打設個所の障害物・打設後異常の調査作業の要否、3-16-29 注2)であるため独立したプロンプトを追加した。`SPQW_FRONTWALL_VibroJetEstimate` にも海上打設時のみ同じ規格表(3-16-18 注2 で 16節3-1・3-2 共有を確認済み)を適用。メイン船(クレーン付台船・起重機船)のトン数ランク選定はジェット併用側に対応表が見当たらず未実装のまま(必要吊上げ荷重 Cf の数値のみ)。
> - 2026-07-26 第3版 (l): **控え杭の打撃工法・打設歩掛積算を陸上打設限定で追加**、542 テスト green。控え杭は継手を持たない単独の鋼管杭であり、前壁が拠る 4節 3-4.5「鋼矢板式」ではなく **4節 3-4.6「鋼杭式」**(3-4.6-9〜17)が正しい出典であることを原文突き合わせで確認。貫入抵抗値 R・ハンマ規格決定図・打撃速度 Sb 表・溶接時間表・準備時間 Tp・基準作業能力係数(ei・E1〜E3)は 3-4.5 と数値まで完全一致するため既存 `FrontWall.DriveEstimate` の該当メソッドをそのまま再利用し、実質的な差である**打撃時間 Tb の係数 K(直杭1.0/斜杭1.2。3-4.5 は斜杭の値を定義しない)**と**労務編成**のみ `Core.AnchorPile.AnchorDriveEstimate` に新規実装した。控え杭は `AnchorInput.InclDeg` を持ち傾斜杭が実務上あり得るため K 係数は無視できない差異。副産物として、既存 `FrontWall.DriveEstimate.GetLabor`(前壁)が実際の労務編成表(陸上打設も杭長 20m で 2→3 人・1→2 人と変化する)と食い違うバグを発見(陸上側を一律固定していた)。前壁側の修正は本件のスコープ外として記録に留め、控え杭側は正しい表で実装した(README §9.3 参照)。コマンド `SPQW_ANCHORPILE_Estimate` を新設(18→19)。海上打設(3-4.6-11〜16)は未実装。
> - 2026-07-26 第3版 (k): **帳票 CSV 取り込みを追加**、524 テスト green。サーチマス等の積算ソフト出力を CSV UTF-8 で保存したものから前壁・タイロッド・控え杭のパラメータを一括入力する `Core.Import` 名前空間(CsvTable・SpecTextParser・FrontWallCsvImporter・TieRodCsvImporter・AnchorPileCsvImporter・QuantityReconciliation、6 ファイル)と `Plugin.Commands.ImportCommands`(4 コマンド。14→18)を新設。前壁 CSV は §9.2 にあった「壁一括生成が無い」を解消(直線配置・累積有効幅による自動 Y 配置、施工順位が無ければファイル総行数と出現順で自動採番)。1 行の不備で取り込み全体を止めない設計(行番号付きエラー一覧 + 残り行は生成)。`SPQW_QUAYWALL_ReconcileCsv` は帳票の数量・質量と 009 計算値を許容誤差 1% で突合する。既存 `TieRodCommands.BuildSolid` / `AnchorPileCommands.BuildSolid` を `private→internal` にしてソリッド生成ロジックの重複を避け、`QuayWallCommands.Estimate()` から `BuildCompositionFromPrompts` を抽出して突合検証と共有した(挙動は変えない純粋な抽出)。**実際のサーチマス等のエクスポート列名・レイアウトは未確認**であり、列名は別名リストで解決する設計とした(README §9.1 に記録)。
> - 2026-07-26 第3版 (j): **振動工法・ジェット併用の打設歩掛積算を追加**、475 テスト green。積算基準 3章16節 3-1(3-16-11〜25)から `FrontWall.VibroJetEstimate` を新設し、`SPQW_FRONTWALL_VibroJetEstimate` コマンド(13→14 個)を追加。規格選定の基礎が 3-2 と根本的に異なり(**必要偏心モーメント K₀ = A₀×Wp×98**)、陸上/海上とも適用できる。基本振幅係数 A₀ 表と 1m 当り打込み時間 γ 表は土質のくくり方が異なる(粘性土は A₀ では砂質土等、γ では γ₃ 側)ため、`JetLayerType` で受けて両表へ振り分ける。**噴射ノズル数・ジェット使用台数の表(3-16-16)はセル結合により OCR 復元不能**のため推測せず利用者入力とした。配管系部材・導材・拘束費は別代価表につき範囲外。
> - 2026-07-26 第3版 (i): **振動工法(バイブロハンマ)の打設歩掛積算を追加**、382 テスト green。積算基準 3章16節 3-2(3-16-26〜31)から `FrontWall.VibroEstimate` を新設し、`SPQW_FRONTWALL_VibroEstimate` コマンド(12→13 個)を追加。4節 3-4.5 の注記「バイブロハンマによる場合は 16節 仮設工を適用することができる」に従い、打撃工法(`DriveEstimate`)とは別モジュール・別コマンドとして実装した。**鋼管矢板には継手の貫入抵抗 Rj = R1×10⁻¹ が加算される**点が打撃工法との実質的な差(3-16-29)。適用範囲は海上打設のみで、ウォータージェット併用(16節 3-1)は範囲外。
> - 2026-07-26 第3版 (h): 実装後の批判的レビュー指摘を反映 — ① 前壁挿入点を 1011(World 座標点)で併記保存し読み側で優先(決定9 でキー=値の文字列化により失われていた MOVE 追随を復活。§6・§6.1)、② タイロッドの鋼種・設計基準・荷重状態プロンプトを追加(008 `TryGrade`/`TryCode`/`TryState` 相当の移植漏れ修正)し、積算基準表内の径(φ38〜φ65)はナット高さ・調節長を自動設定、③ タイロッドのプロンプト範囲 5 項目を Core 検証範囲に一致、④ §4 に `SPQW_QUAYWALL_Estimate` を追加・Dynamo ノード名を実装(`CalcSection`・`CalcQuayWallQuantity`)に更新・`ProtoGeometry.dll` の参照記載を削除、⑤ `SPQW_ANCHORPILE_Query` に積算数量出力(1 本あたり)を復元(006 同等)、⑥ `SPQW_TIEROD_Action` は Y を保持して同位置再生成(§2.4 整合)。

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
| 8 | タイロッドの前壁参照方式(第3版(c)新設、§12 未解決事項 1・6 を解決) | `SPQW_TIEROD_Create` を目視クリックから**前壁選択方式**に変更する(控え杭 `SPQW_ANCHORPILE_Create` と同じパターン)。海側取付点の X 座標は、前壁 XData から読んだ杭先端標高 `Z_tip`・傾斜角 `θ` と `PileGeometry.AxisXAt(前壁tip, θ, tie_elevation)` から自動計算する。ユーザーが行う入力は施設延長方向の位置(Y)と前壁の選択のみで、平面 X 座標の目視ピックは廃止する | 008 の計算層(`TieRodCalculator.SeaEndX`/`LandEndX`)は「X=0 が海側鋼管矢板の中心軸」を高さによらず一定と仮定しており、前壁に傾斜角 θ(0〜15°)を導入すると成立しない(θ=15°・標高差 20.5m でずれ量 約5.5m)。目視クリックでは傾斜杭の軸位置を正確に指すことが困難なため自動計算に置き換える必要がある。`AxisXAt` は控え杭(フェーズ2 `AnchorAlignment`)で実装・検証済みの関数を再利用でき、006 の控え杭が確立した「前壁 XData 連携による自動整列」のパターンとも一貫する |
| 9 | XData のエンコード方式(第3版(e)新設) | 3 部材とも **008 の「`キー=値` の ASCII 文字列 + `fmt` バージョン」方式**に統一する。詳細と根拠は §6.1 | 007・006 は位置依存で「順序変更禁止」の制約を永続的に負うが、008 の方式は順序非依存で項目追加が非破壊。008 で実装・運用済みの実績がある |
| 10 | `SPQW_FRONTWALL_JointModel` の削除(第3版(e)新設、§12 未解決事項 2 を解決) | 007 `SPSP_JointModel`(245 行)は 009 へ移植しない。コマンドは 12 個 → **11 個**になる | `SPSP_Create` は既に継手 A 側・B 側を `BoolUnite` で一体化しており、`_JointModel` は隣接 2 本をピッチ B で並べて嵌合を目視確認する検証ツールである。009 は `JointShapes.cs` を 007 から無変更コピーする方針(決定 3)で実形状は 007 側で検証済みのため、検証ツールとしての価値が低い。009 の `_Create` は `PieceAssignment` で継手側を判定するため、`pieceIndex=1/2` の 2 本を作れば同等の嵌合ペアが得られる |
| 11 | 旧 RegApp 図面の互換方針(第3版(e)新設、§12 未解決事項 5 を解決) | 006/007/008 で作成した既存図面(`STEELPIPEPILE` / `SPSP` / `TAIROD_PARAM` / `ANCHORPILE`)との**互換は持たない**。旧図面は旧プラグインで扱うか、009 で再作成する | 006 が `6a777b1` で XData を 11→8 要素に変更した際、既に「旧図面は再作成が必要」と割り切っており、`ANCHORPILE` 図面は現時点でどのプラグインからも操作できない。009 で救済する場合も XData は読むだけであり、必要が生じた時点で変換コマンドを後から追加できる(フェーズ 4 の作業量を先に増やす理由がない) |

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
│   │   ├── Point3.cs                        【第3版追加】3次元点(AutoCAD Point3d の代替)
│   │   ├── PileGeometry.cs                  【第3版追加】傾斜杭の共通幾何(前壁・控え杭で共用。006 BuildPileSolid の変換部分)
│   │   ├── FrontWallRef.cs                  【フェーズ3追加】前壁の参照情報(控え杭・タイロッド・部材間整合が共用。フェーズ2では AnchorPile 配下にあったものを移動し継手形式を追加)
│   │   ├── CrossMemberValidator.cs          【フェーズ3追加】部材間の横断チェック 4 組(§9)
│   │   ├── FrontWall/                       前壁鋼管矢板(007 Core 移植 + 006 ロジック抽出)
│   │   │   ├── JointCatalog.cs              継手部材諸元(007 そのまま移植)
│   │   │   ├── JointParameters.cs           JointType enum・有効幅ディスパッチ(007 そのまま移植。第2版で補完 — 他の FrontWall 全ファイルがこの enum に依存し、これ無しではコンパイル不能)
│   │   │   ├── JointGeometry.cs             継手有効間隔 J・有効幅(007 そのまま移植)
│   │   │   ├── JointShapes.cs               実形状断面(007 ファイルをそのままコピー、決定3)
│   │   │   ├── JointPlacement.cs            配置変換(007 そのまま移植)
│   │   │   ├── SectionProperties.cs         断面性能(007 そのまま移植)
│   │   │   ├── DriveEstimate.cs             打設歩掛積算(007 そのまま移植)
│   │   │   ├── PieceAssignment.cs           【新規】施工順位→継手要否(006 ロジック抽出)
│   │   │   ├── FrontWallPlacement.cs        【新規】挿入点の組立(§2.2)と傾斜角・杭先端標高の範囲検証。幾何は PileGeometry に委譲
│   │   │   └── InputValidator.cs            統合入力検証(007 のまま。mm 混入は既存の範囲チェックが検出する)
│   │   ├── TieRod/                          タイロッド(008 TaiRod.Core そのまま移植)
│   │   │   ├── TieRodCatalog.cs
│   │   │   ├── TieRodParameters.cs
│   │   │   ├── TieRodCalculator.cs
│   │   │   ├── TieRodResult.cs
│   │   │   ├── TieRodPlacement.cs           【フェーズ3追加】海側取付点の自動計算(決定8。目視クリックの置換)
│   │   │   └── Enums.cs
│   │   ├── AnchorPile/                      控え杭(006 ANCHORPILE ロジック抽出)
│   │   │   ├── AnchorAlignment.cs           整列計算(前壁軸 + span → 控え杭軸、§2.2 のD.L.統一を反映)+ 整合性チェック
│   │   │   ├── AnchorPileSteel.cs           【第3版追加】JIS A 5525 標準径・K011 径別肉厚範囲・JIS スナップ(006 から抽出。前壁の範囲とは異なる)
│   │   │   ├── AnchorInput.cs               入力パラメータ(前壁の参照情報は Core ルートの FrontWallRef へ移動)
│   │   │   └── AnchorResult.cs
│   │   └── SheetPileQuayWall.Core.csproj    net8.0、AutoCAD 参照なし(CLAUDE.PRIVATE.md §9 対象外)
│   │
│   └── SheetPileQuayWall.Plugin/          ← AutoCAD / Civil3D / Dynamo 依存
│       ├── Commands/
│       │   ├── FrontWallCommands.cs         SPQW_FRONTWALL_Create / _Action / _Query / _Estimate
│       │   ├── TieRodCommands.cs            SPQW_TIEROD_Create / _Action / _Query / _Color(決定8 の前壁選択方式)
│       │   └── AnchorPileCommands.cs        SPQW_ANCHORPILE_Create / _Action / _Query
│       ├── XData/
│       │   ├── XDataStore.cs                キー=値 の読み書き基盤(決定9、§6.1)
│       │   ├── FrontWallRecord.cs           RegApp SPQW_FRONTWALL
│       │   ├── TieRodRecord.cs              RegApp SPQW_TIEROD(前壁 Handle 参照を持つ)
│       │   └── AnchorPileRecord.cs          RegApp SPQW_ANCHORPILE(同上)
│       ├── DrawingHelper.cs                 レイヤー作成・モデル空間追加・選択・Handle 解決
│       ├── Prompt.cs                        対話入力(mm→m 変換の境界。決定7)
│       ├── SolidBuilder.cs                  断面→Region→Extrude(継手部材の押し出しを含む)
│       ├── Dynamo/                          【フェーズ5】SpqwNodes.cs
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
| `SPQW_FRONTWALL_Estimate` | `SPSP_Estimate`(007) | **打撃工法**の打設歩掛積算(貫入抵抗・ハンマ選定・労務編成。4節 3-4.5) |
| `SPQW_FRONTWALL_VibroEstimate` | (新設。旧版に相当なし) | **振動工法(バイブロ単独)**の打設歩掛積算(鋼材質量+貫入抵抗値で規格選定。16節 3-2、海上打設のみ。第3版(i)) |
| `SPQW_FRONTWALL_VibroJetEstimate` | (新設。旧版に相当なし) | **振動工法(ジェット併用)**の打設歩掛積算(必要偏心モーメント K₀ で規格選定。16節 3-1、陸上/海上とも。第3版(j)) |
| ~~`SPQW_FRONTWALL_JointModel`~~ | ~~`SPSP_JointModel`(007)~~ | **移植しない(決定10)**。`_Create` を `pieceIndex=1/2` で 2 回実行すれば同等の嵌合ペアが得られる |
| `SPQW_TIEROD_Create` | `TAIROD_Create`(008)+ 前壁選択方式へ変更(決定8) | 前壁選択 → θ 付き軸位置を自動計算 → 組数分の Solid3d 生成、XData 記録(旧: 目視クリックのみ) |
| `SPQW_TIEROD_Action` | `TAIROD_Action`(008)+ 前壁Handle追随(決定8) | 選択1本を、前壁の現在の位置・θ・Z_tip に基づき再計算した軸位置で再生成 |
| `SPQW_TIEROD_Query` | `TAIROD_Query`(008) | 諸元・張力照査・受杭数量を出力 |
| `SPQW_TIEROD_Color` | `TAIROD_Color`(008) | 色番号のみ変更 |
| `SPQW_ANCHORPILE_Create` | `ANCHORPILE_Create`(006) | 前壁選択 → タイロッド軸線に整列した控え杭を生成 |
| `SPQW_ANCHORPILE_Action` | `ANCHORPILE_Action`(006) | 前壁基準の整列位置に再生成 |
| `SPQW_ANCHORPILE_Query` | `ANCHORPILE_Query`(006) | 諸元・整列座標・積算数量(1 本あたり)を出力 |
| `SPQW_ANCHORPILE_Estimate` | (新設。旧版に相当なし) | **打撃工法**の打設歩掛積算(4節 3-4.6 鋼杭式、**陸上打設のみ**。第3版(l)) |
| `SPQW_ANCHORPILE_VibroEstimate` | (新設。旧版に相当なし) | **振動工法・バイブロ単独**の打設歩掛積算(16節 3-2、**海上打設のみ**。2026-08-01) |
| `SPQW_ANCHORPILE_VibroJetEstimate` | (新設。旧版に相当なし) | **振動工法・ジェット併用**の打設歩掛積算(16節 3-1、陸上/海上とも。陸上の控え杭に振動工法を適用できる唯一の基準準拠経路。2026-08-01) |
| `SPQW_QUAYWALL_Estimate` | (新設。旧版に相当なし) | 岸壁 1 施設分の鋼材質量を 3 部材まとめて集計(フェーズ 5) |
| `SPQW_FRONTWALL_ImportCsv` | (新設。旧版に相当なし) | 帳票 CSV から前壁鋼管矢板を一括生成(壁一括生成。第3版(k)) |
| `SPQW_TIEROD_ImportCsv` | (新設。旧版に相当なし) | 前壁選択 → 帳票 CSV からタイロッドを一括生成(第3版(k)) |
| `SPQW_ANCHORPILE_ImportCsv` | (新設。旧版に相当なし) | 前壁選択 → 帳票 CSV から控え杭を一括生成(第3版(k)) |
| `SPQW_QUAYWALL_ReconcileCsv` | (新設。旧版に相当なし) | 帳票の数量・質量と 009 の計算値を突合検証(第3版(k)) |

Dynamo ノード(`SpqwNodes` クラス): `CalcSection`(007 `SpspNodes` 移植)・`CalcQuayWallQuantity`(009 新設)。ジオメトリ生成ノード(007 `CreateSolid` 相当)は移植しない(§12 項目3 の決定)。

---

## 5. 参照アセンブリ(暫定)

| アセンブリ | 用途 | 対象 |
|---|---|---|
| `AcCoreMgd.dll` | AutoCAD コア | Plugin |
| `AcDbMgd.dll` | Database / Solid3d / XData | Plugin |
| `AcMgd.dll` | Application | Plugin |
| `DynamoServices.dll` | `MultiReturn` / 警告ログ | Plugin(`ExcludeDynamo` で除外可、007 の方式踏襲) |

`ProtoGeometry.dll` は**参照しない**(Dynamo ノードはジオメトリを扱わない純計算のため。§12 項目3、第3版(g))。

すべて `<Private>False</Private>`。**Core は BCL のみ参照し、上記アセンブリを一切参照しない**(CLAUDE.PRIVATE.md §9 の検証対象外)。バージョンは 006/007/008 と同様に**現時点で未検証**(開発機に AutoCAD 未インストール)。

---

## 6. XData 設計(新 RegApp 名)

| 部材 | RegApp 名(新) | 参考: 旧 RegApp 名 | 主なフィールド(暫定) |
|---|---|---|---|
| 前壁 | `SPQW_FRONTWALL` | `SPSP`(007)/ `STEELPIPEPILE`(006) | `fmt`, `outer_d`, `wall_t`, `length`, `joint`, `grade`, `incl_deg`, `piece_index`, `piece_count`, `color`, `tip_x`/`tip_y`/`tip_z`(杭先端=挿入点。`tip_z` が D.L. 基準の杭先端標高)+ **1011(World 座標点)を併記**(第3版(h)) |
| タイロッド | `SPQW_TIEROD` | `TAIROD_PARAM`(008) | `fmt` + 008 の 18 項目(`rod_d`, `grade`, `code`, `state`, `span_length`, `pile_d`, `pile_pitch`, `tie_spacing`, `tie_count`, `hwl`, `tie_elev`, `waling_h`, `plate_t`, `washer_t`, `nut_h`, `adjust_l`, `anchor_reaction`, `color`)+ `front_handle`, `pos_y`, `rod_index` |
| 控え杭 | `SPQW_ANCHORPILE` | `ANCHORPILE`(006) | `fmt`, `outer_d`, `wall_t`, `length`, `incl_deg`, `closed_tip`, `span`, `tie_elev`, `tip_elev`, `color`, `front_handle` |

**位置情報の持ち方(フェーズ4 で確定、第3版(h) で 1011 併記を追加)**: 前壁のみ挿入点を保存する。キー=値の `tip_x`/`tip_y`/`tip_z` に加えて **DxfCode 1011(World 座標点)を併記**し、読み側は 1011 を優先する。1011 は AutoCAD が MOVE 等の変換に自動追随させるグループコードであり(006 が依存していた特性)、キー=値の文字列だけでは MOVE 追随が失われるため。タイロッドと控え杭は**平面 X を保存しない**。前壁 Handle と `span` / `tie_elev` から `_Action` のたびに再計算するため、前壁を MOVE したり傾斜角 θ を変更しても整列位置に追随する(移植元 006 `ANCHORPILE_Action` の「位置は常に前壁+span から導出する」を、決定8 によりタイロッドへも広げたもの)。移植元 008 は `base_x` を保存していたが 009 では持たない。

### 6.1 エンコード方式(決定9、第3版(e))

3 部材とも **008 の「`キー=値` の ASCII 文字列を並べる」方式**に統一する(移植元 008 `XDataStore` と同じ)。先頭に形式バージョン `fmt=1` を置く。

```
XData (RegApp: SPQW_FRONTWALL)
  "fmt=1"
  "outer_d=0.800"
  "wall_t=0.012"
  "length=20.000"
  "joint=LT75"
  "incl_deg=0.0"
  "piece_index=3"
  ...
```

初版〜第3版(d)の §6 は「007 の『順序変更禁止』規約を踏襲する」としていたが、調査の結果 007(`SPSP`)・006(`ANCHORPILE`)が**位置依存**(index 1=D, 2=t, 3=L…)であるのに対し、008(`TAIROD_PARAM`)は**キー=値 + FormatVersion** という別方式を採用しており、後者が明確に優れていることが分かったため第3版(e)で差し替えた。

| 採用理由 | 内容 |
|---|---|
| 項目追加が非破壊 | 末尾に追記しても既存図面の読み取りが壊れない。位置依存では「順序変更禁止」を全部材が永続的に背負う |
| 順序管理が不要 | 007 のソースが警告する「順序変更禁止 — 既存 DWG との後方互換を維持するため」という脆い制約から解放される |
| 形式変更に備えられる | `fmt` により将来の形式改訂時に旧形式を判別して読める |
| 実績がある | 008 で実装・運用済み |

数値は `System.Globalization.CultureInfo.InvariantCulture` で書式化する(008 と同じ。ロケール依存の小数点で図面が壊れることを防ぐ)。

**位置の 1011 併記(第3版(h) で追加)**: キー=値の文字列は DxfCode 1000 であり、AutoCAD の MOVE 追随(1011 = World space position のみが変換対象)を受けない。決定9 の当初検討ではこの特性喪失を見落としていた(実装後レビューの指摘 A)。対処として、前壁の挿入点は `tip_x`/`tip_y`/`tip_z` に加えて **1011 の点を併記保存し、読み側(`FrontWallRecord.Read`)は 1011 を優先**する。これにより §13.5 検証項目 4(前壁 MOVE 後の `_Action` 追随)が設計上成立する。文字列キーは人間可読なフォールバックとして残す。

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

**前壁参照方式への変更(決定8)**: 008 の「海側鋼管矢板の中心をクリック(X, Y)」は廃止し、控え杭と同じ**前壁選択方式**に変更する。ユーザー入力は前壁ソリッドの選択(1回)+ 施設延長方向の位置(Y のみ)。海側取付点の平面 X 座標は自動計算とし、目視ピック用の平面位置パラメータは無くなる。

| 英語名 | 日本語名 | 単位 | デフォルト値 | 範囲 | 由来 |
|---|---|---|---|---|---|
| `frontWallSelection` | 基準とする前壁の選択 | − | − | `SPQW_FRONTWALL` XData を持つ Solid3d | 006 `ANCHORPILE_Create` パターンを踏襲(決定8) |
| `positionY` | 施設延長方向位置 Y | m | − | 前壁の Y 範囲内 | 決定8(新規)。X は自動計算のため入力しない |

自動計算式(`SheetPileQuayWall.Core.PileGeometry.AxisXAt`、フェーズ2実装済み): `海側取付X = AxisXAt(前壁.TipPoint, 前壁.InclDeg, tie_elevation)`。前壁が鉛直(θ=0)の場合は従来どおり X が高さによらず一定になり、008 の挙動と一致する。

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
| **部材間(横断)** | **① タイロッド `PileDiameter` ⟺ 前壁 `outerDiameter`、② タイロッド `PilePitch` ⟺ 前壁の有効幅 B(継手形式から算出)、③ タイロッド `TieElevation` ⟺ 控え杭 `Z_tr`、④ タイロッド `SpanLength` ⟺ 控え杭 `span`** | **009 新規(`CrossMemberValidator`、フェーズ3)。上 3 行はいずれも単体チェックであり、同じ量を 2 部材が別々に入力している箇所の突き合わせが無かった** |

上 3 行は 1 件目のエラーで停止する(`string?` 返却)。部材間チェックのみ `ValidateAll` が全不一致を返す(移植元 008 `TieRodParameters.Validate` と同じ規約)。

すべて誤差許容 1 mm = 0.001 m、不一致時はエラー停止・再生成しない(自動補正しない。前壁外径の JIS/カタログスナップのみ例外)。

**④ の span の同一性について**: 008 README の断面図と全長算定式(`LandEndX = SpanLength + 金物厚`)より、`SpanLength` は「前壁矢板中心 〜 陸側定着面」であり、定着金物はその面より陸側へ張り出す。006 の控え杭 `span` も同一定義(控え杭軸 X = 前壁軸 X + span − D_a/2)であり、両者は等しくなければならない。なお移植元 008 の `TieRodParameters.SpanLength` の XML コメントは「控工中心までの距離」と書いているが、README の図・算定式・006 の定義のいずれとも一致しない(控え杭軸までなら D_a/2 だけ短くなる)。**コメント側の誤りとして扱う**(移植方針によりコードは変更していない)。

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

1. ~~**タイロッドの前壁参照方式**~~ → **解決済み(第3版(c)、決定8)**: 前壁選択方式に統一する。§1 決定8・§4・§6・§7.2 参照。
2. ~~**`SPQW_FRONTWALL_JointModel` の要否**~~ → **解決済み(第3版(e)、決定10)**: 移植しない。§1 決定10・§4 参照。
3. ~~**Dynamo ノードの対象範囲**~~ → **解決済み(第3版(g)、その後 (n)・(o) で拡張)**: 当初は前壁の計算ノードのみ(`CalcSection`・`CalcQuayWallQuantity` の 2 個)としたが、(n) で柱状図解析 `CalcWeightedN`、(o) で打設歩掛積算 4 ノードを追加し、現在は **7 個**。いずれも AutoCAD トランザクションを伴わない純計算のみをノード化する方針は不変で、ジオメトリ生成ノード(007 `CreateSolid` 相当)は実機でしか検証できないため移植しない。`ProtoGeometry` は参照せず `DynamoServices` のみ(`ExcludeDynamo=true` で除外可)。
4. ~~**プロジェクト名 `SPQW`**~~ → **解決済み(第3版(e))**: `SPQW`(Sheet Pile Quay Wall)を確定とし、部材名も短縮せず `SPQW_FRONTWALL_Create` 等の 3 階層で統一する。代替候補として挙げていた `KSMY`〈鋼矢板護岸〉は別種の構造物の名称であり不適切のため取り下げた。
5. ~~**旧 RegApp 図面の互換方針**~~ → **解決済み(第3版(e)、決定11)**: 互換は持たない(旧図面は旧プラグインで扱うか 009 で再作成)。§1 決定11 参照。必要が生じた時点で変換コマンドを後から追加できる。
6. ~~**θ 付き前壁とタイロッドの幾何整合**~~ → **解決済み(第3版(c)、決定8)**: タイロッドの前壁選択方式への統一(項目1)と同時に解決した。008 の計算層(`TieRodCalculator.SeaEndX`/`LandEndX`)は前壁が鉛直であることを暗黙に仮定しており(θ=15°・標高差20.5mでずれ量約5.5m)、目視クリックでは傾斜杭の軸位置を正確に指せない。前壁 XData から `Z_tip`・`θ` を読み取り `PileGeometry.AxisXAt` で自動計算することで、目視ピックそのものを廃止して解決する(θ=0 の場合は 008 と同じ結果になることを§7.2 に明記)。控え杭側は移植元 006 が既に同じ補正を持ち、フェーズ2で `AnchorAlignment` に引き継ぎ済み(T971 で検証)。フェーズ3(部材間整合)のブロッカーは解消した。
7. ~~**継手の雌雄と部材質量の側別配分**~~ → **解決済み(第3版(g)、実データによる)**: `JointShapes` の実形状(DXF 抽出)を読むと側別部材が確定する。`CurvesA`/`CurvesB` の P-P 形はいずれも外半径 0.0826 m・内半径 0.0736 m の円弧、すなわち **両側とも φ165.2×9 の鋼管**である(P-T 形の B 側は T-76×85×9×9 の直線群で明確に別部材)。したがって 34.7 kg/m は**片側分**であり、P-P 形の 1 接続は 69.4 kg/m。<br>**この確認により移植元のバグが判明した**: `JointCatalog.JointMassPerM` は `Angle + Tee + Pipe` を単純に足すため、L-T 形(19.9+12.7=32.6)と P-T 形(10.9+34.7=45.6)では正しく両側を合計するが、**P-P 形だけ鋼管を 1 本しか数えず 34.7 を返す**(doc コメントは「1組 = オス側＋メス側部材」)。積算が約 50% 過小になる。<br>009 では側別質量を持つ `FrontWall.JointMass` を新設して積算に使い、移植元ファイルは変更していない(`port-from-legacy.sh` の再実行で失われるため)。**007 側の修正は別途必要**。<br><br>側別部材の確定表:<br>A 側(+Y、雌): LT65 山形鋼×2 15.3 / LT75 山形鋼×2 19.9 / LT100 山形鋼×2 26.0 / PP 鋼管 34.7 / PT 鋼管 34.7<br>B 側(−Y、雄): LT65・LT75・LT100 T形鋼 12.7 / PP 鋼管 34.7 / PT T形鋼 10.9
8. **前壁と控え杭で外径の規則が異なる**(第3版追加): 前壁は K011 の D 0.500〜2.000 m・肉厚一律 0.009〜0.025 m でスナップなし(007 由来)、控え杭は JIS A 5525 の D 0.3185〜2.500 m・径別肉厚範囲・JIS スナップあり(006 由来)。控え杭は継手を持たない単独杭なので規則が違うこと自体は妥当だが、同一図面内で径の扱いが非対称になる点は確認が必要。

## 13. 実装フェーズ計画

### 13.1 方針の根拠(実測)

着手前に開発環境を実測し、当初の前提を 1 点訂正した。**開発機の WSL に .NET SDK 10.0.302 がインストール済みで、net8.0 のビルドとテスト実行ができる**(CLAUDE.md §5 は「SDK 未インストールなら構文レビューに留める」としていたが、Core 層には当てはまらない)。一方、AutoCAD は未インストールで `/mnt/c` 自体が未マウントのため、**Plugin 層はスタブによる構文検証までが限界**であり、実機確認とDLLバージョン検証(§9)はユーザー環境に依存する。

この非対称性(WSL で検証できることは安く、実機確認は高い)から、**WSL で検証できる範囲を先に消化するボトムアップ順**を採用する。骨格構築のみ最初に置き、実機貫通の前倒し(Walking Skeleton)は採らない。

### 13.2 フェーズ構成

| フェーズ | 内容 | 検証基準 | §12 未解決の影響 | 状態 |
|---|---|---|---|---|
| **0. 骨格** | プロジェクト 4 個(Core / Plugin / tests / stubs)、スタブ移植、`scripts/verify-dll-versions.ps1` 配置 | Core・tests がビルド成功。スタブ経由で Plugin がビルド成功 | なし | **完了 2026-07-25** |
| **1. 移植のみの Core** | 007 `src/Data` 8 ファイル → `Core/FrontWall`、008 `TaiRod.Core` 5 ファイル → `Core/TieRod`、テスト 12 ファイル | `dotnet test` で **275/275 pass** | なし | **完了 2026-07-25** |
| **2. 006 由来の新規 Core** | `PieceAssignment` / `FrontWallPlacement` / `AnchorAlignment` を `006@6d6d8cf` から抽出。`ed.WriteMessage` 依存を `string?` 返却へ、`Point3d` を独自 struct へ置換 | `dotnet test` で **311/311 pass**(275 移植 + 36 新規) | なし | **完了 2026-07-25** |
| **3. 部材間整合** | `CrossMemberValidator` 新設(§9 の横断チェック 4 組)、`TieRodPlacement` 新設(決定8 の θ 補正)、`FrontWallRef` を Core ルートへ移動し継手形式を追加 | `dotnet test` で **323/323 pass**(311 + 新規 12) | なし(項目1・6 は決定8で解決済み) | **完了 2026-07-26** |
| **4. Plugin** | XData 3 種(決定9 の キー=値 方式)、コマンド **11 個**(決定10 で `_JointModel` を削除)。前壁 → タイロッド → 控え杭の順。タイロッドは決定8により前壁選択方式で実装(008 の目視クリックからの書き直し)。旧図面の互換は持たない(決定11) | スタブビルドが **エラー 0・警告 0**(スタブ使用の意図的な警告を除く)。コマンド 11 個の登録確認。XData の書き込みキーと読み取りキーの完全一致を確認。**実機確認は未実施**(AutoCAD 未インストール) | **なし**(項目2・4・5 は決定9〜11 で解決済み) | **完了 2026-07-26** |
| **5. 仕上げ** | Dynamo ノード 2 個(前壁断面性能・施設数量。§12 項目3 の決定により前壁の計算ノードのみ)、`JointMass`(側別質量)と `QuayWallEstimate`(施設 1 件分の集計)、`SPQW_QUAYWALL_Estimate` コマンド、README(§7 の 9 章構成) | `dotnet test` で **335/335 pass**(323 + 新規 12)。スタブビルドが `ExcludeDynamo` の有無どちらでも成功 | なし(項目3 は決定、項目7 は実データで解決) | **完了 2026-07-26** |

フェーズ 0〜2 は §12 の未解決事項の影響を受けない(未解決 5 件はいずれもコマンド名・XData・部材間参照方式に関わり、Core の数値ロジックには波及しないため)。

### 13.3 追加するテスト(移植 275 件に対する新規分)

| 対象 | テスト ID | ケース | 件数 | フェーズ | 状態 |
|---|---|---|---|---|---|
| `PieceAssignment` | T950〜T954 | 1 本目/中間/最終本の継手有無、`pieceCount=1`、施工順位・総本数の範囲外 | 10 | 2 | 完了 |
| `FrontWallPlacement` / `PileGeometry` | T960〜T965 | θ=0/10° の杭頭位置、θ=15° での `AxisXAt` と `LocalToWorld` の整合、挿入点がピック点の Z を使わないこと、傾斜角・杭先端標高の範囲外 | 12 | 2 | 完了 |
| `AnchorAlignment` | T970〜T976 | 直杭どうし、前壁 θ=10° の軸ずれ、控え杭 θ=10° の先端オフセット、派生量(軸間水平距離・杭面間浄距離・杭頭標高)、Z_tr が控え杭/前壁の杭体範囲外、干渉 span の境界(誤差 1 mm) | 11 | 2 | 完了 |
| mm 混入検出 | T980〜T982 | 前壁 D/t と控え杭 D に mm 値を渡すと範囲チェックが検出すること、JIS スナップが m 単位で動作すること | 3 | 2 | 完了 |
| `TieRodPlacement` | T985〜T987 | θ=0 で移植元 008 と一致すること、θ=15° で約 5.5 m 陸側へずれること、取付点の Y/Z 合成 | 3 | 3 | 完了 |
| `CrossMemberValidator` | T990〜T998 | 径一致 / `PilePitch` ⟺ 有効幅 B / `TieElevation` ⟺ Z_tr / `SpanLength` ⟺ span の各正常・異常、`ValidateAll` が全不一致を返すこと | 9 | 3 | 完了 |

フェーズ 2 の新規は 36 件(`[Xunit.Theory]` の `InlineData` を 1 件と数えた場合は 18 件)。移植 275 件と合わせて 311 件。フェーズ 3 の新規 12 件を加えて **323 件**。

`CrossMemberValidator` のピッチ照合テストは、継手有効間隔 J が D 非依存(0.2478 m)で期待値を厳密に書ける P-P 形を用いる(D=1.000 m → 有効幅 B=1.2478 m)。

「XData 位置追随(MOVE 後の再生成一致)」は AutoCAD ランタイムの挙動であり Core では検証できないため、§10 のテスト計画から外し、実機手動検証項目とする。

### 13.5 実機手動検証項目(AutoCAD 2025 / Civil 3D 2025 の環境で実施)

Plugin 層はスタブによる構文・型検証までしか自動化できない。以下は実機で確認する。

| # | 手順 | 期待結果 |
|---|---|---|
| 1 | `scripts/verify-dll-versions.ps1` を実行 | exit 0(DLL バージョン一致)。**これが通るまで配布しない**(CLAUDE.PRIVATE.md §9) |
| 2 | `SPQW_FRONTWALL_Create` を θ=0 で実行 | 「前壁鋼管矢板」レイヤーに Solid3d 1 個。継手は施工順位に応じた側だけに付く(1/5 なら +Y 側のみ) |
| 3 | 同じ前壁を `SPQW_FRONTWALL_Action` で再生成 | 平面位置が変わらない。諸元だけが更新される |
| 4 | 前壁を MOVE してから `SPQW_TIEROD_Action` / `SPQW_ANCHORPILE_Action` | タイロッド・控え杭が前壁の新しい位置に整列し直す |
| 5 | `SPQW_FRONTWALL_Create` を θ=15° で実行し `SPQW_TIEROD_Create` | タイロッドの海側端が、傾斜した前壁のタイロッド軸心標高での軸位置に一致する(決定8。目視で前壁中心をクリックしていた 008 では合わなかった箇所) |
| 6 | 前壁と径・ピッチが食い違うタイロッドを作成 | `CrossMemberValidator` がエラー停止し、ソリッドを生成しない |
| 7 | `SPQW_TIEROD_Color` で色変更 | ソリッドの色と XData の `color` が両方変わる |
| 8 | 生成済みソリッドを再度 `_Query` | 保存した値がそのまま読み出せる(キー=値方式の往復確認) |

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
