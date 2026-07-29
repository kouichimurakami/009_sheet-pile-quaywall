// === 参照DLLバージョン: 未検証 ===
// AcCoreMgd.dll : 未検証 (期待値 25.x.x.x)
// AcDbMgd.dll   : 未検証 (期待値 25.x.x.x)
// AcMgd.dll     : 未検証 (期待値 25.x.x.x)
// 検証日: 未実施 / 検証コマンド: scripts/verify-dll-versions.ps1 → exit 2 (未検出)
// リスク: TypeLoadException / MissingMethodException が実行時に発生する可能性がある。
//
// 前壁鋼管矢板のジェット併用バイブロハンマ打設積算
// (SPQW_FRONTWALL_VibroJetEstimate)
// 出典: 港湾土木請負工事積算基準 令和7年度改訂版 3章16節 3-1(3-16-11〜25)
//
// 打設積算 3 系統のうちの 1 つ。SPQW_FRONTWALL_Estimate(打撃・4節 3-4.5)、
// SPQW_FRONTWALL_VibroEstimate(バイブロ単独・16節 3-2)とは別節・別歩掛であり、
// 規格選定の基礎(本項は必要偏心モーメント K0)も労務編成も異なる。
//
// 【入力に委ねる項目】噴射ノズル数およびウォータージェット使用台数の表(3-16-16)は
// 原本テキストのセル結合により OCR が復元不能であったため、推測で埋めず利用者入力と
// する。台数が決まれば発動発電機・水中ポンプ・水槽は自動決定される。
//
// 【簡略化】γ は基準上 4 土質の加重平均だが、本コマンドは代表 1 層で入力を受ける。
// 互層の場合は Core の VibroJetEstimate.WeightedGamma で加重平均すること。

namespace SheetPileQuayWall.Plugin.Commands
{
    [Autodesk.DesignScript.Runtime.IsVisibleInDynamoLibrary(false)]
    public static class VibroJetEstimateCommand
    {
        // ════════════════════════════════════════════════════════════════════
        // SPQW_FRONTWALL_VibroJetEstimate: 前壁選択 → ジェット併用の施工歩掛積算
        // ════════════════════════════════════════════════════════════════════
        [Autodesk.AutoCAD.Runtime.CommandMethod("SPQW_FRONTWALL_VibroJetEstimate")]
        public static void Estimate()
        {
            Autodesk.AutoCAD.ApplicationServices.Document doc =
                Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager
                    .MdiActiveDocument;
            Autodesk.AutoCAD.DatabaseServices.Database db = doc.Database;
            Autodesk.AutoCAD.EditorInput.Editor ed = doc.Editor;

            string frontHandle;
            SheetPileQuayWall.Plugin.XData.FrontWallRecord? record =
                SheetPileQuayWall.Plugin.DrawingHelper.SelectFrontWall(
                    ed, db, "\n積算する前壁鋼管矢板 (SPQW_FRONTWALL) を選択: ",
                    out frontHandle);
            if (record == null)
            {
                return;
            }

            ed.WriteMessage(
                "\n--- 振動工法・ジェット併用 (積算基準 3章16節 3-1) ---");

            // ── 適用範囲の確認(3-1-3 注3)────────────────────────────────
            if (!SheetPileQuayWall.Plugin.Prompt.Report(ed,
                SheetPileQuayWall.Core.FrontWall.VibroJetEstimate.ValidateJetApplicability(
                    record.OuterDm, record.LengthM)))
            {
                return;
            }

            int D_mm = (int)System.Math.Round(record.OuterDm * 1000.0);
            int t_mm = (int)System.Math.Round(record.WallTm * 1000.0);

            // ── 杭 1 本当り質量 Wp(本管 + 継手)──────────────────────────
            SheetPileQuayWall.Core.FrontWall.JointType jointType =
                SheetPileQuayWall.Core.FrontWall.JointParameters.FromCode(record.JointCode);
            SheetPileQuayWall.Core.FrontWall.PieceJoints joints =
                SheetPileQuayWall.Core.FrontWall.PieceAssignment.Assign(
                    record.PieceIndex, record.PieceCount);

            double bodyMass_kg =
                SheetPileQuayWall.Core.FrontWall.SectionProperties.CalcW(
                    record.OuterDm, record.WallTm) * record.LengthM;
            double jointMass_kg =
                SheetPileQuayWall.Core.FrontWall.JointMass.PerPile_kgPerM(jointType, joints)
                * record.LengthM;
            double pileMass_t = (bodyMass_kg + jointMass_kg) / 1000.0;

            // ── 施工条件 ────────────────────────────────────────────────
            string siteText;
            if (!SheetPileQuayWall.Plugin.Prompt.TryAskKeyword(
                ed, "\n施工区分 [陸上(L)/海上(O)] <O>: ",
                new string[] { "L", "O" }, "O", out siteText))
            {
                return;
            }
            SheetPileQuayWall.Core.FrontWall.ConstructionSite site =
                siteText == "L"
                ? SheetPileQuayWall.Core.FrontWall.ConstructionSite.Onshore
                : SheetPileQuayWall.Core.FrontWall.ConstructionSite.Offshore;

            // 1 日当り運転時間 T。作業船は 6h/日で固定、陸上機械は標準運転時間による。
            double operatingHours = 6.0;
            if (site == SheetPileQuayWall.Core.FrontWall.ConstructionSite.Onshore)
            {
                if (!SheetPileQuayWall.Plugin.Prompt.TryAskDouble(
                    ed, "\nクローラクレーンの 1 日当り標準運転時間 T (h/日) <8.0>: ",
                    8.0, 1.0, 24.0, out operatingHours))
                {
                    return;
                }
            }

            double driveLength_m;
            if (!SheetPileQuayWall.Plugin.Prompt.TryAskDouble(
                ed, $"\n打込長 ℓ (m) <{record.LengthM:F1}>: ",
                record.LengthM, 1.0, 80.0, out driveLength_m))
            {
                return;
            }

            double liftLength_m;
            if (!SheetPileQuayWall.Plugin.Prompt.TryAskDouble(
                ed, $"\n吊込 1 回ごとの杭長 L0 (m) <{record.LengthM:F1}>: ",
                record.LengthM, 1.0, 80.0, out liftLength_m))
            {
                return;
            }

            int liftCount;
            if (!SheetPileQuayWall.Plugin.Prompt.TryAskInt(
                ed, "\n杭の吊込み回数 ns (回) <1>: ", 1, 1, 10, out liftCount))
            {
                return;
            }

            int pileCount;
            if (!SheetPileQuayWall.Plugin.Prompt.TryAskInt(
                ed, $"\n打設本数 (本) <{record.PieceCount}>: ",
                record.PieceCount, 1, 500, out pileCount))
            {
                return;
            }

            // ── 土質(A0 表と γ 表でくくり方が異なるため 5 区分で受ける)──────
            string soilText;
            if (!SheetPileQuayWall.Plugin.Prompt.TryAskKeyword(
                ed, "\n土質 [砂質土･レキ質土(SG)/粘性土(CL)/玉石混りレキ(CG)/固結土(CE)/岩盤(RK)] <SG>: ",
                new string[] { "SG", "CL", "CG", "CE", "RK" }, "SG", out soilText))
            {
                return;
            }
            SheetPileQuayWall.Core.FrontWall.JetLayerType layer = soilText switch
            {
                "CL" => SheetPileQuayWall.Core.FrontWall.JetLayerType.Clay,
                "CG" => SheetPileQuayWall.Core.FrontWall.JetLayerType.CobbleGravel,
                "CE" => SheetPileQuayWall.Core.FrontWall.JetLayerType.Cemented,
                "RK" => SheetPileQuayWall.Core.FrontWall.JetLayerType.Rock,
                _    => SheetPileQuayWall.Core.FrontWall.JetLayerType.SandGravel,
            };

            int nAvg;
            if (!SheetPileQuayWall.Plugin.Prompt.TryAskInt(
                ed, "\n加重平均 N 値 <30>: ", 30, 1, 100, out nAvg))
            {
                return;
            }

            // 玉石混りレキのみ η を求めるための最大玉石径を尋ねる
            double eta = 0.0;
            if (layer == SheetPileQuayWall.Core.FrontWall.JetLayerType.CobbleGravel)
            {
                double maxCobble_mm;
                if (!SheetPileQuayWall.Plugin.Prompt.TryAskDouble(
                    ed, "\n最大玉石径 (mm) <100>: ", 100.0, 76.0, 200.0, out maxCobble_mm))
                {
                    return;
                }
                double? etaValue =
                    SheetPileQuayWall.Core.FrontWall.VibroJetEstimate.GetEta(maxCobble_mm);
                if (etaValue == null)
                {
                    ed.WriteMessage(
                        "\nエラー: 最大玉石径 200mm 超の地盤は基準上 η を別途定めます。積算を中止しました。");
                    return;
                }
                eta = etaValue.Value;
            }

            // 岩盤は qu で γ・A0 を決めるため一軸圧縮強度を尋ねる
            double qu = 0.0;
            bool useQu = layer == SheetPileQuayWall.Core.FrontWall.JetLayerType.Rock;
            if (useQu)
            {
                if (!SheetPileQuayWall.Plugin.Prompt.TryAskDouble(
                    ed, "\n加重平均一軸圧縮強度 qu (N/mm²) <10.0>: ",
                    10.0, 0.1, 29.4, out qu))
                {
                    return;
                }
            }

            // ── 基本振幅係数 A0 → 必要偏心モーメント K0 → 規格 ──────────────
            SheetPileQuayWall.Core.FrontWall.JetSoilType a0Soil =
                SheetPileQuayWall.Core.FrontWall.VibroJetEstimate.ToAmplitudeSoil(layer);

            double? a0 = useQu
                ? SheetPileQuayWall.Core.FrontWall.VibroJetEstimate.GetAmplitudeFactorByQu(
                    a0Soil, qu)
                : SheetPileQuayWall.Core.FrontWall.VibroJetEstimate.GetAmplitudeFactor(
                    a0Soil, nAvg);

            if (a0 == null)
            {
                ed.WriteMessage(
                    "\nエラー: 指定した土質と N 値(または qu)の組合せは基本振幅係数表 (3-16-15) に" +
                    "定義がありません。基準の適用範囲を確認してください。積算を中止しました。");
                return;
            }

            string chuckText;
            if (!SheetPileQuayWall.Plugin.Prompt.TryAskKeyword(
                ed, "\n鋼管チャックの装備 [あり(Y)/なし(N)] <Y>: ",
                new string[] { "Y", "N" }, "Y", out chuckText))
            {
                return;
            }
            double a0Applied = chuckText == "N"
                ? SheetPileQuayWall.Core.FrontWall.VibroJetEstimate.AdjustForNoChuck(a0.Value)
                : a0.Value;

            double k0 = SheetPileQuayWall.Core.FrontWall.VibroJetEstimate.CalcK0(
                a0Applied, pileMass_t);
            var (vibroClass, vibroGenerator) =
                SheetPileQuayWall.Core.FrontWall.VibroJetEstimate.GetVibroClass(k0);

            if (vibroGenerator.Length == 0)
            {
                ed.WriteMessage(
                    $"\nエラー: 必要偏心モーメント K0 = {k0:F1} N·m が規格表 (3-16-15、上限 2,900) を" +
                    "超えています。基準上「別途検討」であり本コマンドでは積算できません。");
                return;
            }

            // ── 打込み時間の係数 ────────────────────────────────────────
            double gamma = SheetPileQuayWall.Core.FrontWall.VibroJetEstimate.CalcGamma(
                layer, nAvg, qu, eta);

            double? beta = SheetPileQuayWall.Core.FrontWall.VibroJetEstimate.GetBeta(D_mm, t_mm);
            if (beta == null)
            {
                ed.WriteMessage(
                    $"\nエラー: φ{D_mm}×t{t_mm} は係数 β の表 (3-16-20) の範囲外です。" +
                    "基準上「別途考慮」であり本コマンドでは積算できません。");
                return;
            }

            double? delta = SheetPileQuayWall.Core.FrontWall.VibroJetEstimate.GetDelta(
                D_mm, vibroClass);
            if (delta == null)
            {
                ed.WriteMessage(
                    $"\nエラー: φ{D_mm} と {vibroClass} の組合せは係数 δ の表 (3-16-21) で" +
                    "「−」(適用対象外)です。バイブロ規格を見直してください。");
                return;
            }

            // 鋼管矢板は継手合わせ・継手抵抗の加算時間 ε を計上する
            double jointLength_m;
            if (!SheetPileQuayWall.Plugin.Prompt.TryAskDouble(
                ed, $"\n継手の長さ ℓj (m、ε 算定用) <{record.LengthM:F1}>: ",
                record.LengthM, 0.0, 80.0, out jointLength_m))
            {
                return;
            }
            double epsilon =
                SheetPileQuayWall.Core.FrontWall.VibroJetEstimate.CalcEpsilon(jointLength_m);

            // ── ジェット設備 ────────────────────────────────────────────
            int jetCount;
            if (!SheetPileQuayWall.Plugin.Prompt.TryAskInt(
                ed, "\nウォータージェット使用台数 (1〜4。基準 3-16-16 の表は要確認) <2>: ",
                2, 1, 4, out jetCount))
            {
                return;
            }

            int nozzleCount;
            if (!SheetPileQuayWall.Plugin.Prompt.TryAskInt(
                ed, "\n噴射ノズル数 (基準 3-16-16 の表による) <6>: ", 6, 1, 20, out nozzleCount))
            {
                return;
            }

            string waterText;
            if (!SheetPileQuayWall.Plugin.Prompt.TryAskKeyword(
                ed, "\n水源が遠く水中ポンプ・水槽を計上するか [する(Y)/しない(N)] <N>: ",
                new string[] { "Y", "N" }, "N", out waterText))
            {
                return;
            }
            bool needWaterSupply = waterText == "Y";

            // ── 溶接・海象・障害 ────────────────────────────────────────
            int jointCountPerPile;
            if (!SheetPileQuayWall.Plugin.Prompt.TryAskInt(
                ed, "\n継杭の継手個所数 (単杭=0) <0>: ", 0, 0, 5, out jointCountPerPile))
            {
                return;
            }
            bool splicing = jointCountPerPile > 0;

            SheetPileQuayWall.Core.FrontWall.SeaCondition sea =
                SheetPileQuayWall.Core.FrontWall.SeaCondition.Normal;
            if (site == SheetPileQuayWall.Core.FrontWall.ConstructionSite.Offshore)
            {
                string seaText;
                if (!SheetPileQuayWall.Plugin.Prompt.TryAskKeyword(
                    ed, "\n海象条件 [普通(N)/悪い(S)] <N>: ",
                    new string[] { "N", "S" }, "N", out seaText))
                {
                    return;
                }
                sea = seaText == "S"
                    ? SheetPileQuayWall.Core.FrontWall.SeaCondition.Severe
                    : SheetPileQuayWall.Core.FrontWall.SeaCondition.Normal;
            }

            string obstacleText;
            if (!SheetPileQuayWall.Plugin.Prompt.TryAskKeyword(
                ed, "\n障害の有無 [なし(N)/あり(E)] <N>: ",
                new string[] { "N", "E" }, "N", out obstacleText))
            {
                return;
            }
            SheetPileQuayWall.Core.FrontWall.ObstacleStatus obstacle =
                obstacleText == "E"
                ? SheetPileQuayWall.Core.FrontWall.ObstacleStatus.Exists
                : SheetPileQuayWall.Core.FrontWall.ObstacleStatus.None;

            // 付帯船舶(台船・引船・揚錨船・潜水士船)は海上打設のみ計上する(3-16-18)。
            // 潜水士船は E2(障害区分)とは別の判断軸で、打設個所の障害物・打設後の
            // 異常有無等の調査作業が伴うかどうかで決める(3-16-18 注4)。
            bool needDiverVessel = false;
            if (site == SheetPileQuayWall.Core.FrontWall.ConstructionSite.Offshore)
            {
                string diverText;
                if (!SheetPileQuayWall.Plugin.Prompt.TryAskKeyword(
                    ed, "\n潜水士船 (打設個所の障害物・打設後異常の調査作業) [計上しない(N)/計上する(Y)] <N>: ",
                    new string[] { "N", "Y" }, "N", out diverText))
                {
                    return;
                }
                needDiverVessel = diverText == "Y";
            }

            double vibroMass_t;
            if (!SheetPileQuayWall.Plugin.Prompt.TryAskDouble(
                ed, "\nバイブロハンマ質量 Wv (t、鋼管チャック質量を含む。クレーン規格算定用) <10.0>: ",
                10.0, 0.1, 100.0, out vibroMass_t))
            {
                return;
            }

            // ── 積算 ────────────────────────────────────────────────────
            double Tp = SheetPileQuayWall.Core.FrontWall.VibroJetEstimate.CalcTp(
                liftLength_m, liftCount);
            double Tb = SheetPileQuayWall.Core.FrontWall.VibroJetEstimate.CalcTb(
                gamma, beta.Value, delta.Value, driveLength_m, epsilon);
            // 溶接時間は 4節 3-4.5 の表による(3-16-21 の表と同一データ)
            double Tw = SheetPileQuayWall.Core.FrontWall.DriveEstimate.CalcTw(
                D_mm, t_mm, jointCountPerPile);
            double Tc = Tp + Tb + Tw;

            double Q = SheetPileQuayWall.Core.FrontWall.VibroJetEstimate.CalcQ(
                site, operatingHours, Tc, sea, obstacle, pileCount);
            if (Q <= 0.0)
            {
                ed.WriteMessage("\nエラー: 1 日当り打設本数が 0 以下になりました。入力条件を確認してください。");
                return;
            }
            int driveDays = (int)System.Math.Ceiling(pileCount / Q);

            var labor = SheetPileQuayWall.Core.FrontWall.VibroJetEstimate.GetLabor(
                site, record.LengthM, splicing, D_mm);
            double craneCapacity_t =
                SheetPileQuayWall.Core.FrontWall.VibroJetEstimate.CalcCraneCapacity(
                    vibroMass_t, pileMass_t);
            string jetGenerator =
                SheetPileQuayWall.Core.FrontWall.VibroJetEstimate.GetJetGenerator(jetCount);

            // ── 出力 ────────────────────────────────────────────────────
            ed.WriteMessage(
                "\n=== 前壁鋼管矢板 施工歩掛積算 (振動工法・ジェット併用) ===" +
                "\n    出典: 港湾土木請負工事積算基準 令和7年度改訂版 3章16節 3-1");

            ed.WriteMessage("\n--- 入力条件 ---");
            ed.WriteMessage($"\n  外径 D      : {D_mm,6} mm   肉厚 t : {t_mm,4} mm   全長 L : {record.LengthM,6:F1} m");
            ed.WriteMessage($"\n  打込長 ℓ    : {driveLength_m,6:F1} m   本数 N : {pileCount,4} 本   施工区分: " +
                $"{(site == SheetPileQuayWall.Core.FrontWall.ConstructionSite.Onshore ? "陸上" : "海上")}");
            ed.WriteMessage($"\n  吊込杭長 L0 : {liftLength_m,6:F1} m   吊込回数 ns: {liftCount,3} 回");
            ed.WriteMessage($"\n  土質        : {SoilLabel(layer)}   加重平均N値: {nAvg}" +
                (useQu ? $"   qu: {qu:F1} N/mm²" : "") +
                (eta > 0.0 ? $"   η: {eta:F1}" : ""));

            ed.WriteMessage("\n--- 杭 1 本当り質量 Wp ---");
            ed.WriteMessage($"\n  本管        : {bodyMass_kg,10:F0} kg  [確定]");
            ed.WriteMessage($"\n  継手金物    : {jointMass_kg,10:F0} kg  [確定]");
            ed.WriteMessage($"\n  合計 Wp     : {pileMass_t,10:F3} t");

            ed.WriteMessage("\n--- バイブロハンマの規格選定 (3-16-15) ---");
            ed.WriteMessage($"\n  基本振幅係数 A0 : {a0Applied,10:F3}" +
                (chuckText == "N" ? $"  (表値 {a0.Value:F2} を鋼管チャック非装備により 1.3 で除した値)" : ""));
            ed.WriteMessage($"\n  必要偏心 K0     : {k0,10:F1} N·m  (A0 × Wp × 98)");
            ed.WriteMessage($"\n  バイブロハンマ  : {vibroClass}");
            ed.WriteMessage($"\n  発動発電機      : {vibroGenerator} (バイブロハンマ用)");
            ed.WriteMessage($"\n  クレーン吊上げ  : {craneCapacity_t,10:F1} t 以上  ((Wv + Wp) × 6、3-16-18)");
            ed.WriteMessage(
                "\n  (クレーン付台船・起重機船の規格は上記吊上げ荷重から別途選定すること)");

            if (site == SheetPileQuayWall.Core.FrontWall.ConstructionSite.Offshore)
            {
                ed.WriteMessage("\n--- 付帯船舶 (3-16-18、積載物の長さ = 杭全長で選定) ---");
                var (barge, tug) = SheetPileQuayWall.Core.FrontWall.VibroEstimate.GetBargeAndTug(
                    record.LengthM);
                if (barge.Length == 0)
                {
                    ed.WriteMessage(
                        $"\n  台船・引船      : 全長 {record.LengthM:F1}m は規格表の範囲" +
                        $"(44m未満)を超えています。別途選定してください。");
                }
                else
                {
                    ed.WriteMessage($"\n  台船            : {barge} × 1");
                    ed.WriteMessage($"\n  引船            : {tug} × 1");
                }
                ed.WriteMessage(
                    $"\n  揚錨船          : {SheetPileQuayWall.Core.FrontWall.VibroEstimate.AnchorHandlingVesselSpec} × 1");
                if (needDiverVessel)
                {
                    ed.WriteMessage(
                        $"\n  潜水士船        : {SheetPileQuayWall.Core.FrontWall.VibroEstimate.DiverVesselSpec} × 1" +
                        " (必要に応じて計上)");
                }
            }

            ed.WriteMessage("\n--- ウォータージェット設備 (3-16-16〜17) ---");
            ed.WriteMessage("\n  ジェット規格    : 243kW / 吐出圧力 14.7MPa / 吐出流量 895 ℓ/min");
            ed.WriteMessage($"\n  使用台数        : {jetCount} 台   噴射ノズル数: {nozzleCount}(いずれも入力値)");
            ed.WriteMessage($"\n  発動発電機      : {jetGenerator} (ジェット付属水中ポンプ用)");
            if (needWaterSupply)
            {
                var w = SheetPileQuayWall.Core.FrontWall.VibroJetEstimate.GetWaterSupply(jetCount);
                ed.WriteMessage($"\n  水中ポンプ      : 工事用 {w.pump} {w.pumpOutput_kW:F1}kW × {w.pumpCount} 台");
                ed.WriteMessage($"\n  水槽            : {w.tankVolume_m3} m³ × {w.tankCount} 基");
                ed.WriteMessage($"\n  発動発電機      : {w.generator} (水槽給水水中ポンプ用)");
            }
            else
            {
                ed.WriteMessage("\n  水中ポンプ・水槽: 計上しない(設置位置直下に水深 1m 以上の水源がある場合等)");
            }

            ed.WriteMessage("\n--- 施工能力 (3-16-19〜21) ---");
            ed.WriteMessage($"\n  γ (1m当り)  : {gamma,8:F3} 分/m  ({GammaLabel(layer)})");
            ed.WriteMessage($"\n  β (径・板厚): {beta.Value,8:F2}");
            ed.WriteMessage($"\n  δ (規格・径): {delta.Value,8:F2}");
            ed.WriteMessage($"\n  ε (継手加算): {epsilon,8:F2} 分  (0.3 × ℓj)");
            ed.WriteMessage($"\n  準備時間 Tp : {Tp,8:F1} 分/本  ((0.3·L0 + 11)×ns + 5)");
            ed.WriteMessage($"\n  打込時間 Tb : {Tb,8:F1} 分/本  (γ·β·δ·ℓ + ε)");
            ed.WriteMessage($"\n  溶接時間 Tw : {Tw,8:F1} 分/本  (4節 4.5 を適用)");
            ed.WriteMessage($"\n  打設時間 Tc : {Tc,8:F1} 分/本");
            ed.WriteMessage($"\n  日当り打設  : {Q,8:F2} 本/日  (ei=" +
                $"{(site == SheetPileQuayWall.Core.FrontWall.ConstructionSite.Onshore ? "0.80" : "0.70")}" +
                $"、T={operatingHours:F1}h/日)");
            ed.WriteMessage($"\n  打設日数    : {driveDays,8} 日");

            ed.WriteMessage("\n--- 労務編成 (人/日、3-16-21) ---");
            ed.WriteMessage($"\n  世話役 {labor.foreman} / とび工 {labor.rigger} / " +
                $"普通作業員 {labor.laborer} / 特殊作業員 {labor.specialist} / 溶接工 {labor.welder}");

            ed.WriteMessage("\n--- 適用上の注意 ---");
            ed.WriteMessage("\n  ・ジェット併用は外径 1,500mm 以下・全長 40m 以下に適用する (3-1-3 注3)。");
            ed.WriteMessage("\n  ・噴射ノズル数とジェット使用台数は基準 3-16-16 の表で確認すること" +
                "(本コマンドは入力値をそのまま用いる)。");
            ed.WriteMessage("\n  ・γ は代表 1 層で算定している。互層の場合は基準 3-16-19 により" +
                "打込み長で加重平均すること。");
            ed.WriteMessage("\n  ・配管系部材の材料費・取付費、導材、拘束費は本コマンドの対象外(別代価表)。");
            ed.WriteMessage("\n  ・振動対策に配慮を要する場合、バイブロ規格およびジェット台数は別途検討する。");
            ed.WriteMessage("\nSPQW_FRONTWALL_VibroJetEstimate 完了。");
        }

        private static string SoilLabel(SheetPileQuayWall.Core.FrontWall.JetLayerType layer)
        {
            return layer switch
            {
                SheetPileQuayWall.Core.FrontWall.JetLayerType.Clay => "粘性土",
                SheetPileQuayWall.Core.FrontWall.JetLayerType.CobbleGravel => "玉石混りレキ",
                SheetPileQuayWall.Core.FrontWall.JetLayerType.Cemented => "固結土",
                SheetPileQuayWall.Core.FrontWall.JetLayerType.Rock => "岩盤",
                _ => "砂質土･レキ質土",
            };
        }

        private static string GammaLabel(SheetPileQuayWall.Core.FrontWall.JetLayerType layer)
        {
            return layer switch
            {
                SheetPileQuayWall.Core.FrontWall.JetLayerType.CobbleGravel => "γ2 = 0.02N + 0.5 + η",
                SheetPileQuayWall.Core.FrontWall.JetLayerType.Clay => "γ3 = 0.04N + 0.6",
                SheetPileQuayWall.Core.FrontWall.JetLayerType.Cemented => "γ3 = 0.04N + 0.6",
                SheetPileQuayWall.Core.FrontWall.JetLayerType.Rock => "γ4 = 0.82qu + 3",
                _ => "γ1 = 0.02N + 0.5",
            };
        }
    }
}
