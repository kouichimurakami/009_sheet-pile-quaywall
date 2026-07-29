// === 参照DLLバージョン検証済み ===
// 本ファイルは AutoCAD / Civil 3D / Dynamo のアセンブリを参照しない（純計算層）。
// 参照は .NET 8.0 BCL のみ。検証対象DLLなし。
// 検証日: 2026-07-25 / 検証コマンド: scripts/verify-dll-versions.ps1
//
// 座標系 (CLAUDE.PRIVATE.md §2.2 に対する本構造物固有の定義):
//   X : 陸側 +X / 海側 -X。X = 0 は海側鋼管矢板の中心軸。
//   Y : 施設延長方向。
//   Z : 鉛直上向き。Z = 0 は D.L.（基本水準面）。
//       ※ §2.2 の「底版底面 = Z = 0」は鋼管矢板式に底版が無いため適用しない。

namespace SheetPileQuayWall.Core.TieRod
{
    /// <summary>タイロッドの入力パラメータ。長さは全てメートル。</summary>
    public sealed class TieRodParameters
    {
        /// <summary>タイロッド径 (m)。</summary>
        public double RodDiameter { get; set; } = 0.048;

        /// <summary>鋼種。</summary>
        public SteelGrade Grade { get; set; } = SteelGrade.HT690;

        /// <summary>設計基準。</summary>
        public DesignCode Code { get; set; } = DesignCode.PartialFactor;

        /// <summary>荷重状態。許容応力度法の常時/地震時、部分係数法の永続/変動に対応する。</summary>
        public LoadState State { get; set; } = LoadState.Normal;

        /// <summary>法線直角方向延長 (m)。海側鋼管矢板の中心から控工中心までの距離。</summary>
        public double SpanLength { get; set; } = 10.000;

        /// <summary>海側鋼管矢板の径 (m)。半割の成立性判定に用いる。</summary>
        public double PileDiameter { get; set; } = 1.000;

        /// <summary>鋼管矢板のピッチ (m)。</summary>
        public double PilePitch { get; set; } = 1.200;

        /// <summary>タイロッド取付間隔 (m)。鋼管矢板中央を横断するためピッチの整数倍であること。</summary>
        public double TieSpacing { get; set; } = 2.400;

        /// <summary>タイロッド組数 (組)。1 組につき Solid3d 1 個を生成する。</summary>
        public int TieCount { get; set; } = 1;

        /// <summary>朔望平均満潮面 H.W.L. の標高 (m, D.L. 基準)。現場ごとに設定する。</summary>
        public double Hwl { get; set; } = 1.000;

        /// <summary>タイロッド軸心の標高 (m, D.L. 基準)。既定値は H.W.L. + 0.5 m。</summary>
        public double TieElevation { get; set; } = 1.500;

        /// <summary>腹起し溝形鋼の高さ h (m)。鋼管矢板を半割にして設置するため 0 は許容しない。</summary>
        public double WalingHeight { get; set; } = 0.300;

        /// <summary>定着プレート厚 t2 (m)。</summary>
        public double PlateThickness { get; set; } = 0.025;

        /// <summary>定着ワッシャー厚 t1 (m)。</summary>
        public double WasherThickness { get; set; } = 0.006;

        /// <summary>定着ナット高さ (m)。積算基準の長さ算定に用いる（形状は生成しない）。</summary>
        public double NutHeight { get; set; } = 0.055;

        /// <summary>調節長 (m)。</summary>
        public double AdjustLength { get; set; } = 0.055;

        /// <summary>タイロッド取付点の反力 Ap (kN/m)。0 以下のとき張力照査を行わない。</summary>
        public double AnchorReaction { get; set; } = 0.0;

        /// <summary>ソリッドの色番号 (AutoCAD Color Index)。</summary>
        public int LayerColor { get; set; } = 8;

        /// <summary>英語パラメータ名と日本語説明の対応 (CLAUDE.PRIVATE.md §2.1)。</summary>
        public static readonly System.Collections.Generic.IReadOnlyDictionary<string, string> DisplayNames =
            new System.Collections.Generic.Dictionary<string, string>
            {
                { "rod_diameter",    "タイロッド径" },
                { "steel_grade",     "鋼種" },
                { "design_code",     "設計基準" },
                { "load_state",      "荷重状態" },
                { "span_length",     "法線直角方向延長" },
                { "pile_diameter",   "海側鋼管矢板径" },
                { "pile_pitch",      "鋼管矢板ピッチ" },
                { "tie_spacing",     "タイロッド取付間隔" },
                { "tie_count",       "タイロッド組数" },
                { "hwl",             "朔望平均満潮面" },
                { "tie_elevation",   "タイロッド軸心標高" },
                { "waling_height",   "腹起し溝形鋼高さ" },
                { "plate_thickness", "定着プレート厚" },
                { "washer_thickness","定着ワッシャー厚" },
                { "nut_height",      "定着ナット高さ" },
                { "adjust_length",   "調節長" },
                { "anchor_reaction", "タイロッド取付点反力" },
                { "layer_color",     "ソリッド色番号" }
            };

        /// <summary>H.W.L. から既定のタイロッド軸心標高を求める。</summary>
        public static double DefaultTieElevation(double hwl)
        {
            return hwl + 0.500;
        }

        /// <summary>
        /// 積算基準表（φ38〜φ65）にナット高さがある径なら、ナット高さと調節長を表値に
        /// 設定して true を返す。表に無い径では何も変更せず false を返す。
        /// 表内の径では Validate が表値との一致を要求するため、径を変更したら本メソッドを
        /// 呼び直すこと。
        /// </summary>
        public bool ApplyStandardNutHeight()
        {
            double value;
            if (TieRodCatalog.TryGetNutHeight(RodDiameter, out value))
            {
                NutHeight = value;
                AdjustLength = value;
                return true;
            }
            return false;
        }

        /// <summary>
        /// パラメータ整合性チェック (CLAUDE.PRIVATE.md §6-5)。
        /// 誤差許容は 1 mm = 0.001 m。エラーが 1 件でもあれば生成してはならない。
        /// </summary>
        public System.Collections.Generic.IReadOnlyList<string> Validate()
        {
            System.Collections.Generic.List<string> errors =
                new System.Collections.Generic.List<string>();

            // 1. 径がカタログ規格値であること。mm 値（例 48）を入れた場合もここで捕捉される。
            if (!TieRodCatalog.IsStandardDiameter(RodDiameter))
            {
                errors.Add(string.Format(
                    "タイロッド径 {0:F3} m がカタログ規格径 (φ25〜φ90 の19種) に一致しません。"
                    + "mm 値を入力していないか確認してください（単位はメートル）。",
                    RodDiameter));
            }

            // 2. 設計基準と鋼種の組み合わせ。
            if (Code == DesignCode.PartialFactor && !TieRodCatalog.SupportsPartialFactor(Grade))
            {
                errors.Add(string.Format(
                    "部分係数法（新基準）に対応する鋼種は HT690 と SS400 のみです。指定: {0}", Grade));
            }

            // 3. 各長さの範囲。上限を超える値は mm 混入の可能性が高い。
            AddRangeError(errors, "法線直角方向延長", SpanLength, 3.000, 40.000);
            AddRangeError(errors, "海側鋼管矢板径", PileDiameter, 0.600, 1.600);
            AddRangeError(errors, "鋼管矢板ピッチ", PilePitch, 0.600, 2.000);
            AddRangeError(errors, "タイロッド取付間隔", TieSpacing, 0.600, 20.000);
            AddRangeError(errors, "朔望平均満潮面", Hwl, 0.000, 5.000);
            AddRangeError(errors, "タイロッド軸心標高", TieElevation, -5.000, 10.000);
            AddRangeError(errors, "定着プレート厚", PlateThickness, 0.001, 0.100);
            AddRangeError(errors, "定着ワッシャー厚", WasherThickness, 0.001, 0.100);
            AddRangeError(errors, "定着ナット高さ", NutHeight, 0.001, 0.200);
            AddRangeError(errors, "調節長", AdjustLength, 0.001, 0.200);

            // 4. 腹起し高さ。h = 0 は許容しない（鋼管矢板を半割にして腹起しを設置する設計のため）。
            if (WalingHeight <= TieRodCatalog.Tolerance)
            {
                errors.Add(string.Format(
                    "腹起し溝形鋼高さ h = {0:F3} m は 0 を許容しません。"
                    + "鋼管矢板を半割にして腹起しを設置する設計です。", WalingHeight));
            }
            else if (WalingHeight > PileDiameter + TieRodCatalog.Tolerance)
            {
                errors.Add(string.Format(
                    "腹起し溝形鋼高さ h = {0:F3} m が海側鋼管矢板径 {1:F3} m を超えており、"
                    + "半割部に収まりません。", WalingHeight, PileDiameter));
            }

            // 5. 矢板ピッチが矢板径以上であること。ピッチ < 径 は矢板同士が物理的に重なる。
            if (PilePitch + TieRodCatalog.Tolerance < PileDiameter)
            {
                errors.Add(string.Format(
                    "鋼管矢板ピッチ {0:F3} m が矢板径 {1:F3} m より小さく、矢板同士が重なります。",
                    PilePitch, PileDiameter));
            }

            // 6. 積算基準表（φ38〜φ65）に規定がある径では、ナット高さ・調節長が表値と
            //    一致すること。タイロッド長算定式の根拠が積算基準にあるため、表内の径で
            //    別値を使うと積算根拠が崩れる。表に無い径は利用者の明示入力に委ねる。
            {
                double standardNut;
                if (TieRodCatalog.TryGetNutHeight(RodDiameter, out standardNut))
                {
                    if (System.Math.Abs(NutHeight - standardNut) > TieRodCatalog.Tolerance ||
                        System.Math.Abs(AdjustLength - standardNut) > TieRodCatalog.Tolerance)
                    {
                        errors.Add(string.Format(
                            "φ{0} のナット高さ・調節長は積算基準表により {1:F3} m です"
                            + "（入力: ナット高さ {2:F3} / 調節長 {3:F3}）。"
                            + "ApplyStandardNutHeight() で表値を設定してください。",
                            TieRodCatalog.ToNominalMillimeter(RodDiameter),
                            standardNut, NutHeight, AdjustLength));
                    }
                }
            }

            // 7. 取付間隔が矢板ピッチの整数倍であること。
            //    「タイロッドは海側鋼管矢板の中央を横断する」ことの直接の帰結。
            if (PilePitch > TieRodCatalog.Tolerance)
            {
                double ratio = TieSpacing / PilePitch;
                double deviation = System.Math.Abs(ratio - System.Math.Round(ratio)) * PilePitch;
                if (deviation > TieRodCatalog.Tolerance)
                {
                    errors.Add(string.Format(
                        "タイロッド取付間隔 {0:F3} m が鋼管矢板ピッチ {1:F3} m の整数倍ではありません"
                        + "（ずれ {2:F4} m）。矢板中央を横断できません。",
                        TieSpacing, PilePitch, deviation));
                }
            }

            // 8. 組数と色番号。
            if (TieCount < 1 || TieCount > 200)
            {
                errors.Add(string.Format("タイロッド組数 {0} 組が範囲 1〜200 を外れています。", TieCount));
            }
            if (LayerColor < 1 || LayerColor > 255)
            {
                errors.Add(string.Format("ソリッド色番号 {0} が範囲 1〜255 を外れています。", LayerColor));
            }

            return errors;
        }

        private static void AddRangeError(
            System.Collections.Generic.List<string> errors,
            string label, double value, double min, double max)
        {
            if (value < min || value > max)
            {
                errors.Add(string.Format(
                    "{0} {1:F3} m が範囲 {2:F3}〜{3:F3} m を外れています。"
                    + "単位はメートルです（mm 値の混入に注意）。",
                    label, value, min, max));
            }
        }
    }
}
