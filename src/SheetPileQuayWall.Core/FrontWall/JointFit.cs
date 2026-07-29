// AutoCAD 非依存 — xUnit で単体テスト可能
//
// 継手 A側(Trailing, +Y側)・B側(Leading, −Y側)を、カタログ有効幅 B のピッチで
// 配置したとき、2D 断面(JointShapes)が幾何学的に交差(干渉)しないことを検証する。
//
// 対象は「山形鋼を T形鋼の溝へ差し込む」構造の LT65/LT75/LT100 のみ。
// PP/PT は「円形継手管(φ165.2)同士が絡み合う」構造であり、本モジュールの単純な
// 多角形交差判定では噛み合わせの実態を正しく評価できないため対象外とする
// (2026-07-29 判断。別途 3D 的な評価が必要)。
//
// 検証方法: A側は φ=+90° で原点配置、B側は φ=−90° で配置したのち Y 方向に
// 有効幅 B だけシフトする(JointPlacement.TransformLoop と同じ変換式)。
// 2 つの多角形集合について、(1) 頂点が相手の多角形内部にあるか、
// (2) 辺同士が交差するか、の両方を調べて干渉の有無を判定する。
// 交差しない場合は最近接距離(辺同士の最短距離)を最小離隔として返す。
//
// PpPipeCenterDistance は上記とは別の検証(2026-07-29 追加)。P-P形は A側・B側とも
// φ165.2mm鋼管(JointCatalog.Pipe)であり、2本の鋼管が「絡み合う」構造のため
// 2D断面は正しい組立でも重なる(= 上記の交差判定はそもそも使えない)。代わりに
// 2本の鋼管の中心間距離を計算し、パイプ半径(82.6mm)にほぼ一致することを確認する
// (JointGeometry の J=0.2478m が、この中心間距離をパイプ半径に保つよう
// K011 カタログ側で調整済みであることの裏付け)。P-T形は B側が T形鋼(パイプではない)
// のため本関数の対象外(A側どうしの比較という前提が成立しない)。

namespace SheetPileQuayWall.Core.FrontWall
{
    public static class JointFit
    {
        public static bool IsInterlockingType(JointType jt)
        {
            return jt == JointType.LT65 || jt == JointType.LT75 || jt == JointType.LT100;
        }

        // A側・B側の断面が交差(干渉)するか。
        public static bool Overlaps(JointType jt, double D_m)
        {
            RequireInterlockingType(jt);
            return Evaluate(jt, D_m).Overlaps;
        }

        // 交差しない場合の最小離隔 [m]。交差する場合は例外(呼び出し前に Overlaps を確認すること)。
        public static double MinClearance(JointType jt, double D_m)
        {
            RequireInterlockingType(jt);
            (bool overlaps, double minClearance_m) = Evaluate(jt, D_m);
            if (overlaps)
            {
                throw new System.InvalidOperationException(
                    $"{jt} D={D_m * 1000:F0}mm は継手同士が交差(干渉)しているため、" +
                    "最小離隔を定義できません。");
            }
            return minClearance_m;
        }

        // P-P形の継手金物(φ165.2mm鋼管)どうしの中心間距離 [m]。
        // A側・B側とも JointShapes の先頭要素(外径 R=0.0826 の円弧)が鋼管の外周を表す
        // (JointShapes.cs は自動生成・手編集禁止のため、この前提の変化は
        // JointFitTests の回帰テストで検出する)。
        public static double PpPipeCenterDistance(double D_m)
        {
            double r = D_m / 2.0;
            double j = JointGeometry.JointSpacing(D_m, JointType.PP);
            double b = D_m + j;

            JointArc2d pipeA = (JointArc2d)JointShapes.CurvesA(JointType.PP)[0];
            JointArc2d pipeB = (JointArc2d)JointShapes.CurvesB(JointType.PP)[0];

            double xA = JointPlacement.TransformX(pipeA.Cx, pipeA.Cy, r, System.Math.PI / 2.0);
            double yA = JointPlacement.TransformY(pipeA.Cx, pipeA.Cy, r, System.Math.PI / 2.0);
            double xB = JointPlacement.TransformX(pipeB.Cx, pipeB.Cy, r, -System.Math.PI / 2.0);
            double yB = JointPlacement.TransformY(pipeB.Cx, pipeB.Cy, r, -System.Math.PI / 2.0) + b;

            double dx = xB - xA;
            double dy = yB - yA;
            return System.Math.Sqrt(dx * dx + dy * dy);
        }

        private static (bool Overlaps, double MinClearance_m) Evaluate(JointType jt, double D_m)
        {
            double r = D_m / 2.0;
            double j = JointGeometry.JointSpacing(D_m, jt);
            // LT100 はカタログ式が無く D+0.100 を推定値として使う(JointParameters と同じ)
            double b = double.IsNaN(j) ? D_m + 0.100 : D_m + j;

            System.Collections.Generic.List<(double X, double Y)[]> loopsA =
                TransformLoops(JointShapes.LoopsA(jt), r, System.Math.PI / 2.0, 0.0);
            System.Collections.Generic.List<(double X, double Y)[]> loopsB =
                TransformLoops(JointShapes.LoopsB(jt), r, -System.Math.PI / 2.0, b);

            bool overlaps = false;
            double minDist = double.MaxValue;

            foreach ((double X, double Y)[] pa in loopsA)
            {
                foreach ((double X, double Y)[] pb in loopsB)
                {
                    foreach ((double X, double Y) p in pa)
                    {
                        if (PointInPolygon(p, pb)) { overlaps = true; }
                    }
                    foreach ((double X, double Y) p in pb)
                    {
                        if (PointInPolygon(p, pa)) { overlaps = true; }
                    }

                    for (int i = 0; i < pa.Length; i++)
                    {
                        (double X, double Y) a0 = pa[i];
                        (double X, double Y) a1 = pa[(i + 1) % pa.Length];
                        for (int k = 0; k < pb.Length; k++)
                        {
                            (double X, double Y) b0 = pb[k];
                            (double X, double Y) b1 = pb[(k + 1) % pb.Length];
                            if (SegmentsIntersect(a0, a1, b0, b1)) { overlaps = true; }

                            double d = SegmentDistance(a0, a1, b0, b1);
                            if (d < minDist) { minDist = d; }
                        }
                    }
                }
            }

            return (overlaps, minDist);
        }

        private static void RequireInterlockingType(JointType jt)
        {
            if (!IsInterlockingType(jt))
            {
                throw new System.ArgumentException(
                    $"{jt} は差し込み型(LT65/LT75/LT100)ではないため JointFit の対象外です。" +
                    "PP/PT(円形継手管の絡み合い型)は別の評価方法が必要です。", nameof(jt));
            }
        }

        private static System.Collections.Generic.List<(double X, double Y)[]> TransformLoops(
            double[][] loops, double r, double phiRad, double shiftY_m)
        {
            System.Collections.Generic.List<(double X, double Y)[]> result =
                new System.Collections.Generic.List<(double X, double Y)[]>();
            foreach (double[] loop in loops)
            {
                (double X, double Y)[] pts = new (double, double)[loop.Length / 2];
                for (int i = 0; i + 1 < loop.Length; i += 2)
                {
                    double wx = JointPlacement.TransformX(loop[i], loop[i + 1], r, phiRad);
                    double wy = JointPlacement.TransformY(loop[i], loop[i + 1], r, phiRad) + shiftY_m;
                    pts[i / 2] = (wx, wy);
                }
                result.Add(pts);
            }
            return result;
        }

        // Ray casting による点-多角形内包判定。
        private static bool PointInPolygon((double X, double Y) p, (double X, double Y)[] poly)
        {
            bool inside = false;
            int n = poly.Length;
            for (int i = 0, k = n - 1; i < n; k = i++)
            {
                double xi = poly[i].X, yi = poly[i].Y, xk = poly[k].X, yk = poly[k].Y;
                bool crosses = ((yi > p.Y) != (yk > p.Y)) &&
                    (p.X < (xk - xi) * (p.Y - yi) / (yk - yi) + xi);
                if (crosses) { inside = !inside; }
            }
            return inside;
        }

        // 線分 a0-a1 と b0-b1 が交差するか(端点での接触は交差扱いにしない)。
        private static bool SegmentsIntersect(
            (double X, double Y) a0, (double X, double Y) a1,
            (double X, double Y) b0, (double X, double Y) b1)
        {
            double d1 = Cross(b0, b1, a0);
            double d2 = Cross(b0, b1, a1);
            double d3 = Cross(a0, a1, b0);
            double d4 = Cross(a0, a1, b1);
            return ((d1 > 0 && d2 < 0) || (d1 < 0 && d2 > 0)) &&
                   ((d3 > 0 && d4 < 0) || (d3 < 0 && d4 > 0));
        }

        private static double Cross(
            (double X, double Y) o, (double X, double Y) a, (double X, double Y) b)
        {
            return (a.X - o.X) * (b.Y - o.Y) - (a.Y - o.Y) * (b.X - o.X);
        }

        // 2 線分間の最短距離(交差する場合は呼び出し側で別途 SegmentsIntersect により検出する)。
        private static double SegmentDistance(
            (double X, double Y) a0, (double X, double Y) a1,
            (double X, double Y) b0, (double X, double Y) b1)
        {
            double d1 = PointToSegmentDistance(a0, b0, b1);
            double d2 = PointToSegmentDistance(a1, b0, b1);
            double d3 = PointToSegmentDistance(b0, a0, a1);
            double d4 = PointToSegmentDistance(b1, a0, a1);
            return System.Math.Min(System.Math.Min(d1, d2), System.Math.Min(d3, d4));
        }

        private static double PointToSegmentDistance(
            (double X, double Y) p, (double X, double Y) s0, (double X, double Y) s1)
        {
            double vx = s1.X - s0.X, vy = s1.Y - s0.Y;
            double wx = p.X - s0.X, wy = p.Y - s0.Y;
            double c1 = vx * wx + vy * wy;
            if (c1 <= 0)
            {
                return System.Math.Sqrt(wx * wx + wy * wy);
            }
            double c2 = vx * vx + vy * vy;
            if (c2 <= c1)
            {
                double dx = p.X - s1.X, dy = p.Y - s1.Y;
                return System.Math.Sqrt(dx * dx + dy * dy);
            }
            double t = c1 / c2;
            double projX = s0.X + t * vx, projY = s0.Y + t * vy;
            double ex = p.X - projX, ey = p.Y - projY;
            return System.Math.Sqrt(ex * ex + ey * ey);
        }
    }
}
