// T1160〜T1174: AnchorPileCsvImporter の単体テスト
// AutoCAD 非依存のため xUnit で直接実行可能

namespace SheetPileQuayWall.Core.Tests
{
    public class AnchorPileImportTests
    {
        // T1160: 全列が揃っていれば AnchorInput が正しく埋まる(mm→m 変換込み)
        [Xunit.Fact]
        public void T1160_Parse_AllColumnsPresent_FillsAnchorInput()
        {
            string csv = "outer_d_mm,wall_t_mm,length_m,incl_deg,closed_tip,span," +
                "tie_elev,tip_elev,color,pos_y\n" +
                "800,12,20.0,0.0,0,10.000,2.500,-18.0,8,0.0\n";

            SheetPileQuayWall.Core.Import.ImportResult<SheetPileQuayWall.Core.Import.AnchorPileImportRow> r =
                SheetPileQuayWall.Core.Import.AnchorPileCsvImporter.Parse(csv);

            Xunit.Assert.Empty(r.Errors);
            Xunit.Assert.Single(r.Rows);
            SheetPileQuayWall.Core.AnchorPile.AnchorInput a = r.Rows[0].Input;
            Xunit.Assert.Equal(0.800, a.OuterDm, 3);
            Xunit.Assert.Equal(0.012, a.WallTm, 3);
            Xunit.Assert.Equal(20.0, a.LengthM, 3);
            Xunit.Assert.False(a.ClosedTip);
            Xunit.Assert.Equal(10.000, a.SpanM, 3);
        }

        // T1161: 外径は JIS 標準径へ自動スナップされる(_Create と同じ挙動)
        [Xunit.Fact]
        public void T1161_Parse_SnapsOuterDiameterToJisStandard()
        {
            string csv = "outer_d_mm,wall_t_mm,length_m,incl_deg,closed_tip,span," +
                "tie_elev,tip_elev,color,pos_y\n" +
                "810,12,20.0,0.0,0,10.000,2.500,-18.0,8,0.0\n";

            SheetPileQuayWall.Core.Import.ImportResult<SheetPileQuayWall.Core.Import.AnchorPileImportRow> r =
                SheetPileQuayWall.Core.Import.AnchorPileCsvImporter.Parse(csv);

            Xunit.Assert.Empty(r.Errors);
            Xunit.Assert.Equal(0.800, r.Rows[0].Input.OuterDm, 3);
        }

        // T1162: 閉端の表記ゆれ("閉端"/"1"/"true"/"closed")をすべて true と解釈する
        [Xunit.Theory]
        [Xunit.InlineData("閉端")]
        [Xunit.InlineData("1")]
        [Xunit.InlineData("true")]
        [Xunit.InlineData("closed")]
        public void T1162_Parse_ClosedTipVariants_AllParseToTrue(string closedTipText)
        {
            string csv = "outer_d_mm,wall_t_mm,length_m,incl_deg,closed_tip,span," +
                "tie_elev,tip_elev,color,pos_y\n" +
                $"800,12,20.0,0.0,{closedTipText},10.000,2.500,-18.0,8,0.0\n";

            SheetPileQuayWall.Core.Import.ImportResult<SheetPileQuayWall.Core.Import.AnchorPileImportRow> r =
                SheetPileQuayWall.Core.Import.AnchorPileCsvImporter.Parse(csv);

            Xunit.Assert.Empty(r.Errors);
            Xunit.Assert.True(r.Rows[0].Input.ClosedTip);
        }

        // T1163: 肉厚が(スナップ後の)外径に対する K011 製造範囲外なら AnchorPileSteel.ValidateT
        //        が検出する。外径は SnapToJis により常に JIS 標準径(= D_Min〜D_Max 内)に
        //        丸まるため、外径そのものの範囲外エラーは原理的に発生しない。
        [Xunit.Fact]
        public void T1163_Parse_WallThicknessOutOfManufacturingRange_ProducesError()
        {
            string csv = "outer_d_mm,wall_t_mm,length_m,incl_deg,closed_tip,span," +
                "tie_elev,tip_elev,color,pos_y\n" +
                "800,200,20.0,0.0,0,10.000,2.500,-18.0,8,0.0\n";

            SheetPileQuayWall.Core.Import.ImportResult<SheetPileQuayWall.Core.Import.AnchorPileImportRow> r =
                SheetPileQuayWall.Core.Import.AnchorPileCsvImporter.Parse(csv);

            Xunit.Assert.Empty(r.Rows);
            Xunit.Assert.Single(r.Errors);
            Xunit.Assert.Contains("肉厚", r.Errors[0].Message);
        }

        // T1164: 必須列の欠落はエラー(既定値で埋めない)
        [Xunit.Fact]
        public void T1164_Parse_MissingRequiredColumn_ProducesError()
        {
            string csv = "wall_t_mm,length_m,incl_deg,closed_tip,span,tie_elev,tip_elev,color,pos_y\n" +
                "12,20.0,0.0,0,10.000,2.500,-18.0,8,0.0\n";

            SheetPileQuayWall.Core.Import.ImportResult<SheetPileQuayWall.Core.Import.AnchorPileImportRow> r =
                SheetPileQuayWall.Core.Import.AnchorPileCsvImporter.Parse(csv);

            Xunit.Assert.Empty(r.Rows);
            Xunit.Assert.Single(r.Errors);
            Xunit.Assert.Contains("外径", r.Errors[0].Message);
        }

        // T1165: 前壁とのクロスチェック(AnchorAlignment.Validate)はここでは行わない
        //        (前壁選択前のため)。span が前壁次第で不整合になり得る値でも
        //        単体検証さえ通れば取り込まれる。
        [Xunit.Fact]
        public void T1165_Parse_DoesNotPerformFrontWallCrossCheck()
        {
            string csv = "outer_d_mm,wall_t_mm,length_m,incl_deg,closed_tip,span," +
                "tie_elev,tip_elev,color,pos_y\n" +
                "800,12,20.0,0.0,0,3.000,2.500,-18.0,8,0.0\n"; // span=3.0m は前壁次第で干渉し得る

            SheetPileQuayWall.Core.Import.ImportResult<SheetPileQuayWall.Core.Import.AnchorPileImportRow> r =
                SheetPileQuayWall.Core.Import.AnchorPileCsvImporter.Parse(csv);

            Xunit.Assert.Empty(r.Errors);
            Xunit.Assert.Single(r.Rows);
        }

        // T1166: 複数行を一括で取り込める
        [Xunit.Fact]
        public void T1166_Parse_MultipleRows_AllImported()
        {
            string csv = "outer_d_mm,wall_t_mm,length_m,incl_deg,closed_tip,span," +
                "tie_elev,tip_elev,color,pos_y\n" +
                "800,12,20.0,0.0,0,10.000,2.500,-18.0,8,0.0\n" +
                "900,14,22.0,0.0,1,12.000,2.500,-20.0,8,2.6256\n";

            SheetPileQuayWall.Core.Import.ImportResult<SheetPileQuayWall.Core.Import.AnchorPileImportRow> r =
                SheetPileQuayWall.Core.Import.AnchorPileCsvImporter.Parse(csv);

            Xunit.Assert.Empty(r.Errors);
            Xunit.Assert.Equal(2, r.Rows.Count);
            Xunit.Assert.True(r.Rows[1].Input.ClosedTip);

            // 位置 Y が行ごとに読まれる(旧版は列自体が無く全行が重なっていた)
            Xunit.Assert.Equal(0.0, r.Rows[0].Input.PositionY, 4);
            Xunit.Assert.Equal(2.6256, r.Rows[1].Input.PositionY, 4);
        }

        // T1167: 位置 Y の列が無ければエラー。省略を許すと全行が同一座標に重なるため
        //        必須にしている(README §9.2 の 7 の解消)
        [Xunit.Fact]
        public void T1167_Parse_MissingPositionY_ProducesError()
        {
            string csv = "outer_d_mm,wall_t_mm,length_m,incl_deg,closed_tip,span," +
                "tie_elev,tip_elev,color\n" +
                "800,12,20.0,0.0,0,10.000,2.500,-18.0,8\n";

            SheetPileQuayWall.Core.Import.ImportResult<SheetPileQuayWall.Core.Import.AnchorPileImportRow> r =
                SheetPileQuayWall.Core.Import.AnchorPileCsvImporter.Parse(csv);

            Xunit.Assert.Empty(r.Rows);
            Xunit.Assert.Single(r.Errors);
            Xunit.Assert.Contains("位置 Y", r.Errors[0].Message);
        }

        // T1168: 位置 Y の別名(Y / 位置y)も解決できる(タイロッドと同じ別名リスト)
        [Xunit.Theory]
        [Xunit.InlineData("pos_y")]
        [Xunit.InlineData("Y")]
        [Xunit.InlineData("位置y")]
        public void T1168_Parse_PositionYAliases_AllResolve(string columnName)
        {
            string csv = "outer_d_mm,wall_t_mm,length_m,incl_deg,closed_tip,span," +
                $"tie_elev,tip_elev,color,{columnName}\n" +
                "800,12,20.0,0.0,0,10.000,2.500,-18.0,8,7.8768\n";

            SheetPileQuayWall.Core.Import.ImportResult<SheetPileQuayWall.Core.Import.AnchorPileImportRow> r =
                SheetPileQuayWall.Core.Import.AnchorPileCsvImporter.Parse(csv);

            Xunit.Assert.Empty(r.Errors);
            Xunit.Assert.Equal(7.8768, r.Rows[0].Input.PositionY, 4);
        }
    }
}
