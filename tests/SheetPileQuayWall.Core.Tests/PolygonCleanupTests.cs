// T1287〜T1297: PolygonCleanup(継手断面の縮退辺・共線点除去)の単体テスト
// 検証基準: 2026-07-29 実機で SPQW_FRONTWALL_Create が eInvalidInput でクラッシュした
//   事象の原因調査。JointShapes(007 の DXF 抽出データ)に含まれる極端に短い辺
//   (LT65/75/100 の LoopsB で実測 0.0023mm)が、AutoCAD の Region.CreateFromCurves に
//   拒否されていた。縮退辺除去だけでは不十分で、隣接3点が完全に同一直線上にあり
//   進行方向が反転する極薄のノッチ(同ファイルの LoopsB、ある点から35.7mm進んだ直後
//   わずか0.177mm逆方向に戻る)も同様に拒否されることが同日 2回目の実機確認で判明した。

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

        // T1293: 隣接3点が完全に同一直線上にある場合、中間点(進行方向が反転していても)
        //        を除去する。実機で見つかった LT65 LoopsB #7-#8-#9 の再現ケース
        //        (X座標が同じ縦線上で、下に35.7mm進んだ直後わずかに逆戻りする)
        [Xunit.Fact]
        public void T1293_RemoveNearCollinearVertices_RemovesReversingNotch()
        {
            double[] loop =
            {
                0.0, 0.0,
                0.02993, 0.0539777,   // #7 相当
                0.02993, 0.0183007,   // #8 相当(除去されるべき中間点)
                0.02993, 0.0184777,   // #9 相当
                0.05, 0.02,
            };
            double[] result = SheetPileQuayWall.Core.FrontWall.PolygonCleanup
                .RemoveNearCollinearVertices(loop);

            Xunit.Assert.Equal(8, result.Length); // 5頂点 → 4頂点
            for (int i = 0; i + 1 < result.Length; i += 2)
            {
                Xunit.Assert.NotEqual(0.0183007, result[i + 1], 6);
            }
        }

        // T1294: 通常の(共線でない)頂点は保持される
        [Xunit.Fact]
        public void T1294_RemoveNearCollinearVertices_KeepsNonCollinearVertices()
        {
            double[] loop = { 0.0, 0.0, 1.0, 0.0, 1.0, 1.0, 0.0, 1.0 };
            double[] result = SheetPileQuayWall.Core.FrontWall.PolygonCleanup
                .RemoveNearCollinearVertices(loop);

            Xunit.Assert.Equal(loop.Length, result.Length);
        }

        // T1295: 除去後に頂点が4未満になる場合は元の配列をそのまま返す
        [Xunit.Fact]
        public void T1295_RemoveNearCollinearVertices_TooFewVertices_ReturnsOriginal()
        {
            double[] loop = { 0.0, 0.0, 1.0, 0.0, 2.0, 0.0 }; // 3点とも同一直線上
            double[] result = SheetPileQuayWall.Core.FrontWall.PolygonCleanup
                .RemoveNearCollinearVertices(loop);

            Xunit.Assert.Equal(loop, result);
        }

        // T1296: 縮退辺除去 → 共線点除去の順で適用すると、実データ(全 JointType)に
        //        縮退辺・共線点のどちらも残らないこと(SolidBuilder.JointMember と同じ手順)
        [Xunit.Theory]
        [Xunit.InlineData(SheetPileQuayWall.Core.FrontWall.JointType.LT65)]
        [Xunit.InlineData(SheetPileQuayWall.Core.FrontWall.JointType.LT75)]
        [Xunit.InlineData(SheetPileQuayWall.Core.FrontWall.JointType.LT100)]
        [Xunit.InlineData(SheetPileQuayWall.Core.FrontWall.JointType.PP)]
        [Xunit.InlineData(SheetPileQuayWall.Core.FrontWall.JointType.PT)]
        public void T1296_FullCleanupPipeline_NoDegenerateOrCollinearVertices(
            SheetPileQuayWall.Core.FrontWall.JointType jt)
        {
            foreach (double[] loop in SheetPileQuayWall.Core.FrontWall.JointShapes.LoopsA(jt))
            {
                AssertCleanPolygon(loop);
            }
            foreach (double[] loop in SheetPileQuayWall.Core.FrontWall.JointShapes.LoopsB(jt))
            {
                AssertCleanPolygon(loop);
            }
        }

        // T1297: LT65/75/100 の LoopsB(共通形状)は、フルクリーニング後に自己交差しない
        [Xunit.Theory]
        [Xunit.InlineData(SheetPileQuayWall.Core.FrontWall.JointType.LT65)]
        [Xunit.InlineData(SheetPileQuayWall.Core.FrontWall.JointType.LT75)]
        [Xunit.InlineData(SheetPileQuayWall.Core.FrontWall.JointType.LT100)]
        public void T1297_FullCleanupPipeline_LoopsB_NoSelfIntersection(
            SheetPileQuayWall.Core.FrontWall.JointType jt)
        {
            foreach (double[] loop in SheetPileQuayWall.Core.FrontWall.JointShapes.LoopsB(jt))
            {
                double[] cleaned = SheetPileQuayWall.Core.FrontWall.PolygonCleanup
                    .RemoveNearCollinearVertices(
                        SheetPileQuayWall.Core.FrontWall.PolygonCleanup.RemoveDegenerateVertices(loop));
                Xunit.Assert.False(HasSelfIntersection(cleaned));
            }
        }

        private static void AssertCleanPolygon(double[] loop)
        {
            double[] cleaned = SheetPileQuayWall.Core.FrontWall.PolygonCleanup
                .RemoveNearCollinearVertices(
                    SheetPileQuayWall.Core.FrontWall.PolygonCleanup.RemoveDegenerateVertices(loop));

            int n = cleaned.Length / 2;
            for (int i = 0; i < n; i++)
            {
                int prevIdx = (i - 1 + n) % n;
                int nextIdx = (i + 1) % n;
                double ax = cleaned[2 * prevIdx], ay = cleaned[2 * prevIdx + 1];
                double bx = cleaned[2 * i], by = cleaned[2 * i + 1];
                double cx = cleaned[2 * nextIdx], cy = cleaned[2 * nextIdx + 1];

                double edge = System.Math.Sqrt((bx - cx) * (bx - cx) + (by - cy) * (by - cy));
                Xunit.Assert.True(edge >= 0.00001,
                    $"縮退辺が残っている: ({bx},{by})-({cx},{cy}) 距離={edge * 1000:F5}mm");

                double area2 = System.Math.Abs((bx - ax) * (cy - ay) - (cx - ax) * (by - ay));
                double baseLen = System.Math.Sqrt((cx - ax) * (cx - ax) + (cy - ay) * (cy - ay));
                double perp = baseLen < 1e-12 ? 0.0 : area2 / baseLen;
                Xunit.Assert.True(perp >= 0.00001,
                    $"共線点が残っている: 頂点#{i} ({bx},{by}) 垂線距離={perp * 1000:F5}mm");
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
