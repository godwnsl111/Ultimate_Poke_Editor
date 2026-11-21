using PKHeX.Core;
using UltimateEditor;

static class Gen5ShinyTests
{
    static void Check(bool value, string message)
    { if (!value) throw new Exception(message); Console.WriteLine("PASS GEN5 SHINY: " + message); }
    public static void Run(IEnumerable<string> paths)
    {
        foreach (string path in paths)
        {
            byte[] bytes = File.ReadAllBytes(path);
            var source = SaveUtil.GetSaveFile(bytes)!;
            var versions = source is SAV5B2W2 ? new[] { GameVersion.B2, GameVersion.W2 } : new[] { GameVersion.B, GameVersion.W };
            foreach (var version in versions)
            {
                var save = InjectionPlan.CloneSave(source); save.Version = version;
                var report = EncounterCatalog.Build(version);
                var routes = report.Selected.Where(r => r.Kind == RouteKind.Wild && EncounterCatalog.GetTemplate(r) is EncounterSlot5 { Shiny: Shiny.Random }).Take(3)
                    .Concat(report.Selected.Where(r => r.Kind == RouteKind.Gift && EncounterCatalog.GetTemplate(r) is EncounterStatic5 { Shiny: Shiny.Random }).Take(2)).ToArray();
                Check(routes.Length >= 4, version + " wild and gift cases available");
                var timer = System.Diagnostics.Stopwatch.StartNew();
                var options = routes.ToDictionary(r => r.Id, _ => new GenerationSettings(Shiny: 1));
                var hash = InjectionPlan.Fingerprint(save);
                var plan = InjectionPlan.Prepare(save, version, routes, new Dictionary<GameVersion, TrainerProfile>(), 0, generationSettings: options);
                Check(plan.Slots.Count == routes.Length && plan.Slots.All(s => s.Pokemon.IsShiny && s.Pokemon.ID32 == save.ID32 && new LegalityAnalysis(s.Pokemon).Valid),
                    $"{version} shiny-only wild/gift batch {routes.Length}: {timer.Elapsed.TotalSeconds:F3}s, trainer unchanged and legal");
                plan.Commit(save); plan.Undo(save);
                Check(hash.SequenceEqual(InjectionPlan.Fingerprint(save)), version + " commit/undo preserves input");
                var combined = new GenerationSettings(Shiny: 1, Nature: 10, Gender: 1, SpAttack: 31, Speed: 31);
                var wild = routes.First(r => r.Kind == RouteKind.Wild && PersonalTable.B2W2[r.Species].IsDualGender);
                var configured = InjectionPlan.Prepare(save, version, [wild], new Dictionary<GameVersion, TrainerProfile>(), 0,
                    generationSettings: new Dictionary<string, GenerationSettings> { [wild.Id] = combined });
                Check(combined.Matches(configured.Slots[0].Pokemon) && new LegalityAnalysis(configured.Slots[0].Pokemon).Valid, version + " shiny/nature/female/two-V criteria all honored");
                for (int ability = 0; ability < 2; ability++)
                {
                    var selected = combined with { Ability = ability };
                    var abilityPlan = InjectionPlan.Prepare(save, version, [wild], new Dictionary<GameVersion, TrainerProfile>(), 0,
                        generationSettings: new Dictionary<string, GenerationSettings> { [wild.Id] = selected });
                    var result = abilityPlan.Slots[0].Pokemon;
                    Check(selected.Matches(result) && MonochromeRNG.GetBitXor(result, result.PID) == 0 && new LegalityAnalysis(result).Valid,
                        version + " shiny gender/IV and ability slot " + (ability + 1) + " preserve wild PID correlation");
                }
                var locked = report.Selected.First(r => EncounterCatalog.GetTemplate(r).Shiny == Shiny.Never);
                bool blocked = false;
                try { GenerationEngine.Generate(new(locked, new(version.ToString(), save.OT, save.TID16, save.SID16, save.Gender, save.Language), new(Shiny: 1))); }
                catch (ArgumentException ex) { blocked = ex.Message.Contains("잠금"); }
                Check(blocked, version + " actual shiny lock still rejected");
                var randomCard = report.Selected.FirstOrDefault(r => r.Kind == RouteKind.Distribution &&
                    EncounterCatalog.GetTemplate(r) is PGF { PID: 0, PIDType: 1, IsEgg: false } && DistributionCatalog.MatchesLanguage(r, save.Language));
                if (randomCard is not null)
                {
                    var cardPlan = InjectionPlan.Prepare(save, version, [randomCard], new Dictionary<GameVersion, TrainerProfile>(), 0,
                        generationSettings: new Dictionary<string, GenerationSettings> { [randomCard.Id] = new(Shiny: 1) });
                    Check(cardPlan.Slots[0].Pokemon.IsShiny && new LegalityAnalysis(cardPlan.Slots[0].Pokemon).Valid, version + " random-PID Wonder Card shiny generation");
                }
            }
            Check(bytes.SequenceEqual(File.ReadAllBytes(path)), "original save file unchanged");
        }
    }
}
