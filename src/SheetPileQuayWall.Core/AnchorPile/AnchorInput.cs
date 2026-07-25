// 控え杭の入力パラメータ
// 移植元: 006@6d6d8cf src/AnchorPile.cs AnchorInput。
// 整列の基準となる前壁の参照情報は Core ルートの FrontWallRef を用いる
// (フェーズ 3 でタイロッド・部材間整合も使うようになったため移動した)。
//
// 長さはすべて m、角度は deg (CLAUDE.PRIVATE.md §2.1)。移植元は D と t を
// mm 呼称で保持していたが、009 では Core に mm を持ち込まない (決定 7)。

namespace SheetPileQuayWall.Core.AnchorPile
{
    public sealed class AnchorInput
    {
        public double OuterDm;    // 外径 D [m]
        public double WallTm;     // 肉厚 t [m]
        public double LengthM;    // 全長 L [m]
        public double InclDeg;    // 傾斜角 θ [deg] (Y 軸周り、0=直杭)
        public bool   ClosedTip;  // 先端形状 (true=閉端)
        public double SpanM;      // 法線直角方向延長 span [m]
                                  //   前壁矢板中心 〜 控え杭の陸側定着面の水平距離
                                  //   (積算基準 3-4.5-(13) に合わせた定義)
        public double TieElevM;   // タイロッド軸心標高 Z_tr [m] (D.L. 基準)
        public double TipElevM;   // 杭先端標高 Z_tip [m] (D.L. 基準)
        public int    ColorIdx;   // 本管の色 (ACI 1〜255)
    }
}
