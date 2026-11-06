using PKHeX.Core;

namespace UltimateEditor;

public sealed record CatalogReport(string Version, string CoreVersion, string Scope,
    IReadOnlyList<Route> Selected, IReadOnlyList<Exclusion> Excluded);

public static class EncounterCatalog
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<GameVersion, CatalogReport> Cache = new();
    public static CatalogReport Build(GameVersion version) => Cache.GetOrAdd(version, BuildCore);
    private static CatalogReport BuildCore(GameVersion version)
    {
        var profile = GameProfile.For(version);
        var routes = new List<Route>();
        var ancestors = new Dictionary<ushort, HashSet<ushort>>();
        var index = 0;
        var tree = EvolutionTree.GetEvolutionTree(profile.Context);
        var generator = EncounterGenerator.GetGenerator(version, profile.Generation);
        var sv = version is GameVersion.SL or GameVersion.VL or GameVersion.ZA ? ScarletVioletCatalog.Load(version) : null;
        PKM template = EntityBlank.GetBlank(profile.Context);
        template.Version = version;
        // Entire game catalog, including older-generation species and postgame areas.
        for (ushort species = 1; species <= profile.MaxSpecies; species++)
        {
            ancestors[species] = tree.Reverse.GetPreEvolutions(species, 0)
                .Select(x => x.Species).Where(x => x != species).ToHashSet();
            EvoCriteria[] chain = [new() { Species = species, LevelMin = 1, LevelMax = 100 }];
            var candidates = sv is not null ? sv[species] : generator.GetPossible(template, chain, version,
                EncounterTypeGroup.Slot | EncounterTypeGroup.Static | EncounterTypeGroup.Trade);
            foreach (var e in candidates)
            {
                var kind = EncounterAdapter.Classify(e);
                if (!e.Version.Contains(version)) kind = RouteKind.Unavailable;
                if (e is EncounterTera9 raid)
                {
                    if (raid.IsDistribution) kind = RouteKind.Event;
                    else if (version == GameVersion.SL && !raid.IsAvailableHostScarlet ||
                             version == GameVersion.VL && !raid.IsAvailableHostViolet)
                        kind = RouteKind.Unavailable;
                }
                var method = EncounterAdapter.Method(e);
                var requiresEvent = e is IFatefulEncounterReadOnly { FatefulEncounter: true };
                routes.Add(new($"{version}-{index++:D5}", e.Species, e.Form, version.ToString(), kind,
                    e.LevelMin, e.LevelMax, e.Location, method, e.LongName, requiresEvent, e.EggLocation));
            }
        }
        var result = SelectionPolicy.Select(routes, version.ToString(), ancestors, 1, profile.MaxSpecies);
        return new(version.ToString(), typeof(PKM).Assembly.GetName().Version?.ToString() ?? "unknown",
            "해당 게임 전체 입수 후보" +
            "; no breeding or evolution expansion; direct event distributions pending verification; trade levels are template bounds, not generation-ready levels",
            result.Selected, result.Excluded);
    }
}
