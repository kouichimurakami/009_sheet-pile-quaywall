// T1010〜T1014: JointMass の単体テスト
// 検証基準: JointShapes の実形状(DXF 抽出)から確定した側別部材
//   A 側(+Y、雌): LT = 山形鋼×2 / PP・PT = 鋼管 φ165.2×9 (34.7 kg/m)
//   B 側(−Y、雄): LT・PT = T 形鋼 / PP = 鋼管 φ165.2×9
//
// 移植元 007 の JointCatalog.JointMassPerM は P-P 形で鋼管を 1 本分しか数えない。
// T1013 がその差(69.4 と 34.7)を固定し、回帰を検出する。

namespace SheetPileQuayWall.Core.Tests
{
    public class JointMassTests
    {
        // T1010: L-T 形は A 側が山形鋼×2、B 側が T 形鋼
        [Xunit.Theory]
        [Xunit.InlineData(SheetPileQuayWall.Core.FrontWall.JointType.LT65, 15.3, 12.7)]
        [Xunit.InlineData(SheetPileQuayWall.Core.FrontWall.JointType.LT75, 19.9, 12.7)]
        [Xunit.InlineData(SheetPileQuayWall.Core.FrontWall.JointType.LT100, 26.0, 12.7)]
        public void T1010_SideMass_LT(
            SheetPileQuayWall.Core.FrontWall.JointType jt, double expectedA, double expectedB)
        {
            Xunit.Assert.Equal(expectedA,
                SheetPileQuayWall.Core.FrontWall.JointMass.SideA_kgPerM(jt), 6);
            Xunit.Assert.Equal(expectedB,
                SheetPileQuayWall.Core.FrontWall.JointMass.SideB_kgPerM(jt), 6);
        }

        // T1011: P-P 形は両側とも鋼管 φ165.2×9 (34.7 kg/m)
        [Xunit.Fact]
        public void T1011_SideMass_PP_BothSidesArePipe()
        {
            Xunit.Assert.Equal(34.7,
                SheetPileQuayWall.Core.FrontWall.JointMass.SideA_kgPerM(
                    SheetPileQuayWall.Core.FrontWall.JointType.PP), 6);
            Xunit.Assert.Equal(34.7,
                SheetPileQuayWall.Core.FrontWall.JointMass.SideB_kgPerM(
                    SheetPileQuayWall.Core.FrontWall.JointType.PP), 6);
        }

        // T1012: P-T 形は A 側が鋼管、B 側が T 形鋼
        [Xunit.Fact]
        public void T1012_SideMass_PT_PipeAndTee()
        {
            Xunit.Assert.Equal(34.7,
                SheetPileQuayWall.Core.FrontWall.JointMass.SideA_kgPerM(
                    SheetPileQuayWall.Core.FrontWall.JointType.PT), 6);
            Xunit.Assert.Equal(10.9,
                SheetPileQuayWall.Core.FrontWall.JointMass.SideB_kgPerM(
                    SheetPileQuayWall.Core.FrontWall.JointType.PT), 6);
        }

        // T1013: 1 接続あたり質量。P-P 形のみ移植元 JointMassPerM と一致しない
        //        (移植元は鋼管 1 本分 34.7 しか数えないため)
        [Xunit.Fact]
        public void T1013_PerConnection_DiffersFromLegacyForPP()
        {
            SheetPileQuayWall.Core.FrontWall.JointType[] consistent =
            {
                SheetPileQuayWall.Core.FrontWall.JointType.LT65,
                SheetPileQuayWall.Core.FrontWall.JointType.LT75,
                SheetPileQuayWall.Core.FrontWall.JointType.LT100,
                SheetPileQuayWall.Core.FrontWall.JointType.PT
            };
            for (int i = 0; i < consistent.Length; i++)
            {
                Xunit.Assert.Equal(
                    SheetPileQuayWall.Core.FrontWall.JointCatalog.JointMassPerM(consistent[i]),
                    SheetPileQuayWall.Core.FrontWall.JointMass.PerConnection_kgPerM(consistent[i]),
                    6);
            }

            // P-P 形: 正しくは鋼管 2 本分 = 69.4。移植元は 34.7 を返す
            Xunit.Assert.Equal(69.4,
                SheetPileQuayWall.Core.FrontWall.JointMass.PerConnection_kgPerM(
                    SheetPileQuayWall.Core.FrontWall.JointType.PP), 6);
            Xunit.Assert.Equal(34.7,
                SheetPileQuayWall.Core.FrontWall.JointCatalog.JointMassPerM(
                    SheetPileQuayWall.Core.FrontWall.JointType.PP), 6);
        }

        // T1014: 矢板 1 本あたり質量は施工順位で変わる(1 本目=A側のみ、最終本=B側のみ)
        [Xunit.Fact]
        public void T1014_PerPile_DependsOnPieceIndex()
        {
            SheetPileQuayWall.Core.FrontWall.JointType jt =
                SheetPileQuayWall.Core.FrontWall.JointType.LT75;

            double first = SheetPileQuayWall.Core.FrontWall.JointMass.PerPile_kgPerM(
                jt, SheetPileQuayWall.Core.FrontWall.PieceAssignment.Assign(1, 5));
            double middle = SheetPileQuayWall.Core.FrontWall.JointMass.PerPile_kgPerM(
                jt, SheetPileQuayWall.Core.FrontWall.PieceAssignment.Assign(3, 5));
            double last = SheetPileQuayWall.Core.FrontWall.JointMass.PerPile_kgPerM(
                jt, SheetPileQuayWall.Core.FrontWall.PieceAssignment.Assign(5, 5));
            double single = SheetPileQuayWall.Core.FrontWall.JointMass.PerPile_kgPerM(
                jt, SheetPileQuayWall.Core.FrontWall.PieceAssignment.Assign(1, 1));

            Xunit.Assert.Equal(19.9, first, 6);   // A 側のみ
            Xunit.Assert.Equal(32.6, middle, 6);  // 両側
            Xunit.Assert.Equal(12.7, last, 6);    // B 側のみ
            Xunit.Assert.Equal(0.0, single, 6);   // 単独杭は継手なし
        }
    }
}
