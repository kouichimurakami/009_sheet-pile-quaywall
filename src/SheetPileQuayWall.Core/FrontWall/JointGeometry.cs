// === 参照DLLバージョン検証: 不要 (AutoCAD 非依存の純粋計算) ===
// 継手有効間隔 J・有効幅 W の計算。出典: 日本製鉄カタログ K011。単位: m。
// - P-P型:  J = 0.2478 (D 非依存・一定)
// - P-T型:  J = 0.180  (D 非依存・一定)
// - L-T型(65): J = (D/2 + 0.076  + √((D/2)² − 0.080²)) − D
// - L-T型(75): J = (D/2 + 0.0855 + √((D/2)² − 0.090²)) − D
// - L-T型(100×75): カタログ式なし → NaN
//   (実用値は JointParameters.EffectiveWidth が D + 0.100 を推定値として返す)

namespace SheetPileQuayWall.Core.FrontWall
{
    public static class JointGeometry
    {
        // 継手有効間隔 J [m]。カタログ式が無い LT100、および平方根の中が負に
        // なる小径 (LT65: D<0.160 / LT75: D<0.180) では NaN。
        public static double JointSpacing(double D_m, JointType jointType)
        {
            double r = D_m / 2.0;
            return jointType switch
            {
                JointType.PP => 0.2478,
                JointType.PT => 0.180,
                JointType.LT65 =>
                    (r + 0.076 + System.Math.Sqrt(r * r - 0.080 * 0.080)) - D_m,
                JointType.LT75 =>
                    (r + 0.0855 + System.Math.Sqrt(r * r - 0.090 * 0.090)) - D_m,
                JointType.LT100 => double.NaN,
                _ => throw new System.ArgumentOutOfRangeException(nameof(jointType))
            };
        }

        // 有効幅 W [m] = D + J。J が NaN の場合は NaN。
        public static double EffectiveWidth(double D_m, JointType jointType)
        {
            double j = JointSpacing(D_m, jointType);
            return double.IsNaN(j) ? double.NaN : D_m + j;
        }
    }
}
