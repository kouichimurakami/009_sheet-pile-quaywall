// T051〜T059: JointShapes (継手断面形状データ) の単体テスト
// 検証基準: 日本製鉄カタログ K011 / JFE d1j-503 表4 の断面寸法 (単位 m)
//   LT65: L-65×65×8 / LT75: L-75×75×9 / LT100: L-100×75×9 (いずれもスロット30mm)
//   PT B側: CT形鋼 76×85×9×9 / LT B側: T-125×9 / PP: φ165.2 継手鋼管の爪付き帯

namespace SheetPileQuayWall.Core.Tests
{
    public class JointShapesTests
    {
        private static readonly SheetPileQuayWall.Core.FrontWall.JointType[] AllTypes =
        {
            SheetPileQuayWall.Core.FrontWall.JointType.LT65,
            SheetPileQuayWall.Core.FrontWall.JointType.LT75,
            SheetPileQuayWall.Core.FrontWall.JointType.LT100,
            SheetPileQuayWall.Core.FrontWall.JointType.PP,
            SheetPileQuayWall.Core.FrontWall.JointType.PT,
        };

        // ── ヘルパ ─────────────────────────────────────────────────────────

        private static double MaxX(double[] loop)
        {
            double m = double.MinValue;
            for (int i = 0; i < loop.Length; i += 2)
                if (loop[i] > m) m = loop[i];
            return m;
        }

        private static double MaxY(double[] loop)
        {
            double m = double.MinValue;
            for (int i = 1; i < loop.Length; i += 2)
                if (loop[i] > m) m = loop[i];
            return m;
        }

        private static double MinY(double[] loop)
        {
            double m = double.MaxValue;
            for (int i = 1; i < loop.Length; i += 2)
                if (loop[i] < m) m = loop[i];
            return m;
        }

        // ── テスト ─────────────────────────────────────────────────────────

        // T051: 全型式で LoopsA / LoopsB が非空、各ループは頂点3個以上・偶数長
        [Xunit.Fact]
        public void T051_AllTypes_LoopsNonEmptyAndWellFormed()
        {
            foreach (SheetPileQuayWall.Core.FrontWall.JointType jt in AllTypes)
            {
                foreach (double[][] loops in new[]
                {
                    SheetPileQuayWall.Core.FrontWall.JointShapes.LoopsA(jt),
                    SheetPileQuayWall.Core.FrontWall.JointShapes.LoopsB(jt),
                })
                {
                    Xunit.Assert.NotEmpty(loops);
                    foreach (double[] loop in loops)
                    {
                        Xunit.Assert.True(loop.Length >= 6, $"{jt}: 頂点数不足");
                        Xunit.Assert.True(loop.Length % 2 == 0, $"{jt}: 座標数が奇数");
                    }
                }
            }
        }

        // T052: 単位検査 — 全ローカル座標 |v| < 0.5 m (mm 混入検出)
        [Xunit.Fact]
        public void T052_AllCoordinates_AreMeters()
        {
            foreach (SheetPileQuayWall.Core.FrontWall.JointType jt in AllTypes)
            {
                foreach (double[][] loops in new[]
                {
                    SheetPileQuayWall.Core.FrontWall.JointShapes.LoopsA(jt),
                    SheetPileQuayWall.Core.FrontWall.JointShapes.LoopsB(jt),
                })
                {
                    foreach (double[] loop in loops)
                        foreach (double v in loop)
                            Xunit.Assert.True(System.Math.Abs(v) < 0.5,
                                $"{jt}: 座標 {v} が 0.5m 超 — mm 混入の疑い");
                }
            }
        }

        // T053〜T055: LT系 A側 (L形鋼チャンネル) の寸法とミラー対称
        //   全高 = スロット0.030 + 2×周方向脚長、板厚 = 呼称値

        private static void AssertLtASide(
            SheetPileQuayWall.Core.FrontWall.JointType jt,
            double legX_m, double legY_m, double t_m)
        {
            double[][] loops = SheetPileQuayWall.Core.FrontWall.JointShapes.LoopsA(jt);
            Xunit.Assert.Equal(2, loops.Length);
            double[] up = loops[0];
            double[] lo = loops[1];

            // ミラー対称 (x 同一, y 反転)
            Xunit.Assert.Equal(up.Length, lo.Length);
            for (int i = 0; i < up.Length; i += 2)
            {
                Xunit.Assert.Equal(up[i], lo[i], 9);
                Xunit.Assert.Equal(up[i + 1], -lo[i + 1], 9);
            }

            double h = 0.030 + 2.0 * legY_m;
            Xunit.Assert.Equal(legX_m, MaxX(up), 9);        // 半径方向脚長
            Xunit.Assert.Equal(h / 2.0, MaxY(up), 9);       // 全高/2
            Xunit.Assert.Equal(0.015, MinY(up), 9);         // スロット半幅
            Xunit.Assert.Equal(legX_m - t_m,                // ウェブ内面 = 脚長 − 板厚
                SecondMaxX(up), 9);
        }

        private static double SecondMaxX(double[] loop)
        {
            double max = MaxX(loop);
            double second = double.MinValue;
            for (int i = 0; i < loop.Length; i += 2)
                if (loop[i] < max - 1e-12 && loop[i] > second) second = loop[i];
            return second;
        }

        // T053: LT65 A側 = L-65×65×8 (全高 160mm)
        [Xunit.Fact]
        public void T053_LT65_ASide_MatchesCatalog()
        {
            AssertLtASide(SheetPileQuayWall.Core.FrontWall.JointType.LT65, 0.065, 0.065, 0.008);
        }

        // T054: LT75 A側 = L-75×75×9 (全高 180mm)
        [Xunit.Fact]
        public void T054_LT75_ASide_MatchesCatalog()
        {
            AssertLtASide(SheetPileQuayWall.Core.FrontWall.JointType.LT75, 0.075, 0.075, 0.009);
        }

        // T055: LT100 A側 = L-100×75×9 (半径方向脚100mm・全高 180mm)
        [Xunit.Fact]
        public void T055_LT100_ASide_MatchesCatalog()
        {
            AssertLtASide(SheetPileQuayWall.Core.FrontWall.JointType.LT100, 0.100, 0.075, 0.009);
        }

        // T056: PT B側 = CT形鋼 76×85×9×9 (x: 0〜0.076, y: ±0.0425)
        [Xunit.Fact]
        public void T056_PT_BSide_IsCtSteel76x85()
        {
            double[][] loops = SheetPileQuayWall.Core.FrontWall.JointShapes.LoopsB(
                SheetPileQuayWall.Core.FrontWall.JointType.PT);
            Xunit.Assert.Single(loops);
            Xunit.Assert.Equal(0.076, MaxX(loops[0]), 9);
            Xunit.Assert.Equal(0.0425, MaxY(loops[0]), 9);
            Xunit.Assert.Equal(-0.0425, MinY(loops[0]), 9);
        }

        // T057: PP A/B側 = 継手鋼管の爪付き帯 (頂点100以上、半径方向最大 ≈ 165.1mm)
        [Xunit.Fact]
        public void T057_PP_BothSides_AreClawBands()
        {
            foreach (double[][] loops in new[]
            {
                SheetPileQuayWall.Core.FrontWall.JointShapes.LoopsA(
                    SheetPileQuayWall.Core.FrontWall.JointType.PP),
                SheetPileQuayWall.Core.FrontWall.JointShapes.LoopsB(
                    SheetPileQuayWall.Core.FrontWall.JointType.PP),
            })
            {
                Xunit.Assert.Single(loops);
                Xunit.Assert.True(loops[0].Length >= 200, "PP: 頂点数不足");
                Xunit.Assert.InRange(MaxX(loops[0]), 0.164, 0.166);
            }
        }

        // T058: LT系 B側 = T-125×9 (y スパン 125mm・全 LT 型式で同一データ)
        [Xunit.Fact]
        public void T058_LT_BSide_IsT125_SharedAcrossTypes()
        {
            double[][] lt65 = SheetPileQuayWall.Core.FrontWall.JointShapes.LoopsB(
                SheetPileQuayWall.Core.FrontWall.JointType.LT65);
            Xunit.Assert.Single(lt65);
            Xunit.Assert.Equal(0.125, MaxY(lt65[0]) - MinY(lt65[0]), 4);

            foreach (SheetPileQuayWall.Core.FrontWall.JointType jt in new[]
            {
                SheetPileQuayWall.Core.FrontWall.JointType.LT75,
                SheetPileQuayWall.Core.FrontWall.JointType.LT100,
            })
            {
                double[][] other = SheetPileQuayWall.Core.FrontWall.JointShapes.LoopsB(jt);
                Xunit.Assert.Equal(lt65[0], other[0]);
            }
        }

        // T059: 2D 断面 (CurvesA / CurvesB) が全型式で非空
        [Xunit.Fact]
        public void T059_AllTypes_CurvesNonEmpty()
        {
            foreach (SheetPileQuayWall.Core.FrontWall.JointType jt in AllTypes)
            {
                Xunit.Assert.NotEmpty(SheetPileQuayWall.Core.FrontWall.JointShapes.CurvesA(jt));
                Xunit.Assert.NotEmpty(SheetPileQuayWall.Core.FrontWall.JointShapes.CurvesB(jt));
            }
        }
    }
}
