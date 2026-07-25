// data/tairod_catalog_dimensions.json の品質チェック (CLAUDE.PRIVATE.md §4 ステップ3)。
//
// カタログPDFからの読み取り誤りを、部品どうしの寸法整合という物理的な制約で検出する。
// たとえばワッシャ内径はリング部孔径と同じ孔を通るので一致しなければならない。
// 検出力には濃淡がある: 孔径・ねじ・両表共通部品は 1 セルの誤りでも落ちるが、
// 質量系など単調性チェックのみの項目は、単調性を保つ誤記なら検出できない。

namespace SheetPileQuayWall.Core.Tests
{
    /// <summary>JSON 読み込みのヘルパー。</summary>
    internal static class CatalogJson
    {
        private static readonly System.Text.Json.JsonDocument Document = Load();

        private static System.Text.Json.JsonDocument Load()
        {
            string path = System.IO.Path.Combine(
                System.AppContext.BaseDirectory, "data", "tairod_catalog_dimensions.json");
            return System.Text.Json.JsonDocument.Parse(System.IO.File.ReadAllText(path));
        }

        public static System.Text.Json.JsonElement Root
        {
            get { return Document.RootElement; }
        }

        public static string[] TableIds
        {
            get { return new string[] { "HT690", "SS400" }; }
        }

        public static int[] Diameters()
        {
            System.Collections.Generic.List<int> list = new System.Collections.Generic.List<int>();
            foreach (System.Text.Json.JsonElement e in Root.GetProperty("diameters_mm").EnumerateArray())
            {
                list.Add(e.GetInt32());
            }
            return list.ToArray();
        }

        public static System.Text.Json.JsonElement Table(string tableId)
        {
            foreach (System.Text.Json.JsonElement t in Root.GetProperty("tables").EnumerateArray())
            {
                if (t.GetProperty("id").GetString() == tableId)
                {
                    return t;
                }
            }
            throw new System.InvalidOperationException("表が見つかりません: " + tableId);
        }

        public static System.Text.Json.JsonElement Item(
            string tableId, string componentId, string itemId)
        {
            foreach (System.Text.Json.JsonElement c in Table(tableId).GetProperty("components").EnumerateArray())
            {
                if (c.GetProperty("id").GetString() != componentId)
                {
                    continue;
                }
                foreach (System.Text.Json.JsonElement i in c.GetProperty("items").EnumerateArray())
                {
                    if (i.GetProperty("id").GetString() == itemId)
                    {
                        return i;
                    }
                }
            }
            throw new System.InvalidOperationException(
                "項目が見つかりません: " + tableId + "/" + componentId + "/" + itemId);
        }

        public static double[] Numbers(string tableId, string componentId, string itemId)
        {
            System.Collections.Generic.List<double> list = new System.Collections.Generic.List<double>();
            foreach (System.Text.Json.JsonElement e in
                     Item(tableId, componentId, itemId).GetProperty("values").EnumerateArray())
            {
                list.Add(e.GetDouble());
            }
            return list.ToArray();
        }

        public static string[] Strings(string tableId, string componentId, string itemId)
        {
            System.Collections.Generic.List<string> list = new System.Collections.Generic.List<string>();
            foreach (System.Text.Json.JsonElement e in
                     Item(tableId, componentId, itemId).GetProperty("values").EnumerateArray())
            {
                list.Add(e.GetString() ?? string.Empty);
            }
            return list.ToArray();
        }
    }

    public class CatalogJsonTests
    {
        private const int DiameterCount = 19;

        [Xunit.Fact]
        public void 径の並びが計算層の規格径と一致する()
        {
            int[] json = CatalogJson.Diameters();
            Xunit.Assert.Equal(DiameterCount, json.Length);

            for (int i = 0; i < json.Length; i++)
            {
                Xunit.Assert.Equal(
                    SheetPileQuayWall.Core.TieRod.TieRodCatalog.StandardDiameters[i], json[i] / 1000.0,
                    SheetPileQuayWall.Core.TieRod.TieRodCatalog.Tolerance);
            }
        }

        [Xunit.Fact]
        public void 全ての項目が19列そろっている()
        {
            // 列の欠落・重複は転記事故の典型。全項目を機械的に数える。
            int checkedItems = 0;

            foreach (System.Text.Json.JsonElement t in
                     CatalogJson.Root.GetProperty("tables").EnumerateArray())
            {
                string tableId = t.GetProperty("id").GetString() ?? "?";

                foreach (System.Text.Json.JsonElement c in t.GetProperty("components").EnumerateArray())
                {
                    string componentId = c.GetProperty("id").GetString() ?? "?";

                    foreach (System.Text.Json.JsonElement i in c.GetProperty("items").EnumerateArray())
                    {
                        string itemId = i.GetProperty("id").GetString() ?? "?";
                        int count = i.GetProperty("values").GetArrayLength();

                        Xunit.Assert.True(
                            count == DiameterCount,
                            string.Format("{0}/{1}/{2} の列数が {3} です（期待 {4}）",
                                tableId, componentId, itemId, count, DiameterCount));
                        checkedItems++;
                    }
                }
            }

            // 1 表あたり 41 項目（本体5 + プレート5 + ピン7 + ナット3 + ワッシャ3
            //                     + ターンバックル6 + 定着ナット3 + 質量9）× 2 表 = 82 項目
            Xunit.Assert.Equal(82, checkedItems);
        }

        [Xunit.Theory]
        [Xunit.InlineData("HT690")]
        [Xunit.InlineData("SS400")]
        public void 棒部単位質量が断面積かける密度と一致する(string tableId)
        {
            double[] catalogMass = CatalogJson.Numbers(tableId, "mass", "rod_unit_mass");
            int[] diameters = CatalogJson.Diameters();

            for (int i = 0; i < diameters.Length; i++)
            {
                double computed = SheetPileQuayWall.Core.TieRod.TieRodCatalog.UnitMass(diameters[i] / 1000.0);
                Xunit.Assert.Equal(catalogMass[i], computed, 0.06);
            }
        }

        [Xunit.Theory]
        [Xunit.InlineData("HT690")]
        [Xunit.InlineData("SS400")]
        public void リングジョイントのピン径はタイロッド呼び径に等しい(string tableId)
        {
            double[] pin = CatalogJson.Numbers(tableId, "ring_joint_pin", "diameter");
            int[] diameters = CatalogJson.Diameters();

            for (int i = 0; i < diameters.Length; i++)
            {
                Xunit.Assert.Equal(diameters[i], pin[i], 0.001);
            }
        }

        [Xunit.Theory]
        [Xunit.InlineData("HT690")]
        [Xunit.InlineData("SS400")]
        public void 同じ孔を通る部品の孔径が一致する(string tableId)
        {
            // ①リング部孔径 = ②プレート孔径 = ⑤ワッシャ内径
            double[] ringHole = CatalogJson.Numbers(tableId, "rod_body", "ring_hole_diameter");
            double[] plateHole = CatalogJson.Numbers(tableId, "plate", "hole_diameter");
            double[] washerInner = CatalogJson.Numbers(tableId, "ring_washer", "inner_diameter");

            Xunit.Assert.Equal(ringHole, plateHole);
            Xunit.Assert.Equal(ringHole, washerInner);
        }

        [Xunit.Theory]
        [Xunit.InlineData("HT690")]
        [Xunit.InlineData("SS400")]
        public void 本体のねじにターンバックルと定着ナットが適合する(string tableId)
        {
            string[] rod = CatalogJson.Strings(tableId, "rod_body", "thread_designation");
            string[] turnbuckle = CatalogJson.Strings(tableId, "turnbuckle", "thread_designation");
            string[] anchorNut = CatalogJson.Strings(tableId, "anchor_nut", "thread_designation");

            Xunit.Assert.Equal(rod, turnbuckle);
            Xunit.Assert.Equal(rod, anchorNut);
        }

        [Xunit.Theory]
        [Xunit.InlineData("HT690")]
        [Xunit.InlineData("SS400")]
        public void ピンのねじと対辺距離にナットが適合する(string tableId)
        {
            string[] pinThread = CatalogJson.Strings(tableId, "ring_joint_pin", "thread_designation");
            string[] nutThread = CatalogJson.Strings(tableId, "ring_nut", "thread_designation");
            Xunit.Assert.Equal(pinThread, nutThread);

            double[] pinFlats = CatalogJson.Numbers(tableId, "ring_joint_pin", "head_across_flats");
            double[] nutFlats = CatalogJson.Numbers(tableId, "ring_nut", "across_flats");
            Xunit.Assert.Equal(pinFlats, nutFlats);
        }

        [Xunit.Theory]
        [Xunit.InlineData("HT690")]
        [Xunit.InlineData("SS400")]
        public void ターンバックルは外径が内径を上回る(string tableId)
        {
            double[] outer = CatalogJson.Numbers(tableId, "turnbuckle", "outer_diameter");
            double[] inner = CatalogJson.Numbers(tableId, "turnbuckle", "inner_diameter");

            for (int i = 0; i < outer.Length; i++)
            {
                Xunit.Assert.True(outer[i] > inner[i],
                    string.Format("{0} の列 {1}: 外径 {2} ≦ 内径 {3}", tableId, i, outer[i], inner[i]));
            }
        }

        [Xunit.Theory]
        [Xunit.InlineData("HT690")]
        [Xunit.InlineData("SS400")]
        public void ワッシャは外径が内径を上回る(string tableId)
        {
            double[] outer = CatalogJson.Numbers(tableId, "ring_washer", "outer_diameter");
            double[] inner = CatalogJson.Numbers(tableId, "ring_washer", "inner_diameter");

            for (int i = 0; i < outer.Length; i++)
            {
                Xunit.Assert.True(outer[i] > inner[i],
                    string.Format("{0} の列 {1}: 外径 {2} ≦ 内径 {3}", tableId, i, outer[i], inner[i]));
            }
        }

        [Xunit.Theory]
        [Xunit.InlineData("rod_body", "ring_width")]
        [Xunit.InlineData("rod_body", "ring_thickness")]
        [Xunit.InlineData("rod_body", "ring_hole_diameter")]
        [Xunit.InlineData("plate", "thickness")]
        [Xunit.InlineData("plate", "length")]
        [Xunit.InlineData("plate", "hole_pitch")]
        [Xunit.InlineData("ring_joint_pin", "overall_length")]
        [Xunit.InlineData("ring_joint_pin", "head_across_flats")]
        [Xunit.InlineData("ring_nut", "height")]
        [Xunit.InlineData("ring_washer", "outer_diameter")]
        [Xunit.InlineData("turnbuckle", "inner_diameter")]
        [Xunit.InlineData("turnbuckle", "overall_length")]
        [Xunit.InlineData("anchor_nut", "height")]
        [Xunit.InlineData("anchor_nut", "across_flats")]
        [Xunit.InlineData("mass", "rod_unit_mass")]
        [Xunit.InlineData("mass", "ring_part_mass")]
        [Xunit.InlineData("mass", "thread_part_mass")]
        [Xunit.InlineData("mass", "ring_plate_mass")]
        [Xunit.InlineData("mass", "ring_pin_mass")]
        [Xunit.InlineData("mass", "anchor_nut_mass")]
        public void 径の増加に対して単調非減少である(string componentId, string itemId)
        {
            // ターンバックル質量とリングワッシャ質量は肉厚設定の切替で非単調になるため対象外。
            for (int t = 0; t < CatalogJson.TableIds.Length; t++)
            {
                string tableId = CatalogJson.TableIds[t];
                double[] values = CatalogJson.Numbers(tableId, componentId, itemId);

                for (int i = 1; i < values.Length; i++)
                {
                    Xunit.Assert.True(values[i] >= values[i - 1],
                        string.Format("{0}/{1}/{2}: 列 {3} で {4} → {5} と減少",
                            tableId, componentId, itemId, i, values[i - 1], values[i]));
                }
            }
        }

        [Xunit.Theory]
        [Xunit.InlineData("rod_body")]
        [Xunit.InlineData("ring_nut")]
        [Xunit.InlineData("ring_washer")]
        [Xunit.InlineData("anchor_nut")]
        public void 両表で寸法が共通の部品は完全に一致する(string componentId)
        {
            // ①本体・④ナット・⑤ワッシャ・⑦定着ナットは HT690 表と SS400 表で寸法が同一。
            // 差があるのは材質のみ。
            System.Text.Json.JsonElement ht = CatalogJson.Table("HT690");
            System.Collections.Generic.List<string> itemIds =
                new System.Collections.Generic.List<string>();

            foreach (System.Text.Json.JsonElement c in ht.GetProperty("components").EnumerateArray())
            {
                if (c.GetProperty("id").GetString() != componentId)
                {
                    continue;
                }
                foreach (System.Text.Json.JsonElement i in c.GetProperty("items").EnumerateArray())
                {
                    itemIds.Add(i.GetProperty("id").GetString() ?? "?");
                }
            }

            Xunit.Assert.NotEmpty(itemIds);

            for (int k = 0; k < itemIds.Count; k++)
            {
                string itemId = itemIds[k];
                System.Text.Json.JsonElement a = CatalogJson.Item("HT690", componentId, itemId);
                System.Text.Json.JsonElement b = CatalogJson.Item("SS400", componentId, itemId);

                Xunit.Assert.Equal(
                    a.GetProperty("values").GetRawText(), b.GetProperty("values").GetRawText());
            }
        }

        [Xunit.Fact]
        public void 積算基準とカタログの定着ナット高さ不一致が記録どおりである()
        {
            // 実データから差分を再計算し、cross_reference の記載と突き合わせる。
            int[] diameters = CatalogJson.Diameters();
            double[] catalogHeights = CatalogJson.Numbers("HT690", "anchor_nut", "height");

            System.Text.Json.JsonElement comparison = CatalogJson.Root
                .GetProperty("cross_reference")
                .GetProperty("discrepancy")
                .GetProperty("comparison_mm");

            int compared = 0;

            for (int i = 0; i < diameters.Length; i++)
            {
                double estimation;
                if (!SheetPileQuayWall.Core.TieRod.TieRodCatalog.TryGetNutHeight(diameters[i] / 1000.0, out estimation))
                {
                    continue;   // 積算基準表に無い径
                }

                double estimationMm = estimation * 1000.0;

                // カタログ値は全径で積算基準値より大きい。
                Xunit.Assert.True(catalogHeights[i] > estimationMm,
                    string.Format("φ{0}: カタログ {1} ≦ 積算基準 {2}",
                        diameters[i], catalogHeights[i], estimationMm));

                System.Text.Json.JsonElement row = comparison.GetProperty(diameters[i].ToString());
                Xunit.Assert.Equal(estimationMm, row.GetProperty("estimation").GetDouble(), 0.001);
                Xunit.Assert.Equal(catalogHeights[i], row.GetProperty("catalog").GetDouble(), 0.001);
                Xunit.Assert.Equal(
                    catalogHeights[i] - estimationMm, row.GetProperty("diff").GetDouble(), 0.001);
                compared++;
            }

            int recorded = 0;
            foreach (System.Text.Json.JsonProperty unused in comparison.EnumerateObject())
            {
                recorded++;
            }

            // 積算基準表は φ38〜φ65 の 10 径のみ規定されている。
            Xunit.Assert.Equal(10, compared);
            Xunit.Assert.Equal(10, recorded);
        }
    }
}
