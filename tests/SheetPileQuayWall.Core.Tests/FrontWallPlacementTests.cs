// T960〜T965: FrontWallPlacement / PileGeometry の単体テスト
// 検証基準: 006@6d6d8cf src/SteelPipePile.cs BuildPileSolid の変換順序
//   Matrix3d.Rotation(θ, YAxis, Origin) → Matrix3d.Displacement(tip)
// および docs/implementation-plan.md §2.2 (平面位置ピック + 標高数値入力の分離)

namespace SheetPileQuayWall.Core.Tests
{
    public class FrontWallPlacementTests
    {
        // T960: 直杭 (θ=0) の杭頭は杭先端の真上、標高差は全長に等しい
        [Xunit.Fact]
        public void T960_LocalToWorld_Vertical_HeadDirectlyAboveTip()
        {
            SheetPileQuayWall.Core.Point3 tip =
                new SheetPileQuayWall.Core.Point3(10.0, 20.0, -18.0);
            SheetPileQuayWall.Core.Point3 head =
                SheetPileQuayWall.Core.PileGeometry.LocalToWorld(
                    new SheetPileQuayWall.Core.Point3(0.0, 0.0, 20.0), 0.0, tip);

            Xunit.Assert.Equal(10.0, head.X, 9);
            Xunit.Assert.Equal(20.0, head.Y, 9);
            Xunit.Assert.Equal(2.0, head.Z, 9);
        }

        // T961: θ=10° の杭頭は陸側 (+X) へ L·sinθ ずれ、標高は L·cosθ 上がる
        [Xunit.Fact]
        public void T961_LocalToWorld_Inclined10deg_MatchesSinCos()
        {
            double L = 20.0;
            double rad = 10.0 * System.Math.PI / 180.0;
            SheetPileQuayWall.Core.Point3 tip =
                new SheetPileQuayWall.Core.Point3(0.0, 0.0, -18.0);
            SheetPileQuayWall.Core.Point3 head =
                SheetPileQuayWall.Core.PileGeometry.LocalToWorld(
                    new SheetPileQuayWall.Core.Point3(0.0, 0.0, L), 10.0, tip);

            Xunit.Assert.Equal(L * System.Math.Sin(rad), head.X, 9);
            Xunit.Assert.Equal(0.0, head.Y, 9);
            Xunit.Assert.Equal(-18.0 + L * System.Math.Cos(rad), head.Z, 9);
            Xunit.Assert.Equal(
                head.Z,
                SheetPileQuayWall.Core.PileGeometry.HeadElevation(-18.0, L, 10.0), 9);
        }

        // T962: AxisXAt は LocalToWorld と厳密に整合する (θ=15°、杭頭標高で照合)
        [Xunit.Fact]
        public void T962_AxisXAt_ConsistentWithLocalToWorld()
        {
            double L = 25.0;
            SheetPileQuayWall.Core.Point3 tip =
                new SheetPileQuayWall.Core.Point3(-3.5, 7.0, -22.0);
            SheetPileQuayWall.Core.Point3 head =
                SheetPileQuayWall.Core.PileGeometry.LocalToWorld(
                    new SheetPileQuayWall.Core.Point3(0.0, 0.0, L), 15.0, tip);

            double axisX = SheetPileQuayWall.Core.PileGeometry.AxisXAt(tip, 15.0, head.Z);
            Xunit.Assert.Equal(head.X, axisX, 9);
        }

        // T963: 基準点はピック点の Z ではなく入力した杭上端標高を使う (§2.2、
        //       2026-07-29 に Z_tip → Z_head へ変更)
        [Xunit.Fact]
        public void T963_HeadPoint_UsesInputElevationNotPickedZ()
        {
            SheetPileQuayWall.Core.Point3 head =
                SheetPileQuayWall.Core.FrontWall.FrontWallPlacement.HeadPoint(
                    12.345, -6.789, 2.0);

            Xunit.Assert.Equal(12.345, head.X, 9);
            Xunit.Assert.Equal(-6.789, head.Y, 9);
            Xunit.Assert.Equal(2.0, head.Z, 9);
        }

        // T1302: TipFromHead は LocalToWorld(head 側)の厳密な逆演算である
        //        (2026-07-29、前壁の内部表現を Z_head 基準へ変更するために新設)
        [Xunit.Fact]
        public void T1302_TipFromHead_IsExactInverseOfLocalToWorld()
        {
            double L = 25.0;
            double inclDeg = 12.0;
            SheetPileQuayWall.Core.Point3 tip =
                new SheetPileQuayWall.Core.Point3(-3.5, 7.0, -22.0);
            SheetPileQuayWall.Core.Point3 head =
                SheetPileQuayWall.Core.PileGeometry.LocalToWorld(
                    new SheetPileQuayWall.Core.Point3(0.0, 0.0, L), inclDeg, tip);

            SheetPileQuayWall.Core.Point3 recoveredTip =
                SheetPileQuayWall.Core.PileGeometry.TipFromHead(head, L, inclDeg);

            Xunit.Assert.Equal(tip.X, recoveredTip.X, 9);
            Xunit.Assert.Equal(tip.Y, recoveredTip.Y, 9);
            Xunit.Assert.Equal(tip.Z, recoveredTip.Z, 9);
        }

        // T1303: 直杭 (θ=0) では X・Y は不変、Z のみ L だけ下がる
        [Xunit.Fact]
        public void T1303_TipFromHead_Vertical_ShiftsZOnlyByLength()
        {
            SheetPileQuayWall.Core.Point3 head =
                new SheetPileQuayWall.Core.Point3(10.0, 20.0, 2.0);
            SheetPileQuayWall.Core.Point3 tip =
                SheetPileQuayWall.Core.PileGeometry.TipFromHead(head, 20.0, 0.0);

            Xunit.Assert.Equal(10.0, tip.X, 9);
            Xunit.Assert.Equal(20.0, tip.Y, 9);
            Xunit.Assert.Equal(-18.0, tip.Z, 9);
        }

        // T964: 傾斜角の範囲チェック (0〜15度)
        [Xunit.Theory]
        [Xunit.InlineData(0.0, true)]
        [Xunit.InlineData(15.0, true)]
        [Xunit.InlineData(-0.1, false)]
        [Xunit.InlineData(15.1, false)]
        public void T964_ValidateInclination_Range(double inclDeg, bool expectValid)
        {
            string? e = SheetPileQuayWall.Core.FrontWall.FrontWallPlacement
                .ValidateInclination(inclDeg);
            Xunit.Assert.Equal(expectValid, e == null);
        }

        // T965: 杭先端標高の範囲チェック (−80〜10 m、D.L. 基準)
        [Xunit.Theory]
        [Xunit.InlineData(-80.0, true)]
        [Xunit.InlineData(10.0, true)]
        [Xunit.InlineData(-80.1, false)]
        [Xunit.InlineData(10.1, false)]
        public void T965_ValidateTipElevation_Range(double tipElevM, bool expectValid)
        {
            string? e = SheetPileQuayWall.Core.FrontWall.FrontWallPlacement
                .ValidateTipElevation(tipElevM);
            Xunit.Assert.Equal(expectValid, e == null);
        }
    }
}
