// T061〜T064: JointPlacement (継手ローカル座標の配置変換) の単体テスト
// 変換式: wx = (R+lx)·cosφ − ly·sinφ, wy = (R+lx)·sinφ + ly·cosφ

namespace SheetPileQuayWall.Core.Tests
{
    public class JointPlacementTests
    {
        // T061: φ=0 → (R+lx, ly) 恒等配置
        [Xunit.Fact]
        public void T061_PhiZero_PlacesOnPositiveX()
        {
            double x = SheetPileQuayWall.Core.FrontWall.JointPlacement.TransformX(
                0.010, 0.020, 0.400, 0.0);
            double y = SheetPileQuayWall.Core.FrontWall.JointPlacement.TransformY(
                0.010, 0.020, 0.400, 0.0);
            Xunit.Assert.Equal(0.410, x, 12);
            Xunit.Assert.Equal(0.020, y, 12);
        }

        // T062: φ=+π/2 → (−ly, R+lx) — +Y 側配置 (継手A側)
        [Xunit.Fact]
        public void T062_PhiPlusHalfPi_PlacesOnPositiveY()
        {
            double phi = System.Math.PI / 2.0;
            double x = SheetPileQuayWall.Core.FrontWall.JointPlacement.TransformX(
                0.010, 0.020, 0.400, phi);
            double y = SheetPileQuayWall.Core.FrontWall.JointPlacement.TransformY(
                0.010, 0.020, 0.400, phi);
            Xunit.Assert.Equal(-0.020, x, 12);
            Xunit.Assert.Equal(0.410, y, 12);
        }

        // T063: φ=−π/2 → (ly, −(R+lx)) — −Y 側配置 (継手B側)
        [Xunit.Fact]
        public void T063_PhiMinusHalfPi_PlacesOnNegativeY()
        {
            double phi = -System.Math.PI / 2.0;
            double x = SheetPileQuayWall.Core.FrontWall.JointPlacement.TransformX(
                0.010, 0.020, 0.400, phi);
            double y = SheetPileQuayWall.Core.FrontWall.JointPlacement.TransformY(
                0.010, 0.020, 0.400, phi);
            Xunit.Assert.Equal(0.020, x, 12);
            Xunit.Assert.Equal(-0.410, y, 12);
        }

        // T064: TransformLoop は長さを保持し、各頂点が TransformX/Y と一致
        [Xunit.Fact]
        public void T064_TransformLoop_MatchesPointwiseTransform()
        {
            double[] loop = { 0.0, 0.08, 0.065, 0.08, 0.065, 0.015, 0.0, -0.072 };
            double phi = System.Math.PI / 2.0;
            double[] world = SheetPileQuayWall.Core.FrontWall.JointPlacement.TransformLoop(
                loop, 0.400, phi);

            Xunit.Assert.Equal(loop.Length, world.Length);
            for (int i = 0; i < loop.Length; i += 2)
            {
                Xunit.Assert.Equal(
                    SheetPileQuayWall.Core.FrontWall.JointPlacement.TransformX(
                        loop[i], loop[i + 1], 0.400, phi), world[i], 12);
                Xunit.Assert.Equal(
                    SheetPileQuayWall.Core.FrontWall.JointPlacement.TransformY(
                        loop[i], loop[i + 1], 0.400, phi), world[i + 1], 12);
            }
        }
    }
}
