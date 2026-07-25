// 派生量の算出テスト。
// 期待値は 港湾土木請負工事積算基準 令和7年度改訂版 3-4.5-(13) の算定例および表による。

namespace SheetPileQuayWall.Core.Tests
{
    public class CalculatorTests
    {
        private const double LengthTolerance = 0.001;   // m（1 mm）

        /// <summary>積算基準の算定例（φ48・法線直角方向延長 10.0 m）に合わせた既定パラメータ。</summary>
        private static SheetPileQuayWall.Core.TieRod.TieRodParameters Sample()
        {
            return new SheetPileQuayWall.Core.TieRod.TieRodParameters();
        }

        [Xunit.Fact]
        public void 全長が積算基準の算定式と一致する()
        {
            // 10.0 + (0.006 + 0.025 + 0.055 + 0.055) × 2 + 0.300 = 10.582
            SheetPileQuayWall.Core.TieRod.TieRodResult r = SheetPileQuayWall.Core.TieRod.TieRodCalculator.Compute(Sample());
            Xunit.Assert.Equal(10.582, r.TotalLength, LengthTolerance);
        }

        [Xunit.Fact]
        public void 端部座標が全長と整合する()
        {
            SheetPileQuayWall.Core.TieRod.TieRodResult r = SheetPileQuayWall.Core.TieRod.TieRodCalculator.Compute(Sample());
            Xunit.Assert.Equal(-0.441, r.SeaEndX, LengthTolerance);
            Xunit.Assert.Equal(10.141, r.LandEndX, LengthTolerance);
            Xunit.Assert.Equal(r.TotalLength, r.LandEndX - r.SeaEndX, LengthTolerance);
        }

        [Xunit.Fact]
        public void 海側鋼管矢板の中心を横断する()
        {
            // X = 0 が海側鋼管矢板の中心軸。タイロッドはこれを跨ぐ。
            SheetPileQuayWall.Core.TieRod.TieRodResult r = SheetPileQuayWall.Core.TieRod.TieRodCalculator.Compute(Sample());
            Xunit.Assert.True(r.SeaEndX < 0.0, "海側端が矢板中心より海側にない");
            Xunit.Assert.True(r.LandEndX > 0.0, "陸側端が矢板中心より陸側にない");
        }

        [Xunit.Fact]
        public void 軸心標高はDL基準の入力値をそのまま用いる()
        {
            SheetPileQuayWall.Core.TieRod.TieRodParameters p = Sample();
            p.Hwl = 1.800;
            p.TieElevation = SheetPileQuayWall.Core.TieRod.TieRodParameters.DefaultTieElevation(p.Hwl);

            SheetPileQuayWall.Core.TieRod.TieRodResult r = SheetPileQuayWall.Core.TieRod.TieRodCalculator.Compute(p);
            Xunit.Assert.Equal(2.300, r.AxisZ, LengthTolerance);
        }

        [Xunit.Theory]
        [Xunit.InlineData(0.000, 0.500)]
        [Xunit.InlineData(1.800, 2.300)]
        [Xunit.InlineData(2.000, 2.500)]
        public void 既定軸心標高はHWLプラス05メートル(double hwl, double expected)
        {
            Xunit.Assert.Equal(
                expected, SheetPileQuayWall.Core.TieRod.TieRodParameters.DefaultTieElevation(hwl), LengthTolerance);
        }

        [Xunit.Fact]
        public void 組数分のY座標が取付間隔で並ぶ()
        {
            SheetPileQuayWall.Core.TieRod.TieRodParameters p = Sample();
            p.TieCount = 3;

            SheetPileQuayWall.Core.TieRod.TieRodResult r = SheetPileQuayWall.Core.TieRod.TieRodCalculator.Compute(p);
            Xunit.Assert.Equal(3, r.RodPositionsY.Count);
            Xunit.Assert.Equal(0.000, r.RodPositionsY[0], LengthTolerance);
            Xunit.Assert.Equal(2.400, r.RodPositionsY[1], LengthTolerance);
            Xunit.Assert.Equal(4.800, r.RodPositionsY[2], LengthTolerance);
        }

        // --- 継手方法（積算基準の表）--------------------------------------------------

        [Xunit.Theory]
        [Xunit.InlineData(14.900, 0.048, 4, 1, 2)]   // 延長 15 m 未満
        [Xunit.InlineData(15.000, 0.048, 5, 2, 2)]   // 15 m 以上 20 m 未満
        [Xunit.InlineData(19.900, 0.048, 5, 2, 2)]
        [Xunit.InlineData(20.000, 0.048, 6, 2, 3)]   // 20 m 以上
        [Xunit.InlineData(10.000, 0.055, 6, 2, 3)]   // 径 φ55 以上は延長に依らず 6 本
        [Xunit.InlineData(10.000, 0.052, 4, 1, 2)]   // φ52 は径条件に該当しない
        public void 継手本数が積算基準の表と一致する(
            double span, double diameter, int segments, int turnbuckles, int ringJoints)
        {
            SheetPileQuayWall.Core.TieRod.TieRodParameters p = Sample();
            p.SpanLength = span;
            p.RodDiameter = diameter;
            p.ApplyStandardNutHeight();   // 表内の径では Validate が表値との一致を要求する

            SheetPileQuayWall.Core.TieRod.TieRodResult r = SheetPileQuayWall.Core.TieRod.TieRodCalculator.Compute(p);
            Xunit.Assert.Equal(segments, r.SegmentCount);
            Xunit.Assert.Equal(turnbuckles, r.TurnbuckleCount);
            Xunit.Assert.Equal(ringJoints, r.RingJointCount);
        }

        [Xunit.Fact]
        public void 本体本数は継手点数プラス1である()
        {
            // 4本継ぎ = リングジョイント2 + ターンバックル1 + 1、という関係が全ケースで成立する。
            double[] spans = new double[] { 10.0, 17.0, 25.0 };
            for (int i = 0; i < spans.Length; i++)
            {
                SheetPileQuayWall.Core.TieRod.TieRodParameters p = Sample();
                p.SpanLength = spans[i];

                SheetPileQuayWall.Core.TieRod.TieRodResult r = SheetPileQuayWall.Core.TieRod.TieRodCalculator.Compute(p);
                Xunit.Assert.Equal(r.SegmentCount, r.TurnbuckleCount + r.RingJointCount + 1);
            }
        }

        [Xunit.Theory]
        [Xunit.InlineData(10.000, false)]
        [Xunit.InlineData(17.000, true)]    // 5 本継ぎはカタログ標準図（2〜4 本継ぎ）の範囲外
        [Xunit.InlineData(25.000, true)]
        public void カタログ標準外の継手を検出する(double span, bool expected)
        {
            SheetPileQuayWall.Core.TieRod.TieRodParameters p = Sample();
            p.SpanLength = span;

            SheetPileQuayWall.Core.TieRod.TieRodResult r = SheetPileQuayWall.Core.TieRod.TieRodCalculator.Compute(p);
            Xunit.Assert.Equal(expected, r.BeyondCatalogStandard);
        }

        // --- 受杭 ---------------------------------------------------------------------

        [Xunit.Theory]
        [Xunit.InlineData(14.000, 1)]
        [Xunit.InlineData(14.998, 1)]   // 境界の 1 mm 超手前は下位区分
        [Xunit.InlineData(14.999, 2)]   // 誤差許容 1 mm により境界扱い → 上位区分(安全側)
        [Xunit.InlineData(15.000, 2)]   // 境界: 15 m ちょうどは「15〜20 m 未満」に属する
        [Xunit.InlineData(17.000, 2)]
        [Xunit.InlineData(19.998, 2)]
        [Xunit.InlineData(20.000, 3)]   // 境界: 20 m ちょうどは「20 m 以上」に属する
        [Xunit.InlineData(25.000, 3)]
        public void 受杭箇所数が積算基準と一致する(double span, int expected)
        {
            SheetPileQuayWall.Core.TieRod.TieRodParameters p = Sample();
            p.SpanLength = span;

            SheetPileQuayWall.Core.TieRod.TieRodResult r = SheetPileQuayWall.Core.TieRod.TieRodCalculator.Compute(p);
            Xunit.Assert.Equal(expected, r.SupportPileCount);
        }

        [Xunit.Theory]
        [Xunit.InlineData(1, 1)]
        [Xunit.InlineData(2, 1)]
        [Xunit.InlineData(3, 2)]
        [Xunit.InlineData(4, 2)]
        [Xunit.InlineData(5, 3)]
        public void 受杭対象組数は法線方向1本おきである(int tieCount, int expected)
        {
            // 積算基準 3-4.5-(14) ②法線方向「タイロッド１本おきに受杭を入れる」。
            SheetPileQuayWall.Core.TieRod.TieRodParameters p = Sample();
            p.TieCount = tieCount;

            SheetPileQuayWall.Core.TieRod.TieRodResult r = SheetPileQuayWall.Core.TieRod.TieRodCalculator.Compute(p);
            Xunit.Assert.Equal(expected, r.SupportedRodCount);
        }

        [Xunit.Fact]
        public void 受杭合計は1本あたり箇所数と対象組数の積である()
        {
            // 延長 17 m → 法線直角方向 2 ヶ所、5 組 → 対象 3 組、合計 6 ヶ所。
            SheetPileQuayWall.Core.TieRod.TieRodParameters p = Sample();
            p.SpanLength = 17.000;
            p.TieCount = 5;

            SheetPileQuayWall.Core.TieRod.TieRodResult r = SheetPileQuayWall.Core.TieRod.TieRodCalculator.Compute(p);
            Xunit.Assert.Equal(2, r.SupportPileCount);
            Xunit.Assert.Equal(3, r.SupportedRodCount);
            Xunit.Assert.Equal(6, r.TotalSupportPileCount);
        }

        // --- 質量・体積 ---------------------------------------------------------------

        [Xunit.Fact]
        public void 体積と質量が単位質量と整合する()
        {
            SheetPileQuayWall.Core.TieRod.TieRodResult r = SheetPileQuayWall.Core.TieRod.TieRodCalculator.Compute(Sample());

            // 体積 × 鋼の単位体積質量 = 棒部質量
            Xunit.Assert.Equal(
                r.Volume * SheetPileQuayWall.Core.TieRod.TieRodCatalog.SteelDensity, r.RodMass, 0.001);

            // カタログの単位質量 14.2 kg/m × 全長 10.582 m ≒ 150.3 kg
            Xunit.Assert.Equal(150.3, r.RodMass, 0.5);
        }

        [Xunit.Fact]
        public void 組数分の質量が合計される()
        {
            SheetPileQuayWall.Core.TieRod.TieRodParameters p = Sample();
            p.TieCount = 10;

            SheetPileQuayWall.Core.TieRod.TieRodResult r = SheetPileQuayWall.Core.TieRod.TieRodCalculator.Compute(p);
            Xunit.Assert.Equal(r.RodMass * 10.0, r.TotalRodMass, 0.001);
        }

        // --- 張力照査 -----------------------------------------------------------------

        [Xunit.Fact]
        public void 反力未入力のとき張力照査を行わない()
        {
            SheetPileQuayWall.Core.TieRod.TieRodResult r = SheetPileQuayWall.Core.TieRod.TieRodCalculator.Compute(Sample());
            Xunit.Assert.False(r.TensionChecked);
            Xunit.Assert.Equal(0.0, r.DesignTension, 0.001);
        }

        [Xunit.Fact]
        public void 作用張力は反力かける取付間隔である()
        {
            SheetPileQuayWall.Core.TieRod.TieRodParameters p = Sample();
            p.AnchorReaction = 30.0;   // kN/m

            SheetPileQuayWall.Core.TieRod.TieRodResult r = SheetPileQuayWall.Core.TieRod.TieRodCalculator.Compute(p);
            Xunit.Assert.True(r.TensionChecked);
            Xunit.Assert.Equal(72.0, r.DesignTension, 0.001);   // 30.0 × 2.4
            Xunit.Assert.True(r.TensionOk);
        }

        [Xunit.Fact]
        public void 荷重状態が許容張力に反映される()
        {
            // φ48 HT690 部分係数法: 永続 395 kN / 変動 476 kN (カタログ p.6)。
            SheetPileQuayWall.Core.TieRod.TieRodParameters p = Sample();
            p.State = SheetPileQuayWall.Core.TieRod.LoadState.Normal;
            double normal = SheetPileQuayWall.Core.TieRod.TieRodCalculator.Compute(p).AllowableTension;

            p.State = SheetPileQuayWall.Core.TieRod.LoadState.Seismic;
            double seismic = SheetPileQuayWall.Core.TieRod.TieRodCalculator.Compute(p).AllowableTension;

            Xunit.Assert.Equal(395.0, normal, 1.0);
            Xunit.Assert.Equal(476.0, seismic, 1.0);
        }

        [Xunit.Fact]
        public void 地震時の照査は地震時許容値と比較する()
        {
            // 作用 420 kN は永続 395 kN では NG、変動 476 kN では OK となる荷重。
            SheetPileQuayWall.Core.TieRod.TieRodParameters p = Sample();
            p.AnchorReaction = 175.0;   // 175 × 2.4 = 420 kN

            p.State = SheetPileQuayWall.Core.TieRod.LoadState.Normal;
            Xunit.Assert.False(SheetPileQuayWall.Core.TieRod.TieRodCalculator.Compute(p).TensionOk);

            p.State = SheetPileQuayWall.Core.TieRod.LoadState.Seismic;
            Xunit.Assert.True(SheetPileQuayWall.Core.TieRod.TieRodCalculator.Compute(p).TensionOk);
        }

        [Xunit.Fact]
        public void 入力誤差はカタログ規格径へスナップされる()
        {
            // 0.5 mm ずれた入力でも派生量は正確な規格径 φ48 で計算される。
            SheetPileQuayWall.Core.TieRod.TieRodParameters p = Sample();
            p.RodDiameter = 0.0475;

            SheetPileQuayWall.Core.TieRod.TieRodResult r = SheetPileQuayWall.Core.TieRod.TieRodCalculator.Compute(p);
            Xunit.Assert.Equal(0.048, r.NominalDiameter, 0.0001);
            Xunit.Assert.Equal(1810.0, r.SectionArea * 1.0e6, 1.0);
        }

        [Xunit.Fact]
        public void 許容張力を超える反力を不合格と判定する()
        {
            SheetPileQuayWall.Core.TieRod.TieRodParameters p = Sample();
            p.AnchorReaction = 200.0;   // 200 × 2.4 = 480 kN > 許容 395 kN

            SheetPileQuayWall.Core.TieRod.TieRodResult r = SheetPileQuayWall.Core.TieRod.TieRodCalculator.Compute(p);
            Xunit.Assert.True(r.TensionChecked);
            Xunit.Assert.False(r.TensionOk);
            Xunit.Assert.True(r.TensionRatio > 1.0);
        }
    }
}
