using PKHeX.Core;
using UltimateEditor;

static class LinkedTests
{
    static void Check(bool condition, string message)
    { if (!condition) throw new Exception(message); Console.WriteLine("PASS LINKED: " + message); }
    public static void Run(IEnumerable<string> paths)
    {
        foreach (var v in GameProfile.Supported)
            Check((LinkedCatalog.Build(v).Count > 0) == LinkedCatalog.IsSupported(v), "target gate " + v);
        var routes = LinkedCatalog.Build(GameVersion.E);
        Check(routes.Any(r => r.LinkedSource == "XD") && routes.Any(r => r.LinkedSource == "COLO"), "both GC catalogs");
        Check(routes.Any(r => r.Species == 250 && r.LinkedSource == "COLO"), "Mt Battle Ho-oh included");
        Check(LinkedCatalog.Build(GameVersion.W2).Count == 26, "all 26 Dream Radar species/forms");
        foreach (var path in paths)
        {
            var bytes = File.ReadAllBytes(path);
            var save = SaveUtil.GetSaveFile(bytes)!;
            if (save is SAV3E)
            {
                var trainers = new Dictionary<GameVersion, TrainerProfile> {
                    [GameVersion.COLO] = new("COLO", "COLO", 12345, 54321, 0, 2),
                    [GameVersion.XD] = new("XD", "MICHAEL", 23456, 43210, 0, 2),
                };
                var chosen = routes;
                foreach (var route in chosen)
                {
                    if (route.LinkedSource == "COLO") trainers[GameVersion.COLO] = new("COLO", route.Location == 128 ? "コロ" : "COLO", 12345, 54321, 0, route.Location == 128 ? 1 : 2);
                    var plan = InjectionPlan.Prepare(save, GameVersion.E, [route], new Dictionary<GameVersion, TrainerProfile>(), 0,
                        prerequisitesReviewed: true, linkedTrainers: trainers);
                    var pk = plan.Slots.Single().Pokemon;
                    Check(pk is PK3 && new LegalityAnalysis(pk).Valid, "GC native transfer legal " + route.Species + " " + route.LinkedSource);
                    if (route.Species == 250) Check(pk.OriginalTrainerName == "MATTLE" && pk.TID16 == 10048, "MATTLE fixed trainer preserved");
                    if (route.Species == 249) Check(!pk.IsShiny && ((PK3)pk).RibbonNational, "XD purified nonshiny shadow");
                    plan.Commit(save); plan.Undo(save);
                }
            }
            if (save is SAV5B2W2 b2w2)
            {
                foreach (var badgeMask in new[] { 0, 1, 3, 7, 15, 31, 63, 127, 255 })
                {
                    var clone = (SAV5B2W2)InjectionPlan.CloneSave(b2w2); clone.Misc.Badges = badgeMask;
                    foreach (var route in LinkedCatalog.Build(clone.Version))
                    {
                        var plan = InjectionPlan.Prepare(clone, clone.Version, [route], new Dictionary<GameVersion, TrainerProfile>(), 0, prerequisitesReviewed: true);
                        var pk = plan.Slots.Single().Pokemon;
                        var level = badgeMask == 0 ? 5 : ((System.Numerics.BitOperations.PopCount((uint)badgeMask) + 1) / 2) * 10;
                        Check(pk.MetLevel == level && !pk.IsShiny && pk.Ball == (byte)Ball.Dream && pk.ID32 == clone.ID32,
                            $"Radar {route.Species}/{route.Form} badges {badgeMask} level {level}");
                    }
                }
            }
            Check(bytes.SequenceEqual(File.ReadAllBytes(path)), "original file unchanged " + Path.GetFileName(path));
        }
    }
}
