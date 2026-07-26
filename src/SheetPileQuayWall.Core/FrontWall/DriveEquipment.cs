// AutoCAD 非依存 — xUnit で単体テスト可能
// 計算式出典: 港湾土木請負工事積算基準 令和7年度改訂版
//             4節 本体工 4.5「鋼矢板式」2-1-4-2-2「作業船・機械の選定」(3-4.5-14〜15)
//             4節 本体工 4.6「鋼杭式」  2-3-3-2-2「作業船・機械の選定」(3-4.6-12〜13)
//
// 打撃工法(前壁・控え杭)のハンマ規格に対応する陸上打設の杭打機・クローラクレーン、
// および海上打設の杭打船を扱う。ハンマ規格決定表(HammerTable)自体は移植元 007 由来の
// FrontWall.DriveEstimate にあり、同ファイルは scripts/port-from-legacy.sh が
// 007@b12b188 と完全同期させる対象のため、009 側の追加はこの新規ファイルに置く
// (DriveEstimate.cs 自体には触れない。控え杭側の AnchorPile.AnchorDriveEstimate と
// 同じ回避パターン)。
//
// ハンマ規格と杭打機・杭打船の対応は、原文表のセル結合位置から次のように読み取れる
// (信頼度:推定。境界の最終確認は原本を推奨):
//   クローラ式杭打機(陸上): 4～4.5t / 6.5～8t(縦結合セル) / 10～12.5t の 3 ランク。
//                            陸上打設の表にはハンマ 15.0t の行が存在しない(表外)。
//   杭打船(海上):           H-65(4～4.5t・6.5t) / H-125(7～8t・10～12.5t) / H-150(15t)。
// 台船・引船・揚錨船・潜水士船は FrontWall.VibroEstimate の該当メンバーを再利用する
// (3-4.5-15 注3「台船および引船の規格は、鋼矢板・鋼管矢板海上運搬の規格とする」
//  〔3-4.6-13 注3 は「鋼杭海上運搬」〕により、16節 3-2 と同一表であることが確認済み)。
// なお引船・潜水士船は原文で「現場条件による追加船団」に区分され、引船は
// 「現場条件により杭打船の移動が必要な場合」のみ計上する(3-4.5-15 注1 / 3-4.6-13 注1)。
// 計上要否の判断は呼び出し側(コマンド・ノード)が担う。

namespace SheetPileQuayWall.Core.FrontWall
{
    public static class DriveEquipment
    {
        /// <summary>クローラクレーン(小運搬用)の規格。陸上打設で条件該当時のみ計上する
        /// (杭置場が遠い・高低差がある・近隣に被害の恐れがある場合。3-4.5-14/3-4.6-12)。</summary>
        public const string CrawlerCraneSpec = "50t吊";

        // ── クローラ式杭打機の規格選定テーブル(陸上打設、3-4.5-14/3-4.6-12)──
        // ハンマ規格を3ランクの杭打機規格へ変換する。「6.5～8 t」は原文の縦結合セル
        // (ハンマ 6.5t 行と 7～8t 行の間に単独出現)。陸上打設の表にはハンマ 15.0t の
        // 行が無いため、15.0t は表外(空文字 = 別途検討)とする。
        // キーは FrontWall.DriveEstimate.GetHammerClass の戻り値ラベルと一致させる
        // (連結の健全性は T1259 で検証。007 側でラベル表記が変わると空文字化するため)。
        private static readonly (string hammerLabel, string driverSpec)[] CrawlerDriverTable =
        {
            ( "4～4.5 t",    "4～4.5 t"   ),
            ( "6.5 t",       "6.5～8 t"   ),
            ( "7～8 t",      "6.5～8 t"   ),
            ( "10～12.5 t",  "10～12.5 t" ),
        };

        // ── 杭打船の規格選定テーブル(海上打設、3-4.5-15/3-4.6-13)──────────
        // ハンマ規格(5ランク)を3ランクの杭打船規格へ変換する。対応は原文の
        // セル結合位置(H-65 が 4～4.5t/6.5t 行間、H-125 が 7～8t/10～12.5t 行間、
        // H-150 が 15t 行)による。
        private static readonly (string hammerLabel, string vesselSpec)[] PileDriverVesselTable =
        {
            ( "4～4.5 t",    "H-65"  ),
            ( "6.5 t",       "H-65"  ),
            ( "7～8 t",      "H-125" ),
            ( "10～12.5 t",  "H-125" ),
            ( "15.0 t",      "H-150" ),
        };

        /// <summary>クローラ式杭打機の規格(陸上打設、3-4.5-14/3-4.6-12)。
        /// テーブル外は空文字を返す(別途検討)。ハンマ「15.0 t」も陸上打設の表に
        /// 行が無いため空文字になる点に注意。</summary>
        public static string GetCrawlerDriver(string hammerClass)
        {
            foreach (var (label, driver) in CrawlerDriverTable)
            {
                if (label == hammerClass) { return driver; }
            }
            return "";
        }

        /// <summary>杭打船の規格(海上打設、3-4.5-15/3-4.6-13)。テーブル外は空文字を返す。</summary>
        public static string GetPileDriverVessel(string hammerClass)
        {
            foreach (var (label, vessel) in PileDriverVesselTable)
            {
                if (label == hammerClass) { return vessel; }
            }
            return "";
        }
    }
}
