using PKHeX.Core;
using UltimateEditor;

internal static class InjectionTests
{
    private static void Check(bool value, string text)
    {
        if (!value) throw new Exception(text);
        Console.WriteLine($"PASS INJECT: {text}");
    }
    private static void Rejected(Action action, string text)
    {
        bool rejected = false;
        try { action(); } catch (InvalidOperationException) { rejected = true; }
        Check(rejected, text);
    }
    public static void Run(SaveFile source, GameVersion? selectedVersion = null)
    {
        var version = selectedVersion ?? source.Version;
        ushort starter = version switch {
            GameVersion.FR or GameVersion.LG => 1,
            GameVersion.R or GameVersion.S or GameVersion.E => 252,
            GameVersion.D or GameVersion.P or GameVersion.Pt => 387,
            GameVersion.B or GameVersion.W or GameVersion.B2 or GameVersion.W2 => 501,
            _ => 0,
        };
        if (starter == 0) return;
        var route = EncounterCatalog.Build(version).Selected.First(r => r.Species == starter && r.Kind == RouteKind.Gift);
        var work = InjectionPlan.CloneSave(source);
        var start = InjectionPlan.Fingerprint(work);
        var trainers = new Dictionary<GameVersion, TrainerProfile>();
        var plan = InjectionPlan.Prepare(work, version, [route], trainers, 0);
        Check(start.SequenceEqual(InjectionPlan.Fingerprint(work)), $"{version} preview does not modify target");
        plan.Commit(work);
        var injected = plan.Slots[0];
        Check(work.GetBoxSlotAtIndex(injected.Box, injected.Slot).Species == starter, $"{version} actual clone injection");
        var afterFirst = InjectionPlan.Fingerprint(work);
        var second = InjectionPlan.Prepare(work, version, [route], trainers, 0);
        Check(second.Slots[0].Box != injected.Box || second.Slots[0].Slot != injected.Slot, $"{version} occupied slot skipped");
        second.Commit(work); second.Undo(work);
        Check(afterFirst.SequenceEqual(InjectionPlan.Fingerprint(work)), $"{version} batch undo preserves earlier injection");
        plan.Undo(work);
        Check(start.SequenceEqual(InjectionPlan.Fingerprint(work)), $"{version} original clone restored");
        var stale = InjectionPlan.Prepare(work, version, [route], trainers, 0);
        var changed = InjectionPlan.Prepare(work, version, [route], trainers, 1);
        changed.Commit(work);
        var changedHash = InjectionPlan.Fingerprint(work);
        Rejected(() => stale.Commit(work), $"{version} stale preview rejected");
        Check(changedHash.SequenceEqual(InjectionPlan.Fingerprint(work)), $"{version} rejected commit preserves current data");
        changed.Undo(work);
        using var cancel = new CancellationTokenSource();
        cancel.Cancel();
        bool cancelled = false;
        try { InjectionPlan.Prepare(work, version, [route], trainers, 0, cancel.Token); }
        catch (OperationCanceledException) { cancelled = true; }
        Check(cancelled && start.SequenceEqual(InjectionPlan.Fingerprint(work)), $"{version} cancellation leaves target intact");
        if (version != GameVersion.FR) return;
        var lg = InjectionPlan.CloneSave(source);
        ((SAV3FRLG)lg).ResetPersonal(GameVersion.LG);
        Check(InjectionPlan.CloneSave(lg).Version == GameVersion.LG, "resolved LG edition survives cloning");
        var lgRoute = EncounterCatalog.Build(GameVersion.LG).Selected.First(r => r.Species == 1 && r.Kind == RouteKind.Gift);
        var lgPlan = InjectionPlan.Prepare(lg, GameVersion.LG, [lgRoute], trainers, 0);
        lgPlan.Commit(lg); lgPlan.Undo(lg);
        Check(lg.Version == GameVersion.LG, "LG commit and undo preserve resolved edition");

        var pk = plan.Slots[0].Pokemon.Clone();
        int first = InjectionPlan.EmptySlots(work, 0)[0];
        pk.IsEgg = true;
        work.SetBoxSlotAtIndex(pk, first, EntityImportSettings.None);
        Check(!InjectionPlan.EmptySlots(work, 0).Contains(first), "egg slot skipped");
        work.CopyChangesFrom(source);
        work.SetBoxSlotAtIndex(work.BlankPKM, 0, EntityImportSettings.None);
        work.SetBoxSlotAtIndex(work.BlankPKM, work.BoxCount - 1, 0, EntityImportSettings.None);
        var lastBox = InjectionPlan.EmptySlots(work, work.BoxCount - 1);
        Check(lastBox[0] / work.BoxSlotCount == work.BoxCount - 1 && lastBox.Any(i => i / work.BoxSlotCount == 0), "current box scan wraps to earlier boxes");
        var junk = work.BlankPKM.Clone();
        junk.Data[junk.SIZE_STORED - 1] = 1;
        work.SetBoxSlotAtIndex(junk, 0, EntityImportSettings.None);
        Check(!InjectionPlan.EmptySlots(work, 0).Contains(0), "nonempty suspicious data is not overwritten");
        var existing = plan.Slots[0].Pokemon.Clone();
        for (int i = 0; i < work.SlotCount; i++) work.SetBoxSlotAtIndex(existing.Clone(), i, EntityImportSettings.None);
        var fullHash = InjectionPlan.Fingerprint(work);
        Rejected(() => InjectionPlan.Prepare(work, version, [route], trainers, 0), "full storage rejects whole batch");
        Check(fullHash.SequenceEqual(InjectionPlan.Fingerprint(work)), "full-storage rejection preserves target");

        // Mirror origin survives adaptation to the FR save.
        work = InjectionPlan.CloneSave(source);
        var mirror = EncounterCatalog.Build(GameVersion.LG).Selected.First(r => r.Species == 37 && r.Kind == RouteKind.Wild);
        var foreign = new TrainerProfile("LG", "MIRROR", 12345, 54321, 0, 2);
        var mirrorPlan = InjectionPlan.Prepare(work, version, [mirror], new Dictionary<GameVersion, TrainerProfile> { [GameVersion.LG] = foreign }, 0);
        var foreignPk = mirrorPlan.Slots[0].Pokemon;
        Check(foreignPk.Version == GameVersion.LG && foreignPk.TID16 == 12345 && foreignPk.SID16 == 54321 && foreignPk.OriginalTrainerName == "MIRROR",
            "mirror version and original trainer retained");
    }
}
