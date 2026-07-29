// === 参照DLLバージョン: 未検証 ===
// AcCoreMgd.dll : 未検証 (期待値 25.x.x.x)
// AcDbMgd.dll   : 未検証 (期待値 25.x.x.x)
// AcMgd.dll     : 未検証 (期待値 25.x.x.x)
// 検証日: 未実施 / 検証コマンド: scripts/verify-dll-versions.ps1 → exit 2 (未検出)
// リスク: TypeLoadException / MissingMethodException が実行時に発生する可能性がある。
//
// 控え杭のコマンド(SPQW_ANCHORPILE_Create / _Action / _Query)
// 移植元: 006@6d6d8cf ANCHORPILE_Create / _Action / _Query。
//
// 位置は常に「前壁 + span」から導出する(移植元と同じ)。したがって MOVE しても
// _Action で整列位置へ戻る。整列計算・整合性チェックは Core の AnchorAlignment。

namespace SheetPileQuayWall.Plugin.Commands
{
    public static class AnchorPileCommands
    {
        public const string LayerName = "控え杭";

        // SPQW_ANCHORPILE_Create の一括生成の既定値
        private const int DefaultEveryNPiles = 3;
        private const int DefaultPileCount = 4;
        private const int MaxPileCount = 200;

        // ════════════════════════════════════════════════════════════════════
        // SPQW_ANCHORPILE_Create: 前壁選択 → タイロッド軸線に整列した控え杭を生成
        // ════════════════════════════════════════════════════════════════════
        [Autodesk.AutoCAD.Runtime.CommandMethod("SPQW_ANCHORPILE_Create")]
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

            SheetPileQuayWall.Plugin.XData.AnchorPileRecord record =
                new SheetPileQuayWall.Plugin.XData.AnchorPileRecord();
            record.FrontHandle = frontHandle;
            record.HasPositionY = true;
            // 杭先端標高の既定値は前壁に合わせる(移植元 006 と同じ)
            record.Input.TipElevM = front.TipPoint.Z;
            // 始点は選択した前壁の Y。以降は配置間隔ぶんずつ +Y へ進む
            record.Input.PositionY = front.TipPoint.Y;

            if (!PromptRecord(ed, record, front))
            {
                return;
            }

            // 配置間隔・本数(タイロッドと同じく「矢板何本ごと」で受ける)。
            // ピッチは前壁が壁一括生成で実際に使った有効幅 B を優先する
            // (TieRodCommands.PromptParameters と同じ理由。2026-07-29 発見)。
            double pilePitch_m = front.ToRef().ResolveEffectiveWidth();
            ed.WriteMessage(
                $"\n  前壁から取得: 矢板ピッチ (有効幅 B) {pilePitch_m:F4} m");

            int everyNPiles;
            if (!SheetPileQuayWall.Plugin.Prompt.TryAskInt(
                ed, $"\n控え杭 配置間隔 (矢板何本ごと) <{DefaultEveryNPiles}>: ",
                DefaultEveryNPiles,
                SheetPileQuayWall.Core.TieRod.TieRodPitch.EveryNPiles_Min,
                SheetPileQuayWall.Core.TieRod.TieRodPitch.EveryNPiles_Max,
                out everyNPiles))
            {
                return;
            }
            if (!SheetPileQuayWall.Plugin.Prompt.Report(ed,
                SheetPileQuayWall.Core.TieRod.TieRodPitch.Validate(pilePitch_m, everyNPiles)))
            {
                return;
            }

            double spacing_m = SheetPileQuayWall.Core.TieRod.TieRodPitch.SpacingFor(
                pilePitch_m, everyNPiles);
            ed.WriteMessage(
                $"\n  → 配置間隔 = {pilePitch_m:F4} m × {everyNPiles} 本 = {spacing_m:F4} m");

            int pileCount;
            if (!SheetPileQuayWall.Plugin.Prompt.TryAskInt(
                ed, $"\n控え杭 本数 (本) <{DefaultPileCount}>: ",
                DefaultPileCount, 1, MaxPileCount, out pileCount))
            {
                return;
            }

            double startY = record.Input.PositionY;
            SheetPileQuayWall.Core.AnchorPile.AnchorResult? lastResult = null;

            for (int i = 0; i < pileCount; i++)
            {
                record.Input.PositionY = startY + i * spacing_m;
                lastResult = SheetPileQuayWall.Core.AnchorPile.AnchorAlignment.Compute(
                    front.ToRef(), record.Input);
                BuildAndAppend(db, record, lastResult, replaceId: null);
            }

            ed.WriteMessage("\n=== 控え杭 一括生成 ===");
            ed.WriteMessage($"\n  本数          : {pileCount,10} 本");
            ed.WriteMessage($"\n  配置間隔      : {spacing_m,10:F4} m ({everyNPiles} 本ごと)");
            ed.WriteMessage($"\n  始点 Y (1 本目): {startY,10:F4} m");
            ed.WriteMessage($"\n  終点 Y ({pileCount} 本目): " +
                $"{startY + (pileCount - 1) * spacing_m,10:F4} m");

            PrintSummary(ed, record, lastResult!, front);
            ed.WriteMessage(
                $"\n{pileCount} 本を生成し、パラメータを XData (RegApp: " +
                $"{SheetPileQuayWall.Plugin.XData.AnchorPileRecord.RegAppName}) に保存しました。");
            ed.WriteMessage("\nSPQW_ANCHORPILE_Create 完了。");
        }

        // ════════════════════════════════════════════════════════════════════
        // SPQW_ANCHORPILE_Action: 前壁基準の整列位置に再生成する
        // (MOVE していても整列位置へ戻る。位置は常に前壁+span から導出する)
        // ════════════════════════════════════════════════════════════════════
        [Autodesk.AutoCAD.Runtime.CommandMethod("SPQW_ANCHORPILE_Action")]
        public static void Action()
        {
            Autodesk.AutoCAD.ApplicationServices.Document doc =
                Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager
                    .MdiActiveDocument;
            Autodesk.AutoCAD.DatabaseServices.Database db = doc.Database;
            Autodesk.AutoCAD.EditorInput.Editor ed = doc.Editor;

            Autodesk.AutoCAD.EditorInput.PromptEntityResult res =
                SheetPileQuayWall.Plugin.DrawingHelper.SelectSolid(
                    ed, "\n再生成する控え杭 (Solid3d) を選択: ");
            if (res.Status != Autodesk.AutoCAD.EditorInput.PromptStatus.OK)
            {
                return;
            }

            SheetPileQuayWall.Plugin.XData.AnchorPileRecord? stored =
                ReadRecord(db, res.ObjectId);
            if (stored == null)
            {
                ed.WriteMessage(
                    $"\nエラー: XData (RegApp: " +
                    $"{SheetPileQuayWall.Plugin.XData.AnchorPileRecord.RegAppName}) が" +
                    $"見つかりません。");
                return;
            }

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
                stored.FrontHandle = frontHandle;
            }

            // pos_y を持たない旧図面は、従来の挙動どおり前壁の Y へ整列させる
            if (!stored.HasPositionY)
            {
                stored.Input.PositionY = front.TipPoint.Y;
                stored.HasPositionY = true;
                ed.WriteMessage(
                    $"\n  位置 Y が保存されていないため、前壁の Y = {front.TipPoint.Y:F4} m " +
                    "へ整列します(旧版で作成した図面)。");
            }

            if (!PromptRecord(ed, stored, front))
            {
                return;
            }

            SheetPileQuayWall.Core.AnchorPile.AnchorResult result =
                SheetPileQuayWall.Core.AnchorPile.AnchorAlignment.Compute(
                    front.ToRef(), stored.Input);

            BuildAndAppend(db, stored, result, replaceId: res.ObjectId);

            PrintSummary(ed, stored, result, front);
            ed.WriteMessage("\nSPQW_ANCHORPILE_Action 完了(前壁基準の整列位置に再生成しました)。");
        }

        // ════════════════════════════════════════════════════════════════════
        // SPQW_ANCHORPILE_Query: 諸元・整列座標・数量を出力
        // ════════════════════════════════════════════════════════════════════
        [Autodesk.AutoCAD.Runtime.CommandMethod("SPQW_ANCHORPILE_Query")]
        public static void Query()
        {
            Autodesk.AutoCAD.ApplicationServices.Document doc =
                Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager
                    .MdiActiveDocument;
            Autodesk.AutoCAD.DatabaseServices.Database db = doc.Database;
            Autodesk.AutoCAD.EditorInput.Editor ed = doc.Editor;

            Autodesk.AutoCAD.EditorInput.PromptEntityResult res =
                SheetPileQuayWall.Plugin.DrawingHelper.SelectSolid(
                    ed, "\n諸元を表示する控え杭 (Solid3d) を選択: ");
            if (res.Status != Autodesk.AutoCAD.EditorInput.PromptStatus.OK)
            {
                return;
            }

            SheetPileQuayWall.Plugin.XData.AnchorPileRecord? record =
                ReadRecord(db, res.ObjectId);
            if (record == null)
            {
                ed.WriteMessage("\nエラー: 控え杭の XData が見つかりません。");
                return;
            }

            SheetPileQuayWall.Plugin.XData.FrontWallRecord? front =
                SheetPileQuayWall.Plugin.DrawingHelper.TryResolveFrontWall(
                    db, record.FrontHandle);

            ed.WriteMessage($"\n=== SPQW_ANCHORPILE_Query (前壁 Handle: {record.FrontHandle}) ===");

            if (front == null)
            {
                ed.WriteMessage("\n  前壁参照   : 見つかりません(整列座標は算出できません)");
                PrintSpec(ed, record);
            }
            else
            {
                SheetPileQuayWall.Core.AnchorPile.AnchorResult result =
                    SheetPileQuayWall.Core.AnchorPile.AnchorAlignment.Compute(
                        front.ToRef(), record.Input);
                PrintSummary(ed, record, result, front);
            }

            ed.WriteMessage("\nSPQW_ANCHORPILE_Query 完了。");
        }

        // ────────────────────────────────────────────────────────────────────
        private static bool PromptRecord(
            Autodesk.AutoCAD.EditorInput.Editor ed,
            SheetPileQuayWall.Plugin.XData.AnchorPileRecord record,
            SheetPileQuayWall.Plugin.XData.FrontWallRecord front)
        {
            SheetPileQuayWall.Core.AnchorPile.AnchorInput a = record.Input;

            // 外径は mm 呼称で入力し(決定7)、JIS A 5525 の標準径へスナップする(006 由来)
            double outerD_m;
            if (!SheetPileQuayWall.Plugin.Prompt.TryAskMillimeters(
                ed, $"\n外径 D (mm) [JIS A 5525: 318.5〜2500] <{a.OuterDm * 1000:F1}>: ",
                a.OuterDm * 1000.0,
                SheetPileQuayWall.Core.AnchorPile.AnchorPileSteel.D_Min_m * 1000.0,
                SheetPileQuayWall.Core.AnchorPile.AnchorPileSteel.D_Max_m * 1000.0,
                out outerD_m))
            {
                return false;
            }

            double snapped_m =
                SheetPileQuayWall.Core.AnchorPile.AnchorPileSteel.SnapToJis(outerD_m);
            if (System.Math.Abs(snapped_m - outerD_m) > 0.000001)
            {
                ed.WriteMessage(
                    $"\n注意: 外径 {outerD_m * 1000:F1} mm を JIS 標準径 " +
                    $"{snapped_m * 1000:F1} mm にスナップしました。");
            }
            outerD_m = snapped_m;

            (double tMin_m, double tMax_m) =
                SheetPileQuayWall.Core.AnchorPile.AnchorPileSteel.ThicknessRange(outerD_m);
            double wallT_m;
            if (!SheetPileQuayWall.Plugin.Prompt.TryAskMillimeters(
                ed, $"\n肉厚 t (mm) [製造範囲: {tMin_m * 1000:F0}〜{tMax_m * 1000:F0}] " +
                    $"<{a.WallTm * 1000:F1}>: ",
                a.WallTm * 1000.0, tMin_m * 1000.0, tMax_m * 1000.0, out wallT_m))
            {
                return false;
            }

            double value;
            if (!SheetPileQuayWall.Plugin.Prompt.TryAskDouble(
                ed, $"\n全長 L (m) <{a.LengthM:F1}>: ", a.LengthM,
                SheetPileQuayWall.Core.AnchorPile.AnchorPileSteel.L_Min_m,
                SheetPileQuayWall.Core.AnchorPile.AnchorPileSteel.L_Max_m,
                out value))
            {
                return false;
            }
            double length_m = value;

            if (!SheetPileQuayWall.Plugin.Prompt.TryAskDouble(
                ed, $"\n傾斜角 θ (度, Y軸周り, 0=直杭) <{a.InclDeg:F1}>: ", a.InclDeg,
                SheetPileQuayWall.Core.FrontWall.FrontWallPlacement.Incl_Min_Deg,
                SheetPileQuayWall.Core.FrontWall.FrontWallPlacement.Incl_Max_Deg,
                out value))
            {
                return false;
            }
            double inclDeg = value;

            string tipText;
            if (!SheetPileQuayWall.Plugin.Prompt.TryAskKeyword(
                ed, $"\n先端形状 [開端(O)/閉端(C)] <{(a.ClosedTip ? "C" : "O")}>: ",
                new string[] { "O", "C" }, a.ClosedTip ? "C" : "O", out tipText))
            {
                return false;
            }
            bool closedTip = tipText == "C";

            if (!SheetPileQuayWall.Plugin.Prompt.TryAskDouble(
                ed, $"\n法線直角方向延長 span (m, 前壁矢板中心〜控え杭陸側定着面) " +
                    $"<{a.SpanM:F3}>: ",
                a.SpanM, 3.0, 40.0, out value))
            {
                return false;
            }
            double spanM = value;

            if (!SheetPileQuayWall.Plugin.Prompt.TryAskDouble(
                ed, $"\nタイロッド軸心標高 Z_tr (m, D.L. 基準) <{a.TieElevM:F3}>: ",
                a.TieElevM, -5.0, 10.0, out value))
            {
                return false;
            }
            double tieElevM = value;

            if (!SheetPileQuayWall.Plugin.Prompt.TryAskDouble(
                ed, $"\n杭先端標高 Z_tip (m, D.L. 基準) <{a.TipElevM:F3}>: ", a.TipElevM,
                SheetPileQuayWall.Core.FrontWall.FrontWallPlacement.TipElev_Min_m,
                SheetPileQuayWall.Core.FrontWall.FrontWallPlacement.TipElev_Max_m,
                out value))
            {
                return false;
            }
            double tipElevM = value;

            int colorIdx;
            if (!SheetPileQuayWall.Plugin.Prompt.TryAskInt(
                ed, $"\n本管の色 (ACI 1〜255) <{a.ColorIdx}>: ",
                a.ColorIdx, 1, 255, out colorIdx))
            {
                return false;
            }

            a.OuterDm = outerD_m;
            a.WallTm = wallT_m;
            a.LengthM = length_m;
            a.InclDeg = inclDeg;
            a.ClosedTip = closedTip;
            a.SpanM = spanM;
            a.TieElevM = tieElevM;
            a.TipElevM = tipElevM;
            a.ColorIdx = colorIdx;

            // ── 整合性チェック(不一致はエラー停止、再生成しない)────────────
            return SheetPileQuayWall.Plugin.Prompt.Report(ed,
                SheetPileQuayWall.Core.AnchorPile.AnchorAlignment.Validate(front.ToRef(), a));
        }

        private static void BuildAndAppend(
            Autodesk.AutoCAD.DatabaseServices.Database db,
            SheetPileQuayWall.Plugin.XData.AnchorPileRecord record,
            SheetPileQuayWall.Core.AnchorPile.AnchorResult result,
            Autodesk.AutoCAD.DatabaseServices.ObjectId? replaceId)
        {
            using (Autodesk.AutoCAD.DatabaseServices.Transaction tr =
                db.TransactionManager.StartTransaction())
            {
                SheetPileQuayWall.Plugin.XData.XDataStore.EnsureRegApp(
                    tr, db, SheetPileQuayWall.Plugin.XData.AnchorPileRecord.RegAppName);
                SheetPileQuayWall.Plugin.DrawingHelper.EnsureLayer(db, tr, LayerName, 8);

                if (replaceId.HasValue)
                {
                    SheetPileQuayWall.Plugin.DrawingHelper.EraseSolid(tr, replaceId.Value);
                }

                SheetPileQuayWall.Plugin.DrawingHelper.AppendSolid(
                    db, tr, BuildSolid(record.Input, result),
                    LayerName, record.Input.ColorIdx, record.ToBuffer());

                tr.Commit();
            }
        }

        // 本管(+ 閉端底板)を 1 ソリッドに集約し、傾斜・整列位置への変換まで行う。
        // 控え杭は継手を持たない単独杭である。
        // internal: ImportCommands の CSV 一括生成から再利用するため
        // (ソリッド生成ロジックの重複を避ける)。
        internal static Autodesk.AutoCAD.DatabaseServices.Solid3d BuildSolid(
            SheetPileQuayWall.Core.AnchorPile.AnchorInput a,
            SheetPileQuayWall.Core.AnchorPile.AnchorResult result)
        {
            double outerR = a.OuterDm / 2.0;
            double innerR = (a.OuterDm - 2.0 * a.WallTm) / 2.0;

            Autodesk.AutoCAD.DatabaseServices.Solid3d pile =
                SheetPileQuayWall.Plugin.SolidBuilder.HollowCylinder(
                    outerR, innerR, a.LengthM);

            if (a.ClosedTip)
            {
                // 底板は Z=0〜+t で生成されるため −t ずらして先端下へ配置する
                Autodesk.AutoCAD.DatabaseServices.Solid3d plate =
                    SheetPileQuayWall.Plugin.SolidBuilder.Disk(outerR, a.WallTm);
                plate.TransformBy(
                    Autodesk.AutoCAD.Geometry.Matrix3d.Displacement(
                        new Autodesk.AutoCAD.Geometry.Vector3d(0.0, 0.0, -a.WallTm)));
                pile.BooleanOperation(
                    Autodesk.AutoCAD.DatabaseServices.BooleanOperationType.BoolUnite, plate);
            }

            if (System.Math.Abs(a.InclDeg) > 0.001)
            {
                pile.TransformBy(
                    Autodesk.AutoCAD.Geometry.Matrix3d.Rotation(
                        a.InclDeg * System.Math.PI / 180.0,
                        Autodesk.AutoCAD.Geometry.Vector3d.YAxis,
                        Autodesk.AutoCAD.Geometry.Point3d.Origin));
            }

            pile.TransformBy(
                Autodesk.AutoCAD.Geometry.Matrix3d.Displacement(
                    new Autodesk.AutoCAD.Geometry.Vector3d(
                        result.TipPoint.X, result.TipPoint.Y, result.TipPoint.Z)));

            return pile;
        }

        private static SheetPileQuayWall.Plugin.XData.AnchorPileRecord? ReadRecord(
            Autodesk.AutoCAD.DatabaseServices.Database db,
            Autodesk.AutoCAD.DatabaseServices.ObjectId id)
        {
            SheetPileQuayWall.Plugin.XData.AnchorPileRecord? record = null;
            using (Autodesk.AutoCAD.DatabaseServices.Transaction tr =
                db.TransactionManager.StartTransaction())
            {
                Autodesk.AutoCAD.DatabaseServices.Solid3d? solid =
                    tr.GetObject(id, Autodesk.AutoCAD.DatabaseServices.OpenMode.ForRead)
                    as Autodesk.AutoCAD.DatabaseServices.Solid3d;
                if (solid != null)
                {
                    record = SheetPileQuayWall.Plugin.XData.AnchorPileRecord.Read(solid);
                }
                tr.Commit();
            }
            return record;
        }

        private static void PrintSpec(
            Autodesk.AutoCAD.EditorInput.Editor ed,
            SheetPileQuayWall.Plugin.XData.AnchorPileRecord record)
        {
            SheetPileQuayWall.Core.AnchorPile.AnchorInput a = record.Input;
            double d = a.OuterDm - 2.0 * a.WallTm;

            ed.WriteMessage("\n=== 控え杭 (鋼管杭) 諸元 ===");
            ed.WriteMessage($"\n  外径 D          : {a.OuterDm * 1000,10:F1} mm");
            ed.WriteMessage($"\n  内径 d          : {d * 1000,10:F1} mm");
            ed.WriteMessage($"\n  肉厚 t          : {a.WallTm * 1000,10:F1} mm");
            ed.WriteMessage($"\n  全長 L          : {a.LengthM,10:F1} m");
            ed.WriteMessage($"\n  傾斜角 θ        : {a.InclDeg,10:F1} deg");
            ed.WriteMessage($"\n  先端形状        : {(a.ClosedTip ? "閉端" : "開端")}");
            ed.WriteMessage($"\n  span            : {a.SpanM,10:F3} m (前壁矢板中心〜陸側定着面)");
            ed.WriteMessage($"\n  タイロッド軸心 Z_tr: {a.TieElevM,7:F3} m (D.L.)");
            ed.WriteMessage($"\n  杭先端標高 Z_tip: {a.TipElevM,10:F3} m (D.L.)");
            ed.WriteMessage("\n  継手            : なし(控え杭は単独杭)");

            // 積算数量(1 本あたり)。移植元 006 ANCHORPILE_Query と同じ出力。
            // 式は QuayWallEstimate に一本化し、1 本分の構成で呼び出す
            SheetPileQuayWall.Core.QuayWallQuantity q =
                SheetPileQuayWall.Core.QuayWallEstimate.Compute(
                    new SheetPileQuayWall.Core.QuayWallComposition
                    {
                        FrontPieceCount = 0,
                        AnchorPileCount = 1,
                        AnchorOuterDm = a.OuterDm,
                        AnchorWallTm = a.WallTm,
                        AnchorLengthM = a.LengthM,
                        AnchorClosedTip = a.ClosedTip
                    });

            ed.WriteMessage("\n--- 積算数量 (1 本あたり) ---");
            ed.WriteMessage($"\n  鋼管本体        : {q.AnchorBodyKg,10:F1} kg  [確定](K011 単位重量 × L)");
            if (a.ClosedTip)
            {
                ed.WriteMessage($"\n  底板 (閉端)     : {q.AnchorPlateKg,10:F1} kg  [概算](π/4·D²·t、密度 7.85 g/cm³)");
            }
            ed.WriteMessage($"\n  合計            : {q.AnchorTotalKg,10:F1} kg = " +
                $"{q.AnchorTotalKg / 1000.0:F3} t");
        }

        private static void PrintSummary(
            Autodesk.AutoCAD.EditorInput.Editor ed,
            SheetPileQuayWall.Plugin.XData.AnchorPileRecord record,
            SheetPileQuayWall.Core.AnchorPile.AnchorResult result,
            SheetPileQuayWall.Plugin.XData.FrontWallRecord front)
        {
            PrintSpec(ed, record);

            ed.WriteMessage("\n--- 整列 (前壁基準) ---");
            ed.WriteMessage($"\n  前壁軸 X (Z_tr) : {result.FrontAxisXAtTie_m,10:F3} m" +
                (System.Math.Abs(front.InclDeg) > 0.001
                    ? $" (前壁 θ={front.InclDeg:F1}° を補正済み)" : ""));
            ed.WriteMessage($"\n  控え杭軸 X(Z_tr): {result.AnchorAxisXAtTie_m,10:F3} m");
            ed.WriteMessage($"\n  杭先端 (挿入点) : {result.TipPoint.X,10:F3}, " +
                $"{result.TipPoint.Y:F3}, {result.TipPoint.Z:F3} m");
            ed.WriteMessage($"\n  杭頭標高        : {result.HeadElev_m,10:F3} m (D.L.)");
            ed.WriteMessage($"\n  軸間水平距離    : {result.AxisSpacing_m,10:F3} m");
            ed.WriteMessage($"\n  杭面間浄距離    : {result.FaceClearance_m,10:F3} m");
        }
    }
}
