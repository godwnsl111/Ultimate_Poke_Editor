using PKHeX.Core;

namespace UltimateEditor;

public static class LocationNames
{
    public static string Get(Route route, string? language = null)
    {
        if (!Enum.TryParse<GameVersion>(route.Version, out var version)) return "장소 정보 없음";
        var profile = GameProfile.For(version);
        var egg = route.Kind == RouteKind.GiftEgg && route.EggLocation != 0;
        var id = egg ? route.EggLocation : route.Location;
        var strings = language is null ? GameInfo.Strings : GameInfo.GetStrings(language);
        var name = strings.GetLocationName(egg, id, profile.Generation, profile.Generation, version);
        if (string.IsNullOrWhiteSpace(name))
            name = GameInfo.GetStrings("en").GetLocationName(egg, id, profile.Generation, profile.Generation, version);
        return string.IsNullOrWhiteSpace(name) ? "장소 정보 없음" : name;
    }
}
