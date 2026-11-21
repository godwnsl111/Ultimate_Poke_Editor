using PKHeX.Core;
using UltimateEditor;

static class SymbolEncounterTests
{
    static void Check(bool value, string message)
    { if (!value) throw new Exception(message); Console.WriteLine("PASS SYMBOL: " + message); }
    public static void Run(string whitePath, string white2Path)
    {
        foreach (var version in new[] { GameVersion.B, GameVersion.W, GameVersion.B2, GameVersion.W2 })
        {
            var report = EncounterCatalog.Build(version);
            byte level = version is GameVersion.B or GameVersion.W ? (byte)70 : (byte)35;
            var volcarona = report.Selected.Single(r => r.Species == 637 && r.Location == 35 && r.MinLevel == level && r.IsSymbol);
            Check(volcarona.Kind == RouteKind.Wild && EncounterCatalog.GetTemplate(volcarona) is EncounterStatic5,
                version + " Relic Castle Volcarona retained under wild captures");
            if (version is GameVersion.B2 or GameVersion.W2)
            {
                var haxorus = report.Selected.Single(r => r.Species == 612 && r.Location == 147 && r.IsSymbol);
                Check(haxorus.Kind == RouteKind.Wild && EncounterCatalog.GetTemplate(haxorus).Shiny == Shiny.Always,
                    version + " Nature Preserve Haxorus retained despite wild pre-evolution, shiny lock preserved");
            }
        }
        var baseWild = new Route("ordinary", 611, 0, "W2", RouteKind.Wild, 20, 20, 1, "Grass", "");
        var evolved = baseWild with { Id = "ordinary-evolved", Species = 612 };
        var symbol = evolved with { Id = "symbol-evolved", IsSymbol = true, MinLevel = 60, MaxLevel = 60 };
        var selected = SelectionPolicy.Select([baseWild, evolved, symbol], "W2", new Dictionary<ushort, HashSet<ushort>> { [612] = [611] }, 1, 649);
        Check(selected.Selected.Contains(symbol) && !selected.Selected.Contains(evolved), "symbol exception preserves ordinary wild evolution filtering");
        foreach (string path in new[] { whitePath, white2Path })
        {
            var save = SaveUtil.GetSaveFile(File.ReadAllBytes(path))!;
            var report = EncounterCatalog.Build(save.Version);
            var volcarona = report.Selected.First(r => r.Species == 637 && r.IsSymbol);
            Route[] routes = save is SAV5B2W2 ? [volcarona, report.Selected.Single(r => r.Species == 612 && r.Location == 147 && r.IsSymbol)] : [volcarona];
            var settings = routes.ToDictionary(r => r.Id, _ => new GenerationSettings(Shiny: 1));
            var hash = InjectionPlan.Fingerprint(save);
            var plan = InjectionPlan.Prepare(save, save.Version, routes, new Dictionary<GameVersion, TrainerProfile>(), 0, generationSettings: settings);
            Check(plan.Slots.Count == routes.Length && plan.Slots.All(s => s.Pokemon.IsShiny && new LegalityAnalysis(s.Pokemon).Valid), save.Version + " symbol generation/box roundtrip is legal");
            plan.Commit(save); plan.Undo(save);
            Check(hash.SequenceEqual(InjectionPlan.Fingerprint(save)), save.Version + " symbol injection undo preserves original");
        }
    }
}
