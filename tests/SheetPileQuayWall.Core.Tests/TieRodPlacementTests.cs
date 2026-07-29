// T985〜T987: TieRodPlacement の単体テスト(決定8)
// 検証基準: docs/implementation-plan.md §1 決定8 / §7.2
//   海側取付 X = PileGeometry.AxisXAt(前壁.TipPoint, 前壁.InclDeg, tie_elevation)
//   θ=0 のときは移植元 008(杭先端 X をそのまま使う目視クリック方式)と一致すること

namespace SheetPileQuayWall.Core.Tests
{
    public class TieRodPlacementTests
    {
        // Z_tip=(0.0, 5.0, -18.0) 相当になるよう、L・θ に応じた HeadPoint を
        // LocalToWorld(順変換)で逆算する(2026-07-29、内部表現を Z_head 基準へ変更)。
        private static SheetPileQuayWall.Core.FrontWallRef Front(double inclDeg)
        {
            const double L = 25.0;
            SheetPileQuayWall.Core.Point3 tip =
                new SheetPileQuayWall.Core.Point3(0.0, 5.0, -18.0);
            SheetPileQuayWall.Core.Point3 head =
                SheetPileQuayWall.Core.PileGeometry.LocalToWorld(
                    new SheetPileQuayWall.Core.Point3(0.0, 0.0, L), inclDeg, tip);

            return new SheetPileQuayWall.Core.FrontWallRef
            {
                HeadPoint = head,
                OuterDm = 1.000,
                InclDeg = inclDeg,
                LengthM = L,
                JointType = SheetPileQuayWall.Core.FrontWall.JointType.PP
            };
        }

        // T985: 前壁が鉛直なら海側取付 X は杭先端の X と一致する(008 互換)
        [Xunit.Fact]
        public void T985_SeaAttachmentX_VerticalFrontWall_EqualsTipX()
        {
            double x = SheetPileQuayWall.Core.TieRod.TieRodPlacement.SeaAttachmentX(
                Front(0.0), 2.5);
            Xunit.Assert.Equal(0.0, x, 9);
        }

        // T986: θ=15° では (tie_elevation − Z_tip)·tan15° だけ陸側へずれる
        //       (標高差 20.5 m で約 5.49 m。目視クリックでは追随できない量)
        [Xunit.Fact]
        public void T986_SeaAttachmentX_Inclined15deg_ShiftsLandward()
        {
            double expected = (2.5 - (-18.0))
                * System.Math.Tan(15.0 * System.Math.PI / 180.0);

            double x = SheetPileQuayWall.Core.TieRod.TieRodPlacement.SeaAttachmentX(
                Front(15.0), 2.5);

            Xunit.Assert.Equal(expected, x, 9);
            Xunit.Assert.True(x > 5.0, "θ=15° のずれは 5 m を超える");
        }

        // T987: 取付点は X を自動計算し、Y は入力位置、Z はタイロッド軸心標高
        [Xunit.Fact]
        public void T987_SeaAttachmentPoint_ComposesYAndElevation()
        {
            SheetPileQuayWall.Core.Point3 p =
                SheetPileQuayWall.Core.TieRod.TieRodPlacement.SeaAttachmentPoint(
                    Front(10.0), 2.5, 42.0);

            double expectedX = (2.5 - (-18.0))
                * System.Math.Tan(10.0 * System.Math.PI / 180.0);

            Xunit.Assert.Equal(expectedX, p.X, 9);
            Xunit.Assert.Equal(42.0, p.Y, 9);
            Xunit.Assert.Equal(2.5, p.Z, 9);
        }
    }
}
