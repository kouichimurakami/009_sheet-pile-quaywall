// AutoCAD 非依存 — xUnit で単体テスト可能
//
// 帳票取り込みの結果を「成功した行」と「行番号付きエラー」に分けて返す共通型。
// 1 行の不備で取り込み全体を止めないため(部材数十件の CSV で 1 行だけ誤記が
// あっても、残りは生成できるようにする)。

namespace SheetPileQuayWall.Core.Import
{
    public sealed class ImportRowError
    {
        public ImportRowError(int rowNumber, string message)
        {
            RowNumber = rowNumber;
            Message = message;
        }

        // ヘッダー行を 1 行目として数えた行番号(データの 1 行目 = 2)。
        public int RowNumber { get; }

        public string Message { get; }
    }

    public sealed class ImportResult<T>
    {
        public ImportResult(
            System.Collections.Generic.IReadOnlyList<T> rows,
            System.Collections.Generic.IReadOnlyList<ImportRowError> errors)
        {
            Rows = rows;
            Errors = errors;
        }

        public System.Collections.Generic.IReadOnlyList<T> Rows { get; }

        public System.Collections.Generic.IReadOnlyList<ImportRowError> Errors { get; }
    }
}
