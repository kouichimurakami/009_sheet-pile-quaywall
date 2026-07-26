// T1030〜T1059: VibroEstimate の単体テスト
// 検証基準: 港湾土木請負工事積算基準 令和7年度改訂版 3章16節 3-2(3-16-26〜31)
// 期待値は基準の算定式・規格表から独立に手計算した値をハードコードする。
// AutoCAD 非依存のため xUnit で直接実行可能

namespace SheetPileQuayWall.Core.Tests
{
    public class VibroEstimateTests
    {
        // ── 貫入抵抗値 R1 / Rj / R ────────────────────────────────────────

        // T1030: D=0.8m, Lb=20m, N_tip=50, N_avg=20 での R1
        //   Ap = π/4 × 0.8² = 0.50265482 m²  → 300×50×Ap = 7539.822369
        //   As = π × 0.8    = 2.51327412 m   → 2×20×20×As = 2010.619298
        //   R1 = 9550.441667 → 小数1位四捨五入 = 9550.4 kN
        [Xunit.Fact]
        public void T1030_CalcR1_D800_L20_MatchesHandCalculation()
        {
            double r1 = SheetPileQuayWall.Core.FrontWall.VibroEstimate.CalcR1(0.8, 20.0, 50, 20.0);
            Xunit.Assert.Equal(9550.4, r1, 1);
        }

        // T1031: 継手抵抗 Rj は鋼管矢板でのみ R1 の 1/10 が加算される(3-16-29)
        //   Rj = 9550.4 × 0.1 = 955.04 → 955.0 kN
        [Xunit.Fact]
        public void T1031_CalcRj_SheetPile_IsOneTenthOfR1()
        {
            double r1 = SheetPileQuayWall.Core.FrontWall.VibroEstimate.CalcR1(0.8, 20.0, 50, 20.0);
            double rj = SheetPileQuayWall.Core.FrontWall.VibroEstimate.CalcRj(
                r1, SheetPileQuayWall.Core.FrontWall.VibroDriveTarget.SteelPipeSheetPile);
            Xunit.Assert.Equal(955.0, rj, 1);
        }

        // T1032: 鋼管杭には継手が無いため Rj = 0
        [Xunit.Fact]
        public void T1032_CalcRj_Pile_IsZero()
        {
            double r1 = SheetPileQuayWall.Core.FrontWall.VibroEstimate.CalcR1(0.8, 20.0, 50, 20.0);
            double rj = SheetPileQuayWall.Core.FrontWall.VibroEstimate.CalcRj(
                r1, SheetPileQuayWall.Core.FrontWall.VibroDriveTarget.SteelPipePile);
            Xunit.Assert.Equal(0.0, rj, 6);
        }

        // T1033: 鋼管矢板の R = R1 + Rj = 9550.4 + 955.0 = 10505.4 kN
        [Xunit.Fact]
        public void T1033_CalcR_SheetPile_AddsJointResistance()
        {
            double r = SheetPileQuayWall.Core.FrontWall.VibroEstimate.CalcR(
                0.8, 20.0, 50, 20.0,
                SheetPileQuayWall.Core.FrontWall.VibroDriveTarget.SteelPipeSheetPile);
            Xunit.Assert.Equal(10505.4, r, 1);
        }

        // T1034: 鋼管杭の R は R1 と一致する(継手項が加算されないこと)
        [Xunit.Fact]
        public void T1034_CalcR_Pile_EqualsR1()
        {
            double r1 = SheetPileQuayWall.Core.FrontWall.VibroEstimate.CalcR1(0.8, 20.0, 50, 20.0);
            double r = SheetPileQuayWall.Core.FrontWall.VibroEstimate.CalcR(
                0.8, 20.0, 50, 20.0,
                SheetPileQuayWall.Core.FrontWall.VibroDriveTarget.SteelPipePile);
            Xunit.Assert.Equal(r1, r, 1);
        }

        // T1035: R1 の式は打撃工法(3-4.5-14)の R と同一形である。
        //        節が違うため実装は独立だが、値は一致しなければならない。
        //        片方だけ書き換えた場合にこのテストが落ちる。
        [Xunit.Fact]
        public void T1035_CalcR1_AgreesWithDriveEstimateFormula()
        {
            double vibro = SheetPileQuayWall.Core.FrontWall.VibroEstimate.CalcR1(1.0, 18.0, 40, 15.0);
            double drive = SheetPileQuayWall.Core.FrontWall.DriveEstimate.CalcR(1.0, 18.0, 40, 15.0);
            Xunit.Assert.Equal(drive, vibro, 1);
        }

        // ── バイブロハンマ規格選定(3-16-29)────────────────────────────────

        // T1036: 各規格の境界値ちょうどは、その規格に収まる(境界は上限として含む)
        [Xunit.Theory]
        [Xunit.InlineData(2.0, 2000.0, "90kW")]
        [Xunit.InlineData(5.0, 6000.0, "120kW")]
        [Xunit.InlineData(9.0, 13000.0, "150kW")]
        [Xunit.InlineData(15.0, 20000.0, "200kW")]
        [Xunit.InlineData(20.0, 28000.0, "240kW")]
        public void T1036_GetVibroClass_BoundaryValuesSelectSameRank(
            double mass_t, double r_kN, string expected)
        {
            Xunit.Assert.Equal(expected,
                SheetPileQuayWall.Core.FrontWall.VibroEstimate.GetVibroClass(mass_t, r_kN));
        }

        // T1037: 質量が境界を超えると 1 ランク上がる(抵抗値は据置き)
        [Xunit.Fact]
        public void T1037_GetVibroClass_MassOverBoundary_StepsUp()
        {
            Xunit.Assert.Equal("120kW",
                SheetPileQuayWall.Core.FrontWall.VibroEstimate.GetVibroClass(2.01, 2000.0));
        }

        // T1038: 抵抗値が境界を超えると 1 ランク上がる(質量は据置き)
        [Xunit.Fact]
        public void T1038_GetVibroClass_ResistanceOverBoundary_StepsUp()
        {
            Xunit.Assert.Equal("120kW",
                SheetPileQuayWall.Core.FrontWall.VibroEstimate.GetVibroClass(2.0, 2000.1));
        }

        // T1039: 質量・抵抗値の一方でも最大規格を超えたら別途検討
        [Xunit.Theory]
        [Xunit.InlineData(20.1, 100.0)]
        [Xunit.InlineData(1.0, 28000.1)]
        public void T1039_GetVibroClass_BeyondMaxRank_RequiresSeparateStudy(
            double mass_t, double r_kN)
        {
            string result =
                SheetPileQuayWall.Core.FrontWall.VibroEstimate.GetVibroClass(mass_t, r_kN);
            Xunit.Assert.Contains("別途検討", result);
        }

        // T1040: 単位混入(外径に mm 値 800 を渡す)は選定不能な抵抗値として顕在化する
        [Xunit.Fact]
        public void T1040_MillimeterDiameter_ProducesOutOfRangeResistance()
        {
            double r = SheetPileQuayWall.Core.FrontWall.VibroEstimate.CalcR(
                800.0, 20.0, 50, 20.0,
                SheetPileQuayWall.Core.FrontWall.VibroDriveTarget.SteelPipeSheetPile);
            Xunit.Assert.Contains("別途検討",
                SheetPileQuayWall.Core.FrontWall.VibroEstimate.GetVibroClass(5.0, r));
        }

        // ── 付帯機械(3-16-29)──────────────────────────────────────────────

        // T1041: バイブロ規格に対応する発動発電機・起重機船
        [Xunit.Theory]
        [Xunit.InlineData("90kW", "300kVA", "80t吊")]
        [Xunit.InlineData("120kW", "400kVA", "150t吊")]
        [Xunit.InlineData("150kW", "500kVA", "150t吊")]
        [Xunit.InlineData("200kW", "600kVA", "200t吊")]
        [Xunit.InlineData("240kW", "800kVA", "200t吊")]
        public void T1041_GetEquipment_MatchesStandardTable(
            string vibro, string generator, string craneVessel)
        {
            var (g, c) = SheetPileQuayWall.Core.FrontWall.VibroEstimate.GetEquipment(vibro);
            Xunit.Assert.Equal(generator, g);
            Xunit.Assert.Equal(craneVessel, c);
        }

        // T1042: 表に無い規格(240kW 超)は空を返し、呼び出し側が別途検討へ誘導できる
        [Xunit.Fact]
        public void T1042_GetEquipment_UnknownClass_ReturnsEmpty()
        {
            var (g, c) = SheetPileQuayWall.Core.FrontWall.VibroEstimate.GetEquipment(
                "240kW 超(別途検討)");
            Xunit.Assert.Equal("", g);
            Xunit.Assert.Equal("", c);
        }

        // T1043: 継手溶接機械は φ800mm 以上で 2 台 + 125kVA、未満で 1 台 + 100kVA
        [Xunit.Theory]
        [Xunit.InlineData(800, 2, "125kVA")]
        [Xunit.InlineData(1000, 2, "125kVA")]
        [Xunit.InlineData(799, 1, "100kVA")]
        [Xunit.InlineData(600, 1, "100kVA")]
        public void T1043_GetWeldEquipment_SplitsAtD800(
            int d_mm, int count, string generator)
        {
            var (c, g) = SheetPileQuayWall.Core.FrontWall.VibroEstimate.GetWeldEquipment(d_mm, true);
            Xunit.Assert.Equal(count, c);
            Xunit.Assert.Equal(generator, g);
        }

        // T1044: 継杭が無ければ溶接機械は計上しない
        [Xunit.Fact]
        public void T1044_GetWeldEquipment_NoSplicing_ReturnsZero()
        {
            var (c, g) = SheetPileQuayWall.Core.FrontWall.VibroEstimate.GetWeldEquipment(1000, false);
            Xunit.Assert.Equal(0, c);
            Xunit.Assert.Equal("", g);
        }

        // ── 準備時間 Tp(3-16-30)──────────────────────────────────────────

        // T1045: Tp = 24 + 0.6 × (Lb − 25)
        //   Lb=25 → 24.00 / Lb=40 → 24+9 = 33.00 / Lb=20 → 24−3 = 21.00
        [Xunit.Theory]
        [Xunit.InlineData(25.0, 24.00)]
        [Xunit.InlineData(40.0, 33.00)]
        [Xunit.InlineData(20.0, 21.00)]
        public void T1045_CalcTp_MatchesFormula(double L_m, double expected)
        {
            Xunit.Assert.Equal(expected,
                SheetPileQuayWall.Core.FrontWall.VibroEstimate.CalcTp(L_m), 2);
        }

        // ── 打込み時間 Tb(3-16-30)────────────────────────────────────────

        // T1046: 鋼管矢板 Lo=0.75 m/分。Lb=30 → 30/0.75 = 40.00 分
        [Xunit.Fact]
        public void T1046_CalcTb_SheetPile_UsesSlowerSpeed()
        {
            Xunit.Assert.Equal(40.00,
                SheetPileQuayWall.Core.FrontWall.VibroEstimate.CalcTb(
                    30.0, SheetPileQuayWall.Core.FrontWall.VibroDriveTarget.SteelPipeSheetPile), 2);
        }

        // T1047: 鋼管杭 Lo=0.90 m/分。Lb=30 → 30/0.90 = 33.333… → 33.33 分
        [Xunit.Fact]
        public void T1047_CalcTb_Pile_UsesFasterSpeed()
        {
            Xunit.Assert.Equal(33.33,
                SheetPileQuayWall.Core.FrontWall.VibroEstimate.CalcTb(
                    30.0, SheetPileQuayWall.Core.FrontWall.VibroDriveTarget.SteelPipePile), 2);
        }

        // T1048: 同一打設長では鋼管矢板の方が必ず時間がかかる(Lo が小さいため)
        [Xunit.Fact]
        public void T1048_CalcTb_SheetPileSlowerThanPile()
        {
            double sheet = SheetPileQuayWall.Core.FrontWall.VibroEstimate.CalcTb(
                25.0, SheetPileQuayWall.Core.FrontWall.VibroDriveTarget.SteelPipeSheetPile);
            double pile = SheetPileQuayWall.Core.FrontWall.VibroEstimate.CalcTb(
                25.0, SheetPileQuayWall.Core.FrontWall.VibroDriveTarget.SteelPipePile);
            Xunit.Assert.True(sheet > pile);
        }

        // ── 施工能力 Q(3-16-30)──────────────────────────────────────────

        // T1049: 標準条件(海象普通・障害なし・50本以上)
        //   Lb=25 の鋼管矢板: Tp=24.00, Tb=25/0.75=33.33, Tw=0 → Tc=57.33
        //   Q = 6×60/57.33 × 0.70 = 6.279435 × 0.70 = 4.3956… → 4.40 本/日
        [Xunit.Fact]
        public void T1049_CalcQ_StandardConditions()
        {
            double tc = SheetPileQuayWall.Core.FrontWall.VibroEstimate.CalcTp(25.0)
                + SheetPileQuayWall.Core.FrontWall.VibroEstimate.CalcTb(
                    25.0, SheetPileQuayWall.Core.FrontWall.VibroDriveTarget.SteelPipeSheetPile);
            Xunit.Assert.Equal(57.33, tc, 2);

            double q = SheetPileQuayWall.Core.FrontWall.VibroEstimate.CalcQ(
                tc,
                SheetPileQuayWall.Core.FrontWall.SeaCondition.Normal,
                SheetPileQuayWall.Core.FrontWall.ObstacleStatus.None,
                50);
            Xunit.Assert.Equal(4.40, q, 2);
        }

        // T1050: 3 つの補正が全て効く場合 (0.70−0.05−0.05−0.05 = 0.55)
        //   Q = 6.279435 × 0.55 = 3.4537… → 3.45 本/日
        [Xunit.Fact]
        public void T1050_CalcQ_AllPenaltiesApplied()
        {
            double q = SheetPileQuayWall.Core.FrontWall.VibroEstimate.CalcQ(
                57.33,
                SheetPileQuayWall.Core.FrontWall.SeaCondition.Severe,
                SheetPileQuayWall.Core.FrontWall.ObstacleStatus.Exists,
                49);
            Xunit.Assert.Equal(3.45, q, 2);
        }

        // T1051: 施工規模区分 E3 の境界は 50 本(50 本ちょうどは補正なし)
        [Xunit.Fact]
        public void T1051_CalcQ_ScaleBoundaryIs50Piles()
        {
            double at50 = SheetPileQuayWall.Core.FrontWall.VibroEstimate.CalcQ(
                57.33,
                SheetPileQuayWall.Core.FrontWall.SeaCondition.Normal,
                SheetPileQuayWall.Core.FrontWall.ObstacleStatus.None, 50);
            double at49 = SheetPileQuayWall.Core.FrontWall.VibroEstimate.CalcQ(
                57.33,
                SheetPileQuayWall.Core.FrontWall.SeaCondition.Normal,
                SheetPileQuayWall.Core.FrontWall.ObstacleStatus.None, 49);
            Xunit.Assert.True(at50 > at49);
            Xunit.Assert.Equal(4.40, at50, 2);
        }

        // T1052: 振動工法の基準作業能力係数は海上打設 0.70。
        //        打撃工法の海上 0.50(3-4.5)より高く、取り違えを検出する。
        [Xunit.Fact]
        public void T1052_Ei_OffshoreIsHigherThanImpactMethod()
        {
            Xunit.Assert.Equal(0.70,
                SheetPileQuayWall.Core.FrontWall.VibroEstimate.Ei_Offshore, 2);

            double vibroQ = SheetPileQuayWall.Core.FrontWall.VibroEstimate.CalcQ(
                60.0,
                SheetPileQuayWall.Core.FrontWall.SeaCondition.Normal,
                SheetPileQuayWall.Core.FrontWall.ObstacleStatus.None, 50);
            double impactQ = SheetPileQuayWall.Core.FrontWall.DriveEstimate.CalcQ(
                SheetPileQuayWall.Core.FrontWall.ConstructionSite.Offshore, 60.0,
                SheetPileQuayWall.Core.FrontWall.SeaCondition.Normal,
                SheetPileQuayWall.Core.FrontWall.ObstacleStatus.None, 50);
            Xunit.Assert.True(vibroQ > impactQ);
        }

        // ── 労務編成(3-16-31)────────────────────────────────────────────

        // T1053: とび工は 打込み対象 × 打設長 25m 境界 の 4 通り
        [Xunit.Theory]
        [Xunit.InlineData(SheetPileQuayWall.Core.FrontWall.VibroDriveTarget.SteelPipePile, 20.0, 2)]
        [Xunit.InlineData(SheetPileQuayWall.Core.FrontWall.VibroDriveTarget.SteelPipePile, 30.0, 4)]
        [Xunit.InlineData(SheetPileQuayWall.Core.FrontWall.VibroDriveTarget.SteelPipeSheetPile, 20.0, 3)]
        [Xunit.InlineData(SheetPileQuayWall.Core.FrontWall.VibroDriveTarget.SteelPipeSheetPile, 30.0, 5)]
        public void T1053_GetLabor_RiggerCountByTargetAndLength(
            SheetPileQuayWall.Core.FrontWall.VibroDriveTarget target, double L_m, int expected)
        {
            var labor = SheetPileQuayWall.Core.FrontWall.VibroEstimate.GetLabor(target, L_m, false, 800);
            Xunit.Assert.Equal(expected, labor.rigger);
        }

        // T1054: 打設長 25m ちょうどは「25m 以下」側(境界の取り違え検出)
        [Xunit.Fact]
        public void T1054_GetLabor_At25m_UsesShortPileRow()
        {
            var labor = SheetPileQuayWall.Core.FrontWall.VibroEstimate.GetLabor(
                SheetPileQuayWall.Core.FrontWall.VibroDriveTarget.SteelPipeSheetPile,
                25.0, false, 800);
            Xunit.Assert.Equal(3, labor.rigger);
        }

        // T1055: 世話役 1・普通作業員 3・特殊作業員 1 は打設長・対象によらず一定
        [Xunit.Fact]
        public void T1055_GetLabor_FixedMembersAreConstant()
        {
            var shortSheet = SheetPileQuayWall.Core.FrontWall.VibroEstimate.GetLabor(
                SheetPileQuayWall.Core.FrontWall.VibroDriveTarget.SteelPipeSheetPile,
                20.0, false, 800);
            var longPile = SheetPileQuayWall.Core.FrontWall.VibroEstimate.GetLabor(
                SheetPileQuayWall.Core.FrontWall.VibroDriveTarget.SteelPipePile,
                40.0, false, 600);

            Xunit.Assert.Equal(1, shortSheet.foreman);
            Xunit.Assert.Equal(3, shortSheet.laborer);
            Xunit.Assert.Equal(1, shortSheet.specialist);
            Xunit.Assert.Equal(1, longPile.foreman);
            Xunit.Assert.Equal(3, longPile.laborer);
            Xunit.Assert.Equal(1, longPile.specialist);
        }

        // T1056: 溶接工は継杭がある場合のみ計上し、人数は溶接機械の台数に一致する
        [Xunit.Fact]
        public void T1056_GetLabor_WelderFollowsWeldMachineCount()
        {
            var noSplice = SheetPileQuayWall.Core.FrontWall.VibroEstimate.GetLabor(
                SheetPileQuayWall.Core.FrontWall.VibroDriveTarget.SteelPipeSheetPile,
                30.0, false, 1000);
            var spliceLarge = SheetPileQuayWall.Core.FrontWall.VibroEstimate.GetLabor(
                SheetPileQuayWall.Core.FrontWall.VibroDriveTarget.SteelPipeSheetPile,
                30.0, true, 1000);
            var spliceSmall = SheetPileQuayWall.Core.FrontWall.VibroEstimate.GetLabor(
                SheetPileQuayWall.Core.FrontWall.VibroDriveTarget.SteelPipeSheetPile,
                30.0, true, 700);

            Xunit.Assert.Equal(0, noSplice.welder);
            Xunit.Assert.Equal(2, spliceLarge.welder);
            Xunit.Assert.Equal(1, spliceSmall.welder);
        }

        // ── 端数処理 ──────────────────────────────────────────────────────

        // T1057: 基準の「四捨五入」であること(.NET 既定の銀行丸めでないこと)。
        //   Lb = 25.125 → Tp = 24 + 0.6×0.125 = 24.075 → 四捨五入 24.08
        //   (銀行丸めなら 24.08 の手前で 24.07 になり得る)
        [Xunit.Fact]
        public void T1057_CalcTp_UsesRoundHalfUpNotBankersRounding()
        {
            Xunit.Assert.Equal(24.08,
                SheetPileQuayWall.Core.FrontWall.VibroEstimate.CalcTp(25.125), 2);
        }

        // ── 一貫性 ────────────────────────────────────────────────────────

        // T1058: 前壁(鋼管矢板)の代表ケースが一気通貫で成立すること。
        //   D=1.0m, Lb=30m, N_tip=50, N_avg=25, 鋼材質量 8.0t
        //   Ap = π/4 = 0.785398 → 300×50×Ap = 11780.972
        //   As = π       = 3.141593 → 2×25×30×As = 4712.389
        //   R1 = 16493.361 → 16493.4 / Rj = 1649.34 → 1649.3 / R = 18142.7
        //   → 質量 8.0t は 150kW 枠だが R=18142.7 > 13000 のため 200kW
        [Xunit.Fact]
        public void T1058_FrontWallScenario_EndToEnd()
        {
            double r1 = SheetPileQuayWall.Core.FrontWall.VibroEstimate.CalcR1(1.0, 30.0, 50, 25.0);
            Xunit.Assert.Equal(16493.4, r1, 1);

            double r = SheetPileQuayWall.Core.FrontWall.VibroEstimate.CalcR(
                1.0, 30.0, 50, 25.0,
                SheetPileQuayWall.Core.FrontWall.VibroDriveTarget.SteelPipeSheetPile);
            Xunit.Assert.Equal(18142.7, r, 1);

            string cls = SheetPileQuayWall.Core.FrontWall.VibroEstimate.GetVibroClass(8.0, r);
            Xunit.Assert.Equal("200kW", cls);

            var (generator, craneVessel) =
                SheetPileQuayWall.Core.FrontWall.VibroEstimate.GetEquipment(cls);
            Xunit.Assert.Equal("600kVA", generator);
            Xunit.Assert.Equal("200t吊", craneVessel);
        }

        // T1059: 継手抵抗の加算により、同一諸元でも鋼管矢板が鋼管杭より上位規格に
        //        なり得る(Rj を落とすと再現しない)
        //   D=1.0m, Lb=40m, N_tip=30, N_avg=20
        //   R1 = 300×30×0.785398 + 2×20×40×3.141593 = 7068.583 + 5026.548 = 12095.1
        //   鋼管杭  : R = 12095.1 ≦ 13000 → 150kW
        //   鋼管矢板: R = 12095.1 + 1209.5 = 13304.6 > 13000 → 200kW
        [Xunit.Fact]
        public void T1059_JointResistanceCanRaiseVibroRank()
        {
            double rPile = SheetPileQuayWall.Core.FrontWall.VibroEstimate.CalcR(
                1.0, 40.0, 30, 20.0,
                SheetPileQuayWall.Core.FrontWall.VibroDriveTarget.SteelPipePile);
            double rSheet = SheetPileQuayWall.Core.FrontWall.VibroEstimate.CalcR(
                1.0, 40.0, 30, 20.0,
                SheetPileQuayWall.Core.FrontWall.VibroDriveTarget.SteelPipeSheetPile);

            Xunit.Assert.Equal(12095.1, rPile, 1);
            Xunit.Assert.Equal(13304.6, rSheet, 1);

            Xunit.Assert.True(rPile <= 13000.0);
            Xunit.Assert.True(rSheet > 13000.0);
            Xunit.Assert.Equal("150kW",
                SheetPileQuayWall.Core.FrontWall.VibroEstimate.GetVibroClass(8.0, rPile));
            Xunit.Assert.Equal("200kW",
                SheetPileQuayWall.Core.FrontWall.VibroEstimate.GetVibroClass(8.0, rSheet));
        }

        // ── 台船・引船・揚錨船・潜水士船(付帯船舶、3-4.6-6 / 3-16-29)──────────

        // T1215: 積載物の長さ(杭の全長)による台船・引船の選定(境界未満の代表値)
        [Xunit.Theory]
        [Xunit.InlineData(20.0, "鋼300t積", "鋼D450PS型")]
        [Xunit.InlineData(30.0, "鋼400t積", "鋼D450PS型")]
        [Xunit.InlineData(33.0, "鋼500t積", "鋼D500PS型")]
        [Xunit.InlineData(38.0, "鋼700t積", "鋼D550PS型")]
        [Xunit.InlineData(43.0, "鋼1,000t積", "鋼D600PS型")]
        public void T1215_GetBargeAndTug_MatchesTableByPileLength(
            double length_m, string barge, string tug)
        {
            var (b, t) = SheetPileQuayWall.Core.FrontWall.VibroEstimate.GetBargeAndTug(length_m);
            Xunit.Assert.Equal(barge, b);
            Xunit.Assert.Equal(tug, t);
        }

        // T1216: 各区分の境界は「未満」— ちょうどの値は次のランクに入る
        //   (28.0m は「28m未満」に含まれず「28〜31m」= 400t 側になる)
        [Xunit.Theory]
        [Xunit.InlineData(27.999, "鋼300t積")]
        [Xunit.InlineData(28.0, "鋼400t積")]
        [Xunit.InlineData(31.0, "鋼500t積")]
        [Xunit.InlineData(34.0, "鋼700t積")]
        [Xunit.InlineData(39.0, "鋼1,000t積")]
        public void T1216_GetBargeAndTug_BoundaryIsExclusiveUpperBound(
            double length_m, string expectedBarge)
        {
            var (b, _) = SheetPileQuayWall.Core.FrontWall.VibroEstimate.GetBargeAndTug(length_m);
            Xunit.Assert.Equal(expectedBarge, b);
        }

        // T1217: 44m 以上は基準に規定が無く、空(別途選定)を返す
        [Xunit.Theory]
        [Xunit.InlineData(44.0)]
        [Xunit.InlineData(60.0)]
        public void T1217_GetBargeAndTug_At44mOrMore_ReturnsEmpty(double length_m)
        {
            var (b, t) = SheetPileQuayWall.Core.FrontWall.VibroEstimate.GetBargeAndTug(length_m);
            Xunit.Assert.Equal("", b);
            Xunit.Assert.Equal("", t);
        }

        // T1218: 揚錨船・潜水士船の規格はバイブロ規格・杭長によらず一定
        [Xunit.Fact]
        public void T1218_AnchorHandlingAndDiverVesselSpecs_AreConstants()
        {
            Xunit.Assert.Equal("鋼D 5t吊",
                SheetPileQuayWall.Core.FrontWall.VibroEstimate.AnchorHandlingVesselSpec);
            Xunit.Assert.Equal("D270PS型 3〜5t吊",
                SheetPileQuayWall.Core.FrontWall.VibroEstimate.DiverVesselSpec);
        }

        // T1219: 台船・引船の規格上限は 44m(BargeTugMaxLength_m と GetBargeAndTug の
        //        別途選定境界が食い違わないことを固定する)
        [Xunit.Fact]
        public void T1219_BargeTugMaxLength_MatchesGetBargeAndTugBoundary()
        {
            double justUnder = SheetPileQuayWall.Core.FrontWall.VibroEstimate.BargeTugMaxLength_m - 0.001;
            var (b, _) = SheetPileQuayWall.Core.FrontWall.VibroEstimate.GetBargeAndTug(justUnder);
            Xunit.Assert.NotEqual("", b);

            var (bAtMax, _) = SheetPileQuayWall.Core.FrontWall.VibroEstimate.GetBargeAndTug(
                SheetPileQuayWall.Core.FrontWall.VibroEstimate.BargeTugMaxLength_m);
            Xunit.Assert.Equal("", bAtMax);
        }
    }
}
