// 部材間の整合チェック(前壁 ⟺ タイロッド ⟺ 控え杭)
//
// §9 の整合性チェックは 006/007/008 それぞれの単体チェックの寄せ集めであり、
// 「同じ量を 2 つの部材が別々に入力している」箇所の突き合わせが無かった。
// 統合版である 009 でこそ必要なチェックとして、フェーズ 3 で新設した。
//
// 突き合わせる 4 組:
//   1. タイロッドの海側鋼管矢板径 PileDiameter ⟺ 前壁の外径 D_f
//   2. タイロッドの矢板ピッチ PilePitch     ⟺ 前壁の有効幅 B (継手形式から算出)
//   3. タイロッドの軸心標高 TieElevation    ⟺ 控え杭のタイロッド軸心標高 Z_tr
//   4. タイロッドの法線直角方向延長 SpanLength ⟺ 控え杭の span
//
// 4 の同一性について: 008 README の断面図と全長算定式 (LandEndX = SpanLength +
// 金物厚) より、SpanLength は「前壁矢板中心 〜 陸側定着面」であり、定着金物は
// その面より陸側へ張り出す。006 の控え杭 span も同一定義 (陸側定着面まで、
// 控え杭軸 X = 前壁軸 X + span − D_a/2) であり、両者は等しくなければならない。
// なお移植元 008 の TieRodParameters.SpanLength の XML コメントは「控工中心まで」と
// 書いているが、README の図・算定式・006 の定義のいずれとも一致しない
// (控え杭軸までなら D_a/2 だけ短くなる)。コメントの誤りとして扱う。
//
// 誤差許容は 1 mm = 0.001 m (CLAUDE.PRIVATE.md §6)。不一致は自動補正せずエラーとする。

namespace SheetPileQuayWall.Core
{
    public static class CrossMemberValidator
    {
        public const double Tol_m = 0.001;

        // 1. 前壁 ⟺ タイロッド: 鋼管矢板径
        public static string? ValidatePileDiameter(
            FrontWallRef front, TieRod.TieRodParameters tieRod)
        {
            if (System.Math.Abs(tieRod.PileDiameter - front.OuterDm) > Tol_m)
                return $"タイロッドの海側鋼管矢板径 {tieRod.PileDiameter:F3}m が " +
                       $"前壁の外径 {front.OuterDm:F3}m と一致しません。";
            return null;
        }

        // 2. 前壁 ⟺ タイロッド: 矢板ピッチ (前壁の継手形式から定まる有効幅 B と一致すること)
        public static string? ValidatePilePitch(
            FrontWallRef front, TieRod.TieRodParameters tieRod)
        {
            double b_m = FrontWall.JointParameters.EffectiveWidth(
                front.OuterDm, front.JointType);
            if (System.Math.Abs(tieRod.PilePitch - b_m) > Tol_m)
                return $"タイロッドの矢板ピッチ {tieRod.PilePitch:F3}m が " +
                       $"前壁の有効幅 B={b_m:F3}m " +
                       $"(D={front.OuterDm:F3}m, 継手={FrontWall.JointParameters.ToCode(front.JointType)}) " +
                       $"と一致しません。";
            return null;
        }

        // 3. タイロッド ⟺ 控え杭: 軸心標高
        public static string? ValidateTieElevation(
            TieRod.TieRodParameters tieRod, AnchorPile.AnchorInput anchor)
        {
            if (System.Math.Abs(tieRod.TieElevation - anchor.TieElevM) > Tol_m)
                return $"タイロッドの軸心標高 {tieRod.TieElevation:F3}m が " +
                       $"控え杭のタイロッド軸心標高 Z_tr={anchor.TieElevM:F3}m と一致しません。";
            return null;
        }

        // 4. タイロッド ⟺ 控え杭: 法線直角方向延長
        public static string? ValidateSpan(
            TieRod.TieRodParameters tieRod, AnchorPile.AnchorInput anchor)
        {
            if (System.Math.Abs(tieRod.SpanLength - anchor.SpanM) > Tol_m)
                return $"タイロッドの法線直角方向延長 {tieRod.SpanLength:F3}m が " +
                       $"控え杭の span={anchor.SpanM:F3}m と一致しません " +
                       $"(いずれも前壁矢板中心〜陸側定着面の水平距離)。";
            return null;
        }

        // 全チェックをまとめて実行する。1 件目で止めず、不一致を全て返す
        // (移植元 008 TieRodParameters.Validate と同じ規約)。
        public static System.Collections.Generic.IReadOnlyList<string> ValidateAll(
            FrontWallRef front, TieRod.TieRodParameters tieRod, AnchorPile.AnchorInput anchor)
        {
            System.Collections.Generic.List<string> errors =
                new System.Collections.Generic.List<string>();

            string? e = ValidatePileDiameter(front, tieRod);
            if (e != null) errors.Add(e);

            e = ValidatePilePitch(front, tieRod);
            if (e != null) errors.Add(e);

            e = ValidateTieElevation(tieRod, anchor);
            if (e != null) errors.Add(e);

            e = ValidateSpan(tieRod, anchor);
            if (e != null) errors.Add(e);

            return errors;
        }
    }
}
