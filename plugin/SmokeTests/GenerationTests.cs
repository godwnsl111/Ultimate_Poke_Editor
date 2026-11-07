using System.Text.Json;
using PKHeX.Core;
using UltimateEditor;

static class GenerationTests
{
    static void Check(bool condition, string message)
    { if (!condition) throw new Exception(message); Console.WriteLine("PASS GENERATE: " + message); }
    static GenerationSettings From(PKM pk) => new(pk.IsShiny ? 1 : 0, (int)pk.Nature,
        pk.AbilityNumber == 4 ? 2 : pk.AbilityNumber == 2 ? 1 : 0, pk.IV_HP, pk.IV_ATK, pk.IV_DEF, pk.IV_SPA, pk.IV_SPD, pk.IV_SPE, Gender: pk.Gender);
    public static void ProbeInstalledWorker(string path)
    {
        GenerationWorkerClient.WorkerPathOverride = null;
        var bytes = File.ReadAllBytes(path); var save = SaveUtil.GetSaveFile(bytes)!; save.Language = 2;
        var route = EncounterCatalog.Build(GameVersion.FR).Selected.First(r => r.Species == 1 && r.Kind == RouteKind.Gift);
        var spec = new GenerationSettings(Nature: 3);
        var result = GenerationWorkerClient.Generate(route, new SimpleTrainerInfo(save, GameVersion.FR), spec, CancellationToken.None);
        Check(spec.Matches(result.Pokemon) && result.Pokemon.ID32 == save.ID32, "installed relative worker path resolves without cwd dependency");
        Check(bytes.SequenceEqual(File.ReadAllBytes(path)), "installed probe preserves original file");
    }
    public static void Run(IEnumerable<string> paths)
    {
        Check(new GenerationSettings().TimeoutSeconds == 30, "default search deadline is 30 seconds");
        var customDeadline = new GenerationSettings(TimeoutSeconds: 45); customDeadline.Validate();
        Check(customDeadline.TimeoutSeconds == 45, "deadline remains adjustable");
        string root = Directory.GetCurrentDirectory();
        GenerationWorkerClient.WorkerPathOverride = Path.Combine(root, "plugin/GeneratorWorker/bin/Release/net10.0/UltimateEditor.Generator.exe");
        string vectors = Path.Combine(root, "vendor/pokefinder/PokeFinder-3624cb7737da247f22d3afa5265a0698f8be8fc1/Test/Util/ivtopidcalculator.json");
        using var document = JsonDocument.Parse(File.ReadAllText(vectors));
        foreach (var test in document.RootElement.GetProperty("calculatePIDs").EnumerateArray())
        {
            int[] ivs = test.GetProperty("ivs").EnumerateArray().Select(x => x.GetInt32()).ToArray();
            var actual = PokeFinderRng.Calculate(ivs, test.GetProperty("nature").GetInt32(), test.GetProperty("tid").GetUInt16()).ToHashSet();
            var expected = test.GetProperty("results").EnumerateArray().Where(x => x.GetProperty("method").GetString() != "Channel")
                .Select(x => new PidCandidate(x.GetProperty("pid").GetUInt32(), x.GetProperty("seed").GetUInt32(),
                    x.GetProperty("sid").GetUInt16(), x.GetProperty("method").GetString()!)).ToHashSet();
            Check(actual.SetEquals(expected), "PokeFinder upstream oracle " + test.GetProperty("name").GetString());
        }
        bool frTested = false, ptTested = false, radarTested = false;
        foreach (var path in paths)
        {
            var bytes = File.ReadAllBytes(path); var save = SaveUtil.GetSaveFile(bytes)!;
            if (save.Version == GameVersion.FR && !frTested)
            {
                frTested = true; save.Language = 2;
                var pidgeyRoute = EncounterCatalog.Build(GameVersion.FR).Selected.First(r => r.Species == 16 && r.Kind == RouteKind.Wild);
                var shinyBird = new GenerationSettings(Shiny: 1, Nature: 15, Gender: 0);
                var birdPlan = InjectionPlan.Prepare(save, GameVersion.FR, [pidgeyRoute], new Dictionary<GameVersion, TrainerProfile>(), 0,
                    generationSettings: new Dictionary<string, GenerationSettings> { [pidgeyRoute.Id] = shinyBird });
                var bird = birdPlan.Slots.Single().Pokemon;
                var birdAnalysis = new LegalityAnalysis(bird);
                Check(shinyBird.Matches(bird) && bird.ID32 == save.ID32 && birdAnalysis.Valid && birdAnalysis.Info.FrameMatches && birdAnalysis.Info.PIDIVMatches,
                    "shiny male modest Pidgey has a verified wild encounter frame (no origin-seed X)");
                var timer = System.Diagnostics.Stopwatch.StartNew();
                var twoV = new GenerationSettings(Shiny: 1, HP: 31, Speed: 31, TimeoutSeconds: 30);
                var fastPlan = InjectionPlan.Prepare(save, GameVersion.FR, [pidgeyRoute], new Dictionary<GameVersion, TrainerProfile>(), 0,
                    generationSettings: new Dictionary<string, GenerationSettings> { [pidgeyRoute.Id] = twoV });
                var fastBird = fastPlan.Slots.Single().Pokemon;
                Check(twoV.Matches(fastBird) && new LegalityAnalysis(fastBird).Info.FrameMatches,
                    $"shiny two-V Pidgey PID reversal: {timer.Elapsed.TotalSeconds:F3}s, verified frame");
                var caterpieRoute = EncounterCatalog.Build(GameVersion.FR).Selected.First(r => r.Species == 10 && r.Kind == RouteKind.Wild);
                var caterpieSpec = new GenerationSettings(Shiny: 1, Nature: 10, Gender: 1, SpAttack: 31, Speed: 31);
                timer.Restart();
                try
                {
                    var caterpiePlan = InjectionPlan.Prepare(save, GameVersion.FR, [caterpieRoute], new Dictionary<GameVersion, TrainerProfile>(), 0,
                        generationSettings: new Dictionary<string, GenerationSettings> { [caterpieRoute.Id] = caterpieSpec });
                    var caterpie = caterpiePlan.Slots.Single().Pokemon;
                    Check(caterpieSpec.Matches(caterpie) && new LegalityAnalysis(caterpie).Info.FrameMatches,
                        $"user Caterpie shiny/timid/female/SpA+Speed V: {timer.Elapsed.TotalSeconds:F3}s, verified frame");
                }
                catch (ArgumentException ex) when (ex.Message.Contains("H1/H2/H4"))
                {
                    Check(timer.Elapsed.TotalSeconds < 30, $"user Caterpie finite H1/H2/H4 exhaustion: {timer.Elapsed.TotalSeconds:F3}s");
                }
                PreviewTests.Run(path);
                var route = EncounterCatalog.Build(GameVersion.FR).Selected.First(r => r.Species == 1 && r.Kind == RouteKind.Gift);
                // Find a real Method-1 shiny spread using the unchanged destination ID32.
                uint seed = 0x12345678, pid = 0, iv1 = 0, iv2 = 0;
                for (int i = 0; i < 2000000; i++)
                {
                    uint s = seed;
                    uint low = LCRNG.Next16(ref s), high = LCRNG.Next16(ref s);
                    pid = low | high << 16; iv1 = LCRNG.Next16(ref s) & 32767; iv2 = LCRNG.Next16(ref s) & 32767;
                    if (ShinyUtil.GetIsShiny3(save.ID32, pid)) break;
                    seed = LCRNG.Next(seed);
                }
                Check(ShinyUtil.GetIsShiny3(save.ID32, pid), "real shiny spread found without changing TID/SID");
                var spec = new GenerationSettings(1, (int)(pid % 25), (int)(pid & 1), (int)(iv1 & 31), (int)(iv1 >> 5 & 31),
                    (int)(iv1 >> 10 & 31), (int)(iv2 >> 5 & 31), (int)(iv2 >> 10 & 31), (int)(iv2 & 31), Gender: EntityGender.GetFromPID(1, pid));
                var hash = InjectionPlan.Fingerprint(save);
                var plan = InjectionPlan.Prepare(save, GameVersion.FR, [route], new Dictionary<GameVersion, TrainerProfile>(), 0,
                    generationSettings: new Dictionary<string, GenerationSettings> { [route.Id] = spec });
                var pk = plan.Slots.Single().Pokemon;
                Check(spec.Matches(pk) && pk.ID32 == save.ID32 && plan.Slots.Single().Generator.StartsWith("PokeFinder"), "shiny/nature/6 IV/ability exact via PokeFinder with unchanged IDs");
                Check(plan.Slots.Single().Seed is not null && new LegalityAnalysis(pk).Valid, "search seed retained and real PKHeX legality passes");
                plan.Commit(save); plan.Undo(save);
                Check(hash.SequenceEqual(InjectionPlan.Fingerprint(save)), "configured generation commit and undo");
                var wild = EncounterCatalog.Build(GameVersion.FR).Selected.First(r => r.Species == 25 && r.Kind == RouteKind.Wild);
                var wildBase = EncounterCatalog.GetTemplate(wild).ConvertToPKM(new SimpleTrainerInfo(save, GameVersion.FR));
                var wildSpec = From(wildBase);
                var wildPlan = InjectionPlan.Prepare(save, GameVersion.FR, [wild], new Dictionary<GameVersion, TrainerProfile>(), 0,
                    generationSettings: new Dictionary<string, GenerationSettings> { [wild.Id] = wildSpec });
                Check(wildSpec.Matches(wildPlan.Slots.Single().Pokemon), "wild Method H exact spread retains encounter legality");
                Check(new LegalityAnalysis(wildPlan.Slots.Single().Pokemon).Info.FrameMatches, "PokeFinder exact-IV wild result also verifies the encounter frame");
                var mirror = MirrorCatalog.Build(GameVersion.FR, true).Selected.First(r => r.Species == 37 && r.Version == "LG");
                var mirrorTrainer = new TrainerProfile("LG", "LEAF", 23456, 65432, 1, 2);
                var mirrorSpec = new GenerationSettings(Shiny: 1, Nature: 10);
                var mirrorPlan = InjectionPlan.Prepare(save, GameVersion.FR, [mirror], new Dictionary<GameVersion, TrainerProfile> { [GameVersion.LG] = mirrorTrainer }, 0,
                    generationSettings: new Dictionary<string, GenerationSettings> { [mirror.Id] = mirrorSpec });
                var mirrorPk = mirrorPlan.Slots.Single().Pokemon;
                Check(mirrorPk.IsShiny && (int)mirrorPk.Nature == 10 && mirrorPk.TID16 == mirrorTrainer.TID && mirrorPk.SID16 == mirrorTrainer.SID && mirrorPk.Version == GameVersion.LG,
                    "mirror shiny calculated for source trainer and version");
                foreach (var gcSource in new[] { GameVersion.COLO, GameVersion.XD })
                {
                    var gcRoute = LinkedCatalog.Build(GameVersion.FR).First(r => r.LinkedSource == gcSource.ToString() && r.Species == (gcSource == GameVersion.COLO ? 243 : 249));
                    var gcTrainer = new TrainerProfile(gcSource.ToString(), "MICHAEL", 23456, 43210, 0, 2);
                    var gcBase = EncounterCatalog.GetTemplate(gcRoute).ConvertToPKM(new SimpleTrainerInfo(GameVersion.CXD) { OT = gcTrainer.OT, TID16 = gcTrainer.TID, SID16 = gcTrainer.SID, Language = 2 });
                    var gcSpec = From(gcBase);
                    var gcPlan = InjectionPlan.Prepare(save, GameVersion.FR, [gcRoute], new Dictionary<GameVersion, TrainerProfile>(), 0, prerequisitesReviewed: true,
                        linkedTrainers: new Dictionary<GameVersion, TrainerProfile> { [gcSource] = gcTrainer },
                        generationSettings: new Dictionary<string, GenerationSettings> { [gcRoute.Id] = gcSpec });
                    Check(gcSpec.Matches(gcPlan.Slots.Single().Pokemon) && new LegalityAnalysis(gcPlan.Slots.Single().Pokemon).Valid,
                        "GC full-IV reverse respects shadow locks and GBA transfer " + gcSource);
                }
                var dualRoute = LinkedCatalog.Build(GameVersion.FR).First(r => r.LinkedSource == "XD" && r.Species == 280);
                var dualTrainer = new TrainerProfile("XD", "MICHAEL", 23456, 43210, 0, 2);
                var dualBase = EncounterCatalog.GetTemplate(dualRoute).ConvertToPKM(new SimpleTrainerInfo(GameVersion.CXD) { OT = dualTrainer.OT, TID16 = dualTrainer.TID, SID16 = dualTrainer.SID, Language = 2 });
                var dualCandidate = PokeFinderRng.Calculate(new[] { dualBase.IV_HP, dualBase.IV_ATK, dualBase.IV_DEF, dualBase.IV_SPA, dualBase.IV_SPD, dualBase.IV_SPE },
                    (int)dualBase.Nature, dualBase.TID16).First(c => c.Method == "XDColo" && c.PID == dualBase.PID);
                int dualAbility = PokeFinderRng.GameCubeAbility(dualCandidate.Seed);
                var dualSpec = From(dualBase) with { Ability = dualAbility };
                var dualPlan = InjectionPlan.Prepare(save, GameVersion.FR, [dualRoute], new Dictionary<GameVersion, TrainerProfile>(), 0, prerequisitesReviewed: true,
                    linkedTrainers: new Dictionary<GameVersion, TrainerProfile> { [GameVersion.XD] = dualTrainer },
                    generationSettings: new Dictionary<string, GenerationSettings> { [dualRoute.Id] = dualSpec });
                Check(dualSpec.Matches(dualPlan.Slots.Single().Pokemon), "XD dual ability follows the dedicated ability RNG frame");
                var partialDual = new GenerationSettings(Ability: 1);
                var partialPlan = InjectionPlan.Prepare(save, GameVersion.FR, [dualRoute], new Dictionary<GameVersion, TrainerProfile>(), 0, prerequisitesReviewed: true,
                    linkedTrainers: new Dictionary<GameVersion, TrainerProfile> { [GameVersion.XD] = dualTrainer },
                    generationSettings: new Dictionary<string, GenerationSettings> { [dualRoute.Id] = partialDual });
                Check(partialPlan.Slots.Single().Pokemon.AbilityNumber == 2 && partialPlan.Slots.Single().Pokemon.TID16 == dualTrainer.TID,
                    "XD ability-only criteria uses source RNG and preserves trainer");
                foreach (int gender in new[] { 0, 1 })
                {
                    var genderSpec = new GenerationSettings(Gender: gender);
                    var genderPlan = InjectionPlan.Prepare(save, GameVersion.FR, [route], new Dictionary<GameVersion, TrainerProfile>(), 0,
                        generationSettings: new Dictionary<string, GenerationSettings> { [route.Id] = genderSpec });
                    Check(genderPlan.Slots[0].Pokemon.Gender == gender && new LegalityAnalysis(genderPlan.Slots[0].Pokemon).Valid,
                        "Gen3 gender-only PID search " + gender);
                }
                var impossible = new GenerationSettings(Shiny: 1, Nature: 24, HP: 31, Attack: 31, Defense: 31, TimeoutSeconds: 1);
                var slowRoute = EncounterCatalog.Build(GameVersion.FR).Selected.First(r => r.Species == 243 && r.Kind != RouteKind.Distribution);
                bool timedOut = false;
                try { GenerationWorkerClient.Generate(slowRoute, new SimpleTrainerInfo(save, GameVersion.FR), impossible, CancellationToken.None); }
                catch (TimeoutException) { timedOut = true; }
                catch (ArgumentException ex) when (ex.Message.Contains("후보")) { timedOut = true; }
                Check(timedOut && hash.SequenceEqual(InjectionPlan.Fingerprint(save)), "bounded or exhausted search preserves target");
                using var cancel = new CancellationTokenSource(300);
                bool cancelled = false;
                try { GenerationWorkerClient.Generate(slowRoute, new SimpleTrainerInfo(save, GameVersion.FR), impossible with { TimeoutSeconds = 30 }, cancel.Token); }
                catch (OperationCanceledException) { cancelled = true; }
                catch (ArgumentException ex) when (ex.Message.Contains("후보")) { cancelled = true; }
                Check(cancelled && hash.SequenceEqual(InjectionPlan.Fingerprint(save)), "cancel or early exhaustion preserves target");
                var xd = LinkedCatalog.Build(GameVersion.FR).First(r => r.LinkedSource == "XD" && r.Species == 249);
                bool locked = false;
                try { GenerationEngine.Generate(new(xd, new("FR", "MICHAEL", 12345, 54321, 0, 2), new(Shiny: 1))); }
                catch (ArgumentException ex) { locked = ex.Message.Contains("잠금"); }
                Check(locked, "XD shiny lock rejected before RNG search");
            }
            if (save.Version == GameVersion.Pt && !ptTested)
            {
                ptTested = true;
                var route = EncounterCatalog.Build(GameVersion.Pt).Selected.First(r => r.Species == 491 && r.Kind != RouteKind.Distribution);
                var baseline = EncounterCatalog.GetTemplate(route).ConvertToPKM(new SimpleTrainerInfo(save, GameVersion.Pt));
                var spec = From(baseline);
                var plan = InjectionPlan.Prepare(save, save.Version, [route], new Dictionary<GameVersion, TrainerProfile>(), 0,
                    generationSettings: new Dictionary<string, GenerationSettings> { [route.Id] = spec });
                Check(spec.Matches(plan.Slots.Single().Pokemon), "Gen4 Darkrai six-IV reversal and nature");
                Check(plan.Slots[0].Pokemon.Gender == 2, "genderless request retained in reversed result");
                bool wrongGender = false;
                try { GenerationEngine.Generate(new(route, new("Pt", "TEST", 1234, 5678, 0, 8), new(Gender: 0))); }
                catch (ArgumentException ex) { wrongGender = ex.Message.Contains("성별"); }
                Check(wrongGender, "genderless species rejects male before RNG search");
                var fixedRoute = DistributionCatalog.Build(save.Version).First(r => DistributionCatalog.MatchesLanguage(r, save.Language) &&
                    DistributionCatalog.GetTemplate(r) is PCD pcd && pcd.Gift.PK.PID != 1);
                var fixedBase = DistributionCatalog.GetTemplate(fixedRoute).ConvertToPKM(new SimpleTrainerInfo(save, save.Version));
                var conflicting = new GenerationSettings(Nature: ((int)fixedBase.Nature + 1) % 25);
                bool fixedRejected = false;
                try { GenerationWorkerClient.Generate(fixedRoute, new SimpleTrainerInfo(save, save.Version), conflicting, CancellationToken.None); }
                catch (ArgumentException) { fixedRejected = true; }
                Check(fixedRejected, "fixed Wonder Card PID/nature cannot be overwritten");
            }
            if (save is SAV5B2W2 && !radarTested)
            {
                radarTested = true;
                var route = LinkedCatalog.Build(save.Version).First(r => r.Species == 442);
                var spec = new GenerationSettings(0, 15, 2, 31, 31, 31, 31, 31, 31);
                var plan = InjectionPlan.Prepare(save, save.Version, [route], new Dictionary<GameVersion, TrainerProfile>(), 0, prerequisitesReviewed: true,
                    generationSettings: new Dictionary<string, GenerationSettings> { [route.Id] = spec });
                Check(spec.Matches(plan.Slots.Single().Pokemon) && new LegalityAnalysis(plan.Slots.Single().Pokemon).Valid, "Dream Radar six-IV nature hidden ability native generation");
            }
            Check(bytes.SequenceEqual(File.ReadAllBytes(path)), "original file unchanged " + Path.GetFileName(path));
        }
        Check(frTested && ptTested && radarTested, "real Gen3/Gen4/BW2 fixtures exercised");
    }
}
