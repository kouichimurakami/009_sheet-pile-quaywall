// T1320〜T1323: 控え杭を振動工法で積算する経路の連結テスト(2026-08-01)
//
// 計算層(VibroEstimate / VibroJetEstimate)自体は VibroEstimateTests /
// VibroJetEstimateTests が検証済みのため、ここでは**控え杭固有の使い方**だけを扱う:
//   - 打込み対象が鋼管杭(継手なし)であること
//   - 控え杭の実諸元(アライズ計算書の φ1000×11 L=12.5m)で一気通貫に成立すること
//   - 継手加算時間 ε = 0 で打込み時間が継手ぶん増えないこと

namespace SheetPileQuayWall.Core.Tests
{
    public class AnchorVibroEstimateTests
    {
        // 控え杭の代表諸元(docs/samples/arise_design_input.json の anchor_pile)
        private const double D_m = 1.000;
        private const double T_m = 0.011;
        private const double L_m = 12.5;

        // T1320: 控え杭(鋼管杭)をバイブロ単独で積算する一気通貫。
        //        φ1000×11 L=12.5m / Lb=12.5m / N_tip=50 / N_avg=20
        [Xunit.Fact]
        public void T1320_AnchorPile_VibroAlone_EndToEnd()
        {
            SheetPileQuayWall.Core.FrontWall.VibroDriveTarget target =
                SheetPileQuayWall.Core.FrontWall.VibroDriveTarget.SteelPipePile;

            // 鋼材質量は本管のみ(継手を持たない単独杭)
            // W = 2.466 × 1.1 × (100 − 1.1) = 268.276 kg/m → × 12.5 = 3353.45 kg
            double W_kgPerM = SheetPileQuayWall.Core.FrontWall.SectionProperties.CalcW(D_m, T_m);
            double steelMass_t = W_kgPerM * L_m / 1000.0;
            Xunit.Assert.Equal(3.353, steelMass_t, 3);

            // R = 300×50×(π/4) + 2×20×12.5×π = 11780.97 + 1570.80 = 13351.8 kN
            double r = SheetPileQuayWall.Core.FrontWall.VibroEstimate.CalcR(
                D_m, L_m, 50, 20.0, target);
            Xunit.Assert.Equal(13351.8, r, 1);

            // 鋼管杭のため継手抵抗の加算は無く、R は R1 と一致する
            Xunit.Assert.Equal(
                SheetPileQuayWall.Core.FrontWall.VibroEstimate.CalcR1(D_m, L_m, 50, 20.0), r, 1);

            // 質量は 120kW の範囲だが、R が 150kW の上限 13,000kN を超えるため 200kW
            string vibroClass =
                SheetPileQuayWall.Core.FrontWall.VibroEstimate.GetVibroClass(steelMass_t, r);
            Xunit.Assert.Equal("200kW", vibroClass);

            var (generator, craneVessel) =
                SheetPileQuayWall.Core.FrontWall.VibroEstimate.GetEquipment(vibroClass);
            Xunit.Assert.Equal("600kVA", generator);
            Xunit.Assert.Equal("200t吊", craneVessel);

            // Tp = 24 + 0.6×(12.5−25) = 16.5 分/本(基準打設長 25m を下回るため減算)
            Xunit.Assert.Equal(16.5,
                SheetPileQuayWall.Core.FrontWall.VibroEstimate.CalcTp(L_m), 2);

            // Tb = 12.5 ÷ 0.90 = 13.89 分/本(鋼管杭の Lo。鋼管矢板の 0.75 ではない)
            Xunit.Assert.Equal(13.89,
                SheetPileQuayWall.Core.FrontWall.VibroEstimate.CalcTb(L_m, target), 2);

            // 労務は鋼管杭の行。打設長 12.5m ≤ 25m のため とび工 2 人(鋼管矢板は 3 人)
            var labor = SheetPileQuayWall.Core.FrontWall.VibroEstimate.GetLabor(
                target, L_m, false, 1000);
            Xunit.Assert.Equal(1, labor.foreman);
            Xunit.Assert.Equal(2, labor.rigger);
            Xunit.Assert.Equal(3, labor.laborer);
            Xunit.Assert.Equal(1, labor.specialist);
            Xunit.Assert.Equal(0, labor.welder);
        }

        // T1321: 控え杭の全長(12.5m)は台船・引船の規格表(44m 未満)に収まる
        [Xunit.Fact]
        public void T1321_AnchorPile_BargeAndTug_WithinTable()
        {
            var (barge, tug) =
                SheetPileQuayWall.Core.FrontWall.VibroEstimate.GetBargeAndTug(L_m);
            Xunit.Assert.Equal("鋼300t積", barge);
            Xunit.Assert.Equal("鋼D450PS型", tug);
        }

        // T1322: ジェット併用の適用範囲(外径 1,500mm 以下・全長 40m 以下、3-1-3 注3)を
        //        控え杭の諸元で確認する。控え杭は陸上で振動工法を使える唯一の基準準拠経路
        [Xunit.Fact]
        public void T1322_AnchorPile_JetApplicability()
        {
            Xunit.Assert.Null(
                SheetPileQuayWall.Core.FrontWall.VibroJetEstimate.ValidateJetApplicability(
                    D_m, L_m));

            // 外径が範囲外(φ1600)
            Xunit.Assert.NotNull(
                SheetPileQuayWall.Core.FrontWall.VibroJetEstimate.ValidateJetApplicability(
                    1.600, L_m));
        }

        // T1323: 控え杭は継手を持たないため ε = 0。継手加算のぶん打込み時間が
        //        増えないことを、前壁(ε>0)との対比で確認する
        [Xunit.Fact]
        public void T1323_AnchorPile_NoJointExtraTime()
        {
            const double gamma = 0.9;
            const double beta = 1.0;
            const double delta = 1.0;

            // γ·β·δ·ℓ = 11.25 → 小数1位切上げ 11.3
            double tbAnchor = SheetPileQuayWall.Core.FrontWall.VibroJetEstimate.CalcTb(
                gamma, beta, delta, L_m, 0.0);
            Xunit.Assert.Equal(11.3, tbAnchor, 2);

            // 前壁は継手長 ℓj=12.5m → ε = 0.3 × 12.5 = 3.75 分ぶん長くなる
            double epsilon =
                SheetPileQuayWall.Core.FrontWall.VibroJetEstimate.CalcEpsilon(L_m);
            double tbSheetPile = SheetPileQuayWall.Core.FrontWall.VibroJetEstimate.CalcTb(
                gamma, beta, delta, L_m, epsilon);
            Xunit.Assert.Equal(3.75, epsilon, 2);
            Xunit.Assert.True(tbSheetPile > tbAnchor);
        }
    }
}
