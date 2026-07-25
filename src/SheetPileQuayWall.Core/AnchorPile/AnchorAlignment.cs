// 控え杭の整列計算と整合性チェック
// 移植元: 006@6d6d8cf src/AnchorPile.cs ComputeTipPoint / ValidateAlignment。
//
// 整列の定義 (移植元のコメントより):
//   平面視: 控え杭中心の Y = 基準とした前壁矢板中心の Y (タイロッドは矢板中心を横断する)
//   立面視: タイロッド軸心標高 Z_tr で控え杭軸とタイロッド軸が交差する
//   X 位置: span は「前壁矢板中心 〜 控え杭の陸側定着面」の水平距離 (積算基準 3-4.5-(13))
//           よって 控え杭軸 X = 前壁軸 X(Z_tr) + span − D_a/2
//
// 009 での変更点: 前壁・控え杭とも Z=0 が D.L. に統一されたため、標高の突き合わせが
// 常に同一基準で成立する (docs/implementation-plan.md §2.4)。移植元は AutoCAD の
// Editor へ直接エラーを書いていたが、Core では文字列を返して呼び出し側に委ねる。

namespace SheetPileQuayWall.Core.AnchorPile
{
    public static class AnchorAlignment
    {
        // 誤差許容 1 mm (CLAUDE.PRIVATE.md §6)
        public const double Tol_m = 0.001;

        // 戻り値: null = 正常、非null = エラーメッセージ。
        // 不一致時はエラー停止し、自動補正も再生成もしない (§9)。
        public static string? Validate(FrontWallRef front, AnchorInput a)
        {
            string? e = AnchorPileSteel.ValidateD(a.OuterDm);
            if (e != null) return e;

            e = AnchorPileSteel.ValidateT(a.WallTm, a.OuterDm);
            if (e != null) return e;

            e = AnchorPileSteel.ValidateL(a.LengthM);
            if (e != null) return e;

            e = SheetPileQuayWall.Core.FrontWall.FrontWallPlacement.ValidateInclination(a.InclDeg);
            if (e != null) return e;

            e = SheetPileQuayWall.Core.FrontWall.FrontWallPlacement.ValidateTipElevation(a.TipElevM);
            if (e != null) return e;

            // Z_tr が前壁の杭体範囲内にあること (D.L. 基準で突き合わせる)
            double frontHeadZ = SheetPileQuayWall.Core.PileGeometry.HeadElevation(
                front.TipPoint.Z, front.LengthM, front.InclDeg);
            if (a.TieElevM < front.TipPoint.Z - Tol_m || a.TieElevM > frontHeadZ + Tol_m)
                return $"タイロッド軸心標高 Z_tr={a.TieElevM:F3}m が前壁の杭体範囲 " +
                       $"({front.TipPoint.Z:F3}〜{frontHeadZ:F3}m) 外です。";

            // Z_tr が控え杭の杭体範囲内にあること
            double headZ = SheetPileQuayWall.Core.PileGeometry.HeadElevation(
                a.TipElevM, a.LengthM, a.InclDeg);
            if (a.TieElevM < a.TipElevM - Tol_m || a.TieElevM > headZ + Tol_m)
                return $"タイロッド軸心標高 Z_tr={a.TieElevM:F3}m が控え杭の杭体範囲 " +
                       $"({a.TipElevM:F3}〜{headZ:F3}m) 外です。";

            // 干渉チェック: 杭面間浄距離 ≥ 0 ⇔ span ≥ D_f/2 + D_a
            double minSpan_m = MinSpan(front.OuterDm, a.OuterDm);
            if (a.SpanM < minSpan_m - Tol_m)
                return $"span={a.SpanM:F3}m では前壁と控え杭が干渉します " +
                       $"(必要 span ≥ {minSpan_m:F3}m)。";

            return null;
        }

        // 干渉しない最小の span [m]
        public static double MinSpan(double frontOuterDm, double anchorOuterDm)
        {
            return frontOuterDm / 2.0 + anchorOuterDm;
        }

        // 呼び出し前に Validate を通すこと。
        public static AnchorResult Compute(FrontWallRef front, AnchorInput a)
        {
            // Z_tr における前壁軸の X (前壁が傾斜していれば標高差の分だけずれる)
            double frontAxisX = SheetPileQuayWall.Core.PileGeometry.AxisXAt(
                front.TipPoint, front.InclDeg, a.TieElevM);

            // Z_tr における控え杭軸の X (span は陸側定着面までの距離)
            double anchorAxisX = frontAxisX + a.SpanM - a.OuterDm / 2.0;

            // 控え杭が傾斜していれば、Z_tr の軸位置から杭先端へ戻す
            double tipX = anchorAxisX
                - (a.TieElevM - a.TipElevM)
                  * System.Math.Tan(a.InclDeg * System.Math.PI / 180.0);

            double axisSpacing = anchorAxisX - frontAxisX;
            double faceClearance = axisSpacing - front.OuterDm / 2.0 - a.OuterDm / 2.0;
            double headElev = SheetPileQuayWall.Core.PileGeometry.HeadElevation(
                a.TipElevM, a.LengthM, a.InclDeg);

            return new AnchorResult(
                new SheetPileQuayWall.Core.Point3(tipX, front.TipPoint.Y, a.TipElevM),
                frontAxisX, anchorAxisX, axisSpacing, faceClearance, headElev);
        }
    }
}
