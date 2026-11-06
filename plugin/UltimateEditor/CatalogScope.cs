using PKHeX.Core;

namespace UltimateEditor;

public enum CatalogScope { Region, Game }

// Regional dexes need not be contiguous national-dex ranges. Keep membership explicit
// and separate from the generation-specific encounter adapter.
public sealed record GameProfile(string RegionName, ushort MaxSpecies, IReadOnlySet<ushort> RegionalSpecies)
{
    public static GameProfile For(GameVersion version) => version switch
    {
        GameVersion.FR or GameVersion.LG => new("관동", 386,
            Enumerable.Range(1, 151).Select(x => (ushort)x).ToHashSet()),
        _ => throw new NotSupportedException("현재는 GBA 파이어레드·리프그린만 지원합니다."),
    };
}
