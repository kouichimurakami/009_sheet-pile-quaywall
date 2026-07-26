// T1220〜T1249: BoringLogAnalysis の単体テスト
// 検証基準: 港湾土木請負工事積算基準 令和7年度改訂版 3-16-19注3・3-16-6注2(換算N値)、
//           3-4.5-14/3-4.6-12(R用除外)、3-4.5-16/3-4.6-14(Sb用除外)
// 期待値は基準の式から独立に手計算(Python検算)した値をハードコードする。
// AutoCAD 非依存のため xUnit で直接実行可能

namespace SheetPileQuayWall.Core.Tests
{
    public class BoringLogTests
    {
        private const string FiveLayerCsv =
            "土層名,土質区分,標高上端,標高下端,層厚,N値,打撃回数法,貫入量,一軸圧縮強度\n" +
            "埋土,砂質土等,0.0,-2.0,2.0,3,,,\n" +
            "沖積粘土層,粘性土,-2.0,-5.0,3.0,8,,,\n" +
            "洪積砂質土層,砂質土等,-5.0,-10.0,5.0,22,,,\n" +
            "洪積砂礫層,砂質土等,-10.0,-15.0,5.0,55,50,3.3,\n" +
            "軟岩層,岩盤,-15.0,-20.0,5.0,,,,4.2\n";

        // ── 取り込み(正常系)────────────────────────────────────────────

        // T1220: 5層すべてが正しく取り込まれ、標高降順に並ぶ
        [Xunit.Fact]
        public void T1220_Parse_AllLayersParsedInOrder()
        {
            var r = SheetPileQuayWall.Core.Geotech.BoringLogAnalysis.Parse(FiveLayerCsv);

            Xunit.Assert.Empty(r.Errors);
            Xunit.Assert.Equal(5, r.Rows.Count);
            Xunit.Assert.Equal(0.0, r.Rows[0].ElevationTopM, 3);
            Xunit.Assert.Equal(-20.0, r.Rows[4].ElevationBottomM, 3);
            Xunit.Assert.Equal(SheetPileQuayWall.Core.FrontWall.JetLayerType.Rock, r.Rows[4].SoilType);
        }

        // T1221: 打止め値(N=55, 50回法, 貫入量3.3cm)が換算N値に置き換えられる
        [Xunit.Fact]
        public void T1221_Parse_ConvertsBlowCountAndPenetrationToN()
        {
            var r = SheetPileQuayWall.Core.Geotech.BoringLogAnalysis.Parse(FiveLayerCsv);

            Xunit.Assert.Equal(1500.0 / 3.3, r.Rows[3].NValue!.Value, 4);
        }

        // T1222: 岩盤層は NValue が null、QuValue が設定される
        [Xunit.Fact]
        public void T1222_Parse_RockLayerHasNoNValueButHasQu()
        {
            var r = SheetPileQuayWall.Core.Geotech.BoringLogAnalysis.Parse(FiveLayerCsv);

            Xunit.Assert.Null(r.Rows[4].NValue);
            Xunit.Assert.Equal(4.2, r.Rows[4].QuValue!.Value, 3);
        }

        // T1223: CSV の行順が深度順でなくても、標高降順に並べ替えて連続性を検証できる
        [Xunit.Fact]
        public void T1223_Parse_ReordersRowsRegardlessOfCsvOrder()
        {
            string shuffled =
                "土層名,土質区分,標高上端,標高下端,層厚,N値\n" +
                "B,砂質土等,-2.0,-5.0,3.0,8\n" +
                "A,砂質土等,0.0,-2.0,2.0,3\n";

            var r = SheetPileQuayWall.Core.Geotech.BoringLogAnalysis.Parse(shuffled);

            Xunit.Assert.Empty(r.Errors);
            Xunit.Assert.Equal("A", r.Rows[0].LayerName);
            Xunit.Assert.Equal("B", r.Rows[1].LayerName);
        }

        // ── 換算N値(3-16-19注3、3-16-6注2)────────────────────────────────

        [Xunit.Theory]
        [Xunit.InlineData(50, 1500.0)]
        [Xunit.InlineData(60, 1800.0)]
        [Xunit.InlineData(70, 2100.0)]
        [Xunit.InlineData(80, 2400.0)]
        public void T1224_Parse_ConversionNumeratorByBlowCountMethod(int blowCount, double numerator)
        {
            string csv = "土質区分,標高上端,標高下端,層厚,N値,打撃回数法,貫入量\n" +
                $"砂質土等,0.0,-1.0,1.0,999,{blowCount},2.0\n";

            var r = SheetPileQuayWall.Core.Geotech.BoringLogAnalysis.Parse(csv);

            Xunit.Assert.Empty(r.Errors);
            Xunit.Assert.Equal(numerator / 2.0, r.Rows[0].NValue!.Value, 4);
        }

        // T1225: 未対応の打撃回数法(55回法等)はエラー
        [Xunit.Fact]
        public void T1225_Parse_UnsupportedBlowCountMethod_ProducesError()
        {
            string csv = "土質区分,標高上端,標高下端,層厚,N値,打撃回数法,貫入量\n" +
                "砂質土等,0.0,-1.0,1.0,999,55,2.0\n";

            var r = SheetPileQuayWall.Core.Geotech.BoringLogAnalysis.Parse(csv);

            Xunit.Assert.Empty(r.Rows);
            Xunit.Assert.Single(r.Errors);
            Xunit.Assert.Contains("打撃回数法", r.Errors[0].Message);
        }

        // T1226: 打撃回数法・貫入量は片方だけの指定でエラー
        [Xunit.Fact]
        public void T1226_Parse_BlowCountWithoutPenetration_ProducesError()
        {
            string csv = "土質区分,標高上端,標高下端,層厚,N値,打撃回数法\n" +
                "砂質土等,0.0,-1.0,1.0,55,50\n";

            var r = SheetPileQuayWall.Core.Geotech.BoringLogAnalysis.Parse(csv);

            Xunit.Assert.Empty(r.Rows);
            Xunit.Assert.Single(r.Errors);
            Xunit.Assert.Contains("両方指定", r.Errors[0].Message);
        }

        // ── 検証エラー ────────────────────────────────────────────────────

        // T1227: 層厚が標高差と一致しないとエラー
        [Xunit.Fact]
        public void T1227_Parse_ThicknessMismatch_ProducesError()
        {
            string csv = "土質区分,標高上端,標高下端,層厚,N値\n" +
                "砂質土等,0.0,-2.0,3.0,5\n";

            var r = SheetPileQuayWall.Core.Geotech.BoringLogAnalysis.Parse(csv);

            Xunit.Assert.Empty(r.Rows);
            Xunit.Assert.Single(r.Errors);
            Xunit.Assert.Contains("層厚", r.Errors[0].Message);
        }

        // T1228: 標高上端が標高下端以下(Z軸鉛直上向きに反する)はエラー
        [Xunit.Fact]
        public void T1228_Parse_TopNotAboveBottom_ProducesError()
        {
            string csv = "土質区分,標高上端,標高下端,層厚,N値\n" +
                "砂質土等,-2.0,0.0,2.0,5\n";

            var r = SheetPileQuayWall.Core.Geotech.BoringLogAnalysis.Parse(csv);

            Xunit.Assert.Empty(r.Rows);
            Xunit.Assert.NotEmpty(r.Errors);
        }

        // T1229: 未知の土質区分はエラー
        [Xunit.Fact]
        public void T1229_Parse_UnknownSoilType_ProducesError()
        {
            string csv = "土質区分,標高上端,標高下端,層厚,N値\n" +
                "凍土,0.0,-2.0,2.0,5\n";

            var r = SheetPileQuayWall.Core.Geotech.BoringLogAnalysis.Parse(csv);

            Xunit.Assert.Empty(r.Rows);
            Xunit.Assert.Single(r.Errors);
            Xunit.Assert.Contains("土質区分", r.Errors[0].Message);
        }

        // T1230: 岩盤以外の行で N値 が欠落しているとエラー
        [Xunit.Fact]
        public void T1230_Parse_MissingNValue_NonRock_ProducesError()
        {
            string csv = "土質区分,標高上端,標高下端,層厚\n" +
                "砂質土等,0.0,-2.0,2.0\n";

            var r = SheetPileQuayWall.Core.Geotech.BoringLogAnalysis.Parse(csv);

            Xunit.Assert.Empty(r.Rows);
            Xunit.Assert.Single(r.Errors);
            Xunit.Assert.Contains("N値", r.Errors[0].Message);
        }

        // T1231: 岩盤の行で一軸圧縮強度が欠落しているとエラー
        [Xunit.Fact]
        public void T1231_Parse_MissingQu_Rock_ProducesError()
        {
            string csv = "土質区分,標高上端,標高下端,層厚\n" +
                "岩盤,0.0,-2.0,2.0\n";

            var r = SheetPileQuayWall.Core.Geotech.BoringLogAnalysis.Parse(csv);

            Xunit.Assert.Empty(r.Rows);
            Xunit.Assert.Single(r.Errors);
            Xunit.Assert.Contains("一軸圧縮強度", r.Errors[0].Message);
        }

        // T1232: 岩盤以外の行に一軸圧縮強度が指定されているとエラー(土質区分の誤りを検出)
        [Xunit.Fact]
        public void T1232_Parse_QuOnNonRockRow_ProducesError()
        {
            string csv = "土質区分,標高上端,標高下端,層厚,N値,一軸圧縮強度\n" +
                "砂質土等,0.0,-2.0,2.0,10,4.0\n";

            var r = SheetPileQuayWall.Core.Geotech.BoringLogAnalysis.Parse(csv);

            Xunit.Assert.Empty(r.Rows);
            Xunit.Assert.Single(r.Errors);
            Xunit.Assert.Contains("一軸圧縮強度", r.Errors[0].Message);
        }

        // T1233: 標高が不連続(ギャップ)だとエラー
        [Xunit.Fact]
        public void T1233_Parse_ElevationGap_ProducesError()
        {
            string csv = "土質区分,標高上端,標高下端,層厚,N値\n" +
                "砂質土等,0.0,-2.0,2.0,5\n" +
                "砂質土等,-2.5,-5.0,2.5,8\n"; // -2.0 と -2.5 の間に0.5mのギャップ

            var r = SheetPileQuayWall.Core.Geotech.BoringLogAnalysis.Parse(csv);

            Xunit.Assert.NotEmpty(r.Errors);
        }

        // ── 加重平均N値(R用・Sb用)の除外ルール(独立した単純ケース)────────────

        // T1234: 閾値0、N=[0,0,10] 各層厚1m → 先頭2層(N=0)を除外し、残り(N=10)のみ
        [Xunit.Fact]
        public void T1234_CalcWeightedN_ThresholdZero_ExcludesOnlyZero()
        {
            var layers = MakeLayers(
                (0.0, 1.0), (0.0, 1.0), (10.0, 1.0));

            var result = SheetPileQuayWall.Core.Geotech.BoringLogAnalysis.CalcWeightedN(
                layers, SheetPileQuayWall.Core.Geotech.BoringLogAnalysis.RExclusionThreshold);

            Xunit.Assert.Equal(10.0, result.weightedN, 4);
            Xunit.Assert.Equal(1.0, result.reckoningLength_m, 4);
        }

        // T1235: 閾値0、先頭層が N=1(>0)なら除外は一切発生しない
        [Xunit.Fact]
        public void T1235_CalcWeightedN_ThresholdZero_NoExclusionWhenFirstLayerAboveThreshold()
        {
            var layers = MakeLayers(
                (1.0, 1.0), (0.0, 1.0), (10.0, 1.0));

            var result = SheetPileQuayWall.Core.Geotech.BoringLogAnalysis.CalcWeightedN(
                layers, SheetPileQuayWall.Core.Geotech.BoringLogAnalysis.RExclusionThreshold);

            Xunit.Assert.Equal(11.0 / 3.0, result.weightedN, 4);
            Xunit.Assert.Equal(3.0, result.reckoningLength_m, 4);
        }

        // T1236: 閾値5、N=[3,5,8] 各層厚1m → 先頭2層(N≦5)を除外
        [Xunit.Fact]
        public void T1236_CalcWeightedN_ThresholdFive_ExcludesUpToFive()
        {
            var layers = MakeLayers(
                (3.0, 1.0), (5.0, 1.0), (8.0, 1.0));

            var result = SheetPileQuayWall.Core.Geotech.BoringLogAnalysis.CalcWeightedN(
                layers, SheetPileQuayWall.Core.Geotech.BoringLogAnalysis.SbExclusionThreshold);

            Xunit.Assert.Equal(8.0, result.weightedN, 4);
            Xunit.Assert.Equal(1.0, result.reckoningLength_m, 4);
        }

        // T1237: R用(閾値0)とSb用(閾値5)で同じ柱状図から異なる値が出ることを固定する
        [Xunit.Fact]
        public void T1237_CalcWeightedN_RAndSbThresholdsDiffer()
        {
            var r = SheetPileQuayWall.Core.Geotech.BoringLogAnalysis.Parse(FiveLayerCsv);
            var forR = SheetPileQuayWall.Core.Geotech.BoringLogAnalysis.CalcWeightedN(
                r.Rows, SheetPileQuayWall.Core.Geotech.BoringLogAnalysis.RExclusionThreshold);
            var forSb = SheetPileQuayWall.Core.Geotech.BoringLogAnalysis.CalcWeightedN(
                r.Rows, SheetPileQuayWall.Core.Geotech.BoringLogAnalysis.SbExclusionThreshold);

            Xunit.Assert.Equal(160.848, forR.weightedN, 2);
            Xunit.Assert.Equal(15.0, forR.reckoningLength_m, 3);
            Xunit.Assert.Equal(185.133, forSb.weightedN, 2);
            Xunit.Assert.Equal(13.0, forSb.reckoningLength_m, 3);
            Xunit.Assert.NotEqual(forR.weightedN, forSb.weightedN);
        }

        // T1238: 岩盤層は R/Sb 用の加重平均から常に除外され、除外本数・層厚が返る
        [Xunit.Fact]
        public void T1238_CalcWeightedN_ExcludesRockAndReportsIt()
        {
            var r = SheetPileQuayWall.Core.Geotech.BoringLogAnalysis.Parse(FiveLayerCsv);
            var forR = SheetPileQuayWall.Core.Geotech.BoringLogAnalysis.CalcWeightedN(
                r.Rows, SheetPileQuayWall.Core.Geotech.BoringLogAnalysis.RExclusionThreshold);

            Xunit.Assert.Equal(1, forR.excludedRockLayerCount);
            Xunit.Assert.Equal(5.0, forR.excludedRockThickness_m, 3);
        }

        // T1239: 岩盤層が表層(1層目)にあると、表層除外スキャンはそこで止まる
        //        (岩盤は N を持たないため、しきい値比較の対象にならない)
        [Xunit.Fact]
        public void T1239_CalcWeightedN_RockAtSurfaceStopsExclusionScan()
        {
            var layers = new System.Collections.Generic.List<SheetPileQuayWall.Core.Geotech.BoringLayer>
            {
                new SheetPileQuayWall.Core.Geotech.BoringLayer
                {
                    SoilType = SheetPileQuayWall.Core.FrontWall.JetLayerType.Rock,
                    ElevationTopM = 0.0, ElevationBottomM = -1.0, ThicknessM = 1.0,
                    QuValue = 3.0
                },
                new SheetPileQuayWall.Core.Geotech.BoringLayer
                {
                    SoilType = SheetPileQuayWall.Core.FrontWall.JetLayerType.SandGravel,
                    ElevationTopM = -1.0, ElevationBottomM = -2.0, ThicknessM = 1.0,
                    NValue = 10.0
                },
            };

            var result = SheetPileQuayWall.Core.Geotech.BoringLogAnalysis.CalcWeightedN(
                layers, SheetPileQuayWall.Core.Geotech.BoringLogAnalysis.RExclusionThreshold);

            Xunit.Assert.Equal(10.0, result.weightedN, 4);
            Xunit.Assert.Equal(1, result.excludedRockLayerCount);
        }

        // ── 土質区分別の加重平均N値(γ用)──────────────────────────────────

        // T1240: 土質区分ごとの加重平均N値には表層除外を適用しない
        [Xunit.Fact]
        public void T1240_CalcWeightedNBySoilType_MatchesHandCalculation()
        {
            var r = SheetPileQuayWall.Core.Geotech.BoringLogAnalysis.Parse(FiveLayerCsv);

            double? sand = SheetPileQuayWall.Core.Geotech.BoringLogAnalysis.CalcWeightedNBySoilType(
                r.Rows, SheetPileQuayWall.Core.FrontWall.JetLayerType.SandGravel);
            double? clay = SheetPileQuayWall.Core.Geotech.BoringLogAnalysis.CalcWeightedNBySoilType(
                r.Rows, SheetPileQuayWall.Core.FrontWall.JetLayerType.Clay);

            Xunit.Assert.Equal(199.061, sand!.Value, 2);
            Xunit.Assert.Equal(8.0, clay!.Value, 3);
        }

        // T1241: 該当層が無い土質区分は null
        [Xunit.Fact]
        public void T1241_CalcWeightedNBySoilType_NoMatchingLayers_ReturnsNull()
        {
            var r = SheetPileQuayWall.Core.Geotech.BoringLogAnalysis.Parse(FiveLayerCsv);

            double? cobble = SheetPileQuayWall.Core.Geotech.BoringLogAnalysis.CalcWeightedNBySoilType(
                r.Rows, SheetPileQuayWall.Core.FrontWall.JetLayerType.CobbleGravel);

            Xunit.Assert.Null(cobble);
        }

        // T1242: 岩盤を指定すると常に null(qu を使うべきことを示す)
        [Xunit.Fact]
        public void T1242_CalcWeightedNBySoilType_Rock_AlwaysReturnsNull()
        {
            var r = SheetPileQuayWall.Core.Geotech.BoringLogAnalysis.Parse(FiveLayerCsv);

            double? rock = SheetPileQuayWall.Core.Geotech.BoringLogAnalysis.CalcWeightedNBySoilType(
                r.Rows, SheetPileQuayWall.Core.FrontWall.JetLayerType.Rock);

            Xunit.Assert.Null(rock);
        }

        // ── 岩盤の加重平均一軸圧縮強度 ────────────────────────────────────

        // T1243: 単一の岩盤層のqu(単純ケースなのでそのまま一致)
        [Xunit.Fact]
        public void T1243_CalcWeightedQu_SingleRockLayer()
        {
            var r = SheetPileQuayWall.Core.Geotech.BoringLogAnalysis.Parse(FiveLayerCsv);

            double? qu = SheetPileQuayWall.Core.Geotech.BoringLogAnalysis.CalcWeightedQu(r.Rows);

            Xunit.Assert.Equal(4.2, qu!.Value, 3);
        }

        // T1244: 岩盤層が無ければ null
        [Xunit.Fact]
        public void T1244_CalcWeightedQu_NoRockLayers_ReturnsNull()
        {
            string csv = "土質区分,標高上端,標高下端,層厚,N値\n" +
                "砂質土等,0.0,-2.0,2.0,10\n";
            var r = SheetPileQuayWall.Core.Geotech.BoringLogAnalysis.Parse(csv);

            double? qu = SheetPileQuayWall.Core.Geotech.BoringLogAnalysis.CalcWeightedQu(r.Rows);

            Xunit.Assert.Null(qu);
        }

        // T1245: 複数岩盤層があれば層厚加重平均になる(2層、qu=3.0と6.0、層厚1mと2m)
        //   = (3.0×1 + 6.0×2) / 3 = 5.0
        [Xunit.Fact]
        public void T1245_CalcWeightedQu_MultipleRockLayers_WeightedByThickness()
        {
            string csv = "土質区分,標高上端,標高下端,層厚,一軸圧縮強度\n" +
                "岩盤,0.0,-1.0,1.0,3.0\n" +
                "岩盤,-1.0,-3.0,2.0,6.0\n";
            var r = SheetPileQuayWall.Core.Geotech.BoringLogAnalysis.Parse(csv);

            double? qu = SheetPileQuayWall.Core.Geotech.BoringLogAnalysis.CalcWeightedQu(r.Rows);

            Xunit.Assert.Equal(5.0, qu!.Value, 3);
        }

        // ── ヘルパー ──────────────────────────────────────────────────────
        private static System.Collections.Generic.IReadOnlyList<SheetPileQuayWall.Core.Geotech.BoringLayer>
            MakeLayers(params (double n, double thickness)[] spec)
        {
            System.Collections.Generic.List<SheetPileQuayWall.Core.Geotech.BoringLayer> layers =
                new System.Collections.Generic.List<SheetPileQuayWall.Core.Geotech.BoringLayer>();
            double elevation = 0.0;
            for (int i = 0; i < spec.Length; i++)
            {
                double top = elevation;
                double bottom = elevation - spec[i].thickness;
                layers.Add(new SheetPileQuayWall.Core.Geotech.BoringLayer
                {
                    SoilType = SheetPileQuayWall.Core.FrontWall.JetLayerType.SandGravel,
                    ElevationTopM = top,
                    ElevationBottomM = bottom,
                    ThicknessM = spec[i].thickness,
                    NValue = spec[i].n
                });
                elevation = bottom;
            }
            return layers;
        }
    }
}
