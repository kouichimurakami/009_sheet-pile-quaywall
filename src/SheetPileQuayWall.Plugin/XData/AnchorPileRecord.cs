// === 参照DLLバージョン: 未検証 (scripts/verify-dll-versions.ps1 → exit 2) ===
//
// 控え杭の XData(RegApp: SPQW_ANCHORPILE)
// 形式は決定9 の "キー=値"(docs/implementation-plan.md §6.1)。長さは m、角度は deg。
//
// 位置は保存しない。控え杭の位置は常に「前壁 + span」から導出されるため
// (移植元 006 ANCHORPILE_Action と同じ挙動。MOVE しても整列位置へ戻る)、
// 前壁 Handle と span さえあれば再現できる。
//
// キー一覧:
//   outer_d / wall_t / length / incl_deg / closed_tip / span / tie_elev / tip_elev / color
//   front_handle  基準とした前壁 Solid3d の Handle(16 進文字列)

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

            r.FrontHandle = XDataStore.ReadText(map, "front_handle", "");
            return r;
        }
    }
}
