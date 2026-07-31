// === 参照DLLバージョン: 未検証 (scripts/verify-dll-versions.ps1 → exit 2) ===
//
// 図面操作の共通ヘルパー(レイヤー作成・モデル空間追加・エンティティ選択・Handle 解決)
// 移植元: 006 EnsureLayer / SelectPileSolid / TryResolveFrontPile、008 TieRodBuilder。

namespace SheetPileQuayWall.Plugin
{
    public static class DrawingHelper
    {
        // 同名レイヤーが無ければ作成する(CLAUDE.PRIVATE.md §2.3: 日本語レイヤー名)。
        public static void EnsureLayer(
            Autodesk.AutoCAD.DatabaseServices.Database db,
            Autodesk.AutoCAD.DatabaseServices.Transaction tr,
            string layerName, int colorIndex)
        {
            Autodesk.AutoCAD.DatabaseServices.LayerTable lt =
                (Autodesk.AutoCAD.DatabaseServices.LayerTable)tr.GetObject(
                    db.LayerTableId,
                    Autodesk.AutoCAD.DatabaseServices.OpenMode.ForRead);

            if (lt.Has(layerName))
            {
                return;
            }

            lt.UpgradeOpen();
            Autodesk.AutoCAD.DatabaseServices.LayerTableRecord ltr =
                new Autodesk.AutoCAD.DatabaseServices.LayerTableRecord();
            ltr.Name = layerName;
            ltr.Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(
                Autodesk.AutoCAD.Colors.ColorMethod.ByAci, (short)colorIndex);
            lt.Add(ltr);
            tr.AddNewlyCreatedDBObject(ltr, true);
        }

        // ソリッドをモデル空間へ追加し、レイヤー・色・XData を設定する。
        public static void AppendSolid(
            Autodesk.AutoCAD.DatabaseServices.Database db,
            Autodesk.AutoCAD.DatabaseServices.Transaction tr,
            Autodesk.AutoCAD.DatabaseServices.Solid3d solid,
            string layerName, int colorIndex,
            Autodesk.AutoCAD.DatabaseServices.ResultBuffer xdata)
        {
            Autodesk.AutoCAD.DatabaseServices.BlockTable bt =
                (Autodesk.AutoCAD.DatabaseServices.BlockTable)tr.GetObject(
                    db.BlockTableId,
                    Autodesk.AutoCAD.DatabaseServices.OpenMode.ForRead);
            Autodesk.AutoCAD.DatabaseServices.BlockTableRecord btr =
                (Autodesk.AutoCAD.DatabaseServices.BlockTableRecord)tr.GetObject(
                    bt[Autodesk.AutoCAD.DatabaseServices.BlockTableRecord.ModelSpace],
                    Autodesk.AutoCAD.DatabaseServices.OpenMode.ForWrite);

            solid.Layer = layerName;
            solid.ColorIndex = colorIndex;
            solid.XData = xdata;

            btr.AppendEntity(solid);
            tr.AddNewlyCreatedDBObject(solid, true);
        }

        // Solid3d の選択を求める。
        public static Autodesk.AutoCAD.EditorInput.PromptEntityResult SelectSolid(
            Autodesk.AutoCAD.EditorInput.Editor ed, string message)
        {
            Autodesk.AutoCAD.EditorInput.PromptEntityOptions opt =
                new Autodesk.AutoCAD.EditorInput.PromptEntityOptions(message);
            opt.SetRejectMessage("\n3D ソリッドを選択してください。");
            opt.AddAllowedClass(typeof(Autodesk.AutoCAD.DatabaseServices.Solid3d), true);
            return ed.GetEntity(opt);
        }

        // Handle 文字列から前壁の XData を復元する。見つからない/消去済みの場合は null。
        public static SheetPileQuayWall.Plugin.XData.FrontWallRecord? TryResolveFrontWall(
            Autodesk.AutoCAD.DatabaseServices.Database db, string handleText)
        {
            if (string.IsNullOrEmpty(handleText))
            {
                return null;
            }

            long handleValue;
            try
            {
                handleValue = System.Convert.ToInt64(handleText, 16);
            }
            catch (System.Exception)
            {
                return null;
            }

            Autodesk.AutoCAD.DatabaseServices.ObjectId id;
            if (!db.TryGetObjectId(
                new Autodesk.AutoCAD.DatabaseServices.Handle(handleValue), out id))
            {
                return null;
            }

            if (id.IsErased)
            {
                return null;
            }

            SheetPileQuayWall.Plugin.XData.FrontWallRecord? record = null;
            using (Autodesk.AutoCAD.DatabaseServices.Transaction tr =
                db.TransactionManager.StartTransaction())
            {
                Autodesk.AutoCAD.DatabaseServices.Solid3d? solid =
                    tr.GetObject(id, Autodesk.AutoCAD.DatabaseServices.OpenMode.ForRead)
                    as Autodesk.AutoCAD.DatabaseServices.Solid3d;
                if (solid != null)
                {
                    record = SheetPileQuayWall.Plugin.XData.FrontWallRecord.Read(solid);
                }
                tr.Commit();
            }
            return record;
        }

        // 前壁の選択を求め、その XData と Handle を返す。選択中止・XData 無しの場合は null。
        public static SheetPileQuayWall.Plugin.XData.FrontWallRecord? SelectFrontWall(
            Autodesk.AutoCAD.EditorInput.Editor ed,
            Autodesk.AutoCAD.DatabaseServices.Database db,
            string message, out string handleText)
        {
            handleText = "";

            Autodesk.AutoCAD.EditorInput.PromptEntityResult res = SelectSolid(ed, message);
            if (res.Status != Autodesk.AutoCAD.EditorInput.PromptStatus.OK)
            {
                return null;
            }

            SheetPileQuayWall.Plugin.XData.FrontWallRecord? record = null;
            string handle = "";
            using (Autodesk.AutoCAD.DatabaseServices.Transaction tr =
                db.TransactionManager.StartTransaction())
            {
                Autodesk.AutoCAD.DatabaseServices.Solid3d? solid =
                    tr.GetObject(res.ObjectId,
                        Autodesk.AutoCAD.DatabaseServices.OpenMode.ForRead)
                    as Autodesk.AutoCAD.DatabaseServices.Solid3d;
                if (solid != null)
                {
                    record = SheetPileQuayWall.Plugin.XData.FrontWallRecord.Read(solid);
                    handle = solid.Handle.ToString();
                }
                tr.Commit();
            }

            if (record == null)
            {
                ed.WriteMessage(
                    $"\nエラー: 選択したソリッドに前壁の XData " +
                    $"(RegApp: {SheetPileQuayWall.Plugin.XData.FrontWallRecord.RegAppName}) " +
                    $"がありません。SPQW_FRONTWALL_Create で作成したソリッドを選択してください。");
                return null;
            }

            handleText = handle;
            return record;
        }

        // モデル空間内の SPQW_FRONTWALL XData を持つ全 Solid3d を走査し、杭中心 Y が
        // 最小のもの(壁の 1 本目)のレコードと Handle を返す。該当が無ければ null。
        // SPQW_TIEROD_Create の前壁自動選択(2026-07-31。従来は選択を求めていたが、
        // 壁の途中の矢板を選ぶと基準がずれるため、常に 1 本目を使う)と、控え杭の
        // 配置開始位置の自動整列(2026-07-29)に使う。
        public static SheetPileQuayWall.Plugin.XData.FrontWallRecord? FindFirstFrontWall(
            Autodesk.AutoCAD.DatabaseServices.Database db, out string handleText)
        {
            SheetPileQuayWall.Plugin.XData.FrontWallRecord? best = null;
            string bestHandle = "";

            using (Autodesk.AutoCAD.DatabaseServices.Transaction tr =
                db.TransactionManager.StartTransaction())
            {
                Autodesk.AutoCAD.DatabaseServices.BlockTable bt =
                    (Autodesk.AutoCAD.DatabaseServices.BlockTable)tr.GetObject(
                        db.BlockTableId,
                        Autodesk.AutoCAD.DatabaseServices.OpenMode.ForRead);
                Autodesk.AutoCAD.DatabaseServices.BlockTableRecord btr =
                    (Autodesk.AutoCAD.DatabaseServices.BlockTableRecord)tr.GetObject(
                        bt[Autodesk.AutoCAD.DatabaseServices.BlockTableRecord.ModelSpace],
                        Autodesk.AutoCAD.DatabaseServices.OpenMode.ForRead);

                foreach (Autodesk.AutoCAD.DatabaseServices.ObjectId id in btr)
                {
                    Autodesk.AutoCAD.DatabaseServices.Solid3d? solid =
                        tr.GetObject(id, Autodesk.AutoCAD.DatabaseServices.OpenMode.ForRead)
                        as Autodesk.AutoCAD.DatabaseServices.Solid3d;
                    if (solid == null)
                    {
                        continue;
                    }

                    SheetPileQuayWall.Plugin.XData.FrontWallRecord? record =
                        SheetPileQuayWall.Plugin.XData.FrontWallRecord.Read(solid);
                    if (record == null)
                    {
                        continue;
                    }

                    if (best == null || record.HeadPoint.Y < best.HeadPoint.Y)
                    {
                        best = record;
                        bestHandle = solid.Handle.ToString();
                    }
                }

                tr.Commit();
            }

            handleText = bestHandle;
            return best;
        }

        // 杭中心 Y の最小値。該当が無ければ null。
        public static double? MinFrontWallY(
            Autodesk.AutoCAD.DatabaseServices.Database db)
        {
            string handle;
            SheetPileQuayWall.Plugin.XData.FrontWallRecord? first =
                FindFirstFrontWall(db, out handle);
            return first == null ? (double?)null : first.HeadPoint.Y;
        }

        // モデル空間内の SPQW_TIEROD XData を持つ全 Solid3d を走査し、位置 Y が最小の
        // ものを返す。該当が無ければ null。SPQW_ANCHORPILE_Create の自動選択
        // (2026-07-31)。前壁は返り値の FrontHandle から解決する。
        public static SheetPileQuayWall.Plugin.XData.TieRodRecord? FindFirstTieRod(
            Autodesk.AutoCAD.DatabaseServices.Database db)
        {
            SheetPileQuayWall.Plugin.XData.TieRodRecord? best = null;

            using (Autodesk.AutoCAD.DatabaseServices.Transaction tr =
                db.TransactionManager.StartTransaction())
            {
                Autodesk.AutoCAD.DatabaseServices.BlockTable bt =
                    (Autodesk.AutoCAD.DatabaseServices.BlockTable)tr.GetObject(
                        db.BlockTableId,
                        Autodesk.AutoCAD.DatabaseServices.OpenMode.ForRead);
                Autodesk.AutoCAD.DatabaseServices.BlockTableRecord btr =
                    (Autodesk.AutoCAD.DatabaseServices.BlockTableRecord)tr.GetObject(
                        bt[Autodesk.AutoCAD.DatabaseServices.BlockTableRecord.ModelSpace],
                        Autodesk.AutoCAD.DatabaseServices.OpenMode.ForRead);

                foreach (Autodesk.AutoCAD.DatabaseServices.ObjectId id in btr)
                {
                    Autodesk.AutoCAD.DatabaseServices.Solid3d? solid =
                        tr.GetObject(id, Autodesk.AutoCAD.DatabaseServices.OpenMode.ForRead)
                        as Autodesk.AutoCAD.DatabaseServices.Solid3d;
                    if (solid == null)
                    {
                        continue;
                    }

                    SheetPileQuayWall.Plugin.XData.TieRodRecord? record =
                        SheetPileQuayWall.Plugin.XData.TieRodRecord.Read(solid);
                    if (record == null)
                    {
                        continue;
                    }

                    if (best == null || record.PositionY < best.PositionY)
                    {
                        best = record;
                    }
                }

                tr.Commit();
            }

            return best;
        }

        // 既存ソリッドを消去する(_Action の再生成前)。
        public static void EraseSolid(
            Autodesk.AutoCAD.DatabaseServices.Transaction tr,
            Autodesk.AutoCAD.DatabaseServices.ObjectId id)
        {
            Autodesk.AutoCAD.DatabaseServices.Entity? entity =
                tr.GetObject(id, Autodesk.AutoCAD.DatabaseServices.OpenMode.ForWrite)
                as Autodesk.AutoCAD.DatabaseServices.Entity;
            if (entity != null)
            {
                entity.Erase();
            }
        }
    }
}
