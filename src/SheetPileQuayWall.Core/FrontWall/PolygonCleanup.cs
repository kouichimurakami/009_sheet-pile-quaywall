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
//
// 縮退辺除去だけでは不十分なケースが実機で見つかった: LT65/75/100 共通の LoopsB に、
// 隣接3点が完全に同一直線上にあり、かつ進行方向が反転する極薄のノッチ(実測: ある点から
// 35.7mm 進んだ直後、次の点でわずか0.177mm 逆方向に戻る)が含まれ、これも
// Region.CreateFromCurves を eInvalidInput にしていた(2026-07-29 実機で確認)。
// 縮退辺(距離ゼロ)とは別の問題のため、共線点の除去を別途行う。

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

        // 隣接3点(前, 当該, 次)がほぼ一直線上にある場合、当該点(中間点)を除去する。
        // 距離としては縮退していない(RemoveDegenerateVertices では捕捉できない)が、
        // 直線から外れる量(垂線距離)が極めて小さい点が対象。1 点除去すると新たに
        // 別の共線点が生まれ得るため、除去が起きなくなるまで繰り返す。
        // 除去後に頂点が 3 未満になる場合は元の配列をそのまま返す(RemoveDegenerateVertices と同じ規約)。
        public const double CollinearTolerance_m = 0.00001;

        public static double[] RemoveNearCollinearVertices(double[] loopXY_m)
        {
            System.Collections.Generic.List<double> pts =
                new System.Collections.Generic.List<double>(loopXY_m);

            bool changed = true;
            while (changed)
            {
                int n = pts.Count / 2;
                if (n < 4)
                {
                    break;
                }

                changed = false;
                for (int i = 0; i < n; i++)
                {
                    int prevIdx = (i - 1 + n) % n;
                    int nextIdx = (i + 1) % n;
                    double ax = pts[2 * prevIdx], ay = pts[2 * prevIdx + 1];
                    double bx = pts[2 * i], by = pts[2 * i + 1];
                    double cx = pts[2 * nextIdx], cy = pts[2 * nextIdx + 1];

                    if (PerpendicularDistance(ax, ay, bx, by, cx, cy) < CollinearTolerance_m)
                    {
                        pts.RemoveRange(2 * i, 2);
                        changed = true;
                        break;
                    }
                }
            }

            return pts.Count >= 6 ? pts.ToArray() : loopXY_m;
        }

        // 点 b から、直線 a-c までの垂線距離。a=c(縮退)の場合は 0 を返す
        // (縮退辺の判定は RemoveDegenerateVertices の責務のため、ここでは扱わない)。
        private static double PerpendicularDistance(
            double ax, double ay, double bx, double by, double cx, double cy)
        {
            double area2 = System.Math.Abs((bx - ax) * (cy - ay) - (cx - ax) * (by - ay));
            double baseLen = Distance(ax, ay, cx, cy);
            return baseLen < 1e-12 ? 0.0 : area2 / baseLen;
        }

        private static double Distance(double x1, double y1, double x2, double y2)
        {
            double dx = x1 - x2, dy = y1 - y2;
            return System.Math.Sqrt(dx * dx + dy * dy);
        }
    }
}
