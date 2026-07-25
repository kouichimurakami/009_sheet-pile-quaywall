// AutoCAD 非依存の純粋計算クラス — xUnit で単体テスト可能
// 計算式出典: 日本製鉄 K011、JFE d1j-503 (JIS A 5530 準拠)
// 入力単位: m (メートル)  出力単位: cm 系 (カタログ慣例)

namespace SheetPileQuayWall.Core.FrontWall
{
    public static class SectionProperties
    {
        // 断面積 A [cm²]
        public static double CalcA(double D_m, double t_m)
        {
            double D = D_m * 100.0;
            double d = (D_m - 2.0 * t_m) * 100.0;
            return System.Math.PI / 4.0 * (D * D - d * d);
        }

        // 断面2次モーメント I [cm⁴]
        public static double CalcI(double D_m, double t_m)
        {
            double D = D_m * 100.0;
            double d = (D_m - 2.0 * t_m) * 100.0;
            return System.Math.PI / 64.0 *
                (System.Math.Pow(D, 4) - System.Math.Pow(d, 4));
        }

        // 断面係数 Z [cm³]
        public static double CalcZ(double D_m, double t_m)
        {
            double D_cm = D_m * 100.0;
            return CalcI(D_m, t_m) / (D_cm / 2.0);
        }

        // 単位重量 W [kg/m]  — K011 近似式: W = 2.466 × t_cm × (D_cm − t_cm)
        public static double CalcW(double D_m, double t_m)
        {
            double D_cm = D_m * 100.0;
            double t_cm = t_m * 100.0;
            return 2.466 * t_cm * (D_cm - t_cm);
        }

        // 断面2次半径 i [cm]  — i = 0.25 × √(D² + d²)
        public static double CalcRadius(double D_m, double t_m)
        {
            double D_cm = D_m * 100.0;
            double d_cm = (D_m - 2.0 * t_m) * 100.0;
            return 0.25 * System.Math.Sqrt(D_cm * D_cm + d_cm * d_cm);
        }
    }
}
