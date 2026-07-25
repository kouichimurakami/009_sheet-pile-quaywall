// 鋼管矢板 継手部材の諸元カタログ
// 出典: JFE d1j-503「JFEの鋼管矢板」表4 継手及び連結継手の寸法並びに単位質量の例
//       (JIS A 5530 / 鋼管杭・鋼矢板技術協会標準製作仕様 図6 継手形状)
// 単位: 長さ m / 質量 kg/m  (寸法のラベル文字列のみカタログ呼称 mm を保持)
// 注: 本ファイルは AutoCAD 非依存の純粋データ — xUnit で単体テスト可能

namespace SheetPileQuayWall.Core.FrontWall
{
    // 継手形状の大分類 (図6)
    public enum JointForm { LT, PP, PT }

    // 山形鋼 L  (継手寸法 A×C×t)  — 摘要 L: A×C×t
    public sealed class AngleSteel
    {
        public string Label { get; }
        public double A_m { get; }   // 脚長 A
        public double C_m { get; }   // 脚長 C
        public double T_m { get; }   // 板厚 t
        public double MassKgPerM { get; }
        public AngleSteel(string label, double a_m, double c_m, double t_m, double mass)
        { Label = label; A_m = a_m; C_m = c_m; T_m = t_m; MassKgPerM = mass; }
    }

    // T形鋼  (継手寸法 H×B×t1×t2)  — 摘要 T: B×t2(×H×t1) / P-T形は T: H×B×t1×t2
    public sealed class TeeSteel
    {
        public string Label { get; }
        public double H_m { get; }   // ウェブ高さ H
        public double B_m { get; }   // フランジ幅 B
        public double T1_m { get; }  // ウェブ厚 t1
        public double T2_m { get; }  // フランジ厚 t2
        public double MassKgPerM { get; }
        public TeeSteel(string label, double h_m, double b_m, double t1_m, double t2_m, double mass)
        { Label = label; H_m = h_m; B_m = b_m; T1_m = t1_m; T2_m = t2_m; MassKgPerM = mass; }
    }

    // 鋼管継手 P  (継手寸法 φD×t)  — 摘要 P: D×t
    public sealed class PipeJoint
    {
        public string Label { get; }
        public double OD_m { get; }  // 外径 D
        public double T_m { get; }   // 板厚 t
        public double MassKgPerM { get; }
        public PipeJoint(string label, double od_m, double t_m, double mass)
        { Label = label; OD_m = od_m; T_m = t_m; MassKgPerM = mass; }
    }

    public static class JointCatalog
    {
        // JointType (継手コード) → 継手形状
        public static JointForm Form(JointType jt) => jt switch
        {
            JointType.LT65 or JointType.LT75 or JointType.LT100 => JointForm.LT,
            JointType.PP => JointForm.PP,
            JointType.PT => JointForm.PT,
            _ => throw new System.ArgumentOutOfRangeException(nameof(jt))
        };

        // L-T形の山形鋼 (表4)。L-T形以外は null
        public static AngleSteel? Angle(JointType jt) => jt switch
        {
            JointType.LT65  => new AngleSteel("L-65×65×8",  0.065, 0.065, 0.008, 15.3),
            JointType.LT75  => new AngleSteel("L-75×75×9",  0.075, 0.075, 0.009, 19.9),
            JointType.LT100 => new AngleSteel("L-100×75×9", 0.100, 0.075, 0.009, 26.0),
            _ => null
        };

        // T形鋼 (表4)。L-T形は T-125×9(×39×12)、P-T形は T-76×85×9×9。P-P形は null
        public static TeeSteel? Tee(JointType jt) => jt switch
        {
            JointType.LT65 or JointType.LT75 or JointType.LT100
                => new TeeSteel("T-125×9(×39×12)", 0.039, 0.125, 0.012, 0.009, 12.7),
            JointType.PT
                => new TeeSteel("T-76×85×9×9", 0.076, 0.085, 0.009, 0.009, 10.9),
            _ => null
        };

        // 鋼管継手 (表4)。P-P形・P-T形は φ165.2×9。L-T形は null
        public static PipeJoint? Pipe(JointType jt) => jt switch
        {
            JointType.PP or JointType.PT => new PipeJoint("φ165.2×9", 0.1652, 0.009, 34.7),
            _ => null
        };

        // 継手単位質量の合計 [kg/m] (1組 = オス側＋メス側部材)
        public static double JointMassPerM(JointType jt)
        {
            double m = 0.0;
            AngleSteel? a = Angle(jt); if (a != null) m += a.MassKgPerM;
            TeeSteel?   t = Tee(jt);   if (t != null) m += t.MassKgPerM;
            PipeJoint?  p = Pipe(jt);  if (p != null) m += p.MassKgPerM;
            return m;
        }
    }
}
