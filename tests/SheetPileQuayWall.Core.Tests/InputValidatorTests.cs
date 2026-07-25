// T030〜T038: InputValidator の単体テスト

namespace SheetPileQuayWall.Core.Tests
{
    public class InputValidatorTests
    {
        // ── ValidateD ──────────────────────────────────────────────────────

        // T030: D 境界最小値(500mm) → null (正常)
        [Xunit.Fact]
        public void T030_ValidateD_MinBoundary_ReturnsNull()
        {
            Xunit.Assert.Null(
                SheetPileQuayWall.Core.FrontWall.InputValidator.ValidateD(0.500));
        }

        // T031: D 境界最大値(2000mm) → null (正常)
        [Xunit.Fact]
        public void T031_ValidateD_MaxBoundary_ReturnsNull()
        {
            Xunit.Assert.Null(
                SheetPileQuayWall.Core.FrontWall.InputValidator.ValidateD(2.000));
        }

        // T032: D が最小値未満(400mm) → エラーメッセージを返す
        [Xunit.Fact]
        public void T032_ValidateD_BelowMin_ReturnsError()
        {
            string? err = SheetPileQuayWall.Core.FrontWall.InputValidator.ValidateD(0.400);
            Xunit.Assert.NotNull(err);
            Xunit.Assert.Contains("400.0mm", err);
        }

        // T033: D が最大値超(2100mm) → エラーメッセージを返す
        [Xunit.Fact]
        public void T033_ValidateD_AboveMax_ReturnsError()
        {
            Xunit.Assert.NotNull(
                SheetPileQuayWall.Core.FrontWall.InputValidator.ValidateD(2.100));
        }

        // ── ValidateT ──────────────────────────────────────────────────────

        // T034: t 正常範囲(12mm) → null
        [Xunit.Fact]
        public void T034_ValidateT_NormalValue_ReturnsNull()
        {
            Xunit.Assert.Null(
                SheetPileQuayWall.Core.FrontWall.InputValidator.ValidateT(0.012, 0.800));
        }

        // T035: t が最小値未満(8mm) → エラー
        [Xunit.Fact]
        public void T035_ValidateT_BelowMin_ReturnsError()
        {
            Xunit.Assert.NotNull(
                SheetPileQuayWall.Core.FrontWall.InputValidator.ValidateT(0.008, 0.800));
        }

        // T036: t が最大値超(26mm) → エラー
        [Xunit.Fact]
        public void T036_ValidateT_AboveMax_ReturnsError()
        {
            Xunit.Assert.NotNull(
                SheetPileQuayWall.Core.FrontWall.InputValidator.ValidateT(0.026, 0.800));
        }

        // T037: 内径 ≤ 1mm になる組合せ → エラー (d = 500 - 2×250 = 0mm)
        [Xunit.Fact]
        public void T037_ValidateT_InnerDiameterTooSmall_ReturnsError()
        {
            // D=500mm, t=25mm → d = 450mm は正常だが、极端な例として
            // D=500mm, t=248mm は範囲外 → まずは t 範囲チェックが先に発動する
            // 内径チェックを直接テストするために t を範囲内に保ちつつ D を小さくする
            // D=30mm(範囲外) → このケースでは ValidateD で弾かれるが
            // ValidateT は D をそのまま使うため内径チェックは独立して動作する
            // t=25mm, D=51mm → d = 51 - 50 = 1mm → 境界 (1mm = 0.001m は ≤ 0.001 なのでエラー)
            string? err = SheetPileQuayWall.Core.FrontWall.InputValidator.ValidateT(0.025, 0.051);
            Xunit.Assert.NotNull(err);
            Xunit.Assert.Contains("内径", err);
        }

        // ── ValidateL ──────────────────────────────────────────────────────

        // T038: L 正常値(20m) → null
        [Xunit.Fact]
        public void T038_ValidateL_NormalValue_ReturnsNull()
        {
            Xunit.Assert.Null(
                SheetPileQuayWall.Core.FrontWall.InputValidator.ValidateL(20.0));
        }

        // T039: L が最小値未満(0.5m) → エラー
        [Xunit.Fact]
        public void T039_ValidateL_BelowMin_ReturnsError()
        {
            Xunit.Assert.NotNull(
                SheetPileQuayWall.Core.FrontWall.InputValidator.ValidateL(0.5));
        }

        // T040: L が最大値超(81m) → エラー
        [Xunit.Fact]
        public void T040_ValidateL_AboveMax_ReturnsError()
        {
            Xunit.Assert.NotNull(
                SheetPileQuayWall.Core.FrontWall.InputValidator.ValidateL(81.0));
        }

    }
}
