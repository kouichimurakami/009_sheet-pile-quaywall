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
        //        (2026-07-29。2026-08-01 に両端固定の配置規則へ変更し、端数が出る
        //         組合せでは最終矢板ぶんが 1 組増える)
        [Xunit.Fact]
        public void T1301_CountFor_DerivesCountFromPieceCountAndEveryN()
        {
            // 12 本の壁、3 本ごと → 1, 4, 7, 10 本目 + 最終 12 本目 の 5 組
            Xunit.Assert.Equal(5,
                SheetPileQuayWall.Core.TieRod.TieRodPitch.CountFor(12, 3));

            // 1 本ごと(新デフォルト) → 全本数ぶん
            Xunit.Assert.Equal(115,
                SheetPileQuayWall.Core.TieRod.TieRodPitch.CountFor(115, 1));

            // 10 本、5 本ごと → 1, 6 本目 + 最終 10 本目 の 3 組(端数 4 本)
            Xunit.Assert.Equal(3,
                SheetPileQuayWall.Core.TieRod.TieRodPitch.CountFor(10, 5));

            // 割り切れる場合(11 本、5 本ごと)→ 1, 6, 11 本目 の 3 組(端数なし)
            Xunit.Assert.Equal(3,
                SheetPileQuayWall.Core.TieRod.TieRodPitch.CountFor(11, 5));

            // 単独杭(1 本)は 1 組
            Xunit.Assert.Equal(1,
                SheetPileQuayWall.Core.TieRod.TieRodPitch.CountFor(1, 3));
        }

        // T1310: 両端固定の配置規則(2026-08-01)。1 本目と最終 P 本目に必ず入る
        [Xunit.Fact]
        public void T1310_PileIndices_AlwaysIncludesFirstAndLastPile()
        {
            // 端数あり: 12 本 3 本ごと → (11 mod 3 = 2) 最終スパンのみ 2 本
            Xunit.Assert.Equal(new int[] { 1, 4, 7, 10, 12 },
                SheetPileQuayWall.Core.TieRod.TieRodPitch.PileIndices(12, 3));

            // 端数なし: 13 本 3 本ごと → 全スパン 3 本の等間隔
            Xunit.Assert.Equal(new int[] { 1, 4, 7, 10, 13 },
                SheetPileQuayWall.Core.TieRod.TieRodPitch.PileIndices(13, 3));

            // 1 本ごとは全矢板
            Xunit.Assert.Equal(new int[] { 1, 2, 3, 4 },
                SheetPileQuayWall.Core.TieRod.TieRodPitch.PileIndices(4, 1));

            // n が壁を跨ぐ場合は両端のみ
            Xunit.Assert.Equal(new int[] { 1, 6 },
                SheetPileQuayWall.Core.TieRod.TieRodPitch.PileIndices(6, 20));

            // 単独杭は 1 組(最終 = 1 本目のため重複追加しない)
            Xunit.Assert.Equal(new int[] { 1 },
                SheetPileQuayWall.Core.TieRod.TieRodPitch.PileIndices(1, 3));
        }

        // T1311: 100 m 級の実寸(B=0.8752 の 115 本)でも両端が入る
        [Xunit.Fact]
        public void T1311_PileIndices_LongWall_KeepsBothEnds()
        {
            int[] indices = SheetPileQuayWall.Core.TieRod.TieRodPitch.PileIndices(115, 4);

            Xunit.Assert.Equal(1, indices[0]);
            Xunit.Assert.Equal(115, indices[indices.Length - 1]);
            Xunit.Assert.Equal(30, indices.Length);

            // 最後から 2 番目は 113 本目。最終スパンだけ 2 本ぶんと短い
            Xunit.Assert.Equal(113, indices[indices.Length - 2]);
            Xunit.Assert.Equal(2,
                SheetPileQuayWall.Core.TieRod.TieRodPitch.RemainderPiles(115, 4));

            // 割り切れる n では端数 0
            Xunit.Assert.Equal(0,
                SheetPileQuayWall.Core.TieRod.TieRodPitch.RemainderPiles(115, 3));
        }

        // T1312: CountFor は PileIndices の要素数に一致する(定義の二重化を防ぐ)
        [Xunit.Fact]
        public void T1312_CountFor_MatchesPileIndicesLength()
        {
            for (int pieceCount = 1; pieceCount <= 40; pieceCount++)
            {
                for (int n = 1; n <= 12; n++)
                {
                    Xunit.Assert.Equal(
                        SheetPileQuayWall.Core.TieRod.TieRodPitch.PileIndices(pieceCount, n).Length,
                        SheetPileQuayWall.Core.TieRod.TieRodPitch.CountFor(pieceCount, n));
                }
            }
        }

        // T1313: 端数スパンを含む全スパンが矢板ピッチの整数倍であること。
        //        矢板中心を横断する条件(TieRodParameters.Validate 7 番)は
        //        端数スパンでも崩れてはならない
        [Xunit.Fact]
        public void T1313_OffsetsY_AllSpansAreIntegerMultiplesOfPitch()
        {
            double[] offsets = SheetPileQuayWall.Core.TieRod.TieRodPitch.OffsetsY(
                115, 4, B_LT75_800);

            for (int i = 1; i < offsets.Length; i++)
            {
                double span = offsets[i] - offsets[i - 1];
                Xunit.Assert.True(
                    SheetPileQuayWall.Core.TieRod.TieRodPitch.SpacingDeviation(
                        span, B_LT75_800)
                    <= SheetPileQuayWall.Core.TieRod.TieRodPitch.Tol_m,
                    $"スパン {span:F4} m が矢板ピッチの整数倍ではありません(組 {i})。");
            }

            // 最終スパンは 2 本ぶん
            Xunit.Assert.Equal(2.0 * B_LT75_800,
                offsets[offsets.Length - 1] - offsets[offsets.Length - 2], 9);
        }

        // T1314: 末尾のオフセットが前壁の最終矢板の中心 Y と一致する(連結テスト)
        [Xunit.Fact]
        public void T1314_OffsetsY_LastMatchesLastFrontWallPileY()
        {
            const int pieceCount = 115;
            double[] offsets = SheetPileQuayWall.Core.TieRod.TieRodPitch.OffsetsY(
                pieceCount, 4, B_LT75_800);

            Xunit.Assert.Equal(0.0, offsets[0], 9);
            Xunit.Assert.Equal(
                SheetPileQuayWall.Core.FrontWall.WallLayout.PositionY(
                    0.0, pieceCount, B_LT75_800),
                offsets[offsets.Length - 1], 9);
        }

        // T1315: 等間隔にできる n の候補は (P−1) の約数のうち間隔範囲に収まるもの
        [Xunit.Fact]
        public void T1315_UniformCandidates_ReturnsDivisorsWithinRange()
        {
            // 115 本 → P−1 = 114 = 2×3×19。約数 1,2,3,6,19,38,57,114 のうち
            // n ≤ 50 かつ間隔 ≤ 20.000 m (0.8752 × 22 = 19.25 m が上限) を満たすもの
            Xunit.Assert.Equal(new int[] { 1, 2, 3, 6, 19 },
                SheetPileQuayWall.Core.TieRod.TieRodPitch.UniformCandidates(115, B_LT75_800));

            // 候補で割り付ければ端数は必ず 0 になる
            foreach (int n in
                SheetPileQuayWall.Core.TieRod.TieRodPitch.UniformCandidates(115, B_LT75_800))
            {
                Xunit.Assert.Equal(0,
                    SheetPileQuayWall.Core.TieRod.TieRodPitch.RemainderPiles(115, n));
            }
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
