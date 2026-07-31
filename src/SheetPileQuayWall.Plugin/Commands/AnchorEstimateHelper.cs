// === 参照DLLバージョン: 未検証 ===
// AcCoreMgd.dll : 未検証 (期待値 25.x.x.x)
// AcDbMgd.dll   : 未検証 (期待値 25.x.x.x)
// AcMgd.dll     : 未検証 (期待値 25.x.x.x)
// 検証日: 未実施 / 検証コマンド: scripts/verify-dll-versions.ps1 → exit 2 (未検出)
//
// 控え杭の打設歩掛積算コマンド 3 種(打撃 / バイブロ単独 / ジェット併用)で共通する
// 選択処理と、振動工法固有の斜杭警告。振動工法のコマンド追加(2026-08-01)で
// 2 コマンドが同じ処理を持つことになったため切り出した。

namespace SheetPileQuayWall.Plugin.Commands
{
    internal static class AnchorEstimateHelper
    {
        // 控え杭ソリッドを選択し、XData から諸元を復元する。選択中止・XData 無しは null。
        internal static SheetPileQuayWall.Core.AnchorPile.AnchorInput? SelectAnchorInput(
            Autodesk.AutoCAD.EditorInput.Editor ed,
            Autodesk.AutoCAD.DatabaseServices.Database db,
            string prompt)
        {
            Autodesk.AutoCAD.EditorInput.PromptEntityResult res =
                SheetPileQuayWall.Plugin.DrawingHelper.SelectSolid(ed, prompt);
            if (res.Status != Autodesk.AutoCAD.EditorInput.PromptStatus.OK)
            {
                return null;
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
                return null;
            }

            return record.Input;
        }

        // 振動工法の基準作業能力係数 ei は直杭を前提とし、斜杭は「別途検討」と定める
        // (3-16-19 の ei 定義)。打撃工法の K=1.2(3-4.6-14)に相当する係数は
        // 振動工法に無いため、直杭の値で計算したうえで注意を出す(2026-08-01 決定)。
        internal static void ReportInclinedPileWarning(
            Autodesk.AutoCAD.EditorInput.Editor ed, double inclDeg)
        {
            bool inclined = System.Math.Abs(inclDeg) >
                SheetPileQuayWall.Core.AnchorPile.AnchorDriveEstimate.InclinationTolerance_deg;
            if (!inclined)
            {
                return;
            }

            ed.WriteMessage(
                $"\n  ・**傾斜角 θ={inclDeg:F1}° の斜杭です**。振動工法の基準作業能力係数 ei は" +
                "直杭を前提とし、斜杭は基準上「別途検討」です(打撃工法の K=1.2 に相当する" +
                "係数は振動工法にありません)。上記は直杭の値で算出した参考値であり、" +
                "斜杭の能力低下は別途考慮してください。");
        }
    }
}
