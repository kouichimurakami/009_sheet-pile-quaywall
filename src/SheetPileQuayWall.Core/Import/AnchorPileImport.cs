// AutoCAD 非依存 — xUnit で単体テスト可能
//
// 控え杭の帳票 CSV 取り込み。1 行 = 控え杭 1 本。
//
// 前壁との整合(AnchorAlignment.Validate)は前壁選択後でないと検証できないため、
// ここでは単体で完結する検証(外径・肉厚・全長の JIS/K011 範囲)のみ行う。
// 前壁とのクロスチェックは呼び出し側(Plugin)が前壁選択後に行うこと。
//
// 単位: 外径・肉厚は決定7 の境界に合わせ mm 入力、取り込み時に m へ変換し
// JIS A 5525 標準径へスナップする(_Create と同じ挙動)。

namespace SheetPileQuayWall.Core.Import
{
    public sealed class AnchorPileImportRow
    {
        public int RowNumber;
        public SheetPileQuayWall.Core.AnchorPile.AnchorInput Input =
            new SheetPileQuayWall.Core.AnchorPile.AnchorInput
            {
                OuterDm = 0.800, WallTm = 0.012, LengthM = 20.0, InclDeg = 0.0,
                ClosedTip = false, SpanM = 10.0, TieElevM = 2.5, TipElevM = -18.0,
                ColorIdx = 8
            };
    }

    public static class AnchorPileCsvImporter
    {
        private static readonly string[] OuterDAliases = { "outer_d_mm", "外径" };
        private static readonly string[] WallTAliases = { "wall_t_mm", "肉厚" };
        private static readonly string[] LengthAliases = { "length_m", "全長" };
        private static readonly string[] InclAliases = { "incl_deg", "傾斜角" };
        private static readonly string[] ClosedTipAliases = { "closed_tip", "先端形状" };
        private static readonly string[] SpanAliases = { "span", "法線直角方向延長" };
        private static readonly string[] TieElevAliases = { "tie_elev", "タイロッド軸心標高" };
        private static readonly string[] TipElevAliases = { "tip_elev", "杭先端標高" };
        private static readonly string[] ColorAliases = { "color", "色" };
        private static readonly string[] PosYAliases = { "pos_y", "y", "位置y" };

        private static readonly System.Globalization.CultureInfo Inv =
            System.Globalization.CultureInfo.InvariantCulture;

        public static ImportResult<AnchorPileImportRow> Parse(string csvText)
        {
            CsvTable table = CsvTable.Parse(csvText);

            System.Collections.Generic.List<AnchorPileImportRow> rows =
                new System.Collections.Generic.List<AnchorPileImportRow>();
            System.Collections.Generic.List<ImportRowError> errors =
                new System.Collections.Generic.List<ImportRowError>();

            for (int i = 0; i < table.Rows.Count; i++)
            {
                int rowNumber = i + 2;
                System.Collections.Generic.IReadOnlyDictionary<string, string> row = table.Rows[i];
                System.Collections.Generic.List<string> rowErrors =
                    new System.Collections.Generic.List<string>();

                AnchorPileImportRow r = new AnchorPileImportRow { RowNumber = rowNumber };
                SheetPileQuayWall.Core.AnchorPile.AnchorInput a = r.Input;

                double? outerDMm = ReadReal(row, OuterDAliases, rowErrors, "外径 D");
                if (outerDMm != null)
                {
                    a.OuterDm = SheetPileQuayWall.Core.AnchorPile.AnchorPileSteel.SnapToJis(
                        outerDMm.Value / 1000.0);
                }

                double? wallTMm = ReadReal(row, WallTAliases, rowErrors, "肉厚 t");
                if (wallTMm != null)
                {
                    a.WallTm = wallTMm.Value / 1000.0;
                }

                double? lengthM = ReadReal(row, LengthAliases, rowErrors, "全長 L");
                if (lengthM != null) { a.LengthM = lengthM.Value; }

                double? inclDeg = ReadRealOptional(row, InclAliases);
                if (inclDeg != null) { a.InclDeg = inclDeg.Value; }

                string closedTipText;
                if (CsvTable.TryGetField(row, ClosedTipAliases, out closedTipText))
                {
                    a.ClosedTip = closedTipText == "1" ||
                        closedTipText.Equals("閉端", System.StringComparison.Ordinal) ||
                        closedTipText.Equals("true", System.StringComparison.OrdinalIgnoreCase) ||
                        closedTipText.Equals("closed", System.StringComparison.OrdinalIgnoreCase);
                }

                double? span = ReadReal(row, SpanAliases, rowErrors, "法線直角方向延長 span");
                if (span != null) { a.SpanM = span.Value; }

                double? tieElev = ReadReal(row, TieElevAliases, rowErrors, "タイロッド軸心標高 Z_tr");
                if (tieElev != null) { a.TieElevM = tieElev.Value; }

                double? tipElev = ReadReal(row, TipElevAliases, rowErrors, "杭先端標高 Z_tip");
                if (tipElev != null) { a.TipElevM = tipElev.Value; }

                string colorText;
                if (CsvTable.TryGetField(row, ColorAliases, out colorText))
                {
                    int color;
                    a.ColorIdx = int.TryParse(colorText, System.Globalization.NumberStyles.Integer,
                        Inv, out color) ? color : 8;
                }

                // 位置 Y はタイロッドと同じく必須。前壁からの相対位置は自動計算できず、
                // 省略すると全行が同一座標に重なるため (README §9.2 の 7 の解消)
                string posYText;
                if (CsvTable.TryGetField(row, PosYAliases, out posYText))
                {
                    double posY;
                    if (double.TryParse(posYText, System.Globalization.NumberStyles.Float,
                        Inv, out posY))
                    {
                        a.PositionY = posY;
                    }
                    else
                    {
                        rowErrors.Add($"位置 Y の値 '{posYText}' を数値として解釈できません。");
                    }
                }
                else
                {
                    rowErrors.Add(
                        "位置 Y を読み取れません(列 pos_y/Y)。省略すると全行が同一座標に重なるため必須。");
                }

                if (rowErrors.Count == 0)
                {
                    string? errD = SheetPileQuayWall.Core.AnchorPile.AnchorPileSteel.ValidateD(a.OuterDm);
                    if (errD != null) { rowErrors.Add(errD); }

                    string? errT = SheetPileQuayWall.Core.AnchorPile.AnchorPileSteel.ValidateT(
                        a.WallTm, a.OuterDm);
                    if (errT != null) { rowErrors.Add(errT); }

                    string? errL = SheetPileQuayWall.Core.AnchorPile.AnchorPileSteel.ValidateL(a.LengthM);
                    if (errL != null) { rowErrors.Add(errL); }
                }

                if (rowErrors.Count > 0)
                {
                    errors.Add(new ImportRowError(rowNumber, string.Join("; ", rowErrors)));
                }
                else
                {
                    rows.Add(r);
                }
            }

            return new ImportResult<AnchorPileImportRow>(rows, errors);
        }

        private static double? ReadReal(
            System.Collections.Generic.IReadOnlyDictionary<string, string> row,
            string[] aliases, System.Collections.Generic.List<string> rowErrors, string label)
        {
            string text;
            if (!CsvTable.TryGetField(row, aliases, out text))
            {
                rowErrors.Add($"{label} を読み取れません(列が見つかりません)。");
                return null;
            }
            double value;
            if (!double.TryParse(text, System.Globalization.NumberStyles.Float, Inv, out value))
            {
                rowErrors.Add($"{label} の値 '{text}' を数値として解釈できません。");
                return null;
            }
            return value;
        }

        private static double? ReadRealOptional(
            System.Collections.Generic.IReadOnlyDictionary<string, string> row, string[] aliases)
        {
            string text;
            if (!CsvTable.TryGetField(row, aliases, out text))
            {
                return null;
            }
            double value;
            return double.TryParse(text, System.Globalization.NumberStyles.Float, Inv, out value)
                ? value : (double?)null;
        }
    }
}
