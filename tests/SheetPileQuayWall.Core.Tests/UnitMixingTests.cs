// T980〜T982: mm 値の内部混入検出
// 検証基準: CLAUDE.PRIVATE.md §2.1 / §8、docs/implementation-plan.md §1 決定7
//   mm は対話プロンプトの入力時呼称のみ。Core に渡る時点で m でなければならない。
//   mm 値をそのまま渡した場合 (例: 800 を D_m として渡す) は範囲チェックが検出する。

namespace SheetPileQuayWall.Core.Tests
{
    public class UnitMixingTests
    {
        // T980: 前壁 外径に mm 値 (800) を渡すと範囲外エラー。m 値 (0.800) は正常
        [Xunit.Fact]
        public void T980_FrontWall_ValidateD_RejectsMillimeterValue()
        {
            Xunit.Assert.NotNull(
                SheetPileQuayWall.Core.FrontWall.InputValidator.ValidateD(800.0));
            Xunit.Assert.Null(
                SheetPileQuayWall.Core.FrontWall.InputValidator.ValidateD(0.800));
        }

        // T981: 前壁 肉厚に mm 値 (12) を渡すと範囲外エラー。m 値 (0.012) は正常
        [Xunit.Fact]
        public void T981_FrontWall_ValidateT_RejectsMillimeterValue()
        {
            Xunit.Assert.NotNull(
                SheetPileQuayWall.Core.FrontWall.InputValidator.ValidateT(12.0, 0.800));
            Xunit.Assert.Null(
                SheetPileQuayWall.Core.FrontWall.InputValidator.ValidateT(0.012, 0.800));
        }

        // T982: 控え杭も同様。JIS スナップは m 単位で動作する (800mm → 0.800m ではなく
        //       0.800m がそのまま最近傍となること)
        [Xunit.Fact]
        public void T982_AnchorPile_RejectsMillimeterValueAndSnapsInMeters()
        {
            Xunit.Assert.NotNull(
                SheetPileQuayWall.Core.AnchorPile.AnchorPileSteel.ValidateD(800.0));
            Xunit.Assert.Null(
                SheetPileQuayWall.Core.AnchorPile.AnchorPileSteel.ValidateD(0.800));
            Xunit.Assert.Equal(
                0.800,
                SheetPileQuayWall.Core.AnchorPile.AnchorPileSteel.SnapToJis(0.79), 9);
        }
    }
}
