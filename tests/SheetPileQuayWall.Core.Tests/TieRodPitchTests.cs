// T1270〜T1276: TieRodPitch の単体テスト
// 検証基準: 取付間隔は矢板ピッチ (= 前壁の有効幅 B) の整数倍でなければならない
//   (TieRodParameters.Validate の 7 番。タイロッドは矢板の中央を横断するため)
// 基準値: D=800mm / LT75 の有効幅 B = 0.8752 m

namespace SheetPileQuayWall.Core.Tests
{
    public class TieRodPitchTests
    {
        private const double B_LT75_800 = 0.8752;

        // T1270: 「何本ごと」から取付間隔を導出する
        [Xunit.Fact]
        public void T1270_SpacingFor_MultipliesPitchByCount()
        {
            Xunit.Assert.Equal(2.6256,
                SheetPileQuayWall.Core.TieRod.TieRodPitch.SpacingFor(B_LT75_800, 3), 9);
            Xunit.Assert.Equal(0.8752,
                SheetPileQuayWall.Core.TieRod.TieRodPitch.SpacingFor(B_LT75_800, 1), 9);
        }

        // T1271: 導出した間隔は TieRodParameters.Validate の整数倍チェックを必ず通る。
        //        従来は利用者が m 単位で手入力しており、既定値 2.400 m は
        //        0.8752 m の整数倍でないため Enter で必ず検証エラーになっていた
        [Xunit.Fact]
        public void T1271_SpacingFor_AlwaysPassesIntegerMultipleCheck()
        {
            for (int n = 1; n <= 10; n++)
            {
                SheetPileQuayWall.Core.TieRod.TieRodParameters p =
                    new SheetPileQuayWall.Core.TieRod.TieRodParameters();
                p.PileDiameter = 0.800;
                p.PilePitch = B_LT75_800;
                p.TieSpacing = SheetPileQuayWall.Core.TieRod.TieRodPitch.SpacingFor(
                    B_LT75_800, n);
                p.ApplyStandardNutHeight();

                System.Collections.Generic.IReadOnlyList<string> errors = p.Validate();
                foreach (string e in errors)
                {
                    Xunit.Assert.DoesNotContain("整数倍", e);
                }
            }
        }

        // T1272: 取付間隔から「何本ごと」を逆算する(CSV 取り込み値・既存 XData の表示用)
        [Xunit.Fact]
        public void T1272_PilesPerSpacing_RecoversCount()
        {
            Xunit.Assert.Equal(3,
                SheetPileQuayWall.Core.TieRod.TieRodPitch.PilesPerSpacing(2.6256, B_LT75_800));
            Xunit.Assert.Equal(1,
                SheetPileQuayWall.Core.TieRod.TieRodPitch.PilesPerSpacing(0.8752, B_LT75_800));

            // ピッチが 0 の異常値では 0 を返す(ゼロ除算しない)
            Xunit.Assert.Equal(0,
                SheetPileQuayWall.Core.TieRod.TieRodPitch.PilesPerSpacing(2.6256, 0.0));
        }

        // T1273: 非整数倍の値は SpacingDeviation で検出できる
        //        (旧既定値 2.400 m は 0.8752 m の 2.742 倍)
        [Xunit.Fact]
        public void T1273_SpacingDeviation_DetectsNonIntegerMultiple()
        {
            Xunit.Assert.True(
                SheetPileQuayWall.Core.TieRod.TieRodPitch.SpacingDeviation(2.400, B_LT75_800)
                > SheetPileQuayWall.Core.TieRod.TieRodPitch.Tol_m);

            Xunit.Assert.True(
                SheetPileQuayWall.Core.TieRod.TieRodPitch.SpacingDeviation(2.6256, B_LT75_800)
                <= SheetPileQuayWall.Core.TieRod.TieRodPitch.Tol_m);
        }

        // T1274: 本数が範囲外、または導出した間隔が取付間隔の許容範囲を外れるとエラー
        [Xunit.Fact]
        public void T1274_Validate_OutOfRange_ReturnsError()
        {
            Xunit.Assert.Null(
                SheetPileQuayWall.Core.TieRod.TieRodPitch.Validate(B_LT75_800, 3));

            // 0 本ごとは不正
            Xunit.Assert.NotNull(
                SheetPileQuayWall.Core.TieRod.TieRodPitch.Validate(B_LT75_800, 0));

            // 0.8752 × 23 = 20.13 m は取付間隔の上限 20.0 m を超える
            Xunit.Assert.NotNull(
                SheetPileQuayWall.Core.TieRod.TieRodPitch.Validate(B_LT75_800, 23));

            // 0.8752 × 22 = 19.25 m は範囲内
            Xunit.Assert.Null(
                SheetPileQuayWall.Core.TieRod.TieRodPitch.Validate(B_LT75_800, 22));
        }

        // T1275: TieRodPitch が複写している取付間隔の範囲が、
        //        TieRodParameters.Validate の実際の判定と一致していること。
        //        TieRodParameters.cs は port-from-legacy.sh の同期対象で定数を
        //        公開できないため、乖離をこのテストで検出する
        [Xunit.Fact]
        public void T1275_SpacingRange_MatchesTieRodParametersValidate()
        {
            SheetPileQuayWall.Core.TieRod.TieRodParameters p =
                new SheetPileQuayWall.Core.TieRod.TieRodParameters();
            p.PileDiameter = 0.800;

            // 上限ちょうど (20.000 m) は通る。ピッチ 2.000 m × 10 本
            p.PilePitch = 2.000;
            p.TieSpacing = SheetPileQuayWall.Core.TieRod.TieRodPitch.Spacing_Max_m;
            p.ApplyStandardNutHeight();
            foreach (string e in p.Validate())
            {
                Xunit.Assert.DoesNotContain("タイロッド取付間隔", e);
            }

            // 上限を超えると TieRodParameters 側がエラーにする
            p.TieSpacing = SheetPileQuayWall.Core.TieRod.TieRodPitch.Spacing_Max_m + 0.100;
            bool found = false;
            foreach (string e in p.Validate())
            {
                if (e.Contains("タイロッド取付間隔")) { found = true; }
            }
            Xunit.Assert.True(found);
        }

        // T1301: 矢板本数と配置間隔(何本ごと)からタイロッド組数を自動算定する
        //        (2026-07-29。1本目に配置し、以降 n 本ごとに配置した場合に必要な組数)
        [Xunit.Fact]
        public void T1301_CountFor_DerivesCountFromPieceCountAndEveryN()
        {
            // 12 本の壁、3 本ごと → 1, 4, 7, 10 本目の 4 組
            Xunit.Assert.Equal(4,
                SheetPileQuayWall.Core.TieRod.TieRodPitch.CountFor(12, 3));

            // 1 本ごと(新デフォルト) → 全本数ぶん
            Xunit.Assert.Equal(115,
                SheetPileQuayWall.Core.TieRod.TieRodPitch.CountFor(115, 1));

            // ちょうど割り切れる場合(10 本、5 本ごと)→ 1, 6 の 2 組
            Xunit.Assert.Equal(2,
                SheetPileQuayWall.Core.TieRod.TieRodPitch.CountFor(10, 5));

            // 単独杭(1 本)は 1 組
            Xunit.Assert.Equal(1,
                SheetPileQuayWall.Core.TieRod.TieRodPitch.CountFor(1, 3));
        }

        // T1276: 前壁の有効幅からタイロッド・控え杭が同じ間隔で並ぶ(連結テスト)
        [Xunit.Fact]
        public void T1276_SameEveryN_ProducesIdenticalSpacingForBothMembers()
        {
            double b = SheetPileQuayWall.Core.FrontWall.JointParameters.EffectiveWidth(
                0.800, SheetPileQuayWall.Core.FrontWall.JointType.LT75);
            Xunit.Assert.Equal(B_LT75_800, b, 4);

            double tieSpacing = SheetPileQuayWall.Core.TieRod.TieRodPitch.SpacingFor(b, 3);
            double anchorSpacing = SheetPileQuayWall.Core.TieRod.TieRodPitch.SpacingFor(b, 3);

            Xunit.Assert.Equal(tieSpacing, anchorSpacing, 9);

            // 3 本ごとなら、前壁の 1・4・7・10 本目の中心に一致する
            for (int i = 0; i < 4; i++)
            {
                double tieY = i * tieSpacing;
                double frontPileY = (1 + i * 3 - 1) * b;   // 施工順位 1, 4, 7, 10
                Xunit.Assert.Equal(frontPileY, tieY, 9);
            }
        }
    }
}
