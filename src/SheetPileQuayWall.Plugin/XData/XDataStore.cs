// === 参照DLLバージョン: 未検証 ===
// AcCoreMgd.dll : 未検証 (期待値 25.x.x.x)
// AcDbMgd.dll   : 未検証 (期待値 25.x.x.x)
// AcMgd.dll     : 未検証 (期待値 25.x.x.x)
// 検証日: 未実施 / 検証コマンド: scripts/verify-dll-versions.ps1 → exit 2 (未検出)
// リスク: 実DLLのバージョンを実測できていないため、TypeLoadException /
//         MissingMethodException が実行時に発生する可能性がある。
//
// XData の読み書き基盤(決定9、docs/implementation-plan.md §6.1)
//
// 形式は "キー=値" の ASCII 文字列を並べたもの。移植元 008 XDataStore と同じ方式で、
// 順序に依存せず項目追加にも耐える。先頭に形式バージョン fmt を置く。
// 007/006 の位置依存(index 1=D, 2=t, …)方式は採らない。
//
// 数値は InvariantCulture で書式化する。ロケール依存の小数点(1,200 等)で
// 図面が壊れることを防ぐため。

namespace SheetPileQuayWall.Plugin.XData
{
    [Autodesk.DesignScript.Runtime.IsVisibleInDynamoLibrary(false)]
    public static class XDataStore
    {
        // 形式バージョン。将来キー名や意味を変える場合はこれを上げ、読み側で分岐する。
        public const string FormatVersion = "1";

        public const string KeyFormat = "fmt";

        private static readonly System.Globalization.CultureInfo Inv =
            System.Globalization.CultureInfo.InvariantCulture;

        // RegApp を登録する。既に存在すれば何もしない。
        public static void EnsureRegApp(
            Autodesk.AutoCAD.DatabaseServices.Transaction tr,
            Autodesk.AutoCAD.DatabaseServices.Database db,
            string regAppName)
        {
            Autodesk.AutoCAD.DatabaseServices.RegAppTable table =
                (Autodesk.AutoCAD.DatabaseServices.RegAppTable)tr.GetObject(
                    db.RegAppTableId,
                    Autodesk.AutoCAD.DatabaseServices.OpenMode.ForRead);

            if (table.Has(regAppName))
            {
                return;
            }

            table.UpgradeOpen();
            Autodesk.AutoCAD.DatabaseServices.RegAppTableRecord record =
                new Autodesk.AutoCAD.DatabaseServices.RegAppTableRecord();
            record.Name = regAppName;
            table.Add(record);
            tr.AddNewlyCreatedDBObject(record, true);
        }

        // ── 書き込み ────────────────────────────────────────────────────────

        public static System.Collections.Generic.List<
            Autodesk.AutoCAD.DatabaseServices.TypedValue> BeginBuffer(string regAppName)
        {
            System.Collections.Generic.List<
                Autodesk.AutoCAD.DatabaseServices.TypedValue> values =
                new System.Collections.Generic.List<
                    Autodesk.AutoCAD.DatabaseServices.TypedValue>();

            values.Add(new Autodesk.AutoCAD.DatabaseServices.TypedValue(
                (int)Autodesk.AutoCAD.DatabaseServices.DxfCode.ExtendedDataRegAppName,
                regAppName));
            AddText(values, KeyFormat, FormatVersion);
            return values;
        }

        public static void AddText(
            System.Collections.Generic.List<
                Autodesk.AutoCAD.DatabaseServices.TypedValue> values,
            string key, string value)
        {
            values.Add(new Autodesk.AutoCAD.DatabaseServices.TypedValue(
                (int)Autodesk.AutoCAD.DatabaseServices.DxfCode.ExtendedDataAsciiString,
                key + "=" + value));
        }

        public static void AddReal(
            System.Collections.Generic.List<
                Autodesk.AutoCAD.DatabaseServices.TypedValue> values,
            string key, double value)
        {
            AddText(values, key, value.ToString("R", Inv));
        }

        public static void AddInt(
            System.Collections.Generic.List<
                Autodesk.AutoCAD.DatabaseServices.TypedValue> values,
            string key, int value)
        {
            AddText(values, key, value.ToString(Inv));
        }

        public static void AddBool(
            System.Collections.Generic.List<
                Autodesk.AutoCAD.DatabaseServices.TypedValue> values,
            string key, bool value)
        {
            AddText(values, key, value ? "1" : "0");
        }

        public static void AddPoint(
            System.Collections.Generic.List<
                Autodesk.AutoCAD.DatabaseServices.TypedValue> values,
            string keyPrefix, SheetPileQuayWall.Core.Point3 point)
        {
            AddReal(values, keyPrefix + "_x", point.X);
            AddReal(values, keyPrefix + "_y", point.Y);
            AddReal(values, keyPrefix + "_z", point.Z);
        }

        // World 座標点(DxfCode 1011)。AutoCAD が MOVE 等の変換に自動追随させる
        // グループコードであり、006 の挿入点追随はこれに依存していた。
        // キー=値の文字列は追随しないため、位置は 1011 を併記し読み側で優先する。
        public static void AddWorldPoint(
            System.Collections.Generic.List<
                Autodesk.AutoCAD.DatabaseServices.TypedValue> values,
            SheetPileQuayWall.Core.Point3 point)
        {
            values.Add(new Autodesk.AutoCAD.DatabaseServices.TypedValue(
                (int)Autodesk.AutoCAD.DatabaseServices.DxfCode.ExtendedDataWorldXCoordinate,
                new Autodesk.AutoCAD.Geometry.Point3d(point.X, point.Y, point.Z)));
        }

        public static Autodesk.AutoCAD.DatabaseServices.ResultBuffer ToBuffer(
            System.Collections.Generic.List<
                Autodesk.AutoCAD.DatabaseServices.TypedValue> values)
        {
            return new Autodesk.AutoCAD.DatabaseServices.ResultBuffer(values.ToArray());
        }

        // ── 読み込み ────────────────────────────────────────────────────────

        // 指定 RegApp のセクションを "キー→値" の辞書として取り出す。
        // XData 未記録・該当セクション無しの場合は null。
        public static System.Collections.Generic.Dictionary<string, string>? ReadMap(
            Autodesk.AutoCAD.DatabaseServices.Entity entity, string regAppName)
        {
            Autodesk.AutoCAD.DatabaseServices.ResultBuffer buffer =
                entity.GetXDataForApplication(regAppName);
            if (buffer == null)
            {
                return null;
            }

            System.Collections.Generic.Dictionary<string, string> map =
                new System.Collections.Generic.Dictionary<string, string>();
            bool inOurSection = false;

            Autodesk.AutoCAD.DatabaseServices.TypedValue[] items = buffer.AsArray();
            for (int i = 0; i < items.Length; i++)
            {
                if (items[i].TypeCode ==
                    (short)Autodesk.AutoCAD.DatabaseServices.DxfCode.ExtendedDataRegAppName)
                {
                    // 他アプリの XData が続けて格納されている場合に備え、
                    // 自分のセクションだけを読む。
                    inOurSection = string.Equals(items[i].Value as string, regAppName);
                    continue;
                }

                if (!inOurSection)
                {
                    continue;
                }

                string? text = items[i].Value as string;
                if (text == null)
                {
                    continue;
                }

                int sep = text.IndexOf('=');
                if (sep <= 0)
                {
                    continue;
                }

                map[text.Substring(0, sep)] = text.Substring(sep + 1);
            }

            buffer.Dispose();
            return map.Count > 0 ? map : null;
        }

        public static string ReadText(
            System.Collections.Generic.Dictionary<string, string> map,
            string key, string fallback)
        {
            string? value;
            return map.TryGetValue(key, out value) ? value : fallback;
        }

        public static double ReadReal(
            System.Collections.Generic.Dictionary<string, string> map,
            string key, double fallback)
        {
            string? value;
            if (!map.TryGetValue(key, out value))
            {
                return fallback;
            }

            double parsed;
            return double.TryParse(
                value, System.Globalization.NumberStyles.Float, Inv, out parsed)
                ? parsed : fallback;
        }

        public static int ReadInt(
            System.Collections.Generic.Dictionary<string, string> map,
            string key, int fallback)
        {
            string? value;
            if (!map.TryGetValue(key, out value))
            {
                return fallback;
            }

            int parsed;
            return int.TryParse(
                value, System.Globalization.NumberStyles.Integer, Inv, out parsed)
                ? parsed : fallback;
        }

        public static bool ReadBool(
            System.Collections.Generic.Dictionary<string, string> map,
            string key, bool fallback)
        {
            string? value;
            if (!map.TryGetValue(key, out value))
            {
                return fallback;
            }

            return value == "1";
        }

        // 指定 RegApp のセクションから最初の 1011(World 座標点)を読む。
        // MOVE 後は AutoCAD が変換済みの値を返すため、文字列キーより現在位置に忠実。
        public static bool TryReadWorldPoint(
            Autodesk.AutoCAD.DatabaseServices.Entity entity, string regAppName,
            out SheetPileQuayWall.Core.Point3 point)
        {
            point = new SheetPileQuayWall.Core.Point3(0.0, 0.0, 0.0);

            Autodesk.AutoCAD.DatabaseServices.ResultBuffer buffer =
                entity.GetXDataForApplication(regAppName);
            if (buffer == null)
            {
                return false;
            }

            bool inOurSection = false;
            bool found = false;

            Autodesk.AutoCAD.DatabaseServices.TypedValue[] items = buffer.AsArray();
            for (int i = 0; i < items.Length; i++)
            {
                if (items[i].TypeCode ==
                    (short)Autodesk.AutoCAD.DatabaseServices.DxfCode.ExtendedDataRegAppName)
                {
                    inOurSection = string.Equals(items[i].Value as string, regAppName);
                    continue;
                }

                if (!inOurSection)
                {
                    continue;
                }

                if (items[i].TypeCode ==
                    (short)Autodesk.AutoCAD.DatabaseServices.DxfCode
                        .ExtendedDataWorldXCoordinate &&
                    items[i].Value is Autodesk.AutoCAD.Geometry.Point3d p)
                {
                    point = new SheetPileQuayWall.Core.Point3(p.X, p.Y, p.Z);
                    found = true;
                    break;
                }
            }

            buffer.Dispose();
            return found;
        }

        public static SheetPileQuayWall.Core.Point3 ReadPoint(
            System.Collections.Generic.Dictionary<string, string> map,
            string keyPrefix, SheetPileQuayWall.Core.Point3 fallback)
        {
            return new SheetPileQuayWall.Core.Point3(
                ReadReal(map, keyPrefix + "_x", fallback.X),
                ReadReal(map, keyPrefix + "_y", fallback.Y),
                ReadReal(map, keyPrefix + "_z", fallback.Z));
        }

        public static bool HasPoint(
            System.Collections.Generic.Dictionary<string, string> map, string keyPrefix)
        {
            return map.ContainsKey(keyPrefix + "_x")
                && map.ContainsKey(keyPrefix + "_y")
                && map.ContainsKey(keyPrefix + "_z");
        }
    }
}
