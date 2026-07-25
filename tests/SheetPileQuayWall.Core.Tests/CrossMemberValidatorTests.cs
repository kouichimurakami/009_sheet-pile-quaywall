// T990〜T998: CrossMemberValidator の単体テスト
// 検証基準: docs/implementation-plan.md §13.3(部材間の横断チェック 4 組)
//   PP 形の継手有効間隔 J は D 非依存の 0.2478 m なので、D=1.000 → 有効幅 B=1.2478 m。
//   期待値を厳密に書けるため本テストは PP 形を用いる。

namespace SheetPileQuayWall.Core.Tests
{
    public class CrossMemberValidatorTests
    {
        private const double FrontOuterD_m = 1.000;
        private const double EffectiveWidthPP_m = 1.2478;   // = D + 0.2478

        private static SheetPileQuayWall.Core.FrontWallRef Front()
        {
            return new SheetPileQuayWall.Core.FrontWallRef
            {
                TipPoint = new SheetPileQuayWall.Core.Point3(0.0, 5.0, -18.0),
                OuterDm = FrontOuterD_m,
                InclDeg = 0.0,
                LengthM = 25.0,
                JointType = SheetPileQuayWall.Core.FrontWall.JointType.PP
            };
        }

        // 前壁・控え杭と整合した状態のタイロッド入力
        private static SheetPileQuayWall.Core.TieRod.TieRodParameters TieRod()
        {
            SheetPileQuayWall.Core.TieRod.TieRodParameters p =
                new SheetPileQuayWall.Core.TieRod.TieRodParameters();
            p.PileDiameter = FrontOuterD_m;
            p.PilePitch = EffectiveWidthPP_m;
            p.TieElevation = 2.5;
            p.SpanLength = 10.0;
            return p;
        }

        private static SheetPileQuayWall.Core.AnchorPile.AnchorInput Anchor()
        {
            return new SheetPileQuayWall.Core.AnchorPile.AnchorInput
            {
                OuterDm = 0.800, WallTm = 0.012, LengthM = 20.0, InclDeg = 0.0,
                ClosedTip = false, SpanM = 10.0, TieElevM = 2.5, TipElevM = -14.0,
                ColorIdx = 8
            };
        }

        // T990: 鋼管矢板径が前壁の外径と一致していれば正常
        [Xunit.Fact]
        public void T990_ValidatePileDiameter_Match()
        {
            Xunit.Assert.Null(
                SheetPileQuayWall.Core.CrossMemberValidator.ValidatePileDiameter(
                    Front(), TieRod()));
        }

        // T991: 鋼管矢板径の不一致を検出する(誤差許容 1 mm を超える差)
        [Xunit.Fact]
        public void T991_ValidatePileDiameter_Mismatch()
        {
            SheetPileQuayWall.Core.TieRod.TieRodParameters p = TieRod();
            p.PileDiameter = 1.200;

            string? e = SheetPileQuayWall.Core.CrossMemberValidator.ValidatePileDiameter(
                Front(), p);
            Xunit.Assert.NotNull(e);
            Xunit.Assert.Contains("海側鋼管矢板径", e);
        }

        // T992: 矢板ピッチが前壁の有効幅 B と一致していれば正常
        [Xunit.Fact]
        public void T992_ValidatePilePitch_MatchesEffectiveWidth()
        {
            Xunit.Assert.Null(
                SheetPileQuayWall.Core.CrossMemberValidator.ValidatePilePitch(
                    Front(), TieRod()));
        }

        // T993: 有効幅 B を無視したピッチ(008 の既定値 1.200)を検出する
        [Xunit.Fact]
        public void T993_ValidatePilePitch_MismatchAgainstEffectiveWidth()
        {
            SheetPileQuayWall.Core.TieRod.TieRodParameters p = TieRod();
            p.PilePitch = 1.200;   // B=1.2478 との差 47.8 mm

            string? e = SheetPileQuayWall.Core.CrossMemberValidator.ValidatePilePitch(
                Front(), p);
            Xunit.Assert.NotNull(e);
            Xunit.Assert.Contains("有効幅", e);
        }

        // T994: タイロッド軸心標高と控え杭の Z_tr が一致していれば正常
        [Xunit.Fact]
        public void T994_ValidateTieElevation_Match()
        {
            Xunit.Assert.Null(
                SheetPileQuayWall.Core.CrossMemberValidator.ValidateTieElevation(
                    TieRod(), Anchor()));
        }

        // T995: タイロッド軸心標高と控え杭の Z_tr の不一致を検出する
        [Xunit.Fact]
        public void T995_ValidateTieElevation_Mismatch()
        {
            SheetPileQuayWall.Core.AnchorPile.AnchorInput a = Anchor();
            a.TieElevM = 3.0;

            string? e = SheetPileQuayWall.Core.CrossMemberValidator.ValidateTieElevation(
                TieRod(), a);
            Xunit.Assert.NotNull(e);
            Xunit.Assert.Contains("Z_tr", e);
        }

        // T996: span が一致していれば正常(いずれも前壁矢板中心〜陸側定着面)
        [Xunit.Fact]
        public void T996_ValidateSpan_Match()
        {
            Xunit.Assert.Null(
                SheetPileQuayWall.Core.CrossMemberValidator.ValidateSpan(
                    TieRod(), Anchor()));
        }

        // T997: span の不一致を検出する。控え杭軸まで(span − D_a/2)と取り違えた場合も
        //       0.4 m の差として検出されること
        [Xunit.Fact]
        public void T997_ValidateSpan_MismatchWhenMeasuredToAnchorAxis()
        {
            SheetPileQuayWall.Core.AnchorPile.AnchorInput a = Anchor();
            a.SpanM = 10.0 - 0.400;   // 陸側定着面ではなく控え杭軸までにした場合

            string? e = SheetPileQuayWall.Core.CrossMemberValidator.ValidateSpan(
                TieRod(), a);
            Xunit.Assert.NotNull(e);
            Xunit.Assert.Contains("陸側定着面", e);
        }

        // T998: ValidateAll は整合時に空、複数不一致は 1 件目で止めずすべて返す
        [Xunit.Fact]
        public void T998_ValidateAll_EmptyWhenConsistent_AllErrorsWhenNot()
        {
            Xunit.Assert.Empty(
                SheetPileQuayWall.Core.CrossMemberValidator.ValidateAll(
                    Front(), TieRod(), Anchor()));

            SheetPileQuayWall.Core.TieRod.TieRodParameters p = TieRod();
            p.PileDiameter = 1.200;   // 不一致 1
            p.PilePitch = 1.200;      // 不一致 2
            SheetPileQuayWall.Core.AnchorPile.AnchorInput a = Anchor();
            a.TieElevM = 3.0;         // 不一致 3
            a.SpanM = 9.0;            // 不一致 4

            System.Collections.Generic.IReadOnlyList<string> errors =
                SheetPileQuayWall.Core.CrossMemberValidator.ValidateAll(Front(), p, a);
            Xunit.Assert.Equal(4, errors.Count);
        }
    }
}
