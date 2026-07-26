// === 参照DLLバージョン: 未検証 ===
// AcCoreMgd.dll : 未検証 (期待値 25.x.x.x)
// AcDbMgd.dll   : 未検証 (期待値 25.x.x.x)
// AcMgd.dll     : 未検証 (期待値 25.x.x.x)
// 検証日: 未実施 / 検証コマンド: scripts/verify-dll-versions.ps1 → exit 2 (未検出)
// リスク: TypeLoadException / MissingMethodException が実行時に発生する可能性がある。
//
// 控え杭の打撃工法・打設歩掛積算(SPQW_ANCHORPILE_Estimate、陸上打設のみ)
// 出典: 港湾土木請負工事積算基準 令和7年度改訂版 4節 本体工 4.6「鋼杭式」(3-4.6-9〜17)
//
// 控え杭は継手を持たない単独の鋼管杭であり、前壁鋼管矢板(4.5「鋼矢板式」)とは
// 節が異なる。ただし実データ突き合わせの結果、貫入抵抗値 R・ハンマ規格決定図・
// 打撃速度 Sb 表・溶接時間表・準備時間 Tp・基準作業能力係数は 4.5 と数値まで
// 完全一致するため、Core.FrontWall.DriveEstimate の該当メソッドをそのまま
// 呼び出す。差異があるのは打撃時間 Tb の係数 K(直杭1.0/斜杭1.2。4.5 は斜杭の
// 値を定義していない)と労務編成のみで、これらは Core.AnchorPile.AnchorDriveEstimate
// に新規実装した(詳細は同ファイルのコメント参照)。
//
// 適用範囲は本コマンドでは**陸上打設のみ**。海上打設(独自の船団構成・労務編成を
// 持つ、3-4.6-11〜16)は未実装。

namespace SheetPileQuayWall.Plugin.Commands
{
    public static class AnchorDriveEstimateCommand
    {
        // ════════════════════════════════════════════════════════════════════
        // SPQW_ANCHORPILE_Estimate: 控え杭選択 → 打撃工法(陸上)の施工歩掛積算
        // ════════════════════════════════════════════════════════════════════
        [Autodesk.AutoCAD.Runtime.CommandMethod("SPQW_ANCHORPILE_Estimate")]
        public static void Estimate()
        {
            Autodesk.AutoCAD.ApplicationServices.Document doc =
                Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager
                    .MdiActiveDocument;
            Autodesk.AutoCAD.DatabaseServices.Database db = doc.Database;
            Autodesk.AutoCAD.EditorInput.Editor ed = doc.Editor;

            Autodesk.AutoCAD.EditorInput.PromptEntityResult res =
                SheetPileQuayWall.Plugin.DrawingHelper.SelectSolid(
                    ed, "\n積算する控え杭 (SPQW_ANCHORPILE) を選択: ");
            if (res.Status != Autodesk.AutoCAD.EditorInput.PromptStatus.OK)
            {
                return;
            }

            SheetPileQuayWall.Plugin.XData.AnchorPileRecord? record = null;
            using (Autodesk.AutoCAD.DatabaseServices.Transaction tr =
                db.TransactionManager.StartTransaction())
            {
                Autodesk.AutoCAD.DatabaseServices.Solid3d? solid =
                    tr.GetObject(res.ObjectId, Autodesk.AutoCAD.DatabaseServices.OpenMode.ForRead)
                    as Autodesk.AutoCAD.DatabaseServices.Solid3d;
                if (solid != null)
                {
                    record = SheetPileQuayWall.Plugin.XData.AnchorPileRecord.Read(solid);
                }
                tr.Commit();
            }

            if (record == null)
            {
                ed.WriteMessage("\nエラー: 控え杭の XData が見つかりません。");
                return;
            }

            SheetPileQuayWall.Core.AnchorPile.AnchorInput a = record.Input;

            ed.WriteMessage(
                "\n--- 打撃工法・陸上打設 (積算基準 4節 本体工 4.6 鋼杭式) ---");

            // ── 施工条件 ────────────────────────────────────────────────
            double penetration_m;
            if (!SheetPileQuayWall.Plugin.Prompt.TryAskDouble(
                ed, $"\n根入れ長 (m) <{a.LengthM * 0.5:F1}>: ",
                a.LengthM * 0.5, 0.1, a.LengthM, out penetration_m))
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
                ed, "\n先端 N 値 <50>: ", 50, 1, 100, out nTip))
            {
                return;
            }

            int nAvg;
            if (!SheetPileQuayWall.Plugin.Prompt.TryAskInt(
                ed, "\n加重平均 N 値 <20>: ", 20, 1, 100, out nAvg))
            {
                return;
            }

            int jointCountPerPile;
            if (!SheetPileQuayWall.Plugin.Prompt.TryAskInt(
                ed, "\n継杭の継手個所数 (単杭=0) <0>: ", 0, 0, 5, out jointCountPerPile))
            {
                return;
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

            string craneText;
            if (!SheetPileQuayWall.Plugin.Prompt.TryAskKeyword(
                ed, "\nクローラクレーン (小運搬用) [計上しない(N)/計上する(Y)] <N>: ",
                new string[] { "N", "Y" }, "N", out craneText))
            {
                return;
            }
            bool needCrawlerCrane = craneText == "Y";

            // ── 鋼材質量(本管のみ。控え杭は継手を持たない単独杭)──────────
            double W_kgPerM = SheetPileQuayWall.Core.FrontWall.SectionProperties.CalcW(
                a.OuterDm, a.WallTm);
            double mass1_kg = W_kgPerM * a.LengthM;
            double totalMass_t = mass1_kg * pileCount / 1000.0;

            int D_mm = (int)System.Math.Round(a.OuterDm * 1000.0);
            int t_mm = (int)System.Math.Round(a.WallTm * 1000.0);

            // ── 積算(4節 3-4.6。共有部分は FrontWall.DriveEstimate を再利用)────
            double R = SheetPileQuayWall.Core.FrontWall.DriveEstimate.CalcR(
                a.OuterDm, penetration_m, nTip, nAvg);
            string hammer = SheetPileQuayWall.Core.FrontWall.DriveEstimate.GetHammerClass(
                mass1_kg / 1000.0, R);

            double Sb = SheetPileQuayWall.Core.FrontWall.DriveEstimate.GetSb(D_mm, nAvg);
            double Tp = SheetPileQuayWall.Core.FrontWall.DriveEstimate.CalcTp(
                SheetPileQuayWall.Core.FrontWall.ConstructionSite.Onshore, jointCountPerPile);
            double Tb = SheetPileQuayWall.Core.AnchorPile.AnchorDriveEstimate.CalcTb(
                penetration_m, Sb, a.InclDeg);
            double Tw = SheetPileQuayWall.Core.FrontWall.DriveEstimate.CalcTw(
                D_mm, t_mm, jointCountPerPile);
            double Tc = Tp + Tb + Tw;

            double Q = SheetPileQuayWall.Core.FrontWall.DriveEstimate.CalcQ(
                SheetPileQuayWall.Core.FrontWall.ConstructionSite.Onshore, Tc,
                SheetPileQuayWall.Core.FrontWall.SeaCondition.Normal, obstacle, pileCount);
            if (Q <= 0.0)
            {
                ed.WriteMessage("\nエラー: 1 日当り打設本数が 0 以下になりました。入力条件を確認してください。");
                return;
            }
            int driveDays = (int)System.Math.Ceiling(pileCount / Q);

            var labor = SheetPileQuayWall.Core.AnchorPile.AnchorDriveEstimate.GetLabor(
                a.LengthM, jointCountPerPile > 0, D_mm);

            bool inclined = System.Math.Abs(a.InclDeg) >
                SheetPileQuayWall.Core.AnchorPile.AnchorDriveEstimate.InclinationTolerance_deg;

            // ── 出力 ────────────────────────────────────────────────────
            ed.WriteMessage(
                "\n=== 控え杭 施工歩掛積算 (打撃工法・陸上打設) ===" +
                "\n    出典: 港湾土木請負工事積算基準 令和7年度改訂版 4節 本体工 4.6 鋼杭式");
            ed.WriteMessage("\n--- 入力条件 ---");
            ed.WriteMessage($"\n  外径 D      : {D_mm,6} mm   肉厚 t : {t_mm,4} mm   全長 L : {a.LengthM,6:F1} m");
            ed.WriteMessage($"\n  根入れ長    : {penetration_m,6:F1} m   本数 N : {pileCount,4} 本   施工区分: 陸上");
            ed.WriteMessage($"\n  傾斜角 θ    : {a.InclDeg,6:F1} deg  ({(inclined ? "斜杭 K=1.2" : "直杭 K=1.0")})");
            ed.WriteMessage($"\n  先端N値     : {nTip,6}      加重平均N値: {nAvg,4}");
            ed.WriteMessage($"\n  障害        : {(obstacle == SheetPileQuayWall.Core.FrontWall.ObstacleStatus.Exists ? "あり" : "なし")}" +
                $"        継手個所数 : {jointCountPerPile}");

            ed.WriteMessage("\n--- 鋼材質量 ---");
            ed.WriteMessage($"\n  単位重量 W  : {W_kgPerM,10:F2} kg/m");
            ed.WriteMessage($"\n  1本当り質量 : {mass1_kg,10:F0} kg/本");
            ed.WriteMessage($"\n  合計質量    : {totalMass_t,10:F2} t   ({pileCount}本合計)");

            ed.WriteMessage("\n--- 貫入抵抗値・ハンマ規格 (3-4.6-12) ---");
            ed.WriteMessage($"\n  貫入抵抗値 R: {R,10:F1} kN");
            ed.WriteMessage($"\n  推奨ハンマ  :  {hammer}");

            ed.WriteMessage("\n--- 作業船・機械 (3-4.6-12。杭打機規格は[推定]、原本確認を推奨) ---");
            string crawlerDriver =
                SheetPileQuayWall.Core.FrontWall.DriveEquipment.GetCrawlerDriver(hammer);
            ed.WriteMessage(crawlerDriver.Length == 0
                ? "\n  クローラ式杭打機: 規格表の範囲外です。基準の規格決定図を超えるため別途検討してください。"
                : $"\n  クローラ式杭打機: {crawlerDriver}");
            if (needCrawlerCrane)
            {
                ed.WriteMessage(
                    $"\n  クローラクレーン: {SheetPileQuayWall.Core.FrontWall.DriveEquipment.CrawlerCraneSpec}" +
                    " (小運搬用、必要に応じて計上)");
            }

            ed.WriteMessage("\n--- 施工能力 (打設、3-4.6-13〜14) ---");
            ed.WriteMessage($"\n  打撃速度 Sb : {Sb,8:F2} m/分");
            ed.WriteMessage($"\n  準備時間 Tp : {Tp,8:F1} 分/本");
            ed.WriteMessage($"\n  打撃時間 Tb : {Tb,8:F1} 分/本  (K×L÷Sb、K={(inclined ? "1.2" : "1.0")})");
            ed.WriteMessage($"\n  溶接時間 Tw : {Tw,8:F1} 分/本");
            ed.WriteMessage($"\n  打設時間 Tc : {Tc,8:F1} 分/本");
            ed.WriteMessage($"\n  日当り打設  : {Q,8:F2} 本/日");
            ed.WriteMessage($"\n  打設日数    : {driveDays,8} 日");

            ed.WriteMessage("\n--- 労務編成 (人/日、3-4.6-15) ---");
            ed.WriteMessage($"\n  世話役 {labor.foreman} / とび工 {labor.rigger} / " +
                $"普通作業員 {labor.laborer} / 溶接工 {labor.welder}");

            ed.WriteMessage("\n--- 適用上の注意 ---");
            ed.WriteMessage("\n  ・本歩掛は陸上打設のみに適用する。海上打設は未実装(3-4.6-11〜16 参照)。");
            ed.WriteMessage("\n  ・本管質量のみで規格選定している(前壁の打撃工法 _Estimate と同じ扱い)。");
            ed.WriteMessage("\nSPQW_ANCHORPILE_Estimate 完了。");
        }
    }
}
