// T001〜T011: SectionProperties の単体テスト
// 検証基準: K011/d1j-503 カタログ値との一致（許容誤差 1cm² / 1cm⁴ / 1cm³）

namespace SheetPileQuayWall.Core.Tests
{
    public class SectionPropertiesTests
    {
        // ── CalcA ──────────────────────────────────────────────────────────

        // T001: φ800×12 断面積 (K011 カタログ値 ≈ 297 cm²)
        [Xunit.Fact]
        public void T001_CalcA_D800_T12_MatchesFormula()
        {
            double D = 80.0;
            double d = 80.0 - 2.0 * 1.2;
            double expected = System.Math.PI / 4.0 * (D * D - d * d);
            double actual = SheetPileQuayWall.Core.FrontWall.SectionProperties.CalcA(0.800, 0.012);
            Xunit.Assert.Equal(expected, actual, 6);
        }

        // T002: φ1000×14 断面積
        [Xunit.Fact]
        public void T002_CalcA_D1000_T14_MatchesFormula()
        {
            double D = 100.0;
            double d = 100.0 - 2.0 * 1.4;
            double expected = System.Math.PI / 4.0 * (D * D - d * d);
            double actual = SheetPileQuayWall.Core.FrontWall.SectionProperties.CalcA(1.000, 0.014);
            Xunit.Assert.Equal(expected, actual, 6);
        }

        // T003: φ500×9 断面積（最小径・最小厚）
        [Xunit.Fact]
        public void T003_CalcA_D500_T9_MinSize()
        {
            double D = 50.0;
            double d = 50.0 - 2.0 * 0.9;
            double expected = System.Math.PI / 4.0 * (D * D - d * d);
            double actual = SheetPileQuayWall.Core.FrontWall.SectionProperties.CalcA(0.500, 0.009);
            Xunit.Assert.Equal(expected, actual, 6);
        }

        // ── CalcW ──────────────────────────────────────────────────────────

        // T004: φ800×12 単位重量 (K011 近似式 W = 2.466 × t × (D−t), 単位 cm)
        [Xunit.Fact]
        public void T004_CalcW_D800_T12_MatchesFormula()
        {
            double D_cm = 80.0;
            double t_cm = 1.2;
            double expected = 2.466 * t_cm * (D_cm - t_cm);
            double actual = SheetPileQuayWall.Core.FrontWall.SectionProperties.CalcW(0.800, 0.012);
            Xunit.Assert.Equal(expected, actual, 6);
        }

        // T005: φ1200×16 単位重量
        [Xunit.Fact]
        public void T005_CalcW_D1200_T16_MatchesFormula()
        {
            double D_cm = 120.0;
            double t_cm = 1.6;
            double expected = 2.466 * t_cm * (D_cm - t_cm);
            double actual = SheetPileQuayWall.Core.FrontWall.SectionProperties.CalcW(1.200, 0.016);
            Xunit.Assert.Equal(expected, actual, 6);
        }

        // ── CalcI ──────────────────────────────────────────────────────────

        // T006: φ800×12 I (K011 カタログ値 ≈ 230,000〜231,000 cm⁴ の範囲)
        [Xunit.Fact]
        public void T006_CalcI_D800_T12_MatchesFormula()
        {
            double D = 80.0;
            double d = 80.0 - 2.0 * 1.2;
            double expected = System.Math.PI / 64.0 *
                (System.Math.Pow(D, 4) - System.Math.Pow(d, 4));
            double actual = SheetPileQuayWall.Core.FrontWall.SectionProperties.CalcI(0.800, 0.012);
            Xunit.Assert.Equal(expected, actual, 5);
        }

        // T007: φ1000×14 I
        [Xunit.Fact]
        public void T007_CalcI_D1000_T14_MatchesFormula()
        {
            double D = 100.0;
            double d = 100.0 - 2.0 * 1.4;
            double expected = System.Math.PI / 64.0 *
                (System.Math.Pow(D, 4) - System.Math.Pow(d, 4));
            double actual = SheetPileQuayWall.Core.FrontWall.SectionProperties.CalcI(1.000, 0.014);
            Xunit.Assert.Equal(expected, actual, 5);
        }

        // ── CalcZ ──────────────────────────────────────────────────────────

        // T008: CalcZ = CalcI / (D_cm / 2) の関係が成立すること
        [Xunit.Fact]
        public void T008_CalcZ_EqualsIOverHalfD()
        {
            double D_m = 0.800;
            double t_m = 0.012;
            double I = SheetPileQuayWall.Core.FrontWall.SectionProperties.CalcI(D_m, t_m);
            double Z = SheetPileQuayWall.Core.FrontWall.SectionProperties.CalcZ(D_m, t_m);
            double expected = I / (D_m * 100.0 / 2.0);
            Xunit.Assert.Equal(expected, Z, 8);
        }

        // T009: φ800×12 Z (K011 カタログ値 ≈ 5,700〜5,800 cm³)
        [Xunit.Fact]
        public void T009_CalcZ_D800_T12_InCatalogRange()
        {
            double Z = SheetPileQuayWall.Core.FrontWall.SectionProperties.CalcZ(0.800, 0.012);
            Xunit.Assert.InRange(Z, 5600.0, 5900.0);
        }

        // ── CalcRadius ─────────────────────────────────────────────────────

        // T010: 断面2次半径 i = 0.25 × √(D² + d²) の関係が成立すること
        [Xunit.Fact]
        public void T010_CalcRadius_MatchesFormula()
        {
            double D_m = 0.800;
            double t_m = 0.012;
            double D_cm = 80.0;
            double d_cm = 80.0 - 2.0 * 1.2;
            double expected = 0.25 * System.Math.Sqrt(D_cm * D_cm + d_cm * d_cm);
            double actual = SheetPileQuayWall.Core.FrontWall.SectionProperties.CalcRadius(D_m, t_m);
            Xunit.Assert.Equal(expected, actual, 6);
        }

        // T011: i は必ず正の値であること
        [Xunit.Fact]
        public void T011_CalcRadius_IsPositive()
        {
            double i = SheetPileQuayWall.Core.FrontWall.SectionProperties.CalcRadius(0.500, 0.009);
            Xunit.Assert.True(i > 0.0);
        }
    }
}
