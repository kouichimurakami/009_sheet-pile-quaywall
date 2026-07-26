// T1140〜T1154: TieRodCsvImporter の単体テスト
// AutoCAD 非依存のため xUnit で直接実行可能

namespace SheetPileQuayWall.Core.Tests
{
    public class TieRodImportTests
    {
        private const string ValidRow =
            "rod_d,grade,code,state,span_length,pile_d,pile_pitch,tie_spacing,tie_count," +
            "hwl,tie_elev,waling_h,plate_t,washer_t,nut_h,adjust_l,anchor_reaction,color,pos_y\n" +
            "0.048,HT690,PartialFactor,Normal,10.000,1.000,1.200,2.400,1," +
            "2.000,2.500,0.300,0.025,0.006,0.055,0.055,0.0,30,5.000\n";

        // T1140: 全列が揃っていれば TieRodParameters の全フィールドが正しく埋まる
        [Xunit.Fact]
        public void T1140_Parse_AllColumnsPresent_FillsAllFields()
        {
            SheetPileQuayWall.Core.Import.ImportResult<SheetPileQuayWall.Core.Import.TieRodImportRow> r =
                SheetPileQuayWall.Core.Import.TieRodCsvImporter.Parse(ValidRow);

            Xunit.Assert.Empty(r.Errors);
            Xunit.Assert.Single(r.Rows);
            SheetPileQuayWall.Core.Import.TieRodImportRow row = r.Rows[0];
            Xunit.Assert.Equal(0.048, row.Parameters.RodDiameter, 3);
            Xunit.Assert.Equal(SheetPileQuayWall.Core.TieRod.SteelGrade.HT690, row.Parameters.Grade);
            Xunit.Assert.Equal(SheetPileQuayWall.Core.TieRod.DesignCode.PartialFactor, row.Parameters.Code);
            Xunit.Assert.Equal(SheetPileQuayWall.Core.TieRod.LoadState.Normal, row.Parameters.State);
            Xunit.Assert.Equal(10.000, row.Parameters.SpanLength, 3);
            Xunit.Assert.Equal(5.000, row.PositionY, 3);
        }

        // T1141: 鋼種・設計基準・荷重状態は列名の大小文字を無視して解釈できる
        [Xunit.Fact]
        public void T1141_Parse_EnumValuesAreCaseInsensitive()
        {
            string csv = ValidRow.Replace("HT690", "ht690").Replace("PartialFactor", "partialfactor");

            SheetPileQuayWall.Core.Import.ImportResult<SheetPileQuayWall.Core.Import.TieRodImportRow> r =
                SheetPileQuayWall.Core.Import.TieRodCsvImporter.Parse(csv);

            Xunit.Assert.Empty(r.Errors);
            Xunit.Assert.Equal(SheetPileQuayWall.Core.TieRod.SteelGrade.HT690, r.Rows[0].Parameters.Grade);
            Xunit.Assert.Equal(
                SheetPileQuayWall.Core.TieRod.DesignCode.PartialFactor, r.Rows[0].Parameters.Code);
        }

        // T1142: 位置 Y の列が無ければエラー(前壁からの自動計算はできないため必須)
        [Xunit.Fact]
        public void T1142_Parse_MissingPositionY_ProducesError()
        {
            string csv =
                "rod_d,grade,code,state,span_length,pile_d,pile_pitch,tie_spacing,tie_count," +
                "hwl,tie_elev,waling_h,plate_t,washer_t,nut_h,adjust_l,anchor_reaction,color\n" +
                "0.048,HT690,PartialFactor,Normal,10.000,1.000,1.200,2.400,1," +
                "2.000,2.500,0.300,0.025,0.006,0.055,0.055,0.0,30\n";

            SheetPileQuayWall.Core.Import.ImportResult<SheetPileQuayWall.Core.Import.TieRodImportRow> r =
                SheetPileQuayWall.Core.Import.TieRodCsvImporter.Parse(csv);

            Xunit.Assert.Empty(r.Rows);
            Xunit.Assert.Single(r.Errors);
            Xunit.Assert.Contains("位置 Y", r.Errors[0].Message);
        }

        // T1143: 必須列が欠けている場合はエラー(列不足を無視して既定値で埋めない)
        [Xunit.Fact]
        public void T1143_Parse_MissingRequiredColumn_ProducesError()
        {
            string csv = "grade,code,state,span_length,pile_d,pile_pitch,tie_spacing,tie_count," +
                "hwl,tie_elev,waling_h,plate_t,washer_t,nut_h,adjust_l,anchor_reaction,color,pos_y\n" +
                "HT690,PartialFactor,Normal,10.000,1.000,1.200,2.400,1," +
                "2.000,2.500,0.300,0.025,0.006,0.055,0.055,0.0,30,5.000\n";

            SheetPileQuayWall.Core.Import.ImportResult<SheetPileQuayWall.Core.Import.TieRodImportRow> r =
                SheetPileQuayWall.Core.Import.TieRodCsvImporter.Parse(csv);

            Xunit.Assert.Empty(r.Rows);
            Xunit.Assert.Single(r.Errors);
            Xunit.Assert.Contains("タイロッド径", r.Errors[0].Message);
        }

        // T1144: 既存の TieRodParameters.Validate() の検証がそのまま効く
        //        (カタログ規格外径・矢板ピッチ不整合など)。ここでは規格外径を検証。
        //        0.049 はカタログ 19 種(0.048, 0.050 等)のいずれにも一致しない。
        [Xunit.Fact]
        public void T1144_Parse_InvalidCatalogDiameter_ProducesValidateError()
        {
            string csv = ValidRow.Replace("0.048,HT690", "0.049,HT690");

            SheetPileQuayWall.Core.Import.ImportResult<SheetPileQuayWall.Core.Import.TieRodImportRow> r =
                SheetPileQuayWall.Core.Import.TieRodCsvImporter.Parse(csv);

            Xunit.Assert.Empty(r.Rows);
            Xunit.Assert.Single(r.Errors);
            Xunit.Assert.Contains("カタログ規格径", r.Errors[0].Message);
        }

        // T1145: mm 混入(タイロッド径に 48 を入れる典型ミス)は Validate() が検出する
        [Xunit.Fact]
        public void T1145_Parse_MillimeterMixupInRodDiameter_IsDetected()
        {
            string csv = ValidRow.Replace("0.048,HT690", "48,HT690");

            SheetPileQuayWall.Core.Import.ImportResult<SheetPileQuayWall.Core.Import.TieRodImportRow> r =
                SheetPileQuayWall.Core.Import.TieRodCsvImporter.Parse(csv);

            Xunit.Assert.Empty(r.Rows);
            Xunit.Assert.Single(r.Errors);
        }

        // T1146: 複数行(異なる Y の組)を一括で取り込める
        [Xunit.Fact]
        public void T1146_Parse_MultipleRows_AllImported()
        {
            string csv =
                "rod_d,grade,code,state,span_length,pile_d,pile_pitch,tie_spacing,tie_count," +
                "hwl,tie_elev,waling_h,plate_t,washer_t,nut_h,adjust_l,anchor_reaction,color,pos_y\n" +
                "0.048,HT690,PartialFactor,Normal,10.000,1.000,1.200,2.400,1," +
                "2.000,2.500,0.300,0.025,0.006,0.055,0.055,0.0,30,0.000\n" +
                "0.048,HT690,PartialFactor,Normal,10.000,1.000,1.200,2.400,1," +
                "2.000,2.500,0.300,0.025,0.006,0.055,0.055,0.0,30,2.400\n";

            SheetPileQuayWall.Core.Import.ImportResult<SheetPileQuayWall.Core.Import.TieRodImportRow> r =
                SheetPileQuayWall.Core.Import.TieRodCsvImporter.Parse(csv);

            Xunit.Assert.Empty(r.Errors);
            Xunit.Assert.Equal(2, r.Rows.Count);
            Xunit.Assert.Equal(0.000, r.Rows[0].PositionY, 3);
            Xunit.Assert.Equal(2.400, r.Rows[1].PositionY, 3);
        }
    }
}
