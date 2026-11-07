using PKHeX.Core;
using UltimateEditor;

static class DistributionDateTests
{
    static void Check(bool value, string message)
    { if (!value) throw new Exception(message); Console.WriteLine("PASS DATE: " + message); }
    static bool Rejects(Action action) { try { action(); return false; } catch (ArgumentException) { return true; } }
    public static void Run(IEnumerable<string> paths)
    {
        GenerationWorkerClient.WorkerPathOverride = Path.GetFullPath("plugin/GeneratorWorker/bin/Release/net10.0/UltimateEditor.Generator.exe");
        foreach (var group in DistributionSources.GetAll().Where(g => g.IsEntity).GroupBy(g => g.Type))
        {
            var info = group.Select(DistributionDates.Describe).ToArray();
            Console.WriteLine($"DATE COVERAGE {group.Key}: {info.Length} cards; stored {info.Count(d => d.CardDate != null)}; period {info.Count(d => d.Start != null)}; unknown {info.Count(d => d.Suggested == null)}");
        }
        var card = new WC6 { Date = new(2014, 11, 21) };
        var info6 = DistributionDates.Describe(card);
        var pk6 = new PK6();
        DistributionDates.Apply(pk6, card, new(), info6);
        Check(pk6.MetDate == card.Date && info6.Start is null && info6.Source.Contains("공식 배포 기간 미확인"), "stored card date applied without calling it an official period");
        var overrideDate = new GenerationSettings(MetDate: new(2014, 12, 1));
        DistributionDates.Apply(pk6, card, overrideDate, info6);
        Check(pk6.MetDate == overrideDate.MetDate && card.Date == new DateOnly(2014, 11, 21), "manual override does not mutate source card");
        var egg = new PK6 { IsEgg = true };
        DistributionDates.Apply(egg, card, new(), info6);
        Check(egg.IsEgg && egg.EggMetDate == card.Date && egg.MetDate == card.Date, "gift egg receipt date applied without hatching");
        var server = new WC9 { CardID = 501 };
        var serverInfo = DistributionDates.Describe(server);
        var pk9 = new PK9();
        DistributionDates.Apply(pk9, server, new(), serverInfo);
        Check(serverInfo.Start == new DateOnly(2023, 2, 16) && pk9.MetDate == serverInfo.Suggested && server.IsWithinDistributionWindow(pk9.MetDate!.Value), "native distribution window selects an allowed historical date");
        Check(Rejects(() => DistributionDates.Apply(pk9, server, new(MetDate: new(2020, 1, 1)), serverInfo)), "manual date outside known period rejected");
        var unknown = new WC8 { CardID = 65000 };
        var unknownInfo = DistributionDates.Describe(unknown);
        var pk8 = new PK8 { MetDate = new(2024, 1, 2) };
        DistributionDates.Apply(pk8, unknown, new(), unknownInfo);
        Check(unknownInfo.Suggested is null && pk8.MetDate == new DateOnly(2024, 1, 2), "missing date remains explicitly unknown and native result is preserved");
        Check(DistributionDates.ParseInput("자동") is null && DistributionDates.ParseInput("2010-05-01") == new DateOnly(2010, 5, 1), "preview automatic/manual date parsing");
        Check(Rejects(() => DistributionDates.ParseInput("2025-02-29")) && Rejects(() => new GenerationSettings(MetDate: new(2100, 1, 1)).Validate()), "invalid calendar and storage year rejected");
        Check(Rejects(() => DistributionDates.Apply(new PK3(), new EncounterStatic3(1, 5, GameVersion.FR) { Location = 88 }, overrideDate, null)), "Gen3 cannot invent an unsupported date field");
        foreach (var path in paths)
        {
            var bytes = File.ReadAllBytes(path); var save = SaveUtil.GetSaveFile(bytes)!;
            if (save.Version is not (GameVersion.Pt or GameVersion.W2)) continue;
            var candidates = DistributionCatalog.Build(save.Version).Where(r => DistributionCatalog.MatchesLanguage(r, save.Language)).ToArray();
            var route = candidates.FirstOrDefault(r => DistributionCatalog.DateInfo(r)?.Suggested != null && !DistributionCatalog.GetTemplate(r).IsEgg);
            if (route is null)
            {
                route = candidates.First(r => !DistributionCatalog.GetTemplate(r).IsEgg);
                Check(DistributionCatalog.DateInfo(route)!.Source.Contains("날짜 정보 없음"), "Gen4 missing date is reported honestly");
            }
            var hash = InjectionPlan.Fingerprint(save);
            var dates = DistributionCatalog.DateInfo(route)!;
            if (dates.Suggested is { } suggested)
            {
                var template = DistributionCatalog.GetTemplate(route);
                var basePk = template.ConvertToPKM(new SimpleTrainerInfo(save, save.Version));
                var settings = new GenerationSettings(Nature: (int)basePk.Nature);
                var auto = InjectionPlan.Prepare(save, save.Version, [route], new Dictionary<GameVersion, TrainerProfile>(), 0,
                    generationSettings: new Dictionary<string, GenerationSettings> { [route.Id] = settings });
                Check(auto.Slots[0].Pokemon.MetDate == suggested, "automatic card date survives RNG worker and save roundtrip " + save.Version);
            }
            DateOnly manualDate = dates.Start ?? new(2010, 5, 1);
            var configured = new Dictionary<string, GenerationSettings> { [route.Id] = new(MetDate: manualDate) };
            var plan = InjectionPlan.Prepare(save, save.Version, [route], new Dictionary<GameVersion, TrainerProfile>(), 0, generationSettings: configured);
            Check(plan.Slots[0].Pokemon.MetDate == manualDate && new LegalityAnalysis(plan.Slots[0].Pokemon).Valid, "manual historical date legal after roundtrip " + save.Version);
            plan.Commit(save); plan.Undo(save);
            Check(hash.SequenceEqual(InjectionPlan.Fingerprint(save)) && bytes.SequenceEqual(File.ReadAllBytes(path)), "undo and original file preserved " + save.Version);
        }
    }
}
