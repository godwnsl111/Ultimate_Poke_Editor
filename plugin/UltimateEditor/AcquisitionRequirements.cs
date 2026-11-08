using PKHeX.Core;

namespace UltimateEditor;

public static class AcquisitionRequirements
{
    public static IReadOnlyList<Route> Apply(GameVersion version, List<Route> routes)
    {
        var profile = GameProfile.For(version);
        var levels = routes.Where(r => r.Form == 0 && !r.RequiresEvent && r.Kind is RouteKind.Wild or RouteKind.Gift or RouteKind.GiftEgg)
            .GroupBy(r => r.Species).ToDictionary(g => g.Key, g => (int)g.Min(r => r.MinLevel));
        // Only unconditional level-up evolutions establish an automatic lower bound.
        // Stones, trading, breeding and unmodelled special conditions never establish proof here.
        var tree = EvolutionTree.GetEvolutionTree(profile.Context);
        bool changed;
        do
        {
            changed = false;
            foreach (var (species, level) in levels.ToArray())
                foreach (var evo in tree.Forward.GetForward(species, 0).Span)
                {
                    if (evo.Method != EvolutionType.LevelUp || evo.Species == 0) continue;
                    int minimum = Math.Max(evo.Level, level + evo.LevelUp);
                    if (minimum <= 100 && (!levels.TryGetValue(evo.Species, out var known) || minimum < known))
                    { levels[evo.Species] = minimum; changed = true; }
                }
        } while (changed);
        return routes.Select(r => {
            if (r.Kind == RouteKind.Trade)
            {
                ushort outgoing = RequiredTradeSpecies(version, r.Species);
                var known = outgoing != 0 && levels.TryGetValue(outgoing, out var level);
                byte minimum = known ? (byte)Math.Max(r.MinLevel, levels[outgoing]) : r.MinLevel;
                string name = outgoing == 0 ? "NPC 입수 조건(요구 포켓몬/선물 조건)" : $"{(Species)outgoing} (#{outgoing})";
                return r with { MinLevel = minimum, MaxLevel = profile.Generation <= 4 ? (byte)100 : r.MaxLevel, RequiredSpecies = outgoing, TradeLevelKnown = known,
                    Requirement = $"{name} 확보 조건 충족 가정. " + (known ? $"출현/일반 레벨업 기준 최소 {minimum}레벨." : "교환 레벨 미지정 시 기본 50레벨 적용.") };
            }
            string requirement = EventText(version, r);
            return requirement.Length == 0 ? r : r with { RequiresEvent = true, Requirement = requirement };
        }).ToArray();
    }

    public static void CheckLanguage(Route route, int language)
    {
        if (LanguageIssue(route, language) is { } issue) throw new ArgumentException(issue);
    }

    public static string? LanguageIssue(Route route, int language)
    {
        if (route.Kind == RouteKind.Distribution) return null;
        if (route.Version == "E" && route.Species == 151 && route.Location == 201 && language != 1)
            return "외딴섬 뮤의 오래된해도 배포는 일본어 에메랄드가 필요합니다.";
        if (route.Version == "Pt" && route.Species == 492 && language == 8 && route.Location == 63)
            return "한국어 기라티나의 꽃의낙원 쉐이미는 PKHeX에서 미배포로 판정됩니다. 한국어 직접 배포 카드를 선택하세요.";
        return null;
    }

    private static string EventText(GameVersion v, Route r)
    {
        if (v is GameVersion.FR or GameVersion.LG && r.Location == 174) return "배꼽바위: 신비의티켓을 해당 게임에서 직접 수령한 경로가 필요합니다.";
        if (v is GameVersion.FR or GameVersion.LG && r.Species == 386) return "탄생의섬: 오로라티켓 직접 배포가 필요합니다.";
        if (v == GameVersion.E && r.Species == 151 && r.Location == 201) return "외딴섬: 일본어 에메랄드에 직접 배포된 오래된해도 필요.";
        if (v is GameVersion.R or GameVersion.S or GameVersion.E && r.Location == 73) return "남쪽의외딴섬: 해당 게임에서 직접 수령한 무한의티켓 경로가 필요합니다.";
        if (v == GameVersion.E && r.Location == 211) return "배꼽바위: 신비의티켓 직접 배포가 필요합니다.";
        if (v == GameVersion.E && r.Species == 386) return "탄생의섬: 오로라티켓 직접 배포가 필요합니다.";
        if (v == GameVersion.Pt && r.Species == 491) return "신월섬: 멤버스카드 직접 배포가 필요합니다.";
        if (v == GameVersion.Pt && r.Species == 492) return "꽃의낙원: 오박사의편지 직접 배포가 필요합니다.";
        if (v == GameVersion.Pt && r.Species is 377 or 378 or 379) return "유적 포획: 해당 게임 대상 배포 레지기가스가 필요합니다.";
        if (v is GameVersion.D or GameVersion.P or GameVersion.Pt && r.Species == 486) return "레지락·레지아이스·레지스틸을 허용된 입수 경로로 확보해야 합니다.";
        if (v is GameVersion.D or GameVersion.P or GameVersion.Pt && r.Species == 442) return "쐐기돌과 지하통로 대화 조건을 충족해야 합니다.";
        return r.RequiresEvent || r.Kind == RouteKind.Event ? "이벤트/배포 포획입니다. 배포·입장·선행 개체 조건은 충족한 것으로 가정합니다." : "";
    }

    private static ushort RequiredTradeSpecies(GameVersion v, ushort s)
    {
        if (v is GameVersion.FR or GameVersion.LG)
        {
            ushort common = s switch { 122 => 63, 124 => 61, 83 => 21, 101 => 26, 114 => 48, 86 => 77, _ => 0 };
            if (common != 0) return common;
            return (ushort)(v == GameVersion.FR ? s switch { 29 => 32, 30 => 33, 108 => 55, _ => 0 }
                : s switch { 32 => 29, 33 => 30, 108 => 80, _ => 0 });
        }
        if (v is GameVersion.R or GameVersion.S) return s switch { 296 => 287, 300 => 25, 222 => 182, _ => 0 };
        if (v == GameVersion.E) return s switch { 273 => 280, 311 => 313, 116 => 371, 52 => 300, _ => 0 };
        if (v is GameVersion.D or GameVersion.P or GameVersion.Pt) return s switch { 63 => 66, 441 => 418, 93 => 308, 129 => 456, _ => 0 };
        return 0;
    }
}
