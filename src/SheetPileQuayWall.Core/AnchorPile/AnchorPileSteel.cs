// 控え杭 (単独鋼管杭) の鋼材諸元
// 移植元: 006@6d6d8cf src/SteelPipePile.cs の SnapToJis / ThicknessRange。
// 出典: JIS A 5525 標準外径、日本製鉄カタログ K011 製造範囲。
//
// 単位はメートル (CLAUDE.PRIVATE.md §2.1)。移植元は mm 呼称で保持していたため、
// 全定数を 1/1000 して移した。
//
// 注意: 前壁鋼管矢板の範囲 (FrontWall.InputValidator: D 0.500〜2.000 m、
// t 0.009〜0.025 m 一律) とは異なる。控え杭は継手を持たない単独杭であり、
// JIS A 5525 の全径 (0.3185〜2.500 m) と径別の肉厚範囲を扱う。
// 両者を統一すべきかは未決 (docs/implementation-plan.md §12 参照)。

namespace SheetPileQuayWall.Core.AnchorPile
{
    public static class AnchorPileSteel
    {
        // JIS A 5525 鋼管杭 標準外径 [m]
        public static readonly double[] StandardDiameters_m =
        {
            0.3185, 0.3556, 0.400, 0.500, 0.600, 0.700, 0.800, 0.900, 1.000,
            1.100, 1.200, 1.300, 1.400, 1.500, 1.600, 1.700, 1.800,
            1.900, 2.000, 2.100, 2.200, 2.300, 2.400, 2.500
        };

        public const double D_Min_m = 0.3185;
        public const double D_Max_m = 2.500;
        public const double L_Min_m = 1.0;
        public const double L_Max_m = 80.0;

        // 入力値に最も近い JIS 標準径へ丸める。前壁と異なり控え杭は自動スナップする
        // (移植元の挙動。docs/implementation-plan.md §9 の「前壁外径のスナップのみ例外」に対応)。
        public static double SnapToJis(double D_m)
        {
            double nearest = StandardDiameters_m[0];
            double minDiff = System.Math.Abs(D_m - nearest);
            foreach (double jd in StandardDiameters_m)
            {
                double diff = System.Math.Abs(D_m - jd);
                if (diff < minDiff)
                {
                    minDiff = diff;
                    nearest = jd;
                }
            }
            return nearest;
        }

        // 外径別の肉厚製造範囲 [m] (K011)
        public static (double min_m, double max_m) ThicknessRange(double D_m)
        {
            if (D_m <= 0.400) return (0.009, 0.016);
            if (D_m <= 0.600) return (0.009, 0.022);
            if (D_m <= 1.000) return (0.009, 0.025);
            if (D_m <= 1.500) return (0.011, 0.025);
            if (D_m <= 2.000) return (0.014, 0.025);
            return (0.018, 0.025);
        }

        // 戻り値: null = 正常、非null = エラーメッセージ (InputValidator と同じ規約)
        public static string? ValidateD(double D_m)
        {
            if (D_m < D_Min_m || D_m > D_Max_m)
                return $"控え杭 外径 D={D_m * 1000:F1}mm は範囲外 " +
                       $"(JIS A 5525: {D_Min_m * 1000:F1}〜{D_Max_m * 1000:F0}mm)。";
            return null;
        }

        public static string? ValidateT(double t_m, double D_m)
        {
            (double tMin, double tMax) = ThicknessRange(D_m);
            if (t_m < tMin - 0.000001 || t_m > tMax + 0.000001)
                return $"控え杭 肉厚 t={t_m * 1000:F1}mm は D={D_m * 1000:F1}mm の " +
                       $"K011 製造範囲 {tMin * 1000:F0}〜{tMax * 1000:F0}mm 外です。";
            double innerD_m = D_m - 2.0 * t_m;
            if (innerD_m <= 0.001)
                return $"控え杭 内径 d={innerD_m * 1000:F1}mm ≤ 1mm — D と t の組合せが不正です。";
            return null;
        }

        public static string? ValidateL(double L_m)
        {
            if (L_m < L_Min_m || L_m > L_Max_m)
                return $"控え杭 全長 L={L_m:F1}m は範囲外 ({L_Min_m:F0}〜{L_Max_m:F0}m)。";
            return null;
        }
    }
}
