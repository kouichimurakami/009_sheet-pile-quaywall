// カタログ規格値の照合テスト。
// 期待値は 日鉄神鋼建材「タイロッド 港湾・土木用」2023年10月版 の表から採取した実測値。
// カタログ表は丸めた断面積を用い、かつ小数を切り捨て表示しているため、
// 張力の許容差は 1 kN、単位質量は 0.06 kg/m とする。

namespace SheetPileQuayWall.Core.Tests
{
    public class CatalogTests
    {
        private const double TensionTolerance = 1.0;    // kN
        private const double AreaTolerance = 1.0;       // mm^2
        private const double MassTolerance = 0.06;      // kg/m

        [Xunit.Fact]
        public void 規格径は19種である()
        {
            Xunit.Assert.Equal(19, SheetPileQuayWall.Core.TieRod.TieRodCatalog.StandardDiameters.Count);
        }

        [Xunit.Theory]
        [Xunit.InlineData(0.048, true)]
        [Xunit.InlineData(0.025, true)]
        [Xunit.InlineData(0.090, true)]
        [Xunit.InlineData(0.0475, true)]   // 呼び径へ四捨五入して 48 → 一致
        [Xunit.InlineData(0.047, false)]   // 呼び径 47 は規格に無い
        [Xunit.InlineData(0.0486, false)]  // 呼び径へ四捨五入して 49 → 規格に無い
        [Xunit.InlineData(48.000, false)]  // mm 値の混入
        [Xunit.InlineData(0.100, false)]   // 上限超過
        public void 規格径の判定(double diameter, bool expected)
        {
            Xunit.Assert.Equal(expected, SheetPileQuayWall.Core.TieRod.TieRodCatalog.IsStandardDiameter(diameter));
        }

        [Xunit.Fact]
        public void スナップは規格径の正確な値を返す()
        {
            double snapped;
            Xunit.Assert.True(SheetPileQuayWall.Core.TieRod.TieRodCatalog.TrySnapToStandard(0.0475, out snapped));
            Xunit.Assert.Equal(0.048, snapped);   // 誤差なしの完全一致

            Xunit.Assert.False(SheetPileQuayWall.Core.TieRod.TieRodCatalog.TrySnapToStandard(0.047, out snapped));
        }

        [Xunit.Theory]
        [Xunit.InlineData(0.025, 490.9)]
        [Xunit.InlineData(0.048, 1810.0)]
        [Xunit.InlineData(0.090, 6362.0)]
        public void 断面積がカタログと一致する(double diameter, double expectedMm2)
        {
            double actualMm2 = SheetPileQuayWall.Core.TieRod.TieRodCatalog.SectionArea(diameter) * 1.0e6;
            Xunit.Assert.Equal(expectedMm2, actualMm2, AreaTolerance);
        }

        [Xunit.Theory]
        [Xunit.InlineData(0.025, 3.85)]
        [Xunit.InlineData(0.042, 10.9)]
        [Xunit.InlineData(0.048, 14.2)]
        [Xunit.InlineData(0.065, 26.0)]
        [Xunit.InlineData(0.090, 49.9)]
        public void 単位質量がカタログと一致する(double diameter, double expectedKgPerM)
        {
            Xunit.Assert.Equal(
                expectedKgPerM, SheetPileQuayWall.Core.TieRod.TieRodCatalog.UnitMass(diameter), MassTolerance);
        }

        // --- 許容応力度法（旧基準）カタログ p.4「本体の許容張力表」---------------------

        [Xunit.Theory]
        [Xunit.InlineData(0.025, SheetPileQuayWall.Core.TieRod.SteelGrade.HT690, 86.0)]
        [Xunit.InlineData(0.025, SheetPileQuayWall.Core.TieRod.SteelGrade.HT740, 106.0)]
        [Xunit.InlineData(0.025, SheetPileQuayWall.Core.TieRod.SteelGrade.SS400, 46.0)]
        [Xunit.InlineData(0.025, SheetPileQuayWall.Core.TieRod.SteelGrade.SS490, 53.0)]
        [Xunit.InlineData(0.038, SheetPileQuayWall.Core.TieRod.SteelGrade.SS400, 106.0)]
        [Xunit.InlineData(0.042, SheetPileQuayWall.Core.TieRod.SteelGrade.SS400, 119.0)]
        [Xunit.InlineData(0.048, SheetPileQuayWall.Core.TieRod.SteelGrade.HT690, 318.0)]
        [Xunit.InlineData(0.048, SheetPileQuayWall.Core.TieRod.SteelGrade.HT740, 390.0)]
        [Xunit.InlineData(0.048, SheetPileQuayWall.Core.TieRod.SteelGrade.SS400, 155.0)]
        [Xunit.InlineData(0.048, SheetPileQuayWall.Core.TieRod.SteelGrade.SS490, 184.0)]
        [Xunit.InlineData(0.055, SheetPileQuayWall.Core.TieRod.SteelGrade.HT690, 418.0)]
        [Xunit.InlineData(0.070, SheetPileQuayWall.Core.TieRod.SteelGrade.HT740, 831.0)]
        [Xunit.InlineData(0.090, SheetPileQuayWall.Core.TieRod.SteelGrade.HT690, 1119.0)]
        [Xunit.InlineData(0.090, SheetPileQuayWall.Core.TieRod.SteelGrade.HT740, 1374.0)]
        [Xunit.InlineData(0.090, SheetPileQuayWall.Core.TieRod.SteelGrade.SS400, 547.0)]
        [Xunit.InlineData(0.090, SheetPileQuayWall.Core.TieRod.SteelGrade.SS490, 648.0)]
        public void 旧基準許容張力_常時(
            double diameter, SheetPileQuayWall.Core.TieRod.SteelGrade grade, double expectedKn)
        {
            double actual = SheetPileQuayWall.Core.TieRod.TieRodCatalog.AllowableTension(
                grade, SheetPileQuayWall.Core.TieRod.DesignCode.Allowable, SheetPileQuayWall.Core.TieRod.LoadState.Normal, diameter);
            Xunit.Assert.Equal(expectedKn, actual, TensionTolerance);
        }

        [Xunit.Theory]
        [Xunit.InlineData(0.025, SheetPileQuayWall.Core.TieRod.SteelGrade.HT690, 129.0)]
        [Xunit.InlineData(0.025, SheetPileQuayWall.Core.TieRod.SteelGrade.HT740, 159.0)]
        [Xunit.InlineData(0.025, SheetPileQuayWall.Core.TieRod.SteelGrade.SS400, 69.0)]
        [Xunit.InlineData(0.025, SheetPileQuayWall.Core.TieRod.SteelGrade.SS490, 80.0)]
        [Xunit.InlineData(0.048, SheetPileQuayWall.Core.TieRod.SteelGrade.HT690, 477.0)]
        [Xunit.InlineData(0.048, SheetPileQuayWall.Core.TieRod.SteelGrade.HT740, 586.0)]
        [Xunit.InlineData(0.048, SheetPileQuayWall.Core.TieRod.SteelGrade.SS400, 233.0)]
        [Xunit.InlineData(0.048, SheetPileQuayWall.Core.TieRod.SteelGrade.SS490, 276.0)]
        [Xunit.InlineData(0.090, SheetPileQuayWall.Core.TieRod.SteelGrade.HT690, 1679.0)]
        [Xunit.InlineData(0.090, SheetPileQuayWall.Core.TieRod.SteelGrade.HT740, 2061.0)]
        [Xunit.InlineData(0.090, SheetPileQuayWall.Core.TieRod.SteelGrade.SS400, 820.0)]
        [Xunit.InlineData(0.090, SheetPileQuayWall.Core.TieRod.SteelGrade.SS490, 973.0)]
        public void 旧基準許容張力_地震時(
            double diameter, SheetPileQuayWall.Core.TieRod.SteelGrade grade, double expectedKn)
        {
            double actual = SheetPileQuayWall.Core.TieRod.TieRodCatalog.AllowableTension(
                grade, SheetPileQuayWall.Core.TieRod.DesignCode.Allowable, SheetPileQuayWall.Core.TieRod.LoadState.Seismic, diameter);
            Xunit.Assert.Equal(expectedKn, actual, TensionTolerance);
        }

        // --- 部分係数法（新基準）カタログ p.6「タイロッドの張力 T」---------------------

        [Xunit.Theory]
        [Xunit.InlineData(0.025, SheetPileQuayWall.Core.TieRod.SteelGrade.HT690, 107.0)]
        [Xunit.InlineData(0.025, SheetPileQuayWall.Core.TieRod.SteelGrade.SS400, 57.0)]
        [Xunit.InlineData(0.048, SheetPileQuayWall.Core.TieRod.SteelGrade.HT690, 395.0)]
        [Xunit.InlineData(0.048, SheetPileQuayWall.Core.TieRod.SteelGrade.SS400, 193.0)]
        [Xunit.InlineData(0.090, SheetPileQuayWall.Core.TieRod.SteelGrade.HT690, 1388.0)]
        [Xunit.InlineData(0.090, SheetPileQuayWall.Core.TieRod.SteelGrade.SS400, 678.0)]
        public void 新基準許容張力_永続状態(
            double diameter, SheetPileQuayWall.Core.TieRod.SteelGrade grade, double expectedKn)
        {
            double actual = SheetPileQuayWall.Core.TieRod.TieRodCatalog.AllowableTension(
                grade, SheetPileQuayWall.Core.TieRod.DesignCode.PartialFactor, SheetPileQuayWall.Core.TieRod.LoadState.Normal, diameter);
            Xunit.Assert.Equal(expectedKn, actual, TensionTolerance);
        }

        [Xunit.Theory]
        [Xunit.InlineData(0.025, SheetPileQuayWall.Core.TieRod.SteelGrade.HT690, 129.0)]
        [Xunit.InlineData(0.025, SheetPileQuayWall.Core.TieRod.SteelGrade.SS400, 69.0)]
        [Xunit.InlineData(0.048, SheetPileQuayWall.Core.TieRod.SteelGrade.HT690, 476.0)]
        [Xunit.InlineData(0.048, SheetPileQuayWall.Core.TieRod.SteelGrade.SS400, 233.0)]
        [Xunit.InlineData(0.090, SheetPileQuayWall.Core.TieRod.SteelGrade.HT690, 1676.0)]
        [Xunit.InlineData(0.090, SheetPileQuayWall.Core.TieRod.SteelGrade.SS400, 819.0)]
        public void 新基準許容張力_変動状態(
            double diameter, SheetPileQuayWall.Core.TieRod.SteelGrade grade, double expectedKn)
        {
            double actual = SheetPileQuayWall.Core.TieRod.TieRodCatalog.AllowableTension(
                grade, SheetPileQuayWall.Core.TieRod.DesignCode.PartialFactor, SheetPileQuayWall.Core.TieRod.LoadState.Seismic, diameter);
            Xunit.Assert.Equal(expectedKn, actual, TensionTolerance);
        }

        [Xunit.Fact]
        public void 新基準はHT740とSS490を対象外とする()
        {
            Xunit.Assert.True(
                SheetPileQuayWall.Core.TieRod.TieRodCatalog.SupportsPartialFactor(SheetPileQuayWall.Core.TieRod.SteelGrade.HT690));
            Xunit.Assert.True(
                SheetPileQuayWall.Core.TieRod.TieRodCatalog.SupportsPartialFactor(SheetPileQuayWall.Core.TieRod.SteelGrade.SS400));
            Xunit.Assert.False(
                SheetPileQuayWall.Core.TieRod.TieRodCatalog.SupportsPartialFactor(SheetPileQuayWall.Core.TieRod.SteelGrade.HT740));
            Xunit.Assert.False(
                SheetPileQuayWall.Core.TieRod.TieRodCatalog.SupportsPartialFactor(SheetPileQuayWall.Core.TieRod.SteelGrade.SS490));
        }

        // --- 積算基準のナット高さ／調節長 ---------------------------------------------

        [Xunit.Theory]
        [Xunit.InlineData(0.038, 0.040)]
        [Xunit.InlineData(0.044, 0.050)]
        [Xunit.InlineData(0.048, 0.055)]
        [Xunit.InlineData(0.055, 0.060)]
        [Xunit.InlineData(0.065, 0.075)]
        public void ナット高さが積算基準表と一致する(double diameter, double expected)
        {
            double actual;
            Xunit.Assert.True(SheetPileQuayWall.Core.TieRod.TieRodCatalog.TryGetNutHeight(diameter, out actual));
            Xunit.Assert.Equal(expected, actual, SheetPileQuayWall.Core.TieRod.TieRodCatalog.Tolerance);
        }

        [Xunit.Theory]
        [Xunit.InlineData(0.025)]   // 表の下限より小さい
        [Xunit.InlineData(0.036)]
        [Xunit.InlineData(0.070)]   // 表の上限より大きい
        [Xunit.InlineData(0.090)]
        public void 積算基準表に無い径では推測値を返さない(double diameter)
        {
            double actual;
            Xunit.Assert.False(SheetPileQuayWall.Core.TieRod.TieRodCatalog.TryGetNutHeight(diameter, out actual));
        }
    }
}
