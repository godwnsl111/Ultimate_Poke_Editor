using PKHeX.Core;
using UltimateEditor;

static class DistributionFilterTests
{
    static void Check(bool condition, string message)
    { if (!condition) throw new Exception(message); Console.WriteLine("PASS FILTER: " + message); }
    public static void Run(IEnumerable<string> paths)
    {
        // Fixed Pokemon language is independent from permitted receiver language.
        var common = new WC6 { Language = 1, RestrictLanguage = 0 };
        Check(DistributionCatalog.CanReceiveLanguage(common, 8), "common card can supply fixed Japanese Pokemon to Korean receiver");
        common.RestrictLanguage = 1;
        Check(!DistributionCatalog.CanReceiveLanguage(common, 8), "Japanese-only receiver restriction excludes Korean");
        foreach (var v in new[] { GameVersion.Pt, GameVersion.W2, GameVersion.X, GameVersion.SN, GameVersion.SW, GameVersion.SL })
        {
            var routes = DistributionCatalog.Build(v);
            Check(routes.Count > 0, "distribution data " + v);
            int foreign = 0, korean = 0;
            foreach (var route in routes)
            {
                string label = DistributionCatalog.ReceiverLanguageLabel(route);
                bool shown = DistributionCatalog.MatchesLanguage(route, 8);
                if (label == "한국어") { korean++; if (!shown) throw new Exception("Korean card missing"); }
                else if (label.StartsWith("공통")) { if (!shown) throw new Exception("Common card missing"); }
                else { foreign++; if (shown) throw new Exception("Foreign-only card leaked"); }
            }
            foreach (var route in routes.GroupBy(DistributionCatalog.ReceiverLanguageLabel).Select(g => g.First()))
            {
                var card = DistributionCatalog.GetTemplate(route);
                Check(DistributionCatalog.MatchesLanguage(route, 8) == DistributionCatalog.CanReceiveLanguage(card, 8),
                    "filter and injection agree " + v + " " + DistributionCatalog.ReceiverLanguageLabel(route));
            }
            Console.WriteLine($"FILTER SUMMARY {v}: {routes.Count} cards / Korean-only {korean} / excluded foreign-only {foreign}");
        }
        foreach (var path in paths)
        {
            var bytes = File.ReadAllBytes(path);
            var save = SaveUtil.GetSaveFile(bytes)!;
            if (save.Version is not (GameVersion.Pt or GameVersion.W2)) continue;
            var routes = DistributionCatalog.Build(save.Version);
            var foreign = routes.First(r => DistributionCatalog.RequiredLanguage(DistributionCatalog.GetTemplate(r)) is 1 or 2);
            var hash = InjectionPlan.Fingerprint(save);
            bool rejected = false;
            try { InjectionPlan.Prepare(save, save.Version, [foreign], new Dictionary<GameVersion, TrainerProfile>(), 0); }
            catch (ArgumentException) { rejected = true; }
            Check(rejected && hash.SequenceEqual(InjectionPlan.Fingerprint(save)), "foreign-only gift blocked in Korean save " + save.Version);
            Check(bytes.SequenceEqual(File.ReadAllBytes(path)), "original file unchanged");
        }
    }
}
