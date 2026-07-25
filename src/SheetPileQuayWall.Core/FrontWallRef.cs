// 整列・整合の基準となる前壁鋼管矢板の参照情報
// Plugin 層が前壁の XData から復元して各部材へ渡す。
//
// フェーズ 2 では控え杭専用として AnchorPile 名前空間に置いていたが、フェーズ 3 で
// タイロッドの配置 (決定8) と部材間整合チェックも同じ情報を必要とするようになったため、
// Core ルートへ移した。JointType はピッチ ⟺ 有効幅 B の照合 (CrossMemberValidator) 用に
// フェーズ 3 で追加した。
//
// 単位: 長さ m、角度 deg。

namespace SheetPileQuayWall.Core
{
    public sealed class FrontWallRef
    {
        public Point3 TipPoint;                        // 杭先端 (挿入点) [m]
        public double OuterDm;                         // 外径 D_f [m]
        public double InclDeg;                         // 傾斜角 θ_f [deg]
        public double LengthM;                         // 全長 L_f [m]
        public FrontWall.JointType JointType;          // 継手形式 (既定 LT65)
    }
}
