// 継手形式と有効幅 B の定義
// 出典: JFE d1j-503 継手諸元、日本製鉄 K011
// B = 矢板ピッチ (パイル芯間距離) [m]

namespace SheetPileQuayWall.Core.FrontWall
{
    public enum JointType { LT65, LT75, LT100, PP, PT }

    public static class JointParameters
    {
        // 有効幅 B [m] = D + 継手有効間隔 J (K011 準拠、JointGeometry に委譲) [確定]
        // LT100 のみカタログ式が存在しないため D + 0.100 の概算値 [推定]
        public static double EffectiveWidth(double D_m, JointType jointType)
        {
            if (jointType == JointType.LT100) return D_m + 0.100;
            return JointGeometry.EffectiveWidth(D_m, jointType);
        }

        public static string ToCode(JointType jointType) => jointType switch
        {
            JointType.LT65  => "LT65",
            JointType.LT75  => "LT75",
            JointType.LT100 => "LT100",
            JointType.PP    => "PP",
            JointType.PT    => "PT",
            _ => throw new System.ArgumentOutOfRangeException(nameof(jointType))
        };

        public static JointType FromCode(string code) => code switch
        {
            "LT65"  => JointType.LT65,
            "LT75"  => JointType.LT75,
            "LT100" => JointType.LT100,
            "PP"    => JointType.PP,
            "PT"    => JointType.PT,
            _ => throw new System.ArgumentException($"不明な継手コード: {code}", nameof(code))
        };
    }
}
