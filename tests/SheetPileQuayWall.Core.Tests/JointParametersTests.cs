// T020〜T028: JointParameters の単体テスト
// 検証基準: 日本製鉄カタログ K011 (有効幅 B = D + 継手有効間隔 J)
//   ※ 旧実装 (LT: D+脚長 / PP・PT: D) は K011 と不整合だったため、
//     T020〜T024 の期待値を K011 準拠値へ更新済み (2026-07-25)。

namespace SheetPileQuayWall.Core.Tests
{
    public class JointParametersTests
    {
        // ── EffectiveWidth ─────────────────────────────────────────────────

        // T020: LT65 有効幅 = D + J(K011 √式)。D=0.8 → 0.8679184 m
        [Xunit.Fact]
        public void T020_EffectiveWidth_LT65_MatchesK011()
        {
            double B = SheetPileQuayWall.Core.FrontWall.JointParameters.EffectiveWidth(
                0.800, SheetPileQuayWall.Core.FrontWall.JointType.LT65);
            Xunit.Assert.Equal(0.8679184, B, 6);
        }

        // T021: LT75 有効幅 = D + J(K011 √式)。D=0.8 → 0.8752435 m
        [Xunit.Fact]
        public void T021_EffectiveWidth_LT75_MatchesK011()
        {
            double B = SheetPileQuayWall.Core.FrontWall.JointParameters.EffectiveWidth(
                0.800, SheetPileQuayWall.Core.FrontWall.JointType.LT75);
            Xunit.Assert.Equal(0.8752435, B, 6);
        }

        // T022: LT100 有効幅 = D + 100mm (カタログ式なし → 概算値 [推定])
        [Xunit.Fact]
        public void T022_EffectiveWidth_LT100_EqualsDPlus100mm()
        {
            double D_m = 1.000;
            double B = SheetPileQuayWall.Core.FrontWall.JointParameters.EffectiveWidth(
                D_m, SheetPileQuayWall.Core.FrontWall.JointType.LT100);
            Xunit.Assert.Equal(D_m + 0.100, B, 10);
        }

        // T023: PP 有効幅 = D + 247.8mm (K011・D 非依存の継手有効間隔)
        [Xunit.Fact]
        public void T023_EffectiveWidth_PP_EqualsDPlus2478()
        {
            double D_m = 0.900;
            double B = SheetPileQuayWall.Core.FrontWall.JointParameters.EffectiveWidth(
                D_m, SheetPileQuayWall.Core.FrontWall.JointType.PP);
            Xunit.Assert.Equal(D_m + 0.2478, B, 10);
        }

        // T024: PT 有効幅 = D + 180mm (K011・D 非依存の継手有効間隔)
        [Xunit.Fact]
        public void T024_EffectiveWidth_PT_EqualsDPlus180()
        {
            double D_m = 0.700;
            double B = SheetPileQuayWall.Core.FrontWall.JointParameters.EffectiveWidth(
                D_m, SheetPileQuayWall.Core.FrontWall.JointType.PT);
            Xunit.Assert.Equal(D_m + 0.180, B, 10);
        }

        // ── FromCode / ToCode ──────────────────────────────────────────────

        // T025: FromCode 正常系 — 全継手コードを往復変換できること
        [Xunit.Fact]
        public void T025_FromCode_AllValidCodes_RoundTrip()
        {
            string[] codes = { "LT65", "LT75", "LT100", "PP", "PT" };
            foreach (string code in codes)
            {
                SheetPileQuayWall.Core.FrontWall.JointType jt =
                    SheetPileQuayWall.Core.FrontWall.JointParameters.FromCode(code);
                string back = SheetPileQuayWall.Core.FrontWall.JointParameters.ToCode(jt);
                Xunit.Assert.Equal(code, back);
            }
        }

        // T026: FromCode("LT65") → JointType.LT65
        [Xunit.Fact]
        public void T026_FromCode_LT65_ReturnsLT65()
        {
            Xunit.Assert.Equal(
                SheetPileQuayWall.Core.FrontWall.JointType.LT65,
                SheetPileQuayWall.Core.FrontWall.JointParameters.FromCode("LT65"));
        }

        // T027: ToCode(JointType.LT100) → "LT100"
        [Xunit.Fact]
        public void T027_ToCode_LT100_ReturnsLT100String()
        {
            Xunit.Assert.Equal(
                "LT100",
                SheetPileQuayWall.Core.FrontWall.JointParameters.ToCode(
                    SheetPileQuayWall.Core.FrontWall.JointType.LT100));
        }

        // T028: 不明コードは ArgumentException をスローすること
        [Xunit.Fact]
        public void T028_FromCode_InvalidCode_ThrowsArgumentException()
        {
            Xunit.Assert.Throws<System.ArgumentException>(
                () => SheetPileQuayWall.Core.FrontWall.JointParameters.FromCode("INVALID"));
        }
    }
}
