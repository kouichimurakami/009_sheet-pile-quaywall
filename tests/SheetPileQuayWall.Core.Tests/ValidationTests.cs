// パラメータ整合性チェックのテスト (CLAUDE.PRIVATE.md §6-5)。
// 違反時はエラーを返し、Compute は例外で停止して再生成しないこと。

namespace SheetPileQuayWall.Core.Tests
{
    public class ValidationTests
    {
        private static SheetPileQuayWall.Core.TieRod.TieRodParameters Sample()
        {
            return new SheetPileQuayWall.Core.TieRod.TieRodParameters();
        }

        [Xunit.Fact]
        public void 既定パラメータは検査を通過する()
        {
            System.Collections.Generic.IReadOnlyList<string> errors = Sample().Validate();
            Xunit.Assert.Empty(errors);
        }

        [Xunit.Fact]
        public void 規格外の径を拒否する()
        {
            SheetPileQuayWall.Core.TieRod.TieRodParameters p = Sample();
            p.RodDiameter = 0.047;

            Xunit.Assert.Contains(p.Validate(), e => e.Contains("カタログ規格径"));
        }

        [Xunit.Fact]
        public void ミリメートル値の混入を拒否する()
        {
            // 単位はメートル統一。48 (mm) を入れた場合を検出する。
            SheetPileQuayWall.Core.TieRod.TieRodParameters p = Sample();
            p.RodDiameter = 48.0;

            Xunit.Assert.Contains(p.Validate(), e => e.Contains("カタログ規格径"));
        }

        [Xunit.Fact]
        public void 延長にミリメートル値を入れた場合を拒否する()
        {
            SheetPileQuayWall.Core.TieRod.TieRodParameters p = Sample();
            p.SpanLength = 10000.0;

            Xunit.Assert.Contains(p.Validate(), e => e.Contains("法線直角方向延長"));
        }

        [Xunit.Fact]
        public void 腹起し高さゼロを拒否する()
        {
            // 鋼管矢板を半割にして腹起しを設置する設計のため h = 0 は成立しない。
            SheetPileQuayWall.Core.TieRod.TieRodParameters p = Sample();
            p.WalingHeight = 0.0;

            Xunit.Assert.Contains(p.Validate(), e => e.Contains("0 を許容しません"));
        }

        [Xunit.Fact]
        public void 腹起しが半割部に収まらない場合を拒否する()
        {
            SheetPileQuayWall.Core.TieRod.TieRodParameters p = Sample();
            p.PileDiameter = 0.800;
            p.WalingHeight = 0.900;

            Xunit.Assert.Contains(p.Validate(), e => e.Contains("半割部に収まりません"));
        }

        [Xunit.Fact]
        public void 矢板ピッチが矢板径より小さい場合を拒否する()
        {
            // ピッチ 0.8 m に径 1.0 m の矢板は物理的に重なる。
            SheetPileQuayWall.Core.TieRod.TieRodParameters p = Sample();
            p.PileDiameter = 1.000;
            p.PilePitch = 0.800;
            p.TieSpacing = 2.400;   // 0.8 × 3 = 整数倍は満たす

            Xunit.Assert.Contains(p.Validate(), e => e.Contains("重なります"));
        }

        [Xunit.Fact]
        public void 表内の径でナット高さが表値と異なる場合を拒否する()
        {
            // φ55 の表値は 0.060。既定の 0.055 のままでは積算根拠が崩れる。
            SheetPileQuayWall.Core.TieRod.TieRodParameters p = Sample();
            p.RodDiameter = 0.055;

            Xunit.Assert.Contains(p.Validate(), e => e.Contains("積算基準表により"));
        }

        [Xunit.Fact]
        public void ApplyStandardNutHeightが表値を設定する()
        {
            SheetPileQuayWall.Core.TieRod.TieRodParameters p = Sample();
            p.RodDiameter = 0.055;

            Xunit.Assert.True(p.ApplyStandardNutHeight());
            Xunit.Assert.Equal(0.060, p.NutHeight, 0.0001);
            Xunit.Assert.Equal(0.060, p.AdjustLength, 0.0001);
            Xunit.Assert.Empty(p.Validate());
        }

        [Xunit.Fact]
        public void 表に無い径ではApplyStandardNutHeightは何も変更しない()
        {
            SheetPileQuayWall.Core.TieRod.TieRodParameters p = Sample();
            p.RodDiameter = 0.090;
            p.NutHeight = 0.100;
            p.AdjustLength = 0.100;

            Xunit.Assert.False(p.ApplyStandardNutHeight());
            Xunit.Assert.Equal(0.100, p.NutHeight, 0.0001);
            Xunit.Assert.Empty(p.Validate());
        }

        [Xunit.Theory]
        [Xunit.InlineData(1.200, 2.400)]   // 2 倍
        [Xunit.InlineData(1.200, 1.200)]   // 1 倍
        [Xunit.InlineData(1.000, 5.000)]   // 5 倍
        public void 取付間隔が矢板ピッチの整数倍なら通過する(double pitch, double spacing)
        {
            SheetPileQuayWall.Core.TieRod.TieRodParameters p = Sample();
            p.PilePitch = pitch;
            p.TieSpacing = spacing;

            Xunit.Assert.DoesNotContain(p.Validate(), e => e.Contains("整数倍"));
        }

        [Xunit.Theory]
        [Xunit.InlineData(1.200, 2.500)]
        [Xunit.InlineData(1.200, 1.800)]
        public void 矢板中央を横断できない取付間隔を拒否する(double pitch, double spacing)
        {
            SheetPileQuayWall.Core.TieRod.TieRodParameters p = Sample();
            p.PilePitch = pitch;
            p.TieSpacing = spacing;

            Xunit.Assert.Contains(p.Validate(), e => e.Contains("整数倍"));
        }

        [Xunit.Theory]
        [Xunit.InlineData(SheetPileQuayWall.Core.TieRod.SteelGrade.HT740)]
        [Xunit.InlineData(SheetPileQuayWall.Core.TieRod.SteelGrade.SS490)]
        public void 新基準で対象外の鋼種を拒否する(SheetPileQuayWall.Core.TieRod.SteelGrade grade)
        {
            SheetPileQuayWall.Core.TieRod.TieRodParameters p = Sample();
            p.Code = SheetPileQuayWall.Core.TieRod.DesignCode.PartialFactor;
            p.Grade = grade;

            Xunit.Assert.Contains(p.Validate(), e => e.Contains("部分係数法"));
        }

        [Xunit.Theory]
        [Xunit.InlineData(SheetPileQuayWall.Core.TieRod.SteelGrade.HT740)]
        [Xunit.InlineData(SheetPileQuayWall.Core.TieRod.SteelGrade.SS490)]
        public void 旧基準では全鋼種を許容する(SheetPileQuayWall.Core.TieRod.SteelGrade grade)
        {
            SheetPileQuayWall.Core.TieRod.TieRodParameters p = Sample();
            p.Code = SheetPileQuayWall.Core.TieRod.DesignCode.Allowable;
            p.Grade = grade;

            Xunit.Assert.Empty(p.Validate());
        }

        [Xunit.Theory]
        [Xunit.InlineData(0)]
        [Xunit.InlineData(201)]
        public void 組数の範囲外を拒否する(int count)
        {
            SheetPileQuayWall.Core.TieRod.TieRodParameters p = Sample();
            p.TieCount = count;

            Xunit.Assert.Contains(p.Validate(), e => e.Contains("タイロッド組数"));
        }

        [Xunit.Fact]
        public void 違反があるとき計算は例外で停止する()
        {
            SheetPileQuayWall.Core.TieRod.TieRodParameters p = Sample();
            p.WalingHeight = 0.0;

            System.ArgumentException ex = Xunit.Assert.Throws<System.ArgumentException>(
                () => SheetPileQuayWall.Core.TieRod.TieRodCalculator.Compute(p));
            Xunit.Assert.Contains("パラメータ整合性チェック", ex.Message);
        }

        [Xunit.Fact]
        public void 英日パラメータ名対応が全入力を網羅する()
        {
            // CLAUDE.PRIVATE.md §2.1: 英語パラメータ名と日本語説明の対応を内部に保持する。
            Xunit.Assert.Equal(18, SheetPileQuayWall.Core.TieRod.TieRodParameters.DisplayNames.Count);
            Xunit.Assert.Equal(
                "タイロッド径", SheetPileQuayWall.Core.TieRod.TieRodParameters.DisplayNames["rod_diameter"]);
            Xunit.Assert.Equal(
                "タイロッド軸心標高", SheetPileQuayWall.Core.TieRod.TieRodParameters.DisplayNames["tie_elevation"]);
        }
    }
}
