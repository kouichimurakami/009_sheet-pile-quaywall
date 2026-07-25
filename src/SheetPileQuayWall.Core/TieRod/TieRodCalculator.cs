// === 参照DLLバージョン検証済み ===
// 本ファイルは AutoCAD / Civil 3D / Dynamo のアセンブリを参照しない（純計算層）。
// 参照は .NET 8.0 BCL のみ。検証対象DLLなし。
// 検証日: 2026-07-25 / 検証コマンド: scripts/verify-dll-versions.ps1

namespace SheetPileQuayWall.Core.TieRod
{
    /// <summary>タイロッドの派生量を算出する。</summary>
    public static class TieRodCalculator
    {
        /// <summary>本体本数がこの値以上のとき、カタログの標準構成図（2〜4 本継ぎ）の範囲外となる。</summary>
        private const int CatalogMaxSegments = 4;

        /// <summary>
        /// 継手方法と受杭箇所数が切り替わる延長の閾値 (m)。積算基準 3-4.5-(13)(14) で
        /// 両者は同じ 15 m / 20 m 区分を用いるため、定数を共有して乖離を防ぐ。
        /// </summary>
        private const double SpanThresholdMid = 15.0;
        private const double SpanThresholdLong = 20.0;

        /// <summary>
        /// 派生量を算出する。整合性チェックに違反がある場合は例外で停止し、生成を行わない
        /// (CLAUDE.PRIVATE.md §6-5)。
        /// </summary>
        public static TieRodResult Compute(TieRodParameters p)
        {
            if (p == null)
            {
                throw new System.ArgumentNullException(nameof(p));
            }

            System.Collections.Generic.IReadOnlyList<string> errors = p.Validate();
            if (errors.Count > 0)
            {
                throw new System.ArgumentException(
                    "パラメータ整合性チェックに違反があります:" + System.Environment.NewLine
                    + string.Join(System.Environment.NewLine, errors));
            }

            TieRodResult r = new TieRodResult();

            // 入力誤差（例 0.0475 m）を派生量へ伝播させないため、カタログ規格径の
            // 正確な値へスナップする。Validate 通過後なので必ず成功する。
            double nominalDiameter;
            TieRodCatalog.TrySnapToStandard(p.RodDiameter, out nominalDiameter);
            r.NominalDiameter = nominalDiameter;

            // --- 長さと座標 ---------------------------------------------------------
            // 積算基準 3-4.5-(13):
            //   全長 = 法線直角方向延長 + (t1 + t2 + ナット高さ + 調節長) × 2 + 溝形鋼高さ h
            double anchorExtension =
                p.WasherThickness + p.PlateThickness + p.NutHeight + p.AdjustLength;

            r.SeaEndX = -(p.WalingHeight + anchorExtension);
            r.LandEndX = p.SpanLength + anchorExtension;
            r.TotalLength = r.LandEndX - r.SeaEndX;
            r.AxisZ = p.TieElevation;

            double[] positions = new double[p.TieCount];
            for (int i = 0; i < p.TieCount; i++)
            {
                positions[i] = i * p.TieSpacing;
            }
            r.RodPositionsY = positions;

            // --- 断面・質量 ---------------------------------------------------------
            r.SectionArea = TieRodCatalog.SectionArea(nominalDiameter);
            r.Volume = r.SectionArea * r.TotalLength;
            r.UnitMass = TieRodCatalog.UnitMass(nominalDiameter);
            r.RodMass = r.UnitMass * r.TotalLength;
            r.TotalRodMass = r.RodMass * p.TieCount;

            // --- 継手構成（積算基準 3-4.5-(13) 継手方法表）---------------------------
            SetJointCounts(r, p.SpanLength, nominalDiameter);
            r.BeyondCatalogStandard = r.SegmentCount > CatalogMaxSegments;

            // --- 受杭（積算基準 3-4.5-(14)）------------------------------------------
            // ①法線直角方向: 延長により 1 本あたり 1〜3 ヶ所
            // ②法線方向    : タイロッド 1 本おきに受杭を入れる → 対象組数は切り上げ半数
            r.SupportPileCount = SupportPileCount(p.SpanLength);
            r.SupportedRodCount = (p.TieCount + 1) / 2;
            r.TotalSupportPileCount = r.SupportPileCount * r.SupportedRodCount;

            // --- 張力照査 -----------------------------------------------------------
            r.AllowableTension =
                TieRodCatalog.AllowableTension(p.Grade, p.Code, p.State, nominalDiameter);

            if (p.AnchorReaction > 0.0)
            {
                // 傾斜角は現時点で考慮しない (θ = 0, cosθ = 1)。
                // 傾斜を導入する際は T = Ap × ℓ / cosθ に戻すこと。
                r.DesignTension = p.AnchorReaction * p.TieSpacing;
                r.TensionRatio = r.DesignTension / r.AllowableTension;
                r.TensionChecked = true;
                r.TensionOk = r.TensionRatio <= 1.0;
            }

            return r;
        }

        /// <summary>
        /// 継手方法（積算基準 3-4.5-(13)）。
        ///   延長 15 m 未満              : 本体 4 本 / ターンバックル 1 個 / リングジョイント 2 個
        ///   延長 15 m 以上 20 m 未満    : 本体 5 本 / 2 個 / 2 個
        ///   延長 20 m 以上 または φ55 以上 : 本体 6 本 / 2 個 / 3 個
        /// </summary>
        private static void SetJointCounts(TieRodResult r, double spanLength, double diameter)
        {
            bool largeDiameter = diameter >= 0.055 - TieRodCatalog.Tolerance;

            if (spanLength >= SpanThresholdLong - TieRodCatalog.Tolerance || largeDiameter)
            {
                r.SegmentCount = 6;
                r.TurnbuckleCount = 2;
                r.RingJointCount = 3;
            }
            else if (spanLength >= SpanThresholdMid - TieRodCatalog.Tolerance)
            {
                r.SegmentCount = 5;
                r.TurnbuckleCount = 2;
                r.RingJointCount = 2;
            }
            else
            {
                r.SegmentCount = 4;
                r.TurnbuckleCount = 1;
                r.RingJointCount = 2;
            }
        }

        /// <summary>
        /// 受杭の箇所数（積算基準 3-4.5-(13) 法線直角方向）。
        ///   15 m 未満 : 1 ヶ所 / 15 m 以上 20 m 未満 : 2 ヶ所 / 20 m 以上 : 3 ヶ所
        /// </summary>
        private static int SupportPileCount(double spanLength)
        {
            if (spanLength >= SpanThresholdLong - TieRodCatalog.Tolerance)
            {
                return 3;
            }
            if (spanLength >= SpanThresholdMid - TieRodCatalog.Tolerance)
            {
                return 2;
            }
            return 1;
        }
    }
}
