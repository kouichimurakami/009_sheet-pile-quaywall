// === 参照DLLバージョン: 未検証 (scripts/verify-dll-versions.ps1 → exit 2) ===
//
// Solid3d の生成(断面ポリライン → Region → Extrude、CLAUDE.PRIVATE.md §2.3)
// 移植元: 007 SPSP_Create.CreateHollowCylinder / SPSP_JointModel.BuildJointMember・BuildPrism、
//         006 CreateDisk、008 TieRodBuilder。
//
// 決定10 で SPQW_FRONTWALL_JointModel コマンドは移植しないが、継手部材の押し出し
// (BuildJointMember)は _Create が使うため本クラスへ移した。
//
// 生成物はいずれもローカル座標(Z=0 が下端)で、配置変換は呼び出し側が行う。
// 部材 1 本につき Solid3d 1 個に集約する(§8 の「複数のまま残さない」の解釈)。

namespace SheetPileQuayWall.Plugin
{
    public static class SolidBuilder
    {
        // 中空円筒(Z=0 〜 Z=+height_m)。Circle → Region → BoolSubtract → Extrude
        public static Autodesk.AutoCAD.DatabaseServices.Solid3d HollowCylinder(
            double outerR_m, double innerR_m, double height_m)
        {
            Autodesk.AutoCAD.DatabaseServices.Circle outer =
                new Autodesk.AutoCAD.DatabaseServices.Circle(
                    Autodesk.AutoCAD.Geometry.Point3d.Origin,
                    Autodesk.AutoCAD.Geometry.Vector3d.ZAxis, outerR_m);
            Autodesk.AutoCAD.DatabaseServices.Circle inner =
                new Autodesk.AutoCAD.DatabaseServices.Circle(
                    Autodesk.AutoCAD.Geometry.Point3d.Origin,
                    Autodesk.AutoCAD.Geometry.Vector3d.ZAxis, innerR_m);

            Autodesk.AutoCAD.DatabaseServices.Region outerRegion = RegionFromCurve(outer);
            Autodesk.AutoCAD.DatabaseServices.Region innerRegion = RegionFromCurve(inner);

            // innerRegion は BoolSubtract で消費される
            outerRegion.BooleanOperation(
                Autodesk.AutoCAD.DatabaseServices.BooleanOperationType.BoolSubtract,
                innerRegion);

            Autodesk.AutoCAD.DatabaseServices.Solid3d solid =
                new Autodesk.AutoCAD.DatabaseServices.Solid3d();
            solid.Extrude(outerRegion, height_m, 0.0);

            outer.Dispose();
            inner.Dispose();
            outerRegion.Dispose();
            return solid;
        }

        // 円板(Z=0 〜 Z=+thickness_m)。閉端杭の底板に使う。
        public static Autodesk.AutoCAD.DatabaseServices.Solid3d Disk(
            double radius_m, double thickness_m)
        {
            Autodesk.AutoCAD.DatabaseServices.Circle circle =
                new Autodesk.AutoCAD.DatabaseServices.Circle(
                    Autodesk.AutoCAD.Geometry.Point3d.Origin,
                    Autodesk.AutoCAD.Geometry.Vector3d.ZAxis, radius_m);

            Autodesk.AutoCAD.DatabaseServices.Region region = RegionFromCurve(circle);

            Autodesk.AutoCAD.DatabaseServices.Solid3d solid =
                new Autodesk.AutoCAD.DatabaseServices.Solid3d();
            solid.Extrude(region, thickness_m, 0.0);

            circle.Dispose();
            region.Dispose();
            return solid;
        }

        // 2D 閉プロファイル(頂点列 [x0,y0,x1,y1,...] m)を +Z 方向へ押し出す。
        public static Autodesk.AutoCAD.DatabaseServices.Solid3d Prism(
            double[] xyFlat, double height_m)
        {
            Autodesk.AutoCAD.DatabaseServices.Polyline polyline =
                new Autodesk.AutoCAD.DatabaseServices.Polyline();
            int vertexCount = xyFlat.Length / 2;
            for (int i = 0; i < vertexCount; i++)
            {
                polyline.AddVertexAt(i,
                    new Autodesk.AutoCAD.Geometry.Point2d(xyFlat[2 * i], xyFlat[2 * i + 1]),
                    0.0, 0.0, 0.0);
            }
            polyline.Closed = true;

            Autodesk.AutoCAD.DatabaseServices.Region region = RegionFromCurve(polyline);

            Autodesk.AutoCAD.DatabaseServices.Solid3d solid =
                new Autodesk.AutoCAD.DatabaseServices.Solid3d();
            solid.Extrude(region, height_m, 0.0);

            polyline.Dispose();
            region.Dispose();
            return solid;
        }

        // 継手部材(実形状の閉ループ群)を 1 個のソリッドへ集約する。
        // loops は JointShapes.LoopsA / LoopsB、phiRad は取り付け角(+Y 側 = +π/2)。
        public static Autodesk.AutoCAD.DatabaseServices.Solid3d JointMember(
            double[][] loops, double outerR_m, double phiRad, double height_m)
        {
            Autodesk.AutoCAD.DatabaseServices.Solid3d? merged = null;

            for (int i = 0; i < loops.Length; i++)
            {
                Autodesk.AutoCAD.DatabaseServices.Solid3d prism = Prism(
                    SheetPileQuayWall.Core.FrontWall.JointPlacement.TransformLoop(
                        loops[i], outerR_m, phiRad),
                    height_m);

                if (merged == null)
                {
                    merged = prism;
                }
                else
                {
                    merged.BooleanOperation(
                        Autodesk.AutoCAD.DatabaseServices.BooleanOperationType.BoolUnite,
                        prism);
                }
            }

            if (merged == null)
            {
                throw new System.InvalidOperationException(
                    "継手断面のループが空です。JointShapes の実形状データを確認してください。");
            }
            return merged;
        }

        // 矩形断面の角柱(Z=0 〜 Z=+height_m、断面は XY 平面で中心が原点)。
        public static Autodesk.AutoCAD.DatabaseServices.Solid3d Box(
            double sizeX_m, double sizeY_m, double height_m)
        {
            double hx = sizeX_m / 2.0;
            double hy = sizeY_m / 2.0;
            return Prism(new double[] { -hx, -hy, hx, -hy, hx, hy, -hx, hy }, height_m);
        }

        private static Autodesk.AutoCAD.DatabaseServices.Region RegionFromCurve(
            Autodesk.AutoCAD.DatabaseServices.Entity curve)
        {
            Autodesk.AutoCAD.DatabaseServices.DBObjectCollection curves =
                new Autodesk.AutoCAD.DatabaseServices.DBObjectCollection();
            curves.Add(curve);
            return (Autodesk.AutoCAD.DatabaseServices.Region)
                Autodesk.AutoCAD.DatabaseServices.Region.CreateFromCurves(curves)[0];
        }
    }
}
