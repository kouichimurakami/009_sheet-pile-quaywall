// T950〜T954: PieceAssignment の単体テスト
// 検証基準: 006@6d6d8cf src/SteelPipePile.cs ComputeDerived
//   HasTrailingJoint = pieceIndex < pieceCount (+Y 側)
//   HasLeadingJoint  = pieceIndex > 1          (−Y 側)

namespace SheetPileQuayWall.Core.Tests
{
    public class PieceAssignmentTests
    {
        // T950: 1 本目は −Y 側に先行矢板が無いため継手は +Y 側のみ
        [Xunit.Fact]
        public void T950_Assign_FirstPiece_TrailingOnly()
        {
            SheetPileQuayWall.Core.FrontWall.PieceJoints j =
                SheetPileQuayWall.Core.FrontWall.PieceAssignment.Assign(1, 5);
            Xunit.Assert.False(j.HasLeadingJoint);
            Xunit.Assert.True(j.HasTrailingJoint);
        }

        // T951: 中間の矢板は両側に継手を持つ
        [Xunit.Fact]
        public void T951_Assign_MiddlePiece_BothSides()
        {
            SheetPileQuayWall.Core.FrontWall.PieceJoints j =
                SheetPileQuayWall.Core.FrontWall.PieceAssignment.Assign(3, 5);
            Xunit.Assert.True(j.HasLeadingJoint);
            Xunit.Assert.True(j.HasTrailingJoint);
        }

        // T952: 最終本は +Y 側に後続矢板が無いため継手は −Y 側のみ
        [Xunit.Fact]
        public void T952_Assign_LastPiece_LeadingOnly()
        {
            SheetPileQuayWall.Core.FrontWall.PieceJoints j =
                SheetPileQuayWall.Core.FrontWall.PieceAssignment.Assign(5, 5);
            Xunit.Assert.True(j.HasLeadingJoint);
            Xunit.Assert.False(j.HasTrailingJoint);
        }

        // T953: 総本数 1 本 (単独) は両側とも継手なし
        [Xunit.Fact]
        public void T953_Assign_SinglePiece_NoJoints()
        {
            SheetPileQuayWall.Core.FrontWall.PieceJoints j =
                SheetPileQuayWall.Core.FrontWall.PieceAssignment.Assign(1, 1);
            Xunit.Assert.False(j.HasLeadingJoint);
            Xunit.Assert.False(j.HasTrailingJoint);
        }

        // T954: 施工順位・総本数の範囲チェック
        [Xunit.Theory]
        [Xunit.InlineData(1, 5, true)]     // 正常 (下限)
        [Xunit.InlineData(5, 5, true)]     // 正常 (上限)
        [Xunit.InlineData(0, 5, false)]    // 施工順位が 1 未満
        [Xunit.InlineData(6, 5, false)]    // 施工順位が総本数超過
        [Xunit.InlineData(1, 0, false)]    // 総本数が 1 未満
        [Xunit.InlineData(1, 501, false)]  // 総本数が上限超過
        public void T954_Validate_Range(int pieceIndex, int pieceCount, bool expectValid)
        {
            string? e = SheetPileQuayWall.Core.FrontWall.PieceAssignment.Validate(
                pieceIndex, pieceCount);
            Xunit.Assert.Equal(expectValid, e == null);
        }
    }
}
