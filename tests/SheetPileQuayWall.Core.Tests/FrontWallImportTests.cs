// T1120〜T1139: FrontWallCsvImporter の単体テスト
// AutoCAD 非依存のため xUnit で直接実行可能

namespace SheetPileQuayWall.Core.Tests
{
    public class FrontWallImportTests
    {
        // T1120: 個別列がすべて揃っていれば正しく読み取れる(mm→m 変換込み)
        [Xunit.Fact]
        public void T1120_Parse_AllColumnsPresent_ReadsCorrectly()
        {
            string csv = "outer_d_mm,wall_t_mm,length_m,joint,grade,incl_deg,color,tip_z\n" +
                "800,12,20.0,LT75,SKY400,0.0,8,-18.0\n";

            SheetPileQuayWall.Core.Import.ImportResult<SheetPileQuayWall.Core.Import.FrontWallImportRow> r =
                SheetPileQuayWall.Core.Import.FrontWallCsvImporter.Parse(csv);

            Xunit.Assert.Empty(r.Errors);
            Xunit.Assert.Single(r.Rows);
            SheetPileQuayWall.Core.Import.FrontWallImportRow row = r.Rows[0];
            Xunit.Assert.Equal(0.800, row.OuterDm, 3);
            Xunit.Assert.Equal(0.012, row.WallTm, 3);
            Xunit.Assert.Equal(20.0, row.LengthM, 3);
            Xunit.Assert.Equal("LT75", row.JointCode);
            Xunit.Assert.Equal(-18.0, row.TipZ, 3);
        }

        // T1121: piece_count/piece_index 列が無い場合、行数・出現順で自動採番される
        [Xunit.Fact]
        public void T1121_Parse_NoPieceColumns_AutoNumbersByRowOrder()
        {
            string csv = "outer_d_mm,wall_t_mm,length_m,joint,tip_z\n" +
                "800,12,20.0,LT75,-18.0\n" +
                "800,12,20.0,LT75,-18.0\n" +
                "800,12,20.0,LT75,-18.0\n";

            SheetPileQuayWall.Core.Import.ImportResult<SheetPileQuayWall.Core.Import.FrontWallImportRow> r =
                SheetPileQuayWall.Core.Import.FrontWallCsvImporter.Parse(csv);

            Xunit.Assert.Empty(r.Errors);
            Xunit.Assert.Equal(3, r.Rows.Count);
            Xunit.Assert.Equal(3, r.Rows[0].PieceCount);
            Xunit.Assert.Equal(3, r.Rows[1].PieceCount);
            Xunit.Assert.Equal(3, r.Rows[2].PieceCount);
            Xunit.Assert.Equal(1, r.Rows[0].PieceIndex);
            Xunit.Assert.Equal(2, r.Rows[1].PieceIndex);
            Xunit.Assert.Equal(3, r.Rows[2].PieceIndex);
        }

        // T1122: piece_count/piece_index 列が明示されていれば、それを優先する
        [Xunit.Fact]
        public void T1122_Parse_ExplicitPieceColumns_OverrideAutoNumbering()
        {
            string csv = "outer_d_mm,wall_t_mm,length_m,joint,tip_z,piece_count,piece_index\n" +
                "800,12,20.0,LT75,-18.0,5,3\n";

            SheetPileQuayWall.Core.Import.ImportResult<SheetPileQuayWall.Core.Import.FrontWallImportRow> r =
                SheetPileQuayWall.Core.Import.FrontWallCsvImporter.Parse(csv);

            Xunit.Assert.Empty(r.Errors);
            Xunit.Assert.Equal(5, r.Rows[0].PieceCount);
            Xunit.Assert.Equal(3, r.Rows[0].PieceIndex);
        }

        // T1123: 「規格」列があれば φNNN×t / L=NN.N / 継手コードを抽出する(個別列が無い場合)
        [Xunit.Fact]
        public void T1123_Parse_FallsBackToSpecTextColumn()
        {
            string csv = "規格,tip_z\n" +
                "\"φ800×12 L=20.0m LT75\",-18.0\n";

            SheetPileQuayWall.Core.Import.ImportResult<SheetPileQuayWall.Core.Import.FrontWallImportRow> r =
                SheetPileQuayWall.Core.Import.FrontWallCsvImporter.Parse(csv);

            Xunit.Assert.Empty(r.Errors);
            SheetPileQuayWall.Core.Import.FrontWallImportRow row = r.Rows[0];
            Xunit.Assert.Equal(0.800, row.OuterDm, 3);
            Xunit.Assert.Equal(0.012, row.WallTm, 3);
            Xunit.Assert.Equal(20.0, row.LengthM, 3);
            Xunit.Assert.Equal("LT75", row.JointCode);
        }

        // T1124: 未知の継手コードはエラー行になる
        [Xunit.Fact]
        public void T1124_Parse_UnknownJointCode_ProducesError()
        {
            string csv = "outer_d_mm,wall_t_mm,length_m,joint,tip_z\n" +
                "800,12,20.0,XYZ99,-18.0\n";

            SheetPileQuayWall.Core.Import.ImportResult<SheetPileQuayWall.Core.Import.FrontWallImportRow> r =
                SheetPileQuayWall.Core.Import.FrontWallCsvImporter.Parse(csv);

            Xunit.Assert.Empty(r.Rows);
            Xunit.Assert.Single(r.Errors);
            Xunit.Assert.Equal(2, r.Errors[0].RowNumber);
            Xunit.Assert.Contains("継手形式", r.Errors[0].Message);
        }

        // T1125: 杭先端標高の列が無ければエラー(帳票側での必須項目)
        [Xunit.Fact]
        public void T1125_Parse_MissingTipZ_ProducesError()
        {
            string csv = "outer_d_mm,wall_t_mm,length_m,joint\n800,12,20.0,LT75\n";

            SheetPileQuayWall.Core.Import.ImportResult<SheetPileQuayWall.Core.Import.FrontWallImportRow> r =
                SheetPileQuayWall.Core.Import.FrontWallCsvImporter.Parse(csv);

            Xunit.Assert.Empty(r.Rows);
            Xunit.Assert.Single(r.Errors);
            Xunit.Assert.Contains("杭先端標高", r.Errors[0].Message);
        }

        // T1126: 外径が K011 範囲外(mm 混入のような値)は InputValidator により検出される
        [Xunit.Fact]
        public void T1126_Parse_OuterDiameterOutOfRange_ProducesError()
        {
            string csv = "outer_d_mm,wall_t_mm,length_m,joint,tip_z\n" +
                "3000,12,20.0,LT75,-18.0\n";

            SheetPileQuayWall.Core.Import.ImportResult<SheetPileQuayWall.Core.Import.FrontWallImportRow> r =
                SheetPileQuayWall.Core.Import.FrontWallCsvImporter.Parse(csv);

            Xunit.Assert.Empty(r.Rows);
            Xunit.Assert.Single(r.Errors);
            Xunit.Assert.Contains("外径", r.Errors[0].Message);
        }

        // T1127: 1 行だけ不備があっても、残りの行は正常に取り込まれる(部分失敗)
        [Xunit.Fact]
        public void T1127_Parse_OneBadRow_DoesNotBlockOthers()
        {
            string csv = "outer_d_mm,wall_t_mm,length_m,joint,tip_z\n" +
                "800,12,20.0,LT75,-18.0\n" +
                "800,12,20.0,BADCODE,-18.0\n" +
                "900,12,22.0,LT100,-20.0\n";

            SheetPileQuayWall.Core.Import.ImportResult<SheetPileQuayWall.Core.Import.FrontWallImportRow> r =
                SheetPileQuayWall.Core.Import.FrontWallCsvImporter.Parse(csv);

            Xunit.Assert.Equal(2, r.Rows.Count);
            Xunit.Assert.Single(r.Errors);
            Xunit.Assert.Equal(3, r.Errors[0].RowNumber);
            // 自動採番は「CSV に記載された総本数」(3)を基準にする。失敗した行が
            // 抜けても、生き残った行の施工順位(1本目・3本目)は元の位置を保つ
            // ため、継手要否判定(先頭/末尾)がずれない。
            Xunit.Assert.Equal(3, r.Rows[0].PieceCount);
            Xunit.Assert.Equal(1, r.Rows[0].PieceIndex);
            Xunit.Assert.Equal(3, r.Rows[1].PieceIndex);
        }

        // T1128: 継手コード列が小文字でも大文字に正規化される
        [Xunit.Fact]
        public void T1128_Parse_JointCodeIsNormalizedToUppercase()
        {
            string csv = "outer_d_mm,wall_t_mm,length_m,joint,tip_z\n" +
                "800,12,20.0,lt75,-18.0\n";

            SheetPileQuayWall.Core.Import.ImportResult<SheetPileQuayWall.Core.Import.FrontWallImportRow> r =
                SheetPileQuayWall.Core.Import.FrontWallCsvImporter.Parse(csv);

            Xunit.Assert.Empty(r.Errors);
            Xunit.Assert.Equal("LT75", r.Rows[0].JointCode);
        }

        // T1129: 数値として解釈できない値はエラーになる(単位取り違えの検出)
        [Xunit.Fact]
        public void T1129_Parse_NonNumericLength_ProducesError()
        {
            string csv = "outer_d_mm,wall_t_mm,length_m,joint,tip_z\n" +
                "800,12,twenty,LT75,-18.0\n";

            SheetPileQuayWall.Core.Import.ImportResult<SheetPileQuayWall.Core.Import.FrontWallImportRow> r =
                SheetPileQuayWall.Core.Import.FrontWallCsvImporter.Parse(csv);

            Xunit.Assert.Empty(r.Rows);
            Xunit.Assert.Single(r.Errors);
            Xunit.Assert.Contains("全長", r.Errors[0].Message);
        }
    }
}
