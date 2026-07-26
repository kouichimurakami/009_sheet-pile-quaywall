// AutoCAD 非依存 — xUnit で単体テスト可能
// 計算式出典: 港湾土木請負工事積算基準 令和7年度改訂版
//             第3章 4節 本体工 4.6「鋼杭式」(3-4.6-9〜17)、陸上打設のみ
//
// 控え杭は継手を持たない単独の鋼管杭であり、前壁鋼管矢板(4.6 ではなく 4.5
// 「鋼矢板式」)とは節が異なる。実データ突き合わせの結果、以下は 4.5 と
// 4.6 で数値まで完全一致するため、既存 FrontWall.DriveEstimate の該当
// メソッドをそのまま呼び出して重複させない:
//   - 貫入抵抗値 R の式(CalcR)
//   - ハンマ規格決定図(GetHammerClass)
//   - 打撃速度 Sb 表(GetSb)
//   - 溶接時間表(CalcTw / GetWeldTime)
//   - 準備時間 Tp の式(CalcTp)
//   - 基準作業能力係数 ei・E1/E2/E3 補正(CalcQ)
//
// 唯一の実質的な差異は 1 本当り打撃時間 Tb の係数 K である。4.5 は
// 「Ｋ：係数（直杭；1.0）」のみを定義し斜杭の値を与えないが、4.6 は
// 「Ｋ：係数（直杭；1.0　斜杭；1.2）」を明記する。控え杭は AnchorInput.InclDeg
// を持ち実務でも角度をつける用途があるため、この差異は無視できない。
//
// 労務編成(とび工・普通作業員)の表は 4.5・4.6 とも同一だが、既存
// FrontWall.DriveEstimate.GetLabor は陸上側を杭長によらず一律 2・1 人に
// 固定しており、実際の表(陸上 20m 未満/以上で 2→3 人・1→2 人)と食い違う
// 既存の不整合が見つかった(README §9.3 参照)。前壁側の修正は本件の対象外
// のため、控え杭側は正しい表で新規実装する。

namespace SheetPileQuayWall.Core.AnchorPile
{
    public static class AnchorDriveEstimate
    {
        /// <summary>直杭/斜杭の判定に用いる傾斜角の許容誤差 [deg]。
        /// FrontWallCommands 等が回転変換の要否判定に使う値と同じ。</summary>
        public const double InclinationTolerance_deg = 0.001;

        /// <summary>打撃時間の係数 K(直杭、3-4.6-14)。</summary>
        public const double StraightPileFactor = 1.0;

        /// <summary>打撃時間の係数 K(斜杭、3-4.6-14)。4節 3-4.5 には定義が無い。</summary>
        public const double InclinedPileFactor = 1.2;

        /// <summary>労務編成が切り替わる杭長の境界 [m](陸上打設、3-4.6-15)。</summary>
        public const double LaborBoundary_m = 20.0;

        /// <summary>
        /// 控え杭 1 本当り打撃時間 Tb [分](小数 1 位切上げ)。
        /// Tb = K × L / Sb   (出典: 3-4.6-14)
        /// Sb は FrontWall.DriveEstimate.GetSb をそのまま用いる(打撃速度表は
        /// 4.5・4.6 で数値が完全一致するため)。
        /// </summary>
        /// <param name="L_pen_m">根入れ長 [m](ヤットコを含む。表層から連続する N≦5 の区間は除く)。</param>
        /// <param name="Sb">打撃速度 [m/分]。</param>
        /// <param name="inclDeg">傾斜角 θ [deg]。|θ| が許容誤差を超えると斜杭とみなす。</param>
        public static double CalcTb(double L_pen_m, double Sb, double inclDeg)
        {
            double K = System.Math.Abs(inclDeg) > InclinationTolerance_deg
                ? InclinedPileFactor : StraightPileFactor;
            return System.Math.Ceiling(K * L_pen_m / Sb * 10.0) / 10.0;
        }

        /// <summary>
        /// 労務編成 [人/日](陸上打設、3-4.6-15)。杭長 20m 未満/以上でとび工・
        /// 普通作業員が変わる。溶接工は継杭がある場合のみ計上し、
        /// φ800mm 以上は 2 人。
        /// </summary>
        /// <param name="L_m">杭長 [m](根入れ長ではなく杭の全長)。</param>
        /// <param name="splicing">継杭の有無。</param>
        /// <param name="D_mm">外径 [mm]。</param>
        public static (int foreman, int rigger, int laborer, int welder) GetLabor(
            double L_m, bool splicing, int D_mm)
        {
            bool longPile = L_m >= LaborBoundary_m;

            int rigger = longPile ? 3 : 2;
            int laborer = longPile ? 2 : 1;
            int welder = !splicing ? 0 : (D_mm >= 800 ? 2 : 1);

            return (foreman: 1, rigger: rigger, laborer: laborer, welder: welder);
        }
    }
}
