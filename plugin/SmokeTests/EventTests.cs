using PKHeX.Core;
using UltimateEditor;

internal static class EventTests
{
    private static void Check(bool value, string text)
    {
        if (!value) throw new Exception(text);
        Console.WriteLine($"PASS EVENT: {text}");
    }
    public static void Run(SaveFile save)
    {
        var version = save.Version;
        if (version is GameVersion.FR or GameVersion.LG or GameVersion.Pt)
        {
            var work = InjectionPlan.CloneSave(save);
            ushort species = version == GameVersion.Pt ? (ushort)63 : (ushort)122;
            var trade = EncounterCatalog.Build(version).Selected.First(r => r.Kind == RouteKind.Trade && r.Species == species);
            Check(trade.TradeLevelKnown && trade.MinLevel > 1, "NPC trade minimum excludes breeding-only level");
            var hash = InjectionPlan.Fingerprint(work);
            bool rejected = false;
            try { InjectionPlan.Prepare(work, version, [trade], new Dictionary<GameVersion, TrainerProfile>(), 0); }
            catch (ArgumentException) { rejected = true; }
            Check(rejected && hash.SequenceEqual(InjectionPlan.Fingerprint(work)), "NPC prerequisites require explicit review");
            var plan = InjectionPlan.Prepare(work, version, [trade], new Dictionary<GameVersion, TrainerProfile>(), 0, prerequisitesReviewed: true);
            var pk = plan.Slots.Single().Pokemon;
            var template = EncounterCatalog.GetTemplate(trade);
            Check(pk.CurrentLevel == trade.MinLevel && pk.OriginalTrainerName != save.OT, "NPC fixed trainer and corrected level retained");
            if (template is ITrainerID32ReadOnly fixedTrainer)
                Check(pk.TID16 == fixedTrainer.TID16 && pk.SID16 == fixedTrainer.SID16, "NPC fixed trainer IDs retained");
            plan.Commit(work); plan.Undo(work);
            Check(hash.SequenceEqual(InjectionPlan.Fingerprint(work)), "NPC clone injection and undo");
        }
        if (version == GameVersion.FR)
        {
            var work = InjectionPlan.CloneSave(save);
            var capture = EncounterCatalog.Build(version).Selected.First(r => r.Species == 249 && r.Kind == RouteKind.Wild);
            Check(!capture.NeedsReview && capture.Requirement.Contains("티켓"), "event capture condition is informational");
            var plan = InjectionPlan.Prepare(work, version, [capture], new Dictionary<GameVersion, TrainerProfile>(), 0);
            Check(plan.Slots.Single().Pokemon.FatefulEncounter, "event capture fateful flag retained");
        }
        if (version == GameVersion.Pt)
        {
            foreach (ushort species in new ushort[] { 491, 492 })
            {
                var route = EncounterCatalog.Build(version).Selected.First(r => r.Species == species && r.Kind != RouteKind.Distribution);
                Check(!route.NeedsReview, "event capture no prerequisite gate " + species);
                var hash = InjectionPlan.Fingerprint(save);
                if (species == 492 && save.Language == 8)
                {
                    bool rejected = false;
                    try { InjectionPlan.Prepare(save, version, [route], new Dictionary<GameVersion, TrainerProfile>(), 0); }
                    catch (ArgumentException ex) { rejected = ex.Message.Contains("미배포"); }
                    Check(rejected && hash.SequenceEqual(InjectionPlan.Fingerprint(save)), "Korean native Shaymin remains blocked by actual release-language restriction");
                    var koreanGift = DistributionCatalog.Build(version).First(r => r.Species == 492 && DistributionCatalog.MatchesLanguage(r, 8));
                    var giftPlan = InjectionPlan.Prepare(save, version, [koreanGift], new Dictionary<GameVersion, TrainerProfile>(), 0);
                    Check(new LegalityAnalysis(giftPlan.Slots.Single().Pokemon).Valid, "Korean direct-distribution Shaymin is legal");
                    continue;
                }
                var plan = InjectionPlan.Prepare(save, version, [route], new Dictionary<GameVersion, TrainerProfile>(), 0);
                Check(plan.Slots.Single().Pokemon.Species == species && new LegalityAnalysis(plan.Slots.Single().Pokemon).Valid,
                    "Darkrai/Shaymin inject without prerequisite review " + species);
                plan.Commit(save); plan.Undo(save);
                Check(hash.SequenceEqual(InjectionPlan.Fingerprint(save)), "event undo restores save " + species);
            }
        }
        if (version != GameVersion.Pt) return;
        Console.WriteLine($"Distribution test receiving language: {save.Language}");
        var gifts = DistributionCatalog.Build(version);
        Check(gifts.Count > 0 && gifts.All(r => r.Kind == RouteKind.Distribution), "native Wonder Card Pokemon available");
        Check(gifts.Select(g => g.DistributionKey).Distinct().Count() == gifts.Count, "identical card records deduplicated");
        var regular = EncounterCatalog.Build(version).Selected.Where(r => r.Kind is RouteKind.Wild or RouteKind.Gift)
            .Where(r => !r.NeedsReview && r.Form < EncounterUtil.FormDynamic).ToLookup(r => (r.Species, r.Form));
        var plans = new List<(Route Route, InjectionPlan Plan)>();
        Route? ordinary = null;
        foreach (var group in gifts.Where(g => regular.Contains((g.Species, g.Form))).GroupBy(g => (g.Species, g.Form)).OrderByDescending(g => g.Count()))
        {
            var normal = regular[group.Key].First();
            foreach (var gift in group)
            {
                var template = DistributionCatalog.GetTemplate(gift);
                if (template.IsEgg || string.IsNullOrWhiteSpace(template.OriginalTrainerName)) continue;
                try
                {
                    DistributionCatalog.CheckLanguage(template, save.Language);
                    var plan = InjectionPlan.Prepare(save, version, [normal, gift], new Dictionary<GameVersion, TrainerProfile>(), 0);
                    plans.Add((gift, plan)); ordinary = normal;
                    if (plans.Count == 2) break;
                }
                catch (ArgumentException) { }
                catch (InvalidOperationException ex) { Console.WriteLine($"CARD FAIL: {gift.Description}: {ex.Message}"); }
            }
            if (plans.Count > 0) break;
        }
        Check(plans.Count >= 1, "ordinary and direct-distribution Pokemon of same species can coexist");
        var chosenGift = plans[0].Route;
        var chosenPlan = plans[0].Plan;
        var card = DistributionCatalog.GetTemplate(chosenGift);
        var received = chosenPlan.Slots[1].Pokemon;
        Check(received.TID16 == card.TID16 && received.SID16 == card.SID16 && received.OriginalTrainerName == card.OriginalTrainerName,
            "distribution fixed OT and IDs preserved");
        Check(card.Version.Contains(received.Version) || received.Version.Contains(card.Version), "card origin is retained independently of receiving game");
        Check(received.FatefulEncounter && new LegalityAnalysis(received).Valid, "distribution legal after clone serialization");
        if (plans.Count >= 2)
        {
            var combined = InjectionPlan.Prepare(save, version, [ordinary!, plans[0].Route, plans[1].Route], new Dictionary<GameVersion, TrainerProfile>(), 0);
            Check(combined.Slots.Count == 3, "multiple distinct cards of same species allowed alongside ordinary encounter");
        }
        var merged = MirrorCatalog.Build(version, true);
        Check(merged.Selected.Where(r => r.Kind == RouteKind.Distribution).GroupBy(r => (r.DistributionKey, r.Version)).All(g => g.Count() == 1),
            "card/receiving-version pairs are unique");
        bool twoCardsTested = false;
        foreach (var alternative in merged.Selected.Where(r => r.Kind == RouteKind.Distribution && r.Species == ordinary!.Species &&
            r.DistributionKey != chosenGift.DistributionKey && r.Version != version.ToString()))
        {
            var sourceVersion = Enum.Parse<GameVersion>(alternative.Version);
            var alternativeCard = DistributionCatalog.GetTemplate(alternative);
            int language = DistributionCatalog.RequiredLanguage(alternativeCard);
            var receiver = new TrainerProfile(alternative.Version, "EVENT", 1234, 5678, 0, language == 0 ? 2 : language);
            try
            {
                var combined = InjectionPlan.Prepare(save, version, [ordinary!, chosenGift, alternative],
                    new Dictionary<GameVersion, TrainerProfile> { [sourceVersion] = receiver }, 0);
                twoCardsTested = combined.Slots.Count == 3;
                if (twoCardsTested) break;
            }
            catch (ArgumentException) { }
            catch (InvalidOperationException) { }
        }
        Check(twoCardsTested, "ordinary plus two distinct distribution cards of same species coexist");
        Check(merged.Selected.Any(r => r.Kind != RouteKind.Distribution && r.Species == ordinary!.Species) &&
            merged.Selected.Any(r => r.Kind == RouteKind.Distribution && r.Species == ordinary!.Species), "ordinary/mirror species dedup does not remove distributions");
        if (save.Language != 1)
        {
            // An additional in-memory Japanese destination exercises other actual card variants.
            var japanese = InjectionPlan.CloneSave(save);
            japanese.Language = 1; japanese.OT = "テスト";
            Run(japanese);
        }
    }
}
