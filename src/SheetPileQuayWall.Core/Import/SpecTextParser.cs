// AutoCAD 非依存 — xUnit で単体テスト可能
//
// 「規格・形状寸法」列に外径・肉厚・全長・継手形式が 1 セルへまとめて
// 記載されている帳票(例: "φ800×12" "L=20.0m" "LT75")から、正規表現で
// 個別の数値・コードを抽出する。
//
// 【重要】実際のサーチマス等の積算ソフトのエクスポート形式は未確認であり、
// ここでの表記(φDDD×t、L=NN.N 等)は業界で見られる一般的な慣行を仮定した
// ベストエフォートである(README §9.1)。個別列(outer_d_mm 等)が CSV に
// あればそちらを優先し、本パーサは列が無い場合のフォールバックとして使う。
// 実際の帳票を確認したら、パターンを実データに合わせて調整すること。

namespace SheetPileQuayWall.Core.Import
{
    public static class SpecTextParser
    {
        private static readonly System.Globalization.CultureInfo Inv =
            System.Globalization.CultureInfo.InvariantCulture;

        private static readonly string[] KnownJointCodes =
            { "LT65", "LT75", "LT100", "PP", "PT" };

        // "φ800" "φ800.0" 等から外径 [mm] を抽出する。無ければ null。
        public static double? TryExtractOuterDiameterMm(string specText)
        {
            System.Text.RegularExpressions.Match m =
                System.Text.RegularExpressions.Regex.Match(
                    specText, @"[φΦ]\s*([0-9]+(?:\.[0-9]+)?)");
            return ParseOrNull(m);
        }

        // "×12" "×t12" 等から肉厚 [mm] を抽出する(外径の直後にくる乗算記号の次の数値)。
        // 無ければ null。
        public static double? TryExtractWallThicknessMm(string specText)
        {
            System.Text.RegularExpressions.Match m =
                System.Text.RegularExpressions.Regex.Match(
                    specText, @"[×xX]\s*t?\s*([0-9]+(?:\.[0-9]+)?)");
            return ParseOrNull(m);
        }

        // "L=20.0" "L20.0m" "ℓ=20.0" 等から全長 [m] を抽出する。無ければ null。
        public static double? TryExtractLengthM(string specText)
        {
            System.Text.RegularExpressions.Match m =
                System.Text.RegularExpressions.Regex.Match(
                    specText, @"[LℓI]\s*=?\s*([0-9]+(?:\.[0-9]+)?)\s*m?", System.Text.RegularExpressions
                        .RegexOptions.IgnoreCase);
            return ParseOrNull(m);
        }

        // 既知の継手コード(LT65/LT75/LT100/PP/PT)がテキスト中に含まれるか検出する。
        // 複数一致し得る場合は文字列が長いコードを優先する(PP より LT100 等)。
        public static string? TryExtractJointCode(string specText)
        {
            string? best = null;
            for (int i = 0; i < KnownJointCodes.Length; i++)
            {
                string code = KnownJointCodes[i];
                if (specText.IndexOf(code, System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    if (best == null || code.Length > best.Length)
                    {
                        best = code;
                    }
                }
            }
            return best;
        }

        private static double? ParseOrNull(System.Text.RegularExpressions.Match m)
        {
            if (!m.Success)
            {
                return null;
            }
            double value;
            return double.TryParse(
                m.Groups[1].Value, System.Globalization.NumberStyles.Float, Inv, out value)
                ? value : (double?)null;
        }
    }
}
