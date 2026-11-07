using PKHeX.Core;
using UltimateEditor;

static class PreviewTests
{
    static void Check(bool value, string message)
    { if (!value) throw new Exception(message); Console.WriteLine("PASS PREVIEW: " + message); }
    static bool Rejected(Action action) { try { action(); return false; } catch (InvalidOperationException) { return true; } }
    public static void Run(string path)
    {
        GenerationWorkerClient.WorkerPathOverride = Path.GetFullPath("plugin/GeneratorWorker/bin/Release/net10.0/UltimateEditor.Generator.exe");
        var bytes = File.ReadAllBytes(path); var save = SaveUtil.GetSaveFile(bytes)!; save.Language = 2;
        var route = EncounterCatalog.Build(GameVersion.FR).Selected.First(r => r.Species == 1 && r.Kind == RouteKind.Gift);
        var trainers = new Dictionary<GameVersion, TrainerProfile>();
        var initial = InjectionPlan.Prepare(save, GameVersion.FR, [route], trainers, 0);
        var session = new PreviewSearchSession(initial);
        var defaults = new Dictionary<string, GenerationSettings> { [route.Id] = new() };
        Check(session.Confirm(defaults) == initial, "unedited valid preview can be confirmed");
        var requested = new Dictionary<string, GenerationSettings> { [route.Id] = new(Nature: ((int)initial.Slots[0].Pokemon.Nature + 1) % 25) };
        Check(Rejected(() => session.Confirm(requested)), "changed criteria cannot commit old result even before dirty notification");
        var genderEdit = new Dictionary<string, GenerationSettings> { [route.Id] = new(Gender: 1) };
        Check(Rejected(() => session.Confirm(genderEdit)), "gender change also invalidates previous preview");
        var dateEdit = new Dictionary<string, GenerationSettings> { [route.Id] = new(MetDate: new(2010, 5, 1)) };
        Check(Rejected(() => session.Confirm(dateEdit)), "date change also requires refreshed preview");
        var hash = InjectionPlan.Fingerprint(save);
        session.Invalidate();
        Check(Rejected(() => session.Confirm(defaults)), "cancelled or failed search leaves previous result blocked");
        Check(Rejected(() => session.Accept(initial, requested)) && session.RequiresSearch, "mismatched regenerated criteria rejected");
        var updated = InjectionPlan.Prepare(save, GameVersion.FR, [route], trainers, 0, generationSettings: requested);
        session.Accept(updated, requested);
        Check(!session.RequiresSearch && session.Confirm(requested) == updated, "successful search replaces preview plan");
        Check(requested[route.Id].Matches(updated.Slots[0].Pokemon), "new preview uses requested nature rather than old random result");
        Check(hash.SequenceEqual(InjectionPlan.Fingerprint(save)), "editing and re-search leave live save untouched");
        var differentRoute = EncounterCatalog.Build(GameVersion.FR).Selected.First(r => r.Species == 4 && r.Kind == RouteKind.Gift);
        var different = InjectionPlan.Prepare(save, GameVersion.FR, [differentRoute], trainers, 0);
        Check(Rejected(() => session.Accept(different, new Dictionary<string, GenerationSettings> { [differentRoute.Id] = new() })), "different route cannot replace selected preview");
        Check(session.RequiresSearch, "invalid replacement cannot enable commit");
        session.Accept(updated, requested);
        session.Confirm(requested).Commit(save); updated.Undo(save);
        Check(hash.SequenceEqual(InjectionPlan.Fingerprint(save)), "re-searched plan commits and undoes correctly");
        Check(bytes.SequenceEqual(File.ReadAllBytes(path)), "original file unchanged");
    }
}
