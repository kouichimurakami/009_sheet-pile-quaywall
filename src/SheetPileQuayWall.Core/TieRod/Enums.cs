// === 参照DLLバージョン検証済み ===
// 本ファイルは AutoCAD / Civil 3D / Dynamo のアセンブリを参照しない（純計算層）。
// 参照は .NET 8.0 BCL のみ。検証対象DLLなし。
// 検証日: 2026-07-25 / 検証コマンド: scripts/verify-dll-versions.ps1

namespace SheetPileQuayWall.Core.TieRod
{
    /// <summary>鋼種。</summary>
    public enum SteelGrade
    {
        /// <summary>高張力鋼（セミハイテン）HT690。</summary>
        HT690,

        /// <summary>高張力鋼（セミハイテン）HT740。旧基準のみ。</summary>
        HT740,

        /// <summary>普通鋼 SS400（JIS G 3101）。</summary>
        SS400,

        /// <summary>普通鋼 SS490（JIS G 3101）。旧基準のみ。</summary>
        SS490
    }

    /// <summary>設計基準。</summary>
    public enum DesignCode
    {
        /// <summary>許容応力度法（鋼矢板施工指針等）。</summary>
        Allowable,

        /// <summary>部分係数法（港湾の施設の技術上の基準 平成30年5月）。</summary>
        PartialFactor
    }

    /// <summary>荷重状態。</summary>
    public enum LoadState
    {
        /// <summary>常時（許容応力度法）／永続状態（部分係数法）。</summary>
        Normal,

        /// <summary>地震時（許容応力度法）／変動状態（部分係数法）。</summary>
        Seismic
    }
}
