// === 参照DLLバージョン: 未検証 ===
// AcCoreMgd.dll : 未検証 (期待値 25.x.x.x)
// AcDbMgd.dll   : 未検証 (期待値 25.x.x.x)
// AcMgd.dll     : 未検証 (期待値 25.x.x.x)
// 検証日: 未実施 / 検証コマンド: scripts/verify-dll-versions.ps1 → exit 2 (未検出)
// リスク: TypeLoadException / MissingMethodException が実行時に発生する可能性がある。
//
// 帳票(サーチマス等の積算ソフト出力を CSV UTF-8 で保存したもの)からのパラメータ
// 一括取り込み。前壁は壁一括生成(§9.2 既知の課題の解消)、タイロッド・控え杭は
// 前壁選択後の一括生成、施設全体は積算結果の突合検証を行う。
//
// 【重要】実際のサーチマス等のエクスポート列名・レイアウトは未確認である。
// 列名は別名リストで解決するため、実データを確認したら FrontWallCsvImporter /
// TieRodCsvImporter / AnchorPileCsvImporter / QuantityReconciliation の別名配列に
// 実際の列名・ラベルを追加すること(README §9.1)。
//
// 対応エンコードは UTF-8 のみ(BOM 有無どちらも可)。Shift-JIS 等で保存された
// ファイルは「CSV UTF-8 形式で保存」し直してから読み込むこと(追加の NuGet
// パッケージを導入しないための制約)。
//
// 1 行の不備で取り込み全体を止めない。エラー行は行番号付きで一覧表示し、
// 成功した行だけを生成する。

namespace SheetPileQuayWall.Plugin.Commands
{
    [Autodesk.DesignScript.Runtime.IsVisibleInDynamoLibrary(false)]
    public static class ImportCommands
    {
        // ════════════════════════════════════════════════════════════════════
        // SPQW_FRONTWALL_ImportCsv: CSV 帳票 → 前壁鋼管矢板を一括生成(壁一括生成)
        // ════════════════════════════════════════════════════════════════════
        [Autodesk.AutoCAD.Runtime.CommandMethod("SPQW_FRONTWALL_ImportCsv")]
        public static void ImportFrontWall()
        {
            Autodesk.AutoCAD.ApplicationServices.Document doc =
                Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager
                    .MdiActiveDocument;
            Autodesk.AutoCAD.DatabaseServices.Database db = doc.Database;
            Autodesk.AutoCAD.EditorInput.Editor ed = doc.Editor;

            string? csvText = ReadCsvFile(ed, "\n前壁の帳票 CSV ファイルパス: ");
            if (csvText == null)
            {
                return;
            }

            SheetPileQuayWall.Core.Import.ImportResult<
                SheetPileQuayWall.Core.Import.FrontWallImportRow> result =
                SheetPileQuayWall.Core.Import.FrontWallCsvImporter.Parse(csvText);

            ReportImportErrors(ed, result.Errors);

            if (result.Rows.Count == 0)
            {
                ed.WriteMessage("\n取り込める行がありませんでした。SPQW_FRONTWALL_ImportCsv 中止。");
                return;
            }

            // 施工順位の昇順に並べ、1 本目の位置を基準に累積 Y で自動配置する
            // (壁一括生成)。前壁は直線配置のみを想定し、X は共通固定とする。
            System.Collections.Generic.List<SheetPileQuayWall.Core.Import.FrontWallImportRow> ordered =
                new System.Collections.Generic.List<SheetPileQuayWall.Core.Import.FrontWallImportRow>(
                    result.Rows);
            ordered.Sort((a, b) => a.PieceIndex.CompareTo(b.PieceIndex));

            double baseX, baseY;
            if (!SheetPileQuayWall.Plugin.Prompt.TryAskPlanPoint(
                ed, "\n1 本目(施工順位が最小の矢板)の平面位置を指定: ", out baseX, out baseY))
            {
                return;
            }

            using (Autodesk.AutoCAD.DatabaseServices.Transaction tr =
                db.TransactionManager.StartTransaction())
            {
                SheetPileQuayWall.Plugin.XData.XDataStore.EnsureRegApp(
                    tr, db, SheetPileQuayWall.Plugin.XData.FrontWallRecord.RegAppName);
                SheetPileQuayWall.Plugin.DrawingHelper.EnsureLayer(
                    db, tr, FrontWallCommands.LayerName, 8);

                double y = baseY;
                int created = 0;

                for (int i = 0; i < ordered.Count; i++)
                {
                    SheetPileQuayWall.Core.Import.FrontWallImportRow row = ordered[i];

                    // CSV の tip_z(杭先端標高)を、内部表現(Z_head 基準。2026-07-29)へ変換する
                    SheetPileQuayWall.Core.Point3 headPoint =
                        SheetPileQuayWall.Core.PileGeometry.LocalToWorld(
                            new SheetPileQuayWall.Core.Point3(0.0, 0.0, row.LengthM),
                            row.InclDeg,
                            new SheetPileQuayWall.Core.Point3(baseX, y, row.TipZ));

                    SheetPileQuayWall.Plugin.XData.FrontWallRecord record =
                        new SheetPileQuayWall.Plugin.XData.FrontWallRecord
                        {
                            OuterDm = row.OuterDm,
                            WallTm = row.WallTm,
                            LengthM = row.LengthM,
                            JointCode = row.JointCode,
                            Grade = row.Grade,
                            InclDeg = row.InclDeg,
                            PieceIndex = row.PieceIndex,
                            PieceCount = row.PieceCount,
                            ColorIdx = row.ColorIdx,
                            HeadPoint = headPoint
                        };

                    Autodesk.AutoCAD.DatabaseServices.Solid3d solid =
                        FrontWallCommands.BuildSolid(record);
                    SheetPileQuayWall.Plugin.DrawingHelper.AppendSolid(
                        db, tr, solid, FrontWallCommands.LayerName, record.ColorIdx,
                        record.ToBuffer());
                    created++;

                    // 次の矢板の Y は「今の矢板の有効幅」だけ進める。異なる外径・
                    // 継手が混在する遷移区間では概算になる点に注意(README §9.2)。
                    double effectiveWidth = SheetPileQuayWall.Core.FrontWall.JointParameters
                        .EffectiveWidth(row.OuterDm,
                            SheetPileQuayWall.Core.FrontWall.JointParameters.FromCode(row.JointCode));
                    y += effectiveWidth;
                }

                tr.Commit();
                ed.WriteMessage($"\n{created} 本を生成しました。");
            }

            ed.WriteMessage("\nSPQW_FRONTWALL_ImportCsv 完了。");
        }

        // ════════════════════════════════════════════════════════════════════
        // SPQW_TIEROD_ImportCsv: 前壁選択 → CSV 帳票 → タイロッドを一括生成
        // ════════════════════════════════════════════════════════════════════
        [Autodesk.AutoCAD.Runtime.CommandMethod("SPQW_TIEROD_ImportCsv")]
        public static void ImportTieRod()
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

            string? csvText = ReadCsvFile(ed, "\nタイロッドの帳票 CSV ファイルパス: ");
            if (csvText == null)
            {
                return;
            }

            SheetPileQuayWall.Core.Import.ImportResult<
                SheetPileQuayWall.Core.Import.TieRodImportRow> result =
                SheetPileQuayWall.Core.Import.TieRodCsvImporter.Parse(csvText);

            System.Collections.Generic.List<SheetPileQuayWall.Core.Import.ImportRowError> errors =
                new System.Collections.Generic.List<SheetPileQuayWall.Core.Import.ImportRowError>(
                    result.Errors);

            SheetPileQuayWall.Core.FrontWallRef frontRef = front.ToRef();
            int created = 0;

            using (Autodesk.AutoCAD.DatabaseServices.Transaction tr =
                db.TransactionManager.StartTransaction())
            {
                SheetPileQuayWall.Plugin.XData.XDataStore.EnsureRegApp(
                    tr, db, SheetPileQuayWall.Plugin.XData.TieRodRecord.RegAppName);

                for (int i = 0; i < result.Rows.Count; i++)
                {
                    SheetPileQuayWall.Core.Import.TieRodImportRow row = result.Rows[i];

                    string? crossError = SheetPileQuayWall.Core.CrossMemberValidator
                        .ValidatePileDiameter(frontRef, row.Parameters);
                    if (crossError == null)
                    {
                        crossError = SheetPileQuayWall.Core.CrossMemberValidator
                            .ValidatePilePitch(frontRef, row.Parameters);
                    }
                    if (crossError != null)
                    {
                        errors.Add(new SheetPileQuayWall.Core.Import.ImportRowError(
                            row.RowNumber, crossError));
                        continue;
                    }

                    SheetPileQuayWall.Core.TieRod.TieRodResult r;
                    try
                    {
                        r = SheetPileQuayWall.Core.TieRod.TieRodCalculator.Compute(row.Parameters);
                    }
                    catch (System.ArgumentException ex)
                    {
                        errors.Add(new SheetPileQuayWall.Core.Import.ImportRowError(
                            row.RowNumber, ex.Message));
                        continue;
                    }

                    double baseX = SheetPileQuayWall.Core.TieRod.TieRodPlacement.SeaAttachmentX(
                        frontRef, row.Parameters.TieElevation);

                    SheetPileQuayWall.Plugin.DrawingHelper.EnsureLayer(
                        db, tr, TieRodCommands.LayerName, row.Parameters.LayerColor);

                    SheetPileQuayWall.Plugin.XData.TieRodRecord record =
                        new SheetPileQuayWall.Plugin.XData.TieRodRecord
                        {
                            Parameters = row.Parameters,
                            FrontHandle = frontHandle,
                            PositionY = row.PositionY,
                            RodIndex = 0
                        };

                    Autodesk.AutoCAD.DatabaseServices.Solid3d solid =
                        TieRodCommands.BuildSolid(r, baseX, row.PositionY);
                    SheetPileQuayWall.Plugin.DrawingHelper.AppendSolid(
                        db, tr, solid, TieRodCommands.LayerName, row.Parameters.LayerColor,
                        record.ToBuffer());
                    created++;
                }

                tr.Commit();
            }

            ReportImportErrors(ed, errors);
            ed.WriteMessage($"\n{created} 組を生成しました。");
            ed.WriteMessage("\nSPQW_TIEROD_ImportCsv 完了。");
        }

        // ════════════════════════════════════════════════════════════════════
        // SPQW_ANCHORPILE_ImportCsv: 前壁選択 → CSV 帳票 → 控え杭を一括生成
        // ════════════════════════════════════════════════════════════════════
        [Autodesk.AutoCAD.Runtime.CommandMethod("SPQW_ANCHORPILE_ImportCsv")]
        public static void ImportAnchorPile()
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

            string? csvText = ReadCsvFile(ed, "\n控え杭の帳票 CSV ファイルパス: ");
            if (csvText == null)
            {
                return;
            }

            SheetPileQuayWall.Core.Import.ImportResult<
                SheetPileQuayWall.Core.Import.AnchorPileImportRow> result =
                SheetPileQuayWall.Core.Import.AnchorPileCsvImporter.Parse(csvText);

            System.Collections.Generic.List<SheetPileQuayWall.Core.Import.ImportRowError> errors =
                new System.Collections.Generic.List<SheetPileQuayWall.Core.Import.ImportRowError>(
                    result.Errors);

            SheetPileQuayWall.Core.FrontWallRef frontRef = front.ToRef();
            int created = 0;

            using (Autodesk.AutoCAD.DatabaseServices.Transaction tr =
                db.TransactionManager.StartTransaction())
            {
                SheetPileQuayWall.Plugin.XData.XDataStore.EnsureRegApp(
                    tr, db, SheetPileQuayWall.Plugin.XData.AnchorPileRecord.RegAppName);
                SheetPileQuayWall.Plugin.DrawingHelper.EnsureLayer(
                    db, tr, AnchorPileCommands.LayerName, 8);

                for (int i = 0; i < result.Rows.Count; i++)
                {
                    SheetPileQuayWall.Core.Import.AnchorPileImportRow row = result.Rows[i];

                    string? crossError = SheetPileQuayWall.Core.AnchorPile.AnchorAlignment.Validate(
                        frontRef, row.Input);
                    if (crossError != null)
                    {
                        errors.Add(new SheetPileQuayWall.Core.Import.ImportRowError(
                            row.RowNumber, crossError));
                        continue;
                    }

                    SheetPileQuayWall.Core.AnchorPile.AnchorResult ar =
                        SheetPileQuayWall.Core.AnchorPile.AnchorAlignment.Compute(frontRef, row.Input);

                    SheetPileQuayWall.Plugin.XData.AnchorPileRecord record =
                        new SheetPileQuayWall.Plugin.XData.AnchorPileRecord
                        {
                            Input = row.Input,
                            FrontHandle = frontHandle,
                            HasPositionY = true
                        };

                    Autodesk.AutoCAD.DatabaseServices.Solid3d solid =
                        AnchorPileCommands.BuildSolid(row.Input, ar);
                    SheetPileQuayWall.Plugin.DrawingHelper.AppendSolid(
                        db, tr, solid, AnchorPileCommands.LayerName, row.Input.ColorIdx,
                        record.ToBuffer());
                    created++;
                }

                tr.Commit();
            }

            ReportImportErrors(ed, errors);
            ed.WriteMessage($"\n{created} 本を生成しました。");
            ed.WriteMessage("\nSPQW_ANCHORPILE_ImportCsv 完了。");
        }

        // ════════════════════════════════════════════════════════════════════
        // SPQW_QUAYWALL_ReconcileCsv: 帳票の数量・質量と 009 の計算値を突合する
        // ════════════════════════════════════════════════════════════════════
        [Autodesk.AutoCAD.Runtime.CommandMethod("SPQW_QUAYWALL_ReconcileCsv")]
        public static void ReconcileQuayWall()
        {
            Autodesk.AutoCAD.ApplicationServices.Document doc =
                Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager
                    .MdiActiveDocument;
            Autodesk.AutoCAD.DatabaseServices.Database db = doc.Database;
            Autodesk.AutoCAD.EditorInput.Editor ed = doc.Editor;

            string? csvText = ReadCsvFile(ed, "\n突合する帳票 CSV ファイルパス (項目,数量): ");
            if (csvText == null)
            {
                return;
            }

            System.Collections.Generic.IReadOnlyDictionary<string, double> reported =
                SheetPileQuayWall.Core.Import.QuantityReconciliation.ParseReportedCsv(csvText);
            if (reported.Count == 0)
            {
                ed.WriteMessage("\nエラー: 帳票から数値を 1 件も読み取れませんでした" +
                    "(列名 項目/数量、または label/value を確認してください)。");
                return;
            }

            SheetPileQuayWall.Plugin.XData.FrontWallRecord? front;
            SheetPileQuayWall.Core.QuayWallComposition? composition =
                QuayWallCommands.BuildCompositionFromPrompts(ed, db, out front);
            if (composition == null)
            {
                return;
            }

            SheetPileQuayWall.Core.QuayWallQuantity q =
                SheetPileQuayWall.Core.QuayWallEstimate.Compute(composition);

            ed.WriteMessage("\n=== 帳票との突合検証 (許容誤差 " +
                $"{SheetPileQuayWall.Core.Import.QuantityReconciliation.DefaultToleranceRatio * 100:F0}%) ===");

            PrintReconciliation(ed, reported, new[] { "施設延長", "wall_length_m" },
                "施設延長 [m]", q.WallLengthM);
            PrintReconciliation(ed, reported, new[] { "継手接続数", "joint_count" },
                "継手接続数 [箇所]", q.JointConnectionCount);
            PrintReconciliation(ed, reported, new[] { "前壁本管質量", "front_body_kg" },
                "前壁 本管質量 [kg]", q.FrontBodyKg);
            PrintReconciliation(ed, reported, new[] { "前壁継手質量", "front_joint_kg" },
                "前壁 継手質量 [kg]", q.FrontJointKg);
            PrintReconciliation(ed, reported, new[] { "タイロッド質量", "tie_rod_kg" },
                "タイロッド 質量 [kg]", q.TieRodKg);
            PrintReconciliation(ed, reported, new[] { "控え杭質量", "anchor_kg" },
                "控え杭 質量 [kg]", q.AnchorTotalKg);
            PrintReconciliation(ed, reported, new[] { "合計質量", "total_kg" },
                "合計質量 [kg]", q.TotalKg);

            ed.WriteMessage("\nSPQW_QUAYWALL_ReconcileCsv 完了。");
        }

        // ────────────────────────────────────────────────────────────────────
        private static string? ReadCsvFile(
            Autodesk.AutoCAD.EditorInput.Editor ed, string message)
        {
            string path;
            if (!SheetPileQuayWall.Plugin.Prompt.TryAskString(ed, message, "", out path)
                || path.Length == 0)
            {
                return null;
            }

            if (!System.IO.File.Exists(path))
            {
                ed.WriteMessage($"\nエラー: ファイルが見つかりません: {path}");
                return null;
            }

            try
            {
                // UTF-8 のみ対応(BOM 有無どちらも可)。Shift-JIS 等は
                // 「CSV UTF-8 形式で保存」し直してから読み込むこと。
                return System.IO.File.ReadAllText(path, System.Text.Encoding.UTF8);
            }
            catch (System.IO.IOException ex)
            {
                ed.WriteMessage($"\nエラー: ファイルを読み取れません: {ex.Message}");
                return null;
            }
        }

        private static void ReportImportErrors(
            Autodesk.AutoCAD.EditorInput.Editor ed,
            System.Collections.Generic.IReadOnlyList<SheetPileQuayWall.Core.Import.ImportRowError> errors)
        {
            if (errors.Count == 0)
            {
                return;
            }

            ed.WriteMessage($"\n--- 取り込めなかった行 ({errors.Count} 件) ---");
            for (int i = 0; i < errors.Count; i++)
            {
                ed.WriteMessage($"\n  {errors[i].RowNumber} 行目: {errors[i].Message}");
            }
        }

        private static void PrintReconciliation(
            Autodesk.AutoCAD.EditorInput.Editor ed,
            System.Collections.Generic.IReadOnlyDictionary<string, double> reported,
            string[] labelAliases, string displayLabel, double computed)
        {
            SheetPileQuayWall.Core.Import.ReconciliationItem? item =
                SheetPileQuayWall.Core.Import.QuantityReconciliation.Compare(
                    reported, labelAliases, displayLabel, computed);

            if (item == null)
            {
                ed.WriteMessage($"\n  {displayLabel,-20}: 帳票に対応する項目が見つかりません(009 計算値 {computed:F1})");
                return;
            }

            ed.WriteMessage($"\n  {displayLabel,-20}: 帳票 {item.Reported,12:F1} / 009 {item.Computed,12:F1} " +
                $"/ 差 {item.Difference,10:F1} " +
                (item.WithinTolerance ? "(OK)" : "(NG — 許容誤差超過)"));
        }
    }
}
