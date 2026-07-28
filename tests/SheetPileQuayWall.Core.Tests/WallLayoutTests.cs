// T1260〜T1269: WallLayout (壁一括レイアウト) の単体テスト
// 検証基準: 施設全長と有効幅から本数を切り上げで求め、始点から +Y へ等間隔に並べる
//   (SPQW_FRONTWALL_Create。端数処理の決定日 2026-07-29)
// 基準値: D=800mm / LT75 の有効幅 B = 0.8752 m (JointParametersTests と同じ値)

namespace SheetPileQuayWall.Core.Tests
{
    public class WallLayoutTests
    {
        private const double B_LT75_800 = 0.8752;

        // T1260: ちょうど整数倍のとき、浮動小数の丸め誤差で 1 本増えないこと
        [Xunit.Fact]
        public void T1260_PieceCountFor_ExactMultiple_DoesNotOvershoot()
        {
            // 8.752 = 0.8752 × 10。誤差許容を引かずに割ると 11 本になり得る
            Xunit.Assert.Equal(10,
                SheetPileQuayWall.Core.FrontWall.WallLayout.PieceCountFor(8.752, B_LT75_800));
        }

        // T1261: 端数がある場合は切り上げる (切り捨て・四捨五入ではない)
        [Xunit.Fact]
        public void T1261_PieceCountFor_Fraction_RoundsUp()
        {
            // 10.000 / 0.8752 = 11.426 → 12 本
            Xunit.Assert.Equal(12,
                SheetPileQuayWall.Core.FrontWall.WallLayout.PieceCountFor(10.0, B_LT75_800));
        }

        // T1262: 施設全長が有効幅より短くても 1 本は生成する
        [Xunit.Fact]
        public void T1262_PieceCountFor_ShorterThanOneWidth_ReturnsOne()
        {
            Xunit.Assert.Equal(1,
                SheetPileQuayWall.Core.FrontWall.WallLayout.PieceCountFor(0.5, B_LT75_800));
        }

        // T1263: 実延長は施設全長以上になる (切り上げの帰結)
        [Xunit.Fact]
        public void T1263_ActualLength_CoversWallLength()
        {
            int count = SheetPileQuayWall.Core.FrontWall.WallLayout.PieceCountFor(10.0, B_LT75_800);
            double actual =
                SheetPileQuayWall.Core.FrontWall.WallLayout.ActualLength(count, B_LT75_800);

            Xunit.Assert.Equal(10.5024, actual, 9);
            Xunit.Assert.True(actual >= 10.0);
        }

        // T1264: 各本の Y は 始点 + (施工順位 − 1) × 有効幅
        [Xunit.Fact]
        public void T1264_PositionY_AccumulatesEffectiveWidth()
        {
            Xunit.Assert.Equal(5.000,
                SheetPileQuayWall.Core.FrontWall.WallLayout.PositionY(5.0, 1, B_LT75_800), 9);
            Xunit.Assert.Equal(5.0 + B_LT75_800,
                SheetPileQuayWall.Core.FrontWall.WallLayout.PositionY(5.0, 2, B_LT75_800), 9);
            Xunit.Assert.Equal(5.0 + B_LT75_800 * 9.0,
                SheetPileQuayWall.Core.FrontWall.WallLayout.PositionY(5.0, 10, B_LT75_800), 9);
        }

        // T1265: 施設全長が範囲外ならエラー文字列を返す
        [Xunit.Fact]
        public void T1265_Validate_WallLengthOutOfRange_ReturnsError()
        {
            Xunit.Assert.NotNull(
                SheetPileQuayWall.Core.FrontWall.WallLayout.Validate(0.0, B_LT75_800));
            Xunit.Assert.NotNull(
                SheetPileQuayWall.Core.FrontWall.WallLayout.Validate(1000.1, B_LT75_800));
            Xunit.Assert.Null(
                SheetPileQuayWall.Core.FrontWall.WallLayout.Validate(10.0, B_LT75_800));
        }

        // T1266: 有効幅が範囲外ならエラー文字列を返す
        [Xunit.Fact]
        public void T1266_Validate_WidthOutOfRange_ReturnsError()
        {
            Xunit.Assert.NotNull(
                SheetPileQuayWall.Core.FrontWall.WallLayout.Validate(10.0, 0.4));
            Xunit.Assert.NotNull(
                SheetPileQuayWall.Core.FrontWall.WallLayout.Validate(10.0, 2.6));
        }

        // T1267: 本数が PieceAssignment の上限 (500 本) を超える組合せはエラー停止
        [Xunit.Fact]
        public void T1267_Validate_ExceedsPieceCountMax_ReturnsError()
        {
            // 0.5 m 幅 × 500 本 = 250 m。251 m は 502 本となり上限超過
            Xunit.Assert.Null(
                SheetPileQuayWall.Core.FrontWall.WallLayout.Validate(250.0, 0.5));
            Xunit.Assert.NotNull(
                SheetPileQuayWall.Core.FrontWall.WallLayout.Validate(251.0, 0.5));
        }

        // T1268: 算出した本数を PieceAssignment に渡すと、1 本目は +Y 側のみ、
        //        最終本は −Y 側のみに継手が付く (連結テスト)
        [Xunit.Fact]
        public void T1268_PieceCount_FeedsPieceAssignment_EndsHaveSingleJoint()
        {
            int count = SheetPileQuayWall.Core.FrontWall.WallLayout.PieceCountFor(8.752, B_LT75_800);
            Xunit.Assert.Equal(10, count);

            SheetPileQuayWall.Core.FrontWall.PieceJoints first =
                SheetPileQuayWall.Core.FrontWall.PieceAssignment.Assign(1, count);
            Xunit.Assert.False(first.HasLeadingJoint);
            Xunit.Assert.True(first.HasTrailingJoint);

            SheetPileQuayWall.Core.FrontWall.PieceJoints middle =
                SheetPileQuayWall.Core.FrontWall.PieceAssignment.Assign(5, count);
            Xunit.Assert.True(middle.HasLeadingJoint);
            Xunit.Assert.True(middle.HasTrailingJoint);

            SheetPileQuayWall.Core.FrontWall.PieceJoints last =
                SheetPileQuayWall.Core.FrontWall.PieceAssignment.Assign(count, count);
            Xunit.Assert.True(last.HasLeadingJoint);
            Xunit.Assert.False(last.HasTrailingJoint);
        }

        // T1269: 入力幅と継手形式からの算出値の差を返す (警告判定用。エラーにはしない)
        [Xunit.Fact]
        public void T1269_WidthDeviation_MeasuresGapFromJointDerivedWidth()
        {
            // 一致する場合は 0
            Xunit.Assert.Equal(0.0,
                SheetPileQuayWall.Core.FrontWall.WallLayout.WidthDeviation(
                    B_LT75_800, 0.800, SheetPileQuayWall.Core.FrontWall.JointType.LT75), 4);

            // 1 mm 未満のずれは誤差許容内 (警告しない)
            Xunit.Assert.True(
                SheetPileQuayWall.Core.FrontWall.WallLayout.WidthDeviation(
                    0.87565, 0.800, SheetPileQuayWall.Core.FrontWall.JointType.LT75)
                <= SheetPileQuayWall.Core.FrontWall.WallLayout.Tol_m);

            // 10 mm ずらすと誤差許容を超える (警告する)
            Xunit.Assert.True(
                SheetPileQuayWall.Core.FrontWall.WallLayout.WidthDeviation(
                    0.8852, 0.800, SheetPileQuayWall.Core.FrontWall.JointType.LT75)
                > SheetPileQuayWall.Core.FrontWall.WallLayout.Tol_m);
        }
    }
}
