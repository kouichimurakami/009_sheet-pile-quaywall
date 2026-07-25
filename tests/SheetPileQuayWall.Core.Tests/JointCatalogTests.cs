// JointCatalog の単体テスト
// 検証基準: JFE d1j-503 表4 の寸法・単位質量と一致 (許容誤差 1e-9 m / 0.01 kg/m)

namespace SheetPileQuayWall.Core.Tests
{
    public class JointCatalogTests
    {
        // ── Form ────────────────────────────────────────────────────────────

        [Xunit.Fact]
        public void TC01_Form_LT_AllMapToLT()
        {
            Xunit.Assert.Equal(SheetPileQuayWall.Core.FrontWall.JointForm.LT,
                SheetPileQuayWall.Core.FrontWall.JointCatalog.Form(SheetPileQuayWall.Core.FrontWall.JointType.LT65));
            Xunit.Assert.Equal(SheetPileQuayWall.Core.FrontWall.JointForm.LT,
                SheetPileQuayWall.Core.FrontWall.JointCatalog.Form(SheetPileQuayWall.Core.FrontWall.JointType.LT100));
        }

        [Xunit.Fact]
        public void TC02_Form_PP_PT()
        {
            Xunit.Assert.Equal(SheetPileQuayWall.Core.FrontWall.JointForm.PP,
                SheetPileQuayWall.Core.FrontWall.JointCatalog.Form(SheetPileQuayWall.Core.FrontWall.JointType.PP));
            Xunit.Assert.Equal(SheetPileQuayWall.Core.FrontWall.JointForm.PT,
                SheetPileQuayWall.Core.FrontWall.JointCatalog.Form(SheetPileQuayWall.Core.FrontWall.JointType.PT));
        }

        // ── Angle (山形鋼) ──────────────────────────────────────────────────

        [Xunit.Fact]
        public void TC03_Angle_LT65_MatchesCatalog()
        {
            SheetPileQuayWall.Core.FrontWall.AngleSteel? a =
                SheetPileQuayWall.Core.FrontWall.JointCatalog.Angle(SheetPileQuayWall.Core.FrontWall.JointType.LT65);
            Xunit.Assert.NotNull(a);
            Xunit.Assert.Equal(0.065, a!.A_m, 9);
            Xunit.Assert.Equal(0.065, a.C_m, 9);
            Xunit.Assert.Equal(0.008, a.T_m, 9);
            Xunit.Assert.Equal(15.3, a.MassKgPerM, 2);
        }

        [Xunit.Fact]
        public void TC04_Angle_LT100_Unequal()
        {
            SheetPileQuayWall.Core.FrontWall.AngleSteel? a =
                SheetPileQuayWall.Core.FrontWall.JointCatalog.Angle(SheetPileQuayWall.Core.FrontWall.JointType.LT100);
            Xunit.Assert.NotNull(a);
            Xunit.Assert.Equal(0.100, a!.A_m, 9);
            Xunit.Assert.Equal(0.075, a.C_m, 9);
            Xunit.Assert.Equal(26.0, a.MassKgPerM, 2);
        }

        [Xunit.Fact]
        public void TC05_Angle_PP_IsNull()
        {
            Xunit.Assert.Null(
                SheetPileQuayWall.Core.FrontWall.JointCatalog.Angle(SheetPileQuayWall.Core.FrontWall.JointType.PP));
        }

        // ── Tee (T形鋼) ─────────────────────────────────────────────────────

        [Xunit.Fact]
        public void TC06_Tee_LT_Common_T125()
        {
            SheetPileQuayWall.Core.FrontWall.TeeSteel? t =
                SheetPileQuayWall.Core.FrontWall.JointCatalog.Tee(SheetPileQuayWall.Core.FrontWall.JointType.LT75);
            Xunit.Assert.NotNull(t);
            Xunit.Assert.Equal(0.039, t!.H_m, 9);
            Xunit.Assert.Equal(0.125, t.B_m, 9);
            Xunit.Assert.Equal(0.012, t.T1_m, 9);
            Xunit.Assert.Equal(0.009, t.T2_m, 9);
            Xunit.Assert.Equal(12.7, t.MassKgPerM, 2);
        }

        [Xunit.Fact]
        public void TC07_Tee_PT_T76()
        {
            SheetPileQuayWall.Core.FrontWall.TeeSteel? t =
                SheetPileQuayWall.Core.FrontWall.JointCatalog.Tee(SheetPileQuayWall.Core.FrontWall.JointType.PT);
            Xunit.Assert.NotNull(t);
            Xunit.Assert.Equal(0.076, t!.H_m, 9);
            Xunit.Assert.Equal(0.085, t.B_m, 9);
            Xunit.Assert.Equal(10.9, t.MassKgPerM, 2);
        }

        [Xunit.Fact]
        public void TC08_Tee_PP_IsNull()
        {
            Xunit.Assert.Null(
                SheetPileQuayWall.Core.FrontWall.JointCatalog.Tee(SheetPileQuayWall.Core.FrontWall.JointType.PP));
        }

        // ── Pipe (鋼管継手) ─────────────────────────────────────────────────

        [Xunit.Fact]
        public void TC09_Pipe_PP_Phi165()
        {
            SheetPileQuayWall.Core.FrontWall.PipeJoint? p =
                SheetPileQuayWall.Core.FrontWall.JointCatalog.Pipe(SheetPileQuayWall.Core.FrontWall.JointType.PP);
            Xunit.Assert.NotNull(p);
            Xunit.Assert.Equal(0.1652, p!.OD_m, 9);
            Xunit.Assert.Equal(0.009, p.T_m, 9);
            Xunit.Assert.Equal(34.7, p.MassKgPerM, 2);
        }

        [Xunit.Fact]
        public void TC10_Pipe_LT_IsNull()
        {
            Xunit.Assert.Null(
                SheetPileQuayWall.Core.FrontWall.JointCatalog.Pipe(SheetPileQuayWall.Core.FrontWall.JointType.LT65));
        }

        // ── JointMassPerM (1組合計) ─────────────────────────────────────────

        [Xunit.Fact]
        public void TC11_Mass_LT75_AngleePlusTee()
        {
            // L-75 (19.9) + T-125 (12.7) = 32.6
            Xunit.Assert.Equal(32.6,
                SheetPileQuayWall.Core.FrontWall.JointCatalog.JointMassPerM(
                    SheetPileQuayWall.Core.FrontWall.JointType.LT75), 2);
        }

        [Xunit.Fact]
        public void TC12_Mass_PP_PipeOnly()
        {
            // φ165.2×9 (34.7) のみ
            Xunit.Assert.Equal(34.7,
                SheetPileQuayWall.Core.FrontWall.JointCatalog.JointMassPerM(
                    SheetPileQuayWall.Core.FrontWall.JointType.PP), 2);
        }

        [Xunit.Fact]
        public void TC13_Mass_PT_PipePlusTee()
        {
            // φ165.2×9 (34.7) + T-76×85×9×9 (10.9) = 45.6
            Xunit.Assert.Equal(45.6,
                SheetPileQuayWall.Core.FrontWall.JointCatalog.JointMassPerM(
                    SheetPileQuayWall.Core.FrontWall.JointType.PT), 2);
        }
    }
}
