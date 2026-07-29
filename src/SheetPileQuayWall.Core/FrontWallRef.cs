// 整列・整合の基準となる前壁鋼管矢板の参照情報
// Plugin 層が前壁の XData から復元して各部材へ渡す。
//
// フェーズ 2 では控え杭専用として AnchorPile 名前空間に置いていたが、フェーズ 3 で
// タイロッドの配置 (決定8) と部材間整合チェックも同じ情報を必要とするようになったため、
// Core ルートへ移した。JointType はピッチ ⟺ 有効幅 B の照合 (CrossMemberValidator) 用に
// フェーズ 3 で追加した。
//
// 単位: 長さ m、角度 deg。
//
// 内部表現は杭上端(杭頭)標高 Z_head 基準(2026-07-29、決定: 鋼管矢板モデルの
// 内部構造を Z_head 基準へ変更)。TipPoint(杭先端)は HeadPoint・LengthM・InclDeg
// から都度算出する計算プロパティであり、ソリッド生成など杭先端を挿入点として
// 必要とする箇所のためだけに残している。

namespace SheetPileQuayWall.Core
{
    public sealed class FrontWallRef
    {
        public Point3 HeadPoint;                       // 杭上端(杭頭) [m]
        public double OuterDm;                         // 外径 D_f [m]
        public double InclDeg;                         // 傾斜角 θ_f [deg]
        public double LengthM;                         // 全長 L_f [m]
        public FrontWall.JointType JointType;          // 継手形式 (既定 LT65)

        // 杭先端(挿入点)。HeadPoint からの計算値(PileGeometry.TipFromHead の逆演算)。
        public Point3 TipPoint => PileGeometry.TipFromHead(HeadPoint, LengthM, InclDeg);

        // 壁一括生成(WallLayout)で実際に配置に使われた有効幅 B [m]。
        // 0 以下(未設定)の場合は ResolveEffectiveWidth が外径・継手形式からの
        // 算出値にフォールバックする。SPQW_FRONTWALL_Create の有効幅 B は入力値を
        // 優先し外径・継手形式からの算出値と食い違い得るため(README §5.1)、
        // タイロッド・控え杭・施設積算はこのフィールド経由で「実際に使われた値」を
        // 参照すること。算出値を都度呼び直すと、入力値と食い違うケースで
        // CrossMemberValidator が実際の矢板間隔とのズレを検出できなくなる
        // (2026-07-29 発見)。
        public double EffectiveWidthM;

        public double ResolveEffectiveWidth()
        {
            return EffectiveWidthM > 0.0
                ? EffectiveWidthM
                : FrontWall.JointParameters.EffectiveWidth(OuterDm, JointType);
        }
    }
}
