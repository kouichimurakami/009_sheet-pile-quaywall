// AutoCAD 非依存 — xUnit で単体テスト可能
// 計算式出典: 港湾土木請負工事積算基準 令和7年度改訂版
//             第3章 16節 仮設工 3-2「バイブロハンマ鋼管杭・鋼管矢板打設」(3-16-26〜31)
//
// 4節 本体工 4.5「鋼管矢板式」(DriveEstimate が担当) は打撃工法 — ディーゼルハンマ /
// 油圧ハンマ — の歩掛を定める。同節の注記「バイブロハンマによる場合は、現場条件により
// 『16節 仮設工』を適用することができる」に従い、振動工法の歩掛は本ファイル(16節 3-2)
// で扱う。両者は別節・別歩掛であり、混用してはならない。
//
// 適用範囲(3-2-1): **海上**で行うバイブロハンマによる鋼管杭・鋼管矢板の打設。
//   - 陸上打設は本項の対象外(16節 2-1 は鋼矢板・H形鋼杭であり鋼管矢板ではない)
//   - ウォータージェット併用は 16節 3-1 の別歩掛であり本ファイルの対象外
//
// 端数処理: 基準が「四捨五入」と定める箇所は MidpointRounding.AwayFromZero を使う。
//   .NET 既定の System.Math.Round は銀行丸め(偶数丸め)であり四捨五入と一致しない。

namespace SheetPileQuayWall.Core.FrontWall
{
    /// <summary>
    /// バイブロハンマの打込み対象。打込み速度 Lo と労務構成が異なる(3-16-30)。
    /// 009 では前壁 = 鋼管矢板、控え杭 = 鋼管杭に対応する。
    /// </summary>
    public enum VibroDriveTarget
    {
        /// <summary>鋼管杭(Lo = 0.90 m/分)。</summary>
        SteelPipePile,

        /// <summary>鋼管矢板(Lo = 0.75 m/分)。継手の貫入抵抗 Rj が加算される。</summary>
        SteelPipeSheetPile
    }

    public static class VibroEstimate
    {
        // ── バイブロハンマ規格選定テーブル(3-16-29 の規格決定図)────────────
        // (ラベル, 鋼材質量上限 [t], 貫入抵抗値上限 [kN])
        private static readonly (string label, double massLimit_t, double rLimit_kN)[] VibroTable =
        {
            (  "90kW",  2.0,  2_000.0 ),
            ( "120kW",  5.0,  6_000.0 ),
            ( "150kW",  9.0, 13_000.0 ),
            ( "200kW", 15.0, 20_000.0 ),
            ( "240kW", 20.0, 28_000.0 ),
        };

        // ── 作業船舶・機械の組合せ(3-16-29)──────────────────────────────
        // (バイブロ規格, 発動発電機, 起重機船・杭打船)
        private static readonly (string vibro, string generator, string craneVessel)[] EquipmentTable =
        {
            (  "90kW", "300kVA",  "80t吊" ),
            ( "120kW", "400kVA", "150t吊" ),
            ( "150kW", "500kVA", "150t吊" ),
            ( "200kW", "600kVA", "200t吊" ),
            ( "240kW", "800kVA", "200t吊" ),
        };

        // ── 台船・引船の規格(積載物の長さ = 杭の全長により決定、4節 3-4.6-6)──
        // 3-16-29 の注1「台船および引船の規格は、鋼管杭・鋼管矢板運搬の規格とする」
        // により本表を参照する。16節 3-1(ジェット併用)も同じ注記で本表を参照するため
        // (3-16-18 注2)、FrontWall.VibroJetEstimate 側もこのメソッドを直接呼び出す。
        private static readonly (double upperBound_m, string barge, string tug)[] BargeTugTable =
        {
            ( 28.0, "鋼300t積",   "鋼D450PS型" ),
            ( 31.0, "鋼400t積",   "鋼D450PS型" ),
            ( 34.0, "鋼500t積",   "鋼D500PS型" ),
            ( 39.0, "鋼700t積",   "鋼D550PS型" ),
            ( 44.0, "鋼1,000t積", "鋼D600PS型" ),
        };

        /// <summary>台船・引船の規格表がカバーする積載物長の上限 [m]。これ以上は
        /// 基準上「別途長さに見合った台船を選定する」(3-4.6-6)。</summary>
        public const double BargeTugMaxLength_m = 44.0;

        /// <summary>揚錨船の規格(バイブロ規格によらず一定、3-16-29)。</summary>
        public const string AnchorHandlingVesselSpec = "鋼D 5t吊";

        /// <summary>潜水士船の規格(必要な場合のみ計上。バイブロ規格によらず一定、3-16-29)。</summary>
        public const string DiverVesselSpec = "D270PS型 3〜5t吊";

        // ── 定数(3-16-30)────────────────────────────────────────────────

        /// <summary>1 m 当り準備時間 To [分/m]。</summary>
        public const double To_minPerM = 0.6;

        /// <summary>準備時間の基準打設長 [m](この長さで Tp = TpBase_min)。</summary>
        public const double TpBaseLength_m = 25.0;

        /// <summary>基準打設長における 1 本当り準備時間 [分]。</summary>
        public const double TpBase_min = 24.0;

        /// <summary>鋼管杭の打込み速度 Lo [m/分]。</summary>
        public const double Lo_Pile_mPerMin = 0.90;

        /// <summary>鋼管矢板の打込み速度 Lo [m/分]。</summary>
        public const double Lo_SheetPile_mPerMin = 0.75;

        /// <summary>継手の貫入抵抗値の比 Rj = R1 × 10^-1(鋼管矢板のみ)。</summary>
        public const double JointResistanceRatio = 0.1;

        /// <summary>基準作業能力係数 ei(海上打設)。</summary>
        public const double Ei_Offshore = 0.70;

        /// <summary>杭打船の 1 日当り運転時間 T [h/日]。</summary>
        public const double T_hPerDay = 6.0;

        /// <summary>施工規模区分の境界本数(これ未満で E3 = −0.05)。</summary>
        public const int ScaleBoundary_piles = 50;

        /// <summary>労務構成・継手溶接機械が切り替わる打設長の境界 [m]。</summary>
        public const double LaborBoundary_m = 25.0;

        /// <summary>継手溶接機械が 2 台になる外径の境界 [mm]。</summary>
        public const int WeldMachineBoundary_mm = 800;

        // ─────────────────────────────────────────────────────────────────
        // 貫入抵抗値
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// 鋼管杭の貫入抵抗値 R1 [kN](閉塞率 100%、小数 1 位四捨五入)。
        /// R1 = 300 × N × Ap + 2 × N̄ × Lb × As   (出典: 3-16-29)
        /// 打撃工法(3-4.5-14)の R と同一形だが、節が異なるため独立に定義する。
        /// </summary>
        /// <param name="D_m">外径 [m]。</param>
        /// <param name="L_drive_m">打設長 Lb [m](表層から連続する N=0 の区間は含めない)。</param>
        /// <param name="N_tip">先端地盤の N 値。</param>
        /// <param name="N_avg">周辺地盤の加重平均 N 値。</param>
        public static double CalcR1(double D_m, double L_drive_m, int N_tip, double N_avg)
        {
            double Ap = System.Math.PI / 4.0 * D_m * D_m; // 先端面積 [m²]
            double As = System.Math.PI * D_m;              // 周長 [m]
            return RoundHalfUp(300.0 * N_tip * Ap + 2.0 * N_avg * L_drive_m * As, 1);
        }

        /// <summary>
        /// 継手の貫入抵抗値 Rj [kN]。鋼管矢板のみ Rj = R1 × 10^-1、鋼管杭は 0。
        /// (出典: 3-16-29。打撃工法 3-4.5 には継手項が無く、振動工法固有の加算である)
        /// </summary>
        public static double CalcRj(double r1_kN, VibroDriveTarget target)
        {
            return target == VibroDriveTarget.SteelPipeSheetPile
                ? RoundHalfUp(r1_kN * JointResistanceRatio, 1)
                : 0.0;
        }

        /// <summary>
        /// 打込み対象に応じた貫入抵抗値 [kN]。鋼管矢板は R = R1 + Rj(3-16-29)。
        /// </summary>
        public static double CalcR(
            double D_m, double L_drive_m, int N_tip, double N_avg, VibroDriveTarget target)
        {
            double r1 = CalcR1(D_m, L_drive_m, N_tip, N_avg);
            return RoundHalfUp(r1 + CalcRj(r1, target), 1);
        }

        // ─────────────────────────────────────────────────────────────────
        // 規格選定
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// バイブロハンマ規格の選定(3-16-29)。鋼材質量と貫入抵抗値の**両方**が
        /// 収まる最小規格を返す。最大規格 240kW を超える場合は別途検討を促す。
        /// </summary>
        public static string GetVibroClass(double steelMass_t, double R_kN)
        {
            foreach (var (label, massLimit, rLimit) in VibroTable)
            {
                if (steelMass_t <= massLimit && R_kN <= rLimit)
                {
                    return label;
                }
            }
            return "240kW 超(別途検討)";
        }

        /// <summary>
        /// バイブロ規格に対応する発動発電機・起重機船の規格(3-16-29)。
        /// 表に無い規格(240kW 超)では空文字を返す。
        /// </summary>
        public static (string generator, string craneVessel) GetEquipment(string vibroClass)
        {
            foreach (var (vibro, generator, craneVessel) in EquipmentTable)
            {
                if (vibro == vibroClass)
                {
                    return (generator, craneVessel);
                }
            }
            return ("", "");
        }

        /// <summary>
        /// 台船・引船の規格(積載物の長さ = 杭の全長から選定、4節 3-4.6-6)。
        /// 44m 以上は基準に規定が無いため空文字を返す(別途選定が必要)。
        /// </summary>
        public static (string barge, string tug) GetBargeAndTug(double pileLength_m)
        {
            foreach (var (upperBound, barge, tug) in BargeTugTable)
            {
                if (pileLength_m < upperBound)
                {
                    return (barge, tug);
                }
            }
            return ("", "");
        }

        /// <summary>
        /// 継手溶接機械の組合せ(3-16-29)。継杭が無い場合は 0 台・空文字。
        /// φ800mm 未満: 溶接機 1 台 + 発動発電機 100kVA / φ800mm 以上: 2 台 + 125kVA。
        /// </summary>
        public static (int machineCount, string generator) GetWeldEquipment(int D_mm, bool splicing)
        {
            if (!splicing)
            {
                return (0, "");
            }
            return D_mm >= WeldMachineBoundary_mm ? (2, "125kVA") : (1, "100kVA");
        }

        // ─────────────────────────────────────────────────────────────────
        // 施工歩掛
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// 1 本当り準備時間 Tp [分](小数 2 位四捨五入)。
        /// Tp = 24 + To × (Lb − 25)   (出典: 3-16-30)
        /// </summary>
        public static double CalcTp(double L_drive_m)
        {
            return RoundHalfUp(TpBase_min + To_minPerM * (L_drive_m - TpBaseLength_m), 2);
        }

        /// <summary>
        /// 1 本当り打込み時間 Tb [分](小数 2 位四捨五入)。
        /// Tb = Lb / Lo   (出典: 3-16-30)
        /// 注) 玉石混じり層を含む場合の打込み速度は基準上「別途考慮」であり本式の対象外。
        /// </summary>
        public static double CalcTb(double L_drive_m, VibroDriveTarget target)
        {
            double Lo = target == VibroDriveTarget.SteelPipePile
                ? Lo_Pile_mPerMin : Lo_SheetPile_mPerMin;
            return RoundHalfUp(L_drive_m / Lo, 2);
        }

        /// <summary>
        /// 1 日当り打設本数 Q [本/日](小数 2 位四捨五入)。
        /// Q = T × 60 / Tc × (ei + E1 + E2 + E3)   (出典: 3-16-30)
        /// 海上打設のみのため ei = 0.70、T = 6 h/日 で固定される。
        /// </summary>
        public static double CalcQ(
            double Tc_min, SeaCondition sea, ObstacleStatus obstacle, int totalPiles)
        {
            double E1 = sea == SeaCondition.Severe ? -0.05 : 0.0;
            double E2 = obstacle == ObstacleStatus.Exists ? -0.05 : 0.0;
            double E3 = totalPiles < ScaleBoundary_piles ? -0.05 : 0.0;
            return RoundHalfUp(
                T_hPerDay * 60.0 / Tc_min * (Ei_Offshore + E1 + E2 + E3), 2);
        }

        /// <summary>
        /// 労務編成 [人/日](3-16-31 の代価表)。打設長 25 m 以下 / 超で とび工 が変わる。
        /// 溶接工は継杭がある場合のみ計上し、人数は継手溶接機械の台数に合わせる。
        /// </summary>
        public static (int foreman, int rigger, int laborer, int specialist, int welder) GetLabor(
            VibroDriveTarget target, double L_drive_m, bool splicing, int D_mm)
        {
            bool longPile = L_drive_m > LaborBoundary_m;

            int rigger = target == VibroDriveTarget.SteelPipePile
                ? (longPile ? 4 : 2)
                : (longPile ? 5 : 3);

            int welder = GetWeldEquipment(D_mm, splicing).machineCount;

            return (foreman: 1, rigger: rigger, laborer: 3, specialist: 1, welder: welder);
        }

        // 基準の「四捨五入」。.NET 既定の Math.Round は偶数丸めのため明示指定する。
        private static double RoundHalfUp(double value, int digits)
        {
            return System.Math.Round(value, digits, System.MidpointRounding.AwayFromZero);
        }
    }
}
