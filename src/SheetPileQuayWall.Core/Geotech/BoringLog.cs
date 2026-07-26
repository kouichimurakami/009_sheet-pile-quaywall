// AutoCAD 非依存 — xUnit で単体テスト可能
//
// 柱状図 CSV から加重平均N値(および岩盤層の加重平均一軸圧縮強度)を算出する。
// 前壁・タイロッド・控え杭のいずれの部材にも属さない横断的な地盤入力のため、
// FrontWall/TieRod/AnchorPile とは別に Geotech 名前空間を新設した。
//
// 土質区分は FrontWall.JetLayerType をそのまま流用する(ジェット併用の A0/γ 計算と
// 1 対 1 で対応させ、変換ステップを持ち込まないため)。
//
// 算出する加重平均N値は 2 系統ある。基準原文で除外ルールが異なることを確認済み:
//   - 貫入抵抗値 R 用: 表層から連続する N=0 の区間のみ除外(3-4.5-14, 3-4.6-12, 3-16-29)
//   - 打撃速度 Sb 用 : 表層から連続する N≦5 の区間を除外(3-4.5-16, 3-4.6-14。より広い)
// 土質区分別の加重平均N値(ジェット併用のγ用)には除外ルールの明記が無いため、
// 除外を適用しない(全層をそのまま加重平均する)。
//
// 岩盤層は N 値を持たない(γ4 は N ではなく一軸圧縮強度 qu を使うため)。R/Sb 用の
// 加重平均N値からは常に除外し、除外した本数・層厚を呼び出し側へ返す
// (基準は「岩盤を含む地盤でのR/Sb」を明示的に扱っていないため、無理に含めず
// 除外を明示するに留める。§9 の「推測で書き進めない」方針に合わせた判断)。

namespace SheetPileQuayWall.Core.Geotech
{
    public sealed class BoringLayer
    {
        public int RowNumber;
        public string LayerName = "";
        public SheetPileQuayWall.Core.FrontWall.JetLayerType SoilType;
        public double ElevationTopM;
        public double ElevationBottomM;
        public double ThicknessM;

        // 岩盤以外は必須、岩盤は null(qu を使うため)。
        public double? NValue;

        // 岩盤のみ必須、それ以外は null。
        public double? QuValue;
    }

    public static class BoringLogAnalysis
    {
        /// <summary>層厚と標高差の一致検証、標高の連続性検証に使う許容誤差 [m]
        /// (CLAUDE.PRIVATE.md §6 の 1mm = 0.001m と同じ規約)。</summary>
        public const double Tolerance_m = 0.001;

        /// <summary>貫入抵抗値 R 用の表層除外しきい値(N=0 のみ)。</summary>
        public const double RExclusionThreshold = 0.0;

        /// <summary>打撃速度 Sb 用の表層除外しきい値(N≦5)。</summary>
        public const double SbExclusionThreshold = 5.0;

        private static readonly System.Globalization.CultureInfo Inv =
            System.Globalization.CultureInfo.InvariantCulture;

        private static readonly string[] LayerNameAliases = { "layer_name", "土層名" };
        private static readonly string[] SoilTypeAliases = { "soil_type", "土質区分" };
        private static readonly string[] ElevationTopAliases = { "elevation_top", "標高上端" };
        private static readonly string[] ElevationBottomAliases = { "elevation_bottom", "標高下端" };
        private static readonly string[] ThicknessAliases = { "thickness_m", "層厚" };
        private static readonly string[] NValueAliases = { "n_value", "N値" };
        private static readonly string[] BlowCountAliases = { "blow_count", "打撃回数法" };
        private static readonly string[] PenetrationAliases = { "penetration_cm", "貫入量" };
        private static readonly string[] QuValueAliases = { "qu_value", "一軸圧縮強度" };

        // ─────────────────────────────────────────────────────────────────
        // 取り込み
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// 柱状図 CSV を解析する。標高上端の降順(浅い→深い)に並べ替えたうえで、
        /// 層厚の一致・標高の連続性・土質区分ごとの必須列を検証する。
        /// 1 行の不備でも他の行の計算を続けない(地盤入力の性質上、部分的な値で
        /// 計算を進めると設計判断を誤るため。他の CSV 取り込みの部分許容方針とは
        /// あえて変えている)。
        /// </summary>
        public static SheetPileQuayWall.Core.Import.ImportResult<BoringLayer> Parse(string csvText)
        {
            SheetPileQuayWall.Core.Import.CsvTable table =
                SheetPileQuayWall.Core.Import.CsvTable.Parse(csvText);

            System.Collections.Generic.List<BoringLayer> rows =
                new System.Collections.Generic.List<BoringLayer>();
            System.Collections.Generic.List<SheetPileQuayWall.Core.Import.ImportRowError> errors =
                new System.Collections.Generic.List<SheetPileQuayWall.Core.Import.ImportRowError>();

            for (int i = 0; i < table.Rows.Count; i++)
            {
                int rowNumber = i + 2;
                System.Collections.Generic.IReadOnlyDictionary<string, string> row = table.Rows[i];
                System.Collections.Generic.List<string> rowErrors =
                    new System.Collections.Generic.List<string>();

                BoringLayer layer = new BoringLayer { RowNumber = rowNumber };

                string layerNameText;
                layer.LayerName = SheetPileQuayWall.Core.Import.CsvTable.TryGetField(
                    row, LayerNameAliases, out layerNameText) ? layerNameText : "";

                string soilTypeText;
                SheetPileQuayWall.Core.FrontWall.JetLayerType? soilType = null;
                if (!SheetPileQuayWall.Core.Import.CsvTable.TryGetField(
                    row, SoilTypeAliases, out soilTypeText))
                {
                    rowErrors.Add("土質区分を読み取れません(列 soil_type/土質区分)。");
                }
                else
                {
                    soilType = ParseSoilType(soilTypeText);
                    if (soilType == null)
                    {
                        rowErrors.Add($"土質区分 '{soilTypeText}' は未知の区分です" +
                            "(砂質土等/粘性土/玉石混りレキ/固結土/岩盤 のいずれか)。");
                    }
                    else
                    {
                        layer.SoilType = soilType.Value;
                    }
                }

                double? elevTop = ReadReal(row, ElevationTopAliases, rowErrors, "標高上端");
                double? elevBottom = ReadReal(row, ElevationBottomAliases, rowErrors, "標高下端");
                double? thickness = ReadReal(row, ThicknessAliases, rowErrors, "層厚");

                if (elevTop != null) { layer.ElevationTopM = elevTop.Value; }
                if (elevBottom != null) { layer.ElevationBottomM = elevBottom.Value; }
                if (thickness != null) { layer.ThicknessM = thickness.Value; }

                if (elevTop != null && elevBottom != null && thickness != null)
                {
                    double expected = elevTop.Value - elevBottom.Value;
                    if (System.Math.Abs(expected - thickness.Value) > Tolerance_m)
                    {
                        rowErrors.Add($"層厚 {thickness.Value:F3}m が標高差(標高上端−標高下端=" +
                            $"{expected:F3}m)と一致しません。");
                    }
                    if (expected <= 0.0)
                    {
                        rowErrors.Add("標高上端は標高下端より大きくなければなりません" +
                            "(Z軸は鉛直上向き)。");
                    }
                }

                bool isRock = soilType == SheetPileQuayWall.Core.FrontWall.JetLayerType.Rock;

                string nValueText;
                bool hasNValue = SheetPileQuayWall.Core.Import.CsvTable.TryGetField(
                    row, NValueAliases, out nValueText);
                double? nValue = null;
                if (hasNValue)
                {
                    double parsed;
                    if (!double.TryParse(nValueText, System.Globalization.NumberStyles.Float,
                        Inv, out parsed))
                    {
                        rowErrors.Add($"N値の値 '{nValueText}' を数値として解釈できません。");
                    }
                    else
                    {
                        nValue = parsed;
                    }
                }
                else if (!isRock)
                {
                    rowErrors.Add("N値を読み取れません(岩盤以外の行では必須)。");
                }

                string quValueText;
                bool hasQuValue = SheetPileQuayWall.Core.Import.CsvTable.TryGetField(
                    row, QuValueAliases, out quValueText);
                double? quValue = null;
                if (hasQuValue)
                {
                    double parsed;
                    if (!double.TryParse(quValueText, System.Globalization.NumberStyles.Float,
                        Inv, out parsed))
                    {
                        rowErrors.Add($"一軸圧縮強度の値 '{quValueText}' を数値として解釈できません。");
                    }
                    else
                    {
                        quValue = parsed;
                    }
                }

                if (isRock && quValue == null)
                {
                    rowErrors.Add("一軸圧縮強度を読み取れません(岩盤の行では必須)。");
                }
                if (!isRock && quValue != null)
                {
                    rowErrors.Add("一軸圧縮強度は岩盤以外の行では指定できません" +
                        "(土質区分の誤りの可能性があります)。");
                }

                // 打止め(N>50)の換算。打撃回数法・貫入量は両方揃って初めて意味を持つ。
                string blowCountText;
                bool hasBlowCount = SheetPileQuayWall.Core.Import.CsvTable.TryGetField(
                    row, BlowCountAliases, out blowCountText);
                string penetrationText;
                bool hasPenetration = SheetPileQuayWall.Core.Import.CsvTable.TryGetField(
                    row, PenetrationAliases, out penetrationText);

                if (hasBlowCount != hasPenetration)
                {
                    rowErrors.Add("打撃回数法と貫入量は両方指定するか、両方省略してください。");
                }
                else if (hasBlowCount && hasPenetration)
                {
                    int blowCount;
                    double penetration;
                    if (!int.TryParse(blowCountText, System.Globalization.NumberStyles.Integer,
                        Inv, out blowCount))
                    {
                        rowErrors.Add($"打撃回数法の値 '{blowCountText}' を整数として解釈できません。");
                    }
                    else if (!double.TryParse(penetrationText, System.Globalization.NumberStyles.Float,
                        Inv, out penetration))
                    {
                        rowErrors.Add($"貫入量の値 '{penetrationText}' を数値として解釈できません。");
                    }
                    else
                    {
                        double? numerator = GetConversionNumerator(blowCount);
                        if (numerator == null)
                        {
                            rowErrors.Add($"打撃回数法 '{blowCount}' は未対応です(50/60/70/80 のいずれか)。");
                        }
                        else if (penetration <= 0.0)
                        {
                            rowErrors.Add("貫入量は正の値である必要があります。");
                        }
                        else
                        {
                            // 換算N値が生の N値 を上書きする(基準 3-16-19 注3、3-16-6 注2)。
                            nValue = numerator.Value / penetration;
                        }
                    }
                }

                layer.NValue = nValue;
                layer.QuValue = quValue;

                if (rowErrors.Count > 0)
                {
                    errors.Add(new SheetPileQuayWall.Core.Import.ImportRowError(
                        rowNumber, string.Join("; ", rowErrors)));
                }
                else
                {
                    rows.Add(layer);
                }
            }

            // 標高上端の降順(浅い→深い)に並べ替え、連続性を検証する。
            // CSV の記載順が深度順でなくても正しく扱えるようにするため。
            rows.Sort((a, b) => b.ElevationTopM.CompareTo(a.ElevationTopM));

            for (int i = 0; i < rows.Count - 1; i++)
            {
                double gap = System.Math.Abs(rows[i].ElevationBottomM - rows[i + 1].ElevationTopM);
                if (gap > Tolerance_m)
                {
                    errors.Add(new SheetPileQuayWall.Core.Import.ImportRowError(
                        rows[i + 1].RowNumber,
                        $"前の層(行{rows[i].RowNumber})の標高下端 {rows[i].ElevationBottomM:F3}m と" +
                        $"この行の標高上端 {rows[i + 1].ElevationTopM:F3}m が連続していません" +
                        "(ギャップまたは重複があります)。"));
                }
            }

            return new SheetPileQuayWall.Core.Import.ImportResult<BoringLayer>(rows, errors);
        }

        // ─────────────────────────────────────────────────────────────────
        // 加重平均N値・加重平均qu の算出
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// 全層通しの加重平均N値(R 用は exclusionThreshold=0、Sb 用は 5 を渡す)。
        /// 呼び出し前に <see cref="Parse"/> で標高降順・連続性検証済みであること。
        /// 岩盤層は N 値を持たないため常に除外し、除外した本数・層厚を返す。
        /// </summary>
        public static (double weightedN, double reckoningLength_m,
            int excludedRockLayerCount, double excludedRockThickness_m) CalcWeightedN(
            System.Collections.Generic.IReadOnlyList<BoringLayer> layers,
            double exclusionThreshold)
        {
            int start = 0;
            while (start < layers.Count)
            {
                BoringLayer candidate = layers[start];
                double? candidateN = candidate.NValue;
                bool excludable = candidate.SoilType != SheetPileQuayWall.Core.FrontWall.JetLayerType.Rock
                    && candidateN != null
                    && candidateN.Value <= exclusionThreshold;
                if (!excludable)
                {
                    break;
                }
                start++;
            }

            double numerator = 0.0;
            double denominator = 0.0;
            int excludedRockCount = 0;
            double excludedRockThickness = 0.0;

            for (int i = start; i < layers.Count; i++)
            {
                BoringLayer layer = layers[i];
                if (layer.SoilType == SheetPileQuayWall.Core.FrontWall.JetLayerType.Rock)
                {
                    excludedRockCount++;
                    excludedRockThickness += layer.ThicknessM;
                    continue;
                }
                if (layer.NValue == null)
                {
                    continue;
                }
                numerator += layer.NValue.Value * layer.ThicknessM;
                denominator += layer.ThicknessM;
            }

            double weightedN = denominator > 0.0 ? numerator / denominator : 0.0;
            return (weightedN, denominator, excludedRockCount, excludedRockThickness);
        }

        /// <summary>
        /// 土質区分ごとの加重平均N値(ジェット併用のγ用)。除外ルールは適用しない
        /// (基準に明記が無いため)。該当層が無ければ null。岩盤を指定した場合は
        /// 常に null(岩盤は <see cref="CalcWeightedQu"/> を使うこと)。
        /// </summary>
        public static double? CalcWeightedNBySoilType(
            System.Collections.Generic.IReadOnlyList<BoringLayer> layers,
            SheetPileQuayWall.Core.FrontWall.JetLayerType soilType)
        {
            if (soilType == SheetPileQuayWall.Core.FrontWall.JetLayerType.Rock)
            {
                return null;
            }

            double numerator = 0.0;
            double denominator = 0.0;
            for (int i = 0; i < layers.Count; i++)
            {
                BoringLayer layer = layers[i];
                double? n = layer.NValue;
                if (layer.SoilType != soilType || n == null)
                {
                    continue;
                }
                numerator += n.Value * layer.ThicknessM;
                denominator += layer.ThicknessM;
            }

            return denominator > 0.0 ? numerator / denominator : (double?)null;
        }

        /// <summary>
        /// 岩盤層の加重平均一軸圧縮強度 qu(ジェット併用の A0/γ4 用)。
        /// 岩盤層が無ければ null。
        /// </summary>
        public static double? CalcWeightedQu(
            System.Collections.Generic.IReadOnlyList<BoringLayer> layers)
        {
            double numerator = 0.0;
            double denominator = 0.0;
            for (int i = 0; i < layers.Count; i++)
            {
                BoringLayer layer = layers[i];
                double? qu = layer.QuValue;
                if (layer.SoilType != SheetPileQuayWall.Core.FrontWall.JetLayerType.Rock
                    || qu == null)
                {
                    continue;
                }
                numerator += qu.Value * layer.ThicknessM;
                denominator += layer.ThicknessM;
            }

            return denominator > 0.0 ? numerator / denominator : (double?)null;
        }

        // ── 内部ヘルパー ──────────────────────────────────────────────────

        private static SheetPileQuayWall.Core.FrontWall.JetLayerType? ParseSoilType(string text)
        {
            string trimmed = text.Trim();
            if (trimmed == "砂質土等" || trimmed.Equals("SandGravel", System.StringComparison.OrdinalIgnoreCase))
            {
                return SheetPileQuayWall.Core.FrontWall.JetLayerType.SandGravel;
            }
            if (trimmed == "粘性土" || trimmed.Equals("Clay", System.StringComparison.OrdinalIgnoreCase))
            {
                return SheetPileQuayWall.Core.FrontWall.JetLayerType.Clay;
            }
            if (trimmed == "玉石混りレキ" || trimmed.Equals("CobbleGravel", System.StringComparison.OrdinalIgnoreCase))
            {
                return SheetPileQuayWall.Core.FrontWall.JetLayerType.CobbleGravel;
            }
            if (trimmed == "固結土" || trimmed.Equals("Cemented", System.StringComparison.OrdinalIgnoreCase))
            {
                return SheetPileQuayWall.Core.FrontWall.JetLayerType.Cemented;
            }
            if (trimmed == "岩盤" || trimmed.Equals("Rock", System.StringComparison.OrdinalIgnoreCase))
            {
                return SheetPileQuayWall.Core.FrontWall.JetLayerType.Rock;
            }
            return null;
        }

        // 打止め換算N値の分子(3-16-19 注3、3-16-6 注2)。50回法=1500、60=1800、70=2100、80=2400。
        private static double? GetConversionNumerator(int blowCount)
        {
            switch (blowCount)
            {
                case 50: return 1500.0;
                case 60: return 1800.0;
                case 70: return 2100.0;
                case 80: return 2400.0;
                default: return null;
            }
        }

        private static double? ReadReal(
            System.Collections.Generic.IReadOnlyDictionary<string, string> row,
            string[] aliases, System.Collections.Generic.List<string> rowErrors, string label)
        {
            string text;
            if (!SheetPileQuayWall.Core.Import.CsvTable.TryGetField(row, aliases, out text))
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
    }
}
