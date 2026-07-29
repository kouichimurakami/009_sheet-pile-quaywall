// === 参照DLLバージョン: 未検証 (scripts/verify-dll-versions.ps1 → exit 2) ===
//
// 控え杭の XData(RegApp: SPQW_ANCHORPILE)
// 形式は決定9 の "キー=値"(docs/implementation-plan.md §6.1)。長さは m、角度は deg。
//
// 平面 X は保存しない。控え杭の X は常に「前壁 + span」から導出されるため
// (移植元 006 ANCHORPILE_Action と同じ挙動。MOVE しても整列位置へ戻る)、
// 前壁 Handle と span さえあれば再現できる。
// 一方 Y は 1 本ずつ異なるため pos_y として保存する(複数本の一括生成に対応)。
//
// キー一覧:
//   outer_d / wall_t / length / incl_deg / closed_tip / span / tie_elev / tip_elev / color
//   pos_y         施設延長方向の位置 Y [m]
//   front_handle  基準とした前壁 Solid3d の Handle(16 進文字列)
//
// pos_y は複数本一括生成の追加時に新設したキーで、それ以前に作成した図面には無い。
// 読み側は欠落を検出できるよう HasPositionY を併せて返し、呼び出し側が前壁の Y で
// 補えるようにしている(旧図面の _Action / _Query を壊さないため)。

namespace SheetPileQuayWall.Plugin.XData
{
    public sealed class AnchorPileRecord
    {
        public const string RegAppName = "SPQW_ANCHORPILE";

        public SheetPileQuayWall.Core.AnchorPile.AnchorInput Input =
            new SheetPileQuayWall.Core.AnchorPile.AnchorInput
            {
                OuterDm = 0.800, WallTm = 0.012, LengthM = 20.0, InclDeg = 0.0,
                ClosedTip = false, SpanM = 10.0, TieElevM = 2.5, TipElevM = -18.0,
                ColorIdx = 8
            };

        public string FrontHandle = "";

        // pos_y キーを持つ図面から読んだか。false なら旧図面のため、
        // 呼び出し側が前壁の Y で Input.PositionY を補うこと。
        public bool HasPositionY;

        public Autodesk.AutoCAD.DatabaseServices.ResultBuffer ToBuffer()
        {
            System.Collections.Generic.List<
                Autodesk.AutoCAD.DatabaseServices.TypedValue> values =
                XDataStore.BeginBuffer(RegAppName);

            SheetPileQuayWall.Core.AnchorPile.AnchorInput a = Input;
            XDataStore.AddReal(values, "outer_d", a.OuterDm);
            XDataStore.AddReal(values, "wall_t", a.WallTm);
            XDataStore.AddReal(values, "length", a.LengthM);
            XDataStore.AddReal(values, "incl_deg", a.InclDeg);
            XDataStore.AddBool(values, "closed_tip", a.ClosedTip);
            XDataStore.AddReal(values, "span", a.SpanM);
            XDataStore.AddReal(values, "tie_elev", a.TieElevM);
            XDataStore.AddReal(values, "tip_elev", a.TipElevM);
            XDataStore.AddInt(values, "color", a.ColorIdx);
            XDataStore.AddReal(values, "pos_y", a.PositionY);
            XDataStore.AddText(values, "front_handle", FrontHandle);

            return XDataStore.ToBuffer(values);
        }

        public static AnchorPileRecord? Read(Autodesk.AutoCAD.DatabaseServices.Entity entity)
        {
            System.Collections.Generic.Dictionary<string, string>? map =
                XDataStore.ReadMap(entity, RegAppName);
            if (map == null)
            {
                return null;
            }

            AnchorPileRecord r = new AnchorPileRecord();
            SheetPileQuayWall.Core.AnchorPile.AnchorInput a = r.Input;

            a.OuterDm = XDataStore.ReadReal(map, "outer_d", a.OuterDm);
            a.WallTm = XDataStore.ReadReal(map, "wall_t", a.WallTm);
            a.LengthM = XDataStore.ReadReal(map, "length", a.LengthM);
            a.InclDeg = XDataStore.ReadReal(map, "incl_deg", a.InclDeg);
            a.ClosedTip = XDataStore.ReadBool(map, "closed_tip", a.ClosedTip);
            a.SpanM = XDataStore.ReadReal(map, "span", a.SpanM);
            a.TieElevM = XDataStore.ReadReal(map, "tie_elev", a.TieElevM);
            a.TipElevM = XDataStore.ReadReal(map, "tip_elev", a.TipElevM);
            a.ColorIdx = XDataStore.ReadInt(map, "color", a.ColorIdx);

            // pos_y は後から追加したキー。旧図面には無いため、欠落を呼び出し側へ伝える
            r.HasPositionY = map.ContainsKey("pos_y");
            a.PositionY = XDataStore.ReadReal(map, "pos_y", 0.0);

            r.FrontHandle = XDataStore.ReadText(map, "front_handle", "");
            return r;
        }
    }
}
