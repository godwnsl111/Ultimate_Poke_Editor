using PKHeX.Core;
using UltimateEditor;

static class Gen7DateTests
{
    static void Check(bool value, string message)
    { if (!value) throw new Exception(message); Console.WriteLine("PASS GEN7 DATE: " + message); }
    static bool Refuses(Action action)
    { try { action(); return false; } catch (ArgumentException) { return true; } catch (InvalidOperationException) { return true; } }
    public static void Run()
    {
        GenerationWorkerClient.WorkerPathOverride = Path.GetFullPath("plugin/GeneratorWorker/bin/Release/net10.0/UltimateEditor.Generator.exe");
        var today = EncounterDate.GetDate3DS();
        var native = EncounterEvent.MGDB_G7.Select(g => g.Date).ToArray();
        var raw = DistributionSources.GetAll().OfType<WC7>().Where(g => g.IsEntity && !g.IsItem).ToArray();
        Check(raw.Any(g => g.Date is null), "original full-card missing dates remain missing");
        Check(raw.Where(g => g.Date != null).All(g => g.Date != today), "runtime-generated today stamp is absent from original card records");
        Check(EncounterEvent.MGDB_G7.Select(g => g.Date).SequenceEqual(native), "host PKHeX event database is untouched");
        Console.WriteLine($"GEN7 DATE COVERAGE: original historical dates {raw.Count(g => g.Date != null)}, missing {raw.Count(g => g.Date == null)}");

        foreach (var version in new[] { GameVersion.SN, GameVersion.US })
        {
            var save = (SAV7)BlankSaveFile.Get(version, "테스트", LanguageID.Korean);
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(save.Data[^0x1F0..], 0x42454546); // recognition marker missing from the blank fixture
            save.TID16 = 12345; save.SID16 = 54321; save.Country = 136; save.Region = 1; save.ConsoleRegion = 5;
            var trainers = new Dictionary<GameVersion, TrainerProfile>();
            var routes = DistributionCatalog.Build(version).Where(r => DistributionCatalog.MatchesLanguage(r, 8)).ToArray();
            var missing = routes.First(r => DistributionCatalog.DateInfo(r)?.NeedsExplicitDate == true && !DistributionCatalog.GetTemplate(r).IsEgg);
            var info = DistributionCatalog.DateInfo(missing)!;
            Check(info.CardDate is null && info.Suggested is null, version + " synthesized today is not advertised as a distribution date");
            var hash = InjectionPlan.Fingerprint(save);
            Check(Refuses(() => InjectionPlan.Prepare(save, version, [missing], trainers, 0)), version + " undated gift cannot silently inject today");
            var preview = InjectionPlan.Prepare(save, version, [missing], trainers, 0, allowUndatedPreview: true);
            Check(preview.HasUnconfirmedDates && new PreviewSearchSession(preview).RequiresSearch, version + " preview can open for date editing");
            Check(Refuses(() => preview.Commit(save)) && hash.SequenceEqual(InjectionPlan.Fingerprint(save)), version + " provisional date never commits");
            var date = new DateOnly(2018, 6, 1); // explicit test input, not inferred distribution metadata
            var settings = new Dictionary<string, GenerationSettings> { [missing.Id] = new(MetDate: date, Nature: (int)preview.Slots[0].Pokemon.Nature) };
            var plan = InjectionPlan.Prepare(save, version, [missing], trainers, 0, generationSettings: settings);
            Check(!plan.HasUnconfirmedDates && plan.Slots[0].Pokemon.MetDate == date, version + " explicit historical date survives storage roundtrip");
            plan.Commit(save); Check(save.GetBoxSlotAtIndex(plan.Slots[0].Box, plan.Slots[0].Slot).MetDate == date, version + " live commit keeps historical date");
            plan.Undo(save); Check(hash.SequenceEqual(InjectionPlan.Fingerprint(save)), version + " undo restores original save");
            var dated = routes.FirstOrDefault(r => DistributionCatalog.DateInfo(r)?.Suggested != null && !DistributionCatalog.GetTemplate(r).IsEgg);
            if (dated is not null)
            {
                var auto = InjectionPlan.Prepare(save, version, [dated], trainers, 0);
                Check(auto.Slots[0].Pokemon.MetDate == DistributionCatalog.DateInfo(dated)!.Suggested && auto.Slots[0].Pokemon.MetDate != today,
                    version + " actual archived date is automatic and not today");
            }
            Check(routes.Select(r => r.Id).SequenceEqual(DistributionCatalog.Build(version).Where(r => DistributionCatalog.MatchesLanguage(r, 8)).Select(r => r.Id)),
                version + " original-card IDs remain stable across rebuilds");
        }
    }
}
