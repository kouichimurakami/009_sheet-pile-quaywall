// AutoCAD 非依存 — xUnit で単体テスト可能
// 計算式出典: 港湾土木請負工事積算基準 令和7年度改訂版 第3章 3-4.5節

namespace SheetPileQuayWall.Core.FrontWall
{
    public enum ConstructionSite { Onshore, Offshore }  // 陸上/海上
    public enum SeaCondition    { Normal,   Severe    } // 普通/悪い (海上のみ)
    public enum ObstacleStatus  { None,     Exists    } // 障害なし/あり

    public static class DriveEstimate
    {
        // 1日当り荷卸し本数 (出典: 3-4.5-7)
        public const int UnloadPerDay = 60;

        // ── 打撃速度 Sb テーブル [m/min] ─────────────────────────────────
        // 行: φ400/500/.../1500 mm, index = (φ_mm − 400) / 100 (0〜11)
        // 列: 加重平均N値 ≤10 / ≤20 / ≤30 / ≤40 / ≤50, index 0〜4
        // 出典: 3-4.5-16
        private static readonly double[,] SbTable =
        {
            //        N≤10   N≤20  N≤30  N≤40  N≤50
            { 2.61, 1.53, 0.97, 0.74, 0.52 }, // φ  400
            { 2.34, 1.39, 0.88, 0.66, 0.48 }, // φ  500
            { 2.17, 1.27, 0.81, 0.62, 0.46 }, // φ  600
            { 2.00, 1.18, 0.76, 0.58, 0.43 }, // φ  700
            { 1.85, 1.09, 0.70, 0.53, 0.41 }, // φ  800
            { 1.72, 1.02, 0.66, 0.50, 0.39 }, // φ  900
            { 1.61, 0.95, 0.62, 0.48, 0.38 }, // φ 1000
            { 1.52, 0.90, 0.58, 0.45, 0.36 }, // φ 1100
            { 1.43, 0.85, 0.55, 0.42, 0.34 }, // φ 1200
            { 1.36, 0.80, 0.52, 0.41, 0.33 }, // φ 1300
            { 1.29, 0.76, 0.49, 0.38, 0.32 }, // φ 1400
            { 1.23, 0.73, 0.47, 0.37, 0.31 }, // φ 1500
        };

        // ── 溶接時間 [min/継手] テーブル ─────────────────────────────────
        // 行: φ400/500/.../1500 mm
        // 列: 板厚 t = 8/9/10/12/14/16/19/22 mm, index 0〜7
        // φ800mm以上は溶接機2台使用時の値
        // 出典: 3-4.5-17
        private static readonly int[,] WeldTable =
        {
            //     t8   t9  t10  t12  t14  t16  t19  t22
            {  13,  16,  18,  27,  36,  45,  61,  82 }, // φ  400
            {  18,  20,  22,  33,  43,  53,  72,  96 }, // φ  500
            {  22,  24,  27,  38,  50,  61,  82, 110 }, // φ  600
            {  27,  29,  31,  44,  57,  69,  93, 124 }, // φ  700
            {  20,  22,  24,  33,  43,  52,  68,  89 }, // φ  800
            {  23,  25,  27,  37,  47,  57,  74,  97 }, // φ  900
            {  26,  29,  31,  41,  52,  62,  81, 105 }, // φ 1000
            {  30,  32,  34,  45,  56,  67,  87, 114 }, // φ 1100
            {  33,  35,  37,  49,  61,  72,  93, 122 }, // φ 1200
            {  36,  38,  41,  53,  65,  77, 100, 130 }, // φ 1300
            {  40,  42,  44,  57,  70,  83, 106, 138 }, // φ 1400
            {  43,  45,  47,  61,  74,  88, 113, 146 }, // φ 1500
        };

        private static readonly int[] WeldThicknesses = { 8, 9, 10, 12, 14, 16, 19, 22 };

        // ── ハンマ規格選定テーブル ────────────────────────────────────────
        // (ラベル, 鋼材質量上限 [t], 貫入抵抗値上限 [kN])
        // 出典: 3-4.5-14
        private static readonly (string label, double massLimit_t, double rLimit_kN)[] HammerTable =
        {
            ( "4～4.5 t",    4.56,  5_700.0 ),
            ( "6.5 t",       8.71, 10_900.0 ),
            ( "7～8 t",     10.60, 13_100.0 ),
            ( "10～12.5 t", 20.40, 25_600.0 ),
            ( "15.0 t",     28.20, 35_100.0 ),
        };

        // ─────────────────────────────────────────────────────────────────
        // 公開 API
        // ─────────────────────────────────────────────────────────────────

        // 打撃速度 Sb [m/min]  φ>1500mm の場合は φ1500 の値で代用
        public static double GetSb(int D_mm, int N_avg)
        {
            int dIdx = System.Math.Clamp((D_mm - 400) / 100, 0, 11);
            int nIdx = N_avg <= 10 ? 0 :
                       N_avg <= 20 ? 1 :
                       N_avg <= 30 ? 2 :
                       N_avg <= 40 ? 3 : 4;
            return SbTable[dIdx, nIdx];
        }

        // 継手1か所当り溶接時間 [min]  t>22mm の場合は t=22 の値で代用
        public static int GetWeldTime(int D_mm, int t_mm)
        {
            int dIdx = System.Math.Clamp((D_mm - 400) / 100, 0, 11);
            int tIdx = WeldThicknesses.Length - 1;
            for (int i = 0; i < WeldThicknesses.Length; i++)
            {
                if (t_mm <= WeldThicknesses[i]) { tIdx = i; break; }
            }
            return WeldTable[dIdx, tIdx];
        }

        // 貫入抵抗値 R [kN]  (閉塞率100%)
        // R = 300 × N_tip × Ap + 2 × N_avg × L_pen × As  (出典: 3-4.5-14)
        public static double CalcR(double D_m, double L_pen_m, int N_tip, double N_avg)
        {
            double Ap = System.Math.PI / 4.0 * D_m * D_m; // 先端面積 [m²]
            double As = System.Math.PI * D_m;              // 周長 [m]
            return System.Math.Round(300.0 * N_tip * Ap + 2.0 * N_avg * L_pen_m * As, 1);
        }

        // ハンマ規格選定
        public static string GetHammerClass(double steelMass_t, double R_kN)
        {
            foreach (var (label, massLimit, rLimit) in HammerTable)
            {
                if (steelMass_t <= massLimit && R_kN <= rLimit)
                    return label;
            }
            return "15.0 t 超（別途検討）";
        }

        // 1本当り準備時間 Tp [min]  n: 継杭吊込み回数 (単杭=0)
        // 出典: 3-4.5-16
        public static double CalcTp(ConstructionSite site, int n)
        {
            return site == ConstructionSite.Onshore ? 5.0 * n + 14.0 : 5.0 * n + 16.0;
        }

        // 1本当り打撃時間 Tb [min]  (小数1位切上げ)
        public static double CalcTb(double L_pen_m, double Sb)
        {
            return System.Math.Ceiling(L_pen_m / Sb * 10.0) / 10.0;
        }

        // 1本当り溶接時間 Tw [min]  n_joints: 継手個所数 (単杭=0)
        public static double CalcTw(int D_mm, int t_mm, int n_joints)
        {
            if (n_joints == 0) return 0.0;
            return GetWeldTime(D_mm, t_mm) * n_joints;
        }

        // 1日当り打設本数 Q [本/日]  (小数2位四捨五入)
        // Q = T×60/Tc × (ei + E1 + E2 + E3)  (出典: 3-4.5-15)
        public static double CalcQ(ConstructionSite site, double Tc_min,
            SeaCondition sea, ObstacleStatus obstacle, int totalPiles)
        {
            double T  = site == ConstructionSite.Onshore ? 8.0 : 6.0; // 運転時間 [h/日]
            double ei = site == ConstructionSite.Onshore ? 0.90 : 0.50;
            double E1 = (site == ConstructionSite.Offshore && sea == SeaCondition.Severe)
                        ? -0.05 : 0.0;
            double E2 = obstacle == ObstacleStatus.Exists ? -0.05 : 0.0;
            double E3 = totalPiles < 50 ? -0.05 : 0.0;
            return System.Math.Round(T * 60.0 / Tc_min * (ei + E1 + E2 + E3), 2);
        }

        // 労務編成 (世話役, とび工, 普通作業員, 溶接工)  出典: 3-4.5-17
        public static (int foreman, int rigger, int laborer, int welder) GetLabor(
            ConstructionSite site, double L_m, bool splicing, int D_mm)
        {
            int foreman = 1;
            int rigger  = site == ConstructionSite.Onshore ? 2 :
                          L_m < 20.0 ? 3 : L_m < 25.0 ? 4 : 5;
            int laborer = site == ConstructionSite.Onshore ? 1 : 2;
            int welder  = !splicing ? 0 : (D_mm >= 800 ? 2 : 1);
            return (foreman, rigger, laborer, welder);
        }
    }
}
