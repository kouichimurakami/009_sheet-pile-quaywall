// 3 次元点 (単位: m)。
// Core は AutoCAD に依存しないため、Autodesk.AutoCAD.Geometry.Point3d の代わりに用いる。
// Plugin 層は境界で Point3d ↔ Point3 を変換する。
//
// 座標系 (CLAUDE.PRIVATE.md §2.2):
//   X: 陸側 → +X、海側 → −X   Y: 施設延長方向   Z: 鉛直上向き (Z=0 が D.L.)

namespace SheetPileQuayWall.Core
{
    public readonly struct Point3
    {
        public Point3(double x_m, double y_m, double z_m)
        {
            X = x_m;
            Y = y_m;
            Z = z_m;
        }

        public double X { get; }
        public double Y { get; }
        public double Z { get; }
    }
}
