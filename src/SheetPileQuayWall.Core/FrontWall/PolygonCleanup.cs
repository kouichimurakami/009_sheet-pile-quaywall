// AutoCAD 非依存 — xUnit で単体テスト可能
//
// 継手断面(JointShapes.LoopsA/LoopsB、007 の DXF 抽出データ)には、抽出時の
// 丸め誤差によって生じたとみられる極端に短い辺(実測 最小 0.0023mm)が含まれる。
// この辺を含んだまま AutoCAD の Region.CreateFromCurves に渡すと eInvalidInput で
// 失敗する(2026-07-29 実機で確認)。JointShapes.cs は自動生成ファイルで手編集禁止
// (再生成で失われるため)なので、押し出しソリッド生成の直前でこの前処理を挟む。
//
// 閾値 0.01mm は、実測した縮退辺(最大 0.0023mm)より一桁大きく、意匠上の
// 最小辺(LT65/75/100 の LoopsB で実測 0.177mm)より一桁小さい値として設定した。

namespace SheetPileQuayWall.Core.FrontWall
{
    public static class PolygonCleanup
    {
        // この距離未満の隣接頂点は同一点とみなしてマージする。
        public const double DegenerateEdgeTolerance_m = 0.00001;

        // 閉ループ頂点列 [x0,y0,x1,y1,...] から、隣接頂点間の距離が
        // DegenerateEdgeTolerance_m 未満の頂点を除去した新しい配列を返す
        // (始点と終点の間の閉じ辺も対象に含める)。
        // 除去後に頂点が 3 未満になる場合は元の配列をそのまま返す
        // (縮退しきった異常データを閉ループとして扱えないため、
        //  呼び出し側の Region 生成でそのまま検出させる)。
        public static double[] RemoveDegenerateVertices(double[] loopXY_m)
        {
            int n = loopXY_m.Length / 2;
            if (n < 3)
            {
                return loopXY_m;
            }

            System.Collections.Generic.List<double> kept =
                new System.Collections.Generic.List<double>(loopXY_m.Length)
                {
                    loopXY_m[0], loopXY_m[1]
                };

            for (int i = 1; i < n; i++)
            {
                double x = loopXY_m[2 * i];
                double y = loopXY_m[2 * i + 1];
                double lastX = kept[kept.Count - 2];
                double lastY = kept[kept.Count - 1];
                if (Distance(x, y, lastX, lastY) >= DegenerateEdgeTolerance_m)
                {
                    kept.Add(x);
                    kept.Add(y);
                }
            }

            // 閉じ辺(最後に残った点 → 最初の点)も縮退していれば末尾を落とす
            while (kept.Count >= 6 &&
                Distance(kept[kept.Count - 2], kept[kept.Count - 1], kept[0], kept[1])
                    < DegenerateEdgeTolerance_m)
            {
                kept.RemoveRange(kept.Count - 2, 2);
            }

            return kept.Count >= 6 ? kept.ToArray() : loopXY_m;
        }

        private static double Distance(double x1, double y1, double x2, double y2)
        {
            double dx = x1 - x2, dy = y1 - y2;
            return System.Math.Sqrt(dx * dx + dy * dy);
        }
    }
}
