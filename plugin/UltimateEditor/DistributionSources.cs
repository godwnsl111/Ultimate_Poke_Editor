using PKHeX.Core;

namespace UltimateEditor;

// WC6Full/WC7Full.GetArray stamps today's date into resource-backed slices.
// Read fresh resource streams and construct the inner cards directly, without
// touching PKHeX's global event database or mistaking that stamp for evidence.
public static class DistributionSources
{
    private static readonly Lazy<IReadOnlyList<DataMysteryGift>> OriginalSixSeven = new(Load);
    public static IEnumerable<DataMysteryGift> GetAll()
    {
        foreach (var gift in EncounterEvent.GetAllEvents(false).OfType<DataMysteryGift>())
            if (gift is not (WC6 or WC7)) yield return gift;
        foreach (var gift in OriginalSixSeven.Value) yield return gift;
        foreach (var gift in EncounterEvent.EGDB_G6) yield return gift;
        foreach (var gift in EncounterEvent.EGDB_G7) yield return gift;
    }
    private static byte[] Resource(string filename)
    {
        var assembly = typeof(PKM).Assembly;
        string name = assembly.GetManifestResourceNames().Single(n => n.EndsWith("." + filename, StringComparison.Ordinal));
        using var stream = assembly.GetManifestResourceStream(name)!;
        using var copy = new MemoryStream(); stream.CopyTo(copy); return copy.ToArray();
    }
    private static IReadOnlyList<DataMysteryGift> Load()
    {
        var result = new List<DataMysteryGift>();
        foreach (int generation in new[] { 6, 7 })
        {
            byte[] full = Resource($"wc{generation}full.pkl"), flat = Resource($"wc{generation}.pkl");
            for (int offset = 0; offset < full.Length; offset += WC7Full.Size)
            {
                byte[] inner = full.AsSpan(offset + 0x208, WC7.Size).ToArray();
                if (generation == 7) result.Add(new WC7(inner) { RestrictVersion = full[offset], RestrictLanguage = full[offset + 0x1FF] });
                else result.Add(new WC6(inner) { RestrictVersion = full[offset], RestrictLanguage = full[offset + 0x1FF] });
            }
            for (int offset = 0; offset < flat.Length; offset += WC7.Size)
            {
                byte[] inner = flat.AsSpan(offset, WC7.Size).ToArray();
                result.Add(generation == 7 ? new WC7(inner) : new WC6(inner));
            }
        }
        return result;
    }
}
