// 控え杭の整列計算結果
// 移植元: 006@6d6d8cf src/AnchorPile.cs ComputeTipPoint / PrintAnchorSummary の派生量。
// 単位はすべて m。

namespace SheetPileQuayWall.Core.AnchorPile
{
    public sealed class AnchorResult
    {
        public AnchorResult(
            SheetPileQuayWall.Core.Point3 tipPoint,
            double frontAxisXAtTie_m, double anchorAxisXAtTie_m,
            double axisSpacing_m, double faceClearance_m, double headElev_m)
        {
            TipPoint = tipPoint;
            FrontAxisXAtTie_m = frontAxisXAtTie_m;
            AnchorAxisXAtTie_m = anchorAxisXAtTie_m;
            AxisSpacing_m = axisSpacing_m;
            FaceClearance_m = faceClearance_m;
            HeadElev_m = headElev_m;
        }

        // 控え杭の杭先端 (挿入点)。Plugin 層はこの点にソリッドを配置する
        public SheetPileQuayWall.Core.Point3 TipPoint { get; }

        // Z_tr における前壁軸の X 座標 [確定]
        public double FrontAxisXAtTie_m { get; }

        // Z_tr における控え杭軸の X 座標 (= 前壁軸 X + span − D_a/2) [確定]
        public double AnchorAxisXAtTie_m { get; }

        // 軸間水平距離 (= span − D_a/2) [確定]
        public double AxisSpacing_m { get; }

        // 杭面間浄距離 (= 軸間水平距離 − D_f/2 − D_a/2)。負値は干渉を意味する [確定]
        public double FaceClearance_m { get; }

        // 控え杭の杭頭標高 [確定]
        public double HeadElev_m { get; }
    }
}
