// === 参照DLLバージョン: 未検証 ===
// AcCoreMgd.dll : 未検証 (期待値 25.x.x.x)
// AcDbMgd.dll   : 未検証 (期待値 25.x.x.x)
// AcMgd.dll     : 未検証 (期待値 25.x.x.x)
// 検証日: 未実施 / 検証コマンド: scripts/verify-dll-versions.ps1 → exit 2 (未検出)
// リスク: TypeLoadException / MissingMethodException が実行時に発生する可能性がある。
//
// タイロッドのコマンド(SPQW_TIEROD_Create / _Action / _Query / _Color)
// 移植元: 008 Commands / ParameterPrompt / TieRodBuilder。
//
// 008 との違い(決定8):
//   - 海側鋼管矢板中心の目視クリックを廃止し、前壁ソリッドの選択に変更した
//   - 海側取付点の X は TieRodPlacement.SeaAttachmentX が前壁の θ・Z_tip から自動計算する
//     (008 は base_x を保存していたが、009 は前壁 Handle を保存して毎回計算し直す)
//   - ユーザーが指定する平面位置は施設延長方向 Y のみ
//   - 前壁・控え杭との整合を CrossMemberValidator で確認する(フェーズ3)

namespace SheetPileQuayWall.Plugin.Commands
{
    public static class TieRodCommands
    {
        public const string LayerName = "タイ材";

        // ════════════════════════════════════════════════════════════════════
        // SPQW_TIEROD_Create: 前壁選択 → 入力 → 組数分の Solid3d 生成
        // ════════════════════════════════════════════════════════════════════
        [Autodesk.AutoCAD.Runtime.CommandMethod("SPQW_TIEROD_Create")]
        public static void Create()
        {
            Autodesk.AutoCAD.ApplicationServices.Document doc =
                Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager
                    .MdiActiveDocument;
            Autodesk.AutoCAD.DatabaseServices.Database db = doc.Database;
            Autodesk.AutoCAD.EditorInput.Editor ed = doc.Editor;

            string frontHandle;
            SheetPileQuayWall.Plugin.XData.FrontWallRecord? front =
                SheetPileQuayWall.Plugin.DrawingHelper.SelectFrontWall(
                    ed, db, "\n基準とする前壁鋼管矢板 (SPQW_FRONTWALL) を選択: ",
                    out frontHandle);
            if (front == null)
            {
                return;
            }

            SheetPileQuayWall.Core.TieRod.TieRodParameters p =
                new SheetPileQuayWall.Core.TieRod.TieRodParameters();

            // 前壁から決まる値を既定値として提示する(整合チェックを通しやすくするため)
            p.PileDiameter = front.OuterDm;
            p.PilePitch = SheetPileQuayWall.Core.FrontWall.JointParameters.EffectiveWidth(
                front.OuterDm,
                SheetPileQuayWall.Core.FrontWall.JointParameters.FromCode(front.JointCode));

            double positionY;
            if (!PromptParameters(ed, p, front, out positionY))
            {
                return;
            }

            SheetPileQuayWall.Core.TieRod.TieRodResult r;
            if (!TryCompute(ed, p, out r))
            {
                return;
            }

            double baseX = SheetPileQuayWall.Core.TieRod.TieRodPlacement.SeaAttachmentX(
                front.ToRef(), p.TieElevation);

            using (Autodesk.AutoCAD.DatabaseServices.Transaction tr =
                db.TransactionManager.StartTransaction())
            {
                SheetPileQuayWall.Plugin.XData.XDataStore.EnsureRegApp(
                    tr, db, SheetPileQuayWall.Plugin.XData.TieRodRecord.RegAppName);
                SheetPileQuayWall.Plugin.DrawingHelper.EnsureLayer(
                    db, tr, LayerName, p.LayerColor);

                for (int i = 0; i < p.TieCount; i++)
                {
                    double rodY = positionY + r.RodPositionsY[i];

                    SheetPileQuayWall.Plugin.XData.TieRodRecord record =
                        new SheetPileQuayWall.Plugin.XData.TieRodRecord();
                    record.Parameters = p;
                    record.FrontHandle = frontHandle;
                    record.PositionY = rodY;
                    record.RodIndex = i;

                    SheetPileQuayWall.Plugin.DrawingHelper.AppendSolid(
                        db, tr, BuildSolid(r, baseX, rodY),
                        LayerName, p.LayerColor, record.ToBuffer());
                }

                tr.Commit();
            }

            PrintSummary(ed, p, r, baseX, front);
            ed.WriteMessage(
                $"\n{p.TieCount} 組を生成し、パラメータを XData (RegApp: " +
                $"{SheetPileQuayWall.Plugin.XData.TieRodRecord.RegAppName}) に保存しました。");
            ed.WriteMessage("\nSPQW_TIEROD_Create 完了。");
        }

        // ════════════════════════════════════════════════════════════════════
        // SPQW_TIEROD_Action: 選択 1 本を、前壁の現在の θ・Z_tip に基づき再生成
        // ════════════════════════════════════════════════════════════════════
        [Autodesk.AutoCAD.Runtime.CommandMethod("SPQW_TIEROD_Action")]
        public static void Action()
        {
            Autodesk.AutoCAD.ApplicationServices.Document doc =
                Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager
                    .MdiActiveDocument;
            Autodesk.AutoCAD.DatabaseServices.Database db = doc.Database;
            Autodesk.AutoCAD.EditorInput.Editor ed = doc.Editor;

            Autodesk.AutoCAD.EditorInput.PromptEntityResult res =
                SheetPileQuayWall.Plugin.DrawingHelper.SelectSolid(
                    ed, "\n再生成するタイロッド (Solid3d) を選択: ");
            if (res.Status != Autodesk.AutoCAD.EditorInput.PromptStatus.OK)
            {
                return;
            }

            SheetPileQuayWall.Plugin.XData.TieRodRecord? stored = ReadRecord(db, res.ObjectId);
            if (stored == null)
            {
                ed.WriteMessage(
                    $"\nエラー: XData (RegApp: " +
                    $"{SheetPileQuayWall.Plugin.XData.TieRodRecord.RegAppName}) が見つかりません。");
                return;
            }

            // 前壁参照を解決する。消失していれば再選択を求める(006 ANCHORPILE_Action と同じ)
            string frontHandle = stored.FrontHandle;
            SheetPileQuayWall.Plugin.XData.FrontWallRecord? front =
                SheetPileQuayWall.Plugin.DrawingHelper.TryResolveFrontWall(db, frontHandle);
            if (front == null)
            {
                ed.WriteMessage($"\n保存された前壁 (Handle: {stored.FrontHandle}) が見つかりません。");
                front = SheetPileQuayWall.Plugin.DrawingHelper.SelectFrontWall(
                    ed, db, "\n基準とする前壁鋼管矢板 (SPQW_FRONTWALL) を再選択: ",
                    out frontHandle);
                if (front == null)
                {
                    return;
                }
            }

            SheetPileQuayWall.Core.TieRod.TieRodParameters p = stored.Parameters;
            double positionY;
            if (!PromptParameters(ed, p, front, out positionY, defaultY: stored.PositionY))
            {
                return;
            }

            SheetPileQuayWall.Core.TieRod.TieRodResult r;
            if (!TryCompute(ed, p, out r))
            {
                return;
            }

            double baseX = SheetPileQuayWall.Core.TieRod.TieRodPlacement.SeaAttachmentX(
                front.ToRef(), p.TieElevation);

            using (Autodesk.AutoCAD.DatabaseServices.Transaction tr =
                db.TransactionManager.StartTransaction())
            {
                SheetPileQuayWall.Plugin.XData.XDataStore.EnsureRegApp(
                    tr, db, SheetPileQuayWall.Plugin.XData.TieRodRecord.RegAppName);
                SheetPileQuayWall.Plugin.DrawingHelper.EnsureLayer(
                    db, tr, LayerName, p.LayerColor);
                SheetPileQuayWall.Plugin.DrawingHelper.EraseSolid(tr, res.ObjectId);

                SheetPileQuayWall.Plugin.XData.TieRodRecord record =
                    new SheetPileQuayWall.Plugin.XData.TieRodRecord();
                record.Parameters = p;
                record.FrontHandle = frontHandle;
                record.PositionY = positionY;
                record.RodIndex = stored.RodIndex;

                SheetPileQuayWall.Plugin.DrawingHelper.AppendSolid(
                    db, tr, BuildSolid(r, baseX, positionY),
                    LayerName, p.LayerColor, record.ToBuffer());

                tr.Commit();
            }

            PrintSummary(ed, p, r, baseX, front);
            ed.WriteMessage("\nSPQW_TIEROD_Action 完了(前壁基準で再計算しました)。");
        }

        // ════════════════════════════════════════════════════════════════════
        // SPQW_TIEROD_Query: 諸元・張力照査・受杭数量を出力
        // ════════════════════════════════════════════════════════════════════
        [Autodesk.AutoCAD.Runtime.CommandMethod("SPQW_TIEROD_Query")]
        public static void Query()
        {
            Autodesk.AutoCAD.ApplicationServices.Document doc =
                Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager
                    .MdiActiveDocument;
            Autodesk.AutoCAD.DatabaseServices.Database db = doc.Database;
            Autodesk.AutoCAD.EditorInput.Editor ed = doc.Editor;

            Autodesk.AutoCAD.EditorInput.PromptEntityResult res =
                SheetPileQuayWall.Plugin.DrawingHelper.SelectSolid(
                    ed, "\n諸元を表示するタイロッド (Solid3d) を選択: ");
            if (res.Status != Autodesk.AutoCAD.EditorInput.PromptStatus.OK)
            {
                return;
            }

            SheetPileQuayWall.Plugin.XData.TieRodRecord? record = ReadRecord(db, res.ObjectId);
            if (record == null)
            {
                ed.WriteMessage("\nエラー: タイロッドの XData が見つかりません。");
                return;
            }

            SheetPileQuayWall.Core.TieRod.TieRodResult r;
            if (!TryCompute(ed, record.Parameters, out r))
            {
                return;
            }

            SheetPileQuayWall.Plugin.XData.FrontWallRecord? front =
                SheetPileQuayWall.Plugin.DrawingHelper.TryResolveFrontWall(
                    db, record.FrontHandle);
            double baseX = front != null
                ? SheetPileQuayWall.Core.TieRod.TieRodPlacement.SeaAttachmentX(
                    front.ToRef(), record.Parameters.TieElevation)
                : double.NaN;

            ed.WriteMessage($"\n=== SPQW_TIEROD_Query (前壁 Handle: {record.FrontHandle}) ===");
            PrintSummary(ed, record.Parameters, r, baseX, front);
            ed.WriteMessage($"\n  この組の Y 座標 : {record.PositionY,10:F3} m (組内 {record.RodIndex} 番)");
            ed.WriteMessage("\nSPQW_TIEROD_Query 完了。");
        }

        // ════════════════════════════════════════════════════════════════════
        // SPQW_TIEROD_Color: 色番号のみ変更する
        // ════════════════════════════════════════════════════════════════════
        [Autodesk.AutoCAD.Runtime.CommandMethod("SPQW_TIEROD_Color")]
        public static void Color()
        {
            Autodesk.AutoCAD.ApplicationServices.Document doc =
                Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager
                    .MdiActiveDocument;
            Autodesk.AutoCAD.DatabaseServices.Database db = doc.Database;
            Autodesk.AutoCAD.EditorInput.Editor ed = doc.Editor;

            Autodesk.AutoCAD.EditorInput.PromptEntityResult res =
                SheetPileQuayWall.Plugin.DrawingHelper.SelectSolid(
                    ed, "\n色を変更するタイロッド (Solid3d) を選択: ");
            if (res.Status != Autodesk.AutoCAD.EditorInput.PromptStatus.OK)
            {
                return;
            }

            SheetPileQuayWall.Plugin.XData.TieRodRecord? record = ReadRecord(db, res.ObjectId);
            if (record == null)
            {
                ed.WriteMessage("\nエラー: タイロッドの XData が見つかりません。");
                return;
            }

            int colorIdx;
            if (!SheetPileQuayWall.Plugin.Prompt.TryAskInt(
                ed, $"\n色 (ACI 1〜255) <{record.Parameters.LayerColor}>: ",
                record.Parameters.LayerColor, 1, 255, out colorIdx))
            {
                return;
            }

            record.Parameters.LayerColor = colorIdx;

            using (Autodesk.AutoCAD.DatabaseServices.Transaction tr =
                db.TransactionManager.StartTransaction())
            {
                Autodesk.AutoCAD.DatabaseServices.Solid3d? solid =
                    tr.GetObject(res.ObjectId,
                        Autodesk.AutoCAD.DatabaseServices.OpenMode.ForWrite)
                    as Autodesk.AutoCAD.DatabaseServices.Solid3d;
                if (solid != null)
                {
                    solid.ColorIndex = colorIdx;
                    solid.XData = record.ToBuffer();
                }
                tr.Commit();
            }

            ed.WriteMessage($"\n色を ACI {colorIdx} に変更しました。SPQW_TIEROD_Color 完了。");
        }

        // ────────────────────────────────────────────────────────────────────
        private static bool PromptParameters(
            Autodesk.AutoCAD.EditorInput.Editor ed,
            SheetPileQuayWall.Core.TieRod.TieRodParameters p,
            SheetPileQuayWall.Plugin.XData.FrontWallRecord front,
            out double positionY,
            double defaultY = 0.0)
        {
            positionY = defaultY;

            double value;
            if (!SheetPileQuayWall.Plugin.Prompt.TryAskDouble(
                ed, $"\nタイロッド径 (m) <{p.RodDiameter:F3}>: ",
                p.RodDiameter, 0.020, 0.100, out value)) return false;
            p.RodDiameter = value;

            if (!SheetPileQuayWall.Plugin.Prompt.TryAskDouble(
                ed, $"\n法線直角方向延長 span (m, 前壁矢板中心〜陸側定着面) <{p.SpanLength:F3}>: ",
                p.SpanLength, 3.0, 40.0, out value)) return false;
            p.SpanLength = value;

            if (!SheetPileQuayWall.Plugin.Prompt.TryAskDouble(
                ed, $"\n海側鋼管矢板径 (m) <{p.PileDiameter:F3}>: ",
                p.PileDiameter, 0.600, 1.600, out value)) return false;
            p.PileDiameter = value;

            if (!SheetPileQuayWall.Plugin.Prompt.TryAskDouble(
                ed, $"\n鋼管矢板ピッチ (m, 前壁の有効幅 B) <{p.PilePitch:F4}>: ",
                p.PilePitch, 0.600, 2.000, out value)) return false;
            p.PilePitch = value;

            if (!SheetPileQuayWall.Plugin.Prompt.TryAskDouble(
                ed, $"\nタイロッド取付間隔 (m, ピッチの整数倍) <{p.TieSpacing:F4}>: ",
                p.TieSpacing, 0.600, 20.000, out value)) return false;
            p.TieSpacing = value;

            int count;
            if (!SheetPileQuayWall.Plugin.Prompt.TryAskInt(
                ed, $"\nタイロッド組数 (組) <{p.TieCount}>: ",
                p.TieCount, 1, 200, out count)) return false;
            p.TieCount = count;

            if (!SheetPileQuayWall.Plugin.Prompt.TryAskDouble(
                ed, $"\nH.W.L. 標高 (m, D.L. 基準) <{p.Hwl:F3}>: ",
                p.Hwl, -5.0, 10.0, out value)) return false;
            p.Hwl = value;

            if (!SheetPileQuayWall.Plugin.Prompt.TryAskDouble(
                ed, $"\nタイロッド軸心標高 (m, D.L. 基準) <{p.TieElevation:F3}>: ",
                p.TieElevation, -5.0, 10.0, out value)) return false;
            p.TieElevation = value;

            if (!SheetPileQuayWall.Plugin.Prompt.TryAskDouble(
                ed, $"\n腹起し溝形鋼高さ h (m) <{p.WalingHeight:F3}>: ",
                p.WalingHeight, 0.001, 2.000, out value)) return false;
            p.WalingHeight = value;

            if (!SheetPileQuayWall.Plugin.Prompt.TryAskDouble(
                ed, $"\n定着プレート厚 t2 (m) <{p.PlateThickness:F3}>: ",
                p.PlateThickness, 0.001, 0.200, out value)) return false;
            p.PlateThickness = value;

            if (!SheetPileQuayWall.Plugin.Prompt.TryAskDouble(
                ed, $"\n定着ワッシャー厚 t1 (m) <{p.WasherThickness:F3}>: ",
                p.WasherThickness, 0.001, 0.200, out value)) return false;
            p.WasherThickness = value;

            if (!SheetPileQuayWall.Plugin.Prompt.TryAskDouble(
                ed, $"\nナット高さ (m) <{p.NutHeight:F3}>: ",
                p.NutHeight, 0.001, 0.300, out value)) return false;
            p.NutHeight = value;

            if (!SheetPileQuayWall.Plugin.Prompt.TryAskDouble(
                ed, $"\n調節長 (m) <{p.AdjustLength:F3}>: ",
                p.AdjustLength, 0.001, 0.500, out value)) return false;
            p.AdjustLength = value;

            if (!SheetPileQuayWall.Plugin.Prompt.TryAskDouble(
                ed, $"\n取付点反力 Ap (kN/m、0 で張力照査なし) <{p.AnchorReaction:F1}>: ",
                p.AnchorReaction, 0.0, 10000.0, out value)) return false;
            p.AnchorReaction = value;

            int colorIdx;
            if (!SheetPileQuayWall.Plugin.Prompt.TryAskInt(
                ed, $"\n色 (ACI 1〜255) <{p.LayerColor}>: ",
                p.LayerColor, 1, 255, out colorIdx)) return false;
            p.LayerColor = colorIdx;

            // 平面位置は施設延長方向 Y のみ。X は前壁から自動計算する(決定8)
            double pickedX, pickedY;
            if (!SheetPileQuayWall.Plugin.Prompt.TryAskPlanPoint(
                ed, "\n1 組目の位置を指定 (Y のみ使用。X は前壁から自動計算): ",
                out pickedX, out pickedY))
            {
                return false;
            }
            positionY = pickedY;

            // ── 部材間整合チェック(前壁 ⟺ タイロッド、フェーズ3)───────────
            SheetPileQuayWall.Core.FrontWallRef frontRef = front.ToRef();
            if (!SheetPileQuayWall.Plugin.Prompt.Report(ed,
                SheetPileQuayWall.Core.CrossMemberValidator.ValidatePileDiameter(frontRef, p)))
            {
                return false;
            }
            if (!SheetPileQuayWall.Plugin.Prompt.Report(ed,
                SheetPileQuayWall.Core.CrossMemberValidator.ValidatePilePitch(frontRef, p)))
            {
                return false;
            }

            return true;
        }

        // 008 の計算層は違反時に例外を投げる。ここで捕捉してエラー停止に変換する。
        private static bool TryCompute(
            Autodesk.AutoCAD.EditorInput.Editor ed,
            SheetPileQuayWall.Core.TieRod.TieRodParameters p,
            out SheetPileQuayWall.Core.TieRod.TieRodResult result)
        {
            try
            {
                result = SheetPileQuayWall.Core.TieRod.TieRodCalculator.Compute(p);
                return true;
            }
            catch (System.ArgumentException ex)
            {
                ed.WriteMessage($"\n{ex.Message}\n生成を中止しました。");
                result = new SheetPileQuayWall.Core.TieRod.TieRodResult();
                return false;
            }
        }

        // 軸線に沿った円柱。CreateFrustum は Z 軸方向に作るため Y 軸まわりに倒す。
        private static Autodesk.AutoCAD.DatabaseServices.Solid3d BuildSolid(
            SheetPileQuayWall.Core.TieRod.TieRodResult r, double baseX, double rodY)
        {
            double radius = r.NominalDiameter / 2.0;

            Autodesk.AutoCAD.DatabaseServices.Solid3d solid =
                new Autodesk.AutoCAD.DatabaseServices.Solid3d();
            solid.CreateFrustum(r.TotalLength, radius, radius, radius);

            double midX = baseX + (r.SeaEndX + r.LandEndX) / 2.0;

            Autodesk.AutoCAD.Geometry.Matrix3d rotation =
                Autodesk.AutoCAD.Geometry.Matrix3d.Rotation(
                    System.Math.PI / 2.0,
                    Autodesk.AutoCAD.Geometry.Vector3d.YAxis,
                    Autodesk.AutoCAD.Geometry.Point3d.Origin);
            Autodesk.AutoCAD.Geometry.Matrix3d displacement =
                Autodesk.AutoCAD.Geometry.Matrix3d.Displacement(
                    new Autodesk.AutoCAD.Geometry.Vector3d(midX, rodY, r.AxisZ));

            solid.TransformBy(displacement * rotation);
            return solid;
        }

        private static SheetPileQuayWall.Plugin.XData.TieRodRecord? ReadRecord(
            Autodesk.AutoCAD.DatabaseServices.Database db,
            Autodesk.AutoCAD.DatabaseServices.ObjectId id)
        {
            SheetPileQuayWall.Plugin.XData.TieRodRecord? record = null;
            using (Autodesk.AutoCAD.DatabaseServices.Transaction tr =
                db.TransactionManager.StartTransaction())
            {
                Autodesk.AutoCAD.DatabaseServices.Solid3d? solid =
                    tr.GetObject(id, Autodesk.AutoCAD.DatabaseServices.OpenMode.ForRead)
                    as Autodesk.AutoCAD.DatabaseServices.Solid3d;
                if (solid != null)
                {
                    record = SheetPileQuayWall.Plugin.XData.TieRodRecord.Read(solid);
                }
                tr.Commit();
            }
            return record;
        }

        private static void PrintSummary(
            Autodesk.AutoCAD.EditorInput.Editor ed,
            SheetPileQuayWall.Core.TieRod.TieRodParameters p,
            SheetPileQuayWall.Core.TieRod.TieRodResult r,
            double baseX,
            SheetPileQuayWall.Plugin.XData.FrontWallRecord? front)
        {
            ed.WriteMessage("\n=== タイロッド 諸元 (港湾土木請負工事積算基準 3-4.5) ===");
            ed.WriteMessage($"\n  呼び径          : {r.NominalDiameter * 1000,10:F1} mm");
            ed.WriteMessage($"\n  全長            : {r.TotalLength,10:F3} m");
            ed.WriteMessage($"\n  軸心標高        : {r.AxisZ,10:F3} m (D.L.)");
            ed.WriteMessage($"\n  組数            : {p.TieCount,10} 組");
            ed.WriteMessage($"\n  取付間隔        : {p.TieSpacing,10:F4} m");

            if (double.IsNaN(baseX))
            {
                ed.WriteMessage("\n  海側取付 X      :      前壁が見つからないため未算出");
            }
            else
            {
                ed.WriteMessage($"\n  海側取付 X      : {baseX,10:F3} m " +
                    (front != null && System.Math.Abs(front.InclDeg) > 0.001
                        ? $"(前壁 θ={front.InclDeg:F1}° を補正済み)"
                        : "(前壁は鉛直)"));
                ed.WriteMessage($"\n  海側端 X        : {baseX + r.SeaEndX,10:F3} m");
                ed.WriteMessage($"\n  陸側端 X        : {baseX + r.LandEndX,10:F3} m");
            }

            ed.WriteMessage($"\n  断面積          : {r.SectionArea * 1.0e4,10:F2} cm2");
            ed.WriteMessage($"\n  1組あたり質量   : {r.RodMass,10:F1} kg");
            ed.WriteMessage($"\n  全組質量        : {r.TotalRodMass,10:F1} kg");
            ed.WriteMessage($"\n  本体本数        : {r.SegmentCount,10} 本/組");
            ed.WriteMessage($"\n  ターンバックル  : {r.TurnbuckleCount,10} 個/組");
            ed.WriteMessage($"\n  リングジョイント: {r.RingJointCount,10} 個/組");
            ed.WriteMessage($"\n  受杭 (1本あたり): {r.SupportPileCount,10} ヶ所");
            ed.WriteMessage($"\n  受杭合計        : {r.TotalSupportPileCount,10} ヶ所" +
                $" (受杭対象 {r.SupportedRodCount} 組)");
            ed.WriteMessage($"\n  許容張力        : {r.AllowableTension,10:F1} kN");

            if (r.TensionChecked)
            {
                ed.WriteMessage($"\n  設計張力        : {r.DesignTension,10:F1} kN");
                ed.WriteMessage($"\n  張力照査        : {r.TensionRatio,10:F3} " +
                    (r.TensionOk ? "(OK)" : "(NG — 許容張力超過)"));
            }
            else
            {
                ed.WriteMessage("\n  張力照査        :      未実施 (取付点反力 Ap = 0)");
            }

            if (r.BeyondCatalogStandard)
            {
                ed.WriteMessage("\n  注意: 本体本数がカタログの標準構成図 (2〜4 本継ぎ) の範囲外です。");
            }
        }
    }
}
