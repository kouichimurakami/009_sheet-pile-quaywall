// AutoCAD 非依存 — xUnit で単体テスト可能
//
// 前壁鋼管矢板の壁一括レイアウト。
// 「施設全長」と「継手を考慮した有効幅 B」から必要本数を求め、始点から +Y 方向へ
// 等間隔に並べる (SPQW_FRONTWALL_Create)。
//
// 本数の端数処理は切り上げ。施設全長を必ずカバーする代わりに終点は行き過ぎる
// (10.000 m ÷ 0.8752 m = 11.426 → 12 本 = 10.502 m、+0.502 m 超過)。
// 切り捨て・四捨五入は採らない (決定日 2026-07-29)。
//
// 有効幅は利用者の入力値をそのまま使う。外径・継手形式から算出される
// JointParameters.EffectiveWidth と食い違う場合は呼び出し側が警告を出すが、
// 自動補正はしない (§9 の「不一致時は自動補正しない」に従い、本モジュールは
// 判定材料 (WidthDeviation) を返すだけに留める)。

namespace SheetPileQuayWall.Core.FrontWall
{
    public static class WallLayout
    {
        // 誤差許容 1 mm (CLAUDE.PRIVATE.md §6-5)
        public const double Tol_m = 0.001;

        public const double WallLength_Min_m = 0.1;
        public const double WallLength_Max_m = 1000.0;
        public const double Width_Min_m = 0.5;
        public const double Width_Max_m = 2.5;

        /// <summary>
        /// 施設全長を満たすのに必要な本数 (切り上げ)。
        /// ちょうど整数倍のときに浮動小数の丸め誤差で 1 本増えないよう、
        /// 施設全長から誤差許容 1 mm を差し引いてから割る。
        /// 呼び出し前に Validate を通すこと。
        /// </summary>
        public static int PieceCountFor(double wallLength_m, double effectiveWidth_m)
        {
            int count = (int)System.Math.Ceiling((wallLength_m - Tol_m) / effectiveWidth_m);
            return count < 1 ? 1 : count;
        }

        /// <summary>本数 × 有効幅。施設全長以上になる (切り上げのため)。</summary>
        public static double ActualLength(int pieceCount, double effectiveWidth_m)
        {
            return pieceCount * effectiveWidth_m;
        }

        /// <summary>
        /// 施工順位 (1 始まり) の矢板中心の Y 座標。
        /// 1 本目が始点、以降 +Y 方向へ有効幅ずつ進む (README §1 の平面図)。
        /// </summary>
        public static double PositionY(double startY_m, int pieceIndex, double effectiveWidth_m)
        {
            return startY_m + (pieceIndex - 1) * effectiveWidth_m;
        }

        /// <summary>
        /// 入力幅と、外径・継手形式から算出される有効幅との差 [m]。
        /// 呼び出し側はこの値が Tol_m を超えたら警告を出す (エラー停止はしない)。
        /// </summary>
        public static double WidthDeviation(
            double effectiveWidth_m, double outerD_m, JointType jointType)
        {
            return System.Math.Abs(
                effectiveWidth_m - JointParameters.EffectiveWidth(outerD_m, jointType));
        }

        /// <summary>
        /// 戻り値: null = 正常、非null = エラーメッセージ (InputValidator と同じ規約)。
        /// 本数が PieceAssignment の上限を超える組合せもここで捕捉する。
        /// </summary>
        public static string? Validate(double wallLength_m, double effectiveWidth_m)
        {
            if (wallLength_m < WallLength_Min_m || wallLength_m > WallLength_Max_m)
                return $"施設全長 {wallLength_m:F3}m は範囲外 " +
                       $"({WallLength_Min_m:F1}〜{WallLength_Max_m:F0}m)。";

            if (effectiveWidth_m < Width_Min_m || effectiveWidth_m > Width_Max_m)
                return $"有効幅 B {effectiveWidth_m:F4}m は範囲外 " +
                       $"({Width_Min_m:F1}〜{Width_Max_m:F1}m)。";

            int count = PieceCountFor(wallLength_m, effectiveWidth_m);
            if (count > PieceAssignment.PieceCount_Max)
                return $"施設全長 {wallLength_m:F3}m ÷ 有効幅 {effectiveWidth_m:F4}m = {count} 本 は " +
                       $"上限 {PieceAssignment.PieceCount_Max} 本を超えます。" +
                       "施設全長を分割して生成してください。";

            return null;
        }
    }
}
