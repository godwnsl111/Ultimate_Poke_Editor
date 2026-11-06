using PKHeX.Core;

namespace UltimateEditor;

public sealed record CatalogReport(string Version, string CoreVersion, string Scope,
    IReadOnlyList<Route> Selected, IReadOnlyList<Exclusion> Excluded);

public static class EncounterCatalog
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<GameVersion, CatalogReport> Cache = new();
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<(GameVersion, string), IEncounterable> Templates = new();
    public static IEncounterable GetTemplate(Route route)
    {
        if (route.Kind == RouteKind.Linked) return LinkedCatalog.GetTemplate(route);
        if (route.Kind == RouteKind.Distribution) return DistributionCatalog.GetTemplate(route);
        var version = Enum.Parse<GameVersion>(route.Version);
        var report = Build(version);
        if (!report.Selected.Contains(route) || !Templates.TryGetValue((version, route.Id), out var encounter))
            throw new ArgumentException("현재 조회 목록에 없는 입수 경로입니다. 목록을 다시 조회하세요.");
        return encounter;
    }
    public static CatalogReport Build(GameVersion version)
    {
        var native = Cache.GetOrAdd(version, BuildCore);
        return native with { Selected = native.Selected.Concat(DistributionCatalog.Build(version))
            .OrderBy(r => r.Species).ThenBy(r => r.Kind).ThenBy(r => r.Id).ToArray() };
    }
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
                var id = $"{version}-{index++:D5}";
                Templates[(version, id)] = e;
                routes.Add(new(id, e.Species, e.Form, version.ToString(), kind,
                    e.LevelMin, e.LevelMax, e.Location, method, e.LongName, requiresEvent, e.EggLocation));
            }
        }
        var adjusted = AcquisitionRequirements.Apply(version, routes);
        var result = SelectionPolicy.Select(adjusted, version.ToString(), ancestors, 1, profile.MaxSpecies);
        return new(version.ToString(), typeof(PKM).Assembly.GetName().Version?.ToString() ?? "unknown",
            "해당 게임 전체 입수 후보" +
            "; no breeding or evolution expansion; NPC/event prerequisites require review; direct distributions retain card constraints",
            result.Selected, result.Excluded);
    }
}
