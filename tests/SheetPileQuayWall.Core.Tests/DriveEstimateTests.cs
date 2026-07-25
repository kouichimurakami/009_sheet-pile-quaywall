// T101〜T905: DriveEstimate の単体テスト
// 検証基準: 港湾土木請負工事積算基準 令和7年度改訂版 3-4.5節
// AutoCAD 非依存のため xUnit で直接実行可能

namespace SheetPileQuayWall.Core.Tests
{
    public class DriveEstimateTests
    {
        // ── GetSb (打撃速度テーブル参照) ──────────────────────────────────

        // T101: φ800mm, N_avg=20 → Sb=1.09 (基準 3-4.5-16 カタログ値との一致)
        [Xunit.Fact]
        public void T101_GetSb_D800_N20_MatchesCatalog()
        {
            double sb = SheetPileQuayWall.Core.FrontWall.DriveEstimate.GetSb(800, 20);
            Xunit.Assert.Equal(1.09, sb, 2);
        }

        // T102: φ500mm, N_avg=10 → Sb=2.34 (最小径・最小N列)
        [Xunit.Fact]
        public void T102_GetSb_D500_N10_MinDiaMinN()
        {
            double sb = SheetPileQuayWall.Core.FrontWall.DriveEstimate.GetSb(500, 10);
            Xunit.Assert.Equal(2.34, sb, 2);
        }

        // T103: φ1500mm, N_avg=50 → Sb=0.31 (最大径・最大N列)
        [Xunit.Fact]
        public void T103_GetSb_D1500_N50_MaxDiaMaxN()
        {
            double sb = SheetPileQuayWall.Core.FrontWall.DriveEstimate.GetSb(1500, 50);
            Xunit.Assert.Equal(0.31, sb, 2);
        }

        // T104: φ1600mm → φ1500mm にクランプされること
        [Xunit.Fact]
        public void T104_GetSb_D1600_ClampsToD1500()
        {
            double sbClamped = SheetPileQuayWall.Core.FrontWall.DriveEstimate.GetSb(1600, 20);
            double sbD1500   = SheetPileQuayWall.Core.FrontWall.DriveEstimate.GetSb(1500, 20);
            Xunit.Assert.Equal(sbD1500, sbClamped, 2);
        }

        // T105: φ800mm, N_avg=25 → N≤30 列 (Sb=0.70)。N≤20 列 (1.09) ではないこと
        [Xunit.Fact]
        public void T105_GetSb_D800_N25_UsesN30Column()
        {
            double sb = SheetPileQuayWall.Core.FrontWall.DriveEstimate.GetSb(800, 25);
            Xunit.Assert.Equal(0.70, sb, 2);
        }

        // T106: φ1000mm, N_avg=30 → Sb=0.62 (中間径・境界N値)
        [Xunit.Fact]
        public void T106_GetSb_D1000_N30_BoundaryN()
        {
            double sb = SheetPileQuayWall.Core.FrontWall.DriveEstimate.GetSb(1000, 30);
            Xunit.Assert.Equal(0.62, sb, 2);
        }

        // ── GetWeldTime (溶接時間テーブル参照) ────────────────────────────

        // T201: φ800mm, t=12mm → 33 min (φ800以上, 溶接機2台使用時, 基準 3-4.5-17)
        [Xunit.Fact]
        public void T201_GetWeldTime_D800_T12_MatchesCatalog()
        {
            int wt = SheetPileQuayWall.Core.FrontWall.DriveEstimate.GetWeldTime(800, 12);
            Xunit.Assert.Equal(33, wt);
        }

        // T202: φ500mm, t=9mm → 20 min (φ800未満, 溶接機1台)
        [Xunit.Fact]
        public void T202_GetWeldTime_D500_T9_MatchesCatalog()
        {
            int wt = SheetPileQuayWall.Core.FrontWall.DriveEstimate.GetWeldTime(500, 9);
            Xunit.Assert.Equal(20, wt);
        }

        // T203: φ1500mm, t=22mm → 146 min (最大径・最大板厚)
        [Xunit.Fact]
        public void T203_GetWeldTime_D1500_T22_MatchesCatalog()
        {
            int wt = SheetPileQuayWall.Core.FrontWall.DriveEstimate.GetWeldTime(1500, 22);
            Xunit.Assert.Equal(146, wt);
        }

        // T204: t=25mm → t=22mm にクランプされること
        [Xunit.Fact]
        public void T204_GetWeldTime_T25_ClampsToT22()
        {
            int wt25 = SheetPileQuayWall.Core.FrontWall.DriveEstimate.GetWeldTime(800, 25);
            int wt22 = SheetPileQuayWall.Core.FrontWall.DriveEstimate.GetWeldTime(800, 22);
            Xunit.Assert.Equal(wt22, wt25);
        }

        // T205: φ800mm(2台) と φ700mm(1台) で溶接時間が異なること
        [Xunit.Fact]
        public void T205_GetWeldTime_D800VsD700_T8_ValuesAreDifferent()
        {
            int wt800 = SheetPileQuayWall.Core.FrontWall.DriveEstimate.GetWeldTime(800, 8);
            int wt700 = SheetPileQuayWall.Core.FrontWall.DriveEstimate.GetWeldTime(700, 8);
            Xunit.Assert.Equal(20, wt800); // φ800以上, 溶接機2台
            Xunit.Assert.Equal(27, wt700); // φ700, 溶接機1台
        }

        // ── CalcR (貫入抵抗値) ─────────────────────────────────────────────

        // T301: φ800mm, L_pen=15m, N_tip=30, N_avg=20 — 基準式との一致
        // R = 300×N_tip×Ap + 2×N_avg×L_pen×As (出典: 3-4.5-14)
        [Xunit.Fact]
        public void T301_CalcR_D800_L15_Ntip30_Navg20_MatchesFormula()
        {
            double D_m = 0.800, L_pen = 15.0;
            double Ap  = System.Math.PI / 4.0 * D_m * D_m;
            double As  = System.Math.PI * D_m;
            double expected =
                System.Math.Round(300.0 * 30 * Ap + 2.0 * 20.0 * L_pen * As, 1);
            double actual =
                SheetPileQuayWall.Core.FrontWall.DriveEstimate.CalcR(D_m, L_pen, 30, 20.0);
            Xunit.Assert.Equal(expected, actual, 1);
        }

        // T302: 最小N値 → 正の値が返ること
        [Xunit.Fact]
        public void T302_CalcR_MinN_IsPositive()
        {
            double R = SheetPileQuayWall.Core.FrontWall.DriveEstimate.CalcR(0.500, 5.0, 1, 1.0);
            Xunit.Assert.True(R > 0.0);
        }

        // T303: φ1000mm, L_pen=20m, N_tip=50, N_avg=50 — 大径・高N値で式が成立すること
        [Xunit.Fact]
        public void T303_CalcR_D1000_L20_N50_N50_MatchesFormula()
        {
            double D_m = 1.000, L_pen = 20.0;
            double Ap  = System.Math.PI / 4.0 * D_m * D_m;
            double As  = System.Math.PI * D_m;
            double expected =
                System.Math.Round(300.0 * 50 * Ap + 2.0 * 50.0 * L_pen * As, 1);
            double actual =
                SheetPileQuayWall.Core.FrontWall.DriveEstimate.CalcR(D_m, L_pen, 50, 50.0);
            Xunit.Assert.Equal(expected, actual, 1);
        }

        // ── GetHammerClass (ハンマ規格選定) ───────────────────────────────

        // T401: 小質量・小R → 最小ランク "4～4.5 t"
        [Xunit.Fact]
        public void T401_GetHammerClass_SmallMassSmallR_FirstTier()
        {
            string h = SheetPileQuayWall.Core.FrontWall.DriveEstimate.GetHammerClass(3.00, 4000);
            Xunit.Assert.Equal("4～4.5 t", h);
        }

        // T402: 質量で第1ランク超え(5t) → "6.5 t"
        [Xunit.Fact]
        public void T402_GetHammerClass_MassExceedsFirstTier_SecondTier()
        {
            string h = SheetPileQuayWall.Core.FrontWall.DriveEstimate.GetHammerClass(5.00, 6000);
            Xunit.Assert.Equal("6.5 t", h);
        }

        // T403: Rで第1ランク超え(R=6000 > 5700)、質量はOK → "6.5 t"
        [Xunit.Fact]
        public void T403_GetHammerClass_RExceedsFirstTier_SecondTier()
        {
            string h = SheetPileQuayWall.Core.FrontWall.DriveEstimate.GetHammerClass(1.00, 6000);
            Xunit.Assert.Equal("6.5 t", h);
        }

        // T404: 境界値ぴったり(mass=4.56t, R=5700kN) → "4～4.5 t"
        [Xunit.Fact]
        public void T404_GetHammerClass_ExactBoundaryValues_FirstTier()
        {
            string h = SheetPileQuayWall.Core.FrontWall.DriveEstimate.GetHammerClass(4.56, 5700.0);
            Xunit.Assert.Equal("4～4.5 t", h);
        }

        // T405: テーブル全ランク超え → "15.0 t 超（別途検討）"
        [Xunit.Fact]
        public void T405_GetHammerClass_BeyondAllTiers_ReturnsExtraLargeLabel()
        {
            string h = SheetPileQuayWall.Core.FrontWall.DriveEstimate.GetHammerClass(30.0, 40000);
            Xunit.Assert.Equal("15.0 t 超（別途検討）", h);
        }

        // ── CalcTp (準備時間) ─────────────────────────────────────────────

        // T501: 陸上, n=0 (単杭) → 5×0+14 = 14.0 min
        [Xunit.Fact]
        public void T501_CalcTp_Onshore_SinglePile_14min()
        {
            double tp = SheetPileQuayWall.Core.FrontWall.DriveEstimate.CalcTp(
                SheetPileQuayWall.Core.FrontWall.ConstructionSite.Onshore, 0);
            Xunit.Assert.Equal(14.0, tp, 1);
        }

        // T502: 陸上, n=2 (継杭2回) → 5×2+14 = 24.0 min
        [Xunit.Fact]
        public void T502_CalcTp_Onshore_TwoSplices_24min()
        {
            double tp = SheetPileQuayWall.Core.FrontWall.DriveEstimate.CalcTp(
                SheetPileQuayWall.Core.FrontWall.ConstructionSite.Onshore, 2);
            Xunit.Assert.Equal(24.0, tp, 1);
        }

        // T503: 海上, n=0 (単杭) → 5×0+16 = 16.0 min
        [Xunit.Fact]
        public void T503_CalcTp_Offshore_SinglePile_16min()
        {
            double tp = SheetPileQuayWall.Core.FrontWall.DriveEstimate.CalcTp(
                SheetPileQuayWall.Core.FrontWall.ConstructionSite.Offshore, 0);
            Xunit.Assert.Equal(16.0, tp, 1);
        }

        // T504: 海上, n=1 (継杭1回) → 5×1+16 = 21.0 min
        [Xunit.Fact]
        public void T504_CalcTp_Offshore_OneSplice_21min()
        {
            double tp = SheetPileQuayWall.Core.FrontWall.DriveEstimate.CalcTp(
                SheetPileQuayWall.Core.FrontWall.ConstructionSite.Offshore, 1);
            Xunit.Assert.Equal(21.0, tp, 1);
        }

        // ── CalcTb (打撃時間, 小数1位切上げ) ─────────────────────────────

        // T601: L_pen=15m, Sb=1.09 → ceil(15/1.09×10)/10 = 13.8 min
        [Xunit.Fact]
        public void T601_CalcTb_L15_Sb109_CeilingApplied_138min()
        {
            double tb = SheetPileQuayWall.Core.FrontWall.DriveEstimate.CalcTb(15.0, 1.09);
            Xunit.Assert.Equal(13.8, tb, 1);
        }

        // T602: L_pen=10m, Sb=0.70 → ceil(10/0.70×10)/10 = 14.3 min
        [Xunit.Fact]
        public void T602_CalcTb_L10_Sb070_143min()
        {
            double tb = SheetPileQuayWall.Core.FrontWall.DriveEstimate.CalcTb(10.0, 0.70);
            Xunit.Assert.Equal(14.3, tb, 1);
        }

        // T603: 割り切れる場合 (L=10m, Sb=2.00) → 切上げ不要 = 5.0 min
        [Xunit.Fact]
        public void T603_CalcTb_ExactDivision_NoCeiling_5min()
        {
            double tb = SheetPileQuayWall.Core.FrontWall.DriveEstimate.CalcTb(10.0, 2.00);
            Xunit.Assert.Equal(5.0, tb, 1);
        }

        // ── CalcTw (溶接時間) ─────────────────────────────────────────────

        // T701: n_joints=0 (単杭) → 0.0 min
        [Xunit.Fact]
        public void T701_CalcTw_NoJoints_Zero()
        {
            double tw = SheetPileQuayWall.Core.FrontWall.DriveEstimate.CalcTw(800, 12, 0);
            Xunit.Assert.Equal(0.0, tw, 1);
        }

        // T702: φ800mm, t=12mm, 1継手 → GetWeldTime(800,12)×1 = 33 min
        [Xunit.Fact]
        public void T702_CalcTw_D800_T12_OneJoint_33min()
        {
            double tw = SheetPileQuayWall.Core.FrontWall.DriveEstimate.CalcTw(800, 12, 1);
            Xunit.Assert.Equal(33.0, tw, 1);
        }

        // T703: φ800mm, t=12mm, 2継手 → 33×2 = 66 min
        [Xunit.Fact]
        public void T703_CalcTw_D800_T12_TwoJoints_66min()
        {
            double tw = SheetPileQuayWall.Core.FrontWall.DriveEstimate.CalcTw(800, 12, 2);
            Xunit.Assert.Equal(66.0, tw, 1);
        }

        // ── CalcQ (1日当り打設本数, 小数2位四捨五入) ────────────────────

        // T801: 陸上, Tc=30min, 補正なし, N=100 → Q=14.40 本/日
        // T=8h, ei=0.90, E1=E2=E3=0 → 8×60/30×0.90 = 14.40
        [Xunit.Fact]
        public void T801_CalcQ_Onshore_Tc30_NoCorrections_Q1440()
        {
            double Q = SheetPileQuayWall.Core.FrontWall.DriveEstimate.CalcQ(
                SheetPileQuayWall.Core.FrontWall.ConstructionSite.Onshore, 30.0,
                SheetPileQuayWall.Core.FrontWall.SeaCondition.Normal,
                SheetPileQuayWall.Core.FrontWall.ObstacleStatus.None, 100);
            Xunit.Assert.Equal(14.40, Q, 2);
        }

        // T802: 海上, Tc=50min, 補正なし, N=100 → Q=3.60 本/日
        // T=6h, ei=0.50 → 6×60/50×0.50 = 3.60
        [Xunit.Fact]
        public void T802_CalcQ_Offshore_Tc50_NoCorrections_Q360()
        {
            double Q = SheetPileQuayWall.Core.FrontWall.DriveEstimate.CalcQ(
                SheetPileQuayWall.Core.FrontWall.ConstructionSite.Offshore, 50.0,
                SheetPileQuayWall.Core.FrontWall.SeaCondition.Normal,
                SheetPileQuayWall.Core.FrontWall.ObstacleStatus.None, 100);
            Xunit.Assert.Equal(3.60, Q, 2);
        }

        // T803: 海上, Tc=50min, 海象悪い+障害あり+規模小(N=30<50) → Q=2.52 本/日
        // ei=0.50, E1=-0.05, E2=-0.05, E3=-0.05 → 7.2×0.35 = 2.52
        [Xunit.Fact]
        public void T803_CalcQ_Offshore_AllNegativeCorrections_Q252()
        {
            double Q = SheetPileQuayWall.Core.FrontWall.DriveEstimate.CalcQ(
                SheetPileQuayWall.Core.FrontWall.ConstructionSite.Offshore, 50.0,
                SheetPileQuayWall.Core.FrontWall.SeaCondition.Severe,
                SheetPileQuayWall.Core.FrontWall.ObstacleStatus.Exists, 30);
            Xunit.Assert.Equal(2.52, Q, 2);
        }

        // T804: 陸上, Tc=30min, 本数=10(<50) → E3=-0.05 が適用されQ=13.60 本/日
        // 16×(0.90-0.05) = 13.60
        [Xunit.Fact]
        public void T804_CalcQ_Onshore_SmallScale_E3Applied_Q1360()
        {
            double Q = SheetPileQuayWall.Core.FrontWall.DriveEstimate.CalcQ(
                SheetPileQuayWall.Core.FrontWall.ConstructionSite.Onshore, 30.0,
                SheetPileQuayWall.Core.FrontWall.SeaCondition.Normal,
                SheetPileQuayWall.Core.FrontWall.ObstacleStatus.None, 10);
            Xunit.Assert.Equal(13.60, Q, 2);
        }

        // ── GetLabor (労務編成) ───────────────────────────────────────────

        // T901: 陸上, 継杭なし, φ700 → (世話役1, とび工2, 普通1, 溶接0)
        [Xunit.Fact]
        public void T901_GetLabor_Onshore_NoSplice_D700()
        {
            var (f, r, l, w) = SheetPileQuayWall.Core.FrontWall.DriveEstimate.GetLabor(
                SheetPileQuayWall.Core.FrontWall.ConstructionSite.Onshore, 20.0, false, 700);
            Xunit.Assert.Equal(1, f);
            Xunit.Assert.Equal(2, r);
            Xunit.Assert.Equal(1, l);
            Xunit.Assert.Equal(0, w);
        }

        // T902: 海上, L=15m(<20m), 継杭なし → とび工3人
        [Xunit.Fact]
        public void T902_GetLabor_Offshore_L15_NoSplice_3Riggers()
        {
            var (f, r, l, w) = SheetPileQuayWall.Core.FrontWall.DriveEstimate.GetLabor(
                SheetPileQuayWall.Core.FrontWall.ConstructionSite.Offshore, 15.0, false, 700);
            Xunit.Assert.Equal(1, f);
            Xunit.Assert.Equal(3, r);
            Xunit.Assert.Equal(2, l);
            Xunit.Assert.Equal(0, w);
        }

        // T903: 海上, L=22m(20≤L<25m), 継杭あり, φ700 → とび工4人, 溶接工1人
        [Xunit.Fact]
        public void T903_GetLabor_Offshore_L22_Splice_D700_4Riggers1Welder()
        {
            var (f, r, l, w) = SheetPileQuayWall.Core.FrontWall.DriveEstimate.GetLabor(
                SheetPileQuayWall.Core.FrontWall.ConstructionSite.Offshore, 22.0, true, 700);
            Xunit.Assert.Equal(1, f);
            Xunit.Assert.Equal(4, r);
            Xunit.Assert.Equal(2, l);
            Xunit.Assert.Equal(1, w);
        }

        // T904: 海上, L=30m(≥25m), 継杭あり, φ800 → とび工5人, 溶接工2人
        [Xunit.Fact]
        public void T904_GetLabor_Offshore_L30_Splice_D800_5Riggers2Welders()
        {
            var (f, r, l, w) = SheetPileQuayWall.Core.FrontWall.DriveEstimate.GetLabor(
                SheetPileQuayWall.Core.FrontWall.ConstructionSite.Offshore, 30.0, true, 800);
            Xunit.Assert.Equal(1, f);
            Xunit.Assert.Equal(5, r);
            Xunit.Assert.Equal(2, l);
            Xunit.Assert.Equal(2, w);
        }

        // T905: 陸上, 継杭あり, φ800(≥800) → 溶接工2人
        [Xunit.Fact]
        public void T905_GetLabor_Onshore_Splice_D800_TwoWelders()
        {
            var (f, r, l, w) = SheetPileQuayWall.Core.FrontWall.DriveEstimate.GetLabor(
                SheetPileQuayWall.Core.FrontWall.ConstructionSite.Onshore, 20.0, true, 800);
            Xunit.Assert.Equal(1, f);
            Xunit.Assert.Equal(2, r);
            Xunit.Assert.Equal(1, l);
            Xunit.Assert.Equal(2, w);
        }
    }
}
