// === 参照DLLバージョン検証: 不要 (AutoCAD 非依存の純粋幾何) ===
// 継手ローカル形状の配置変換。
// ローカル系: 原点 = 鋼管外表面の溶接点、+X = 半径方向外向き、+Y = 周方向(CCW)。
// 変換 (管中心を原点とする配置座標系、phi = 配置角 [rad]):
//   wx = (R + lx)·cosφ − ly·sinφ
//   wy = (R + lx)·sinφ + ly·cosφ

namespace SheetPileQuayWall.Core.FrontWall
{
    public static class JointPlacement
    {
        public static double TransformX(
            double lx_m, double ly_m, double outerR_m, double phiRad)
        {
            return (outerR_m + lx_m) * System.Math.Cos(phiRad)
                 - ly_m * System.Math.Sin(phiRad);
        }

        public static double TransformY(
            double lx_m, double ly_m, double outerR_m, double phiRad)
        {
            return (outerR_m + lx_m) * System.Math.Sin(phiRad)
                 + ly_m * System.Math.Cos(phiRad);
        }

        // 閉ループ頂点列 [x0,y0,x1,y1,...] を一括変換した新配列を返す。
        public static double[] TransformLoop(
            double[] loopXY_m, double outerR_m, double phiRad)
        {
            double[] result = new double[loopXY_m.Length];
            for (int i = 0; i + 1 < loopXY_m.Length; i += 2)
            {
                result[i] = TransformX(loopXY_m[i], loopXY_m[i + 1], outerR_m, phiRad);
                result[i + 1] = TransformY(loopXY_m[i], loopXY_m[i + 1], outerR_m, phiRad);
            }
            return result;
        }
    }
}
