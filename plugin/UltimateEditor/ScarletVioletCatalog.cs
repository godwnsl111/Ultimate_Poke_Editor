using System.Reflection;
using PKHeX.Core;

namespace UltimateEditor;

// PKHeX 26.10.18's Gen9/9a possible iterators fail to persist some states,
// causing repeated entries. Read the same pinned tables without modifying host/vendor source.
// Missing fields fail explicitly on incompatible PKHeX upgrades.
internal static class ScarletVioletCatalog
{
    public static ILookup<ushort, IEncounterable> Load(GameVersion version)
    {
        var za = version == GameVersion.ZA;
        var type = typeof(PKM).Assembly.GetType(za ? "PKHeX.Core.Encounters9a" : "PKHeX.Core.Encounters9", throwOnError: true)!;
        object Read(string name) => type.GetField(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(null)
            ?? throw new NotSupportedException($"PKHeX SV 데이터 호환 오류: {name}");
        var fields = za ? new[] { "Trades", "Static", "Gifts" } : new[] { "TradeGift_SV", version == GameVersion.SL ? "StaticSL" : "StaticVL", "Encounter_SV", "Fixed",
            "TeraBase", "TeraDLC1", "TeraDLC2", "Dist", "Outbreak", "Might" };
        var result = new List<IEncounterable>();
        foreach (var field in fields) result.AddRange(((System.Collections.IEnumerable)Read(field)).Cast<IEncounterable>());
        if (za)
        {
            foreach (var field in new[] { "Slots", "Hyperspace" })
                foreach (var area in (EncounterArea9a[])Read(field)) result.AddRange(area.Slots);
        }
        else foreach (var area in (EncounterArea9[])Read("Slots")) result.AddRange(area.Slots);
        return result.ToLookup(e => e.Species);
    }
}
