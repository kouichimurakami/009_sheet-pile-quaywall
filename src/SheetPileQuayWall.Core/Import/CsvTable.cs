// AutoCAD 非依存 — xUnit で単体テスト可能
//
// 見出し行付き CSV の最小限パーサ。外部 NuGet パッケージには依存せず、
// .NET 8.0 BCL のみで実装する(サーチマス等の積算ソフトから Excel を
// 「CSV UTF-8 形式で保存」した出力を想定。決定7 の単位境界と同様に、
// CSV も「外部データ入力の境界」として扱う)。
//
// 対応: ダブルクォート囲みフィールド、フィールド内カンマ・改行、
//       "" によるダブルクォートのエスケープ、CRLF/LF 混在、先頭 BOM、空行スキップ。
// 列の対応付けは列名(ヘッダー)の完全一致ではなく別名リストで行う。実際の
// エクスポートの列名は未確認のため、別名を後から追加できる構造にしてある
// (README §9.1 参照)。

namespace SheetPileQuayWall.Core.Import
{
    public sealed class CsvTable
    {
        private CsvTable(
            System.Collections.Generic.IReadOnlyList<string> headers,
            System.Collections.Generic.IReadOnlyList<
                System.Collections.Generic.IReadOnlyDictionary<string, string>> rows)
        {
            Headers = headers;
            Rows = rows;
        }

        public System.Collections.Generic.IReadOnlyList<string> Headers { get; }

        // 各行は「ヘッダー名 → セル値」の辞書(大文字小文字を無視)。
        public System.Collections.Generic.IReadOnlyList<
            System.Collections.Generic.IReadOnlyDictionary<string, string>> Rows { get; }

        public static CsvTable Parse(string text)
        {
            System.Collections.Generic.List<System.Collections.Generic.List<string>> records =
                ParseRecords(text);

            System.Collections.Generic.List<string> headers =
                new System.Collections.Generic.List<string>();
            System.Collections.Generic.List<
                System.Collections.Generic.IReadOnlyDictionary<string, string>> rows =
                new System.Collections.Generic.List<
                    System.Collections.Generic.IReadOnlyDictionary<string, string>>();

            if (records.Count == 0)
            {
                return new CsvTable(headers, rows);
            }

            for (int h = 0; h < records[0].Count; h++)
            {
                headers.Add(records[0][h].Trim());
            }

            for (int r = 1; r < records.Count; r++)
            {
                System.Collections.Generic.List<string> record = records[r];
                System.Collections.Generic.Dictionary<string, string> row =
                    new System.Collections.Generic.Dictionary<string, string>(
                        System.StringComparer.OrdinalIgnoreCase);

                for (int c = 0; c < headers.Count; c++)
                {
                    row[headers[c]] = c < record.Count ? record[c].Trim() : "";
                }
                rows.Add(row);
            }

            return new CsvTable(headers, rows);
        }

        // 別名リストのいずれかに一致する列を、前後の空白・大小文字を無視して探す。
        // 見つからない、または値が空文字の場合は false。
        public static bool TryGetField(
            System.Collections.Generic.IReadOnlyDictionary<string, string> row,
            string[] aliases, out string value)
        {
            for (int i = 0; i < aliases.Length; i++)
            {
                string? found;
                if (row.TryGetValue(aliases[i], out found) && found != null && found.Length > 0)
                {
                    value = found;
                    return true;
                }
            }
            value = "";
            return false;
        }

        // ── CSV レコード分解(引用符・改行対応の状態遷移パーサ)──────────────
        private static System.Collections.Generic.List<System.Collections.Generic.List<string>>
            ParseRecords(string text)
        {
            System.Collections.Generic.List<System.Collections.Generic.List<string>> records =
                new System.Collections.Generic.List<System.Collections.Generic.List<string>>();

            if (text.Length > 0 && text[0] == '\uFEFF')
            {
                text = text.Substring(1);
            }

            System.Collections.Generic.List<string> currentRecord =
                new System.Collections.Generic.List<string>();
            System.Text.StringBuilder field = new System.Text.StringBuilder();
            bool inQuotes = false;
            bool recordHasContent = false;

            int i = 0;
            while (i < text.Length)
            {
                char c = text[i];

                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < text.Length && text[i + 1] == '"')
                        {
                            field.Append('"');
                            i += 2;
                            continue;
                        }
                        inQuotes = false;
                        i++;
                        continue;
                    }
                    field.Append(c);
                    i++;
                    continue;
                }

                if (c == '"')
                {
                    inQuotes = true;
                    recordHasContent = true;
                    i++;
                    continue;
                }

                if (c == ',')
                {
                    currentRecord.Add(field.ToString());
                    field.Clear();
                    recordHasContent = true;
                    i++;
                    continue;
                }

                if (c == '\r')
                {
                    i++;
                    continue;
                }

                if (c == '\n')
                {
                    currentRecord.Add(field.ToString());
                    field.Clear();
                    if (recordHasContent || currentRecord.Count > 1)
                    {
                        records.Add(currentRecord);
                    }
                    currentRecord = new System.Collections.Generic.List<string>();
                    recordHasContent = false;
                    i++;
                    continue;
                }

                field.Append(c);
                recordHasContent = true;
                i++;
            }

            if (field.Length > 0 || currentRecord.Count > 0)
            {
                currentRecord.Add(field.ToString());
                if (recordHasContent || currentRecord.Count > 1)
                {
                    records.Add(currentRecord);
                }
            }

            return records;
        }
    }
}
