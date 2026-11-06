using PKHeX.Core;

namespace UltimateEditor;

public sealed record MirrorReport(CatalogReport Primary, IReadOnlyList<string> MirrorVersions,
    IReadOnlyList<Route> Selected, IReadOnlyList<Exclusion> Excluded);

public sealed record TrainerProfile(string Version, string OT, ushort TID, ushort SID, int Gender, int Language);

public static class MirrorCatalog
{
    public static GameVersion[] Partners(GameVersion version)
    {
        _ = GameProfile.For(version);
        return GameProfile.Groups.Single(g => g.Contains(version)).Where(v => v != version).ToArray();
    }

    public static MirrorReport Build(GameVersion version, bool includeMirror)
    {
        var primary = EncounterCatalog.Build(version);
        if (!includeMirror) return new(primary, [], primary.Selected, primary.Excluded);
        return Merge(primary, Partners(version).Select(EncounterCatalog.Build).ToArray());
    }

    public static MirrorReport Merge(CatalogReport primary, params CatalogReport[] mirrors)
    {
        // A species suppressed by wild evolution/min-level rules is still available
        // in the primary game. Do not reintroduce it through the mirror catalog.
        var primarySpecies = primary.Selected.Select(r => r.Species).Concat(
            primary.Excluded.Where(e => e.Reason is "pre_evolution_available_in_wild" or "alternative_wild_encounter")
                .Select(e => e.Route.Species)).ToHashSet();
        var selected = primary.Selected.ToList();
        var excluded = primary.Excluded.ToList();
        var assignedSpecies = primarySpecies.ToHashSet();
        foreach (var mirror in mirrors)
        {
            excluded.AddRange(mirror.Excluded);
            // Group first: keep all candidate routes for one species in its chosen source version.
            foreach (var species in mirror.Selected.GroupBy(r => r.Species))
            {
                if (assignedSpecies.Add(species.Key)) selected.AddRange(species);
                else excluded.AddRange(species.Select(r => new Exclusion(r,
                    primarySpecies.Contains(r.Species) ? "available_in_primary_version" : "available_in_prior_mirror_version")));
            }
        }
        return new(primary, mirrors.Select(m => m.Version).ToArray(),
            selected.OrderBy(r => r.Species).ThenBy(r => r.Kind).ThenBy(r => r.Id).ToArray(), excluded);
    }
}
