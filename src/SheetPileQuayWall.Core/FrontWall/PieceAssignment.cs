// 施工順位 → 継手の要否
// 移植元: 006@6d6d8cf src/SteelPipePile.cs ComputeDerived の継手判定部分。
//
// 打設は 1 本目から +Y 方向へ進む。したがって
//   −Y 側 (leading)  : 先行して打設済みの矢板と嵌合する。1 本目には無い
//   +Y 側 (trailing) : 後続の矢板を受ける。最終本には無い
//
// 移植元は L-T 形 (75 型) 固定で、−Y 側を雄 (T 形鋼)、+Y 側を雌 (山形鋼×2) と
// 呼んでいた。009 は 5 種の継手形式を扱い、P-T 形は片側が鋼管・片側が T 形鋼、
// P-P 形は両側とも鋼管であって L-T 形とは雌雄の対応が異なる。そのため本クラスは
// 「どちら側に継手が付くか」のみを返し、雌雄の呼称と部材質量は持たない
// (部材は JointCatalog の担当)。

namespace SheetPileQuayWall.Core.FrontWall
{
    public sealed class PieceJoints
    {
        public PieceJoints(bool hasLeadingJoint, bool hasTrailingJoint)
        {
            HasLeadingJoint = hasLeadingJoint;
            HasTrailingJoint = hasTrailingJoint;
        }

        public bool HasLeadingJoint { get; }   // −Y 側
        public bool HasTrailingJoint { get; }  // +Y 側
    }

    public static class PieceAssignment
    {
        public const int PieceCount_Min = 1;
        public const int PieceCount_Max = 500;

        // 戻り値: null = 正常、非null = エラーメッセージ (InputValidator と同じ規約)
        public static string? Validate(int pieceIndex, int pieceCount)
        {
            if (pieceCount < PieceCount_Min || pieceCount > PieceCount_Max)
                return $"総本数 {pieceCount} 本は範囲外 ({PieceCount_Min}〜{PieceCount_Max} 本)。";
            if (pieceIndex < 1 || pieceIndex > pieceCount)
                return $"施工順位 {pieceIndex} 本目は範囲外 (1〜{pieceCount} 本目)。";
            return null;
        }

        // 呼び出し前に Validate を通すこと。範囲外の値に対する結果は保証しない。
        public static PieceJoints Assign(int pieceIndex, int pieceCount)
        {
            return new PieceJoints(pieceIndex > 1, pieceIndex < pieceCount);
        }
    }
}
