// AutoCAD 非依存 — xUnit で単体テスト可能
//
// 前壁鋼管矢板の帳票 CSV 取り込み。1 行 = 矢板 1 本。
//
// 列名は別名リストで解決する(実際のエクスポートの列名は未確認。README §9.1)。
// 個別列(outer_d_mm 等)が無い場合、「規格」列があれば SpecTextParser で
// 抽出を試みる(ベストエフォート)。
//
// 総本数・施工順位を CSV が持たない場合は、**CSV に記載された全行数**を総本数、
// **ファイル内の行の出現順**を施工順位として自動採番する(壁一括生成を意図した
// 既定動作)。他の行の不備で 1 行が取り込み失敗しても、残りの行の施工順位は
// 元のファイル内位置のまま変わらない(総本数から除外しない)。これにより
// 先頭・末尾判定(継手の要否)が失敗行の有無でずれることを防ぐ。
// 継手の要否・雌雄は既存の PieceAssignment がこの値から自動判定する。
//
// 単位: 外径・肉厚は決定7 の境界に合わせ mm 入力(帳票の慣行に合わせる)、
// 取り込み時に m へ変換する。全長・傾斜角・標高は帳票の時点で m/deg とする。

namespace SheetPileQuayWall.Core.Import
{
    public sealed class FrontWallImportRow
    {
        public int RowNumber;
        public double OuterDm;
        public double WallTm;
        public double LengthM;
        public string JointCode = "LT75";
        public string Grade = "SKY400";
        public double InclDeg;
        public int PieceCount;
        public int PieceIndex;
        public int ColorIdx = 8;
        public double TipZ = -18.0;
    }

    public static class FrontWallCsvImporter
    {
        private static readonly string[] OuterDAliases =
            { "outer_d_mm", "outer_d", "外径", "外径(mm)", "D", "呼び径" };
        private static readonly string[] WallTAliases =
            { "wall_t_mm", "wall_t", "肉厚", "肉厚(mm)", "t" };
        private static readonly string[] LengthAliases =
            { "length_m", "length", "全長", "全長(m)", "杭長", "L" };
        private static readonly string[] JointAliases =
            { "joint", "joint_code", "継手形式", "継手" };
        private static readonly string[] GradeAliases =
            { "grade", "鋼種" };
        private static readonly string[] InclAliases =
            { "incl_deg", "傾斜角" };
        private static readonly string[] PieceCountAliases =
            { "piece_count", "総本数" };
        private static readonly string[] PieceIndexAliases =
            { "piece_index", "施工順位" };
        private static readonly string[] ColorAliases =
            { "color", "色" };
        private static readonly string[] TipZAliases =
            { "tip_z", "杭先端標高", "z" };
        private static readonly string[] SpecAliases =
            { "規格", "形状寸法", "spec" };

        private static readonly System.Globalization.CultureInfo Inv =
            System.Globalization.CultureInfo.InvariantCulture;

        public static ImportResult<FrontWallImportRow> Parse(string csvText)
        {
            CsvTable table = CsvTable.Parse(csvText);

            System.Collections.Generic.List<FrontWallImportRow> rows =
                new System.Collections.Generic.List<FrontWallImportRow>();
            System.Collections.Generic.List<ImportRowError> errors =
                new System.Collections.Generic.List<ImportRowError>();

            bool anyRowHasPieceCount = false;
            bool anyRowHasPieceIndex = false;

            for (int i = 0; i < table.Rows.Count; i++)
            {
                System.Collections.Generic.IReadOnlyDictionary<string, string> row = table.Rows[i];
                string text;
                if (CsvTable.TryGetField(row, PieceCountAliases, out text)) { anyRowHasPieceCount = true; }
                if (CsvTable.TryGetField(row, PieceIndexAliases, out text)) { anyRowHasPieceIndex = true; }
            }

            for (int i = 0; i < table.Rows.Count; i++)
            {
                int rowNumber = i + 2;
                System.Collections.Generic.IReadOnlyDictionary<string, string> row = table.Rows[i];
                System.Collections.Generic.List<string> rowErrors =
                    new System.Collections.Generic.List<string>();

                string specText;
                bool hasSpec = CsvTable.TryGetField(row, SpecAliases, out specText);

                FrontWallImportRow r = new FrontWallImportRow { RowNumber = rowNumber };

                double? outerDMm = ReadMillimeterField(
                    row, OuterDAliases, hasSpec ? specText : null,
                    SpecTextParser.TryExtractOuterDiameterMm);
                if (outerDMm == null)
                {
                    rowErrors.Add("外径 D を読み取れません(列 outer_d_mm/外径、または規格列の φNNN)。");
                }
                else
                {
                    r.OuterDm = outerDMm.Value / 1000.0;
                }

                double? wallTMm = ReadMillimeterField(
                    row, WallTAliases, hasSpec ? specText : null,
                    SpecTextParser.TryExtractWallThicknessMm);
                if (wallTMm == null)
                {
                    rowErrors.Add("肉厚 t を読み取れません(列 wall_t_mm/肉厚、または規格列の ×NN)。");
                }
                else
                {
                    r.WallTm = wallTMm.Value / 1000.0;
                }

                string lengthText;
                if (CsvTable.TryGetField(row, LengthAliases, out lengthText))
                {
                    double lengthM;
                    if (double.TryParse(lengthText, System.Globalization.NumberStyles.Float, Inv, out lengthM))
                    {
                        r.LengthM = lengthM;
                    }
                    else
                    {
                        rowErrors.Add($"全長の値 '{lengthText}' を数値として解釈できません。");
                    }
                }
                else if (hasSpec)
                {
                    double? lengthFromSpec = SpecTextParser.TryExtractLengthM(specText);
                    if (lengthFromSpec != null)
                    {
                        r.LengthM = lengthFromSpec.Value;
                    }
                    else
                    {
                        rowErrors.Add("全長 L を読み取れません(列 length_m/全長、または規格列の L=NN.N)。");
                    }
                }
                else
                {
                    rowErrors.Add("全長 L を読み取れません(列 length_m/全長、または規格列の L=NN.N)。");
                }

                string jointText;
                if (CsvTable.TryGetField(row, JointAliases, out jointText))
                {
                    r.JointCode = jointText.ToUpperInvariant();
                }
                else if (hasSpec)
                {
                    r.JointCode = SpecTextParser.TryExtractJointCode(specText) ?? "LT75";
                }

                string gradeText;
                r.Grade = CsvTable.TryGetField(row, GradeAliases, out gradeText)
                    ? gradeText.ToUpperInvariant() : "SKY400";

                string inclText;
                if (CsvTable.TryGetField(row, InclAliases, out inclText))
                {
                    double incl;
                    if (double.TryParse(inclText, System.Globalization.NumberStyles.Float, Inv, out incl))
                    {
                        r.InclDeg = incl;
                    }
                    else
                    {
                        rowErrors.Add($"傾斜角の値 '{inclText}' を数値として解釈できません。");
                    }
                }

                string colorText;
                if (CsvTable.TryGetField(row, ColorAliases, out colorText))
                {
                    int color;
                    r.ColorIdx = int.TryParse(colorText, System.Globalization.NumberStyles.Integer, Inv, out color)
                        ? color : 8;
                }

                string tipZText;
                if (CsvTable.TryGetField(row, TipZAliases, out tipZText))
                {
                    double tipZ;
                    if (double.TryParse(tipZText, System.Globalization.NumberStyles.Float, Inv, out tipZ))
                    {
                        r.TipZ = tipZ;
                    }
                    else
                    {
                        rowErrors.Add($"杭先端標高の値 '{tipZText}' を数値として解釈できません。");
                    }
                }
                else
                {
                    rowErrors.Add("杭先端標高 Z_tip を読み取れません(列 tip_z/杭先端標高)。");
                }

                // 総本数・施工順位: CSV に無ければ出現順で自動採番する(壁一括生成の既定動作)。
                if (anyRowHasPieceCount)
                {
                    string pieceCountText;
                    if (CsvTable.TryGetField(row, PieceCountAliases, out pieceCountText))
                    {
                        int pieceCount;
                        if (!int.TryParse(pieceCountText, System.Globalization.NumberStyles.Integer,
                            Inv, out pieceCount))
                        {
                            rowErrors.Add($"総本数の値 '{pieceCountText}' を整数として解釈できません。");
                        }
                        r.PieceCount = pieceCount;
                    }
                    else
                    {
                        rowErrors.Add("一部の行に総本数 (piece_count) があるため、全行での指定が必要です。");
                    }
                }
                else
                {
                    r.PieceCount = table.Rows.Count;
                }

                if (anyRowHasPieceIndex)
                {
                    string pieceIndexText;
                    if (CsvTable.TryGetField(row, PieceIndexAliases, out pieceIndexText))
                    {
                        int pieceIndex;
                        if (!int.TryParse(pieceIndexText, System.Globalization.NumberStyles.Integer,
                            Inv, out pieceIndex))
                        {
                            rowErrors.Add($"施工順位の値 '{pieceIndexText}' を整数として解釈できません。");
                        }
                        r.PieceIndex = pieceIndex;
                    }
                    else
                    {
                        rowErrors.Add("一部の行に施工順位 (piece_index) があるため、全行での指定が必要です。");
                    }
                }
                else
                {
                    r.PieceIndex = i + 1;
                }

                // 幾何値の整合はここで検証する。継手コード・施工順位の検証は
                // 幾何値が読めていることを前提とするため、幾何エラーがあれば
                // 例外を避けて先に打ち切る。
                if (rowErrors.Count == 0)
                {
                    string? errD = SheetPileQuayWall.Core.FrontWall.InputValidator.ValidateD(r.OuterDm);
                    if (errD != null) { rowErrors.Add(errD); }

                    string? errT = SheetPileQuayWall.Core.FrontWall.InputValidator.ValidateT(r.WallTm, r.OuterDm);
                    if (errT != null) { rowErrors.Add(errT); }

                    string? errL = SheetPileQuayWall.Core.FrontWall.InputValidator.ValidateL(r.LengthM);
                    if (errL != null) { rowErrors.Add(errL); }

                    try
                    {
                        SheetPileQuayWall.Core.FrontWall.JointParameters.FromCode(r.JointCode);
                    }
                    catch (System.ArgumentException)
                    {
                        rowErrors.Add($"継手形式 '{r.JointCode}' は未知のコードです" +
                            "(LT65/LT75/LT100/PP/PT のいずれか)。");
                    }

                    string? errPiece = SheetPileQuayWall.Core.FrontWall.PieceAssignment.Validate(
                        r.PieceIndex, r.PieceCount);
                    if (errPiece != null) { rowErrors.Add(errPiece); }
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

            return new ImportResult<FrontWallImportRow>(rows, errors);
        }

        // 個別列を優先し、無ければ規格文字列からの抽出を試みる。単位は mm。
        private static double? ReadMillimeterField(
            System.Collections.Generic.IReadOnlyDictionary<string, string> row,
            string[] aliases, string? specText,
            System.Func<string, double?> specExtractor)
        {
            string text;
            if (CsvTable.TryGetField(row, aliases, out text))
            {
                double value;
                return double.TryParse(text, System.Globalization.NumberStyles.Float, Inv, out value)
                    ? value : (double?)null;
            }

            return specText != null ? specExtractor(specText) : null;
        }
    }
}
