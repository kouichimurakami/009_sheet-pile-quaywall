// T1287〜T1292: PolygonCleanup(継手断面の縮退辺除去)の単体テスト
// 検証基準: 2026-07-29 実機で SPQW_FRONTWALL_Create が eInvalidInput でクラッシュした
//   事象の原因調査。JointShapes(007 の DXF 抽出データ)に含まれる極端に短い辺
//   (LT65/75/100 の LoopsB で実測 0.0023mm)が、AutoCAD の Region.CreateFromCurves に
//   拒否されていた。

namespace SheetPileQuayWall.Core.Tests
{
    public class PolygonCleanupTests
    {
        // T1287: 隣接頂点間の距離が閾値未満なら、後の頂点を除去(マージ)する
        [Xunit.Fact]
        public void T1287_RemoveDegenerateVertices_MergesCloseAdjacentVertices()
        {
            // (0,0) → (0.00001mm 未満の距離で(0,0.000005)) → (1,0) → (1,1)
            double[] loop = { 0.0, 0.0, 0.0, 0.000005, 1.0, 0.0, 1.0, 1.0 };
            double[] result = SheetPileQuayWall.Core.FrontWall.PolygonCleanup
                .RemoveDegenerateVertices(loop);

            Xunit.Assert.Equal(6, result.Length); // 4頂点 → 3頂点
            Xunit.Assert.Equal(0.0, result[0], 9);
            Xunit.Assert.Equal(0.0, result[1], 9);
            Xunit.Assert.Equal(1.0, result[2], 9);
            Xunit.Assert.Equal(0.0, result[3], 9);
        }

        // T1288: 通常の辺(閾値以上の距離)はそのまま保持される
        [Xunit.Fact]
        public void T1288_RemoveDegenerateVertices_KeepsNormalVertices()
        {
            double[] loop = { 0.0, 0.0, 1.0, 0.0, 1.0, 1.0, 0.0, 1.0 };
            double[] result = SheetPileQuayWall.Core.FrontWall.PolygonCleanup
                .RemoveDegenerateVertices(loop);

            Xunit.Assert.Equal(loop.Length, result.Length);
            Xunit.Assert.Equal(loop, result);
        }

        // T1289: 閉じ辺(最後の頂点→最初の頂点)が縮退している場合も除去する
        [Xunit.Fact]
        public void T1289_RemoveDegenerateVertices_ChecksClosingEdge()
        {
            // 最後の頂点 (0.000003, 0) が最初の頂点 (0,0) とほぼ同一
            double[] loop = { 0.0, 0.0, 1.0, 0.0, 1.0, 1.0, 0.0, 1.0, 0.000003, 0.0 };
            double[] result = SheetPileQuayWall.Core.FrontWall.PolygonCleanup
                .RemoveDegenerateVertices(loop);

            Xunit.Assert.Equal(8, result.Length); // 5頂点 → 4頂点
        }

        // T1290: 縮退しきった異常データ(頂点3未満になる)は元の配列をそのまま返す
        [Xunit.Fact]
        public void T1290_RemoveDegenerateVertices_TooFewVertices_ReturnsOriginal()
        {
            double[] loop = { 0.0, 0.0, 0.000001, 0.0, 0.000002, 0.0 };
            double[] result = SheetPileQuayWall.Core.FrontWall.PolygonCleanup
                .RemoveDegenerateVertices(loop);

            Xunit.Assert.Equal(loop, result);
        }

        // T1291: 実データ(全 JointType の LoopsA/LoopsB)で、クリーニング後に
        //        縮退辺(0.001mm 未満)が残らないこと。実機クラッシュの直接の再発防止
        [Xunit.Theory]
        [Xunit.InlineData(SheetPileQuayWall.Core.FrontWall.JointType.LT65)]
        [Xunit.InlineData(SheetPileQuayWall.Core.FrontWall.JointType.LT75)]
        [Xunit.InlineData(SheetPileQuayWall.Core.FrontWall.JointType.LT100)]
        [Xunit.InlineData(SheetPileQuayWall.Core.FrontWall.JointType.PP)]
        [Xunit.InlineData(SheetPileQuayWall.Core.FrontWall.JointType.PT)]
        public void T1291_RealJointShapes_NoDegenerateEdgesAfterCleanup(
            SheetPileQuayWall.Core.FrontWall.JointType jt)
        {
            foreach (double[] loop in SheetPileQuayWall.Core.FrontWall.JointShapes.LoopsA(jt))
            {
                AssertNoDegenerateEdges(
                    SheetPileQuayWall.Core.FrontWall.PolygonCleanup.RemoveDegenerateVertices(loop));
            }
            foreach (double[] loop in SheetPileQuayWall.Core.FrontWall.JointShapes.LoopsB(jt))
            {
                AssertNoDegenerateEdges(
                    SheetPileQuayWall.Core.FrontWall.PolygonCleanup.RemoveDegenerateVertices(loop));
            }
        }

        // T1292: PP/PT の LoopsA は元データでは自己交差判定になっていたが、
        //        その原因はゼロ長辺による交差判定の誤検出だった。クリーニング後は
        //        自己交差しなくなることを確認する(2026-07-29 の発見の記録)
        [Xunit.Theory]
        [Xunit.InlineData(SheetPileQuayWall.Core.FrontWall.JointType.PP)]
        [Xunit.InlineData(SheetPileQuayWall.Core.FrontWall.JointType.PT)]
        public void T1292_PPPT_LoopsA_NoSelfIntersectionAfterCleanup(
            SheetPileQuayWall.Core.FrontWall.JointType jt)
        {
            foreach (double[] loop in SheetPileQuayWall.Core.FrontWall.JointShapes.LoopsA(jt))
            {
                double[] cleaned = SheetPileQuayWall.Core.FrontWall.PolygonCleanup
                    .RemoveDegenerateVertices(loop);
                Xunit.Assert.False(HasSelfIntersection(cleaned));
            }
        }

        private static void AssertNoDegenerateEdges(double[] loop)
        {
            int n = loop.Length / 2;
            for (int i = 0; i < n; i++)
            {
                double x1 = loop[2 * i], y1 = loop[2 * i + 1];
                int j = (i + 1) % n;
                double x2 = loop[2 * j], y2 = loop[2 * j + 1];
                double dx = x1 - x2, dy = y1 - y2;
                double dist = System.Math.Sqrt(dx * dx + dy * dy);
                Xunit.Assert.True(dist >= 0.00001,
                    $"縮退辺が残っている: ({x1},{y1})-({x2},{y2}) 距離={dist * 1000:F5}mm");
            }
        }

        private static bool HasSelfIntersection(double[] loop)
        {
            int n = loop.Length / 2;
            (double X, double Y)[] pts = new (double, double)[n];
            for (int i = 0; i < n; i++)
            {
                pts[i] = (loop[2 * i], loop[2 * i + 1]);
            }

            for (int i = 0; i < n; i++)
            {
                (double X, double Y) a0 = pts[i], a1 = pts[(i + 1) % n];
                for (int j = i + 1; j < n; j++)
                {
                    if (j == i || (j + 1) % n == i || i == (j + 1) % n) { continue; }
                    (double X, double Y) b0 = pts[j], b1 = pts[(j + 1) % n];
                    if (SegmentsIntersect(a0, a1, b0, b1)) { return true; }
                }
            }
            return false;
        }

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
    }
}
