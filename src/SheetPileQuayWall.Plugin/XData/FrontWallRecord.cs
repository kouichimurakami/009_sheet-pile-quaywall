// === 参照DLLバージョン: 未検証 (scripts/verify-dll-versions.ps1 → exit 2) ===
//
// 前壁鋼管矢板の XData(RegApp: SPQW_FRONTWALL)
// 形式は決定9 の "キー=値"(docs/implementation-plan.md §6.1)。長さは m、角度は deg。
//
// キー一覧:
//   fmt          形式バージョン
//   outer_d      外径 D [m]
//   wall_t       肉厚 t [m]
//   length       全長 L [m]
//   joint        継手コード (LT65/LT75/LT100/PP/PT)
//   grade        鋼種 (SKY400/SKY490)
//   incl_deg     傾斜角 θ [deg]
//   piece_index  施工順位 [本目]
//   piece_count  総本数 [本]
//   effective_width  有効幅 B [m](壁一括生成で実際に使われた値。旧図面には無く、
//                無い場合は外径・継手形式から算出した値へフォールバックする)
//   color        本管の色 (ACI)
//   tip_x/_y/_z  杭先端(挿入点)の WCS 座標 [m]。_z は D.L. 基準の杭先端標高
//   (1011)       杭先端の World 座標点。MOVE に AutoCAD が自動追随させるため、
//                読み側はこちらを優先し、tip_x/_y/_z はフォールバック(006 の追随を維持)

namespace SheetPileQuayWall.Plugin.XData
{
    public sealed class FrontWallRecord
    {
        public const string RegAppName = "SPQW_FRONTWALL";

        public double OuterDm = 0.800;
        public double WallTm = 0.012;
        public double LengthM = 20.0;
        public string JointCode = "LT75";
        public string Grade = "SKY400";
        public double InclDeg = 0.0;
        public int PieceIndex = 1;
        public int PieceCount = 1;
        public double EffectiveWidthM = 0.0; // 0 以下は未設定(旧図面)。ToRef 側でフォールバック
        public int ColorIdx = 8;
        public SheetPileQuayWall.Core.Point3 TipPoint =
            new SheetPileQuayWall.Core.Point3(0.0, 0.0, -18.0);

        public Autodesk.AutoCAD.DatabaseServices.ResultBuffer ToBuffer()
        {
            System.Collections.Generic.List<
                Autodesk.AutoCAD.DatabaseServices.TypedValue> values =
                XDataStore.BeginBuffer(RegAppName);

            XDataStore.AddReal(values, "outer_d", OuterDm);
            XDataStore.AddReal(values, "wall_t", WallTm);
            XDataStore.AddReal(values, "length", LengthM);
            XDataStore.AddText(values, "joint", JointCode);
            XDataStore.AddText(values, "grade", Grade);
            XDataStore.AddReal(values, "incl_deg", InclDeg);
            XDataStore.AddInt(values, "piece_index", PieceIndex);
            XDataStore.AddInt(values, "piece_count", PieceCount);
            XDataStore.AddReal(values, "effective_width", EffectiveWidthM);
            XDataStore.AddInt(values, "color", ColorIdx);
            XDataStore.AddPoint(values, "tip", TipPoint);
            XDataStore.AddWorldPoint(values, TipPoint);

            return XDataStore.ToBuffer(values);
        }

        // XData 未記録・形式不一致の場合は null。
        public static FrontWallRecord? Read(
            Autodesk.AutoCAD.DatabaseServices.Entity entity)
        {
            System.Collections.Generic.Dictionary<string, string>? map =
                XDataStore.ReadMap(entity, RegAppName);
            if (map == null || !XDataStore.HasPoint(map, "tip"))
            {
                return null;
            }

            FrontWallRecord r = new FrontWallRecord();
            r.OuterDm = XDataStore.ReadReal(map, "outer_d", r.OuterDm);
            r.WallTm = XDataStore.ReadReal(map, "wall_t", r.WallTm);
            r.LengthM = XDataStore.ReadReal(map, "length", r.LengthM);
            r.JointCode = XDataStore.ReadText(map, "joint", r.JointCode);
            r.Grade = XDataStore.ReadText(map, "grade", r.Grade);
            r.InclDeg = XDataStore.ReadReal(map, "incl_deg", r.InclDeg);
            r.PieceIndex = XDataStore.ReadInt(map, "piece_index", r.PieceIndex);
            r.PieceCount = XDataStore.ReadInt(map, "piece_count", r.PieceCount);
            // 旧図面には effective_width キーが無いため既定 0.0(未設定)のまま残り、
            // ToRef 側で外径・継手形式からの算出値にフォールバックする
            r.EffectiveWidthM = XDataStore.ReadReal(map, "effective_width", r.EffectiveWidthM);
            r.ColorIdx = XDataStore.ReadInt(map, "color", r.ColorIdx);
            r.TipPoint = XDataStore.ReadPoint(map, "tip", r.TipPoint);

            // MOVE 後は 1011 だけが移動先を指す。存在すれば文字列キーより優先する
            SheetPileQuayWall.Core.Point3 worldTip;
            if (XDataStore.TryReadWorldPoint(entity, RegAppName, out worldTip))
            {
                r.TipPoint = worldTip;
            }
            return r;
        }

        // Core へ渡す前壁参照情報へ変換する。
        public SheetPileQuayWall.Core.FrontWallRef ToRef()
        {
            return new SheetPileQuayWall.Core.FrontWallRef
            {
                TipPoint = TipPoint,
                OuterDm = OuterDm,
                InclDeg = InclDeg,
                LengthM = LengthM,
                JointType = SheetPileQuayWall.Core.FrontWall.JointParameters.FromCode(JointCode),
                EffectiveWidthM = EffectiveWidthM
            };
        }
    }
}
