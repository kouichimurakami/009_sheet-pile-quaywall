// T1060〜T1099: VibroJetEstimate の単体テスト
// 検証基準: 港湾土木請負工事積算基準 令和7年度改訂版 3章16節 3-1(3-16-11〜25)
// 期待値は基準の算定式・規格表から独立に手計算した値をハードコードする。
// AutoCAD 非依存のため xUnit で直接実行可能

namespace SheetPileQuayWall.Core.Tests
{
    public class VibroJetEstimateTests
    {
        // ── 基本振幅係数 A0(3-16-15)──────────────────────────────────────

        // T1060: 砂質土･レキ質土･粘性土の 4 列
        [Xunit.Theory]
        [Xunit.InlineData(3.0, 0.40)]
        [Xunit.InlineData(20.0, 0.65)]
        [Xunit.InlineData(40.0, 1.10)]
        [Xunit.InlineData(60.0, 1.40)]
        public void T1060_GetAmplitudeFactor_SandyGravelClay(double n, double expected)
        {
            double? a0 = SheetPileQuayWall.Core.FrontWall.VibroJetEstimate.GetAmplitudeFactor(
                SheetPileQuayWall.Core.FrontWall.JetSoilType.SandyGravelClay, n);
            Xunit.Assert.Equal(expected, a0!.Value, 2);
        }

        // T1061: N 値の列境界(5 / 30 / 50 はいずれも下側の列に入る)
        [Xunit.Theory]
        [Xunit.InlineData(5.0, 0.40)]
        [Xunit.InlineData(5.1, 0.65)]
        [Xunit.InlineData(30.0, 0.65)]
        [Xunit.InlineData(30.1, 1.10)]
        [Xunit.InlineData(50.0, 1.10)]
        [Xunit.InlineData(50.1, 1.40)]
        public void T1061_GetAmplitudeFactor_ColumnBoundaries(double n, double expected)
        {
            double? a0 = SheetPileQuayWall.Core.FrontWall.VibroJetEstimate.GetAmplitudeFactor(
                SheetPileQuayWall.Core.FrontWall.JetSoilType.SandyGravelClay, n);
            Xunit.Assert.Equal(expected, a0!.Value, 2);
        }

        // T1062: 玉石混りレキ・固結土・岩盤の定義済みセル
        [Xunit.Theory]
        [Xunit.InlineData(SheetPileQuayWall.Core.FrontWall.JetSoilType.CobbleGravel, 20.0, 0.65)]
        [Xunit.InlineData(SheetPileQuayWall.Core.FrontWall.JetSoilType.CobbleGravel, 40.0, 0.90)]
        [Xunit.InlineData(SheetPileQuayWall.Core.FrontWall.JetSoilType.CobbleGravel, 60.0, 1.55)]
        [Xunit.InlineData(SheetPileQuayWall.Core.FrontWall.JetSoilType.Cemented, 40.0, 1.00)]
        [Xunit.InlineData(SheetPileQuayWall.Core.FrontWall.JetSoilType.Cemented, 60.0, 1.70)]
        [Xunit.InlineData(SheetPileQuayWall.Core.FrontWall.JetSoilType.Rock, 60.0, 1.55)]
        public void T1062_GetAmplitudeFactor_OtherSoils(
            SheetPileQuayWall.Core.FrontWall.JetSoilType soil, double n, double expected)
        {
            double? a0 = SheetPileQuayWall.Core.FrontWall.VibroJetEstimate.GetAmplitudeFactor(soil, n);
            Xunit.Assert.Equal(expected, a0!.Value, 2);
        }

        // T1063: 原本で「−」のセルは null(適用対象外)であり 0 に潰してはならない
        [Xunit.Theory]
        [Xunit.InlineData(SheetPileQuayWall.Core.FrontWall.JetSoilType.CobbleGravel, 3.0)]
        [Xunit.InlineData(SheetPileQuayWall.Core.FrontWall.JetSoilType.Cemented, 3.0)]
        [Xunit.InlineData(SheetPileQuayWall.Core.FrontWall.JetSoilType.Cemented, 20.0)]
        [Xunit.InlineData(SheetPileQuayWall.Core.FrontWall.JetSoilType.Rock, 3.0)]
        [Xunit.InlineData(SheetPileQuayWall.Core.FrontWall.JetSoilType.Rock, 40.0)]
        public void T1063_GetAmplitudeFactor_UndefinedCellsAreNull(
            SheetPileQuayWall.Core.FrontWall.JetSoilType soil, double n)
        {
            Xunit.Assert.Null(
                SheetPileQuayWall.Core.FrontWall.VibroJetEstimate.GetAmplitudeFactor(soil, n));
        }

        // T1064: qu 基準の A0 は固結土(qu≦4.9 → 1.70)と岩盤(1.30 / 1.95)のみ
        [Xunit.Fact]
        public void T1064_GetAmplitudeFactorByQu_CementedAndRock()
        {
            Xunit.Assert.Equal(1.70,
                SheetPileQuayWall.Core.FrontWall.VibroJetEstimate.GetAmplitudeFactorByQu(
                    SheetPileQuayWall.Core.FrontWall.JetSoilType.Cemented, 4.9)!.Value, 2);
            Xunit.Assert.Null(
                SheetPileQuayWall.Core.FrontWall.VibroJetEstimate.GetAmplitudeFactorByQu(
                    SheetPileQuayWall.Core.FrontWall.JetSoilType.Cemented, 5.0));
            Xunit.Assert.Equal(1.30,
                SheetPileQuayWall.Core.FrontWall.VibroJetEstimate.GetAmplitudeFactorByQu(
                    SheetPileQuayWall.Core.FrontWall.JetSoilType.Rock, 4.9)!.Value, 2);
            Xunit.Assert.Equal(1.95,
                SheetPileQuayWall.Core.FrontWall.VibroJetEstimate.GetAmplitudeFactorByQu(
                    SheetPileQuayWall.Core.FrontWall.JetSoilType.Rock, 5.0)!.Value, 2);
            Xunit.Assert.Null(
                SheetPileQuayWall.Core.FrontWall.VibroJetEstimate.GetAmplitudeFactorByQu(
                    SheetPileQuayWall.Core.FrontWall.JetSoilType.SandyGravelClay, 3.0));
        }

        // T1065: 鋼管チャック非装備は係数を 1.3 で除す(3-16-15 注3)
        [Xunit.Fact]
        public void T1065_AdjustForNoChuck_DividesBy13()
        {
            Xunit.Assert.Equal(1.10 / 1.3,
                SheetPileQuayWall.Core.FrontWall.VibroJetEstimate.AdjustForNoChuck(1.10), 6);
        }

        // ── 必要偏心モーメント K0 と規格選定(3-16-15)──────────────────────

        // T1066: K0 = A0 × Wp × 98。A0=1.10, Wp=8.0t → 862.4 N·m
        [Xunit.Fact]
        public void T1066_CalcK0_MatchesFormula()
        {
            Xunit.Assert.Equal(862.4,
                SheetPileQuayWall.Core.FrontWall.VibroJetEstimate.CalcK0(1.10, 8.0), 1);
        }

        // T1067: 7 ランクの上限値ちょうどは、そのランクに収まる
        [Xunit.Theory]
        [Xunit.InlineData(200.0, "45kW", "150kVA")]
        [Xunit.InlineData(340.0, "60kW", "200kVA")]
        [Xunit.InlineData(440.0, "90kW", "300kVA")]
        [Xunit.InlineData(740.0, "120kW", "400kVA")]
        [Xunit.InlineData(1800.0, "150kW", "500kVA")]
        [Xunit.InlineData(2500.0, "200kW", "600kVA")]
        [Xunit.InlineData(2900.0, "240kW", "800kVA")]
        public void T1067_GetVibroClass_BoundaryValues(
            double k0, string vibro, string generator)
        {
            var (v, g) = SheetPileQuayWall.Core.FrontWall.VibroJetEstimate.GetVibroClass(k0);
            Xunit.Assert.Equal(vibro, v);
            Xunit.Assert.Equal(generator, g);
        }

        // T1068: 上限を超えると 1 ランク上がる
        [Xunit.Fact]
        public void T1068_GetVibroClass_OverBoundary_StepsUp()
        {
            Xunit.Assert.Equal("60kW",
                SheetPileQuayWall.Core.FrontWall.VibroJetEstimate.GetVibroClass(200.1).vibro);
        }

        // T1069: 表の範囲(2,900)を超えたら別途検討。発電機は空文字
        [Xunit.Fact]
        public void T1069_GetVibroClass_BeyondTable_RequiresSeparateStudy()
        {
            var (v, g) = SheetPileQuayWall.Core.FrontWall.VibroJetEstimate.GetVibroClass(2900.1);
            Xunit.Assert.Contains("別途検討", v);
            Xunit.Assert.Equal("", g);
        }

        // T1070: 選定の基礎が 16節 3-2(バイブロ単独)と異なることの確認。
        //   3-2 は「鋼材質量 + 貫入抵抗値」、3-1 は「必要偏心モーメント K0」で選ぶ。
        //   同じ杭でも両者の選定結果は一致する保証がない。
        [Xunit.Fact]
        public void T1070_SelectionBasisDiffersFromStandaloneVibro()
        {
            // Wp=8.0t、砂質土 N=40 → A0=1.10 → K0=862.4 → 150kW
            double a0 = SheetPileQuayWall.Core.FrontWall.VibroJetEstimate.GetAmplitudeFactor(
                SheetPileQuayWall.Core.FrontWall.JetSoilType.SandyGravelClay, 40.0)!.Value;
            double k0 = SheetPileQuayWall.Core.FrontWall.VibroJetEstimate.CalcK0(a0, 8.0);
            Xunit.Assert.Equal("150kW",
                SheetPileQuayWall.Core.FrontWall.VibroJetEstimate.GetVibroClass(k0).vibro);

            // 同じ 8.0t を 3-2 の基準(貫入抵抗 5,000kN)で選ぶと 150kW とは限らない
            Xunit.Assert.Equal("150kW",
                SheetPileQuayWall.Core.FrontWall.VibroEstimate.GetVibroClass(8.0, 5000.0));
            // 抵抗値が上がれば 3-2 側だけが上位規格になる
            Xunit.Assert.Equal("200kW",
                SheetPileQuayWall.Core.FrontWall.VibroEstimate.GetVibroClass(8.0, 15000.0));
        }

        // ── ジェット付帯設備(3-16-17)──────────────────────────────────────

        // T1071: ジェット用発動発電機は使用台数 1〜4 で 10/20/35/45 kVA
        [Xunit.Theory]
        [Xunit.InlineData(1, "10kVA")]
        [Xunit.InlineData(2, "20kVA")]
        [Xunit.InlineData(3, "35kVA")]
        [Xunit.InlineData(4, "45kVA")]
        public void T1071_GetJetGenerator_MatchesTable(int jetCount, string expected)
        {
            Xunit.Assert.Equal(expected,
                SheetPileQuayWall.Core.FrontWall.VibroJetEstimate.GetJetGenerator(jetCount));
        }

        // T1072: 表の範囲外(0 台・5 台)は空文字
        [Xunit.Theory]
        [Xunit.InlineData(0)]
        [Xunit.InlineData(5)]
        public void T1072_GetJetGenerator_OutOfRange_ReturnsEmpty(int jetCount)
        {
            Xunit.Assert.Equal("",
                SheetPileQuayWall.Core.FrontWall.VibroJetEstimate.GetJetGenerator(jetCount));
        }

        // T1073: 水中ポンプ・水槽・発動発電機(水源が遠い場合)
        [Xunit.Theory]
        [Xunit.InlineData(1, "φ150", 10.6, 1, "35kVA", 20, 1)]
        [Xunit.InlineData(2, "φ200", 15.5, 1, "45kVA", 30, 1)]
        [Xunit.InlineData(3, "φ150", 10.6, 2, "60kVA", 20, 2)]
        [Xunit.InlineData(4, "φ200", 15.5, 2, "75kVA", 30, 2)]
        public void T1073_GetWaterSupply_MatchesTable(
            int jetCount, string pump, double kW, int pumpCount,
            string generator, int tank_m3, int tankCount)
        {
            var r = SheetPileQuayWall.Core.FrontWall.VibroJetEstimate.GetWaterSupply(jetCount);
            Xunit.Assert.Equal(pump, r.pump);
            Xunit.Assert.Equal(kW, r.pumpOutput_kW, 1);
            Xunit.Assert.Equal(pumpCount, r.pumpCount);
            Xunit.Assert.Equal(generator, r.generator);
            Xunit.Assert.Equal(tank_m3, r.tankVolume_m3);
            Xunit.Assert.Equal(tankCount, r.tankCount);
        }

        // ── 1m 当り打込み時間 γ(3-16-20)──────────────────────────────────

        // T1074: 土質別の 4 式
        [Xunit.Fact]
        public void T1074_CalcGamma_EachSoilFormula()
        {
            // γ1 = 0.02×20 + 0.5 = 0.9
            Xunit.Assert.Equal(0.9,
                SheetPileQuayWall.Core.FrontWall.VibroJetEstimate.CalcGamma1(20.0), 3);
            // γ2 = 0.02×20 + 0.5 + 2 = 2.9
            Xunit.Assert.Equal(2.9,
                SheetPileQuayWall.Core.FrontWall.VibroJetEstimate.CalcGamma2(20.0, 2.0), 3);
            // γ3 = 0.04×20 + 0.6 = 1.4
            Xunit.Assert.Equal(1.4,
                SheetPileQuayWall.Core.FrontWall.VibroJetEstimate.CalcGamma3(20.0), 3);
            // γ4 = 0.82×5 + 3 = 7.1
            Xunit.Assert.Equal(7.1,
                SheetPileQuayWall.Core.FrontWall.VibroJetEstimate.CalcGamma4(5.0), 3);
        }

        // T1074b: 粘性土は A0 表では「砂質土･レキ質土･粘性土」、γ 表では γ3(粘性土・
        //         固結土)に属する。両表のくくり方の違いが正しく振り分けられること。
        [Xunit.Fact]
        public void T1074b_Clay_MapsToDifferentGroupsInEachTable()
        {
            // A0 表: 粘性土は砂質土等と同じ行
            Xunit.Assert.Equal(
                SheetPileQuayWall.Core.FrontWall.JetSoilType.SandyGravelClay,
                SheetPileQuayWall.Core.FrontWall.VibroJetEstimate.ToAmplitudeSoil(
                    SheetPileQuayWall.Core.FrontWall.JetLayerType.Clay));
            Xunit.Assert.Equal(
                SheetPileQuayWall.Core.FrontWall.JetSoilType.SandyGravelClay,
                SheetPileQuayWall.Core.FrontWall.VibroJetEstimate.ToAmplitudeSoil(
                    SheetPileQuayWall.Core.FrontWall.JetLayerType.SandGravel));

            // γ 表: 粘性土は γ3(0.04N+0.6)、砂質土は γ1(0.02N+0.5)で別式
            double clay = SheetPileQuayWall.Core.FrontWall.VibroJetEstimate.CalcGamma(
                SheetPileQuayWall.Core.FrontWall.JetLayerType.Clay, 20.0, 0.0, 0.0);
            double sand = SheetPileQuayWall.Core.FrontWall.VibroJetEstimate.CalcGamma(
                SheetPileQuayWall.Core.FrontWall.JetLayerType.SandGravel, 20.0, 0.0, 0.0);
            Xunit.Assert.Equal(1.4, clay, 3);
            Xunit.Assert.Equal(0.9, sand, 3);
        }

        // T1074c: 固結土は A0 表では独立行、γ 表では粘性土と同じ γ3
        [Xunit.Fact]
        public void T1074c_Cemented_UsesOwnA0RowButSharesGamma3()
        {
            Xunit.Assert.Equal(
                SheetPileQuayWall.Core.FrontWall.JetSoilType.Cemented,
                SheetPileQuayWall.Core.FrontWall.VibroJetEstimate.ToAmplitudeSoil(
                    SheetPileQuayWall.Core.FrontWall.JetLayerType.Cemented));
            Xunit.Assert.Equal(
                SheetPileQuayWall.Core.FrontWall.VibroJetEstimate.CalcGamma(
                    SheetPileQuayWall.Core.FrontWall.JetLayerType.Clay, 30.0, 0.0, 0.0),
                SheetPileQuayWall.Core.FrontWall.VibroJetEstimate.CalcGamma(
                    SheetPileQuayWall.Core.FrontWall.JetLayerType.Cemented, 30.0, 0.0, 0.0), 3);
        }

        // T1074d: 岩盤は qu 基準の γ4 を使い、N 値は無視される
        [Xunit.Fact]
        public void T1074d_Rock_UsesQuNotN()
        {
            double g = SheetPileQuayWall.Core.FrontWall.VibroJetEstimate.CalcGamma(
                SheetPileQuayWall.Core.FrontWall.JetLayerType.Rock, 999.0, 5.0, 0.0);
            Xunit.Assert.Equal(7.1, g, 3);
        }

        // T1075: 玉石混りレキの補正係数 η(3-16-20 注1)
        [Xunit.Theory]
        [Xunit.InlineData(75.0, 0.0)]
        [Xunit.InlineData(76.0, 2.0)]
        [Xunit.InlineData(100.0, 2.0)]
        [Xunit.InlineData(150.0, 2.5)]
        [Xunit.InlineData(200.0, 3.0)]
        public void T1075_GetEta_MatchesTable(double maxCobble_mm, double expected)
        {
            Xunit.Assert.Equal(expected,
                SheetPileQuayWall.Core.FrontWall.VibroJetEstimate.GetEta(maxCobble_mm)!.Value, 2);
        }

        // T1076: 最大玉石径 200mm 超は基準上「別途定める」ため null
        [Xunit.Fact]
        public void T1076_GetEta_Over200mm_IsNull()
        {
            Xunit.Assert.Null(
                SheetPileQuayWall.Core.FrontWall.VibroJetEstimate.GetEta(201.0));
        }

        // T1077: γ の加重平均。(0.9×10 + 1.4×5) / 15 = 16/15 = 1.0667
        [Xunit.Fact]
        public void T1077_WeightedGamma_AveragesByLength()
        {
            var layers = new System.Collections.Generic.List<(double, double)>
            {
                (0.9, 10.0),
                (1.4, 5.0),
            };
            Xunit.Assert.Equal(16.0 / 15.0,
                SheetPileQuayWall.Core.FrontWall.VibroJetEstimate.WeightedGamma(layers), 6);
        }

        // T1078: 層が無い場合は 0(ゼロ除算しないこと)
        [Xunit.Fact]
        public void T1078_WeightedGamma_NoLayers_ReturnsZero()
        {
            Xunit.Assert.Equal(0.0,
                SheetPileQuayWall.Core.FrontWall.VibroJetEstimate.WeightedGamma(
                    new System.Collections.Generic.List<(double, double)>()), 6);
        }

        // ── 係数 β(3-16-20)──────────────────────────────────────────────

        // T1079: 表の代表セル
        [Xunit.Theory]
        [Xunit.InlineData(500, 9, 1.05)]
        [Xunit.InlineData(800, 12, 1.00)]
        [Xunit.InlineData(1000, 16, 1.00)]
        [Xunit.InlineData(1500, 22, 1.00)]
        public void T1079_GetBeta_MatchesTable(int d_mm, int t_mm, double expected)
        {
            Xunit.Assert.Equal(expected,
                SheetPileQuayWall.Core.FrontWall.VibroJetEstimate.GetBeta(d_mm, t_mm)!.Value, 3);
        }

        // T1080: 表に無い板厚は直上の列へ丸める(t=10 は t=12 列)
        [Xunit.Fact]
        public void T1080_GetBeta_IntermediateThickness_RoundsUpToColumn()
        {
            Xunit.Assert.Equal(
                SheetPileQuayWall.Core.FrontWall.VibroJetEstimate.GetBeta(800, 12)!.Value,
                SheetPileQuayWall.Core.FrontWall.VibroJetEstimate.GetBeta(800, 10)!.Value, 3);
        }

        // T1081: 表の外径・板厚を超える場合は null(基準上「別途考慮」)
        [Xunit.Theory]
        [Xunit.InlineData(400, 9)]
        [Xunit.InlineData(1600, 9)]
        [Xunit.InlineData(800, 25)]
        public void T1081_GetBeta_OutOfTable_IsNull(int d_mm, int t_mm)
        {
            Xunit.Assert.Null(
                SheetPileQuayWall.Core.FrontWall.VibroJetEstimate.GetBeta(d_mm, t_mm));
        }

        // ── 係数 δ(3-16-21)──────────────────────────────────────────────

        // T1082: 表の代表セル
        [Xunit.Theory]
        [Xunit.InlineData(500, "45kW", 0.95)]
        [Xunit.InlineData(800, "150kW", 0.85)]
        [Xunit.InlineData(1000, "120kW", 1.00)]
        [Xunit.InlineData(1500, "90kW", 1.50)]
        [Xunit.InlineData(1500, "240kW", 1.00)]
        public void T1082_GetDelta_MatchesTable(int d_mm, string vibro, double expected)
        {
            Xunit.Assert.Equal(expected,
                SheetPileQuayWall.Core.FrontWall.VibroJetEstimate.GetDelta(d_mm, vibro)!.Value, 3);
        }

        // T1083: 原本で「−」の組合せは null。0.0 として計算に混入させてはならない
        //        (混入すると Tb = 0 になり打込み時間が消える)
        [Xunit.Theory]
        [Xunit.InlineData(500, "120kW")]
        [Xunit.InlineData(900, "45kW")]
        [Xunit.InlineData(1200, "60kW")]
        [Xunit.InlineData(800, "240kW")]
        public void T1083_GetDelta_DashCellsAreNull(int d_mm, string vibro)
        {
            Xunit.Assert.Null(
                SheetPileQuayWall.Core.FrontWall.VibroJetEstimate.GetDelta(d_mm, vibro));
        }

        // T1084: 未知のバイブロ規格(3-2 側の表記等)は null
        [Xunit.Fact]
        public void T1084_GetDelta_UnknownVibroClass_IsNull()
        {
            Xunit.Assert.Null(
                SheetPileQuayWall.Core.FrontWall.VibroJetEstimate.GetDelta(800, "240kW 超(別途検討)"));
        }

        // ── 加算時間 ε・準備時間 Tp・打込み時間 Tb(3-16-19〜21)─────────────

        // T1085: ε = 0.3 × 継手長。継手長 0.5m → 0.15 分
        [Xunit.Fact]
        public void T1085_CalcEpsilon_MatchesFormula()
        {
            Xunit.Assert.Equal(0.15,
                SheetPileQuayWall.Core.FrontWall.VibroJetEstimate.CalcEpsilon(0.5), 3);
        }

        // T1086: Tp = (0.3·L0 + 11) × ns + 5(小数1位切上げ)
        //   L0=20, ns=1 → (6+11)+5 = 22.0
        //   L0=20, ns=2 → 34+5     = 39.0
        [Xunit.Theory]
        [Xunit.InlineData(20.0, 1, 22.0)]
        [Xunit.InlineData(20.0, 2, 39.0)]
        public void T1086_CalcTp_MatchesFormula(double l0_m, int lifts, double expected)
        {
            Xunit.Assert.Equal(expected,
                SheetPileQuayWall.Core.FrontWall.VibroJetEstimate.CalcTp(l0_m, lifts), 2);
        }

        // T1087: 小数1位「切上げ」であること(四捨五入でも切捨てでもない)
        //   L0=12.5, ns=1 → (3.75+11)+5 = 19.75 → 19.8
        [Xunit.Fact]
        public void T1087_CalcTp_RoundsUpNotNearest()
        {
            Xunit.Assert.Equal(19.8,
                SheetPileQuayWall.Core.FrontWall.VibroJetEstimate.CalcTp(12.5, 1), 2);
        }

        // T1088: Tb = γ·β·δ·ℓ + ε(小数1位切上げ)
        //   0.9 × 1.00 × 0.85 × 20 + 0.15 = 15.30 + 0.15 = 15.45 → 15.5
        [Xunit.Fact]
        public void T1088_CalcTb_MatchesFormula()
        {
            Xunit.Assert.Equal(15.5,
                SheetPileQuayWall.Core.FrontWall.VibroJetEstimate.CalcTb(0.9, 1.00, 0.85, 20.0, 0.15), 2);
        }

        // T1089: 鋼管矢板の ε は Tb を必ず増やす(鋼管杭は ε=0)
        [Xunit.Fact]
        public void T1089_CalcTb_EpsilonIncreasesTime()
        {
            double pile = SheetPileQuayWall.Core.FrontWall.VibroJetEstimate.CalcTb(
                0.9, 1.00, 0.85, 20.0, 0.0);
            double sheetPile = SheetPileQuayWall.Core.FrontWall.VibroJetEstimate.CalcTb(
                0.9, 1.00, 0.85, 20.0,
                SheetPileQuayWall.Core.FrontWall.VibroJetEstimate.CalcEpsilon(1.0));
            Xunit.Assert.True(sheetPile > pile);
        }

        // ── 作業能力 Q(3-16-19)──────────────────────────────────────────

        // T1090: 陸上 ei=0.80。T=8, Tc=40 → 8×60/40 × 0.80 = 9.60 本/日
        [Xunit.Fact]
        public void T1090_CalcQ_Onshore()
        {
            Xunit.Assert.Equal(9.60,
                SheetPileQuayWall.Core.FrontWall.VibroJetEstimate.CalcQ(
                    SheetPileQuayWall.Core.FrontWall.ConstructionSite.Onshore, 8.0, 40.0,
                    SheetPileQuayWall.Core.FrontWall.SeaCondition.Normal,
                    SheetPileQuayWall.Core.FrontWall.ObstacleStatus.None, 50), 2);
        }

        // T1091: 海上 ei=0.70 で全補正。T=6, Tc=40 → 9 × 0.55 = 4.95 本/日
        [Xunit.Fact]
        public void T1091_CalcQ_OffshoreAllPenalties()
        {
            Xunit.Assert.Equal(4.95,
                SheetPileQuayWall.Core.FrontWall.VibroJetEstimate.CalcQ(
                    SheetPileQuayWall.Core.FrontWall.ConstructionSite.Offshore, 6.0, 40.0,
                    SheetPileQuayWall.Core.FrontWall.SeaCondition.Severe,
                    SheetPileQuayWall.Core.FrontWall.ObstacleStatus.Exists, 49), 2);
        }

        // T1092: 陸上打設に海象条件は無く、E1 は常に 0.00(基準の係数表)
        [Xunit.Fact]
        public void T1092_CalcQ_OnshoreIgnoresSeaCondition()
        {
            double normal = SheetPileQuayWall.Core.FrontWall.VibroJetEstimate.CalcQ(
                SheetPileQuayWall.Core.FrontWall.ConstructionSite.Onshore, 8.0, 40.0,
                SheetPileQuayWall.Core.FrontWall.SeaCondition.Normal,
                SheetPileQuayWall.Core.FrontWall.ObstacleStatus.None, 50);
            double severe = SheetPileQuayWall.Core.FrontWall.VibroJetEstimate.CalcQ(
                SheetPileQuayWall.Core.FrontWall.ConstructionSite.Onshore, 8.0, 40.0,
                SheetPileQuayWall.Core.FrontWall.SeaCondition.Severe,
                SheetPileQuayWall.Core.FrontWall.ObstacleStatus.None, 50);
            Xunit.Assert.Equal(normal, severe, 2);
        }

        // T1093: 3 工法の基準作業能力係数は互いに異なる(取り違え検出)
        //   打撃(3-4.5) 海上 0.50 < ジェット併用/バイブロ単独 海上 0.70 < ジェット併用 陸上 0.80
        [Xunit.Fact]
        public void T1093_Ei_DiffersAcrossThreeMethods()
        {
            Xunit.Assert.Equal(0.80,
                SheetPileQuayWall.Core.FrontWall.VibroJetEstimate.Ei_Onshore, 2);
            Xunit.Assert.Equal(0.70,
                SheetPileQuayWall.Core.FrontWall.VibroJetEstimate.Ei_Offshore, 2);
            Xunit.Assert.Equal(
                SheetPileQuayWall.Core.FrontWall.VibroEstimate.Ei_Offshore,
                SheetPileQuayWall.Core.FrontWall.VibroJetEstimate.Ei_Offshore, 2);

            double jet = SheetPileQuayWall.Core.FrontWall.VibroJetEstimate.CalcQ(
                SheetPileQuayWall.Core.FrontWall.ConstructionSite.Offshore, 6.0, 60.0,
                SheetPileQuayWall.Core.FrontWall.SeaCondition.Normal,
                SheetPileQuayWall.Core.FrontWall.ObstacleStatus.None, 50);
            double impact = SheetPileQuayWall.Core.FrontWall.DriveEstimate.CalcQ(
                SheetPileQuayWall.Core.FrontWall.ConstructionSite.Offshore, 60.0,
                SheetPileQuayWall.Core.FrontWall.SeaCondition.Normal,
                SheetPileQuayWall.Core.FrontWall.ObstacleStatus.None, 50);
            Xunit.Assert.True(jet > impact);
        }

        // ── 労務編成(3-16-21)────────────────────────────────────────────

        // T1094: 陸上は 20m、海上は 25m で区分が変わる
        //   陸上 20m未満 (1,2,1,1) / 20m以上 (1,2,2,1)
        //   海上 25m未満 (1,3,3,1) / 25m以上 (1,4,3,1)
        [Xunit.Theory]
        [Xunit.InlineData(SheetPileQuayWall.Core.FrontWall.ConstructionSite.Onshore, 15.0, 2, 1)]
        [Xunit.InlineData(SheetPileQuayWall.Core.FrontWall.ConstructionSite.Onshore, 20.0, 2, 2)]
        [Xunit.InlineData(SheetPileQuayWall.Core.FrontWall.ConstructionSite.Offshore, 20.0, 3, 3)]
        [Xunit.InlineData(SheetPileQuayWall.Core.FrontWall.ConstructionSite.Offshore, 25.0, 4, 3)]
        public void T1094_GetLabor_RiggerAndLaborerByLength(
            SheetPileQuayWall.Core.FrontWall.ConstructionSite site,
            double L_m, int rigger, int laborer)
        {
            var labor = SheetPileQuayWall.Core.FrontWall.VibroJetEstimate.GetLabor(
                site, L_m, false, 800);
            Xunit.Assert.Equal(rigger, labor.rigger);
            Xunit.Assert.Equal(laborer, labor.laborer);
            Xunit.Assert.Equal(1, labor.foreman);
            Xunit.Assert.Equal(1, labor.specialist);
        }

        // T1095: 溶接工は継杭時のみ。φ800mm 以上は 2 人
        [Xunit.Fact]
        public void T1095_GetLabor_WelderOnlyWhenSplicing()
        {
            Xunit.Assert.Equal(0,
                SheetPileQuayWall.Core.FrontWall.VibroJetEstimate.GetLabor(
                    SheetPileQuayWall.Core.FrontWall.ConstructionSite.Offshore,
                    30.0, false, 1000).welder);
            Xunit.Assert.Equal(2,
                SheetPileQuayWall.Core.FrontWall.VibroJetEstimate.GetLabor(
                    SheetPileQuayWall.Core.FrontWall.ConstructionSite.Offshore,
                    30.0, true, 1000).welder);
            Xunit.Assert.Equal(1,
                SheetPileQuayWall.Core.FrontWall.VibroJetEstimate.GetLabor(
                    SheetPileQuayWall.Core.FrontWall.ConstructionSite.Offshore,
                    30.0, true, 700).welder);
        }

        // T1096: 労務編成は 16節 3-2(バイブロ単独)とは別表である。
        //        海上 25m以上の とび工 は 3-1 が 4 人、3-2 が 5 人。
        [Xunit.Fact]
        public void T1096_LaborDiffersFromStandaloneVibro()
        {
            int jet = SheetPileQuayWall.Core.FrontWall.VibroJetEstimate.GetLabor(
                SheetPileQuayWall.Core.FrontWall.ConstructionSite.Offshore,
                30.0, false, 800).rigger;
            int standalone = SheetPileQuayWall.Core.FrontWall.VibroEstimate.GetLabor(
                SheetPileQuayWall.Core.FrontWall.VibroDriveTarget.SteelPipeSheetPile,
                30.0, false, 800).rigger;
            Xunit.Assert.Equal(4, jet);
            Xunit.Assert.Equal(5, standalone);
        }

        // ── クレーン規格・適用範囲 ────────────────────────────────────────

        // T1097: Cf = (Wv + Wp) × 6。Wv=10t, Wp=8t → 108t
        [Xunit.Fact]
        public void T1097_CalcCraneCapacity_MatchesFormula()
        {
            Xunit.Assert.Equal(108.0,
                SheetPileQuayWall.Core.FrontWall.VibroJetEstimate.CalcCraneCapacity(10.0, 8.0), 3);
        }

        // T1098: ジェット併用の適用範囲は外径 1,500mm 以下・全長 40m 以下(3-1-3 注3)
        [Xunit.Fact]
        public void T1098_ValidateJetApplicability_Limits()
        {
            Xunit.Assert.Null(
                SheetPileQuayWall.Core.FrontWall.VibroJetEstimate.ValidateJetApplicability(1.500, 40.0));
            Xunit.Assert.NotNull(
                SheetPileQuayWall.Core.FrontWall.VibroJetEstimate.ValidateJetApplicability(1.600, 40.0));
            Xunit.Assert.NotNull(
                SheetPileQuayWall.Core.FrontWall.VibroJetEstimate.ValidateJetApplicability(1.500, 41.0));
        }

        // T1099: 前壁の代表ケースが一気通貫で成立すること。
        //   φ1000×t12、Wp=8.0t、砂質土 N=40、打込長 20m、継手長 1.0m
        //   A0 = 1.10 → K0 = 1.10×8.0×98 = 862.4 → 150kW / 500kVA
        //   γ = 0.02×40+0.5 = 1.3 / β(1000,12) = 0.90 / δ(1000,150kW) = 0.95
        //   ε = 0.3×1.0 = 0.3
        //   Tb = 1.3×0.90×0.95×20 + 0.3 = 22.23 + 0.3 = 22.53 → 22.6
        [Xunit.Fact]
        public void T1099_FrontWallScenario_EndToEnd()
        {
            double a0 = SheetPileQuayWall.Core.FrontWall.VibroJetEstimate.GetAmplitudeFactor(
                SheetPileQuayWall.Core.FrontWall.JetSoilType.SandyGravelClay, 40.0)!.Value;
            double k0 = SheetPileQuayWall.Core.FrontWall.VibroJetEstimate.CalcK0(a0, 8.0);
            var (vibro, generator) =
                SheetPileQuayWall.Core.FrontWall.VibroJetEstimate.GetVibroClass(k0);
            Xunit.Assert.Equal("150kW", vibro);
            Xunit.Assert.Equal("500kVA", generator);

            double gamma = SheetPileQuayWall.Core.FrontWall.VibroJetEstimate.CalcGamma1(40.0);
            double beta = SheetPileQuayWall.Core.FrontWall.VibroJetEstimate.GetBeta(1000, 12)!.Value;
            double delta = SheetPileQuayWall.Core.FrontWall.VibroJetEstimate.GetDelta(1000, vibro)!.Value;
            double epsilon = SheetPileQuayWall.Core.FrontWall.VibroJetEstimate.CalcEpsilon(1.0);

            Xunit.Assert.Equal(1.3, gamma, 3);
            Xunit.Assert.Equal(0.90, beta, 3);
            Xunit.Assert.Equal(0.95, delta, 3);

            Xunit.Assert.Equal(22.6,
                SheetPileQuayWall.Core.FrontWall.VibroJetEstimate.CalcTb(
                    gamma, beta, delta, 20.0, epsilon), 2);
        }
    }
}
