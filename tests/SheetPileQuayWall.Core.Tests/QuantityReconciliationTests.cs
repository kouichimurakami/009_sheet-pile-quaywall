// T1180〜T1194: QuantityReconciliation の単体テスト
// AutoCAD 非依存のため xUnit で直接実行可能

namespace SheetPileQuayWall.Core.Tests
{
    public class QuantityReconciliationTests
    {
        // T1180: 「項目,数量」形式の CSV をラベル→値の辞書として読める
        [Xunit.Fact]
        public void T1180_ParseReportedCsv_ReadsLabelValuePairs()
        {
            string csv = "項目,数量\n施設延長,120.000\n合計質量,63755\n";

            System.Collections.Generic.IReadOnlyDictionary<string, double> reported =
                SheetPileQuayWall.Core.Import.QuantityReconciliation.ParseReportedCsv(csv);

            Xunit.Assert.Equal(120.000, reported["施設延長"], 3);
            Xunit.Assert.Equal(63755.0, reported["合計質量"], 1);
        }

        // T1181: ラベルの参照は大文字小文字を無視する
        [Xunit.Fact]
        public void T1181_ParseReportedCsv_LabelLookupIsCaseInsensitive()
        {
            string csv = "Label,Value\nTotalMass,63755\n";

            System.Collections.Generic.IReadOnlyDictionary<string, double> reported =
                SheetPileQuayWall.Core.Import.QuantityReconciliation.ParseReportedCsv(csv);

            Xunit.Assert.Equal(63755.0, reported["totalmass"], 1);
        }

        // T1182: 誤差 1% 以内は WithinTolerance = true
        [Xunit.Fact]
        public void T1182_Compare_WithinOnePercent_IsWithinTolerance()
        {
            SheetPileQuayWall.Core.Import.ReconciliationItem item =
                new SheetPileQuayWall.Core.Import.ReconciliationItem(
                    "合計質量", 63755.0, 64000.0,
                    SheetPileQuayWall.Core.Import.QuantityReconciliation.DefaultToleranceRatio);

            // 差 245 / 63755 = 0.00384... < 0.01
            Xunit.Assert.True(item.WithinTolerance);
        }

        // T1183: 誤差 1% 超は WithinTolerance = false
        [Xunit.Fact]
        public void T1183_Compare_OverOnePercent_IsNotWithinTolerance()
        {
            SheetPileQuayWall.Core.Import.ReconciliationItem item =
                new SheetPileQuayWall.Core.Import.ReconciliationItem(
                    "合計質量", 63755.0, 65500.0,
                    SheetPileQuayWall.Core.Import.QuantityReconciliation.DefaultToleranceRatio);

            // 差 1745 / 63755 = 0.02737... > 0.01
            Xunit.Assert.False(item.WithinTolerance);
        }

        // T1184: 帳票値が 0 の場合は絶対差で判定し、0 除算にならない
        [Xunit.Fact]
        public void T1184_Compare_ReportedZero_UsesAbsoluteDifference()
        {
            SheetPileQuayWall.Core.Import.ReconciliationItem sameZero =
                new SheetPileQuayWall.Core.Import.ReconciliationItem("継手接続数", 0.0, 0.0, 0.01);
            SheetPileQuayWall.Core.Import.ReconciliationItem diffFromZero =
                new SheetPileQuayWall.Core.Import.ReconciliationItem("継手接続数", 0.0, 9.0, 0.01);

            Xunit.Assert.True(sameZero.WithinTolerance);
            Xunit.Assert.False(diffFromZero.WithinTolerance);
        }

        // T1185: Difference は Computed − Reported(符号付き)
        [Xunit.Fact]
        public void T1185_Difference_IsComputedMinusReported()
        {
            SheetPileQuayWall.Core.Import.ReconciliationItem item =
                new SheetPileQuayWall.Core.Import.ReconciliationItem("前壁本管質量", 46000.0, 46637.0, 0.01);

            Xunit.Assert.Equal(637.0, item.Difference, 1);
        }

        // T1186: Compare は別名リストの最初に一致したラベルの値を使う
        [Xunit.Fact]
        public void T1186_Compare_ResolvesLabelAlias()
        {
            System.Collections.Generic.Dictionary<string, double> reported =
                new System.Collections.Generic.Dictionary<string, double>(
                    System.StringComparer.OrdinalIgnoreCase)
                { { "TotalMass", 63755.0 } };

            SheetPileQuayWall.Core.Import.ReconciliationItem? item =
                SheetPileQuayWall.Core.Import.QuantityReconciliation.Compare(
                    reported, new[] { "合計質量", "TotalMass" }, "合計質量", 64000.0);

            Xunit.Assert.NotNull(item);
            Xunit.Assert.Equal(63755.0, item!.Reported, 1);
        }

        // T1187: 帳票に該当ラベルが無ければ null(突合不可を呼び出し側が扱える)
        [Xunit.Fact]
        public void T1187_Compare_NoMatchingLabel_ReturnsNull()
        {
            System.Collections.Generic.Dictionary<string, double> reported =
                new System.Collections.Generic.Dictionary<string, double>(
                    System.StringComparer.OrdinalIgnoreCase)
                { { "施設延長", 120.0 } };

            SheetPileQuayWall.Core.Import.ReconciliationItem? item =
                SheetPileQuayWall.Core.Import.QuantityReconciliation.Compare(
                    reported, new[] { "合計質量", "TotalMass" }, "合計質量", 64000.0);

            Xunit.Assert.Null(item);
        }
    }
}
