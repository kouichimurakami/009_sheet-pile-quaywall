// === 参照DLLバージョン: 未検証 ===
// AcCoreMgd.dll : 未検証 (期待値 25.x.x.x)
// AcDbMgd.dll   : 未検証 (期待値 25.x.x.x)
// AcMgd.dll     : 未検証 (期待値 25.x.x.x)
// 検証日: 未実施 / 検証コマンド: scripts/verify-dll-versions.ps1 → exit 2 (未検出)
// リスク: TypeLoadException / MissingMethodException が実行時に発生する可能性がある。
//
// 前壁鋼管矢板のバイブロハンマ打設積算(SPQW_FRONTWALL_VibroEstimate)
// 出典: 港湾土木請負工事積算基準 令和7年度改訂版 3章16節 3-2(3-16-26〜31)
//
// 既存の SPQW_FRONTWALL_Estimate(打撃工法・4節 3-4.5)とは別節・別歩掛のため、
// コマンドを分けている。入力項目・補正係数・作業船が異なり、混在させると
// 積算根拠が崩れる。工法の選択は利用者がコマンドの選択として行う。
//
// 適用範囲(3-2-1)は**海上打設のみ**。陸上打設・ウォータージェット併用(16節 3-1)
// は本コマンドの対象外である。

namespace SheetPileQuayWall.Plugin.Commands
{
    public static class VibroEstimateCommand
    {
        // ════════════════════════════════════════════════════════════════════
        // SPQW_FRONTWALL_VibroEstimate: 前壁選択 → 振動工法の施工歩掛積算
        // ════════════════════════════════════════════════════════════════════
        [Autodesk.AutoCAD.Runtime.CommandMethod("SPQW_FRONTWALL_VibroEstimate")]
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
                "\n--- 振動工法 (バイブロハンマ・海上打設、積算基準 3章16節 3-2) ---");

            // ── 施工条件 ────────────────────────────────────────────────
            double driveLength_m;
            if (!SheetPileQuayWall.Plugin.Prompt.TryAskDouble(
                ed, $"\n打設長 Lb (m, 表層から連続する N=0 区間は除く) <{record.LengthM:F1}>: ",
                record.LengthM, 1.0, 80.0, out driveLength_m))
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

            int nTip;
            if (!SheetPileQuayWall.Plugin.Prompt.TryAskInt(
                ed, "\n先端地盤の N 値 <50>: ", 50, 1, 100, out nTip))
            {
                return;
            }

            int nAvg;
            if (!SheetPileQuayWall.Plugin.Prompt.TryAskInt(
                ed, "\n周辺地盤の加重平均 N 値 <20>: ", 20, 1, 100, out nAvg))
            {
                return;
            }

            int jointCountPerPile;
            if (!SheetPileQuayWall.Plugin.Prompt.TryAskInt(
                ed, "\n継杭の継手個所数 (単杭=0) <0>: ", 0, 0, 5, out jointCountPerPile))
            {
                return;
            }

            string seaText;
            if (!SheetPileQuayWall.Plugin.Prompt.TryAskKeyword(
                ed, "\n海象条件 [普通(N)/悪い(S)] <N>: ",
                new string[] { "N", "S" }, "N", out seaText))
            {
                return;
            }
            SheetPileQuayWall.Core.FrontWall.SeaCondition sea =
                seaText == "S"
                ? SheetPileQuayWall.Core.FrontWall.SeaCondition.Severe
                : SheetPileQuayWall.Core.FrontWall.SeaCondition.Normal;

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

            // 潜水士船は E2(障害区分)とは別の判断軸(3-16-29 注2)。打設予定個所の
            // 障害物・打設後の異常の有無等の調査作業が伴うかどうかで計上する。
            string diverText;
            if (!SheetPileQuayWall.Plugin.Prompt.TryAskKeyword(
                ed, "\n潜水士船 (打設個所の障害物・打設後異常の調査作業) [計上しない(N)/計上する(Y)] <N>: ",
                new string[] { "N", "Y" }, "N", out diverText))
            {
                return;
            }
            bool needDiverVessel = diverText == "Y";

            // ── 鋼材質量(本管 + 継手)───────────────────────────────────
            // バイブロ規格は鋼材質量で選定するため、実際に吊り込む継手金物を含める。
            // (打撃工法の SPQW_FRONTWALL_Estimate は本管のみで選定しており差が出る)
            SheetPileQuayWall.Core.FrontWall.JointType jointType =
                SheetPileQuayWall.Core.FrontWall.JointParameters.FromCode(record.JointCode);
            SheetPileQuayWall.Core.FrontWall.PieceJoints joints =
                SheetPileQuayWall.Core.FrontWall.PieceAssignment.Assign(
                    record.PieceIndex, record.PieceCount);

            double W_kgPerM = SheetPileQuayWall.Core.FrontWall.SectionProperties.CalcW(
                record.OuterDm, record.WallTm);
            double bodyMass_kg = W_kgPerM * record.LengthM;
            double jointMass_kg =
                SheetPileQuayWall.Core.FrontWall.JointMass.PerPile_kgPerM(jointType, joints)
                * record.LengthM;
            double steelMass_t = (bodyMass_kg + jointMass_kg) / 1000.0;

            // ── 積算(3-16-29、3-16-30)──────────────────────────────────
            SheetPileQuayWall.Core.FrontWall.VibroDriveTarget target =
                SheetPileQuayWall.Core.FrontWall.VibroDriveTarget.SteelPipeSheetPile;

            double r1 = SheetPileQuayWall.Core.FrontWall.VibroEstimate.CalcR1(
                record.OuterDm, driveLength_m, nTip, nAvg);
            double rj = SheetPileQuayWall.Core.FrontWall.VibroEstimate.CalcRj(r1, target);
            double r = SheetPileQuayWall.Core.FrontWall.VibroEstimate.CalcR(
                record.OuterDm, driveLength_m, nTip, nAvg, target);

            string vibroClass =
                SheetPileQuayWall.Core.FrontWall.VibroEstimate.GetVibroClass(steelMass_t, r);
            var (generator, craneVessel) =
                SheetPileQuayWall.Core.FrontWall.VibroEstimate.GetEquipment(vibroClass);

            int D_mm = (int)System.Math.Round(record.OuterDm * 1000.0);
            int t_mm = (int)System.Math.Round(record.WallTm * 1000.0);

            double Tp = SheetPileQuayWall.Core.FrontWall.VibroEstimate.CalcTp(driveLength_m);
            double Tb = SheetPileQuayWall.Core.FrontWall.VibroEstimate.CalcTb(driveLength_m, target);
            // 溶接時間は基準の指示により 4節 本体工 4.5/4.6 を適用する(3-16-31 注5)
            double Tw = SheetPileQuayWall.Core.FrontWall.DriveEstimate.CalcTw(
                D_mm, t_mm, jointCountPerPile);
            double Tc = Tp + Tb + Tw;

            double Q = SheetPileQuayWall.Core.FrontWall.VibroEstimate.CalcQ(
                Tc, sea, obstacle, pileCount);
            if (Q <= 0.0)
            {
                ed.WriteMessage("\nエラー: 1 日当り打設本数が 0 以下になりました。入力条件を確認してください。");
                return;
            }
            int driveDays = (int)System.Math.Ceiling(pileCount / Q);

            bool splicing = jointCountPerPile > 0;
            var labor = SheetPileQuayWall.Core.FrontWall.VibroEstimate.GetLabor(
                target, driveLength_m, splicing, D_mm);
            var (weldMachines, weldGenerator) =
                SheetPileQuayWall.Core.FrontWall.VibroEstimate.GetWeldEquipment(D_mm, splicing);

            // ── 出力 ────────────────────────────────────────────────────
            ed.WriteMessage(
                "\n=== 前壁鋼管矢板 施工歩掛積算 (振動工法) ===" +
                "\n    出典: 港湾土木請負工事積算基準 令和7年度改訂版 3章16節 3-2");
            ed.WriteMessage("\n--- 入力条件 ---");
            ed.WriteMessage($"\n  外径 D      : {D_mm,6} mm   肉厚 t : {t_mm,4} mm   全長 L : {record.LengthM,6:F1} m");
            ed.WriteMessage($"\n  打設長 Lb   : {driveLength_m,6:F1} m   本数 N : {pileCount,4} 本   施工区分: 海上 (3-2-1)");
            ed.WriteMessage($"\n  先端N値     : {nTip,6}      加重平均N値: {nAvg,4}      海象 : " +
                $"{(sea == SheetPileQuayWall.Core.FrontWall.SeaCondition.Severe ? "悪い" : "普通")}");
            ed.WriteMessage($"\n  障害        : {(obstacle == SheetPileQuayWall.Core.FrontWall.ObstacleStatus.Exists ? "あり" : "なし")}" +
                $"        継手個所数 : {jointCountPerPile}");

            ed.WriteMessage("\n--- 鋼材質量 (規格選定用) ---");
            ed.WriteMessage($"\n  本管        : {bodyMass_kg,10:F0} kg/本  [確定](K011 単位重量 × L)");
            ed.WriteMessage($"\n  継手金物    : {jointMass_kg,10:F0} kg/本  [確定](側別質量 × L)");
            ed.WriteMessage($"\n  合計        : {steelMass_t,10:F3} t/本");

            ed.WriteMessage("\n--- 貫入抵抗値 (3-16-29) ---");
            ed.WriteMessage($"\n  本管 R1     : {r1,10:F1} kN  [確定](300·N·Ap + 2·N̄·Lb·As)");
            ed.WriteMessage($"\n  継手 Rj     : {rj,10:F1} kN  [確定](R1 × 10⁻¹。鋼管矢板のみ)");
            ed.WriteMessage($"\n  合計 R      : {r,10:F1} kN");

            ed.WriteMessage("\n--- 機械規格 (3-16-29) ---");
            ed.WriteMessage($"\n  バイブロハンマ  : {vibroClass}");
            if (generator.Length == 0)
            {
                ed.WriteMessage(
                    "\n  発動発電機      : 規格表の範囲外です。基準の規格決定図を超えるため別途検討してください。");
            }
            else
            {
                ed.WriteMessage($"\n  発動発電機      : {generator}");
                ed.WriteMessage($"\n  起重機船・杭打船: {craneVessel}");
            }
            if (splicing)
            {
                ed.WriteMessage($"\n  継手溶接機械    : 半自動 500A × {weldMachines} 台 + 発動発電機 {weldGenerator}");
            }

            ed.WriteMessage("\n--- 付帯船舶 (3-16-29、積載物の長さ = 杭全長で選定) ---");
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

            ed.WriteMessage("\n--- 施工能力 (3-16-30) ---");
            ed.WriteMessage($"\n  準備時間 Tp : {Tp,8:F2} 分/本  (24 + 0.6×(Lb−25))");
            ed.WriteMessage($"\n  打込時間 Tb : {Tb,8:F2} 分/本  (Lb ÷ 0.75 m/分)");
            ed.WriteMessage($"\n  溶接時間 Tw : {Tw,8:F2} 分/本  (4節 4.5 を適用)");
            ed.WriteMessage($"\n  打設時間 Tc : {Tc,8:F2} 分/本");
            ed.WriteMessage($"\n  日当り打設  : {Q,8:F2} 本/日  (ei=0.70、T=6h/日)");
            ed.WriteMessage($"\n  打設日数    : {driveDays,8} 日");

            ed.WriteMessage("\n--- 労務編成 (人/日、3-16-31) ---");
            ed.WriteMessage($"\n  世話役 {labor.foreman} / とび工 {labor.rigger} / " +
                $"普通作業員 {labor.laborer} / 特殊作業員 {labor.specialist} / 溶接工 {labor.welder}");

            ed.WriteMessage("\n--- 適用上の注意 ---");
            ed.WriteMessage("\n  ・本歩掛は海上打設のみに適用する (3-2-1)。陸上打設は対象外。");
            ed.WriteMessage("\n  ・ウォータージェット併用は 16節 3-1 の別歩掛であり本コマンドでは扱わない。");
            ed.WriteMessage("\n  ・玉石混じり層を含む場合の打込み速度は基準上「別途考慮」である。");
            ed.WriteMessage("\n  ・支持層へ打込む/中間層を打抜く場合、バイブロ単独は標準適用外 (3-1-3)。");
            ed.WriteMessage("\nSPQW_FRONTWALL_VibroEstimate 完了。");
        }
    }
}
