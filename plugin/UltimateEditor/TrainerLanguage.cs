using PKHeX.Core;

namespace UltimateEditor;

public readonly record struct ImportedLanguage(int Language, bool NeedsConfirmation);

public static class TrainerLanguage
{
    // Raw older saves do not store the exact Western language. Preserve the
    // user's selection rather than treating unset language 0/-1 as an error.
    public static ImportedLanguage Resolve(SaveFile save, IReadOnlyList<int> allowed, int selected)
    {
        var region = save switch
        {
            SAV3 s => s.Japanese ? 1 : 0,
            SAV2 s => s.Japanese ? 1 : s.Korean ? 8 : 0,
            SAV1 s => s.Japanese ? 1 : 0,
            _ => -1,
        };
        if (region > 0 && allowed.Contains(region)) return new(region, false);
        if (allowed.Contains(save.Language) && (region != 0 || save.Language is not (1 or 8)))
            return new(save.Language, false);
        if (region == 0)
        {
            var language = allowed.Contains(selected) && selected is not (1 or 8) ? selected : 2;
            return new(language, true);
        }
        throw new ArgumentException($"이 버전 입력란에서 지원하지 않는 언어입니다 (언어 ID: {save.Language}).");
    }
}
