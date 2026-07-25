// T041〜T048: JointGeometry (継手有効間隔 J・有効幅 W) の単体テスト
// 検証基準: 日本製鉄カタログ K011
//   φ1000 例値: PP=247.8 / PT=180.0 / LT65=69.6 / LT75=77.3 mm

namespace SheetPileQuayWall.Core.Tests
{
    public class JointGeometryTests
    {
        // T041: PP の J = 0.2478 m (D 非依存・一定)
        [Xunit.Fact]
        public void T041_JointSpacing_PP_IsConstant2478()
        {
            Xunit.Assert.Equal(0.2478, SheetPileQuayWall.Core.FrontWall.JointGeometry.JointSpacing(
                0.800, SheetPileQuayWall.Core.FrontWall.JointType.PP), 10);
            Xunit.Assert.Equal(0.2478, SheetPileQuayWall.Core.FrontWall.JointGeometry.JointSpacing(
                1.500, SheetPileQuayWall.Core.FrontWall.JointType.PP), 10);
        }

        // T042: PT の J = 0.180 m (D 非依存・一定)
        [Xunit.Fact]
        public void T042_JointSpacing_PT_IsConstant180()
        {
            Xunit.Assert.Equal(0.180, SheetPileQuayWall.Core.FrontWall.JointGeometry.JointSpacing(
                0.800, SheetPileQuayWall.Core.FrontWall.JointType.PT), 10);
            Xunit.Assert.Equal(0.180, SheetPileQuayWall.Core.FrontWall.JointGeometry.JointSpacing(
                1.200, SheetPileQuayWall.Core.FrontWall.JointType.PT), 10);
        }

        // T043: LT65 φ1000 → J = 69.6 mm (カタログ例値、±0.05mm)
        [Xunit.Fact]
        public void T043_JointSpacing_LT65_Phi1000_Matches696()
        {
            double j = SheetPileQuayWall.Core.FrontWall.JointGeometry.JointSpacing(
                1.000, SheetPileQuayWall.Core.FrontWall.JointType.LT65);
            Xunit.Assert.Equal(0.0696, j, 4);
        }

        // T044: LT75 φ1000 → J = 77.3 mm (カタログ例値、±0.05mm)
        [Xunit.Fact]
        public void T044_JointSpacing_LT75_Phi1000_Matches773()
        {
            double j = SheetPileQuayWall.Core.FrontWall.JointGeometry.JointSpacing(
                1.000, SheetPileQuayWall.Core.FrontWall.JointType.LT75);
            Xunit.Assert.Equal(0.0773, j, 4);
        }

        // T045: W = D + J の恒等関係 (LT100 以外の全型式、D=0.8)
        [Xunit.Fact]
        public void T045_EffectiveWidth_EqualsDPlusJ()
        {
            SheetPileQuayWall.Core.FrontWall.JointType[] types =
            {
                SheetPileQuayWall.Core.FrontWall.JointType.PP,
                SheetPileQuayWall.Core.FrontWall.JointType.PT,
                SheetPileQuayWall.Core.FrontWall.JointType.LT65,
                SheetPileQuayWall.Core.FrontWall.JointType.LT75,
            };
            foreach (SheetPileQuayWall.Core.FrontWall.JointType jt in types)
            {
                double D_m = 0.800;
                double j = SheetPileQuayWall.Core.FrontWall.JointGeometry.JointSpacing(D_m, jt);
                double w = SheetPileQuayWall.Core.FrontWall.JointGeometry.EffectiveWidth(D_m, jt);
                Xunit.Assert.Equal(D_m + j, w, 12);
            }
        }

        // T046: LT100 はカタログ式なし → J・W とも NaN
        [Xunit.Fact]
        public void T046_LT100_ReturnsNaN()
        {
            Xunit.Assert.True(double.IsNaN(
                SheetPileQuayWall.Core.FrontWall.JointGeometry.JointSpacing(
                    1.000, SheetPileQuayWall.Core.FrontWall.JointType.LT100)));
            Xunit.Assert.True(double.IsNaN(
                SheetPileQuayWall.Core.FrontWall.JointGeometry.EffectiveWidth(
                    1.000, SheetPileQuayWall.Core.FrontWall.JointType.LT100)));
        }

        // T047: LT65 で D < 0.160 (√ の中が負) → NaN
        [Xunit.Fact]
        public void T047_JointSpacing_LT65_TooSmallDiameter_ReturnsNaN()
        {
            Xunit.Assert.True(double.IsNaN(
                SheetPileQuayWall.Core.FrontWall.JointGeometry.JointSpacing(
                    0.150, SheetPileQuayWall.Core.FrontWall.JointType.LT65)));
        }

        // T048: LT75 で D < 0.180 (√ の中が負) → NaN
        [Xunit.Fact]
        public void T048_JointSpacing_LT75_TooSmallDiameter_ReturnsNaN()
        {
            Xunit.Assert.True(double.IsNaN(
                SheetPileQuayWall.Core.FrontWall.JointGeometry.JointSpacing(
                    0.170, SheetPileQuayWall.Core.FrontWall.JointType.LT75)));
        }
    }
}
