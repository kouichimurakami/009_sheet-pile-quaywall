// T1298〜T1300: FrontWallRef.ResolveEffectiveWidth の単体テスト
// 検証基準: 2026-07-29 発見の不整合 — 壁一括生成(SPQW_FRONTWALL_Create)は有効幅 B の
//   カスタム入力を許すが(README §5.1)、実際に使われた値は従来 XData に保存されず、
//   タイロッド・控え杭・施設積算は外径・継手形式から常に再計算していたため、
//   カスタム B を使った場合に実際の矢板間隔と食い違っても検出できなかった。

namespace SheetPileQuayWall.Core.Tests
{
    public class FrontWallRefTests
    {
        private static SheetPileQuayWall.Core.FrontWallRef Front(double effectiveWidthM = 0.0)
        {
            return new SheetPileQuayWall.Core.FrontWallRef
            {
                HeadPoint = new SheetPileQuayWall.Core.Point3(0.0, 0.0, 3.0),  // Z_tip=-18.0 相当 (L=21.0, θ=0)
                OuterDm = 0.800,
                InclDeg = 0.0,
                LengthM = 21.0,
                JointType = SheetPileQuayWall.Core.FrontWall.JointType.LT75,
                EffectiveWidthM = effectiveWidthM
            };
        }

        // T1298: EffectiveWidthM 未設定(既定 0.0)の場合、外径・継手形式からの
        //        算出値にフォールバックする(旧図面・既存コードとの後方互換)
        [Xunit.Fact]
        public void T1298_ResolveEffectiveWidth_FallsBackWhenUnset()
        {
            SheetPileQuayWall.Core.FrontWallRef front = Front();
            double auto = SheetPileQuayWall.Core.FrontWall.JointParameters.EffectiveWidth(
                front.OuterDm, front.JointType);

            Xunit.Assert.Equal(auto, front.ResolveEffectiveWidth(), 9);
        }

        // T1299: EffectiveWidthM が設定されていれば、算出値と異なっていてもその値を使う
        [Xunit.Fact]
        public void T1299_ResolveEffectiveWidth_UsesActualValueWhenSet()
        {
            SheetPileQuayWall.Core.FrontWallRef front = Front(0.900);
            double auto = SheetPileQuayWall.Core.FrontWall.JointParameters.EffectiveWidth(
                front.OuterDm, front.JointType);

            Xunit.Assert.Equal(0.900, front.ResolveEffectiveWidth(), 9);
            Xunit.Assert.NotEqual(auto, front.ResolveEffectiveWidth());
        }

        // T1300: EffectiveWidthM がちょうど算出値と同じ場合も正しく動く(境界確認)
        [Xunit.Fact]
        public void T1300_ResolveEffectiveWidth_MatchesAutoValueExactly()
        {
            double auto = SheetPileQuayWall.Core.FrontWall.JointParameters.EffectiveWidth(0.800,
                SheetPileQuayWall.Core.FrontWall.JointType.LT75);
            SheetPileQuayWall.Core.FrontWallRef front = Front(auto);

            Xunit.Assert.Equal(auto, front.ResolveEffectiveWidth(), 9);
        }
    }
}
