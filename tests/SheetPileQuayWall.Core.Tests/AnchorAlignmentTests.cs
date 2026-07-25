// T970〜T976: AnchorAlignment の単体テスト
// 検証基準: 006@6d6d8cf src/AnchorPile.cs ComputeTipPoint / ValidateAlignment
//   前壁軸 X(Z_tr) = 前壁挿入点 X + (Z_tr − 前壁先端 Z)·tanθ_f
//   控え杭軸 X(Z_tr) = 前壁軸 X(Z_tr) + span − D_a/2
//   杭先端 X = 控え杭軸 X(Z_tr) − (Z_tr − Z_tip)·tanθ_a、Y は前壁と同一

namespace SheetPileQuayWall.Core.Tests
{
    public class AnchorAlignmentTests
    {
        // 標準ケース: 前壁 D=0.8m 直杭、控え杭 D=0.8m 直杭、span=10m、Z_tr=2.5m
        private static SheetPileQuayWall.Core.FrontWallRef Front(
            double inclDeg = 0.0, double tipX = 0.0, double tipZ = -18.0)
        {
            return new SheetPileQuayWall.Core.FrontWallRef
            {
                TipPoint = new SheetPileQuayWall.Core.Point3(tipX, 5.0, tipZ),
                OuterDm = 0.800,
                InclDeg = inclDeg,
                LengthM = 25.0
            };
        }

        private static SheetPileQuayWall.Core.AnchorPile.AnchorInput Anchor(
            double inclDeg = 0.0, double spanM = 10.0)
        {
            return new SheetPileQuayWall.Core.AnchorPile.AnchorInput
            {
                OuterDm = 0.800, WallTm = 0.012, LengthM = 20.0, InclDeg = inclDeg,
                ClosedTip = false, SpanM = spanM, TieElevM = 2.5, TipElevM = -14.0,
                ColorIdx = 8
            };
        }

        // T970: 直杭どうし。杭先端 X = 前壁 X + span − D_a/2、Y は前壁と同一
        [Xunit.Fact]
        public void T970_Compute_BothVertical_TipXIsSpanMinusHalfDiameter()
        {
            SheetPileQuayWall.Core.AnchorPile.AnchorResult r =
                SheetPileQuayWall.Core.AnchorPile.AnchorAlignment.Compute(Front(), Anchor());

            Xunit.Assert.Equal(10.0 - 0.400, r.TipPoint.X, 9);
            Xunit.Assert.Equal(5.0, r.TipPoint.Y, 9);
            Xunit.Assert.Equal(-14.0, r.TipPoint.Z, 9);
        }

        // T971: 前壁が θ_f=10° 傾斜すると、Z_tr における前壁軸が (Z_tr − Z_tip_f)·tan10° ずれる
        [Xunit.Fact]
        public void T971_Compute_InclinedFrontWall_ShiftsFrontAxis()
        {
            double expectedShift = (2.5 - (-18.0))
                * System.Math.Tan(10.0 * System.Math.PI / 180.0);

            SheetPileQuayWall.Core.AnchorPile.AnchorResult r =
                SheetPileQuayWall.Core.AnchorPile.AnchorAlignment.Compute(
                    Front(inclDeg: 10.0), Anchor());

            Xunit.Assert.Equal(expectedShift, r.FrontAxisXAtTie_m, 9);
            Xunit.Assert.Equal(expectedShift + 10.0 - 0.400, r.TipPoint.X, 9);
        }

        // T972: 控え杭が θ_a=10° 傾斜すると、杭先端は軸位置から (Z_tr − Z_tip)·tan10° 戻る
        [Xunit.Fact]
        public void T972_Compute_InclinedAnchorPile_TipOffsetFromAxis()
        {
            double back = (2.5 - (-14.0)) * System.Math.Tan(10.0 * System.Math.PI / 180.0);

            SheetPileQuayWall.Core.AnchorPile.AnchorResult r =
                SheetPileQuayWall.Core.AnchorPile.AnchorAlignment.Compute(
                    Front(), Anchor(inclDeg: 10.0));

            Xunit.Assert.Equal(10.0 - 0.400, r.AnchorAxisXAtTie_m, 9);
            Xunit.Assert.Equal(10.0 - 0.400 - back, r.TipPoint.X, 9);
        }

        // T973: 派生量 — 軸間水平距離 = span − D_a/2、杭面間浄距離 = 軸間 − D_f/2 − D_a/2
        [Xunit.Fact]
        public void T973_Compute_DerivedSpacingAndClearance()
        {
            SheetPileQuayWall.Core.AnchorPile.AnchorResult r =
                SheetPileQuayWall.Core.AnchorPile.AnchorAlignment.Compute(Front(), Anchor());

            Xunit.Assert.Equal(10.0 - 0.400, r.AxisSpacing_m, 9);
            Xunit.Assert.Equal(10.0 - 0.400 - 0.400 - 0.400, r.FaceClearance_m, 9);
            Xunit.Assert.Equal(-14.0 + 20.0, r.HeadElev_m, 9);
        }

        // T974: Z_tr が控え杭の杭体範囲外ならエラー (杭頭 = −14+20 = 6.0 m)
        [Xunit.Theory]
        [Xunit.InlineData(6.0, true)]      // 杭頭ちょうど
        [Xunit.InlineData(-14.0, true)]    // 杭先端ちょうど
        [Xunit.InlineData(6.5, false)]     // 杭頭より上
        public void T974_Validate_TieElevationWithinAnchorBody(double tieElevM, bool expectValid)
        {
            SheetPileQuayWall.Core.AnchorPile.AnchorInput a = Anchor();
            a.TieElevM = tieElevM;

            string? e = SheetPileQuayWall.Core.AnchorPile.AnchorAlignment.Validate(Front(), a);
            Xunit.Assert.Equal(expectValid, e == null);
        }

        // T975: Z_tr が前壁の杭体範囲外ならエラー (前壁 杭頭 = −18+25 = 7.0 m)
        [Xunit.Fact]
        public void T975_Validate_TieElevationAboveFrontWallHead()
        {
            SheetPileQuayWall.Core.AnchorPile.AnchorInput a = Anchor();
            a.TieElevM = 8.0;   // 前壁杭頭 7.0 m を超える。控え杭 (杭頭 6.0m) にも掛からない
            a.LengthM = 30.0;   // 控え杭側は範囲内 (杭頭 16.0 m) にして前壁側だけを検出させる

            string? e = SheetPileQuayWall.Core.AnchorPile.AnchorAlignment.Validate(Front(), a);
            Xunit.Assert.NotNull(e);
            Xunit.Assert.Contains("前壁の杭体範囲", e);
        }

        // T976: 干渉チェック span ≥ D_f/2 + D_a (= 0.4 + 0.8 = 1.2 m)。誤差許容 1 mm
        [Xunit.Theory]
        [Xunit.InlineData(1.200, true)]    // 境界ちょうど (杭面間浄距離 0)
        [Xunit.InlineData(1.1995, true)]   // 0.5 mm 不足 — 誤差許容内
        [Xunit.InlineData(1.198, false)]   // 2 mm 不足 — エラー
        public void T976_Validate_InterferenceSpan(double spanM, bool expectValid)
        {
            SheetPileQuayWall.Core.AnchorPile.AnchorInput a = Anchor(spanM: spanM);

            string? e = SheetPileQuayWall.Core.AnchorPile.AnchorAlignment.Validate(Front(), a);
            Xunit.Assert.Equal(expectValid, e == null);
        }
    }
}
