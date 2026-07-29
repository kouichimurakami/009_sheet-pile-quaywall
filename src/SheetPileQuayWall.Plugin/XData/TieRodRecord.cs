// === 参照DLLバージョン: 未検証 (scripts/verify-dll-versions.ps1 → exit 2) ===
//
// タイロッドの XData(RegApp: SPQW_TIEROD)
// 形式は決定9 の "キー=値"(docs/implementation-plan.md §6.1)。長さは m。
//
// 移植元 008 の 18 項目に加え、決定8 により前壁 Handle 参照を持つ。
// 海側取付点の X は保存せず、_Action 再生成時に前壁 XData から
// TieRodPlacement.SeaAttachmentX で毎回計算し直す(前壁が MOVE / 傾斜角変更
// されても整列位置に追随させるため。移植元 008 は base_x を保存していた)。
//
// キー一覧(タイロッド固有分):
//   front_handle  基準とした前壁 Solid3d の Handle(16 進文字列)
//   pos_y         このタイロッド軸線の Y 座標 [m, WCS]
//   rod_index     組内の連番(0 始まり)

namespace SheetPileQuayWall.Plugin.XData
{
    [Autodesk.DesignScript.Runtime.IsVisibleInDynamoLibrary(false)]
    public sealed class TieRodRecord
    {
        public const string RegAppName = "SPQW_TIEROD";

        public SheetPileQuayWall.Core.TieRod.TieRodParameters Parameters =
            new SheetPileQuayWall.Core.TieRod.TieRodParameters();

        public string FrontHandle = "";
        public double PositionY = 0.0;
        public int RodIndex = 0;

        public Autodesk.AutoCAD.DatabaseServices.ResultBuffer ToBuffer()
        {
            System.Collections.Generic.List<
                Autodesk.AutoCAD.DatabaseServices.TypedValue> values =
                XDataStore.BeginBuffer(RegAppName);

            SheetPileQuayWall.Core.TieRod.TieRodParameters p = Parameters;
            XDataStore.AddReal(values, "rod_d", p.RodDiameter);
            XDataStore.AddText(values, "grade", p.Grade.ToString());
            XDataStore.AddText(values, "code", p.Code.ToString());
            XDataStore.AddText(values, "state", p.State.ToString());
            XDataStore.AddReal(values, "span_length", p.SpanLength);
            XDataStore.AddReal(values, "pile_d", p.PileDiameter);
            XDataStore.AddReal(values, "pile_pitch", p.PilePitch);
            XDataStore.AddReal(values, "tie_spacing", p.TieSpacing);
            XDataStore.AddInt(values, "tie_count", p.TieCount);
            XDataStore.AddReal(values, "hwl", p.Hwl);
            XDataStore.AddReal(values, "tie_elev", p.TieElevation);
            XDataStore.AddReal(values, "waling_h", p.WalingHeight);
            XDataStore.AddReal(values, "plate_t", p.PlateThickness);
            XDataStore.AddReal(values, "washer_t", p.WasherThickness);
            XDataStore.AddReal(values, "nut_h", p.NutHeight);
            XDataStore.AddReal(values, "adjust_l", p.AdjustLength);
            XDataStore.AddReal(values, "anchor_reaction", p.AnchorReaction);
            XDataStore.AddInt(values, "color", p.LayerColor);

            XDataStore.AddText(values, "front_handle", FrontHandle);
            XDataStore.AddReal(values, "pos_y", PositionY);
            XDataStore.AddInt(values, "rod_index", RodIndex);

            return XDataStore.ToBuffer(values);
        }

        public static TieRodRecord? Read(Autodesk.AutoCAD.DatabaseServices.Entity entity)
        {
            System.Collections.Generic.Dictionary<string, string>? map =
                XDataStore.ReadMap(entity, RegAppName);
            if (map == null)
            {
                return null;
            }

            TieRodRecord r = new TieRodRecord();
            SheetPileQuayWall.Core.TieRod.TieRodParameters p = r.Parameters;

            p.RodDiameter = XDataStore.ReadReal(map, "rod_d", p.RodDiameter);
            p.Grade = ParseEnum(XDataStore.ReadText(map, "grade", p.Grade.ToString()), p.Grade);
            p.Code = ParseEnum(XDataStore.ReadText(map, "code", p.Code.ToString()), p.Code);
            p.State = ParseEnum(XDataStore.ReadText(map, "state", p.State.ToString()), p.State);
            p.SpanLength = XDataStore.ReadReal(map, "span_length", p.SpanLength);
            p.PileDiameter = XDataStore.ReadReal(map, "pile_d", p.PileDiameter);
            p.PilePitch = XDataStore.ReadReal(map, "pile_pitch", p.PilePitch);
            p.TieSpacing = XDataStore.ReadReal(map, "tie_spacing", p.TieSpacing);
            p.TieCount = XDataStore.ReadInt(map, "tie_count", p.TieCount);
            p.Hwl = XDataStore.ReadReal(map, "hwl", p.Hwl);
            p.TieElevation = XDataStore.ReadReal(map, "tie_elev", p.TieElevation);
            p.WalingHeight = XDataStore.ReadReal(map, "waling_h", p.WalingHeight);
            p.PlateThickness = XDataStore.ReadReal(map, "plate_t", p.PlateThickness);
            p.WasherThickness = XDataStore.ReadReal(map, "washer_t", p.WasherThickness);
            p.NutHeight = XDataStore.ReadReal(map, "nut_h", p.NutHeight);
            p.AdjustLength = XDataStore.ReadReal(map, "adjust_l", p.AdjustLength);
            p.AnchorReaction = XDataStore.ReadReal(map, "anchor_reaction", p.AnchorReaction);
            p.LayerColor = XDataStore.ReadInt(map, "color", p.LayerColor);

            r.FrontHandle = XDataStore.ReadText(map, "front_handle", "");
            r.PositionY = XDataStore.ReadReal(map, "pos_y", 0.0);
            r.RodIndex = XDataStore.ReadInt(map, "rod_index", 0);
            return r;
        }

        private static T ParseEnum<T>(string text, T fallback) where T : struct
        {
            T parsed;
            return System.Enum.TryParse(text, out parsed) ? parsed : fallback;
        }
    }
}
