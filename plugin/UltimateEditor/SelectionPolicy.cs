namespace UltimateEditor;

public enum RouteKind { Wild, Gift, GiftEgg, Trade, Event, BredEgg, Unknown, Unavailable, Distribution, Linked }

public sealed record Route(
    string Id, ushort Species, byte Form, string Version, RouteKind Kind,
    byte MinLevel, byte MaxLevel, ushort Location, string Method,
    string Description, bool RequiresEvent = false, ushort EggLocation = 0,
    string DistributionKey = "", string Requirement = "", ushort RequiredSpecies = 0, bool TradeLevelKnown = false, string LinkedSource = "")
{
    public string LocationName => LocationNames.Get(this);
    public bool NeedsReview => Kind is RouteKind.Trade or RouteKind.Linked;
}

public sealed record Exclusion(Route Route, string Reason);
public sealed record SelectionResult(IReadOnlyList<Route> Selected, IReadOnlyList<Exclusion> Excluded);

public static class SelectionPolicy
{
    // The wild-presence set includes all available species, even outside the requested output range.
    // Evolution ancestry must come from the game's own evolution tree, not national-dex adjacency.
    public static SelectionResult Select(IEnumerable<Route> source, string version,
        IReadOnlyDictionary<ushort, HashSet<ushort>> ancestors, ushort first = 1, ushort last = 151,
        IReadOnlySet<ushort>? allowedSpecies = null)
    {
        if (first == 0 || first > last) throw new ArgumentException("Invalid species range.");
        var routes = source.ToArray();
        var wildSpecies = routes.Where(r => r.Version == version && r.Kind == RouteKind.Wild)
            .Select(r => r.Species).ToHashSet();
        var selected = new List<Route>();
        var excluded = new List<Exclusion>();
        var eligibleWild = new List<Route>();
        foreach (var r in routes)
        {
            string? reason = r.Version != version ? "different_version"
                : r.Species < first || r.Species > last ? "outside_species_range"
                : allowedSpecies is not null && !allowedSpecies.Contains(r.Species) ? "outside_regional_dex"
                : r.Kind == RouteKind.BredEgg ? "breeding_excluded"
                : r.Kind == RouteKind.Unknown ? "unclassified_route"
                : r.Kind == RouteKind.Unavailable ? "not_available_in_source_version"
                : null;
            if (reason is null && r.Kind == RouteKind.Wild &&
                ancestors.TryGetValue(r.Species, out var previous) && previous.Overlaps(wildSpecies))
                reason = "pre_evolution_available_in_wild";
            if (reason is not null) excluded.Add(new(r, reason));
            else if (r.Kind == RouteKind.Wild) eligibleWild.Add(r);
            else selected.Add(r); // Gifts/trades remain visible even for species also present in the wild.
        }
        foreach (var group in eligibleWild.GroupBy(r => (r.Species, r.Form)))
        {
            // Equal-level ties are stable and reproducible, not dependent on source iteration order.
            var ordered = group.OrderBy(r => r.MinLevel).ThenBy(r => r.Location)
                .ThenBy(r => r.Method, StringComparer.Ordinal).ThenBy(r => r.Id, StringComparer.Ordinal).ToArray();
            selected.Add(ordered[0]);
            excluded.AddRange(ordered.Skip(1).Select(r => new Exclusion(r, "alternative_wild_encounter")));
        }
        return new(selected.OrderBy(r => r.Species).ThenBy(r => r.Form).ThenBy(r => r.Kind)
            .ThenBy(r => r.Id, StringComparer.Ordinal).ToArray(), excluded);
    }
}
