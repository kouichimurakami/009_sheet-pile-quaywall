// AutoCAD 非依存 — xUnit で単体テスト可能
//
// タイロッドの帳票 CSV 取り込み。1 行 = 1 組。
// 既存の TieRodParameters をそのまま埋めるため、フィールド定義・検証
// (Validate())は重複させず TieRodParameters 側の実装をそのまま使う。
//
// 平面位置 Y は帳票側で明示が必須(前壁選択後に前壁からの相対位置として
// 決まるため、CSV から一意に自動配置する規約が無い。決定8 と同じ理由で
// X は保存せず、前壁 Handle から都度計算する)。

namespace SheetPileQuayWall.Core.Import
{
    public sealed class TieRodImportRow
    {
        public int RowNumber;
        public SheetPileQuayWall.Core.TieRod.TieRodParameters Parameters =
            new SheetPileQuayWall.Core.TieRod.TieRodParameters();
        public double PositionY;
    }

    public static class TieRodCsvImporter
    {
        private static readonly string[] RodDAliases = { "rod_d", "タイロッド径" };
        private static readonly string[] GradeAliases = { "grade", "鋼種" };
        private static readonly string[] CodeAliases = { "code", "設計基準" };
        private static readonly string[] StateAliases = { "state", "荷重状態" };
        private static readonly string[] SpanAliases = { "span_length", "span", "法線直角方向延長" };
        private static readonly string[] PileDAliases = { "pile_d", "海側鋼管矢板径" };
        private static readonly string[] PilePitchAliases = { "pile_pitch", "鋼管矢板ピッチ" };
        private static readonly string[] TieSpacingAliases = { "tie_spacing", "タイロッド取付間隔" };
        private static readonly string[] TieCountAliases = { "tie_count", "組数" };
        private static readonly string[] HwlAliases = { "hwl", "H.W.L" };
        private static readonly string[] TieElevAliases = { "tie_elev", "タイロッド軸心標高" };
        private static readonly string[] WalingHAliases = { "waling_h", "腹起し溝形鋼高さ" };
        private static readonly string[] PlateTAliases = { "plate_t", "定着プレート厚" };
        private static readonly string[] WasherTAliases = { "washer_t", "定着ワッシャー厚" };
        private static readonly string[] NutHAliases = { "nut_h", "ナット高さ" };
        private static readonly string[] AdjustLAliases = { "adjust_l", "調節長" };
        private static readonly string[] ReactionAliases = { "anchor_reaction", "取付点反力" };
        private static readonly string[] ColorAliases = { "color", "色" };
        private static readonly string[] PosYAliases = { "pos_y", "y", "位置y" };

        private static readonly System.Globalization.CultureInfo Inv =
            System.Globalization.CultureInfo.InvariantCulture;

        public static ImportResult<TieRodImportRow> Parse(string csvText)
        {
            CsvTable table = CsvTable.Parse(csvText);

            System.Collections.Generic.List<TieRodImportRow> rows =
                new System.Collections.Generic.List<TieRodImportRow>();
            System.Collections.Generic.List<ImportRowError> errors =
                new System.Collections.Generic.List<ImportRowError>();

            for (int i = 0; i < table.Rows.Count; i++)
            {
                int rowNumber = i + 2;
                System.Collections.Generic.IReadOnlyDictionary<string, string> row = table.Rows[i];
                System.Collections.Generic.List<string> rowErrors =
                    new System.Collections.Generic.List<string>();

                TieRodImportRow r = new TieRodImportRow { RowNumber = rowNumber };
                SheetPileQuayWall.Core.TieRod.TieRodParameters p = r.Parameters;

                ReadReal(row, RodDAliases, rowErrors, "タイロッド径", v => p.RodDiameter = v);

                SheetPileQuayWall.Core.TieRod.SteelGrade grade;
                ReadEnum(row, GradeAliases, p.Grade, out grade);
                p.Grade = grade;

                SheetPileQuayWall.Core.TieRod.DesignCode code;
                ReadEnum(row, CodeAliases, p.Code, out code);
                p.Code = code;

                SheetPileQuayWall.Core.TieRod.LoadState state;
                ReadEnum(row, StateAliases, p.State, out state);
                p.State = state;

                ReadReal(row, SpanAliases, rowErrors, "法線直角方向延長", v => p.SpanLength = v);
                ReadReal(row, PileDAliases, rowErrors, "海側鋼管矢板径", v => p.PileDiameter = v);
                ReadReal(row, PilePitchAliases, rowErrors, "鋼管矢板ピッチ", v => p.PilePitch = v);
                ReadReal(row, TieSpacingAliases, rowErrors, "タイロッド取付間隔", v => p.TieSpacing = v);
                ReadInt(row, TieCountAliases, rowErrors, "組数", v => p.TieCount = v);
                ReadReal(row, HwlAliases, rowErrors, "H.W.L.", v => p.Hwl = v);
                ReadReal(row, TieElevAliases, rowErrors, "タイロッド軸心標高", v => p.TieElevation = v);
                ReadReal(row, WalingHAliases, rowErrors, "腹起し溝形鋼高さ", v => p.WalingHeight = v);
                ReadReal(row, PlateTAliases, rowErrors, "定着プレート厚", v => p.PlateThickness = v);
                ReadReal(row, WasherTAliases, rowErrors, "定着ワッシャー厚", v => p.WasherThickness = v);
                ReadReal(row, NutHAliases, rowErrors, "ナット高さ", v => p.NutHeight = v);
                ReadReal(row, AdjustLAliases, rowErrors, "調節長", v => p.AdjustLength = v);
                ReadReal(row, ReactionAliases, rowErrors, "取付点反力", v => p.AnchorReaction = v);
                ReadInt(row, ColorAliases, rowErrors, "色", v => p.LayerColor = v);

                string posYText;
                if (CsvTable.TryGetField(row, PosYAliases, out posYText))
                {
                    double posY;
                    if (double.TryParse(posYText, System.Globalization.NumberStyles.Float, Inv, out posY))
                    {
                        r.PositionY = posY;
                    }
                    else
                    {
                        rowErrors.Add($"位置 Y の値 '{posYText}' を数値として解釈できません。");
                    }
                }
                else
                {
                    rowErrors.Add("位置 Y を読み取れません(列 pos_y/Y)。前壁からの相対位置は自動計算されないため必須。");
                }

                if (rowErrors.Count == 0)
                {
                    System.Collections.Generic.IReadOnlyList<string> paramErrors = p.Validate();
                    for (int e = 0; e < paramErrors.Count; e++)
                    {
                        rowErrors.Add(paramErrors[e]);
                    }
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

            return new ImportResult<TieRodImportRow>(rows, errors);
        }

        private static void ReadReal(
            System.Collections.Generic.IReadOnlyDictionary<string, string> row,
            string[] aliases, System.Collections.Generic.List<string> rowErrors,
            string label, System.Action<double> assign)
        {
            string text;
            if (!CsvTable.TryGetField(row, aliases, out text))
            {
                rowErrors.Add($"{label} を読み取れません(列が見つかりません)。");
                return;
            }
            double value;
            if (!double.TryParse(text, System.Globalization.NumberStyles.Float, Inv, out value))
            {
                rowErrors.Add($"{label} の値 '{text}' を数値として解釈できません。");
                return;
            }
            assign(value);
        }

        private static void ReadInt(
            System.Collections.Generic.IReadOnlyDictionary<string, string> row,
            string[] aliases, System.Collections.Generic.List<string> rowErrors,
            string label, System.Action<int> assign)
        {
            string text;
            if (!CsvTable.TryGetField(row, aliases, out text))
            {
                rowErrors.Add($"{label} を読み取れません(列が見つかりません)。");
                return;
            }
            int value;
            if (!int.TryParse(text, System.Globalization.NumberStyles.Integer, Inv, out value))
            {
                rowErrors.Add($"{label} の値 '{text}' を整数として解釈できません。");
                return;
            }
            assign(value);
        }

        private static void ReadEnum<T>(
            System.Collections.Generic.IReadOnlyDictionary<string, string> row,
            string[] aliases, T fallback, out T result) where T : struct
        {
            string text;
            if (CsvTable.TryGetField(row, aliases, out text))
            {
                T parsed;
                result = System.Enum.TryParse(text, true, out parsed) ? parsed : fallback;
                return;
            }
            result = fallback;
        }
    }
}
