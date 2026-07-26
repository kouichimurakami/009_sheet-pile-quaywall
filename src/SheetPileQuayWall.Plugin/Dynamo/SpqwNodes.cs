// === 参照DLLバージョン: 未検証 ===
// DynamoServices.dll : 未検証 (期待値 3.3.x.x)  $(AcadRoot)\C3D\Dynamo\Core\
// 検証日: 未実施 / 検証コマンド: scripts/verify-dll-versions.ps1 → exit 2 (未検出)
// リスク: TypeLoadException / MissingMethodException が実行時に発生する可能性がある。
//
// Dynamo Zero Touch Nodes(Civil 3D 2025 同梱 Dynamo 3.3)
// 移植元: 007 SpspNodes.CalcSection。
//
// 断面性能・数量集計・柱状図解析・打設歩掛積算(4系統)はいずれも AutoCAD トランザクション
// を伴わない純計算のためノード化している。ジオメトリ生成ノード(007 SpspNodes.CreateSolid
// 相当)は AutoCAD のトランザクションを伴い実機でしか検証できないため、この環境では移植しない。
//
// 戻り値は [MultiReturn] で辞書キーに日本語を用いる(CLAUDE.PRIVATE.md §2.1)。
// 入力は実務の呼び径慣行に合わせ mm 呼称で受け、直後に m へ変換する(決定7)。
//
// 打設歩掛系ノード(CalcFrontWallDriveEstimate 等)は対応する AutoCAD コマンドの
// 対話フロー・入出力を 1:1 で移植したもの。コマンド側が「エラーメッセージを表示して
// return」する箇所は、ノードでは ArgumentException を投げる形に置き換えている
// (CalcWeightedN と同じ規約)。コマンドが選択済み XData から読む外径・肉厚・全長等は、
// ノードでは明示的な引数として受け取る(XData を経由しないため)。

namespace SheetPileQuayWall.Plugin.Dynamo
{
    // Dynamo ノードカテゴリ: SheetPileQuayWall.Plugin > Dynamo
    public static class SpqwNodes
    {
        // ノード: SpqwNodes.CalcSection
        // 前壁鋼管矢板の断面性能・有効幅・継手質量を返す。
        [Autodesk.DesignScript.Runtime.MultiReturn(new[]
        {
            "断面積 A [cm2]",
            "断面係数 Z [cm3]",
            "断面2次モーメント I [cm4]",
            "単位重量 W [kg/m]",
            "本管質量 [kg]",
            "断面2次半径 i [cm]",
            "内径 d [mm]",
            "有効幅 B [mm]",
            "継手質量 (1接続) [kg/m]"
        })]
        public static System.Collections.Generic.Dictionary<string, object> CalcSection(
            double D_mm = 800.0,
            double t_mm = 12.0,
            double L_m = 20.0,
            string jointType = "LT75")
        {
            double D_m = D_mm / 1000.0;
            double t_m = t_mm / 1000.0;

            string? errD = SheetPileQuayWall.Core.FrontWall.InputValidator.ValidateD(D_m);
            if (errD != null)
            {
                throw new System.ArgumentException(errD, nameof(D_mm));
            }

            string? errT = SheetPileQuayWall.Core.FrontWall.InputValidator.ValidateT(t_m, D_m);
            if (errT != null)
            {
                throw new System.ArgumentException(errT, nameof(t_mm));
            }

            string? errL = SheetPileQuayWall.Core.FrontWall.InputValidator.ValidateL(L_m);
            if (errL != null)
            {
                throw new System.ArgumentException(errL, nameof(L_m));
            }

            SheetPileQuayWall.Core.FrontWall.JointType jt =
                SheetPileQuayWall.Core.FrontWall.JointParameters.FromCode(jointType);

            double A = SheetPileQuayWall.Core.FrontWall.SectionProperties.CalcA(D_m, t_m);
            double I = SheetPileQuayWall.Core.FrontWall.SectionProperties.CalcI(D_m, t_m);
            double Z = SheetPileQuayWall.Core.FrontWall.SectionProperties.CalcZ(D_m, t_m);
            double W = SheetPileQuayWall.Core.FrontWall.SectionProperties.CalcW(D_m, t_m);
            double i_r = SheetPileQuayWall.Core.FrontWall.SectionProperties.CalcRadius(D_m, t_m);

            double B_mm =
                SheetPileQuayWall.Core.FrontWall.JointParameters.EffectiveWidth(D_m, jt) * 1000.0;
            double d_mm = (D_m - 2.0 * t_m) * 1000.0;

            // 移植元 007 の JointCatalog.JointMassPerM は P-P 形で鋼管を 1 本分しか
            // 数えないため、側別質量を持つ JointMass を使う。
            double jointMass =
                SheetPileQuayWall.Core.FrontWall.JointMass.PerConnection_kgPerM(jt);

            return new System.Collections.Generic.Dictionary<string, object>
            {
                { "断面積 A [cm2]",            A          },
                { "断面係数 Z [cm3]",           Z          },
                { "断面2次モーメント I [cm4]",   I          },
                { "単位重量 W [kg/m]",          W          },
                { "本管質量 [kg]",              W * L_m    },
                { "断面2次半径 i [cm]",         i_r        },
                { "内径 d [mm]",                d_mm       },
                { "有効幅 B [mm]",              B_mm       },
                { "継手質量 (1接続) [kg/m]",    jointMass  }
            };
        }

        // ノード: SpqwNodes.CalcQuayWallQuantity
        // 岸壁 1 施設分の鋼材質量を集計する。
        [Autodesk.DesignScript.Runtime.MultiReturn(new[]
        {
            "施設延長 [m]",
            "継手接続数 [箇所]",
            "前壁 本管質量 [kg]",
            "前壁 継手質量 [kg]",
            "タイロッド質量 [kg]",
            "控え杭 質量 [kg]",
            "合計質量 [kg]"
        })]
        public static System.Collections.Generic.Dictionary<string, object> CalcQuayWallQuantity(
            double frontD_mm = 800.0,
            double frontT_mm = 12.0,
            double frontL_m = 20.0,
            string jointType = "LT75",
            int frontPieceCount = 10,
            int tieRodSetCount = 5,
            double tieRodMassPerSet_kg = 150.0,
            int anchorPileCount = 5,
            double anchorD_mm = 800.0,
            double anchorT_mm = 12.0,
            double anchorL_m = 18.0,
            bool anchorClosedTip = false)
        {
            SheetPileQuayWall.Core.QuayWallComposition c =
                new SheetPileQuayWall.Core.QuayWallComposition
                {
                    FrontOuterDm = frontD_mm / 1000.0,
                    FrontWallTm = frontT_mm / 1000.0,
                    FrontLengthM = frontL_m,
                    FrontJointType =
                        SheetPileQuayWall.Core.FrontWall.JointParameters.FromCode(jointType),
                    FrontPieceCount = frontPieceCount,
                    TieRodSetCount = tieRodSetCount,
                    TieRodMassPerSet = tieRodMassPerSet_kg,
                    AnchorPileCount = anchorPileCount,
                    AnchorOuterDm = anchorD_mm / 1000.0,
                    AnchorWallTm = anchorT_mm / 1000.0,
                    AnchorLengthM = anchorL_m,
                    AnchorClosedTip = anchorClosedTip
                };

            SheetPileQuayWall.Core.QuayWallQuantity q =
                SheetPileQuayWall.Core.QuayWallEstimate.Compute(c);

            return new System.Collections.Generic.Dictionary<string, object>
            {
                { "施設延長 [m]",          q.WallLengthM          },
                { "継手接続数 [箇所]",     q.JointConnectionCount },
                { "前壁 本管質量 [kg]",    q.FrontBodyKg          },
                { "前壁 継手質量 [kg]",    q.FrontJointKg         },
                { "タイロッド質量 [kg]",   q.TieRodKg             },
                { "控え杭 質量 [kg]",      q.AnchorTotalKg        },
                { "合計質量 [kg]",         q.TotalKg              }
            };
        }

        // ノード: SpqwNodes.CalcWeightedN
        // 柱状図 CSV から加重平均N値(R用・Sb用・土質区分別)と岩盤の加重平均一軸圧縮強度
        // を算出する。ジオメトリ・AutoCAD トランザクションを伴わない純計算のため、
        // 他の2ノードと同じくファイルパスを直接受け取れる(Dynamo の File Path ノード等
        // から配線する)。行の不備が1件でもあれば例外を投げて計算全体を止める
        // (AutoCAD コマンド側の CSV 取り込みと異なり、部分的な値のまま地盤条件の
        // 計算を進めると設計判断を誤るため、あえて全件成功を必須にしている)。
        [Autodesk.DesignScript.Runtime.MultiReturn(new[]
        {
            "加重平均N値 (R用、N=0連続除外)",
            "根入れ長 (R用) [m]",
            "加重平均N値 (Sb用、N≦5連続除外)",
            "根入れ長 (Sb用) [m]",
            "加重平均N値 (砂質土等)",
            "加重平均N値 (粘性土)",
            "加重平均N値 (玉石混りレキ)",
            "加重平均N値 (固結土)",
            "加重平均一軸圧縮強度 (岩盤) [N/mm2]",
            "岩盤層の除外本数 (R/Sb計算から除外)"
        })]
        public static System.Collections.Generic.Dictionary<string, object> CalcWeightedN(
            string csvPath = "")
        {
            if (string.IsNullOrEmpty(csvPath))
            {
                throw new System.ArgumentException(
                    "柱状図 CSV のファイルパスを指定してください。", nameof(csvPath));
            }
            if (!System.IO.File.Exists(csvPath))
            {
                throw new System.ArgumentException(
                    $"ファイルが見つかりません: {csvPath}", nameof(csvPath));
            }

            string csvText;
            try
            {
                // 対応エンコードは UTF-8 のみ(帳票 CSV 取り込みと同じ制約)。
                csvText = System.IO.File.ReadAllText(csvPath, System.Text.Encoding.UTF8);
            }
            catch (System.IO.IOException ex)
            {
                throw new System.ArgumentException($"ファイルを読み取れません: {ex.Message}", nameof(csvPath));
            }

            SheetPileQuayWall.Core.Import.ImportResult<SheetPileQuayWall.Core.Geotech.BoringLayer> result =
                SheetPileQuayWall.Core.Geotech.BoringLogAnalysis.Parse(csvText);

            if (result.Errors.Count > 0)
            {
                System.Text.StringBuilder message = new System.Text.StringBuilder(
                    "柱状図 CSV に不備があります: ");
                for (int i = 0; i < result.Errors.Count; i++)
                {
                    if (i > 0) { message.Append("; "); }
                    message.Append(result.Errors[i].RowNumber);
                    message.Append("行目: ");
                    message.Append(result.Errors[i].Message);
                }
                throw new System.ArgumentException(message.ToString());
            }

            var forR = SheetPileQuayWall.Core.Geotech.BoringLogAnalysis.CalcWeightedN(
                result.Rows, SheetPileQuayWall.Core.Geotech.BoringLogAnalysis.RExclusionThreshold);
            var forSb = SheetPileQuayWall.Core.Geotech.BoringLogAnalysis.CalcWeightedN(
                result.Rows, SheetPileQuayWall.Core.Geotech.BoringLogAnalysis.SbExclusionThreshold);

            double? sand = SheetPileQuayWall.Core.Geotech.BoringLogAnalysis.CalcWeightedNBySoilType(
                result.Rows, SheetPileQuayWall.Core.FrontWall.JetLayerType.SandGravel);
            double? clay = SheetPileQuayWall.Core.Geotech.BoringLogAnalysis.CalcWeightedNBySoilType(
                result.Rows, SheetPileQuayWall.Core.FrontWall.JetLayerType.Clay);
            double? cobble = SheetPileQuayWall.Core.Geotech.BoringLogAnalysis.CalcWeightedNBySoilType(
                result.Rows, SheetPileQuayWall.Core.FrontWall.JetLayerType.CobbleGravel);
            double? cemented = SheetPileQuayWall.Core.Geotech.BoringLogAnalysis.CalcWeightedNBySoilType(
                result.Rows, SheetPileQuayWall.Core.FrontWall.JetLayerType.Cemented);
            double? qu = SheetPileQuayWall.Core.Geotech.BoringLogAnalysis.CalcWeightedQu(result.Rows);

            return new System.Collections.Generic.Dictionary<string, object>
            {
                { "加重平均N値 (R用、N=0連続除外)",     forR.weightedN },
                { "根入れ長 (R用) [m]",                 forR.reckoningLength_m },
                { "加重平均N値 (Sb用、N≦5連続除外)",    forSb.weightedN },
                { "根入れ長 (Sb用) [m]",                forSb.reckoningLength_m },
                { "加重平均N値 (砂質土等)",              (object?)sand ?? "" },
                { "加重平均N値 (粘性土)",                (object?)clay ?? "" },
                { "加重平均N値 (玉石混りレキ)",          (object?)cobble ?? "" },
                { "加重平均N値 (固結土)",                (object?)cemented ?? "" },
                { "加重平均一軸圧縮強度 (岩盤) [N/mm2]", (object?)qu ?? "" },
                { "岩盤層の除外本数 (R/Sb計算から除外)", forR.excludedRockLayerCount }
            };
        }

        // ノード: SpqwNodes.CalcFrontWallDriveEstimate
        // 前壁鋼管矢板・打撃工法(4節 3-4.5)の打設歩掛積算。
        // SPQW_FRONTWALL_Estimate の対話フローを 1:1 で移植。
        [Autodesk.DesignScript.Runtime.MultiReturn(new[]
        {
            "単位重量 W [kg/m]",
            "1本当り質量 [kg]",
            "合計質量 [t]",
            "貫入抵抗値 R [kN]",
            "推奨ハンマ",
            "クローラ式杭打機",
            "クローラクレーン",
            "杭打船",
            "台船",
            "引船",
            "揚錨船",
            "潜水士船",
            "打撃速度 Sb [m/分]",
            "準備時間 Tp [分/本]",
            "打撃時間 Tb [分/本]",
            "溶接時間 Tw [分/本]",
            "打設時間 Tc [分/本]",
            "日当り打設 Q [本/日]",
            "打設日数 [日]",
            "世話役",
            "とび工",
            "普通作業員",
            "溶接工"
        })]
        public static System.Collections.Generic.Dictionary<string, object> CalcFrontWallDriveEstimate(
            double D_mm = 800.0,
            double t_mm = 12.0,
            double L_m = 20.0,
            bool isOffshore = true,
            double penetration_m = 10.0,
            int pileCount = 10,
            int nTip = 50,
            int nAvg = 20,
            int jointCountPerPile = 0,
            bool isSevereSea = false,
            bool hasObstacle = false,
            bool needCrawlerCrane = false,
            bool needTugBoat = false,
            bool needDiverVessel = false)
        {
            double D_m = D_mm / 1000.0;
            double t_m = t_mm / 1000.0;
            int D_mmRounded = (int)System.Math.Round(D_mm);
            int t_mmRounded = (int)System.Math.Round(t_mm);

            SheetPileQuayWall.Core.FrontWall.ConstructionSite site = isOffshore
                ? SheetPileQuayWall.Core.FrontWall.ConstructionSite.Offshore
                : SheetPileQuayWall.Core.FrontWall.ConstructionSite.Onshore;
            SheetPileQuayWall.Core.FrontWall.SeaCondition sea = isSevereSea
                ? SheetPileQuayWall.Core.FrontWall.SeaCondition.Severe
                : SheetPileQuayWall.Core.FrontWall.SeaCondition.Normal;
            SheetPileQuayWall.Core.FrontWall.ObstacleStatus obstacle = hasObstacle
                ? SheetPileQuayWall.Core.FrontWall.ObstacleStatus.Exists
                : SheetPileQuayWall.Core.FrontWall.ObstacleStatus.None;

            double W_kgPerM = SheetPileQuayWall.Core.FrontWall.SectionProperties.CalcW(D_m, t_m);
            double mass1_kg = W_kgPerM * L_m;
            double totalMass_t = mass1_kg * pileCount / 1000.0;

            double R = SheetPileQuayWall.Core.FrontWall.DriveEstimate.CalcR(
                D_m, penetration_m, nTip, nAvg);
            string hammer = SheetPileQuayWall.Core.FrontWall.DriveEstimate.GetHammerClass(
                mass1_kg / 1000.0, R);

            double Sb = SheetPileQuayWall.Core.FrontWall.DriveEstimate.GetSb(D_mmRounded, nAvg);
            double Tp = SheetPileQuayWall.Core.FrontWall.DriveEstimate.CalcTp(site, jointCountPerPile);
            double Tb = SheetPileQuayWall.Core.FrontWall.DriveEstimate.CalcTb(penetration_m, Sb);
            double Tw = SheetPileQuayWall.Core.FrontWall.DriveEstimate.CalcTw(
                D_mmRounded, t_mmRounded, jointCountPerPile);
            double Tc = Tp + Tb + Tw;

            double Q = SheetPileQuayWall.Core.FrontWall.DriveEstimate.CalcQ(
                site, Tc, sea, obstacle, pileCount);
            if (Q <= 0.0)
            {
                throw new System.ArgumentException(
                    "1日当り打設本数が0以下になりました。入力条件を確認してください。");
            }
            int driveDays = (int)System.Math.Ceiling(pileCount / Q);

            var labor = SheetPileQuayWall.Core.FrontWall.DriveEstimate.GetLabor(
                site, L_m, jointCountPerPile > 0, D_mmRounded);

            string crawlerDriver = "", crawlerCrane = "", pileDriverVessel = "",
                barge = "", tug = "", anchorVessel = "", diverVessel = "";
            if (!isOffshore)
            {
                crawlerDriver = SheetPileQuayWall.Core.FrontWall.DriveEquipment.GetCrawlerDriver(hammer);
                if (needCrawlerCrane)
                {
                    crawlerCrane = SheetPileQuayWall.Core.FrontWall.DriveEquipment.CrawlerCraneSpec;
                }
            }
            else
            {
                pileDriverVessel =
                    SheetPileQuayWall.Core.FrontWall.DriveEquipment.GetPileDriverVessel(hammer);
                var bargeTug = SheetPileQuayWall.Core.FrontWall.VibroEstimate.GetBargeAndTug(L_m);
                barge = bargeTug.barge;
                // 引船は「現場条件による追加船団」— 杭打船の移動が必要な場合のみ計上
                // (3-4.5-15 注1)。規格は台船とペアで決まる。
                if (needTugBoat)
                {
                    tug = bargeTug.tug;
                }
                anchorVessel = SheetPileQuayWall.Core.FrontWall.VibroEstimate.AnchorHandlingVesselSpec;
                if (needDiverVessel)
                {
                    diverVessel = SheetPileQuayWall.Core.FrontWall.VibroEstimate.DiverVesselSpec;
                }
            }

            return new System.Collections.Generic.Dictionary<string, object>
            {
                { "単位重量 W [kg/m]",      W_kgPerM },
                { "1本当り質量 [kg]",       mass1_kg },
                { "合計質量 [t]",           totalMass_t },
                { "貫入抵抗値 R [kN]",      R },
                { "推奨ハンマ",             hammer },
                { "クローラ式杭打機",       crawlerDriver },
                { "クローラクレーン",       crawlerCrane },
                { "杭打船",                 pileDriverVessel },
                { "台船",                   barge },
                { "引船",                   tug },
                { "揚錨船",                 anchorVessel },
                { "潜水士船",               diverVessel },
                { "打撃速度 Sb [m/分]",     Sb },
                { "準備時間 Tp [分/本]",    Tp },
                { "打撃時間 Tb [分/本]",    Tb },
                { "溶接時間 Tw [分/本]",    Tw },
                { "打設時間 Tc [分/本]",    Tc },
                { "日当り打設 Q [本/日]",   Q },
                { "打設日数 [日]",          driveDays },
                { "世話役",                 labor.foreman },
                { "とび工",                 labor.rigger },
                { "普通作業員",             labor.laborer },
                { "溶接工",                 labor.welder }
            };
        }

        // ノード: SpqwNodes.CalcVibroEstimate
        // 前壁鋼管矢板・振動工法・バイブロ単独(16節 3-2、海上打設のみ)の打設歩掛積算。
        // SPQW_FRONTWALL_VibroEstimate の対話フローを 1:1 で移植。
        [Autodesk.DesignScript.Runtime.MultiReturn(new[]
        {
            "本管質量 [kg]",
            "継手金物質量 [kg]",
            "1本当り合計質量 [t]",
            "本管貫入抵抗 R1 [kN]",
            "継手貫入抵抗 Rj [kN]",
            "合計貫入抵抗 R [kN]",
            "バイブロハンマ規格",
            "発動発電機",
            "起重機船・杭打船",
            "継手溶接機械台数",
            "継手溶接発電機",
            "台船",
            "引船",
            "揚錨船",
            "潜水士船",
            "準備時間 Tp [分/本]",
            "打込時間 Tb [分/本]",
            "溶接時間 Tw [分/本]",
            "打設時間 Tc [分/本]",
            "日当り打設 Q [本/日]",
            "打設日数 [日]",
            "世話役",
            "とび工",
            "普通作業員",
            "特殊作業員",
            "溶接工"
        })]
        public static System.Collections.Generic.Dictionary<string, object> CalcVibroEstimate(
            double D_mm = 800.0,
            double t_mm = 12.0,
            double L_m = 20.0,
            string jointType = "LT75",
            int pieceIndex = 1,
            int pieceCount = 10,
            double driveLength_m = 20.0,
            int pileCount = 10,
            int nTip = 50,
            int nAvg = 20,
            int jointCountPerPile = 0,
            bool isSevereSea = false,
            bool hasObstacle = false,
            bool needDiverVessel = false)
        {
            double D_m = D_mm / 1000.0;
            double t_m = t_mm / 1000.0;
            int D_mmRounded = (int)System.Math.Round(D_mm);
            int t_mmRounded = (int)System.Math.Round(t_mm);

            string? pieceErr =
                SheetPileQuayWall.Core.FrontWall.PieceAssignment.Validate(pieceIndex, pieceCount);
            if (pieceErr != null)
            {
                throw new System.ArgumentException(pieceErr);
            }

            SheetPileQuayWall.Core.FrontWall.JointType jt =
                SheetPileQuayWall.Core.FrontWall.JointParameters.FromCode(jointType);
            SheetPileQuayWall.Core.FrontWall.PieceJoints joints =
                SheetPileQuayWall.Core.FrontWall.PieceAssignment.Assign(pieceIndex, pieceCount);

            SheetPileQuayWall.Core.FrontWall.SeaCondition sea = isSevereSea
                ? SheetPileQuayWall.Core.FrontWall.SeaCondition.Severe
                : SheetPileQuayWall.Core.FrontWall.SeaCondition.Normal;
            SheetPileQuayWall.Core.FrontWall.ObstacleStatus obstacle = hasObstacle
                ? SheetPileQuayWall.Core.FrontWall.ObstacleStatus.Exists
                : SheetPileQuayWall.Core.FrontWall.ObstacleStatus.None;

            double W_kgPerM = SheetPileQuayWall.Core.FrontWall.SectionProperties.CalcW(D_m, t_m);
            double bodyMass_kg = W_kgPerM * L_m;
            double jointMass_kg =
                SheetPileQuayWall.Core.FrontWall.JointMass.PerPile_kgPerM(jt, joints) * L_m;
            double steelMass_t = (bodyMass_kg + jointMass_kg) / 1000.0;

            SheetPileQuayWall.Core.FrontWall.VibroDriveTarget target =
                SheetPileQuayWall.Core.FrontWall.VibroDriveTarget.SteelPipeSheetPile;

            double r1 = SheetPileQuayWall.Core.FrontWall.VibroEstimate.CalcR1(
                D_m, driveLength_m, nTip, nAvg);
            double rj = SheetPileQuayWall.Core.FrontWall.VibroEstimate.CalcRj(r1, target);
            double r = SheetPileQuayWall.Core.FrontWall.VibroEstimate.CalcR(
                D_m, driveLength_m, nTip, nAvg, target);

            string vibroClass = SheetPileQuayWall.Core.FrontWall.VibroEstimate.GetVibroClass(
                steelMass_t, r);
            var equipment = SheetPileQuayWall.Core.FrontWall.VibroEstimate.GetEquipment(vibroClass);

            double Tp = SheetPileQuayWall.Core.FrontWall.VibroEstimate.CalcTp(driveLength_m);
            double Tb = SheetPileQuayWall.Core.FrontWall.VibroEstimate.CalcTb(driveLength_m, target);
            double Tw = SheetPileQuayWall.Core.FrontWall.DriveEstimate.CalcTw(
                D_mmRounded, t_mmRounded, jointCountPerPile);
            double Tc = Tp + Tb + Tw;

            double Q = SheetPileQuayWall.Core.FrontWall.VibroEstimate.CalcQ(
                Tc, sea, obstacle, pileCount);
            if (Q <= 0.0)
            {
                throw new System.ArgumentException(
                    "1日当り打設本数が0以下になりました。入力条件を確認してください。");
            }
            int driveDays = (int)System.Math.Ceiling(pileCount / Q);

            bool splicing = jointCountPerPile > 0;
            var labor = SheetPileQuayWall.Core.FrontWall.VibroEstimate.GetLabor(
                target, driveLength_m, splicing, D_mmRounded);
            var weldEquipment = SheetPileQuayWall.Core.FrontWall.VibroEstimate.GetWeldEquipment(
                D_mmRounded, splicing);

            var bargeTug = SheetPileQuayWall.Core.FrontWall.VibroEstimate.GetBargeAndTug(L_m);
            string diverVessel = needDiverVessel
                ? SheetPileQuayWall.Core.FrontWall.VibroEstimate.DiverVesselSpec
                : "";

            return new System.Collections.Generic.Dictionary<string, object>
            {
                { "本管質量 [kg]",          bodyMass_kg },
                { "継手金物質量 [kg]",      jointMass_kg },
                { "1本当り合計質量 [t]",    steelMass_t },
                { "本管貫入抵抗 R1 [kN]",   r1 },
                { "継手貫入抵抗 Rj [kN]",   rj },
                { "合計貫入抵抗 R [kN]",    r },
                { "バイブロハンマ規格",     vibroClass },
                { "発動発電機",             equipment.generator },
                { "起重機船・杭打船",       equipment.craneVessel },
                { "継手溶接機械台数",       splicing ? weldEquipment.machineCount : 0 },
                { "継手溶接発電機",         splicing ? weldEquipment.generator : "" },
                { "台船",                   bargeTug.barge },
                { "引船",                   bargeTug.tug },
                { "揚錨船",                 SheetPileQuayWall.Core.FrontWall.VibroEstimate.AnchorHandlingVesselSpec },
                { "潜水士船",               diverVessel },
                { "準備時間 Tp [分/本]",    Tp },
                { "打込時間 Tb [分/本]",    Tb },
                { "溶接時間 Tw [分/本]",    Tw },
                { "打設時間 Tc [分/本]",    Tc },
                { "日当り打設 Q [本/日]",   Q },
                { "打設日数 [日]",          driveDays },
                { "世話役",                 labor.foreman },
                { "とび工",                 labor.rigger },
                { "普通作業員",             labor.laborer },
                { "特殊作業員",             labor.specialist },
                { "溶接工",                 labor.welder }
            };
        }

        // ノード: SpqwNodes.CalcVibroJetEstimate
        // 前壁鋼管矢板・振動工法・ジェット併用(16節 3-1、陸上/海上とも)の打設歩掛積算。
        // SPQW_FRONTWALL_VibroJetEstimate の対話フローを 1:1 で移植。
        [Autodesk.DesignScript.Runtime.MultiReturn(new[]
        {
            "本管質量 [kg]",
            "継手金物質量 [kg]",
            "杭1本当り質量 Wp [t]",
            "基本振幅係数 A0",
            "必要偏心モーメント K0 [Nm]",
            "バイブロハンマ規格",
            "発動発電機(バイブロ用)",
            "クレーン吊上げ荷重 Cf [t]",
            "台船",
            "引船",
            "揚錨船",
            "潜水士船",
            "ジェット使用台数",
            "噴射ノズル数",
            "発動発電機(ジェット用)",
            "水中ポンプ",
            "水中ポンプ出力 [kW]",
            "水中ポンプ台数",
            "水中ポンプ用発電機",
            "水槽容量 [m3]",
            "水槽基数",
            "γ (1m当り) [分/m]",
            "β",
            "δ",
            "ε (継手加算) [分]",
            "準備時間 Tp [分/本]",
            "打込時間 Tb [分/本]",
            "溶接時間 Tw [分/本]",
            "打設時間 Tc [分/本]",
            "日当り打設 Q [本/日]",
            "打設日数 [日]",
            "世話役",
            "とび工",
            "普通作業員",
            "特殊作業員",
            "溶接工"
        })]
        public static System.Collections.Generic.Dictionary<string, object> CalcVibroJetEstimate(
            double D_mm = 800.0,
            double t_mm = 12.0,
            double L_m = 20.0,
            string jointType = "LT75",
            int pieceIndex = 1,
            int pieceCount = 10,
            bool isOffshore = true,
            double operatingHours = 8.0,
            double driveLength_m = 20.0,
            double liftLength_m = 20.0,
            int liftCount = 1,
            int pileCount = 10,
            string soilType = "SG",
            int nAvg = 30,
            double maxCobble_mm = 100.0,
            double qu = 10.0,
            bool hasChuck = true,
            double jointLength_m = 20.0,
            int jetCount = 2,
            int nozzleCount = 6,
            bool needWaterSupply = false,
            int jointCountPerPile = 0,
            bool isSevereSea = false,
            bool hasObstacle = false,
            bool needDiverVessel = false,
            double vibroMass_t = 10.0)
        {
            double D_m = D_mm / 1000.0;
            int D_mmRounded = (int)System.Math.Round(D_mm);
            int t_mmRounded = (int)System.Math.Round(t_mm);

            string? jetErr =
                SheetPileQuayWall.Core.FrontWall.VibroJetEstimate.ValidateJetApplicability(D_m, L_m);
            if (jetErr != null)
            {
                throw new System.ArgumentException(jetErr);
            }

            // コマンド側のプロンプト範囲(1〜4)と同じ制約。範囲外を黙って通すと
            // ジェット用発電機・水中ポンプが空欄のまま積算が出てしまう。
            if (jetCount < 1 || jetCount > 4)
            {
                throw new System.ArgumentException(
                    $"ジェット使用台数 {jetCount} は範囲外です(1〜4。基準 3-16-16)。", nameof(jetCount));
            }

            string? pieceErr =
                SheetPileQuayWall.Core.FrontWall.PieceAssignment.Validate(pieceIndex, pieceCount);
            if (pieceErr != null)
            {
                throw new System.ArgumentException(pieceErr);
            }

            SheetPileQuayWall.Core.FrontWall.ConstructionSite site = isOffshore
                ? SheetPileQuayWall.Core.FrontWall.ConstructionSite.Offshore
                : SheetPileQuayWall.Core.FrontWall.ConstructionSite.Onshore;
            double effectiveOperatingHours = isOffshore ? 6.0 : operatingHours;

            SheetPileQuayWall.Core.FrontWall.JointType jt =
                SheetPileQuayWall.Core.FrontWall.JointParameters.FromCode(jointType);
            SheetPileQuayWall.Core.FrontWall.PieceJoints joints =
                SheetPileQuayWall.Core.FrontWall.PieceAssignment.Assign(pieceIndex, pieceCount);

            double bodyMass_kg =
                SheetPileQuayWall.Core.FrontWall.SectionProperties.CalcW(D_m, t_mm / 1000.0) * L_m;
            double jointMass_kg =
                SheetPileQuayWall.Core.FrontWall.JointMass.PerPile_kgPerM(jt, joints) * L_m;
            double pileMass_t = (bodyMass_kg + jointMass_kg) / 1000.0;

            SheetPileQuayWall.Core.FrontWall.JetLayerType layer = soilType switch
            {
                "CL" => SheetPileQuayWall.Core.FrontWall.JetLayerType.Clay,
                "CG" => SheetPileQuayWall.Core.FrontWall.JetLayerType.CobbleGravel,
                "CE" => SheetPileQuayWall.Core.FrontWall.JetLayerType.Cemented,
                "RK" => SheetPileQuayWall.Core.FrontWall.JetLayerType.Rock,
                _ => SheetPileQuayWall.Core.FrontWall.JetLayerType.SandGravel,
            };

            double eta = 0.0;
            if (layer == SheetPileQuayWall.Core.FrontWall.JetLayerType.CobbleGravel)
            {
                double? etaValue = SheetPileQuayWall.Core.FrontWall.VibroJetEstimate.GetEta(maxCobble_mm);
                if (etaValue == null)
                {
                    throw new System.ArgumentException(
                        "最大玉石径200mm超の地盤は基準上ηを別途定めます。本ノードでは算出できません。");
                }
                eta = etaValue.Value;
            }

            bool useQu = layer == SheetPileQuayWall.Core.FrontWall.JetLayerType.Rock;
            SheetPileQuayWall.Core.FrontWall.JetSoilType a0Soil =
                SheetPileQuayWall.Core.FrontWall.VibroJetEstimate.ToAmplitudeSoil(layer);
            double? a0 = useQu
                ? SheetPileQuayWall.Core.FrontWall.VibroJetEstimate.GetAmplitudeFactorByQu(a0Soil, qu)
                : SheetPileQuayWall.Core.FrontWall.VibroJetEstimate.GetAmplitudeFactor(a0Soil, nAvg);
            if (a0 == null)
            {
                throw new System.ArgumentException(
                    "指定した土質とN値(またはqu)の組合せは基本振幅係数表(3-16-15)に定義がありません。");
            }
            double a0Applied = hasChuck
                ? a0.Value
                : SheetPileQuayWall.Core.FrontWall.VibroJetEstimate.AdjustForNoChuck(a0.Value);

            double k0 = SheetPileQuayWall.Core.FrontWall.VibroJetEstimate.CalcK0(a0Applied, pileMass_t);
            var vibro = SheetPileQuayWall.Core.FrontWall.VibroJetEstimate.GetVibroClass(k0);
            if (vibro.generator.Length == 0)
            {
                throw new System.ArgumentException(
                    $"必要偏心モーメント K0={k0:F1}N・m が規格表(3-16-15、上限2,900)を超えています。");
            }

            double gamma = SheetPileQuayWall.Core.FrontWall.VibroJetEstimate.CalcGamma(
                layer, nAvg, qu, eta);

            double? beta = SheetPileQuayWall.Core.FrontWall.VibroJetEstimate.GetBeta(
                D_mmRounded, t_mmRounded);
            if (beta == null)
            {
                throw new System.ArgumentException(
                    $"φ{D_mmRounded}×t{t_mmRounded} は係数βの表(3-16-20)の範囲外です。");
            }

            double? delta = SheetPileQuayWall.Core.FrontWall.VibroJetEstimate.GetDelta(
                D_mmRounded, vibro.vibro);
            if (delta == null)
            {
                throw new System.ArgumentException(
                    $"φ{D_mmRounded} と {vibro.vibro} の組合せは係数δの表(3-16-21)で「−」(適用対象外)です。");
            }

            double epsilon = SheetPileQuayWall.Core.FrontWall.VibroJetEstimate.CalcEpsilon(jointLength_m);

            bool splicing = jointCountPerPile > 0;
            SheetPileQuayWall.Core.FrontWall.SeaCondition sea = isSevereSea
                ? SheetPileQuayWall.Core.FrontWall.SeaCondition.Severe
                : SheetPileQuayWall.Core.FrontWall.SeaCondition.Normal;
            SheetPileQuayWall.Core.FrontWall.ObstacleStatus obstacle = hasObstacle
                ? SheetPileQuayWall.Core.FrontWall.ObstacleStatus.Exists
                : SheetPileQuayWall.Core.FrontWall.ObstacleStatus.None;

            double Tp = SheetPileQuayWall.Core.FrontWall.VibroJetEstimate.CalcTp(liftLength_m, liftCount);
            double Tb = SheetPileQuayWall.Core.FrontWall.VibroJetEstimate.CalcTb(
                gamma, beta.Value, delta.Value, driveLength_m, epsilon);
            double Tw = SheetPileQuayWall.Core.FrontWall.DriveEstimate.CalcTw(
                D_mmRounded, t_mmRounded, jointCountPerPile);
            double Tc = Tp + Tb + Tw;

            double Q = SheetPileQuayWall.Core.FrontWall.VibroJetEstimate.CalcQ(
                site, effectiveOperatingHours, Tc, sea, obstacle, pileCount);
            if (Q <= 0.0)
            {
                throw new System.ArgumentException(
                    "1日当り打設本数が0以下になりました。入力条件を確認してください。");
            }
            int driveDays = (int)System.Math.Ceiling(pileCount / Q);

            var labor = SheetPileQuayWall.Core.FrontWall.VibroJetEstimate.GetLabor(
                site, L_m, splicing, D_mmRounded);
            double craneCapacity_t =
                SheetPileQuayWall.Core.FrontWall.VibroJetEstimate.CalcCraneCapacity(
                    vibroMass_t, pileMass_t);
            string jetGenerator =
                SheetPileQuayWall.Core.FrontWall.VibroJetEstimate.GetJetGenerator(jetCount);

            string barge = "", tug = "", anchorVessel = "", diverVessel = "";
            if (isOffshore)
            {
                var bargeTug = SheetPileQuayWall.Core.FrontWall.VibroEstimate.GetBargeAndTug(L_m);
                barge = bargeTug.barge;
                tug = bargeTug.tug;
                anchorVessel = SheetPileQuayWall.Core.FrontWall.VibroEstimate.AnchorHandlingVesselSpec;
                if (needDiverVessel)
                {
                    diverVessel = SheetPileQuayWall.Core.FrontWall.VibroEstimate.DiverVesselSpec;
                }
            }

            string waterPump = "", waterGenerator = "";
            double waterPumpKw = 0.0;
            int waterPumpCount = 0, waterTankVolume = 0, waterTankCount = 0;
            if (needWaterSupply)
            {
                var w = SheetPileQuayWall.Core.FrontWall.VibroJetEstimate.GetWaterSupply(jetCount);
                waterPump = w.pump;
                waterPumpKw = w.pumpOutput_kW;
                waterPumpCount = w.pumpCount;
                waterGenerator = w.generator;
                waterTankVolume = w.tankVolume_m3;
                waterTankCount = w.tankCount;
            }

            return new System.Collections.Generic.Dictionary<string, object>
            {
                { "本管質量 [kg]",              bodyMass_kg },
                { "継手金物質量 [kg]",          jointMass_kg },
                { "杭1本当り質量 Wp [t]",       pileMass_t },
                { "基本振幅係数 A0",            a0Applied },
                { "必要偏心モーメント K0 [Nm]", k0 },
                { "バイブロハンマ規格",         vibro.vibro },
                { "発動発電機(バイブロ用)",     vibro.generator },
                { "クレーン吊上げ荷重 Cf [t]",  craneCapacity_t },
                { "台船",                       barge },
                { "引船",                       tug },
                { "揚錨船",                     anchorVessel },
                { "潜水士船",                   diverVessel },
                { "ジェット使用台数",           jetCount },
                { "噴射ノズル数",               nozzleCount },
                { "発動発電機(ジェット用)",     jetGenerator },
                { "水中ポンプ",                 waterPump },
                { "水中ポンプ出力 [kW]",        waterPumpKw },
                { "水中ポンプ台数",             waterPumpCount },
                { "水中ポンプ用発電機",         waterGenerator },
                { "水槽容量 [m3]",              waterTankVolume },
                { "水槽基数",                   waterTankCount },
                { "γ (1m当り) [分/m]",         gamma },
                { "β",                          beta.Value },
                { "δ",                          delta.Value },
                { "ε (継手加算) [分]",          epsilon },
                { "準備時間 Tp [分/本]",        Tp },
                { "打込時間 Tb [分/本]",        Tb },
                { "溶接時間 Tw [分/本]",        Tw },
                { "打設時間 Tc [分/本]",        Tc },
                { "日当り打設 Q [本/日]",       Q },
                { "打設日数 [日]",              driveDays },
                { "世話役",                     labor.foreman },
                { "とび工",                     labor.rigger },
                { "普通作業員",                 labor.laborer },
                { "特殊作業員",                 labor.specialist },
                { "溶接工",                     labor.welder }
            };
        }

        // ノード: SpqwNodes.CalcAnchorPileDriveEstimate
        // 控え杭・打撃工法(4節 3-4.6、陸上打設のみ)の打設歩掛積算。
        // SPQW_ANCHORPILE_Estimate の対話フローを 1:1 で移植。
        [Autodesk.DesignScript.Runtime.MultiReturn(new[]
        {
            "単位重量 W [kg/m]",
            "1本当り質量 [kg]",
            "合計質量 [t]",
            "貫入抵抗値 R [kN]",
            "推奨ハンマ",
            "クローラ式杭打機",
            "クローラクレーン",
            "打撃速度 Sb [m/分]",
            "準備時間 Tp [分/本]",
            "打撃時間 Tb [分/本]",
            "溶接時間 Tw [分/本]",
            "打設時間 Tc [分/本]",
            "日当り打設 Q [本/日]",
            "打設日数 [日]",
            "世話役",
            "とび工",
            "普通作業員",
            "溶接工"
        })]
        public static System.Collections.Generic.Dictionary<string, object> CalcAnchorPileDriveEstimate(
            double D_mm = 800.0,
            double t_mm = 12.0,
            double L_m = 20.0,
            double inclDeg = 0.0,
            double penetration_m = 10.0,
            int pileCount = 1,
            int nTip = 50,
            int nAvg = 20,
            int jointCountPerPile = 0,
            bool hasObstacle = false,
            bool needCrawlerCrane = false)
        {
            double D_m = D_mm / 1000.0;
            double t_m = t_mm / 1000.0;
            int D_mmRounded = (int)System.Math.Round(D_mm);
            int t_mmRounded = (int)System.Math.Round(t_mm);

            double W_kgPerM = SheetPileQuayWall.Core.FrontWall.SectionProperties.CalcW(D_m, t_m);
            double mass1_kg = W_kgPerM * L_m;
            double totalMass_t = mass1_kg * pileCount / 1000.0;

            double R = SheetPileQuayWall.Core.FrontWall.DriveEstimate.CalcR(
                D_m, penetration_m, nTip, nAvg);
            string hammer = SheetPileQuayWall.Core.FrontWall.DriveEstimate.GetHammerClass(
                mass1_kg / 1000.0, R);

            double Sb = SheetPileQuayWall.Core.FrontWall.DriveEstimate.GetSb(D_mmRounded, nAvg);
            double Tp = SheetPileQuayWall.Core.FrontWall.DriveEstimate.CalcTp(
                SheetPileQuayWall.Core.FrontWall.ConstructionSite.Onshore, jointCountPerPile);
            double Tb = SheetPileQuayWall.Core.AnchorPile.AnchorDriveEstimate.CalcTb(
                penetration_m, Sb, inclDeg);
            double Tw = SheetPileQuayWall.Core.FrontWall.DriveEstimate.CalcTw(
                D_mmRounded, t_mmRounded, jointCountPerPile);
            double Tc = Tp + Tb + Tw;

            SheetPileQuayWall.Core.FrontWall.ObstacleStatus obstacle = hasObstacle
                ? SheetPileQuayWall.Core.FrontWall.ObstacleStatus.Exists
                : SheetPileQuayWall.Core.FrontWall.ObstacleStatus.None;
            double Q = SheetPileQuayWall.Core.FrontWall.DriveEstimate.CalcQ(
                SheetPileQuayWall.Core.FrontWall.ConstructionSite.Onshore, Tc,
                SheetPileQuayWall.Core.FrontWall.SeaCondition.Normal, obstacle, pileCount);
            if (Q <= 0.0)
            {
                throw new System.ArgumentException(
                    "1日当り打設本数が0以下になりました。入力条件を確認してください。");
            }
            int driveDays = (int)System.Math.Ceiling(pileCount / Q);

            var labor = SheetPileQuayWall.Core.AnchorPile.AnchorDriveEstimate.GetLabor(
                L_m, jointCountPerPile > 0, D_mmRounded);

            string crawlerDriver =
                SheetPileQuayWall.Core.FrontWall.DriveEquipment.GetCrawlerDriver(hammer);
            string crawlerCrane = needCrawlerCrane
                ? SheetPileQuayWall.Core.FrontWall.DriveEquipment.CrawlerCraneSpec
                : "";

            return new System.Collections.Generic.Dictionary<string, object>
            {
                { "単位重量 W [kg/m]",      W_kgPerM },
                { "1本当り質量 [kg]",       mass1_kg },
                { "合計質量 [t]",           totalMass_t },
                { "貫入抵抗値 R [kN]",      R },
                { "推奨ハンマ",             hammer },
                { "クローラ式杭打機",       crawlerDriver },
                { "クローラクレーン",       crawlerCrane },
                { "打撃速度 Sb [m/分]",     Sb },
                { "準備時間 Tp [分/本]",    Tp },
                { "打撃時間 Tb [分/本]",    Tb },
                { "溶接時間 Tw [分/本]",    Tw },
                { "打設時間 Tc [分/本]",    Tc },
                { "日当り打設 Q [本/日]",   Q },
                { "打設日数 [日]",          driveDays },
                { "世話役",                 labor.foreman },
                { "とび工",                 labor.rigger },
                { "普通作業員",             labor.laborer },
                { "溶接工",                 labor.welder }
            };
        }
    }
}
