using PKHeX.Core;

namespace UltimateEditor;

public sealed record CatalogReport(string Version, string CoreVersion, CatalogScope Mode, string Scope,
    IReadOnlyList<Route> Selected, IReadOnlyList<Exclusion> Excluded);

public static class EncounterCatalog
{
    public static CatalogReport Build(GameVersion version, CatalogScope scope = CatalogScope.Region)
    {
        if (!Enum.IsDefined(scope)) throw new ArgumentOutOfRangeException(nameof(scope));
        var profile = GameProfile.For(version);
        var routes = new List<Route>();
        var ancestors = new Dictionary<ushort, HashSet<ushort>>();
        var index = 0;
        // Always resolve wild ancestry against the entire game's catalog, before scope filtering.
        for (ushort species = 1; species <= profile.MaxSpecies; species++)
        {
            ancestors[species] = EvolutionTree.Evolves3.Reverse.GetPreEvolutions(species, 0)
                .Select(x => x.Species).Where(x => x != species).ToHashSet();
            EvoCriteria[] chain = [new() { Species = species, LevelMin = 1, LevelMax = 100 }];
            var candidates = EncounterGenerator3.Instance.GetPossible(new PK3(), chain, version,
                EncounterTypeGroup.Slot | EncounterTypeGroup.Static | EncounterTypeGroup.Trade);
            foreach (var e in candidates)
            {
                var kind = e switch
                {
                    EncounterSlot3 => RouteKind.Wild,
                    EncounterTrade3 => RouteKind.Trade,
                    EncounterStatic3 s when s.IsEgg => RouteKind.GiftEgg,
                    EncounterStatic3 s when s.FixedBall == Ball.Poke => RouteKind.Gift,
                    EncounterStatic3 => RouteKind.Wild,
                    _ => RouteKind.Unknown,
                };
                var method = e is EncounterSlot3 slot ? slot.Type.ToString() : e.GetType().Name;
                var requiresEvent = e is EncounterStatic3 { FatefulEncounter: true };
                routes.Add(new($"{version}-{index++:D5}", e.Species, e.Form, version.ToString(), kind,
                    e.LevelMin, e.LevelMax, e.Location, method, e.LongName, requiresEvent));
            }
        }
        var result = SelectionPolicy.Select(routes, version.ToString(), ancestors, 1, profile.MaxSpecies,
            scope == CatalogScope.Region ? profile.RegionalSpecies : null);
        return new(version.ToString(), typeof(PKM).Assembly.GetName().Version?.ToString() ?? "unknown",
            scope,
            (scope == CatalogScope.Region ? $"해당 지방 ({profile.RegionName} 도감)" : "해당 게임 (전체 입수 후보)") +
            "; no breeding or evolution expansion; direct event distributions pending verification; trade levels are template bounds, not generation-ready levels",
            result.Selected, result.Excluded);
    }
}
