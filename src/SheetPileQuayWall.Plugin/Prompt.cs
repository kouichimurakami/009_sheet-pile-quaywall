// === 参照DLLバージョン: 未検証 (scripts/verify-dll-versions.ps1 → exit 2) ===
//
// 対話入力の共通ヘルパー。Enter で既定値を採用する。
// 移植元: 006 AskDouble / AskInt、008 ParameterPrompt。
//
// 単位の扱い(決定7): 外径・肉厚は mm 呼称でプロンプト表示・入力し、取得直後に
// m へ変換する。AskMillimeters がその境界であり、これより内側は全て m。

namespace SheetPileQuayWall.Plugin
{
    [Autodesk.DesignScript.Runtime.IsVisibleInDynamoLibrary(false)]
    public static class Prompt
    {
        // AutoCAD 2025 SDK では PromptOptions 系の AllowNone が、型によって
        // 外部から見えない中間基底クラス経由でしか継承されず CS0122 になることがある
        // (2026-07-28 実機で確認。PromptDoubleOptions 等は public な再公開があるが
        // 一部の型に無い)。プロパティ自体は必ず存在するため、型を問わずリフレクションで
        // 設定する。
        private static void SetAllowNone(object opt)
        {
            opt.GetType().GetProperty("AllowNone").SetValue(opt, true);
        }

        // 実数入力。範囲外は再入力を促さずエラー終了する(自動補正しない)。
        public static bool TryAskDouble(
            Autodesk.AutoCAD.EditorInput.Editor ed, string message,
            double defaultValue, double min, double max, out double value)
        {
            value = defaultValue;

            Autodesk.AutoCAD.EditorInput.PromptDoubleOptions opt =
                new Autodesk.AutoCAD.EditorInput.PromptDoubleOptions(message);
            opt.DefaultValue = defaultValue;
            opt.UseDefaultValue = true;
            SetAllowNone(opt);

            Autodesk.AutoCAD.EditorInput.PromptDoubleResult res = ed.GetDouble(opt);
            if (res.Status != Autodesk.AutoCAD.EditorInput.PromptStatus.OK &&
                res.Status != Autodesk.AutoCAD.EditorInput.PromptStatus.None)
            {
                return false;
            }

            value = res.Status == Autodesk.AutoCAD.EditorInput.PromptStatus.None
                ? defaultValue : res.Value;

            if (value < min || value > max)
            {
                ed.WriteMessage($"\nエラー: 入力値 {value} が範囲 {min}〜{max} を外れています。");
                return false;
            }
            return true;
        }

        // mm 呼称で入力を受け、m へ変換して返す(決定7 の単位境界)。
        public static bool TryAskMillimeters(
            Autodesk.AutoCAD.EditorInput.Editor ed, string message,
            double defaultMm, double minMm, double maxMm, out double value_m)
        {
            value_m = defaultMm / 1000.0;

            double mm;
            if (!TryAskDouble(ed, message, defaultMm, minMm, maxMm, out mm))
            {
                return false;
            }

            value_m = mm / 1000.0;
            return true;
        }

        public static bool TryAskInt(
            Autodesk.AutoCAD.EditorInput.Editor ed, string message,
            int defaultValue, int min, int max, out int value)
        {
            value = defaultValue;

            Autodesk.AutoCAD.EditorInput.PromptIntegerOptions opt =
                new Autodesk.AutoCAD.EditorInput.PromptIntegerOptions(message);
            opt.DefaultValue = defaultValue;
            opt.UseDefaultValue = true;
            SetAllowNone(opt);
            opt.LowerLimit = min;
            opt.UpperLimit = max;

            Autodesk.AutoCAD.EditorInput.PromptIntegerResult res = ed.GetInteger(opt);
            if (res.Status != Autodesk.AutoCAD.EditorInput.PromptStatus.OK &&
                res.Status != Autodesk.AutoCAD.EditorInput.PromptStatus.None)
            {
                return false;
            }

            value = res.Status == Autodesk.AutoCAD.EditorInput.PromptStatus.None
                ? defaultValue : res.Value;
            return true;
        }

        // キーワード選択。keywords は大文字の識別子(例 "LT65")。
        public static bool TryAskKeyword(
            Autodesk.AutoCAD.EditorInput.Editor ed, string message,
            string[] keywords, string defaultKeyword, out string value)
        {
            value = defaultKeyword;

            Autodesk.AutoCAD.EditorInput.PromptKeywordOptions opt =
                new Autodesk.AutoCAD.EditorInput.PromptKeywordOptions(message);
            for (int i = 0; i < keywords.Length; i++)
            {
                opt.Keywords.Add(keywords[i]);
            }
            opt.Keywords.Default = defaultKeyword;
            SetAllowNone(opt);

            Autodesk.AutoCAD.EditorInput.PromptResult res = ed.GetKeywords(opt);
            if (res.Status != Autodesk.AutoCAD.EditorInput.PromptStatus.OK &&
                res.Status != Autodesk.AutoCAD.EditorInput.PromptStatus.None)
            {
                return false;
            }

            value = res.Status == Autodesk.AutoCAD.EditorInput.PromptStatus.OK
                ? res.StringResult : defaultKeyword;
            return true;
        }

        // 平面位置(X, Y)を UCS でピックし WCS へ変換する。Z は使わない(§2.2)。
        public static bool TryAskPlanPoint(
            Autodesk.AutoCAD.EditorInput.Editor ed, string message,
            out double x_m, out double y_m)
        {
            x_m = 0.0;
            y_m = 0.0;

            Autodesk.AutoCAD.EditorInput.PromptPointOptions opt =
                new Autodesk.AutoCAD.EditorInput.PromptPointOptions(message);
            Autodesk.AutoCAD.EditorInput.PromptPointResult res = ed.GetPoint(opt);
            if (res.Status != Autodesk.AutoCAD.EditorInput.PromptStatus.OK)
            {
                return false;
            }

            // GetPoint は現在の UCS 座標を返す。エンティティは WCS で構築するため変換する。
            Autodesk.AutoCAD.Geometry.Point3d wcs =
                res.Value.TransformBy(ed.CurrentUserCoordinateSystem);
            x_m = wcs.X;
            y_m = wcs.Y;
            return true;
        }

        // 文字列入力(CSV ファイルパス等)。前後の空白は残す(パスの一部の可能性があるため)。
        public static bool TryAskString(
            Autodesk.AutoCAD.EditorInput.Editor ed, string message,
            string defaultValue, out string value)
        {
            value = defaultValue;

            Autodesk.AutoCAD.EditorInput.PromptStringOptions opt =
                new Autodesk.AutoCAD.EditorInput.PromptStringOptions(message);
            opt.AllowSpaces = true;
            opt.DefaultValue = defaultValue;
            opt.UseDefaultValue = !string.IsNullOrEmpty(defaultValue);
            SetAllowNone(opt);

            Autodesk.AutoCAD.EditorInput.PromptResult res = ed.GetString(opt);
            if (res.Status != Autodesk.AutoCAD.EditorInput.PromptStatus.OK &&
                res.Status != Autodesk.AutoCAD.EditorInput.PromptStatus.None)
            {
                return false;
            }

            value = res.Status == Autodesk.AutoCAD.EditorInput.PromptStatus.None
                ? defaultValue : res.StringResult;
            return true;
        }

        // Core の検証関数が返すエラーメッセージを表示する。null なら true(正常)。
        public static bool Report(
            Autodesk.AutoCAD.EditorInput.Editor ed, string? error)
        {
            if (error == null)
            {
                return true;
            }

            ed.WriteMessage($"\nエラー: {error} 生成を中止しました。");
            return false;
        }
    }
}
