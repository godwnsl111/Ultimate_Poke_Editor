using System.Security.Cryptography;
using System.Text.Json;
using PKHeX.Core;
using UltimateEditor;

if (args.Length > 0 && args[0] == "--catalog-options-only") {
    GenerationWorkerClient.WorkerPathOverride = Path.GetFullPath("plugin/GeneratorWorker/bin/Release/net10.0/UltimateEditor.Generator.exe");
    CatalogOptionsTests.Run(args[1], args[2]); return;
}

if (args.Length == 0 || args[0] != "--worker-probe")
    GenerationWorkerClient.WorkerPathOverride = Path.Combine(Directory.GetCurrentDirectory(), "plugin/GeneratorWorker/bin/Release/net10.0/UltimateEditor.Generator.exe");

if (args.Length > 0 && args[0] == "--gen7-dates-only") { Gen7DateTests.Run(); return; }

if (args.Length > 0 && args[0] == "--dates-only") { DistributionDateTests.Run(args.Skip(1)); return; }

if (args.Length > 0 && args[0] == "--preview-only") { PreviewTests.Run(args[1]); return; }

if (args.Length > 0 && args[0] == "--worker-probe") { GenerationTests.ProbeInstalledWorker(args[1]); return; }

if (args.Length > 0 && args[0] == "--generation-only") { GenerationTests.Run(args.Skip(1)); return; }

if (args.Length > 0 && args[0] == "--distribution-filter-only") { DistributionFilterTests.Run(args.Skip(1)); return; }

if (args.Length > 0 && args[0] == "--language-only") { LanguageTests.Run(args.Skip(1)); return; }

if (args.Length > 0 && args[0] == "--linked-only") { LinkedTests.Run(args.Skip(1)); return; }

if (args.Length > 0 && args[0] == "--events-only")
{
    foreach (var path in args.Skip(1)) EventTests.Run(SaveUtil.GetSaveFile(File.ReadAllBytes(path))!);
    return;
}

static void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
    Console.WriteLine($"PASS: {message}");
}
static Route R(string id, ushort species, RouteKind kind, byte level, ushort location = 1,
    string version = "FR") => new(id, species, 0, version, kind, level, level, location, "test", "test");

var fixture = new[] {
    R("low", 29, RouteKind.Wild, 3), R("high", 29, RouteKind.Wild, 5),
    R("evolved", 30, RouteKind.Wild, 16), R("gift", 30, RouteKind.Gift, 20),
    R("egg", 175, RouteKind.GiftEgg, 5), R("bred", 25, RouteKind.BredEgg, 5),
    R("foreign", 27, RouteKind.Wild, 6, version: "LG"),
    R("only-evolved", 26, RouteKind.Wild, 10),
    R("tie", 29, RouteKind.Wild, 3, location: 2),
    R("event", 151, RouteKind.Event, 10),
};
var ancestors = new Dictionary<ushort, HashSet<ushort>> { [30] = [29], [26] = [25] };
var result = SelectionPolicy.Select(fixture, "FR", ancestors, 1, 386);
Check(result.Selected.Select(r => r.Id).ToHashSet().SetEquals(["low", "gift", "egg", "only-evolved", "event"]),
    "minimum wild, wild ancestor exclusion, gift exception, no breeding, version filter, tie break");
Check(result.Selected.SequenceEqual(SelectionPolicy.Select(fixture.Reverse(), "FR", ancestors, 1, 386).Selected),
    "source order does not affect selection");
var branch = SelectionPolicy.Select([
    R("ancestor-lg", 29, RouteKind.Wild, 3, version: "LG"),
    R("descendant-fr", 31, RouteKind.Wild, 20),
], "FR", new Dictionary<ushort, HashSet<ushort>> { [31] = [29, 30] });
Check(branch.Selected.Single().Id == "descendant-fr", "other-version ancestor does not suppress wild evolved species");
var transitive = SelectionPolicy.Select([
    R("base", 29, RouteKind.Wild, 3), R("third-stage", 31, RouteKind.Wild, 20),
], "FR", new Dictionary<ushort, HashSet<ushort>> { [31] = [29, 30] });
Check(transitive.Selected.Single().Id == "base", "earlier wild ancestor excludes third stage");
// Integration checks use the actual pinned PKHeX encounter databases.
foreach (var version in new[] { GameVersion.FR, GameVersion.LG, GameVersion.R, GameVersion.S, GameVersion.E, GameVersion.D, GameVersion.P, GameVersion.Pt })
{
    var report = EncounterCatalog.Build(version);
    var merged = MirrorCatalog.Build(version, true);
    var own = version.ToString();
    Check(report.Selected.Count > 0, $"{version} produces game-wide candidates");
    Check(report.Selected.All(r => r.Version == own), $"{version} primary routes retain source version");
    Check(!report.Selected.Any(r => r.Kind is RouteKind.BredEgg or RouteKind.Unknown), $"{version} no breeding or unclassified candidates");
    Check(!report.Selected.GroupBy(r => (r.Species, r.Form)).Any(g => g.Count(r => r.Kind == RouteKind.Wild) > 1), $"{version} wild minimum route per species/form");
    Check(!merged.Selected.Where(r => r.Kind != RouteKind.Distribution).GroupBy(r => r.Species).Any(g => g.Select(r => r.Version).Distinct().Count() > 1), $"{version} merged source deduplication");
    Check(report.Selected.All(r => merged.Selected.Contains(r)), $"{version} mirror merge preserves primary routes");
    Check(MirrorCatalog.Build(version, false).Selected.SequenceEqual(report.Selected), $"{version} mirror disabled is primary only");
    Check(merged.MirrorVersions.Count == (version is GameVersion.FR or GameVersion.LG ? 1 : 2), $"{version} correct mirror group size");
    if (version is GameVersion.FR or GameVersion.LG)
    {
        Check(report.Selected.Any(r => r.Species == 175 && r.Kind == RouteKind.GiftEgg), $"{version} no Kanto restriction; gift Togepi retained");
        Check(!report.Selected.Any(r => r.Species == 33 && r.Kind == RouteKind.Wild), $"{version} wild Nidorino excluded");
        Check(merged.Selected.Any(r => r.Species == 37 && r.Version == "LG"), $"{version} Vulpix from LG");
        Check(merged.Selected.Any(r => r.Species == 58 && r.Version == "FR"), $"{version} Growlithe from FR");
    }
    if (version is GameVersion.R or GameVersion.S or GameVersion.E)
    {
        Check(report.Selected.Any(r => r.Species == 252 && r.Kind == RouteKind.Gift), $"{version} Treecko gift");
        Check(report.Selected.Any(r => r.Species == 360 && r.Kind == RouteKind.GiftEgg), $"{version} Wynaut gift egg");
    }
    if (version == GameVersion.E)
    {
        Check(report.Selected.Any(r => r.Species == 152), "Emerald game-wide Johto starter retained");
        Check(merged.Selected.Any(r => r.Species == 335 && r.Version == "R"), "Emerald adds Ruby Zangoose");
        Check(merged.Selected.Any(r => r.Species == 337 && r.Version == "S"), "Emerald adds Sapphire Lunatone");
    }
    if (version is GameVersion.D or GameVersion.P or GameVersion.Pt)
    {
        Check(GameProfile.For(version).Generation == 4, $"{version} generation 4 adapter");
        Check(report.Selected.Any(r => r.Species == 387 && r.Kind == RouteKind.Gift), $"{version} Turtwig starter");
        Check(report.Selected.Any(r => r.Species == 447 && r.Kind == RouteKind.GiftEgg), $"{version} Riolu gift egg");
        Check(report.Selected.Any(r => r.Species < 387), $"{version} earlier-generation species retained");
        Check(report.Selected.Any(r => r.Kind == RouteKind.Trade), $"{version} NPC trades classified");
        Check(report.Excluded.Any(r => r.Route.Species == 25 && r.Reason == "pre_evolution_available_in_wild"), $"{version} correct Gen4 evolution tree");
        Check(!report.Selected.Any(r => r.Method.Contains("Ranch")), $"{version} external Ranch gifts excluded");
    }
    if (version == GameVersion.Pt)
    {
        Check(report.Selected.Any(r => r.Species == 175 && r.Kind == RouteKind.GiftEgg), "Platinum Togepi gift egg");
        Check(merged.Selected.Any(r => r.Species == 434 && r.Version == "D"), "Platinum adds Diamond Stunky");
        Check(merged.Selected.Any(r => r.Species == 431 && r.Version == "P"), "Platinum adds Pearl Glameow");
        Check(merged.Selected.Where(r => r.Species == 387 && r.Kind != RouteKind.Distribution).All(r => r.Version == "Pt"), "Platinum shared starter remains primary");
    }
    var output = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../output"));
    Directory.CreateDirectory(output);
    var options = new JsonSerializerOptions { WriteIndented = true, Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } };
    File.WriteAllText(Path.Combine(output, $"catalog-{version}-Game.json"), JsonSerializer.Serialize(report, options));
    File.WriteAllText(Path.Combine(output, $"catalog-{version}-mirrors.json"), JsonSerializer.Serialize(merged, options));
    Console.WriteLine($"{version}: game {report.Selected.Select(r => r.Species).Distinct().Count()}, merged {merged.Selected.Select(r => r.Species).Distinct().Count()} species");
}
foreach (var id in new uint[] { 0, 1, 65535, 65536, 1234567890, uint.MaxValue })
{
    var (t, secret) = TrainerIdCodec.FromDisplay((id % 1_000_000).ToString(), (id / 1_000_000).ToString(), true);
    Check((((uint)secret << 16) | t) == id, $"display ID roundtrip {id}");
}
bool overflowRejected = false;
try { TrainerIdCodec.FromDisplay("999999", "4294", true); }
catch (ArgumentException) { overflowRejected = true; }
Check(overflowRejected, "display ID overflow rejected instead of wrapping");
Check(TrainerIdCodec.FromDisplay("65535", "65535", false) == (ushort.MaxValue, ushort.MaxValue), "legacy ID unchanged");
Check(LocationNames.Get(R("pallet", 1, RouteKind.Gift, 5, 88), "en").Contains("Pallet"), "FR Pallet Town name lookup");
Check(LocationNames.Get(R("route", 252, RouteKind.Gift, 5, 16, "E"), "en").Contains("101"), "Emerald Route 101 uses Gen3 names");
Check(LocationNames.Get(R("route", 387, RouteKind.Gift, 5, 16, "Pt"), "en").Contains("201"), "Platinum Route 201 uses Gen4 names");
var giftEgg = EncounterCatalog.Build(GameVersion.Pt).Selected.First(r => r.Species == 175 && r.Kind == RouteKind.GiftEgg);
Check(giftEgg.EggLocation == 2011 && !string.IsNullOrWhiteSpace(LocationNames.Get(giftEgg, "en")), "gift egg uses received-egg location");
Check(!string.IsNullOrWhiteSpace(LocationNames.Get(R("pallet", 1, RouteKind.Gift, 5, 88), "ko")), "Korean location lookup");
var supportSummary = new List<object>();
foreach (var version in GameProfile.Supported)
{
    var report = EncounterCatalog.Build(version);
    var mirror = MirrorCatalog.Build(version, true);
    Check(report.Selected.Count > 0, $"ALL {version} catalog not empty");
    Check(report.Selected.All(r => r.Version == version.ToString()), $"ALL {version} source version correct");
    Check(report.Selected.All(r => r.Kind is not (RouteKind.BredEgg or RouteKind.Unknown)), $"ALL {version} excludes breeding and unclassified data");
    Check(mirror.MirrorVersions.Count == MirrorCatalog.Partners(version).Length, $"ALL {version} correct mirror count");
    Check(!mirror.Selected.Where(r => r.Kind != RouteKind.Distribution).GroupBy(r => r.Species).Any(g => g.Select(r => r.Version).Distinct().Count() > 1), $"ALL {version} mirror deduplication");
    supportSummary.Add(new { Version = version.ToString(), Name = GameProfile.DisplayName(version),
        Context = GameProfile.For(version).Context.ToString(), Species = report.Selected.Select(r => r.Species).Distinct().Count(),
        Routes = report.Selected.Count, MirrorSpecies = mirror.Selected.Select(r => r.Species).Distinct().Count(),
        Unclassified = report.Excluded.Where(x => x.Reason == "unclassified_route").Select(x => x.Route.Method).Distinct().ToArray() });
}
Check(GameProfile.ResolveCandidates(GameVersion.RB).Contains(GameVersion.RD), "ambiguous RB offers Red choice");
Check(GameProfile.ResolveCandidates(GameVersion.GS).Length == 2, "ambiguous GS offers Gold/Silver choices");
Check(MirrorCatalog.Partners(GameVersion.PLA).Length == 0 && MirrorCatalog.Partners(GameVersion.ZA).Length == 0, "Legends titles are standalone");
Check(EncounterCatalog.Build(GameVersion.HG).Selected.Any(r => r.Species > 251), "HG remake includes later-generation species");
var supportOutput = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../output/support-summary.json"));
File.WriteAllText(supportOutput, JsonSerializer.Serialize(supportSummary, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"Validated {GameProfile.Supported.Length} mainline versions; summary: {supportOutput}");
foreach (var savePath in args)
{
    var before = SHA256.HashData(File.ReadAllBytes(savePath));
    var save = SaveUtil.GetSaveFile(File.ReadAllBytes(savePath));
    Check(save is not null, "save recognized by PKHeX");
    var versions = GameProfile.ResolveCandidates(save!.Version);
    Check(versions.Length > 0, "save has supported exact or ambiguous version candidates");
    Check(versions.All(v => GameProfile.For(v).Generation == save.Generation), "detected save uses correct generation profiles");
    foreach (var chosenVersion in versions) InjectionTests.Run(save, chosenVersion);
    EventTests.Run(save);
    var report = EncounterCatalog.Build(versions[0]);
    Check(report.Version == versions[0].ToString(), "catalog uses resolved version candidate");
    Console.WriteLine($"Save version: {save.Version}; candidates: {string.Join(",", versions)}; trainer values not printed");
    Check(before.SequenceEqual(SHA256.HashData(File.ReadAllBytes(savePath))), "original save unchanged");
}
