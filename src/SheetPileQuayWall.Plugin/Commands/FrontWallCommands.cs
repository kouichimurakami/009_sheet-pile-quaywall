// === 参照DLLバージョン: 未検証 ===
// AcCoreMgd.dll : 未検証 (期待値 25.x.x.x)
// AcDbMgd.dll   : 未検証 (期待値 25.x.x.x)
// AcMgd.dll     : 未検証 (期待値 25.x.x.x)
// 検証日: 未実施 / 検証コマンド: scripts/verify-dll-versions.ps1 → exit 2 (未検出)
// リスク: TypeLoadException / MissingMethodException が実行時に発生する可能性がある。
//
// 前壁鋼管矢板のコマンド(SPQW_FRONTWALL_Create / _Action / _Query / _Estimate)
// 移植元: 007 SPSP_Create / _Action / _Query / _Estimate + 006 の傾斜角・施工順位。
//
// 007 との違い:
//   - 挿入点は「平面位置ピック(UCS→WCS)+ 杭先端標高の数値入力」に分離(§2.2)
//   - 継手は施工順位から要否を判定する(PieceAssignment)。007 は常に両側に付けていた
//   - 傾斜角 θ を持ち、配置は 回転(Y軸) → 平行移動 の順(006 BuildPileSolid 踏襲)
//   - SPSP_JointModel は移植しない(決定10)

namespace SheetPileQuayWall.Plugin.Commands
{
    public static class FrontWallCommands
    {
        public const string LayerName = "前壁鋼管矢板";

        // SPQW_FRONTWALL_Create の施設全長の既定値 [m]
        private const double DefaultWallLength_m = 10.000;

        // ════════════════════════════════════════════════════════════════════
        // SPQW_FRONTWALL_Create: 対話入力 → 施設全長と有効幅から本数を自動算出 →
        //                        始点から +Y 方向へ壁を一括生成 → XData 記録
        //
        // 本数は切り上げ (Core WallLayout)。施工順位は 1 始まりで自動採番するため、
        // 継手の要否・雌雄は PieceAssignment が自動判定する。
        // ════════════════════════════════════════════════════════════════════
        [Autodesk.AutoCAD.Runtime.CommandMethod("SPQW_FRONTWALL_Create")]
        public static void Create()
        {
            Autodesk.AutoCAD.ApplicationServices.Document doc =
                Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager
                    .MdiActiveDocument;
            Autodesk.AutoCAD.DatabaseServices.Database db = doc.Database;
            Autodesk.AutoCAD.EditorInput.Editor ed = doc.Editor;

            SheetPileQuayWall.Plugin.XData.FrontWallRecord record =
                new SheetPileQuayWall.Plugin.XData.FrontWallRecord();

            int pieceCount;
            double effectiveWidth_m;
            double wallLength_m;
            if (!PromptWallRecord(ed, record, out pieceCount, out effectiveWidth_m,
                out wallLength_m))
            {
                return;
            }

            // 実際に配置に使った有効幅を記録する。入力値が外径・継手形式からの
            // 算出値と食い違っていても、タイロッド・控え杭・施設積算はこの値を
            // 参照して整合を取る(算出値を再計算すると実際の矢板間隔とズレる。
            // 2026-07-29 発見)。
            record.EffectiveWidthM = effectiveWidth_m;

            double startY = record.TipPoint.Y;

            using (Autodesk.AutoCAD.DatabaseServices.Transaction tr =
                db.TransactionManager.StartTransaction())
            {
                SheetPileQuayWall.Plugin.XData.XDataStore.EnsureRegApp(
                    tr, db, SheetPileQuayWall.Plugin.XData.FrontWallRecord.RegAppName);
                SheetPileQuayWall.Plugin.DrawingHelper.EnsureLayer(db, tr, LayerName, 8);

                for (int i = 1; i <= pieceCount; i++)
                {
                    record.PieceIndex = i;
                    record.PieceCount = pieceCount;
                    record.TipPoint = SheetPileQuayWall.Core.FrontWall.FrontWallPlacement.TipPoint(
                        record.TipPoint.X,
                        SheetPileQuayWall.Core.FrontWall.WallLayout.PositionY(
                            startY, i, effectiveWidth_m),
                        record.TipPoint.Z);

                    SheetPileQuayWall.Plugin.DrawingHelper.AppendSolid(
                        db, tr, BuildSolid(record), LayerName, record.ColorIdx,
                        record.ToBuffer());
                }

                tr.Commit();
            }

            PrintWallSummary(ed, record, pieceCount, effectiveWidth_m, wallLength_m, startY);
            ed.WriteMessage(
                $"\n{pieceCount} 本を生成し、パラメータを XData (RegApp: " +
                $"{SheetPileQuayWall.Plugin.XData.FrontWallRecord.RegAppName}) に保存しました。");
            ed.WriteMessage("\nSPQW_FRONTWALL_Create 完了。");
        }

        // ════════════════════════════════════════════════════════════════════
        // SPQW_FRONTWALL_Action: 既存選択 → 再入力 → 同位置に再生成
        // 挿入点は XData の 1011(World 座標点)を優先して読むため、MOVE 後は
        // 移動先に追随して再生成される(006 の挿入点追随を維持)
        // ════════════════════════════════════════════════════════════════════
        [Autodesk.AutoCAD.Runtime.CommandMethod("SPQW_FRONTWALL_Action")]
        public static void Action()
        {
            Autodesk.AutoCAD.ApplicationServices.Document doc =
                Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager
                    .MdiActiveDocument;
            Autodesk.AutoCAD.DatabaseServices.Database db = doc.Database;
            Autodesk.AutoCAD.EditorInput.Editor ed = doc.Editor;

            Autodesk.AutoCAD.EditorInput.PromptEntityResult res =
                SheetPileQuayWall.Plugin.DrawingHelper.SelectSolid(
                    ed, "\n再生成する前壁鋼管矢板 (Solid3d) を選択: ");
            if (res.Status != Autodesk.AutoCAD.EditorInput.PromptStatus.OK)
            {
                return;
            }

            SheetPileQuayWall.Plugin.XData.FrontWallRecord? stored = ReadRecord(db, res.ObjectId);
            if (stored == null)
            {
                ed.WriteMessage(
                    $"\nエラー: XData (RegApp: " +
                    $"{SheetPileQuayWall.Plugin.XData.FrontWallRecord.RegAppName}) が" +
                    $"見つかりません。SPQW_FRONTWALL_Create で作成したソリッドを選択してください。");
                return;
            }

            // 平面位置は再ピックせず保持する(MOVE 済みなら 1011 由来の現在位置)。
            // 標高・諸元のみ再入力する。
            if (!PromptRecord(ed, stored, askPlanPoint: false))
            {
                return;
            }

            BuildAndAppend(db, ed, stored, replaceId: res.ObjectId);

            PrintSummary(ed, stored);
            ed.WriteMessage("\nSPQW_FRONTWALL_Action 完了(同位置に再生成しました)。");
        }

        // ════════════════════════════════════════════════════════════════════
        // SPQW_FRONTWALL_Query: XData 読取 → 諸元・断面性能・数量を出力
        // ════════════════════════════════════════════════════════════════════
        [Autodesk.AutoCAD.Runtime.CommandMethod("SPQW_FRONTWALL_Query")]
        public static void Query()
        {
            Autodesk.AutoCAD.ApplicationServices.Document doc =
                Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager
                    .MdiActiveDocument;
            Autodesk.AutoCAD.DatabaseServices.Database db = doc.Database;
            Autodesk.AutoCAD.EditorInput.Editor ed = doc.Editor;

            Autodesk.AutoCAD.EditorInput.PromptEntityResult res =
                SheetPileQuayWall.Plugin.DrawingHelper.SelectSolid(
                    ed, "\n諸元を表示する前壁鋼管矢板 (Solid3d) を選択: ");
            if (res.Status != Autodesk.AutoCAD.EditorInput.PromptStatus.OK)
            {
                return;
            }

            SheetPileQuayWall.Plugin.XData.FrontWallRecord? record =
                ReadRecord(db, res.ObjectId);
            if (record == null)
            {
                ed.WriteMessage("\nエラー: 前壁の XData が見つかりません。");
                return;
            }

            PrintSummary(ed, record);
            ed.WriteMessage("\nSPQW_FRONTWALL_Query 完了。");
        }

        // ════════════════════════════════════════════════════════════════════
        // SPQW_FRONTWALL_Estimate: 打設歩掛積算(貫入抵抗・ハンマ選定・打設日数)
        // ════════════════════════════════════════════════════════════════════
        [Autodesk.AutoCAD.Runtime.CommandMethod("SPQW_FRONTWALL_Estimate")]
        public static void Estimate()
        {
            Autodesk.AutoCAD.ApplicationServices.Document doc =
                Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager
                    .MdiActiveDocument;
            Autodesk.AutoCAD.DatabaseServices.Database db = doc.Database;
            Autodesk.AutoCAD.EditorInput.Editor ed = doc.Editor;

            Autodesk.AutoCAD.EditorInput.PromptEntityResult res =
                SheetPileQuayWall.Plugin.DrawingHelper.SelectSolid(
                    ed, "\n積算する前壁鋼管矢板 (Solid3d) を選択: ");
            if (res.Status != Autodesk.AutoCAD.EditorInput.PromptStatus.OK)
            {
                return;
            }

            SheetPileQuayWall.Plugin.XData.FrontWallRecord? record =
                ReadRecord(db, res.ObjectId);
            if (record == null)
            {
                ed.WriteMessage("\nエラー: 前壁の XData が見つかりません。");
                return;
            }

            // ── 施工条件(積算基準 3-4.5)────────────────────────────────
            string siteText;
            if (!SheetPileQuayWall.Plugin.Prompt.TryAskKeyword(
                ed, "\n施工区分 [海上(O)/陸上(L)] <O>: ",
                new string[] { "O", "L" }, "O", out siteText))
            {
                return;
            }
            SheetPileQuayWall.Core.FrontWall.ConstructionSite site =
                siteText == "L"
                ? SheetPileQuayWall.Core.FrontWall.ConstructionSite.Onshore
                : SheetPileQuayWall.Core.FrontWall.ConstructionSite.Offshore;

            double penetration_m;
            if (!SheetPileQuayWall.Plugin.Prompt.TryAskDouble(
                ed, $"\n根入れ長 (m) <{record.LengthM * 0.5:F1}>: ",
                record.LengthM * 0.5, 0.1, record.LengthM, out penetration_m))
            {
                return;
            }

            int pileCount;
            if (!SheetPileQuayWall.Plugin.Prompt.TryAskInt(
                ed, $"\n打設本数 (本) <{record.PieceCount}>: ",
                record.PieceCount, 1, 500, out pileCount))
            {
                return;
            }

            int nTip;
            if (!SheetPileQuayWall.Plugin.Prompt.TryAskInt(
                ed, "\n先端 N 値 <50>: ", 50, 1, 100, out nTip))
            {
                return;
            }

            int nAvg;
            if (!SheetPileQuayWall.Plugin.Prompt.TryAskInt(
                ed, "\n加重平均 N 値 <20>: ", 20, 1, 100, out nAvg))
            {
                return;
            }

            int jointCountPerPile;
            if (!SheetPileQuayWall.Plugin.Prompt.TryAskInt(
                ed, "\n継杭の継手個所数 (単杭=0) <0>: ", 0, 0, 5, out jointCountPerPile))
            {
                return;
            }

            string seaText = "N";
            if (site == SheetPileQuayWall.Core.FrontWall.ConstructionSite.Offshore)
            {
                if (!SheetPileQuayWall.Plugin.Prompt.TryAskKeyword(
                    ed, "\n海象条件 [普通(N)/悪い(S)] <N>: ",
                    new string[] { "N", "S" }, "N", out seaText))
                {
                    return;
                }
            }
            SheetPileQuayWall.Core.FrontWall.SeaCondition sea =
                seaText == "S"
                ? SheetPileQuayWall.Core.FrontWall.SeaCondition.Severe
                : SheetPileQuayWall.Core.FrontWall.SeaCondition.Normal;

            string obstacleText;
            if (!SheetPileQuayWall.Plugin.Prompt.TryAskKeyword(
                ed, "\n障害の有無 [なし(N)/あり(E)] <N>: ",
                new string[] { "N", "E" }, "N", out obstacleText))
            {
                return;
            }
            SheetPileQuayWall.Core.FrontWall.ObstacleStatus obstacle =
                obstacleText == "E"
                ? SheetPileQuayWall.Core.FrontWall.ObstacleStatus.Exists
                : SheetPileQuayWall.Core.FrontWall.ObstacleStatus.None;

            // ── 作業船・機械(3-4.5-14〜15)─────────────────────────────
            // 引船・潜水士船は「現場条件による追加船団」であり、それぞれ杭打船の移動の
            // 要否(注1)・調査作業の有無(注2)で計上を判断する。
            bool needCrawlerCrane = false;
            bool needTugBoat = false;
            bool needDiverVessel = false;
            if (site == SheetPileQuayWall.Core.FrontWall.ConstructionSite.Onshore)
            {
                string craneText;
                if (!SheetPileQuayWall.Plugin.Prompt.TryAskKeyword(
                    ed, "\nクローラクレーン (小運搬用) [計上しない(N)/計上する(Y)] <N>: ",
                    new string[] { "N", "Y" }, "N", out craneText))
                {
                    return;
                }
                needCrawlerCrane = craneText == "Y";
            }
            else
            {
                string tugText;
                if (!SheetPileQuayWall.Plugin.Prompt.TryAskKeyword(
                    ed, "\n引船 (現場条件により杭打船の移動が必要な場合) [計上しない(N)/計上する(Y)] <N>: ",
                    new string[] { "N", "Y" }, "N", out tugText))
                {
                    return;
                }
                needTugBoat = tugText == "Y";

                string diverText;
                if (!SheetPileQuayWall.Plugin.Prompt.TryAskKeyword(
                    ed, "\n潜水士船 (打設個所の障害物・打設後異常の調査作業) [計上しない(N)/計上する(Y)] <N>: ",
                    new string[] { "N", "Y" }, "N", out diverText))
                {
                    return;
                }
                needDiverVessel = diverText == "Y";
            }

            // ── 積算(移植元 007 SPSP_Estimate と同じ手順)──────────────
            int D_mm = (int)System.Math.Round(record.OuterDm * 1000.0);
            int t_mm = (int)System.Math.Round(record.WallTm * 1000.0);

            double W_kgPerM = SheetPileQuayWall.Core.FrontWall.SectionProperties.CalcW(
                record.OuterDm, record.WallTm);
            double mass1_kg = W_kgPerM * record.LengthM;
            double totalMass_t = mass1_kg * pileCount / 1000.0;

            double R = SheetPileQuayWall.Core.FrontWall.DriveEstimate.CalcR(
                record.OuterDm, penetration_m, nTip, nAvg);
            string hammer = SheetPileQuayWall.Core.FrontWall.DriveEstimate.GetHammerClass(
                mass1_kg / 1000.0, R);

            double Sb = SheetPileQuayWall.Core.FrontWall.DriveEstimate.GetSb(D_mm, nAvg);
            double Tp = SheetPileQuayWall.Core.FrontWall.DriveEstimate.CalcTp(
                site, jointCountPerPile);
            double Tb = SheetPileQuayWall.Core.FrontWall.DriveEstimate.CalcTb(penetration_m, Sb);
            double Tw = SheetPileQuayWall.Core.FrontWall.DriveEstimate.CalcTw(
                D_mm, t_mm, jointCountPerPile);
            double Tc = Tp + Tb + Tw;
            double Q = SheetPileQuayWall.Core.FrontWall.DriveEstimate.CalcQ(
                site, Tc, sea, obstacle, pileCount);
            if (Q <= 0.0)
            {
                ed.WriteMessage("\nエラー: 1 日当り打設本数が 0 以下になりました。入力条件を確認してください。");
                return;
            }

            int driveDays = (int)System.Math.Ceiling(pileCount / Q);
            var labor = SheetPileQuayWall.Core.FrontWall.DriveEstimate.GetLabor(
                site, record.LengthM, jointCountPerPile > 0, D_mm);

            ed.WriteMessage("\n=== 前壁鋼管矢板 施工歩掛積算 (港湾土木請負工事積算基準 3-4.5) ===");
            ed.WriteMessage("\n--- 入力条件 ---");
            ed.WriteMessage($"\n  外径 D      : {D_mm,6} mm   肉厚 t : {t_mm,4} mm   全長 L : {record.LengthM,6:F1} m");
            ed.WriteMessage($"\n  根入れ長    : {penetration_m,6:F1} m   本数 N : {pileCount,4} 本   施工区分: " +
                $"{(site == SheetPileQuayWall.Core.FrontWall.ConstructionSite.Onshore ? "陸上" : "海上")}");
            ed.WriteMessage($"\n  先端N値     : {nTip,6}      加重平均N値: {nAvg,4}      海象 : " +
                $"{(sea == SheetPileQuayWall.Core.FrontWall.SeaCondition.Severe ? "悪い" : "普通")}");
            ed.WriteMessage($"\n  障害        : {(obstacle == SheetPileQuayWall.Core.FrontWall.ObstacleStatus.Exists ? "あり" : "なし")}" +
                $"        継手個所数 : {jointCountPerPile}");
            ed.WriteMessage("\n--- 鋼材質量 ---");
            ed.WriteMessage($"\n  単位重量 W  : {W_kgPerM,10:F2} kg/m");
            ed.WriteMessage($"\n  1本当り質量 : {mass1_kg,10:F0} kg/本");
            ed.WriteMessage($"\n  合計質量    : {totalMass_t,10:F2} t   ({pileCount}本合計)");
            ed.WriteMessage("\n--- 貫入抵抗値・ハンマ規格 ---");
            ed.WriteMessage($"\n  貫入抵抗値 R: {R,10:F1} kN");
            ed.WriteMessage($"\n  推奨ハンマ  :  {hammer}");

            ed.WriteMessage("\n--- 作業船・機械 (3-4.5-14〜15。杭打機規格・杭打船規格は[推定]、原本確認を推奨) ---");
            if (site == SheetPileQuayWall.Core.FrontWall.ConstructionSite.Onshore)
            {
                string crawlerDriver =
                    SheetPileQuayWall.Core.FrontWall.DriveEquipment.GetCrawlerDriver(hammer);
                ed.WriteMessage(crawlerDriver.Length == 0
                    ? "\n  クローラ式杭打機: 規格表の範囲外です。基準の規格決定図を超えるため別途検討してください。"
                    : $"\n  クローラ式杭打機: {crawlerDriver}");
                if (needCrawlerCrane)
                {
                    ed.WriteMessage(
                        $"\n  クローラクレーン: {SheetPileQuayWall.Core.FrontWall.DriveEquipment.CrawlerCraneSpec}" +
                        " (小運搬用、必要に応じて計上)");
                }
            }
            else
            {
                string pileDriverVessel =
                    SheetPileQuayWall.Core.FrontWall.DriveEquipment.GetPileDriverVessel(hammer);
                ed.WriteMessage(pileDriverVessel.Length == 0
                    ? "\n  杭打船          : 規格表の範囲外です。基準の規格決定図を超えるため別途検討してください。"
                    : $"\n  杭打船          : {pileDriverVessel}");

                var (barge, tug) = SheetPileQuayWall.Core.FrontWall.VibroEstimate.GetBargeAndTug(
                    record.LengthM);
                if (barge.Length == 0)
                {
                    ed.WriteMessage(
                        $"\n  台船・引船      : 全長 {record.LengthM:F1}m は規格表の範囲" +
                        $"(44m未満)を超えています。別途選定してください。");
                }
                else
                {
                    ed.WriteMessage($"\n  台船            : {barge} × 1");
                    // 引船は「現場条件による追加船団」— 杭打船の移動が必要な場合のみ計上
                    // (3-4.5-15 注1)。規格は台船とペアで決まる。
                    if (needTugBoat)
                    {
                        ed.WriteMessage($"\n  引船            : {tug} × 1 (杭打船の移動用に計上)");
                    }
                    else
                    {
                        ed.WriteMessage("\n  引船            : 計上しない (杭打船の移動が不要な場合)");
                    }
                }
                ed.WriteMessage(
                    $"\n  揚錨船          : {SheetPileQuayWall.Core.FrontWall.VibroEstimate.AnchorHandlingVesselSpec} × 1");
                if (needDiverVessel)
                {
                    ed.WriteMessage(
                        $"\n  潜水士船        : {SheetPileQuayWall.Core.FrontWall.VibroEstimate.DiverVesselSpec} × 1" +
                        " (必要に応じて計上)");
                }
            }

            ed.WriteMessage("\n--- 施工能力 (打設) ---");
            ed.WriteMessage($"\n  打撃速度 Sb : {Sb,8:F2} m/分");
            ed.WriteMessage($"\n  準備時間 Tp : {Tp,8:F1} 分/本");
            ed.WriteMessage($"\n  打撃時間 Tb : {Tb,8:F1} 分/本");
            ed.WriteMessage($"\n  溶接時間 Tw : {Tw,8:F1} 分/本");
            ed.WriteMessage($"\n  打設時間 Tc : {Tc,8:F1} 分/本");
            ed.WriteMessage($"\n  日当り打設  : {Q,8:F2} 本/日");
            ed.WriteMessage($"\n  打設日数    : {driveDays,8} 日");
            ed.WriteMessage("\n--- 労務編成 (人/日) ---");
            ed.WriteMessage($"\n  世話役 {labor.foreman} / とび工 {labor.rigger} / " +
                $"普通作業員 {labor.laborer} / 溶接工 {labor.welder}");
            ed.WriteMessage("\nSPQW_FRONTWALL_Estimate 完了。");
        }

        // ────────────────────────────────────────────────────────────────────
        // _Create 用の入力。総本数・施工順位の代わりに施設全長と有効幅を尋ね、
        // 本数を切り上げで自動算出する。平面位置は 1 本目(始点)のみピックする。
        // ────────────────────────────────────────────────────────────────────
        private static bool PromptWallRecord(
            Autodesk.AutoCAD.EditorInput.Editor ed,
            SheetPileQuayWall.Plugin.XData.FrontWallRecord record,
            out int pieceCount, out double effectiveWidth_m, out double wallLength_m)
        {
            pieceCount = 0;
            effectiveWidth_m = 0.0;
            wallLength_m = 0.0;

            if (!PromptSpec(ed, record))
            {
                return false;
            }

            if (!SheetPileQuayWall.Plugin.Prompt.TryAskDouble(
                ed, $"\n施設全長 (m, +Y 方向) <{DefaultWallLength_m:F3}>: ",
                DefaultWallLength_m,
                SheetPileQuayWall.Core.FrontWall.WallLayout.WallLength_Min_m,
                SheetPileQuayWall.Core.FrontWall.WallLayout.WallLength_Max_m,
                out wallLength_m))
            {
                return false;
            }

            // 既定値は外径・継手形式から算出した有効幅。Enter で採用すれば
            // タイロッドのピッチ照合 (CrossMemberValidator) を確実に通せる
            double autoWidth_m = SheetPileQuayWall.Core.FrontWall.JointParameters.EffectiveWidth(
                record.OuterDm,
                SheetPileQuayWall.Core.FrontWall.JointParameters.FromCode(record.JointCode));

            if (!SheetPileQuayWall.Plugin.Prompt.TryAskDouble(
                ed, $"\n鋼管矢板 有効幅 B (m, 継手考慮) <{autoWidth_m:F4}>: ",
                autoWidth_m,
                SheetPileQuayWall.Core.FrontWall.WallLayout.Width_Min_m,
                SheetPileQuayWall.Core.FrontWall.WallLayout.Width_Max_m,
                out effectiveWidth_m))
            {
                return false;
            }

            // 入力幅が継手形式からの算出値と食い違う場合は警告のみ(入力値を優先)。
            // タイロッドは EffectiveWidth と照合するため、この状態では
            // SPQW_TIEROD_Create のピッチ照合が通らなくなる
            if (SheetPileQuayWall.Core.FrontWall.WallLayout.WidthDeviation(
                    effectiveWidth_m, record.OuterDm,
                    SheetPileQuayWall.Core.FrontWall.JointParameters.FromCode(record.JointCode))
                > SheetPileQuayWall.Core.FrontWall.WallLayout.Tol_m)
            {
                ed.WriteMessage(
                    $"\n警告: 入力した有効幅 {effectiveWidth_m:F4}m は " +
                    $"D={record.OuterDm * 1000:F0}mm・継手 {record.JointCode} からの算出値 " +
                    $"{autoWidth_m:F4}m と {System.Math.Abs(effectiveWidth_m - autoWidth_m) * 1000:F1}mm " +
                    "違います。入力値で配置しますが、SPQW_TIEROD_Create の矢板ピッチ照合" +
                    $"(算出値 {autoWidth_m:F4}m と一致必須)は通らなくなります。");
            }

            if (!PromptColorAndTip(ed, record))
            {
                return false;
            }

            double planX = record.TipPoint.X;
            double planY = record.TipPoint.Y;
            if (!SheetPileQuayWall.Plugin.Prompt.TryAskPlanPoint(
                ed, "\n始点 (1 本目の杭中心) を指定 (Z は使用せず、標高は入力値による): ",
                out planX, out planY))
            {
                return false;
            }
            record.TipPoint = SheetPileQuayWall.Core.FrontWall.FrontWallPlacement.TipPoint(
                planX, planY, record.TipPoint.Z);

            // ── 整合性チェック(不一致はエラー停止、自動補正しない)────────────
            if (!SheetPileQuayWall.Plugin.Prompt.Report(ed,
                SheetPileQuayWall.Core.FrontWall.WallLayout.Validate(wallLength_m, effectiveWidth_m)))
            {
                return false;
            }

            pieceCount = SheetPileQuayWall.Core.FrontWall.WallLayout.PieceCountFor(
                wallLength_m, effectiveWidth_m);
            return true;
        }

        // ────────────────────────────────────────────────────────────────────
        // 部材 1 本の諸元(_Create の壁一括生成・_Action の単体再生成で共通)。
        // 検証を通ったものだけ record へ書き込む。
        // ────────────────────────────────────────────────────────────────────
        private static bool PromptSpec(
            Autodesk.AutoCAD.EditorInput.Editor ed,
            SheetPileQuayWall.Plugin.XData.FrontWallRecord record)
        {
            // 外径・肉厚は mm 呼称で入力し、直後に m へ変換する(決定7)
            double outerD_m;
            if (!SheetPileQuayWall.Plugin.Prompt.TryAskMillimeters(
                ed, $"\n外径 D (mm) [500〜2000] <{record.OuterDm * 1000:F1}>: ",
                record.OuterDm * 1000.0, 500.0, 2000.0, out outerD_m))
            {
                return false;
            }

            double wallT_m;
            if (!SheetPileQuayWall.Plugin.Prompt.TryAskMillimeters(
                ed, $"\n肉厚 t (mm) [9〜25] <{record.WallTm * 1000:F1}>: ",
                record.WallTm * 1000.0, 9.0, 25.0, out wallT_m))
            {
                return false;
            }

            double length_m;
            if (!SheetPileQuayWall.Plugin.Prompt.TryAskDouble(
                ed, $"\n全長 L (m) <{record.LengthM:F1}>: ",
                record.LengthM, 1.0, 80.0, out length_m))
            {
                return false;
            }

            string jointCode;
            if (!SheetPileQuayWall.Plugin.Prompt.TryAskKeyword(
                ed, $"\n継手形式 [LT65/LT75/LT100/PP/PT] <{record.JointCode}>: ",
                new string[] { "LT65", "LT75", "LT100", "PP", "PT" },
                record.JointCode, out jointCode))
            {
                return false;
            }

            string grade;
            if (!SheetPileQuayWall.Plugin.Prompt.TryAskKeyword(
                ed, $"\n鋼種 [SKY400/SKY490] <{record.Grade}>: ",
                new string[] { "SKY400", "SKY490" }, record.Grade, out grade))
            {
                return false;
            }

            double inclDeg;
            if (!SheetPileQuayWall.Plugin.Prompt.TryAskDouble(
                ed, $"\n傾斜角 θ (度, Y軸周り, 0=直杭) <{record.InclDeg:F1}>: ",
                record.InclDeg,
                SheetPileQuayWall.Core.FrontWall.FrontWallPlacement.Incl_Min_Deg,
                SheetPileQuayWall.Core.FrontWall.FrontWallPlacement.Incl_Max_Deg,
                out inclDeg))
            {
                return false;
            }

            if (!SheetPileQuayWall.Plugin.Prompt.Report(ed,
                SheetPileQuayWall.Core.FrontWall.InputValidator.ValidateD(outerD_m)))
            {
                return false;
            }
            if (!SheetPileQuayWall.Plugin.Prompt.Report(ed,
                SheetPileQuayWall.Core.FrontWall.InputValidator.ValidateT(wallT_m, outerD_m)))
            {
                return false;
            }
            if (!SheetPileQuayWall.Plugin.Prompt.Report(ed,
                SheetPileQuayWall.Core.FrontWall.InputValidator.ValidateL(length_m)))
            {
                return false;
            }

            record.OuterDm = outerD_m;
            record.WallTm = wallT_m;
            record.LengthM = length_m;
            record.JointCode = jointCode;
            record.Grade = grade;
            record.InclDeg = inclDeg;
            return true;
        }

        // 色と杭先端標高(_Create / _Action で共通。標高は record.TipPoint へ反映する)
        private static bool PromptColorAndTip(
            Autodesk.AutoCAD.EditorInput.Editor ed,
            SheetPileQuayWall.Plugin.XData.FrontWallRecord record)
        {
            int colorIdx;
            if (!SheetPileQuayWall.Plugin.Prompt.TryAskInt(
                ed, $"\n本管の色 (ACI 1〜255) <{record.ColorIdx}>: ",
                record.ColorIdx, 1, 255, out colorIdx))
            {
                return false;
            }

            double tipElev_m;
            if (!SheetPileQuayWall.Plugin.Prompt.TryAskDouble(
                ed, $"\n杭先端標高 Z_tip (m, D.L. 基準) <{record.TipPoint.Z:F3}>: ",
                record.TipPoint.Z,
                SheetPileQuayWall.Core.FrontWall.FrontWallPlacement.TipElev_Min_m,
                SheetPileQuayWall.Core.FrontWall.FrontWallPlacement.TipElev_Max_m,
                out tipElev_m))
            {
                return false;
            }

            record.ColorIdx = colorIdx;
            record.TipPoint = SheetPileQuayWall.Core.FrontWall.FrontWallPlacement.TipPoint(
                record.TipPoint.X, record.TipPoint.Y, tipElev_m);
            return true;
        }

        // ────────────────────────────────────────────────────────────────────
        // _Action 用の入力(Enter で record の現在値を採用)。
        // 単体再生成のため総本数・施工順位を明示入力し、平面位置は保持する。
        // ────────────────────────────────────────────────────────────────────
        private static bool PromptRecord(
            Autodesk.AutoCAD.EditorInput.Editor ed,
            SheetPileQuayWall.Plugin.XData.FrontWallRecord record,
            bool askPlanPoint)
        {
            if (!PromptSpec(ed, record))
            {
                return false;
            }

            int pieceCount;
            if (!SheetPileQuayWall.Plugin.Prompt.TryAskInt(
                ed, $"\n総本数 (壁全体、本) <{record.PieceCount}>: ",
                record.PieceCount, 1,
                SheetPileQuayWall.Core.FrontWall.PieceAssignment.PieceCount_Max,
                out pieceCount))
            {
                return false;
            }

            int pieceIndex;
            if (!SheetPileQuayWall.Plugin.Prompt.TryAskInt(
                ed, $"\n施工順位 (1〜{pieceCount} 本目、+Y 方向へ打設) <{record.PieceIndex}>: ",
                record.PieceIndex > pieceCount ? 1 : record.PieceIndex, 1, pieceCount,
                out pieceIndex))
            {
                return false;
            }

            if (!PromptColorAndTip(ed, record))
            {
                return false;
            }

            if (askPlanPoint)
            {
                // 平面位置のみピックする。クリック点の Z は使わない(§2.2)
                double planX, planY;
                if (!SheetPileQuayWall.Plugin.Prompt.TryAskPlanPoint(
                    ed, "\n平面位置 (杭中心) を指定 (Z は使用せず、標高は入力値による): ",
                    out planX, out planY))
                {
                    return false;
                }
                record.TipPoint = SheetPileQuayWall.Core.FrontWall.FrontWallPlacement.TipPoint(
                    planX, planY, record.TipPoint.Z);
            }

            // ── 整合性チェック(不一致はエラー停止、自動補正しない)────────────
            if (!SheetPileQuayWall.Plugin.Prompt.Report(ed,
                SheetPileQuayWall.Core.FrontWall.PieceAssignment.Validate(pieceIndex, pieceCount)))
            {
                return false;
            }

            record.PieceCount = pieceCount;
            record.PieceIndex = pieceIndex;
            return true;
        }

        // ────────────────────────────────────────────────────────────────────
        // ソリッド生成 → モデル空間追加。replaceId 指定時は旧ソリッドを消去する
        // ────────────────────────────────────────────────────────────────────
        private static void BuildAndAppend(
            Autodesk.AutoCAD.DatabaseServices.Database db,
            Autodesk.AutoCAD.EditorInput.Editor ed,
            SheetPileQuayWall.Plugin.XData.FrontWallRecord record,
            Autodesk.AutoCAD.DatabaseServices.ObjectId? replaceId)
        {
            using (Autodesk.AutoCAD.DatabaseServices.Transaction tr =
                db.TransactionManager.StartTransaction())
            {
                SheetPileQuayWall.Plugin.XData.XDataStore.EnsureRegApp(
                    tr, db, SheetPileQuayWall.Plugin.XData.FrontWallRecord.RegAppName);
                SheetPileQuayWall.Plugin.DrawingHelper.EnsureLayer(db, tr, LayerName, 8);

                if (replaceId.HasValue)
                {
                    SheetPileQuayWall.Plugin.DrawingHelper.EraseSolid(tr, replaceId.Value);
                }

                Autodesk.AutoCAD.DatabaseServices.Solid3d solid = BuildSolid(record);

                SheetPileQuayWall.Plugin.DrawingHelper.AppendSolid(
                    db, tr, solid, LayerName, record.ColorIdx, record.ToBuffer());

                tr.Commit();
            }
        }

        // 本管 + 継手を 1 ソリッドに集約し、傾斜・挿入点変換まで行う。
        internal static Autodesk.AutoCAD.DatabaseServices.Solid3d BuildSolid(
            SheetPileQuayWall.Plugin.XData.FrontWallRecord record)
        {
            double outerR = record.OuterDm / 2.0;
            double innerR = (record.OuterDm - 2.0 * record.WallTm) / 2.0;

            // 本管(Z=0 が杭先端、Z=+L が杭頭)
            Autodesk.AutoCAD.DatabaseServices.Solid3d pipe =
                SheetPileQuayWall.Plugin.SolidBuilder.HollowCylinder(
                    outerR, innerR, record.LengthM);

            // 継手の要否は施工順位から決まる(006 由来。007 は常に両側だった)
            SheetPileQuayWall.Core.FrontWall.JointType jointType =
                SheetPileQuayWall.Core.FrontWall.JointParameters.FromCode(record.JointCode);
            SheetPileQuayWall.Core.FrontWall.PieceJoints joints =
                SheetPileQuayWall.Core.FrontWall.PieceAssignment.Assign(
                    record.PieceIndex, record.PieceCount);

            if (joints.HasTrailingJoint)
            {
                // +Y 側(後続の矢板を受ける)
                pipe.BooleanOperation(
                    Autodesk.AutoCAD.DatabaseServices.BooleanOperationType.BoolUnite,
                    SheetPileQuayWall.Plugin.SolidBuilder.JointMember(
                        SheetPileQuayWall.Core.FrontWall.JointShapes.LoopsA(jointType),
                        outerR, System.Math.PI / 2.0, record.LengthM));
            }
            if (joints.HasLeadingJoint)
            {
                // −Y 側(先行して打設済みの矢板と嵌合)
                pipe.BooleanOperation(
                    Autodesk.AutoCAD.DatabaseServices.BooleanOperationType.BoolUnite,
                    SheetPileQuayWall.Plugin.SolidBuilder.JointMember(
                        SheetPileQuayWall.Core.FrontWall.JointShapes.LoopsB(jointType),
                        outerR, -System.Math.PI / 2.0, record.LengthM));
            }

            // 配置: 回転(Y軸まわり θ) → 杭先端への平行移動
            // Core の PileGeometry.LocalToWorld と同じ変換でなければならない
            if (System.Math.Abs(record.InclDeg) > 0.001)
            {
                pipe.TransformBy(
                    Autodesk.AutoCAD.Geometry.Matrix3d.Rotation(
                        record.InclDeg * System.Math.PI / 180.0,
                        Autodesk.AutoCAD.Geometry.Vector3d.YAxis,
                        Autodesk.AutoCAD.Geometry.Point3d.Origin));
            }

            pipe.TransformBy(
                Autodesk.AutoCAD.Geometry.Matrix3d.Displacement(
                    new Autodesk.AutoCAD.Geometry.Vector3d(
                        record.TipPoint.X, record.TipPoint.Y, record.TipPoint.Z)));

            return pipe;
        }

        private static SheetPileQuayWall.Plugin.XData.FrontWallRecord? ReadRecord(
            Autodesk.AutoCAD.DatabaseServices.Database db,
            Autodesk.AutoCAD.DatabaseServices.ObjectId id)
        {
            SheetPileQuayWall.Plugin.XData.FrontWallRecord? record = null;
            using (Autodesk.AutoCAD.DatabaseServices.Transaction tr =
                db.TransactionManager.StartTransaction())
            {
                Autodesk.AutoCAD.DatabaseServices.Solid3d? solid =
                    tr.GetObject(id, Autodesk.AutoCAD.DatabaseServices.OpenMode.ForRead)
                    as Autodesk.AutoCAD.DatabaseServices.Solid3d;
                if (solid != null)
                {
                    record = SheetPileQuayWall.Plugin.XData.FrontWallRecord.Read(solid);
                }
                tr.Commit();
            }
            return record;
        }

        // 壁一括生成の結果(本数・実延長・始点終点)。1 本分の諸元は PrintSummary が出す。
        private static void PrintWallSummary(
            Autodesk.AutoCAD.EditorInput.Editor ed,
            SheetPileQuayWall.Plugin.XData.FrontWallRecord record,
            int pieceCount, double effectiveWidth_m, double wallLength_m, double startY_m)
        {
            double actual_m = SheetPileQuayWall.Core.FrontWall.WallLayout.ActualLength(
                pieceCount, effectiveWidth_m);
            double lastY_m = SheetPileQuayWall.Core.FrontWall.WallLayout.PositionY(
                startY_m, pieceCount, effectiveWidth_m);

            ed.WriteMessage("\n=== 壁一括生成 ===");
            ed.WriteMessage($"\n  施設全長 (入力)  : {wallLength_m,10:F3} m");
            ed.WriteMessage($"\n  有効幅 B         : {effectiveWidth_m,10:F4} m");
            ed.WriteMessage($"\n  本数 (切り上げ)  : {pieceCount,10} 本");
            ed.WriteMessage($"\n  実延長 (本数×B)  : {actual_m,10:F3} m " +
                $"(施設全長との差 {actual_m - wallLength_m:+0.000;-0.000} m)");
            ed.WriteMessage($"\n  始点 Y (1 本目)  : {startY_m,10:F4} m");
            ed.WriteMessage($"\n  終点 Y ({pieceCount} 本目) : {lastY_m,10:F4} m");

            PrintSummary(ed, record);
        }

        private static void PrintSummary(
            Autodesk.AutoCAD.EditorInput.Editor ed,
            SheetPileQuayWall.Plugin.XData.FrontWallRecord record)
        {
            double D = record.OuterDm;
            double t = record.WallTm;
            double d = D - 2.0 * t;

            SheetPileQuayWall.Core.FrontWall.JointType jointType =
                SheetPileQuayWall.Core.FrontWall.JointParameters.FromCode(record.JointCode);
            double B = SheetPileQuayWall.Core.FrontWall.JointParameters.EffectiveWidth(D, jointType);
            SheetPileQuayWall.Core.FrontWall.PieceJoints joints =
                SheetPileQuayWall.Core.FrontWall.PieceAssignment.Assign(
                    record.PieceIndex, record.PieceCount);

            double headElev = SheetPileQuayWall.Core.PileGeometry.HeadElevation(
                record.TipPoint.Z, record.LengthM, record.InclDeg);

            ed.WriteMessage("\n=== 前壁鋼管矢板 断面性能 (JIS A 5530 / 日本製鉄 K011) ===");
            ed.WriteMessage($"\n  外径 D          : {D * 1000,10:F1} mm");
            ed.WriteMessage($"\n  内径 d          : {d * 1000,10:F1} mm");
            ed.WriteMessage($"\n  肉厚 t          : {t * 1000,10:F1} mm");
            ed.WriteMessage($"\n  全長 L          : {record.LengthM,10:F1} m");
            ed.WriteMessage($"\n  継手形式        : {record.JointCode}");
            ed.WriteMessage($"\n  有効幅 B        : {B * 1000,10:F1} mm");
            ed.WriteMessage($"\n  鋼種            : {record.Grade}");
            ed.WriteMessage($"\n  傾斜角 θ        : {record.InclDeg,10:F1} deg");
            ed.WriteMessage($"\n  施工順位        : {record.PieceIndex,10} / {record.PieceCount} 本");
            ed.WriteMessage($"\n  継手            : " +
                (joints.HasLeadingJoint || joints.HasTrailingJoint
                    ? (joints.HasLeadingJoint ? "−Y側あり" : "") +
                      (joints.HasLeadingJoint && joints.HasTrailingJoint ? " / " : "") +
                      (joints.HasTrailingJoint ? "+Y側あり" : "")
                    : "なし(単独)"));
            ed.WriteMessage($"\n  杭先端標高      : {record.TipPoint.Z,10:F3} m (D.L.)");
            ed.WriteMessage($"\n  杭頭標高        : {headElev,10:F3} m (D.L.)");
            ed.WriteMessage($"\n  平面位置 (X, Y) : {record.TipPoint.X,10:F3}, {record.TipPoint.Y:F3} m");
            ed.WriteMessage($"\n  断面積 A        : {SheetPileQuayWall.Core.FrontWall.SectionProperties.CalcA(D, t),10:F2} cm2");
            ed.WriteMessage($"\n  単位重量 W      : {SheetPileQuayWall.Core.FrontWall.SectionProperties.CalcW(D, t),10:F2} kg/m");
            ed.WriteMessage($"\n  全重量          : {SheetPileQuayWall.Core.FrontWall.SectionProperties.CalcW(D, t) * record.LengthM,10:F0} kg");
            ed.WriteMessage($"\n  I               : {SheetPileQuayWall.Core.FrontWall.SectionProperties.CalcI(D, t),10:F0} cm4");
            ed.WriteMessage($"\n  Z               : {SheetPileQuayWall.Core.FrontWall.SectionProperties.CalcZ(D, t),10:F0} cm3");
            ed.WriteMessage($"\n  i (断面2次半径) : {SheetPileQuayWall.Core.FrontWall.SectionProperties.CalcRadius(D, t),10:F2} cm");
        }
    }
}
