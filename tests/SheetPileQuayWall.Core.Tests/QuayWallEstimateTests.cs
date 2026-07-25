// T1020〜T1024: QuayWallEstimate(岸壁 1 施設分の数量集計)の単体テスト
// 検証基準: 前壁の継手は施工順位ごとに要否が変わるため、全本数の合計は
//   「接続数 × 1 接続あたり質量」に一致しなければならない(内部整合)。

namespace SheetPileQuayWall.Core.Tests
{
    public class QuayWallEstimateTests
    {
        private static SheetPileQuayWall.Core.QuayWallComposition Composition()
        {
            return new SheetPileQuayWall.Core.QuayWallComposition
            {
                FrontOuterDm = 0.800,
                FrontWallTm = 0.012,
                FrontLengthM = 20.0,
                FrontJointType = SheetPileQuayWall.Core.FrontWall.JointType.LT75,
                FrontPieceCount = 10,
                TieRodSetCount = 5,
                TieRodMassPerSet = 150.0,
                AnchorPileCount = 5,
                AnchorOuterDm = 0.800,
                AnchorWallTm = 0.012,
                AnchorLengthM = 18.0,
                AnchorClosedTip = false
            };
        }

        // T1020: 前壁本管質量 = 単位重量 × 全長 × 本数
        [Xunit.Fact]
        public void T1020_FrontBodyMass()
        {
            SheetPileQuayWall.Core.QuayWallComposition c = Composition();
            double unit = SheetPileQuayWall.Core.FrontWall.SectionProperties.CalcW(
                c.FrontOuterDm, c.FrontWallTm);

            SheetPileQuayWall.Core.QuayWallQuantity q =
                SheetPileQuayWall.Core.QuayWallEstimate.Compute(c);

            Xunit.Assert.Equal(unit * 20.0 * 10, q.FrontBodyKg, 6);
        }

        // T1021: 継手質量の全本数合計 = 接続数 × 1 接続あたり質量
        //        (1 本目は A 側のみ、最終本は B 側のみになるため両端で半端が出ない)
        [Xunit.Fact]
        public void T1021_FrontJointMass_EqualsConnectionsTimesPerConnection()
        {
            SheetPileQuayWall.Core.QuayWallComposition c = Composition();

            SheetPileQuayWall.Core.QuayWallQuantity q =
                SheetPileQuayWall.Core.QuayWallEstimate.Compute(c);

            double perConnection =
                SheetPileQuayWall.Core.FrontWall.JointMass.PerConnection_kgPerM(c.FrontJointType);
            Xunit.Assert.Equal(9, q.JointConnectionCount);
            Xunit.Assert.Equal(perConnection * c.FrontLengthM * 9, q.FrontJointKg, 6);
        }

        // T1022: P-P 形でも上の関係が成り立つ(移植元 JointMassPerM を使うと崩れる)
        [Xunit.Fact]
        public void T1022_FrontJointMass_PP_UsesBothSides()
        {
            SheetPileQuayWall.Core.QuayWallComposition c = Composition();
            c.FrontJointType = SheetPileQuayWall.Core.FrontWall.JointType.PP;

            SheetPileQuayWall.Core.QuayWallQuantity q =
                SheetPileQuayWall.Core.QuayWallEstimate.Compute(c);

            // 69.4 kg/m × 20 m × 9 接続。移植元の 34.7 を使うとこの半分になる
            Xunit.Assert.Equal(69.4 * 20.0 * 9, q.FrontJointKg, 6);
        }

        // T1023: 施設延長 = 有効幅 B × 本数。単独杭(1 本)は継手接続 0
        [Xunit.Fact]
        public void T1023_WallLengthAndSinglePile()
        {
            SheetPileQuayWall.Core.QuayWallComposition c = Composition();
            double B = SheetPileQuayWall.Core.FrontWall.JointParameters.EffectiveWidth(
                c.FrontOuterDm, c.FrontJointType);

            SheetPileQuayWall.Core.QuayWallQuantity q =
                SheetPileQuayWall.Core.QuayWallEstimate.Compute(c);
            Xunit.Assert.Equal(B * 10, q.WallLengthM, 6);

            c.FrontPieceCount = 1;
            SheetPileQuayWall.Core.QuayWallQuantity single =
                SheetPileQuayWall.Core.QuayWallEstimate.Compute(c);
            Xunit.Assert.Equal(0, single.JointConnectionCount);
            Xunit.Assert.Equal(0.0, single.FrontJointKg, 6);
        }

        // T1024: 閉端控え杭は底板質量が加算され、合計は各部材の和に一致する
        [Xunit.Fact]
        public void T1024_ClosedTipPlateAndTotal()
        {
            SheetPileQuayWall.Core.QuayWallComposition c = Composition();
            c.AnchorClosedTip = true;

            SheetPileQuayWall.Core.QuayWallQuantity q =
                SheetPileQuayWall.Core.QuayWallEstimate.Compute(c);

            // 底板: π/4 × D[cm]^2 × t[cm] × 0.00785 kg/cm3 × 本数
            double expectedPlate = System.Math.PI / 4.0 * 80.0 * 80.0 * 1.2 * 0.00785 * 5;
            Xunit.Assert.Equal(expectedPlate, q.AnchorPlateKg, 6);

            Xunit.Assert.Equal(750.0, q.TieRodKg, 6);   // 150 kg × 5 組
            Xunit.Assert.Equal(
                q.FrontBodyKg + q.FrontJointKg + q.TieRodKg
                    + q.AnchorBodyKg + q.AnchorPlateKg,
                q.TotalKg, 6);
        }
    }
}
