// AutoCAD 非依存 — xUnit で単体テスト可能
// 計算式出典: 港湾土木請負工事積算基準 令和7年度改訂版
//             第3章 16節 仮設工 3-1「仮設鋼管杭・鋼管矢板」(3-16-11〜25)
//
// ウォータージェット併用バイブロハンマ工法。009 の打設積算 3 系統のうちの 1 つ:
//   DriveEstimate     … 打撃工法(4節 3-4.5)。ディーゼル/油圧ハンマ
//   VibroEstimate     … 振動工法・バイブロ単独(16節 3-2)。海上のみ
//   VibroJetEstimate  … 振動工法・ジェット併用(16節 3-1)。陸上/海上とも ← 本ファイル
//
// 適用の目安(3-1-3 の適用工法表): バイブロ単独では施工できない場合に適用する。
// 支持層へ打込む/中間層を打抜く場合、バイブロ単独は標準適用外でありジェット併用が
// 必要となる。ジェット併用は外径 1,500mm 以下・全長 40m 以下に適用する。
//
// 選定の基礎が 3-2 とは根本的に異なる。3-2 は「鋼材質量 + 貫入抵抗値」で規格を選ぶが、
// 本項は「必要偏心モーメント K0」で選ぶ。両者を取り違えてはならない。
//
// 端数処理: Tp・Tb は「小数1位切上げ」、Q は「小数2位四捨五入」と基準が定める。
//           四捨五入には MidpointRounding.AwayFromZero を用いる(.NET 既定は銀行丸め)。
//
// 【未実装】噴射ノズル数およびウォータージェットの使用台数の表(3-16-16)は、
//   原本テキストのセル結合により OCR が復元不能であった。推測で埋めることはせず、
//   ジェット使用台数は呼び出し側からの入力として受け取る。台数が決まれば以降の
//   発動発電機・水中ポンプ・水槽は本ファイルで自動決定される。

namespace SheetPileQuayWall.Core.FrontWall
{
    /// <summary>基本振幅係数 A0 の土質区分(3-16-15)。</summary>
    public enum JetSoilType
    {
        /// <summary>砂質土・レキ質土・粘性土。</summary>
        SandyGravelClay,

        /// <summary>玉石混りレキ(最大径 75mm 超の玉石が混入するレキ層)。</summary>
        CobbleGravel,

        /// <summary>固結土。</summary>
        Cemented,

        /// <summary>岩盤。</summary>
        Rock
    }

    /// <summary>
    /// 地層の土質区分。A0 表(3-16-15)と γ 表(3-16-20)は土質のくくり方が異なり、
    /// とくに**粘性土**は A0 では「砂質土･レキ質土･粘性土」に、γ では「粘性土・固結土」
    /// に属する。両表を取り違えないよう、層の土質はこの区分で受けて各表へ振り分ける。
    /// </summary>
    public enum JetLayerType
    {
        /// <summary>砂・砂質土・レキ質土。A0: 砂質土等 / γ: γ1。</summary>
        SandGravel,

        /// <summary>粘性土。A0: 砂質土等 / γ: γ3。</summary>
        Clay,

        /// <summary>玉石混りレキ。A0: 玉石混りレキ / γ: γ2。</summary>
        CobbleGravel,

        /// <summary>固結土。A0: 固結土 / γ: γ3。</summary>
        Cemented,

        /// <summary>岩盤。A0: 岩盤 / γ: γ4。</summary>
        Rock
    }

    public static class VibroJetEstimate
    {
        // ── 必要偏心モーメントとバイブロハンマ規格(3-16-15)────────────────
        // (K0 上限 [N·m], バイブロ規格, 発動発電機規格)
        private static readonly (double k0Limit, string vibro, string generator)[] VibroTable =
        {
            (   200.0,  "45kW", "150kVA" ),
            (   340.0,  "60kW", "200kVA" ),
            (   440.0,  "90kW", "300kVA" ),
            (   740.0, "120kW", "400kVA" ),
            ( 1_800.0, "150kW", "500kVA" ),
            ( 2_500.0, "200kW", "600kVA" ),
            ( 2_900.0, "240kW", "800kVA" ),
        };

        // ── 対象外径 [mm] と板厚 [mm] の列(β 表、3-16-20)──────────────────
        private static readonly int[] Diameters_mm =
            { 500, 600, 700, 800, 900, 1000, 1100, 1200, 1300, 1400, 1500 };

        private static readonly int[] BetaThicknesses_mm = { 9, 12, 14, 16, 19, 22 };

        // 鋼管の外径と板厚による係数 β(3-16-20)
        private static readonly double[,] BetaTable =
        {
            //  t9    t12   t14   t16   t19   t22
            { 1.05, 1.10, 1.15, 1.20, 1.25, 1.35 }, // φ  500
            { 1.00, 1.05, 1.10, 1.15, 1.20, 1.30 }, // φ  600
            { 0.95, 1.00, 1.05, 1.10, 1.20, 1.25 }, // φ  700
            { 0.95, 1.00, 1.05, 1.10, 1.15, 1.25 }, // φ  800
            { 0.90, 0.95, 1.00, 1.05, 1.10, 1.20 }, // φ  900
            { 0.90, 0.90, 0.95, 1.00, 1.05, 1.20 }, // φ 1000
            { 0.85, 0.90, 0.95, 1.00, 1.05, 1.15 }, // φ 1100
            { 0.85, 0.85, 0.90, 0.95, 1.00, 1.10 }, // φ 1200
            { 0.80, 0.85, 0.90, 0.95, 1.00, 1.05 }, // φ 1300
            { 0.75, 0.80, 0.85, 0.90, 0.95, 1.00 }, // φ 1400
            { 0.75, 0.80, 0.85, 0.90, 0.95, 1.00 }, // φ 1500
        };

        // バイブロハンマ規格の列(δ 表、3-16-21)
        private static readonly string[] DeltaVibroClasses =
            { "45kW", "60kW", "90kW", "120kW", "150kW", "200kW", "240kW" };

        // バイブロハンマ規格と鋼管外径による係数 δ(3-16-21)
        // 0.0 は原本の「−」= 当該組合せは適用対象外を表す。
        private static readonly double[,] DeltaTable =
        {
            // 45kW  60kW  90kW  120kW 150kW 200kW 240kW
            { 0.95, 0.90, 0.80, 0.00, 0.00, 0.00, 0.00 }, // φ  500
            { 1.00, 0.95, 0.90, 0.85, 0.00, 0.00, 0.00 }, // φ  600
            { 1.05, 1.00, 0.90, 0.85, 0.80, 0.00, 0.00 }, // φ  700
            { 1.10, 1.00, 0.95, 0.90, 0.85, 0.80, 0.00 }, // φ  800
            { 0.00, 1.10, 1.00, 0.95, 0.90, 0.85, 0.80 }, // φ  900
            { 0.00, 1.20, 1.10, 1.00, 0.95, 0.90, 0.85 }, // φ 1000
            { 0.00, 1.30, 1.15, 1.05, 0.95, 0.90, 0.85 }, // φ 1100
            { 0.00, 0.00, 1.20, 1.10, 1.00, 0.95, 0.90 }, // φ 1200
            { 0.00, 0.00, 1.30, 1.15, 1.05, 1.00, 0.95 }, // φ 1300
            { 0.00, 0.00, 1.40, 1.25, 1.10, 1.05, 0.95 }, // φ 1400
            { 0.00, 0.00, 1.50, 1.35, 1.20, 1.10, 1.00 }, // φ 1500
        };

        // ── 定数 ──────────────────────────────────────────────────────────

        /// <summary>必要偏心モーメント算定式の係数(K0 = A0 × Wp × 98)。</summary>
        public const double K0Coefficient = 98.0;

        /// <summary>鋼管チャックを装備しない場合に A0 を除する値(3-16-15 注3)。</summary>
        public const double NoChuckDivisor = 1.3;

        /// <summary>鋼管矢板の加算時間の係数(ε = 0.3 × 継手長)。</summary>
        public const double EpsilonCoefficient = 0.3;

        /// <summary>基準作業能力係数 ei(陸上打設・直杭)。</summary>
        public const double Ei_Onshore = 0.80;

        /// <summary>基準作業能力係数 ei(海上打設・直杭)。</summary>
        public const double Ei_Offshore = 0.70;

        /// <summary>クレーン最大吊上げ荷重の算定係数(Cf = (Wv + Wp) × 6)。</summary>
        public const double CraneCapacityFactor = 6.0;

        /// <summary>ジェット併用を適用できる外径の上限 [m](3-1-3 注3)。</summary>
        public const double JetMaxDiameter_m = 1.500;

        /// <summary>ジェット併用を適用できる杭全長の上限 [m](3-1-3 注3)。</summary>
        public const double JetMaxPileLength_m = 40.0;

        /// <summary>施工規模区分の境界本数(これ未満で E3 = −0.05)。</summary>
        public const int ScaleBoundary_piles = 50;

        // ─────────────────────────────────────────────────────────────────
        // 1) バイブロハンマの規格選定(3-16-15)
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// 基本振幅係数 A0 を N 値から求める(3-16-15)。
        /// 該当区分が原本で「−」の場合は null を返す(適用対象外)。
        /// </summary>
        public static double? GetAmplitudeFactor(JetSoilType soil, double nValue)
        {
            int column = nValue <= 5.0 ? 0 : nValue <= 30.0 ? 1 : nValue <= 50.0 ? 2 : 3;

            double?[] row = soil switch
            {
                JetSoilType.SandyGravelClay => new double?[] { 0.40, 0.65, 1.10, 1.40 },
                JetSoilType.CobbleGravel    => new double?[] { null, 0.65, 0.90, 1.55 },
                JetSoilType.Cemented        => new double?[] { null, null, 1.00, 1.70 },
                _                           => new double?[] { null, null, null, 1.55 },
            };
            return row[column];
        }

        /// <summary>
        /// 基本振幅係数 A0 を一軸圧縮強度 qu [N/mm²] から求める(3-16-15)。
        /// 固結土・岩盤のみに定義があり、それ以外は null を返す。
        /// </summary>
        public static double? GetAmplitudeFactorByQu(JetSoilType soil, double qu)
        {
            if (soil == JetSoilType.Cemented)
            {
                return qu <= 4.9 ? 1.70 : (double?)null;
            }
            if (soil == JetSoilType.Rock)
            {
                return qu <= 4.9 ? 1.30 : 1.95;
            }
            return null;
        }

        /// <summary>
        /// 鋼管チャックを装備しない場合の A0 補正(3-16-15 注3。表の係数を 1.3 で除す)。
        /// </summary>
        public static double AdjustForNoChuck(double a0)
        {
            return a0 / NoChuckDivisor;
        }

        /// <summary>
        /// 必要偏心モーメント K0 [N·m](3-16-15)。K0 = A0 × Wp × 98。
        /// </summary>
        /// <param name="a0">基本振幅係数。</param>
        /// <param name="pileMass_t">杭 1 本当り質量 Wp [t]。</param>
        public static double CalcK0(double a0, double pileMass_t)
        {
            return a0 * pileMass_t * K0Coefficient;
        }

        /// <summary>
        /// 必要偏心モーメントに適合するバイブロハンマ規格と発動発電機規格(3-16-15)。
        /// 表の範囲(K0 ≦ 2,900)を超える場合は規格に「別途検討」を返し発電機は空文字。
        /// </summary>
        public static (string vibro, string generator) GetVibroClass(double k0)
        {
            foreach (var (k0Limit, vibro, generator) in VibroTable)
            {
                if (k0 <= k0Limit)
                {
                    return (vibro, generator);
                }
            }
            return ("240kW 超(別途検討)", "");
        }

        // ─────────────────────────────────────────────────────────────────
        // 2) ウォータージェットの付帯設備(3-16-16〜17)
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// ジェット付属の水中ポンプに使用する発動発電機の規格(3-16-17)。
        /// 使用台数 1〜4 台に対し 10 / 20 / 35 / 45 kVA。範囲外は空文字。
        /// </summary>
        public static string GetJetGenerator(int jetCount)
        {
            return jetCount switch
            {
                1 => "10kVA",
                2 => "20kVA",
                3 => "35kVA",
                4 => "45kVA",
                _ => "",
            };
        }

        /// <summary>
        /// 水源が遠い場合に計上する水中ポンプ・水槽・発動発電機の規格と数量(3-16-17)。
        /// 使用台数が範囲外のときは全て空 / 0 を返す。
        /// 注) 設置位置の直下に水深 1m 以上の水源がある場合は計上しない(同注2)。
        /// </summary>
        public static (string pump, double pumpOutput_kW, int pumpCount,
                       string generator, int tankVolume_m3, int tankCount)
            GetWaterSupply(int jetCount)
        {
            return jetCount switch
            {
                1 => ("φ150", 10.6, 1, "35kVA", 20, 1),
                2 => ("φ200", 15.5, 1, "45kVA", 30, 1),
                3 => ("φ150", 10.6, 2, "60kVA", 20, 2),
                4 => ("φ200", 15.5, 2, "75kVA", 30, 2),
                _ => ("", 0.0, 0, "", 0, 0),
            };
        }

        // ─────────────────────────────────────────────────────────────────
        // 3) 1 本当り打込み時間の係数(3-16-19〜21)
        // ─────────────────────────────────────────────────────────────────

        /// <summary>砂・砂質土・レキ質土への 1m 当り打込み時間 γ1 = 0.02·N + 0.5 [分/m]。</summary>
        public static double CalcGamma1(double nAvg)
        {
            return 0.02 * nAvg + 0.5;
        }

        /// <summary>玉石混りレキ層への 1m 当り打込み時間 γ2 = 0.02·N + 0.5 + η [分/m]。</summary>
        public static double CalcGamma2(double nAvg, double eta)
        {
            return 0.02 * nAvg + 0.5 + eta;
        }

        /// <summary>粘性土・固結土への 1m 当り打込み時間 γ3 = 0.04·N + 0.6 [分/m]。</summary>
        public static double CalcGamma3(double nAvg)
        {
            return 0.04 * nAvg + 0.6;
        }

        /// <summary>岩盤層への 1m 当り打込み時間 γ4 = 0.82·qu + 3 [分/m]。</summary>
        public static double CalcGamma4(double qu)
        {
            return 0.82 * qu + 3.0;
        }

        /// <summary>
        /// 玉石混りレキ層の補正係数 η(3-16-20 注1)。最大玉石径 [mm] による。
        /// 75mm 以下は玉石混りレキに該当せず 0、200mm 超は基準上「別途定める」ため null。
        /// </summary>
        public static double? GetEta(double maxCobble_mm)
        {
            if (maxCobble_mm <= 75.0) { return 0.0; }
            if (maxCobble_mm <= 100.0) { return 2.0; }
            if (maxCobble_mm <= 150.0) { return 2.5; }
            if (maxCobble_mm <= 200.0) { return 3.0; }
            return null;
        }

        /// <summary>
        /// 層の土質区分を、基本振幅係数 A0 表(3-16-15)の土質区分へ対応付ける。
        /// 粘性土は A0 表では「砂質土･レキ質土･粘性土」に属する点に注意。
        /// </summary>
        public static JetSoilType ToAmplitudeSoil(JetLayerType layer)
        {
            return layer switch
            {
                JetLayerType.SandGravel   => JetSoilType.SandyGravelClay,
                JetLayerType.Clay         => JetSoilType.SandyGravelClay,
                JetLayerType.CobbleGravel => JetSoilType.CobbleGravel,
                JetLayerType.Cemented     => JetSoilType.Cemented,
                _                         => JetSoilType.Rock,
            };
        }

        /// <summary>
        /// 層の土質区分に対応する 1m 当り打込み時間 γ [分/m](3-16-20)。
        /// 粘性土は γ3(粘性土・固結土)を用いる点に注意。
        /// </summary>
        /// <param name="nAvg">当該土質の根入長に対する加重平均 N 値(岩盤では未使用)。</param>
        /// <param name="qu">岩盤層の加重平均一軸圧縮強度 [N/mm²](岩盤のみ使用)。</param>
        /// <param name="eta">玉石混りレキ層の補正係数 η(玉石混りレキのみ使用)。</param>
        public static double CalcGamma(JetLayerType layer, double nAvg, double qu, double eta)
        {
            return layer switch
            {
                JetLayerType.SandGravel   => CalcGamma1(nAvg),
                JetLayerType.CobbleGravel => CalcGamma2(nAvg, eta),
                JetLayerType.Clay         => CalcGamma3(nAvg),
                JetLayerType.Cemented     => CalcGamma3(nAvg),
                _                         => CalcGamma4(qu),
            };
        }

        /// <summary>
        /// 土質別の γ を打込み長で加重平均する(3-16-19)。
        /// γ = Σ(γi × ℓi) / Σℓi。層が無い(合計長 0)場合は 0 を返す。
        /// </summary>
        public static double WeightedGamma(
            System.Collections.Generic.IReadOnlyList<(double gamma, double length_m)> layers)
        {
            double numerator = 0.0;
            double totalLength = 0.0;
            for (int i = 0; i < layers.Count; i++)
            {
                numerator += layers[i].gamma * layers[i].length_m;
                totalLength += layers[i].length_m;
            }
            return totalLength > 0.0 ? numerator / totalLength : 0.0;
        }

        /// <summary>
        /// 鋼管の外径と板厚による係数 β(3-16-20)。表の外径・板厚を超える場合は null
        /// (基準上「別途考慮」)。板厚は表の値以下で最も近い列に丸める。
        /// </summary>
        public static double? GetBeta(int D_mm, int t_mm)
        {
            int dIdx = IndexOfDiameter(D_mm);
            if (dIdx < 0) { return null; }

            int tIdx = -1;
            for (int i = 0; i < BetaThicknesses_mm.Length; i++)
            {
                if (t_mm <= BetaThicknesses_mm[i]) { tIdx = i; break; }
            }
            if (tIdx < 0) { return null; }

            return BetaTable[dIdx, tIdx];
        }

        /// <summary>
        /// バイブロハンマ規格と鋼管外径による係数 δ(3-16-21)。
        /// 原本で「−」の組合せ(当該規格では打設対象外)および表外は null を返す。
        /// </summary>
        public static double? GetDelta(int D_mm, string vibroClass)
        {
            int dIdx = IndexOfDiameter(D_mm);
            if (dIdx < 0) { return null; }

            int vIdx = System.Array.IndexOf(DeltaVibroClasses, vibroClass);
            if (vIdx < 0) { return null; }

            double value = DeltaTable[dIdx, vIdx];
            return value > 0.0 ? value : (double?)null;
        }

        /// <summary>
        /// 鋼管矢板の場合の加算時間 ε = 0.3 × ℓj [分/本](3-16-21)。
        /// 継手合わせと継手抵抗の分であり、鋼管杭では 0 とする。
        /// </summary>
        public static double CalcEpsilon(double jointLength_m)
        {
            return EpsilonCoefficient * jointLength_m;
        }

        // ─────────────────────────────────────────────────────────────────
        // 4) 1 本当り打設時間・作業能力(3-16-19)
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// 1 本当り準備時間 Tp = (0.3·L0 + 11) × ns + 5 [分/本](小数 1 位切上げ)。
        /// </summary>
        /// <param name="liftLength_m">吊込 1 回ごとの杭長 L0 [m]。</param>
        /// <param name="liftCount">杭の吊込み回数 ns [回]。</param>
        public static double CalcTp(double liftLength_m, int liftCount)
        {
            return CeilingTo1((0.3 * liftLength_m + 11.0) * liftCount + 5.0);
        }

        /// <summary>
        /// 1 本当り打込み時間 Tb = γ·β·δ·ℓ + ε [分/本](小数 1 位切上げ)。
        /// </summary>
        public static double CalcTb(
            double gamma, double beta, double delta, double driveLength_m, double epsilon)
        {
            return CeilingTo1(gamma * beta * delta * driveLength_m + epsilon);
        }

        /// <summary>
        /// 1 日当り打設本数 Q = T×60/Tc × (ei + E1 + E2 + E3) [本/日](小数 2 位四捨五入)。
        /// 陸上打設は E1 = 0.00 で固定される(海象条件が無いため)。
        /// </summary>
        /// <param name="operatingHours">1 日当り運転時間 T [h/日]。作業船は 6、陸上機械は
        /// クローラクレーンの標準運転時間による。</param>
        public static double CalcQ(
            ConstructionSite site, double operatingHours, double Tc_min,
            SeaCondition sea, ObstacleStatus obstacle, int totalPiles)
        {
            double ei = site == ConstructionSite.Onshore ? Ei_Onshore : Ei_Offshore;
            double E1 = (site == ConstructionSite.Offshore && sea == SeaCondition.Severe)
                ? -0.05 : 0.0;
            double E2 = obstacle == ObstacleStatus.Exists ? -0.05 : 0.0;
            double E3 = totalPiles < ScaleBoundary_piles ? -0.05 : 0.0;

            return RoundHalfUp(operatingHours * 60.0 / Tc_min * (ei + E1 + E2 + E3), 2);
        }

        /// <summary>
        /// 労務編成 [人/日](3-16-21)。陸上は杭長 20m、海上は 25m で区分が変わる。
        /// 溶接工は継杭施工の場合のみ計上し、φ800mm 以上は 2 人。
        /// </summary>
        public static (int foreman, int rigger, int laborer, int specialist, int welder) GetLabor(
            ConstructionSite site, double pileLength_m, bool splicing, int D_mm)
        {
            bool longPile = site == ConstructionSite.Onshore
                ? pileLength_m >= 20.0
                : pileLength_m >= 25.0;

            int rigger;
            int laborer;
            if (site == ConstructionSite.Onshore)
            {
                rigger = 2;
                laborer = longPile ? 2 : 1;
            }
            else
            {
                rigger = longPile ? 4 : 3;
                laborer = 3;
            }

            int welder = !splicing ? 0 : (D_mm >= 800 ? 2 : 1);

            return (foreman: 1, rigger: rigger, laborer: laborer, specialist: 1, welder: welder);
        }

        /// <summary>
        /// クレーンの最大吊上げ荷重 Cf = (Wv + Wp) × 6 [t](3-16-18 注1)。
        /// 主クレーン・クレーン付台船・起重機船の規格選定の基礎となる。
        /// </summary>
        /// <param name="vibroMass_t">バイブロハンマの質量 Wv(鋼管チャック質量を含む) [t]。</param>
        /// <param name="pileMass_t">杭 1 本当り質量 Wp [t]。</param>
        public static double CalcCraneCapacity(double vibroMass_t, double pileMass_t)
        {
            return (vibroMass_t + pileMass_t) * CraneCapacityFactor;
        }

        /// <summary>
        /// ジェット併用の適用範囲(3-1-3 注3): 外径 1,500mm 以下・杭全長 40m 以下。
        /// 範囲外ならエラーメッセージ、適合なら null を返す。
        /// </summary>
        public static string? ValidateJetApplicability(double D_m, double pileLength_m)
        {
            if (D_m > JetMaxDiameter_m + 0.0005)
            {
                return $"外径 {D_m * 1000:F1}mm がジェット併用の適用上限 "
                    + $"{JetMaxDiameter_m * 1000:F0}mm を超えています(基準 3-1-3 注3)。別途考慮が必要です。";
            }
            if (pileLength_m > JetMaxPileLength_m + 0.001)
            {
                return $"杭全長 {pileLength_m:F1}m がジェット併用の適用上限 "
                    + $"{JetMaxPileLength_m:F0}m を超えています(基準 3-1-3 注3)。別途考慮が必要です。";
            }
            return null;
        }

        // ── 内部ヘルパー ──────────────────────────────────────────────────

        private static int IndexOfDiameter(int D_mm)
        {
            return System.Array.IndexOf(Diameters_mm, D_mm);
        }

        // 基準の「小数1位切上げ」。
        private static double CeilingTo1(double value)
        {
            return System.Math.Ceiling(value * 10.0) / 10.0;
        }

        // 基準の「四捨五入」。.NET 既定の Math.Round は偶数丸めのため明示指定する。
        private static double RoundHalfUp(double value, int digits)
        {
            return System.Math.Round(value, digits, System.MidpointRounding.AwayFromZero);
        }
    }
}
