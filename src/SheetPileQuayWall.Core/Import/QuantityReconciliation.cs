// AutoCAD 非依存 — xUnit で単体テスト可能
//
// 帳票(積算ソフト出力等)に記載された数量・質量と、009 が QuayWallEstimate
// で計算した値を突き合わせ、許容誤差を超える差分を検出する。
//
// 帳票側は「項目,数量」の 2 列 CSV(ラベル→値)を想定する。ラベル文字列は
// 別名リストで解決する(実際のエクスポートのラベル表記は未確認。README §9.1)。

namespace SheetPileQuayWall.Core.Import
{
    public sealed class ReconciliationItem
    {
        public ReconciliationItem(string label, double reported, double computed, double toleranceRatio)
        {
            Label = label;
            Reported = reported;
            Computed = computed;
            ToleranceRatio = toleranceRatio;
        }

        public string Label { get; }
        public double Reported { get; }
        public double Computed { get; }
        public double ToleranceRatio { get; }

        public double Difference => Computed - Reported;

        // 帳票値が 0 の場合は絶対差で判定する(0 除算回避)。
        public double DifferenceRatio => System.Math.Abs(Reported) > 1.0e-9
            ? System.Math.Abs(Difference) / System.Math.Abs(Reported)
            : (System.Math.Abs(Computed) > 1.0e-9 ? double.PositiveInfinity : 0.0);

        public bool WithinTolerance => DifferenceRatio <= ToleranceRatio;
    }

    public static class QuantityReconciliation
    {
        // 既定許容誤差比率 1%。CLAUDE.PRIVATE.md §6 の 1mm=0.001m とは別種の量
        // (質量・本数)を扱うため、絶対誤差ではなく比率で判定する。
        public const double DefaultToleranceRatio = 0.01;

        private static readonly string[] LabelAliases = { "項目", "label", "name" };
        private static readonly string[] ValueAliases = { "数量", "value", "quantity" };

        // 「項目,数量」形式の CSV をラベル→値の辞書として読む。
        public static System.Collections.Generic.IReadOnlyDictionary<string, double> ParseReportedCsv(
            string csvText)
        {
            CsvTable table = CsvTable.Parse(csvText);
            System.Collections.Generic.Dictionary<string, double> result =
                new System.Collections.Generic.Dictionary<string, double>(
                    System.StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < table.Rows.Count; i++)
            {
                System.Collections.Generic.IReadOnlyDictionary<string, string> row = table.Rows[i];
                string label;
                string valueText;
                if (!CsvTable.TryGetField(row, LabelAliases, out label)) { continue; }
                if (!CsvTable.TryGetField(row, ValueAliases, out valueText)) { continue; }

                double value;
                if (double.TryParse(valueText, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out value))
                {
                    result[label.Trim()] = value;
                }
            }

            return result;
        }

        // 帳票のラベルに対応する数量を探し、009 計算値と突き合わせる。
        // 帳票に該当ラベルが無い項目は null を返す(突合できない旨を呼び出し側で扱う)。
        public static ReconciliationItem? Compare(
            System.Collections.Generic.IReadOnlyDictionary<string, double> reported,
            string[] labelAliases, string displayLabel, double computed,
            double toleranceRatio = DefaultToleranceRatio)
        {
            for (int i = 0; i < labelAliases.Length; i++)
            {
                double value;
                if (reported.TryGetValue(labelAliases[i], out value))
                {
                    return new ReconciliationItem(displayLabel, value, computed, toleranceRatio);
                }
            }
            return null;
        }
    }
}
