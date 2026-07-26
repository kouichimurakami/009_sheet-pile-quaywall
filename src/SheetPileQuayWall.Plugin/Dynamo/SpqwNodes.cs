// === 参照DLLバージョン: 未検証 ===
// DynamoServices.dll : 未検証 (期待値 3.3.x.x)  $(AcadRoot)\C3D\Dynamo\Core\
// 検証日: 未実施 / 検証コマンド: scripts/verify-dll-versions.ps1 → exit 2 (未検出)
// リスク: TypeLoadException / MissingMethodException が実行時に発生する可能性がある。
//
// Dynamo Zero Touch Nodes(Civil 3D 2025 同梱 Dynamo 3.3)
// 移植元: 007 SpspNodes.CalcSection。
//
// 対象範囲は前壁の断面性能ノードのみ(§12 項目3 の決定)。ジオメトリ生成ノード
// (007 SpspNodes.CreateSolid 相当)は AutoCAD のトランザクションを伴い実機でしか
// 検証できないため、この環境では移植しない。
//
// 戻り値は [MultiReturn] で辞書キーに日本語を用いる(CLAUDE.PRIVATE.md §2.1)。
// 入力は実務の呼び径慣行に合わせ mm 呼称で受け、直後に m へ変換する(決定7)。

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
    }
}
