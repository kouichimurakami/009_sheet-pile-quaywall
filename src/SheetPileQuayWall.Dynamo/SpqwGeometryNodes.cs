// === 参照DLLバージョン: 未検証 ===
// ProtoGeometry.dll : 未検証 (期待値 3.3.x.x)  $(AcadRoot)\C3D\Dynamo\Core\
// 検証日: 未実施 / 検証コマンド: scripts/verify-dll-versions.ps1 → exit 2 (未検出)
// リスク: TypeLoadException / MissingMethodException が実行時に発生する可能性がある。
// さらに Point/Circle/Solid の各メソッドシグネチャは一般的な Dynamo 3.x の API知識に
// 基づく推測であり、実際の ProtoGeometry.dll と一致するかは実機ビルドまで未確認。
//
// Dynamo ネイティブジオメトリ(Autodesk.DesignScript.Geometry / ProtoGeometry.dll)による
// 3D ソリッド生成ノード(2026-07-29 新設)。
//
// 従来の SpqwNodes(計算専用)とは異なり、本クラスは Geometry を戻り値として返し、
// Dynamo 自身が図面へ焼き込む。焼き込み後のエンティティに XData を後付けする手段が
// 無いため、本ノードが生成するソリッドには XData を一切書き込まない
// (README §10 参照)。パラメトリック性は AutoCAD コマンド側の
// 「XData 保存 → _Action で再生成」ではなく、Dynamo 自身のグラフ再実行
// (入力ノードの値を変えるとグラフ全体が再評価される)に委ねる。
//
// 前壁(CreateFrontWallPileSolid)は本体円筒のみを生成する。継手(LT65/75/100・PP/PT の
// 実形状、SolidBuilder.JointMember 相当)の複製は次段階(未実装)。
//
// ソリッド生成のローカル原点は杭先端(Z=0)とし、AutoCAD 版
// (AnchorPileCommands.BuildSolid / FrontWallCommands.BuildSolid)と同じ変換順序
// (回転 → 杭先端への平行移動)を踏襲する。挿入点は杭上端(headPoint)で受け取り、
// PileGeometry.TipFromHead で杭先端へ変換してから配置する
// (009 の Z_head 基準アーキテクチャに合わせる、2026-07-29)。
//
// CreateTieRodSolid(2026-07-29 追加)は AutoCAD コマンドの前壁選択(XData 経由の
// 海側鋼管矢板径・矢板ピッチ自動代入)を行わず、TieRodParameters の該当項目を
// 明示引数として直接受け取る(SpqwNodes と同じ「XData を経由しない」規約)。
// 海側取付点の X 座標(baseX_m)も、前壁参照オブジェクトではなく数値そのもので
// 受け取る(TieRodCommands.BuildSolid が baseX を単純な double で受け取るのと同じ設計)。
// 鋼種・設計基準・荷重状態・腹起し高さ・定着プレート/ワッシャー厚・取付点反力は
// TieRodParameters の既定値を固定で使う(_Create のプロンプト廃止方針と同じ、
// 2026-07-29 の別変更)。組数(tieCount)ぶんの Solid をまとめて返す。

namespace SheetPileQuayWall.Dynamo
{
    // Dynamo ノードカテゴリ: SheetPileQuayWall.Dynamo > SpqwGeometryNodes
    public static class SpqwGeometryNodes
    {
        // ノード: SpqwGeometryNodes.CreateAnchorPileSolid
        // 控え杭(本管 + 閉端時は底板)のソリッドを生成する。継手を持たない単独杭。
        public static Autodesk.DesignScript.Geometry.Solid CreateAnchorPileSolid(
            Autodesk.DesignScript.Geometry.Point headPoint,
            double D_mm = 800.0,
            double t_mm = 12.0,
            double L_m = 20.0,
            double inclDeg = 0.0,
            bool closedTip = false)
        {
            // 外径・肉厚・全長は控え杭の規則(JIS A 5525 標準径スナップ + K011 径別肉厚、
            // AnchorPileSteel)で検証する。前壁用の InputValidator(K011 一律)とは範囲が
            // 異なるため使わない(SPQW_ANCHORPILE_Create・CSV 取り込みと同じ経路)。
            double D_m = SheetPileQuayWall.Core.AnchorPile.AnchorPileSteel.SnapToJis(
                D_mm / 1000.0);
            double t_m = t_mm / 1000.0;

            string? errD = SheetPileQuayWall.Core.AnchorPile.AnchorPileSteel.ValidateD(D_m);
            if (errD != null)
            {
                throw new System.ArgumentException(errD, nameof(D_mm));
            }
            string? errT = SheetPileQuayWall.Core.AnchorPile.AnchorPileSteel.ValidateT(t_m, D_m);
            if (errT != null)
            {
                throw new System.ArgumentException(errT, nameof(t_mm));
            }
            string? errL = SheetPileQuayWall.Core.AnchorPile.AnchorPileSteel.ValidateL(L_m);
            if (errL != null)
            {
                throw new System.ArgumentException(errL, nameof(L_m));
            }
            if (inclDeg < SheetPileQuayWall.Core.FrontWall.FrontWallPlacement.Incl_Min_Deg ||
                inclDeg > SheetPileQuayWall.Core.FrontWall.FrontWallPlacement.Incl_Max_Deg)
            {
                throw new System.ArgumentException(
                    $"傾斜角 θ={inclDeg:F1}度 は範囲外 " +
                    $"({SheetPileQuayWall.Core.FrontWall.FrontWallPlacement.Incl_Min_Deg:F0}〜" +
                    $"{SheetPileQuayWall.Core.FrontWall.FrontWallPlacement.Incl_Max_Deg:F0}度)。",
                    nameof(inclDeg));
            }

            double outerR_m = D_m / 2.0;
            double innerR_m = (D_m - 2.0 * t_m) / 2.0;

            Autodesk.DesignScript.Geometry.Solid pile = HollowCylinder(outerR_m, innerR_m, L_m);

            if (closedTip)
            {
                // 底板は Z=0〜+t で生成されるため −t ずらして先端下へ配置する
                // (AnchorPileCommands.BuildSolid と同じ配置規約)
                Autodesk.DesignScript.Geometry.Point plateOrigin =
                    Autodesk.DesignScript.Geometry.Point.ByCoordinates(0.0, 0.0, -t_m);
                Autodesk.DesignScript.Geometry.Vector plateAxis =
                    Autodesk.DesignScript.Geometry.Vector.ZAxis();
                Autodesk.DesignScript.Geometry.Circle plateCircle =
                    Autodesk.DesignScript.Geometry.Circle.ByCenterPointRadiusNormal(
                        plateOrigin, outerR_m, plateAxis);
                Autodesk.DesignScript.Geometry.Solid plate = plateCircle.ExtrudeAsSolid(t_m);

                Autodesk.DesignScript.Geometry.Solid united =
                    (Autodesk.DesignScript.Geometry.Solid)pile.Union(plate);
                pile.Dispose();
                plateOrigin.Dispose();
                plateAxis.Dispose();
                plateCircle.Dispose();
                plate.Dispose();
                pile = united;
            }

            SheetPileQuayWall.Core.Point3 headCore = new SheetPileQuayWall.Core.Point3(
                headPoint.X, headPoint.Y, headPoint.Z);
            SheetPileQuayWall.Core.Point3 tipCore =
                SheetPileQuayWall.Core.PileGeometry.TipFromHead(headCore, L_m, inclDeg);

            return PlaceAtTip(pile, inclDeg, tipCore);
        }

        // ノード: SpqwGeometryNodes.CreateFrontWallPileSolid
        // 前壁鋼管矢板の本体円筒のみを生成する(θ=0 固定、継手は未実装)。
        public static Autodesk.DesignScript.Geometry.Solid CreateFrontWallPileSolid(
            Autodesk.DesignScript.Geometry.Point headPoint,
            double D_mm = 800.0,
            double t_mm = 12.0,
            double L_m = 20.0)
        {
            double D_m = D_mm / 1000.0;
            double t_m = t_mm / 1000.0;

            ValidateDTL(D_m, t_m, L_m);

            double outerR_m = D_m / 2.0;
            double innerR_m = (D_m - 2.0 * t_m) / 2.0;

            Autodesk.DesignScript.Geometry.Solid pile = HollowCylinder(outerR_m, innerR_m, L_m);

            SheetPileQuayWall.Core.Point3 headCore = new SheetPileQuayWall.Core.Point3(
                headPoint.X, headPoint.Y, headPoint.Z);
            SheetPileQuayWall.Core.Point3 tipCore =
                SheetPileQuayWall.Core.PileGeometry.TipFromHead(headCore, L_m, 0.0);

            return PlaceAtTip(pile, 0.0, tipCore);
        }

        // 前壁専用(InputValidator は K011 一律の前壁規則。控え杭は AnchorPileSteel を使う)。
        // ArgumentException の ParamName はノードの引数名(D_mm/t_mm/L_m)に合わせる。
        private static void ValidateDTL(double D_m, double t_m, double L_m)
        {
            string? errD = SheetPileQuayWall.Core.FrontWall.InputValidator.ValidateD(D_m);
            if (errD != null)
            {
                throw new System.ArgumentException(errD, "D_mm");
            }
            string? errT = SheetPileQuayWall.Core.FrontWall.InputValidator.ValidateT(t_m, D_m);
            if (errT != null)
            {
                throw new System.ArgumentException(errT, "t_mm");
            }
            string? errL = SheetPileQuayWall.Core.FrontWall.InputValidator.ValidateL(L_m);
            if (errL != null)
            {
                throw new System.ArgumentException(errL, "L_m");
            }
        }

        // 中空円筒(ローカル座標、Z=0〜Z=+height_m)。Circle → ExtrudeAsSolid → Difference。
        private static Autodesk.DesignScript.Geometry.Solid HollowCylinder(
            double outerR_m, double innerR_m, double height_m)
        {
            Autodesk.DesignScript.Geometry.Point origin =
                Autodesk.DesignScript.Geometry.Point.ByCoordinates(0.0, 0.0, 0.0);
            Autodesk.DesignScript.Geometry.Vector zAxis =
                Autodesk.DesignScript.Geometry.Vector.ZAxis();

            Autodesk.DesignScript.Geometry.Circle outerCircle =
                Autodesk.DesignScript.Geometry.Circle.ByCenterPointRadiusNormal(
                    origin, outerR_m, zAxis);
            Autodesk.DesignScript.Geometry.Circle innerCircle =
                Autodesk.DesignScript.Geometry.Circle.ByCenterPointRadiusNormal(
                    origin, innerR_m, zAxis);

            Autodesk.DesignScript.Geometry.Solid outerSolid = outerCircle.ExtrudeAsSolid(height_m);
            Autodesk.DesignScript.Geometry.Solid innerSolid = innerCircle.ExtrudeAsSolid(height_m);

            Autodesk.DesignScript.Geometry.Solid hollow =
                (Autodesk.DesignScript.Geometry.Solid)outerSolid.Difference(innerSolid);

            origin.Dispose();
            zAxis.Dispose();
            outerCircle.Dispose();
            innerCircle.Dispose();
            outerSolid.Dispose();
            innerSolid.Dispose();

            return hollow;
        }

        // 配置: 回転(Y軸まわり θ、ローカル原点中心) → 杭先端(tip)への平行移動。
        // Core の PileGeometry.LocalToWorld と同じ変換順序でなければならない。
        private static Autodesk.DesignScript.Geometry.Solid PlaceAtTip(
            Autodesk.DesignScript.Geometry.Solid local, double inclDeg,
            SheetPileQuayWall.Core.Point3 tip)
        {
            Autodesk.DesignScript.Geometry.Solid rotated = local;
            if (System.Math.Abs(inclDeg) > 0.001)
            {
                Autodesk.DesignScript.Geometry.Point rotOrigin =
                    Autodesk.DesignScript.Geometry.Point.Origin();
                Autodesk.DesignScript.Geometry.Vector yAxis =
                    Autodesk.DesignScript.Geometry.Vector.YAxis();

                rotated = (Autodesk.DesignScript.Geometry.Solid)local.Rotate(
                    rotOrigin, yAxis, inclDeg);
                local.Dispose();
                rotOrigin.Dispose();
                yAxis.Dispose();
            }

            Autodesk.DesignScript.Geometry.Solid placed =
                (Autodesk.DesignScript.Geometry.Solid)rotated.Translate(tip.X, tip.Y, tip.Z);
            rotated.Dispose();

            return placed;
        }

        // ノード: SpqwGeometryNodes.CreateTieRodSolid
        // タイロッド(直杭、法線直角方向の軸線に沿った円柱)を組数ぶん生成する。
        // 前壁は選択せず、海側鋼管矢板径・矢板ピッチ・海側取付点 X を明示引数で受け取る。
        //
        // 2026-08-01: 引数 tieSpacing_m / tieCount を pieceCount / everyNPiles へ変更した
        // (破壊的変更)。壁の 1 本目と最終 pieceCount 本目に必ず配置し、間を
        // everyNPiles 本ごとに割り付ける — AutoCAD 版 SPQW_TIEROD_Create と同じ規則。
        // positionY_m は壁の 1 本目の矢板中心 Y を渡すこと。
        public static Autodesk.DesignScript.Geometry.Solid[] CreateTieRodSolid(
            double baseX_m = 0.0,
            double positionY_m = 0.0,
            double rodDiameter_m = 0.048,
            double spanLength_m = 10.000,
            double pileDiameter_m = 1.000,
            double pilePitch_m = 1.200,
            int pieceCount = 10,
            int everyNPiles = 1,
            double hwl_m = 1.000,
            double tieElevation_m = 1.500)
        {
            SheetPileQuayWall.Core.TieRod.TieRodParameters p =
                new SheetPileQuayWall.Core.TieRod.TieRodParameters();
            p.RodDiameter = rodDiameter_m;
            p.SpanLength = spanLength_m;
            p.PileDiameter = pileDiameter_m;
            p.PilePitch = pilePitch_m;
            p.TieSpacing = SheetPileQuayWall.Core.TieRod.TieRodPitch.SpacingFor(
                pilePitch_m, everyNPiles);
            p.TieCount = SheetPileQuayWall.Core.TieRod.TieRodPitch.CountFor(
                pieceCount, everyNPiles);
            p.Hwl = hwl_m;
            p.TieElevation = tieElevation_m;
            p.ApplyStandardNutHeight();

            // TieRodCalculator.Compute が内部で Validate() を実行し、違反があれば
            // ArgumentException を投げる(SpqwNodes と同じエラー動作の規約)。
            SheetPileQuayWall.Core.TieRod.TieRodResult r =
                SheetPileQuayWall.Core.TieRod.TieRodCalculator.Compute(p);

            double radius_m = r.NominalDiameter / 2.0;
            double seaX = baseX_m + r.SeaEndX;

            // 配置は TieRodResult.RodPositionsY(008 由来の等間隔)ではなく TieRodPitch を
            // 使う。両端固定の割付では最終スパンだけ間隔が変わるため
            double[] offsetsY = SheetPileQuayWall.Core.TieRod.TieRodPitch.OffsetsY(
                pieceCount, everyNPiles, pilePitch_m);

            Autodesk.DesignScript.Geometry.Solid[] rods =
                new Autodesk.DesignScript.Geometry.Solid[offsetsY.Length];
            for (int i = 0; i < offsetsY.Length; i++)
            {
                double rodY = positionY_m + offsetsY[i];
                rods[i] = PlaceTieRod(radius_m, r.TotalLength, seaX, rodY, r.AxisZ);
            }
            return rods;
        }

        // ローカルで Z 軸沿いの円柱(Z=0〜+L)を生成し、Y 軸まわり +90° 回転で
        // X 軸沿い(X=0〜+L)へ倒してから (x_m, y_m, z_m) へ平行移動する。
        // ローカル原点 = 海側端のため、x_m には海側端 X(baseX + SeaEndX)を渡すこと。
        // AutoCAD 版 TieRodCommands.BuildSolid は CreateFrustum が原点中心(−L/2〜+L/2)の
        // 円柱を作るため中点 midX へ移動するが、こちらは片側押し出しなので原点の規約が異なる。
        private static Autodesk.DesignScript.Geometry.Solid PlaceTieRod(
            double radius_m, double length_m, double x_m, double y_m, double z_m)
        {
            Autodesk.DesignScript.Geometry.Point origin =
                Autodesk.DesignScript.Geometry.Point.ByCoordinates(0.0, 0.0, 0.0);
            Autodesk.DesignScript.Geometry.Vector zAxis =
                Autodesk.DesignScript.Geometry.Vector.ZAxis();
            Autodesk.DesignScript.Geometry.Circle circle =
                Autodesk.DesignScript.Geometry.Circle.ByCenterPointRadiusNormal(
                    origin, radius_m, zAxis);
            Autodesk.DesignScript.Geometry.Solid local = circle.ExtrudeAsSolid(length_m);

            Autodesk.DesignScript.Geometry.Point rotOrigin =
                Autodesk.DesignScript.Geometry.Point.Origin();
            Autodesk.DesignScript.Geometry.Vector yAxis =
                Autodesk.DesignScript.Geometry.Vector.YAxis();
            Autodesk.DesignScript.Geometry.Solid rotated =
                (Autodesk.DesignScript.Geometry.Solid)local.Rotate(rotOrigin, yAxis, 90.0);

            Autodesk.DesignScript.Geometry.Solid placed =
                (Autodesk.DesignScript.Geometry.Solid)rotated.Translate(x_m, y_m, z_m);

            origin.Dispose();
            zAxis.Dispose();
            circle.Dispose();
            local.Dispose();
            rotOrigin.Dispose();
            yAxis.Dispose();
            rotated.Dispose();

            return placed;
        }
    }
}
