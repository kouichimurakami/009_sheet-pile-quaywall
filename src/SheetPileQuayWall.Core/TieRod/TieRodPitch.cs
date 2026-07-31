// AutoCAD 非依存 — xUnit で単体テスト可能
//
// タイロッド取付間隔・控え杭配置間隔を「鋼管矢板 何本ごと」から求める。
//
// 【配置規則】(2026-08-01 決定)
// タイロッド・控え杭は壁の 1 本目と最終 P 本目の矢板に必ず設置し、その間を
// everyNPiles 本ごとに割り付ける。両端固定と等間隔は (P−1) が everyNPiles の
// 倍数のときしか同時に成立しないため、割り切れない場合は端数 r = (P−1) mod n を
// 最終スパンで吸収する (最終スパンのみ r 本ぶんと短くなる)。
// 端数スパンも矢板ピッチの整数倍であるため、「タイ材は矢板中心を横断する」条件
// (TieRodParameters.Validate の 7 番) は全スパンで保たれる。
// 等間隔にできる n の候補は UniformCandidates が返す (呼び出し側の警告表示用)。
//
// TieRodParameters.Validate の 7 番は「取付間隔が矢板ピッチの整数倍であること」を
// 要求する (タイロッドは海側鋼管矢板の中央を横断するため)。従来はこの整数倍を
// 利用者が電卓で計算して m 単位で入力していたが (既定値 2.400 m は 0.8752 m の
// 整数倍ではなく、Enter で通すと必ず検証エラーになっていた)、本モジュールにより
// 「何本ごと」の整数入力から間隔を導出する。整数倍制約は構造的に満たされる。
//
// 【重要】TieRodParameters.cs / TieRodCalculator.cs は scripts/port-from-legacy.sh が
// 008@ff3a986 と完全同期させる対象であり、直接メソッドを追加すると再実行時に消える。
// そのため本ファイルを新設した (FrontWall.DriveEquipment / AnchorPile.AnchorDriveEstimate
// と同じ回避パターン)。

namespace SheetPileQuayWall.Core.TieRod
{
    public static class TieRodPitch
    {
        // 誤差許容 1 mm (CLAUDE.PRIVATE.md §6-5)
        public const double Tol_m = 0.001;

        public const int EveryNPiles_Min = 1;
        public const int EveryNPiles_Max = 50;

        // TieRodParameters.Validate が取付間隔に課す範囲と一致させること。
        // 同ファイルは移植同期の対象で定数を公開できないため、ここに複写している
        // (乖離は TieRodPitchTests の連結テストで検出する)。
        public const double Spacing_Min_m = 0.600;
        public const double Spacing_Max_m = 20.000;

        /// <summary>矢板ピッチ (= 前壁の有効幅 B) の n 本ぶんを取付間隔 [m] とする。</summary>
        public static double SpacingFor(double pilePitch_m, int everyNPiles)
        {
            return pilePitch_m * everyNPiles;
        }

        /// <summary>
        /// 配置する矢板の施工順位 (1 始まり) を昇順で返す。
        /// 1 本目から everyNPiles 本ごとに並べ、末尾が最終矢板 pieceCount でなければ
        /// 最終矢板を追加する (両端に必ず設置する。最終スパンのみ短くなる)。
        /// </summary>
        public static int[] PileIndices(int pieceCount, int everyNPiles)
        {
            if (pieceCount < 1) pieceCount = 1;
            if (everyNPiles < 1) everyNPiles = 1;

            System.Collections.Generic.List<int> indices =
                new System.Collections.Generic.List<int>();
            for (int index = 1; index <= pieceCount; index += everyNPiles)
            {
                indices.Add(index);
            }
            if (indices[indices.Count - 1] != pieceCount)
            {
                indices.Add(pieceCount);
            }
            return indices.ToArray();
        }

        /// <summary>
        /// 壁の 1 本目を原点とした各組の Y オフセット [m]。
        /// 末尾は (pieceCount − 1) × pilePitch となり、最終矢板の中心 Y
        /// (WallLayout.PositionY) と一致する。
        /// </summary>
        public static double[] OffsetsY(int pieceCount, int everyNPiles, double pilePitch_m)
        {
            int[] indices = PileIndices(pieceCount, everyNPiles);
            double[] offsets = new double[indices.Length];
            for (int i = 0; i < indices.Length; i++)
            {
                offsets[i] = (indices[i] - 1) * pilePitch_m;
            }
            return offsets;
        }

        /// <summary>
        /// 矢板本数と配置間隔(何本ごと)から、必要なタイロッド組数を求める(2026-07-29)。
        /// 両端固定の配置規則 (2026-08-01) により PileIndices の要素数に等しい。
        /// </summary>
        public static int CountFor(int pieceCount, int everyNPiles)
        {
            return PileIndices(pieceCount, everyNPiles).Length;
        }

        /// <summary>
        /// 最終スパンの端数 [本]。0 なら全スパンが everyNPiles 本の等間隔になる。
        /// </summary>
        public static int RemainderPiles(int pieceCount, int everyNPiles)
        {
            if (pieceCount < 1) pieceCount = 1;
            if (everyNPiles < 1) everyNPiles = 1;
            return (pieceCount - 1) % everyNPiles;
        }

        /// <summary>
        /// 端数を出さずに等間隔で割り付けられる everyNPiles の候補 (昇順)。
        /// (pieceCount − 1) の約数のうち、Validate を通るものだけを返す。
        /// </summary>
        public static int[] UniformCandidates(int pieceCount, double pilePitch_m)
        {
            System.Collections.Generic.List<int> candidates =
                new System.Collections.Generic.List<int>();
            for (int n = EveryNPiles_Min; n <= EveryNPiles_Max; n++)
            {
                if (RemainderPiles(pieceCount, n) == 0 && Validate(pilePitch_m, n) == null)
                {
                    candidates.Add(n);
                }
            }
            return candidates.ToArray();
        }

        /// <summary>
        /// 取付間隔が矢板ピッチの何本ぶんかを逆算する (CSV 取り込み値・既存 XData の表示用)。
        /// 整数倍でない値に対しては最も近い整数を返すため、非整数倍かどうかは
        /// SpacingDeviation で別途判定すること。
        /// </summary>
        public static int PilesPerSpacing(double tieSpacing_m, double pilePitch_m)
        {
            if (pilePitch_m <= Tol_m)
            {
                return 0;
            }
            return (int)System.Math.Round(
                tieSpacing_m / pilePitch_m, System.MidpointRounding.AwayFromZero);
        }

        /// <summary>
        /// 取付間隔と「最も近い整数倍」との差 [m]。Tol_m 以下なら整数倍とみなせる。
        /// TieRodParameters.Validate の 7 番と同じ判定を、事前確認用に切り出したもの。
        /// </summary>
        public static double SpacingDeviation(double tieSpacing_m, double pilePitch_m)
        {
            if (pilePitch_m <= Tol_m)
            {
                return 0.0;
            }
            double ratio = tieSpacing_m / pilePitch_m;
            return System.Math.Abs(ratio - System.Math.Round(ratio)) * pilePitch_m;
        }

        /// <summary>
        /// 戻り値: null = 正常、非null = エラーメッセージ (InputValidator と同じ規約)。
        /// n 本ごとの指定が、取付間隔の許容範囲に収まるかを検査する。
        /// </summary>
        public static string? Validate(double pilePitch_m, int everyNPiles)
        {
            if (everyNPiles < EveryNPiles_Min || everyNPiles > EveryNPiles_Max)
                return $"取付間隔 {everyNPiles} 本ごと は範囲外 " +
                       $"({EveryNPiles_Min}〜{EveryNPiles_Max} 本ごと)。";

            double spacing_m = SpacingFor(pilePitch_m, everyNPiles);
            if (spacing_m < Spacing_Min_m - Tol_m || spacing_m > Spacing_Max_m + Tol_m)
                return $"矢板ピッチ {pilePitch_m:F4}m × {everyNPiles} 本 = {spacing_m:F4}m は " +
                       $"取付間隔の範囲 ({Spacing_Min_m:F3}〜{Spacing_Max_m:F3}m) を外れています。";

            return null;
        }
    }
}
