// 鋼管矢板式岸壁 1 施設分の数量集計
//
// 006/007/008 はいずれも部材単体の数量しか出さない(前壁 1 本・タイロッド 1 組・
// 控え杭 1 本)。統合版である 009 の付加価値として、施設全体の鋼材質量を
// 3 部材まとめて集計する。フェーズ 5 で新設。
//
// 単位: 長さ m、質量 kg。信頼度ラベルは docs/implementation-plan.md §8 に従う。
//
// 本クラスが行うのは**数量の集計のみ**である。施工歩掛(打設日数・労務編成)は
// 前壁の DriveEstimate が担当し、工種体系(『港湾工事工種体系ツリー.md』)への
// マッピングは行わない。

namespace SheetPileQuayWall.Core
{
    // 岸壁 1 施設の構成。Plugin 層が図面中の各部材の XData を集めて渡す。
    public sealed class QuayWallComposition
    {
        // 前壁鋼管矢板
        public double FrontOuterDm = 0.800;      // 外径 D_f [m]
        public double FrontWallTm = 0.012;       // 肉厚 t_f [m]
        public double FrontLengthM = 20.0;       // 全長 L_f [m]
        public FrontWall.JointType FrontJointType = FrontWall.JointType.LT75;
        public int FrontPieceCount = 1;          // 前壁の総本数 [本]

        // 壁一括生成で実際に配置に使われた有効幅 B [m]。0 以下(未設定)なら
        // FrontOuterDm/FrontJointType からの算出値にフォールバックする
        // (FrontWallRef.ResolveEffectiveWidth と同じ理由。2026-07-29)。
        public double FrontEffectiveWidthM;

        public double ResolveFrontEffectiveWidth()
        {
            return FrontEffectiveWidthM > 0.0
                ? FrontEffectiveWidthM
                : FrontWall.JointParameters.EffectiveWidth(FrontOuterDm, FrontJointType);
        }

        // タイロッド
        public int TieRodSetCount = 0;           // 組数 [組]
        public double TieRodMassPerSet = 0.0;    // 1 組あたり棒部質量 [kg](TieRodResult.RodMass)

        // 控え杭
        public int AnchorPileCount = 0;          // 本数 [本]
        public double AnchorOuterDm = 0.800;     // 外径 D_a [m]
        public double AnchorWallTm = 0.012;      // 肉厚 t_a [m]
        public double AnchorLengthM = 20.0;      // 全長 L_a [m]
        public bool AnchorClosedTip = false;     // 先端形状(閉端なら底板を加算)
    }

    public sealed class QuayWallQuantity
    {
        public QuayWallQuantity(
            double frontBodyKg, double frontJointKg,
            double tieRodKg, double anchorBodyKg, double anchorPlateKg,
            double wallLengthM, int jointConnectionCount)
        {
            FrontBodyKg = frontBodyKg;
            FrontJointKg = frontJointKg;
            TieRodKg = tieRodKg;
            AnchorBodyKg = anchorBodyKg;
            AnchorPlateKg = anchorPlateKg;
            WallLengthM = wallLengthM;
            JointConnectionCount = jointConnectionCount;
        }

        // 前壁 本管質量 [kg] — 確定(K011 単位重量 × 全長 × 本数)
        public double FrontBodyKg { get; }

        // 前壁 継手金物質量 [kg] — 確定(側別質量 × 全長。P-P 形も両側を数える)
        public double FrontJointKg { get; }

        // タイロッド 棒部質量 [kg] — 確定(付属品は含まない。008 の RodMass 準拠)
        public double TieRodKg { get; }

        // 控え杭 本管質量 [kg] — 確定
        public double AnchorBodyKg { get; }

        // 控え杭 閉端底板質量 [kg] — 概算(鋼材密度 7.85 g/cm3 の円板として算出)
        public double AnchorPlateKg { get; }

        // 施設延長 [m] — 確定(有効幅 B × 前壁本数)
        public double WallLengthM { get; }

        // 継手接続数 [箇所] — 確定(前壁本数 − 1)
        public int JointConnectionCount { get; }

        public double FrontTotalKg => FrontBodyKg + FrontJointKg;
        public double AnchorTotalKg => AnchorBodyKg + AnchorPlateKg;
        public double TotalKg => FrontTotalKg + TieRodKg + AnchorTotalKg;
    }

    public static class QuayWallEstimate
    {
        // 鋼材密度 [kg/cm3](006 CreateDisk の底板質量算出と同じ)
        private const double SteelDensity_kgPerCm3 = 0.00785;

        public static QuayWallQuantity Compute(QuayWallComposition c)
        {
            // ── 前壁 ────────────────────────────────────────────────────
            double frontUnitMass = FrontWall.SectionProperties.CalcW(
                c.FrontOuterDm, c.FrontWallTm);
            double frontBodyKg = frontUnitMass * c.FrontLengthM * c.FrontPieceCount;

            // 継手は施工順位ごとに要否が変わる。1 本目は +Y 側のみ、最終本は −Y 側のみ、
            // 中間は両側。全本数を合計すると接続数 × 1 接続あたり質量に一致する。
            double frontJointKg = 0.0;
            for (int index = 1; index <= c.FrontPieceCount; index++)
            {
                FrontWall.PieceJoints joints = FrontWall.PieceAssignment.Assign(
                    index, c.FrontPieceCount);
                frontJointKg += FrontWall.JointMass.PerPile_kgPerM(c.FrontJointType, joints)
                    * c.FrontLengthM;
            }

            double effectiveWidth = c.ResolveFrontEffectiveWidth();
            double wallLengthM = effectiveWidth * c.FrontPieceCount;
            int connections = c.FrontPieceCount > 0 ? c.FrontPieceCount - 1 : 0;

            // ── タイロッド ──────────────────────────────────────────────
            double tieRodKg = c.TieRodMassPerSet * c.TieRodSetCount;

            // ── 控え杭 ──────────────────────────────────────────────────
            double anchorUnitMass = FrontWall.SectionProperties.CalcW(
                c.AnchorOuterDm, c.AnchorWallTm);
            double anchorBodyKg = anchorUnitMass * c.AnchorLengthM * c.AnchorPileCount;

            double anchorPlateKg = 0.0;
            if (c.AnchorClosedTip)
            {
                double diameter_cm = c.AnchorOuterDm * 100.0;
                double thickness_cm = c.AnchorWallTm * 100.0;
                anchorPlateKg = System.Math.PI / 4.0 * diameter_cm * diameter_cm
                    * thickness_cm * SteelDensity_kgPerCm3 * c.AnchorPileCount;
            }

            return new QuayWallQuantity(
                frontBodyKg, frontJointKg, tieRodKg, anchorBodyKg, anchorPlateKg,
                wallLengthM, connections);
        }
    }
}
