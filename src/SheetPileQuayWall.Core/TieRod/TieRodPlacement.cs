// タイロッドの配置基準点(決定8 の Core 実装)
//
// 移植元 008 は「海側鋼管矢板の中心」を画面上でクリックし、その (X, Y) を
// ローカル原点 X=0 として TieRodResult.SeaEndX / LandEndX を水平にオフセット
// していた。この方式は X=0 が高さによらず一定であること、すなわち前壁が鉛直で
// あることを暗黙に仮定している。
//
// 009 の前壁は傾斜角 θ(0〜15°)を持つため、タイロッド軸心標高における前壁軸の
// X は杭先端の X とは異なる(標高差 20.5m・θ=15° でずれ量 約5.5m)。傾斜した
// ソリッドの断面を目視で正確にクリックすることはできないため、平面 X の目視ピックを
// 廃止し、前壁 XData から復元した FrontWallRef を用いて自動計算する
// (docs/implementation-plan.md §1 決定8、§7.2)。
//
// θ=0 のときは AxisXAt が杭先端の X をそのまま返すため、移植元 008 と同じ結果になる。

namespace SheetPileQuayWall.Core.TieRod
{
    public static class TieRodPlacement
    {
        // 海側取付点の X [m]。タイロッド軸心標高における前壁軸の X 座標であり、
        // TieRodResult.SeaEndX / LandEndX はここを原点 (X=0) とするオフセットである。
        public static double SeaAttachmentX(FrontWallRef front, double tieElevationM)
        {
            return PileGeometry.AxisXAt(front.TipPoint, front.InclDeg, tieElevationM);
        }

        // 海側取付点。Y は施設延長方向の配置位置、Z はタイロッド軸心標高 (D.L. 基準)。
        public static Point3 SeaAttachmentPoint(
            FrontWallRef front, double tieElevationM, double positionY_m)
        {
            return new Point3(
                SeaAttachmentX(front, tieElevationM), positionY_m, tieElevationM);
        }
    }
}
