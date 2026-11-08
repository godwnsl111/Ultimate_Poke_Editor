using PKHeX.Core;
using UltimateEditor;

static class CatalogOptionsTests
{
    static void Check(bool value, string message)
    { if (!value) throw new Exception(message); Console.WriteLine("PASS CATALOG OPTIONS: " + message); }
    public static void Run(string frPath, string ptPath)
    {
        var save = SaveUtil.GetSaveFile(File.ReadAllBytes(frPath))!; save.Language = 2;
        var routes = EncounterCatalog.Build(GameVersion.FR).Selected.Where(r => r.Kind == RouteKind.Gift && r.Species is 1 or 4).OrderBy(r => r.Species).ToArray();
        var trainers = new Dictionary<GameVersion, TrainerProfile>();
        var options = routes.ToDictionary(r => r.Id, r => new GenerationSettings(Gender: r.Species == 4 ? 2 : -1));
        var hash = InjectionPlan.Fingerprint(save);
        var partial = InjectionPlan.Prepare(save, GameVersion.FR, routes.Reverse().ToArray(), trainers, 0, generationSettings: options, continueOnFailure: true);
        Check(partial.Slots.Count == 1 && partial.Failures.Count == 1 && partial.Failures[0].Route.Species == 4 && partial.Failures[0].Message.Contains("성별"), "failed species and reason retained alongside successful result");
        Check(partial.Slots[0].Box * save.BoxSlotCount + partial.Slots[0].Slot == InjectionPlan.EmptySlots(save, 0)[0], "failed first route does not consume an empty box slot");
        var session = new PreviewSearchSession(partial);
        Check(session.Confirm(options) == partial, "partial success can be confirmed");
        partial.Commit(save); partial.Undo(save);
        Check(hash.SequenceEqual(InjectionPlan.Fingerprint(save)), "partial commit/undo preserves original save");
        var changed = new Dictionary<string, GenerationSettings>(options) { [routes[1].Id] = new() };
        bool blocked = false;
        try { session.Confirm(changed); } catch (InvalidOperationException) { blocked = true; }
        Check(blocked, "editing failed row requires a new search");
        var retry = InjectionPlan.Prepare(save, GameVersion.FR, routes, trainers, 0, generationSettings: changed, continueOnFailure: true);
        session.Accept(retry, changed);
        Check(session.Confirm(changed).Slots.Count == 2 && retry.Failures.Count == 0, "failed row recovers on retry");
        var allFailed = InjectionPlan.Prepare(save, GameVersion.FR, [routes[1]], trainers, 0, generationSettings: options, continueOnFailure: true);
        Check(allFailed.Slots.Count == 0 && allFailed.Failures.Count == 1, "all-failed batch still retains diagnostics");
        blocked = false;
        try { allFailed.Commit(save); } catch (InvalidOperationException) { blocked = true; }
        Check(blocked, "all-failed batch cannot commit");
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        blocked = false;
        try { InjectionPlan.Prepare(save, GameVersion.FR, routes, trainers, 0, cancel.Token, continueOnFailure: true); }
        catch (OperationCanceledException) { blocked = true; }
        Check(blocked, "explicit cancellation is never converted into a per-Pokemon failure");

        // Full-sized synthetic HG save; BlankSaveFile has no raw export buffer.
        var hg = new SAV4HGSS(new byte[SaveUtil.SIZE_G4RAW]) { Version = GameVersion.HG, OT = "TEST", Language = 2, Magic = SAV4.MAGIC_JAPAN_INTL };
        foreach (int partition in new[] { 0, 0x40000 })
            System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(hg.Data[(partition + SAV4HGSS.GeneralSize - 12)..], SAV4HGSS.GeneralSize);
        var trade = EncounterCatalog.Build(GameVersion.HG).Selected.First(r => r.Kind == RouteKind.Trade && !r.TradeLevelKnown && r.MinLevel <= 50 && r.MaxLevel >= 50);
        var tradePlan = InjectionPlan.Prepare(hg, GameVersion.HG, [trade], trainers, 0, prerequisitesReviewed: true);
        Check(tradePlan.Slots.Single().Pokemon.CurrentLevel == 50 && new LegalityAnalysis(tradePlan.Slots.Single().Pokemon).Valid, "HeartGold variable NPC trade defaults to legal level 50");
        var tradePk = tradePlan.Slots.Single().Pokemon;
        var tradeConditions = new Dictionary<string, GenerationSettings> { [trade.Id] = new(Nature: (int)tradePk.Nature, Gender: tradePk.Gender) };
        var searchedTrade = InjectionPlan.Prepare(hg, GameVersion.HG, [trade], trainers, 0, prerequisitesReviewed: true, generationSettings: tradeConditions);
        Check(searchedTrade.Slots.Single().Pokemon.CurrentLevel == 50 && new LegalityAnalysis(searchedTrade.Slots.Single().Pokemon).Valid, "HeartGold NPC trade conditions search preserves legal HGSS ball bytes");

        var pt = SaveUtil.GetSaveFile(File.ReadAllBytes(ptPath))!;
        var ptRoutes = EncounterCatalog.Build(GameVersion.Pt).Selected.Where(r => r.Kind == RouteKind.Wild).Take(2).ToArray();
        DateOnly date = new(2011, 5, 10);
        var dateOptions = ptRoutes.ToDictionary(r => r.Id, _ => new GenerationSettings(MetDate: date));
        var dated = InjectionPlan.Prepare(pt, GameVersion.Pt, ptRoutes, trainers, 0, generationSettings: dateOptions, continueOnFailure: true);
        Check(dated.Failures.Count == 0 && dated.Slots.Count == 2 && dated.Slots.All(s => s.Pokemon.MetDate == date), "common date applies to every selected route and survives box roundtrip");
        Check(hash.SequenceEqual(InjectionPlan.Fingerprint(save)), "live input remains unchanged");
    }
}
