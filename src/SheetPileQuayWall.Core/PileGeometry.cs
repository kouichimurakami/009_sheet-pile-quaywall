// 傾斜杭の共通幾何 (前壁鋼管矢板・控え杭で共用)。単位: 長さ m、角度 deg。
// 移植元: 006@6d6d8cf src/SteelPipePile.cs BuildPileSolid の変換部分。
//
// ローカル座標系: Z=0 が杭先端、Z=+L が杭頭 (移植元と同一)。
// 配置変換は「Y 軸周りの回転 θ (原点まわり) → 杭先端への平行移動」の順。
// Plugin 層の Matrix3d.Rotation(θ, YAxis, Origin) → Matrix3d.Displacement(tip) が
// これと一致していなければならない。本クラスがその参照実装である。
//
// θ>0 は杭頭が陸側 (+X) へ倒れる向き (CLAUDE.PRIVATE.md §2.2 の X 軸定義による)。

namespace SheetPileQuayWall.Core
{
    public static class PileGeometry
    {
        // ローカル座標 → 世界座標。Y 軸周りに θ 回転してから杭先端へ平行移動する。
        public static Point3 LocalToWorld(Point3 local, double inclDeg, Point3 tip)
        {
            double rad = inclDeg * System.Math.PI / 180.0;
            double c = System.Math.Cos(rad);
            double s = System.Math.Sin(rad);
            return new Point3(
                tip.X + local.X * c + local.Z * s,
                tip.Y + local.Y,
                tip.Z - local.X * s + local.Z * c);
        }

        // 杭頭標高 [m]。傾斜杭では鉛直投影が全長 L より短くなる。
        public static double HeadElevation(double tipElevM, double lengthM, double inclDeg)
        {
            return tipElevM + lengthM * System.Math.Cos(inclDeg * System.Math.PI / 180.0);
        }

        // 杭頭点(局所座標 Z=L)から杭先端点(局所座標 Z=0)を求める。LocalToWorld の逆演算
        // (local=(0,0,L) の像が head であることから X・Z とも厳密に整合する)。
        public static Point3 TipFromHead(Point3 head, double lengthM, double inclDeg)
        {
            double rad = inclDeg * System.Math.PI / 180.0;
            return new Point3(
                head.X - lengthM * System.Math.Sin(rad),
                head.Y,
                head.Z - lengthM * System.Math.Cos(rad));
        }

        // 標高 elevM における杭軸の X 座標 [m]。LocalToWorld と厳密に整合する
        // (ローカル (0,0,s) の像は X=tip.X+s·sinθ、Z=tip.Z+s·cosθ であり、
        //  (Z−tip.Z)·tanθ = s·sinθ となるため)。
        public static double AxisXAt(Point3 tip, double inclDeg, double elevM)
        {
            return tip.X + (elevM - tip.Z) * System.Math.Tan(inclDeg * System.Math.PI / 180.0);
        }
    }
}
