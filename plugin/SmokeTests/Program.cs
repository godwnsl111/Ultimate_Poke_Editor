using System.Security.Cryptography;
using System.Text.Json;
using PKHeX.Core;
using UltimateEditor;

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
Check(result.Selected.Select(r => r.Id).ToHashSet().SetEquals(["low", "gift", "egg", "only-evolved"]),
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
var membership = SelectionPolicy.Select([R("one", 1, RouteKind.Gift, 5), R("three", 3, RouteKind.Gift, 5)],
    "FR", new Dictionary<ushort, HashSet<ushort>>(), 1, 386, new HashSet<ushort> { 1, 25 });
Check(membership.Selected.Single().Species == 1, "non-contiguous regional dex membership supported");

foreach (var version in new[] { GameVersion.FR, GameVersion.LG })
{
    var report = EncounterCatalog.Build(version);
    var wholeGame = EncounterCatalog.Build(version, CatalogScope.Game);
    Check(wholeGame.Selected.Any(r => r.Species == 175 && r.Kind == RouteKind.GiftEgg), $"{version} game scope includes gift Togepi egg");
    Check(wholeGame.Selected.Any(r => r.Species > 151), $"{version} game scope expands beyond regional dex");
    Check(report.Selected.All(r => wholeGame.Selected.Any(g => g.Id == r.Id)), $"{version} region is subset of game scope");
    Check(!wholeGame.Selected.Any(r => r.Kind == RouteKind.BredEgg), $"{version} game scope still excludes breeding");
    Check(!wholeGame.Selected.Any(r => r.Species == 33 && r.Kind == RouteKind.Wild), $"{version} game scope still excludes Nidorino");
    Check(report.Selected.Any(r => r.Species == 1 && r.Kind == RouteKind.Gift), $"{version} starter present");
    Check(!report.Selected.Any(r => r.Species == 33 && r.Kind == RouteKind.Wild), $"{version} Nidorino excluded");
    Check(report.Selected.Any(r => r.Species == 32 && r.Kind == RouteKind.Wild), $"{version} Nidoran male present");
    Check(report.Selected.Any(r => r.Species == 142 && r.Kind == RouteKind.Gift), $"{version} fossil gift present");
    Check(report.Selected.Any(r => r.Kind == RouteKind.Trade), $"{version} NPC trade present");
    Check(!report.Selected.Any(r => r.Kind == RouteKind.BredEgg || r.Species > 151), $"{version} scope enforced");
    Check(!report.Selected.GroupBy(r => (r.Species, r.Form)).Any(g => g.Count(r => r.Kind == RouteKind.Wild) > 1),
        $"{version} single wild route per species/form");
    var output = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../output"));
    Directory.CreateDirectory(output);
    File.WriteAllText(Path.Combine(output, $"catalog-{version}.json"),
        JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
    File.WriteAllText(Path.Combine(output, $"catalog-{version}-Game.json"),
        JsonSerializer.Serialize(wholeGame, new JsonSerializerOptions { WriteIndented = true }));
    Console.WriteLine($"{version} game scope: {wholeGame.Selected.Count} routes / {wholeGame.Selected.Select(r => r.Species).Distinct().Count()} species");
    Console.WriteLine($"{version}: {report.Selected.Count} routes, {report.Selected.Select(r => r.Species).Distinct().Count()} species; report {output}");
}
if (args.Length == 1)
{
    var before = SHA256.HashData(File.ReadAllBytes(args[0]));
    var save = SaveUtil.GetSaveFile(File.ReadAllBytes(args[0]));
    Check(save is not null, "save recognized by PKHeX");
    Check(save!.Generation == 3, "save is generation 3");
    Console.WriteLine($"Save version: {save.Version}; trainer fields available (not printed)");
    Check(before.SequenceEqual(SHA256.HashData(File.ReadAllBytes(args[0]))), "original save unchanged");
}
