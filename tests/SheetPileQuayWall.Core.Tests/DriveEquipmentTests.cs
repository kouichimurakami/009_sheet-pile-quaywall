// T1250〜T1259: DriveEquipment の単体テスト
// 検証基準: 港湾土木請負工事積算基準 令和7年度改訂版 3-4.5-14〜15 / 3-4.6-12〜13
// AutoCAD 非依存のため xUnit で直接実行可能

namespace SheetPileQuayWall.Core.Tests
{
    public class DriveEquipmentTests
    {
        // ── GetCrawlerDriver (クローラ式杭打機、陸上打設) ──────────────────

        // T1250: ハンマ規格→杭打機3ランクへの変換(基準 3-4.5-14 / 3-4.6-12)。
        // 「6.5～8 t」は原文の縦結合セル。ハンマ 15.0t は陸上打設の表に行が無く表外。
        [Xunit.Theory]
        [Xunit.InlineData("4～4.5 t",   "4～4.5 t")]
        [Xunit.InlineData("6.5 t",      "6.5～8 t")]
        [Xunit.InlineData("7～8 t",     "6.5～8 t")]
        [Xunit.InlineData("10～12.5 t", "10～12.5 t")]
        [Xunit.InlineData("15.0 t",     "")]
        public void T1250_GetCrawlerDriver_MatchesTable(string hammerClass, string expected)
        {
            string driver = SheetPileQuayWall.Core.FrontWall.DriveEquipment.GetCrawlerDriver(hammerClass);
            Xunit.Assert.Equal(expected, driver);
        }

        // T1251: テーブル外(「15.0 t 超」)は空文字
        [Xunit.Fact]
        public void T1251_GetCrawlerDriver_BeyondAllTiers_ReturnsEmpty()
        {
            string driver = SheetPileQuayWall.Core.FrontWall.DriveEquipment.GetCrawlerDriver(
                "15.0 t 超（別途検討）");
            Xunit.Assert.Equal("", driver);
        }

        // T1252: クローラクレーン規格は固定 "50t吊"
        [Xunit.Fact]
        public void T1252_CrawlerCraneSpec_IsFixed50tSpec()
        {
            Xunit.Assert.Equal("50t吊", SheetPileQuayWall.Core.FrontWall.DriveEquipment.CrawlerCraneSpec);
        }

        // ── GetPileDriverVessel (杭打船、海上打設) ─────────────────────────

        // T1253〜T1257: ハンマ5規格 → 杭打船3ランクへの変換(基準 3-4.5-15)
        [Xunit.Theory]
        [Xunit.InlineData("4～4.5 t",   "H-65")]
        [Xunit.InlineData("6.5 t",      "H-65")]
        [Xunit.InlineData("7～8 t",     "H-125")]
        [Xunit.InlineData("10～12.5 t", "H-125")]
        [Xunit.InlineData("15.0 t",     "H-150")]
        public void T1253_GetPileDriverVessel_AllHammerTiers_MatchesTable(
            string hammerClass, string expectedVessel)
        {
            string vessel = SheetPileQuayWall.Core.FrontWall.DriveEquipment.GetPileDriverVessel(hammerClass);
            Xunit.Assert.Equal(expectedVessel, vessel);
        }

        // T1258: テーブル外(「15.0 t 超」)は空文字
        [Xunit.Fact]
        public void T1258_GetPileDriverVessel_BeyondAllTiers_ReturnsEmpty()
        {
            string vessel = SheetPileQuayWall.Core.FrontWall.DriveEquipment.GetPileDriverVessel(
                "15.0 t 超（別途検討）");
            Xunit.Assert.Equal("", vessel);
        }

        // ── GetHammerClass との連結 ─────────────────────────────────────────
        //
        // DriveEquipment の照合キーは DriveEstimate.GetHammerClass の戻り値ラベルの
        // 転記であり、DriveEstimate.cs は port-from-legacy.sh が 007 と同期し続ける。
        // 007 側でラベル表記が 1 文字でも変わると GetCrawlerDriver / GetPileDriverVessel
        // は黙って空文字を返し始めるため、実際の戻り値を通して連結を検証する。

        // T1259: GetHammerClass の実戻り値 → 杭打機・杭打船が期待どおり選定されること
        [Xunit.Theory]
        //               鋼材質量   R        杭打船      杭打機
        [Xunit.InlineData( 3.0,   4000.0, "H-65",  "4～4.5 t")]   // → 4～4.5 t
        [Xunit.InlineData( 5.0,   6000.0, "H-65",  "6.5～8 t")]   // → 6.5 t
        [Xunit.InlineData( 9.0,  12000.0, "H-125", "6.5～8 t")]   // → 7～8 t
        [Xunit.InlineData(15.0,  20000.0, "H-125", "10～12.5 t")] // → 10～12.5 t
        [Xunit.InlineData(25.0,  30000.0, "H-150", "")]           // → 15.0 t(陸上表に行なし)
        public void T1259_ChainedWithGetHammerClass_SelectsExpectedEquipment(
            double steelMass_t, double R_kN, string expectedVessel, string expectedDriver)
        {
            string hammer = SheetPileQuayWall.Core.FrontWall.DriveEstimate.GetHammerClass(
                steelMass_t, R_kN);
            Xunit.Assert.Equal(expectedVessel,
                SheetPileQuayWall.Core.FrontWall.DriveEquipment.GetPileDriverVessel(hammer));
            Xunit.Assert.Equal(expectedDriver,
                SheetPileQuayWall.Core.FrontWall.DriveEquipment.GetCrawlerDriver(hammer));
        }
    }
}
