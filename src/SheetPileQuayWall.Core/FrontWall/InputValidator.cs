// パラメータ入力値の検証
// 範囲根拠: 日本製鉄 K011 製造範囲 (D 500〜2000mm、t 9〜25mm)
// 戻り値: null = 正常、非null = エラーメッセージ

namespace SheetPileQuayWall.Core.FrontWall
{
    public static class InputValidator
    {
        public const double D_Min_m = 0.500;
        public const double D_Max_m = 2.000;
        public const double T_Min_m = 0.009;
        public const double T_Max_m = 0.025;
        public const double L_Min_m = 1.0;
        public const double L_Max_m = 80.0;

        public static string? ValidateD(double D_m)
        {
            if (D_m < D_Min_m || D_m > D_Max_m)
                return $"外径 D={D_m * 1000:F1}mm は範囲外 " +
                       $"({D_Min_m * 1000:F0}〜{D_Max_m * 1000:F0}mm)。";
            return null;
        }

        public static string? ValidateT(double t_m, double D_m)
        {
            if (t_m < T_Min_m || t_m > T_Max_m)
                return $"肉厚 t={t_m * 1000:F1}mm は範囲外 " +
                       $"({T_Min_m * 1000:F0}〜{T_Max_m * 1000:F0}mm)。";
            double innerD_m = D_m - 2.0 * t_m;
            if (innerD_m <= 0.001)
                return $"内径 d={innerD_m * 1000:F1}mm ≤ 1mm — D と t の組合せが不正です。";
            return null;
        }

        public static string? ValidateL(double L_m)
        {
            if (L_m < L_Min_m || L_m > L_Max_m)
                return $"全長 L={L_m:F1}m は範囲外 ({L_Min_m:F0}〜{L_Max_m:F0}m)。";
            return null;
        }
    }
}
