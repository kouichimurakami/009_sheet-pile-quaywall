// === 参照DLLバージョン検証済み ===
// 本ファイルは AutoCAD / Civil 3D / Dynamo のアセンブリを参照しない（純計算層）。
// 参照は .NET 8.0 BCL のみ。検証対象DLLなし。
// 検証日: 2026-07-25 / 検証コマンド: scripts/verify-dll-versions.ps1

namespace SheetPileQuayWall.Core.TieRod
{
    /// <summary>タイロッドの派生量。長さは m、質量は kg、張力は kN。</summary>
    public sealed class TieRodResult
    {
        /// <summary>タイロッド全長 (m)。積算基準の長さ算定式による。</summary>
        public double TotalLength { get; internal set; }

        /// <summary>海側端の X 座標 (m)。負値。X = 0 の鋼管矢板中心を横断する。</summary>
        public double SeaEndX { get; internal set; }

        /// <summary>陸側端の X 座標 (m)。</summary>
        public double LandEndX { get; internal set; }

        /// <summary>タイロッド軸心の Z 座標 (m, D.L. 基準)。</summary>
        public double AxisZ { get; internal set; }

        /// <summary>各組の Y 座標 (m)。要素数は組数に等しい。</summary>
        public System.Collections.Generic.IReadOnlyList<double> RodPositionsY { get; internal set; }
            = System.Array.Empty<double>();

        /// <summary>カタログ規格径へスナップした呼び径 (m)。派生量は全てこの値による。</summary>
        public double NominalDiameter { get; internal set; }

        /// <summary>断面積 (m^2)。</summary>
        public double SectionArea { get; internal set; }

        /// <summary>1 組あたりの体積 (m^3)。</summary>
        public double Volume { get; internal set; }

        /// <summary>棒部の単位質量 (kg/m)。</summary>
        public double UnitMass { get; internal set; }

        /// <summary>1 組あたりの棒部質量 (kg)。付属品は含まない。</summary>
        public double RodMass { get; internal set; }

        /// <summary>全組の棒部質量合計 (kg)。</summary>
        public double TotalRodMass { get; internal set; }

        /// <summary>1 組あたりの本体本数（継手方法表）。</summary>
        public int SegmentCount { get; internal set; }

        /// <summary>1 組あたりのターンバックル個数。</summary>
        public int TurnbuckleCount { get; internal set; }

        /// <summary>1 組あたりのリングジョイント個数。</summary>
        public int RingJointCount { get; internal set; }

        /// <summary>受杭を設置するタイロッド 1 本あたりの受杭箇所数（法線直角方向）。</summary>
        public int SupportPileCount { get; internal set; }

        /// <summary>
        /// 受杭を設置するタイロッドの組数。積算基準 3-4.5-(14) ②法線方向
        /// 「タイロッド１本おきに受杭を入れる」により、組数の切り上げ半数となる。
        /// </summary>
        public int SupportedRodCount { get; internal set; }

        /// <summary>全組の受杭合計 (ヶ所) = 1 本あたり箇所数 × 受杭対象組数。</summary>
        public int TotalSupportPileCount { get; internal set; }

        /// <summary>許容張力 (kN)。</summary>
        public double AllowableTension { get; internal set; }

        /// <summary>作用張力 (kN)。取付点反力 Ap × 取付間隔。反力未入力のとき 0。</summary>
        public double DesignTension { get; internal set; }

        /// <summary>照査比（作用張力 / 許容張力）。反力未入力のとき 0。</summary>
        public double TensionRatio { get; internal set; }

        /// <summary>張力照査を実施したか。取付点反力が未入力のとき false。</summary>
        public bool TensionChecked { get; internal set; }

        /// <summary>張力照査の判定。未実施のとき true 扱いとしない（TensionChecked と併せて判断する）。</summary>
        public bool TensionOk { get; internal set; }

        /// <summary>
        /// 本体本数が 5 以上で、カタログの標準構成図（2〜4 本継ぎ）の範囲外であることを示す。
        /// true のときセグメント配置は等分割による推定値となる。
        /// </summary>
        public bool BeyondCatalogStandard { get; internal set; }
    }
}
