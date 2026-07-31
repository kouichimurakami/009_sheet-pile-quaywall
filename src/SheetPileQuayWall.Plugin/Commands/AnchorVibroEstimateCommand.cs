// === 参照DLLバージョン: 未検証 ===
// AcCoreMgd.dll : 未検証 (期待値 25.x.x.x)
// AcDbMgd.dll   : 未検証 (期待値 25.x.x.x)
// AcMgd.dll     : 未検証 (期待値 25.x.x.x)
// 検証日: 未実施 / 検証コマンド: scripts/verify-dll-versions.ps1 → exit 2 (未検出)
// リスク: TypeLoadException / MissingMethodException が実行時に発生する可能性がある。
//
// 控え杭のバイブロハンマ打設積算(SPQW_ANCHORPILE_VibroEstimate、海上打設のみ)
// 出典: 港湾土木請負工事積算基準 令和7年度改訂版 3章16節 3-2(3-16-26〜31)
//
// 追加の根拠(2026-08-01): 控え杭が属する 4節 3-4.6「鋼杭式」の適用工法表は
// 振動工法(バイブロハンマ)を標準工法として挙げ、「バイブロハンマによる施工歩掛は、
// 現場条件を勘案の上、『16節 仮設工』によることができる」と注記する(3-4.6-9)。
// 前壁(3-4.5-11 → 16節)と同じ構造であり、本コマンドはその 16節側を担当する。
//
// 適用範囲(3-2-1)は**海上打設のみ**。陸上打設は本項の対象外であり、16節 2-1 の
// 陸上歩掛は「鋼矢板・H形鋼杭」で鋼管杭を含まないため、**陸上 × 鋼管杭 ×
// バイブロ単独の歩掛は基準に存在しない**。陸上で振動工法が必要な場合は
// ジェット併用(16節 3-1、SPQW_ANCHORPILE_VibroJetEstimate)を用いること。
//
// 打込み対象は鋼管杭(VibroDriveTarget.SteelPipePile)。前壁の鋼管矢板と異なり
// 継手が無いため、打込み速度 Lo = 0.90 m/分、継手の貫入抵抗 Rj = 0、労務の
// とび工が 2/4 人(矢板は 3/5 人)になる。計算層は FrontWall.VibroEstimate を
// そのまま用いる(AnchorDriveEstimateCommand が FrontWall.DriveEstimate を
// 再利用しているのと同じ方針)。

namespace SheetPileQuayWall.Plugin.Commands
{
    public static class AnchorVibroEstimateCommand
    {
        // ════════════════════════════════════════════════════════════════════
        // SPQW_ANCHORPILE_VibroEstimate: 控え杭選択 → 振動工法(海上)の施工歩掛積算
        // ════════════════════════════════════════════════════════════════════
        [Autodesk.AutoCAD.Runtime.CommandMethod("SPQW_ANCHORPILE_VibroEstimate")]
        public static void Estimate()
        {
            Autodesk.AutoCAD.ApplicationServices.Document doc =
                Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager
                    .MdiActiveDocument;
            Autodesk.AutoCAD.DatabaseServices.Database db = doc.Database;
            Autodesk.AutoCAD.EditorInput.Editor ed = doc.Editor;

            SheetPileQuayWall.Core.AnchorPile.AnchorInput? input =
                AnchorEstimateHelper.SelectAnchorInput(
                    ed, db, "\n積算する控え杭 (SPQW_ANCHORPILE) を選択: ");
            if (input == null)
            {
                return;
            }
            SheetPileQuayWall.Core.AnchorPile.AnchorInput a = input;

            ed.WriteMessage(
                "\n--- 振動工法 (バイブロハンマ・海上打設、積算基準 3章16節 3-2) ---");

            // ── 施工条件 ────────────────────────────────────────────────
            double driveLength_m;
            if (!SheetPileQuayWall.Plugin.Prompt.TryAskDouble(
                ed, $"\n打設長 Lb (m, 表層から連続する N=0 区間は除く) <{a.LengthM:F1}>: ",
                a.LengthM, 1.0, 80.0, out driveLength_m))
            {
                return;
            }

            int pileCount;
            if (!SheetPileQuayWall.Plugin.Prompt.TryAskInt(
                ed, "\n打設本数 (本) <1>: ", 1, 1, 500, out pileCount))
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

            // 潜水士船は E2(障害区分)とは別の判断軸(3-16-29 注2)。
            string diverText;
            if (!SheetPileQuayWall.Plugin.Prompt.TryAskKeyword(
                ed, "\n潜水士船 (打設個所の障害物・打設後異常の調査作業) [計上しない(N)/計上する(Y)] <N>: ",
                new string[] { "N", "Y" }, "N", out diverText))
            {
                return;
            }
            bool needDiverVessel = diverText == "Y";

            // ── 鋼材質量(本管のみ。控え杭は継手を持たない単独杭)──────────
            double W_kgPerM = SheetPileQuayWall.Core.FrontWall.SectionProperties.CalcW(
                a.OuterDm, a.WallTm);
            double bodyMass_kg = W_kgPerM * a.LengthM;
            double steelMass_t = bodyMass_kg / 1000.0;

            int D_mm = (int)System.Math.Round(a.OuterDm * 1000.0);
            int t_mm = (int)System.Math.Round(a.WallTm * 1000.0);

            // ── 積算(3-16-29、3-16-30)──────────────────────────────────
            SheetPileQuayWall.Core.FrontWall.VibroDriveTarget target =
                SheetPileQuayWall.Core.FrontWall.VibroDriveTarget.SteelPipePile;

            double r = SheetPileQuayWall.Core.FrontWall.VibroEstimate.CalcR(
                a.OuterDm, driveLength_m, nTip, nAvg, target);

            string vibroClass =
                SheetPileQuayWall.Core.FrontWall.VibroEstimate.GetVibroClass(steelMass_t, r);
            var (generator, craneVessel) =
                SheetPileQuayWall.Core.FrontWall.VibroEstimate.GetEquipment(vibroClass);

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
                "\n=== 控え杭 施工歩掛積算 (振動工法・バイブロ単独) ===" +
                "\n    出典: 港湾土木請負工事積算基準 令和7年度改訂版 3章16節 3-2" +
                "\n          (4節 3-4.6-9 の注記により 16節を適用)");
            ed.WriteMessage("\n--- 入力条件 ---");
            ed.WriteMessage($"\n  外径 D      : {D_mm,6} mm   肉厚 t : {t_mm,4} mm   全長 L : {a.LengthM,6:F1} m");
            ed.WriteMessage($"\n  打設長 Lb   : {driveLength_m,6:F1} m   本数 N : {pileCount,4} 本   施工区分: 海上 (3-2-1)");
            ed.WriteMessage($"\n  先端N値     : {nTip,6}      加重平均N値: {nAvg,4}      海象 : " +
                $"{(sea == SheetPileQuayWall.Core.FrontWall.SeaCondition.Severe ? "悪い" : "普通")}");
            ed.WriteMessage($"\n  障害        : {(obstacle == SheetPileQuayWall.Core.FrontWall.ObstacleStatus.Exists ? "あり" : "なし")}" +
                $"        継手個所数 : {jointCountPerPile}");

            ed.WriteMessage("\n--- 鋼材質量 (規格選定用) ---");
            ed.WriteMessage($"\n  本管        : {bodyMass_kg,10:F0} kg/本  [確定](K011 単位重量 × L)");
            ed.WriteMessage($"\n  合計        : {steelMass_t,10:F3} t/本  (継手なし。閉端底板は含めない)");

            ed.WriteMessage("\n--- 貫入抵抗値 (3-16-29) ---");
            ed.WriteMessage($"\n  本管 R1     : {r,10:F1} kN  [確定](300·N·Ap + 2·N̄·Lb·As)");
            ed.WriteMessage("\n  継手 Rj     :        0.0 kN  (鋼管杭のため加算なし)");

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
                a.LengthM);
            if (barge.Length == 0)
            {
                ed.WriteMessage(
                    $"\n  台船・引船      : 全長 {a.LengthM:F1}m は規格表の範囲" +
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
            ed.WriteMessage($"\n  打込時間 Tb : {Tb,8:F2} 分/本  (Lb ÷ 0.90 m/分、鋼管杭)");
            ed.WriteMessage($"\n  溶接時間 Tw : {Tw,8:F2} 分/本  (4節 4.5/4.6 を適用)");
            ed.WriteMessage($"\n  打設時間 Tc : {Tc,8:F2} 分/本");
            ed.WriteMessage($"\n  日当り打設  : {Q,8:F2} 本/日  (ei=0.70、T=6h/日)");
            ed.WriteMessage($"\n  打設日数    : {driveDays,8} 日");

            ed.WriteMessage("\n--- 労務編成 (人/日、3-16-31 の鋼管杭の行) ---");
            ed.WriteMessage($"\n  世話役 {labor.foreman} / とび工 {labor.rigger} / " +
                $"普通作業員 {labor.laborer} / 特殊作業員 {labor.specialist} / 溶接工 {labor.welder}");

            ed.WriteMessage("\n--- 適用上の注意 ---");
            ed.WriteMessage("\n  ・本歩掛は海上打設のみに適用する (3-2-1)。**陸上打設は基準に規定が無い**" +
                "(16節 2-1 の陸上歩掛は鋼矢板・H形鋼杭で鋼管杭を含まない)。");
            ed.WriteMessage("\n  ・陸上で振動工法が必要な場合はジェット併用" +
                "(SPQW_ANCHORPILE_VibroJetEstimate、16節 3-1)を用いること。");
            AnchorEstimateHelper.ReportInclinedPileWarning(ed, a.InclDeg);
            ed.WriteMessage("\n  ・玉石混じり層を含む場合の打込み速度は基準上「別途考慮」である。");
            ed.WriteMessage("\n  ・支持層へ打込む/中間層を打抜く場合、バイブロ単独は標準適用外 (3-4.6-9)。");
            ed.WriteMessage("\nSPQW_ANCHORPILE_VibroEstimate 完了。");
        }
    }
}
