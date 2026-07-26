// === 参照DLLバージョン: 未検証 ===
// AcCoreMgd.dll : 未検証 (期待値 25.x.x.x)
// AcDbMgd.dll   : 未検証 (期待値 25.x.x.x)
// AcMgd.dll     : 未検証 (期待値 25.x.x.x)
// 検証日: 未実施 / 検証コマンド: scripts/verify-dll-versions.ps1 → exit 2 (未検出)
// リスク: TypeLoadException / MissingMethodException が実行時に発生する可能性がある。
//
// 岸壁 1 施設分の数量集計コマンド(SPQW_QUAYWALL_Estimate)
//
// 006/007/008 はいずれも部材単体の数量しか出せなかった。009 の統合版としての
// 付加価値として、図面中の 3 部材の XData を集めて施設全体の鋼材質量を集計する。
// フェーズ 5 で新設。

namespace SheetPileQuayWall.Plugin.Commands
{
    public static class QuayWallCommands
    {
        // ════════════════════════════════════════════════════════════════════
        // SPQW_QUAYWALL_Estimate: 前壁を選択 → 図面中の 3 部材を集計
        // ════════════════════════════════════════════════════════════════════
        [Autodesk.AutoCAD.Runtime.CommandMethod("SPQW_QUAYWALL_Estimate")]
        public static void Estimate()
        {
            Autodesk.AutoCAD.ApplicationServices.Document doc =
                Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager
                    .MdiActiveDocument;
            Autodesk.AutoCAD.DatabaseServices.Database db = doc.Database;
            Autodesk.AutoCAD.EditorInput.Editor ed = doc.Editor;

            SheetPileQuayWall.Plugin.XData.FrontWallRecord? front;
            SheetPileQuayWall.Core.QuayWallComposition? composition =
                BuildCompositionFromPrompts(ed, db, out front);
            if (composition == null || front == null)
            {
                return;
            }

            SheetPileQuayWall.Core.QuayWallComposition c = composition;

            SheetPileQuayWall.Core.QuayWallQuantity q =
                SheetPileQuayWall.Core.QuayWallEstimate.Compute(c);

            ed.WriteMessage("\n=== 鋼管矢板式岸壁 1 施設分 数量集計 ===");
            ed.WriteMessage("\n--- 構成 ---");
            ed.WriteMessage($"\n  前壁鋼管矢板  : D={c.FrontOuterDm * 1000:F1}mm " +
                $"t={c.FrontWallTm * 1000:F1}mm L={c.FrontLengthM:F1}m " +
                $"{front.JointCode} × {c.FrontPieceCount} 本");
            ed.WriteMessage($"\n  タイロッド    : {c.TieRodSetCount} 組" +
                (c.TieRodSetCount > 0 ? $" (1組 {c.TieRodMassPerSet:F1} kg)" : ""));
            ed.WriteMessage($"\n  控え杭        : D={c.AnchorOuterDm * 1000:F1}mm " +
                $"L={c.AnchorLengthM:F1}m × {c.AnchorPileCount} 本" +
                (c.AnchorClosedTip ? " (閉端)" : ""));
            ed.WriteMessage("\n--- 数量 ---");
            ed.WriteMessage($"\n  施設延長      : {q.WallLengthM,12:F3} m  [確定]");
            ed.WriteMessage($"\n  継手接続数    : {q.JointConnectionCount,12} 箇所  [確定]");
            ed.WriteMessage("\n--- 鋼材質量 ---");
            ed.WriteMessage($"\n  前壁 本管     : {q.FrontBodyKg,12:F0} kg  [確定]");
            ed.WriteMessage($"\n  前壁 継手金物 : {q.FrontJointKg,12:F0} kg  [確定]");
            ed.WriteMessage($"\n  タイロッド棒部: {q.TieRodKg,12:F0} kg  [確定](付属品を含まない)");
            ed.WriteMessage($"\n  控え杭 本管   : {q.AnchorBodyKg,12:F0} kg  [確定]");
            if (c.AnchorClosedTip)
            {
                ed.WriteMessage($"\n  控え杭 底板   : {q.AnchorPlateKg,12:F0} kg  [概算]");
            }
            ed.WriteMessage($"\n  合計          : {q.TotalKg,12:F0} kg " +
                $"({q.TotalKg / 1000.0:F2} t)");
            ed.WriteMessage("\nSPQW_QUAYWALL_Estimate 完了。");
        }

        // ────────────────────────────────────────────────────────────────────
        // 前壁選択 + タイロッド/控え杭の代表選択から QuayWallComposition を組み立てる。
        // SPQW_QUAYWALL_Estimate と ImportCommands.SPQW_QUAYWALL_ReconcileCsv が共有する。
        // 中断・前壁未選択の場合は null(front も null)。
        // ────────────────────────────────────────────────────────────────────
        internal static SheetPileQuayWall.Core.QuayWallComposition? BuildCompositionFromPrompts(
            Autodesk.AutoCAD.EditorInput.Editor ed,
            Autodesk.AutoCAD.DatabaseServices.Database db,
            out SheetPileQuayWall.Plugin.XData.FrontWallRecord? front)
        {
            front = null;

            // 代表となる前壁を選択させ、その諸元を施設全体の代表値として使う。
            // (壁を構成する矢板は同一諸元である前提。異なる場合は本数で按分せず、
            //  選択した矢板の諸元 × 総本数で概算する)
            string frontHandle;
            SheetPileQuayWall.Plugin.XData.FrontWallRecord? f =
                SheetPileQuayWall.Plugin.DrawingHelper.SelectFrontWall(
                    ed, db, "\n代表となる前壁鋼管矢板 (SPQW_FRONTWALL) を選択: ",
                    out frontHandle);
            if (f == null)
            {
                return null;
            }
            front = f;

            SheetPileQuayWall.Core.QuayWallComposition c =
                new SheetPileQuayWall.Core.QuayWallComposition();

            c.FrontOuterDm = f.OuterDm;
            c.FrontWallTm = f.WallTm;
            c.FrontLengthM = f.LengthM;
            c.FrontJointType =
                SheetPileQuayWall.Core.FrontWall.JointParameters.FromCode(f.JointCode);
            c.FrontPieceCount = f.PieceCount;

            // ── タイロッド ──────────────────────────────────────────────
            int tieRodSetCount;
            if (!SheetPileQuayWall.Plugin.Prompt.TryAskInt(
                ed, "\nタイロッドの組数 (0 で計上しない) <0>: ",
                0, 0, 500, out tieRodSetCount))
            {
                return null;
            }

            if (tieRodSetCount > 0)
            {
                Autodesk.AutoCAD.EditorInput.PromptEntityResult res =
                    SheetPileQuayWall.Plugin.DrawingHelper.SelectSolid(
                        ed, "\n代表となるタイロッド (Solid3d) を選択: ");
                if (res.Status != Autodesk.AutoCAD.EditorInput.PromptStatus.OK)
                {
                    return null;
                }

                SheetPileQuayWall.Plugin.XData.TieRodRecord? tieRod = null;
                using (Autodesk.AutoCAD.DatabaseServices.Transaction tr =
                    db.TransactionManager.StartTransaction())
                {
                    Autodesk.AutoCAD.DatabaseServices.Solid3d? solid =
                        tr.GetObject(res.ObjectId,
                            Autodesk.AutoCAD.DatabaseServices.OpenMode.ForRead)
                        as Autodesk.AutoCAD.DatabaseServices.Solid3d;
                    if (solid != null)
                    {
                        tieRod = SheetPileQuayWall.Plugin.XData.TieRodRecord.Read(solid);
                    }
                    tr.Commit();
                }

                if (tieRod == null)
                {
                    ed.WriteMessage("\nエラー: タイロッドの XData が見つかりません。");
                    return null;
                }

                try
                {
                    SheetPileQuayWall.Core.TieRod.TieRodResult r =
                        SheetPileQuayWall.Core.TieRod.TieRodCalculator.Compute(
                            tieRod.Parameters);
                    c.TieRodSetCount = tieRodSetCount;
                    c.TieRodMassPerSet = r.RodMass;
                }
                catch (System.ArgumentException ex)
                {
                    ed.WriteMessage($"\n{ex.Message}\n集計を中止しました。");
                    return null;
                }
            }

            // ── 控え杭 ──────────────────────────────────────────────────
            int anchorPileCount;
            if (!SheetPileQuayWall.Plugin.Prompt.TryAskInt(
                ed, "\n控え杭の本数 (0 で計上しない) <0>: ",
                0, 0, 500, out anchorPileCount))
            {
                return null;
            }

            if (anchorPileCount > 0)
            {
                Autodesk.AutoCAD.EditorInput.PromptEntityResult res =
                    SheetPileQuayWall.Plugin.DrawingHelper.SelectSolid(
                        ed, "\n代表となる控え杭 (Solid3d) を選択: ");
                if (res.Status != Autodesk.AutoCAD.EditorInput.PromptStatus.OK)
                {
                    return null;
                }

                SheetPileQuayWall.Plugin.XData.AnchorPileRecord? anchor = null;
                using (Autodesk.AutoCAD.DatabaseServices.Transaction tr =
                    db.TransactionManager.StartTransaction())
                {
                    Autodesk.AutoCAD.DatabaseServices.Solid3d? solid =
                        tr.GetObject(res.ObjectId,
                            Autodesk.AutoCAD.DatabaseServices.OpenMode.ForRead)
                        as Autodesk.AutoCAD.DatabaseServices.Solid3d;
                    if (solid != null)
                    {
                        anchor = SheetPileQuayWall.Plugin.XData.AnchorPileRecord.Read(solid);
                    }
                    tr.Commit();
                }

                if (anchor == null)
                {
                    ed.WriteMessage("\nエラー: 控え杭の XData が見つかりません。");
                    return null;
                }

                c.AnchorPileCount = anchorPileCount;
                c.AnchorOuterDm = anchor.Input.OuterDm;
                c.AnchorWallTm = anchor.Input.WallTm;
                c.AnchorLengthM = anchor.Input.LengthM;
                c.AnchorClosedTip = anchor.Input.ClosedTip;
            }

            return c;
        }
    }
}
