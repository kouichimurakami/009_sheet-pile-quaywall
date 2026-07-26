// T1195〜T1214: AnchorDriveEstimate の単体テスト
// 検証基準: 港湾土木請負工事積算基準 令和7年度改訂版 4節 本体工 4.6(3-4.6-9〜17)、陸上打設
// 期待値は基準の式・DriveEstimate の共有テーブルから独立に手計算した値をハードコードする。
// AutoCAD 非依存のため xUnit で直接実行可能

namespace SheetPileQuayWall.Core.Tests
{
    public class AnchorDriveEstimateTests
    {
        // ── 打撃時間 Tb(3-4.6-14)──────────────────────────────────────────

        // T1195: 直杭(K=1.0)。D=800mm,N_avg=20 → Sb=1.09(DriveEstimate.GetSb 既存値)
        //   Tb = 1.0 × 15 / 1.09 = 13.76146... → 小数1位切上げ 13.8
        [Xunit.Fact]
        public void T1195_CalcTb_StraightPile_MatchesFormula()
        {
            double sb = SheetPileQuayWall.Core.FrontWall.DriveEstimate.GetSb(800, 20);
            Xunit.Assert.Equal(1.09, sb, 2);

            double tb = SheetPileQuayWall.Core.AnchorPile.AnchorDriveEstimate.CalcTb(15.0, sb, 0.0);
            Xunit.Assert.Equal(13.8, tb, 2);
        }

        // T1196: 斜杭(K=1.2)。同じ Sb・根入れ長で K のみ変える
        //   Tb = 1.2 × 15 / 1.09 = 16.51376... → 16.6
        [Xunit.Fact]
        public void T1196_CalcTb_InclinedPile_AppliesK12()
        {
            double sb = SheetPileQuayWall.Core.FrontWall.DriveEstimate.GetSb(800, 20);
            double tb = SheetPileQuayWall.Core.AnchorPile.AnchorDriveEstimate.CalcTb(15.0, sb, 10.0);
            Xunit.Assert.Equal(16.6, tb, 2);
        }

        // T1197: 別の径・N値でも同じ関係が成り立つこと(D=1000,N=30 → Sb=0.62)
        //   直杭: 1.0×22/0.62=35.483...→35.5 / 斜杭: 1.2×22/0.62=42.580...→42.6
        [Xunit.Fact]
        public void T1197_CalcTb_AnotherDiameter_MatchesFormula()
        {
            double sb = SheetPileQuayWall.Core.FrontWall.DriveEstimate.GetSb(1000, 30);
            Xunit.Assert.Equal(0.62, sb, 2);

            Xunit.Assert.Equal(35.5,
                SheetPileQuayWall.Core.AnchorPile.AnchorDriveEstimate.CalcTb(22.0, sb, 0.0), 2);
            Xunit.Assert.Equal(42.6,
                SheetPileQuayWall.Core.AnchorPile.AnchorDriveEstimate.CalcTb(22.0, sb, 10.0), 2);
        }

        // T1198: 斜杭の Tb は直杭より必ず大きい(K=1.2>1.0 の帰結)
        [Xunit.Fact]
        public void T1198_CalcTb_InclinedAlwaysGreaterThanStraight()
        {
            double sb = SheetPileQuayWall.Core.FrontWall.DriveEstimate.GetSb(700, 25);
            double straight = SheetPileQuayWall.Core.AnchorPile.AnchorDriveEstimate.CalcTb(18.0, sb, 0.0);
            double inclined = SheetPileQuayWall.Core.AnchorPile.AnchorDriveEstimate.CalcTb(18.0, sb, 5.0);
            Xunit.Assert.True(inclined > straight);
        }

        // T1199: 傾斜角の符号によらず斜杭と判定される(絶対値で判定)
        [Xunit.Fact]
        public void T1199_CalcTb_NegativeInclination_TreatedAsInclined()
        {
            double sb = SheetPileQuayWall.Core.FrontWall.DriveEstimate.GetSb(800, 20);
            double positive = SheetPileQuayWall.Core.AnchorPile.AnchorDriveEstimate.CalcTb(15.0, sb, 10.0);
            double negative = SheetPileQuayWall.Core.AnchorPile.AnchorDriveEstimate.CalcTb(15.0, sb, -10.0);
            Xunit.Assert.Equal(positive, negative, 6);
        }

        // T1200: 許容誤差ちょうど(0.001°)は直杭のまま。これを僅かに超えると斜杭になる
        [Xunit.Fact]
        public void T1200_CalcTb_ToleranceBoundary()
        {
            double sb = SheetPileQuayWall.Core.FrontWall.DriveEstimate.GetSb(800, 20);
            double atTolerance = SheetPileQuayWall.Core.AnchorPile.AnchorDriveEstimate.CalcTb(
                15.0, sb, SheetPileQuayWall.Core.AnchorPile.AnchorDriveEstimate.InclinationTolerance_deg);
            double straight = SheetPileQuayWall.Core.AnchorPile.AnchorDriveEstimate.CalcTb(15.0, sb, 0.0);
            double justOver = SheetPileQuayWall.Core.AnchorPile.AnchorDriveEstimate.CalcTb(
                15.0, sb, SheetPileQuayWall.Core.AnchorPile.AnchorDriveEstimate.InclinationTolerance_deg + 0.0001);

            Xunit.Assert.Equal(straight, atTolerance, 6);
            Xunit.Assert.True(justOver > atTolerance);
        }

        // T1201: 小数1位「切上げ」であること(四捨五入でも切捨てでもない)
        //   Sb=1.0, L=10.01 → 1.0×10.01/1.0=10.01 → 切上げ 10.1(四捨五入なら10.0になり得る)
        [Xunit.Fact]
        public void T1201_CalcTb_RoundsUpNotNearest()
        {
            double tb = SheetPileQuayWall.Core.AnchorPile.AnchorDriveEstimate.CalcTb(10.01, 1.0, 0.0);
            Xunit.Assert.Equal(10.1, tb, 2);
        }

        // ── 労務編成(3-4.6-15、陸上打設)────────────────────────────────────

        // T1202: 杭長 20m 未満はとび工2・普通作業員1
        [Xunit.Fact]
        public void T1202_GetLabor_ShortPile_Rigger2Laborer1()
        {
            var labor = SheetPileQuayWall.Core.AnchorPile.AnchorDriveEstimate.GetLabor(19.9, false, 700);
            Xunit.Assert.Equal(2, labor.rigger);
            Xunit.Assert.Equal(1, labor.laborer);
        }

        // T1203: 杭長 20m ちょうどは「20m以上」側(とび工3・普通作業員2)
        [Xunit.Fact]
        public void T1203_GetLabor_At20m_UsesLongPileRow()
        {
            var labor = SheetPileQuayWall.Core.AnchorPile.AnchorDriveEstimate.GetLabor(20.0, false, 700);
            Xunit.Assert.Equal(3, labor.rigger);
            Xunit.Assert.Equal(2, labor.laborer);
        }

        // T1204: 杭長 20m 超も同じく「20m以上」側
        [Xunit.Fact]
        public void T1204_GetLabor_LongPile_Rigger3Laborer2()
        {
            var labor = SheetPileQuayWall.Core.AnchorPile.AnchorDriveEstimate.GetLabor(30.0, false, 700);
            Xunit.Assert.Equal(3, labor.rigger);
            Xunit.Assert.Equal(2, labor.laborer);
        }

        // T1205: 世話役は常に1人(杭長・継杭の有無によらず一定)
        [Xunit.Fact]
        public void T1205_GetLabor_ForemanIsAlwaysOne()
        {
            Xunit.Assert.Equal(1,
                SheetPileQuayWall.Core.AnchorPile.AnchorDriveEstimate.GetLabor(10.0, true, 1000).foreman);
            Xunit.Assert.Equal(1,
                SheetPileQuayWall.Core.AnchorPile.AnchorDriveEstimate.GetLabor(40.0, false, 500).foreman);
        }

        // T1206: 溶接工は継杭が無ければ 0 人
        [Xunit.Fact]
        public void T1206_GetLabor_NoSplicing_WelderIsZero()
        {
            Xunit.Assert.Equal(0,
                SheetPileQuayWall.Core.AnchorPile.AnchorDriveEstimate.GetLabor(20.0, false, 1000).welder);
        }

        // T1207: 継杭ありは φ800mm 未満で溶接工1人、以上で2人
        [Xunit.Theory]
        [Xunit.InlineData(799, 1)]
        [Xunit.InlineData(800, 2)]
        [Xunit.InlineData(1000, 2)]
        public void T1207_GetLabor_SplicingWelderCountByDiameter(int d_mm, int expectedWelder)
        {
            var labor = SheetPileQuayWall.Core.AnchorPile.AnchorDriveEstimate.GetLabor(20.0, true, d_mm);
            Xunit.Assert.Equal(expectedWelder, labor.welder);
        }

        // T1208: 労務編成は前壁(FrontWall.DriveEstimate.GetLabor)とは別実装であることの確認。
        //        既存実装は陸上を杭長によらず一律 rigger=2 に固定しているため、
        //        杭長 20m 以上では新実装(控え杭)と値が食い違う(既存側の不整合を裏取り)。
        [Xunit.Fact]
        public void T1208_GetLabor_DiffersFromExistingFrontWallImplementationAt20m()
        {
            int anchorRigger = SheetPileQuayWall.Core.AnchorPile.AnchorDriveEstimate.GetLabor(
                25.0, false, 700).rigger;
            int frontWallRigger = SheetPileQuayWall.Core.FrontWall.DriveEstimate.GetLabor(
                SheetPileQuayWall.Core.FrontWall.ConstructionSite.Onshore, 25.0, false, 700).rigger;

            Xunit.Assert.Equal(3, anchorRigger);
            Xunit.Assert.Equal(2, frontWallRigger);
            Xunit.Assert.NotEqual(anchorRigger, frontWallRigger);
        }

        // ── 一気通貫シナリオ(既存 DriveEstimate との組合せ)────────────────────

        // T1209: 控え杭の代表ケースが一気通貫で成立すること。
        //   D=800mm, t=12mm, 根入れ長=15m, 傾斜角=10°(斜杭), N_tip=50, N_avg=20
        //   R = DriveEstimate.CalcR(0.8, 15.0, 50, 20) を共有利用
        //   Sb = DriveEstimate.GetSb(800, 20) = 1.09
        //   Tb(斜杭) = 1.2×15/1.09 = 16.6(T1196 と同じ)
        [Xunit.Fact]
        public void T1209_AnchorPileScenario_EndToEnd_SharesDriveEstimate()
        {
            double D_m = 0.8;
            int D_mm = 800;
            double r = SheetPileQuayWall.Core.FrontWall.DriveEstimate.CalcR(D_m, 15.0, 50, 20.0);
            double sb = SheetPileQuayWall.Core.FrontWall.DriveEstimate.GetSb(D_mm, 20);
            double tb = SheetPileQuayWall.Core.AnchorPile.AnchorDriveEstimate.CalcTb(15.0, sb, 10.0);
            double tp = SheetPileQuayWall.Core.FrontWall.DriveEstimate.CalcTp(
                SheetPileQuayWall.Core.FrontWall.ConstructionSite.Onshore, 0);

            Xunit.Assert.True(r > 0.0);
            Xunit.Assert.Equal(16.6, tb, 2);
            Xunit.Assert.Equal(14.0, tp, 2); // 5×0+14

            string hammerClass = SheetPileQuayWall.Core.FrontWall.DriveEstimate.GetHammerClass(4.0, r);
            Xunit.Assert.False(string.IsNullOrEmpty(hammerClass));

            var labor = SheetPileQuayWall.Core.AnchorPile.AnchorDriveEstimate.GetLabor(18.0, false, D_mm);
            Xunit.Assert.Equal(2, labor.rigger);
        }

        // T1210: 継杭ケース(jointCountPerPile=2)での準備時間・溶接時間の連動
        //   Tp = 5×2+14 = 24.0 分/本、Tw は DriveEstimate.CalcTw(800,12,2) をそのまま使う
        [Xunit.Fact]
        public void T1210_SplicingScenario_TpAndTwFromSharedDriveEstimate()
        {
            double tp = SheetPileQuayWall.Core.FrontWall.DriveEstimate.CalcTp(
                SheetPileQuayWall.Core.FrontWall.ConstructionSite.Onshore, 2);
            Xunit.Assert.Equal(24.0, tp, 2);

            double tw = SheetPileQuayWall.Core.FrontWall.DriveEstimate.CalcTw(800, 12, 2);
            Xunit.Assert.True(tw > 0.0);

            var labor = SheetPileQuayWall.Core.AnchorPile.AnchorDriveEstimate.GetLabor(18.0, true, 800);
            Xunit.Assert.Equal(2, labor.welder);
        }
    }
}
