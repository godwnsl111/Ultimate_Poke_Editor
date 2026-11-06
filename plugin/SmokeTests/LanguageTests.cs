using PKHeX.Core;
using UltimateEditor;

static class LanguageTests
{
    static void Check(bool value, string message)
    { if (!value) throw new Exception(message); Console.WriteLine("PASS LANGUAGE: " + message); }
    public static void Run(IEnumerable<string> paths)
    {
        int[] gba = [1, 2, 3, 4, 5, 7];
        var unknown = new SAV3E(false) { Language = 0 };
        Check(TrainerLanguage.Resolve(unknown, gba, 3) == new ImportedLanguage(3, true), "unknown Western language preserves French selection");
        Check(TrainerLanguage.Resolve(unknown, gba, 1) == new ImportedLanguage(2, true), "previous Japanese selection does not override Western structure");
        var japanese = new SAV3E(true) { Language = 0 };
        Check(TrainerLanguage.Resolve(japanese, gba, 2) == new ImportedLanguage(1, false), "Japanese structure overrides unset language");
        unknown.Language = 5;
        Check(TrainerLanguage.Resolve(unknown, gba, 2) == new ImportedLanguage(5, false), "known German language preserved");
        foreach (var path in paths)
        {
            var bytes = File.ReadAllBytes(path);
            var save = SaveUtil.GetSaveFile(bytes)!;
            var priorId = save.ID32; var priorOt = save.OT; var priorLanguage = save.Language;
            int[] allowed = save.Generation >= 7 ? [1, 2, 3, 4, 5, 7, 8, 9, 10] : save.Generation >= 4 ? [1, 2, 3, 4, 5, 7, 8] : gba;
            var result = TrainerLanguage.Resolve(save, allowed, 2);
            Check(allowed.Contains(result.Language), $"raw {save.Version} language {priorLanguage} imports as {result.Language}; confirmation {result.NeedsConfirmation}");
            Check(save.ID32 == priorId && save.OT == priorOt && save.Language == priorLanguage && bytes.SequenceEqual(File.ReadAllBytes(path)), "trainer and original file unchanged");
        }
    }
}
