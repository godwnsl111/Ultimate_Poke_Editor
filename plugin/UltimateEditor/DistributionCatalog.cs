using System.Security.Cryptography;
using PKHeX.Core;

namespace UltimateEditor;

public static class DistributionCatalog
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<(GameVersion, string), DataMysteryGift> Templates = new();

    public static IReadOnlyList<Route> Build(GameVersion version)
    {
        var profile = GameProfile.For(version);
        var pk = EntityBlank.GetBlank(profile.Context); pk.Version = version;
        var result = new List<Route>();
        var seen = new HashSet<string>();
        foreach (var gift in DistributionSources.GetAll())
        {
            if (gift.Context != profile.Context || !gift.IsEntity || gift.IsItem || gift.Species == 0 || gift.Species > profile.MaxSpecies) continue;
            if (!CanReceive(gift, version, pk)) continue;
            var extra = gift switch {
                PGF g => $"{g.RestrictLanguage}:{g.RestrictVersion}",
                WC6 g => $"{g.RestrictLanguage}:{g.RestrictVersion}",
                WC7 g => $"{g.RestrictLanguage}:{g.RestrictVersion}", _ => "",
            };
            var key = gift.Type + ":" + Convert.ToHexString(SHA256.HashData(gift.Write())) + ":" + extra;
            if (!seen.Add(key)) continue;
            var id = $"{version}-distribution-{key}";
            Templates[(version, id)] = gift;
            result.Add(new(id, gift.Species, gift.Form, version.ToString(), RouteKind.Distribution,
                gift.LevelMin, gift.LevelMax, gift.Location, gift.Type, gift.CardHeader, true, gift.EggLocation, key,
                $"직접 배포: {gift.CardHeader}. 수령 언어: {LanguageName(RequiredLanguage(gift))}. 카드 고정값 적용 (과거 배포 포함)."));
        }
        return result;
    }

    public static bool CanReceive(DataMysteryGift gift, GameVersion version, PKM native)
    {
        if (gift is WC8 { IsHOMEGift: true } or WC9 { IsHOMEGift: true } or WB8 { IsHOMEGift: true }) return false;
        return gift switch
        {
            // PCD compatibility is a receiving-game bitmap; PCD.Version is the fixed origin.
            // LE masks verified against the original Mystery Gift Editor research (README).
            PCD g => !g.Gift.IsManaphyEgg && (g.CardCompatibility & (version switch {
                GameVersion.D => 0x0400, GameVersion.P => 0x0800, GameVersion.Pt => 0x1000,
                GameVersion.HG => 0x0080, GameVersion.SS => 0x0100, _ => 0 })) != 0,
            IRestrictVersion g => g.CanBeReceivedByVersion(version),
            WA8 g => g.CanBeReceivedByVersion(version, native),
            WB8 g => g.CanBeReceivedByVersion(version, native),
            WC9 g => g.CanBeReceivedByVersion(native),
            WA9 g => g.CanBeReceivedByVersion(native),
            _ => false,
        };
    }

    public static DataMysteryGift GetTemplate(Route route)
    {
        var version = Enum.Parse<GameVersion>(route.Version);
        if (!Build(version).Contains(route) || !Templates.TryGetValue((version, route.Id), out var gift))
            throw new ArgumentException("배포 데이터가 변경됐습니다. 목록을 다시 조회하세요.");
        var clone = gift.Clone();
        // These constraints are packaged outside the card byte array.
        switch (gift)
        {
            case PGF g when clone is PGF c: c.RestrictLanguage = g.RestrictLanguage; c.RestrictVersion = g.RestrictVersion; break;
            case WC6 g when clone is WC6 c: c.RestrictLanguage = g.RestrictLanguage; c.RestrictVersion = g.RestrictVersion; break;
            case WC7 g when clone is WC7 c: c.RestrictLanguage = g.RestrictLanguage; c.RestrictVersion = g.RestrictVersion; break;
        }
        clone.GiftUsed = false;
        return clone;
    }

    public static DistributionDateInfo? DateInfo(Route route)
    {
        if (route.Kind != RouteKind.Distribution) return null;
        var version = Enum.Parse<GameVersion>(route.Version);
        return Templates.TryGetValue((version, route.Id), out var gift) ? DistributionDates.Describe(gift) : null;
    }
    public static bool MatchesLanguage(Route route, int language)
    {
        if (route.Kind != RouteKind.Distribution) return true;
        if (!Enum.TryParse<GameVersion>(route.Version, out var version) || !Templates.TryGetValue((version, route.Id), out var gift)) return false;
        return CanReceiveLanguage(gift, language);
    }
    public static string ReceiverLanguageLabel(Route route)
    {
        if (route.Kind != RouteKind.Distribution) return "";
        var version = Enum.Parse<GameVersion>(route.Version);
        return Templates.TryGetValue((version, route.Id), out var gift) ? LanguageName(RequiredLanguage(gift)) : "알 수 없음";
    }
    public static bool CanReceiveLanguage(DataMysteryGift gift, int language)
    {
        if (language is not (1 or 2 or 3 or 4 or 5 or 7 or 8 or 9 or 10)) return false;
        if (gift.Generation <= 6 && language is (9 or 10)) return false;
        int required = RequiredLanguage(gift);
        return required == 0 || required == language;
    }

    public static void CheckLanguage(DataMysteryGift gift, int language)
    {
        var required = RequiredLanguage(gift);
        if (!CanReceiveLanguage(gift, language))
            throw new ArgumentException($"이 배포는 수령 언어 {LanguageName(required)}가 필요합니다. 해당 언어의 출신 세이브를 사용하세요.");
    }
    public static int RequiredLanguage(DataMysteryGift gift) => gift switch { PCD g => g.Gift.PK.Language, PGF g => g.RestrictLanguage,
        WC6 g => g.RestrictLanguage, WC7 g => g.RestrictLanguage, _ => 0 };
    public static string LanguageName(int language) => language switch { 0 => "공통 / 수령 언어 제한 없음", 1 => "일본어", 2 => "영어",
        3 => "프랑스어", 4 => "이탈리아어", 5 => "독일어", 7 => "스페인어", 8 => "한국어", 9 => "중국어 간체", 10 => "중국어 번체", _ => language.ToString() };
}
