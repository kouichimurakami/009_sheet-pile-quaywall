// 継手部材の側別質量 [kg/m]
//
// 移植元 007 の JointCatalog.JointMassPerM は Angle + Tee + Pipe を単純に足すため、
// **P-P 形で鋼管を 1 本分しか数えていない**(同関数の doc は「1組 = オス側＋メス側部材」
// と書いており、L-T 形・P-T 形では正しく両側を合計している)。
//
// JointShapes の実形状(DXF 抽出データ)を読むと側別の部材が確定する:
//   A 側(+Y、雌): LT = 山形鋼×2 / PP・PT = 鋼管 φ165.2×9
//                 (CurvesA の PP・PT はいずれも外半径 0.0826 m・内半径 0.0736 m の円弧)
//   B 側(−Y、雄): LT・PT = T 形鋼 / PP = 鋼管 φ165.2×9
//                 (CurvesB の PP は A 側と同じ円弧、PT は T-76×85×9×9 の直線群)
// したがって P-P 形の 1 接続は鋼管 2 本 = 69.4 kg/m であり、34.7 kg/m は片側分にすぎない。
//
// 移植元のファイルを直接修正すると scripts/port-from-legacy.sh の再実行で失われるため、
// 009 側の新規モジュールとして側別質量を定義し、積算はこちらを使う。
// 007 側の修正は別途行う(docs/implementation-plan.md §12 項目7)。

namespace SheetPileQuayWall.Core.FrontWall
{
    public static class JointMass
    {
        // A 側(+Y、後続の矢板を受ける側)の継手部材質量 [kg/m]
        public static double SideA_kgPerM(JointType jointType)
        {
            AngleSteel? angle = JointCatalog.Angle(jointType);
            if (angle != null)
            {
                return angle.MassKgPerM;   // 山形鋼×2 (カタログ値が 2 本分)
            }

            PipeJoint? pipe = JointCatalog.Pipe(jointType);
            if (pipe != null)
            {
                return pipe.MassKgPerM;    // PP・PT の A 側は鋼管
            }

            throw new System.ArgumentOutOfRangeException(nameof(jointType));
        }

        // B 側(−Y、先行して打設済みの矢板と嵌合する側)の継手部材質量 [kg/m]
        public static double SideB_kgPerM(JointType jointType)
        {
            if (JointCatalog.Form(jointType) == JointForm.PP)
            {
                PipeJoint? pipe = JointCatalog.Pipe(jointType);
                if (pipe == null)
                {
                    throw new System.ArgumentOutOfRangeException(nameof(jointType));
                }
                return pipe.MassKgPerM;    // P-P 形は B 側も鋼管
            }

            TeeSteel? tee = JointCatalog.Tee(jointType);
            if (tee == null)
            {
                throw new System.ArgumentOutOfRangeException(nameof(jointType));
            }
            return tee.MassKgPerM;         // LT・PT の B 側は T 形鋼
        }

        // 矢板 1 本が背負う継手質量 [kg/m]。継手の要否は施工順位から決まる。
        public static double PerPile_kgPerM(JointType jointType, PieceJoints joints)
        {
            double mass = 0.0;
            if (joints.HasTrailingJoint) mass += SideA_kgPerM(jointType);
            if (joints.HasLeadingJoint) mass += SideB_kgPerM(jointType);
            return mass;
        }

        // 継手 1 接続(隣接 2 本の嵌合)あたりの質量 [kg/m]。
        // JointCatalog.JointMassPerM と異なり P-P 形でも鋼管 2 本分を返す。
        public static double PerConnection_kgPerM(JointType jointType)
        {
            return SideA_kgPerM(jointType) + SideB_kgPerM(jointType);
        }
    }
}
