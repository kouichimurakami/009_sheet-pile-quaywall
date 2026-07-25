// === 参照DLLバージョン検証済み ===
// 本ファイルは AutoCAD / Civil 3D / Dynamo のアセンブリを参照しない（純計算層）。
// 参照は .NET 8.0 BCL のみ。検証対象DLLなし。
// 検証日: 2026-07-25 / 検証コマンド: scripts/verify-dll-versions.ps1
//
// 出典:
//   日鉄神鋼建材「タイロッド 港湾・土木用」2023年10月版 (tairod_231002.pdf)
//   港湾土木請負工事積算基準 令和7年度改訂版

namespace SheetPileQuayWall.Core.TieRod
{
    /// <summary>タイロッドのカタログ規格値。長さは全てメートル、応力度は N/mm^2。</summary>
    public static class TieRodCatalog
    {
        /// <summary>鋼の単位体積質量 (kg/m^3)。</summary>
        public const double SteelDensity = 7850.0;

        /// <summary>寸法の誤差許容値 (m)。1 mm。</summary>
        public const double Tolerance = 0.001;

        /// <summary>普通鋼の許容応力度が切り替わる径 (m)。40 mm 超で低減される。</summary>
        private const double MildSteelStepDiameter = 0.040;

        /// <summary>カタログ規格径 (m)。φ25〜φ90 の 19 種。</summary>
        public static readonly System.Collections.Generic.IReadOnlyList<double> StandardDiameters =
            new double[]
            {
                0.025, 0.028, 0.032, 0.036, 0.038, 0.042, 0.044, 0.046, 0.048, 0.050,
                0.052, 0.055, 0.060, 0.065, 0.070, 0.075, 0.080, 0.085, 0.090
            };

        /// <summary>
        /// 定着ナット高さおよび調節長 (m)。積算基準 3-4.5-(13) の表。
        /// φ38〜φ65 のみ規定されており、範囲外は利用者が明示指定する必要がある。
        /// ナット高さと調節長は同値のため 1 表で保持する。
        /// </summary>
        private static readonly System.Collections.Generic.Dictionary<int, double> NutHeightTable =
            new System.Collections.Generic.Dictionary<int, double>
            {
                { 38, 0.040 },
                { 42, 0.045 },
                { 44, 0.050 },
                { 46, 0.050 },
                { 48, 0.055 },
                { 50, 0.055 },
                { 52, 0.055 },
                { 55, 0.060 },
                { 60, 0.070 },
                { 65, 0.075 }
            };

        /// <summary>
        /// 指定径がカタログ規格径に含まれるか判定する。
        /// 判定は mm 単位の呼び径への四捨五入一致（実質 ±0.5 mm）で行う。
        /// 浮動小数の差分比較（±1 mm）は「ちょうど 1 mm ずれ」の合否が 2 進表現の偶然に
        /// 依存するため採用しない。
        /// </summary>
        public static bool IsStandardDiameter(double diameter)
        {
            double unused;
            return TrySnapToStandard(diameter, out unused);
        }

        /// <summary>
        /// 指定径を一致するカタログ規格径の正確な値へスナップする。
        /// 一致する規格径が無い場合は false（snapped は 0）。
        /// 派生量（断面積・質量・許容張力・ソリッド半径）は必ずスナップ後の値で計算し、
        /// 入力誤差（例 0.0475 m）が結果へ伝播しないようにする。
        /// </summary>
        public static bool TrySnapToStandard(double diameter, out double snapped)
        {
            int nominal = ToNominalMillimeter(diameter);
            for (int i = 0; i < StandardDiameters.Count; i++)
            {
                if (ToNominalMillimeter(StandardDiameters[i]) == nominal)
                {
                    snapped = StandardDiameters[i];
                    return true;
                }
            }
            snapped = 0.0;
            return false;
        }

        /// <summary>径 (m) を mm 単位の整数呼び径に丸める。表引き用。</summary>
        public static int ToNominalMillimeter(double diameter)
        {
            return (int)System.Math.Round(diameter * 1000.0, System.MidpointRounding.AwayFromZero);
        }

        /// <summary>断面積 (m^2)。</summary>
        public static double SectionArea(double diameter)
        {
            return System.Math.PI * diameter * diameter / 4.0;
        }

        /// <summary>棒部の単位質量 (kg/m)。</summary>
        public static double UnitMass(double diameter)
        {
            return SectionArea(diameter) * SteelDensity;
        }

        /// <summary>
        /// 積算基準表による定着ナット高さ／調節長 (m) を返す。
        /// 表に無い径では false を返す（推測値を返さない）。
        /// </summary>
        public static bool TryGetNutHeight(double diameter, out double nutHeight)
        {
            return NutHeightTable.TryGetValue(ToNominalMillimeter(diameter), out nutHeight);
        }

        /// <summary>
        /// 許容応力度法（旧基準）の許容引張応力度 (N/mm^2)。
        /// 出典: カタログ p.2「許容引張応力度」。
        /// </summary>
        public static double AllowableStress(SteelGrade grade, LoadState state, double diameter)
        {
            bool isLarge = diameter > MildSteelStepDiameter + Tolerance;

            switch (grade)
            {
                case SteelGrade.HT690:
                    return state == LoadState.Normal ? 176.0 : 264.0;

                case SteelGrade.HT740:
                    return state == LoadState.Normal ? 216.0 : 324.0;

                case SteelGrade.SS400:
                    if (state == LoadState.Normal)
                    {
                        return isLarge ? 86.0 : 94.0;
                    }
                    return isLarge ? 129.0 : 141.0;

                case SteelGrade.SS490:
                    if (state == LoadState.Normal)
                    {
                        return isLarge ? 102.0 : 110.0;
                    }
                    return isLarge ? 153.0 : 165.0;

                default:
                    throw new System.ArgumentOutOfRangeException(nameof(grade));
            }
        }

        /// <summary>部分係数法（新基準）が対応する鋼種か判定する。HT740 / SS490 は基準に記載が無い。</summary>
        public static bool SupportsPartialFactor(SteelGrade grade)
        {
            return grade == SteelGrade.HT690 || grade == SteelGrade.SS400;
        }

        /// <summary>
        /// 引張降伏応力度 σy (N/mm^2)。部分係数法で用いる。
        /// 出典: カタログ p.5「機械的性質」。
        /// </summary>
        public static double YieldStress(SteelGrade grade, double diameter)
        {
            if (grade == SteelGrade.HT690)
            {
                return 440.0;
            }
            if (grade == SteelGrade.SS400)
            {
                return diameter > MildSteelStepDiameter + Tolerance ? 215.0 : 235.0;
            }
            throw new System.ArgumentOutOfRangeException(
                nameof(grade), "部分係数法に対応する鋼種は HT690 と SS400 のみです。");
        }

        /// <summary>
        /// 許容張力 (kN)。設計基準・荷重状態・鋼種・径から算出する。
        ///
        /// 許容応力度法: T = A × σa
        /// 部分係数法  : m × Sd / Rd ≦ 1.0 を T について解いて T = A × (γR × σy) / (m × γS)
        ///   永続状態 γR = 0.64, γS = 1.29, m = 1.00
        ///   変動状態 γR = 1.00, γS = 1.00, m = 1.67
        /// </summary>
        public static double AllowableTension(
            SteelGrade grade, DesignCode code, LoadState state, double diameter)
        {
            // 断面積を mm^2 に換算して応力度 (N/mm^2) と掛け合わせ、N を kN に直す。
            double areaMm2 = SectionArea(diameter) * 1.0e6;

            if (code == DesignCode.Allowable)
            {
                return areaMm2 * AllowableStress(grade, state, diameter) / 1000.0;
            }

            double yield = YieldStress(grade, diameter);
            double gammaR = state == LoadState.Normal ? 0.64 : 1.00;
            double gammaS = state == LoadState.Normal ? 1.29 : 1.00;
            double m = state == LoadState.Normal ? 1.00 : 1.67;

            return areaMm2 * (gammaR * yield) / (m * gammaS) / 1000.0;
        }
    }
}
