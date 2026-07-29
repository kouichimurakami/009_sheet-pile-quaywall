// 前壁鋼管矢板の配置
// 移植元: 006@6d6d8cf src/SteelPipePile.cs (傾斜角・挿入点まわり)。
//
// docs/implementation-plan.md §2.2 の入力方式変更を反映する:
//   平面位置 (X, Y) は UCS でクリック取得し WCS へ変換した値を受け取る。クリック点の Z は使わない
//   標高は杭上端(杭頭)標高 Z_head (D.L. 基準) を数値で受け取る(2026-07-29、
//   Z_tip から変更。現場で測りやすい杭頭側に揃え、内部表現も Z_head 基準にした)
// これにより「平面位置は目視ピック、標高は正確な数値入力」という実務の作業分担に対応する。
//
// 幾何そのもの (回転・平行移動・杭頭標高・軸 X) は控え杭と共通のため PileGeometry に委ねる。
// 範囲は docs/implementation-plan.md §7.1 による。

namespace SheetPileQuayWall.Core.FrontWall
{
    public static class FrontWallPlacement
    {
        public const double Incl_Min_Deg  = 0.0;
        public const double Incl_Max_Deg  = 15.0;
        public const double TipElev_Min_m = -80.0;
        public const double TipElev_Max_m = 10.0;

        // 平面位置 (WCS 変換済み) と杭上端標高から基準点を組み立てる。
        // ピック点の Z を使わないことが §2.2 の要点であり、そのための関数である。
        public static SheetPileQuayWall.Core.Point3 HeadPoint(
            double planX_m, double planY_m, double headElevM)
        {
            return new SheetPileQuayWall.Core.Point3(planX_m, planY_m, headElevM);
        }

        // 戻り値: null = 正常、非null = エラーメッセージ (InputValidator と同じ規約)
        public static string? ValidateInclination(double inclDeg)
        {
            if (inclDeg < Incl_Min_Deg || inclDeg > Incl_Max_Deg)
                return $"傾斜角 θ={inclDeg:F1}度 は範囲外 " +
                       $"({Incl_Min_Deg:F0}〜{Incl_Max_Deg:F0}度)。";
            return null;
        }

        public static string? ValidateTipElevation(double tipElevM)
        {
            if (tipElevM < TipElev_Min_m || tipElevM > TipElev_Max_m)
                return $"杭先端標高 Z_tip={tipElevM:F3}m は範囲外 " +
                       $"({TipElev_Min_m:F0}〜{TipElev_Max_m:F0}m、D.L. 基準)。";
            return null;
        }
    }
}
