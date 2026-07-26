// T1100〜T1119: CsvTable の単体テスト
// AutoCAD 非依存のため xUnit で直接実行可能

namespace SheetPileQuayWall.Core.Tests
{
    public class CsvTableTests
    {
        // T1100: 基本の見出し+2行
        [Xunit.Fact]
        public void T1100_Parse_BasicHeaderAndRows()
        {
            string csv = "a,b,c\n1,2,3\n4,5,6\n";
            SheetPileQuayWall.Core.Import.CsvTable table =
                SheetPileQuayWall.Core.Import.CsvTable.Parse(csv);

            Xunit.Assert.Equal(3, table.Headers.Count);
            Xunit.Assert.Equal(2, table.Rows.Count);
            Xunit.Assert.Equal("1", table.Rows[0]["a"]);
            Xunit.Assert.Equal("6", table.Rows[1]["c"]);
        }

        // T1101: ヘッダー参照は大文字小文字を無視する
        [Xunit.Fact]
        public void T1101_Parse_HeaderLookupIsCaseInsensitive()
        {
            string csv = "Outer_D,Wall_T\n800,12\n";
            SheetPileQuayWall.Core.Import.CsvTable table =
                SheetPileQuayWall.Core.Import.CsvTable.Parse(csv);

            Xunit.Assert.Equal("800", table.Rows[0]["outer_d"]);
            Xunit.Assert.Equal("12", table.Rows[0]["WALL_T"]);
        }

        // T1102: ダブルクォート内のカンマはフィールド区切りにならない
        [Xunit.Fact]
        public void T1102_Parse_QuotedFieldWithComma()
        {
            string csv = "name,spec\n\"矢板A\",\"φ800×12, LT75\"\n";
            SheetPileQuayWall.Core.Import.CsvTable table =
                SheetPileQuayWall.Core.Import.CsvTable.Parse(csv);

            Xunit.Assert.Equal("矢板A", table.Rows[0]["name"]);
            Xunit.Assert.Equal("φ800×12, LT75", table.Rows[0]["spec"]);
        }

        // T1103: "" によるダブルクォートのエスケープ
        [Xunit.Fact]
        public void T1103_Parse_EscapedQuote()
        {
            string csv = "note\n\"外径は \"\"800mm\"\" です\"\n";
            SheetPileQuayWall.Core.Import.CsvTable table =
                SheetPileQuayWall.Core.Import.CsvTable.Parse(csv);

            Xunit.Assert.Equal("外径は \"800mm\" です", table.Rows[0]["note"]);
        }

        // T1104: CRLF / LF が混在しても行が正しく分割される
        [Xunit.Fact]
        public void T1104_Parse_MixedLineEndings()
        {
            string csv = "a,b\r\n1,2\n3,4\r\n";
            SheetPileQuayWall.Core.Import.CsvTable table =
                SheetPileQuayWall.Core.Import.CsvTable.Parse(csv);

            Xunit.Assert.Equal(2, table.Rows.Count);
            Xunit.Assert.Equal("2", table.Rows[0]["b"]);
            Xunit.Assert.Equal("4", table.Rows[1]["b"]);
        }

        // T1105: 先頭 BOM は除去される
        [Xunit.Fact]
        public void T1105_Parse_StripsLeadingBom()
        {
            string csv = "﻿a,b\n1,2\n";
            SheetPileQuayWall.Core.Import.CsvTable table =
                SheetPileQuayWall.Core.Import.CsvTable.Parse(csv);

            Xunit.Assert.Equal("a", table.Headers[0]);
            Xunit.Assert.Equal("1", table.Rows[0]["a"]);
        }

        // T1106: 完全な空行はスキップされる(データ行として数えない)
        [Xunit.Fact]
        public void T1106_Parse_SkipsBlankLines()
        {
            string csv = "a,b\n1,2\n\n3,4\n";
            SheetPileQuayWall.Core.Import.CsvTable table =
                SheetPileQuayWall.Core.Import.CsvTable.Parse(csv);

            Xunit.Assert.Equal(2, table.Rows.Count);
        }

        // T1107: 空セルを持つ行(",,")は空行として除外されない
        [Xunit.Fact]
        public void T1107_Parse_RowOfEmptyCellsIsNotBlank()
        {
            string csv = "a,b,c\n,,\n1,2,3\n";
            SheetPileQuayWall.Core.Import.CsvTable table =
                SheetPileQuayWall.Core.Import.CsvTable.Parse(csv);

            Xunit.Assert.Equal(2, table.Rows.Count);
            Xunit.Assert.Equal("", table.Rows[0]["a"]);
        }

        // T1108: 末尾に改行が無い最終行も読み取れる
        [Xunit.Fact]
        public void T1108_Parse_LastLineWithoutTrailingNewline()
        {
            string csv = "a,b\n1,2\n3,4";
            SheetPileQuayWall.Core.Import.CsvTable table =
                SheetPileQuayWall.Core.Import.CsvTable.Parse(csv);

            Xunit.Assert.Equal(2, table.Rows.Count);
            Xunit.Assert.Equal("4", table.Rows[1]["b"]);
        }

        // T1109: 前後の空白はトリムされる
        [Xunit.Fact]
        public void T1109_Parse_TrimsWhitespace()
        {
            string csv = " a , b \n 1 , 2 \n";
            SheetPileQuayWall.Core.Import.CsvTable table =
                SheetPileQuayWall.Core.Import.CsvTable.Parse(csv);

            Xunit.Assert.Equal("a", table.Headers[0]);
            Xunit.Assert.Equal("1", table.Rows[0]["a"]);
        }

        // T1110: TryGetField は別名リストの最初に一致した非空値を返す
        [Xunit.Fact]
        public void T1110_TryGetField_ResolvesFirstMatchingAlias()
        {
            string csv = "外径,D\n800,999\n";
            SheetPileQuayWall.Core.Import.CsvTable table =
                SheetPileQuayWall.Core.Import.CsvTable.Parse(csv);

            string value;
            bool found = SheetPileQuayWall.Core.Import.CsvTable.TryGetField(
                table.Rows[0], new[] { "外径", "D" }, out value);

            Xunit.Assert.True(found);
            Xunit.Assert.Equal("800", value);
        }

        // T1111: 空文字のセルは「値なし」として扱う(次の別名にフォールバック)
        [Xunit.Fact]
        public void T1111_TryGetField_EmptyCellFallsBackToNextAlias()
        {
            string csv = "外径,D\n,999\n";
            SheetPileQuayWall.Core.Import.CsvTable table =
                SheetPileQuayWall.Core.Import.CsvTable.Parse(csv);

            string value;
            bool found = SheetPileQuayWall.Core.Import.CsvTable.TryGetField(
                table.Rows[0], new[] { "外径", "D" }, out value);

            Xunit.Assert.True(found);
            Xunit.Assert.Equal("999", value);
        }

        // T1112: 該当列が無ければ false
        [Xunit.Fact]
        public void T1112_TryGetField_NoMatchingColumn_ReturnsFalse()
        {
            string csv = "x\n1\n";
            SheetPileQuayWall.Core.Import.CsvTable table =
                SheetPileQuayWall.Core.Import.CsvTable.Parse(csv);

            string value;
            bool found = SheetPileQuayWall.Core.Import.CsvTable.TryGetField(
                table.Rows[0], new[] { "外径", "D" }, out value);

            Xunit.Assert.False(found);
        }

        // T1113: 空文字列の入力は 0 行(ヘッダーも無し)
        [Xunit.Fact]
        public void T1113_Parse_EmptyInput_ProducesNoRows()
        {
            SheetPileQuayWall.Core.Import.CsvTable table =
                SheetPileQuayWall.Core.Import.CsvTable.Parse("");

            Xunit.Assert.Empty(table.Headers);
            Xunit.Assert.Empty(table.Rows);
        }
    }
}
