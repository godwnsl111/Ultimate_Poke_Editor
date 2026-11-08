using System.Reflection;
using PKHeX.Core;

namespace UltimateEditor;

// Linked origins remain distinct from native/mirror acquisition and distribution cards.
public static class LinkedCatalog
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<GameVersion, IReadOnlyList<Route>> Cache = new();
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, IEncounterable> Templates = new();
    public static bool HasGameCube(GameVersion v) => v is GameVersion.R or GameVersion.S or GameVersion.E or GameVersion.FR or GameVersion.LG;
    public static bool HasRadar(GameVersion v) => v is GameVersion.B2 or GameVersion.W2;
    public static bool IsSupported(GameVersion v) => HasGameCube(v) || HasRadar(v);
    public static IReadOnlyList<Route> Build(GameVersion v) => Cache.GetOrAdd(v, BuildCore);
    public static IEncounterable GetTemplate(Route route)
    {
        if (!Enum.TryParse<GameVersion>(route.Version, out var v) || !Build(v).Contains(route) || !Templates.TryGetValue(route.Id, out var e))
            throw new ArgumentException("현재 게임에 연결할 수 없는 연동 경로입니다.");
        return e;
    }
    private static IEnumerable<IEncounterable> Table(string type, string field) =>
        (IEnumerable<IEncounterable>)(typeof(PKM).Assembly.GetType("PKHeX.Core." + type, true)!
            .GetField(field, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)!.GetValue(null)!);
    private static IReadOnlyList<Route> BuildCore(GameVersion v)
    {
        if (!IsSupported(v)) return [];
        var entries = new List<(string Source, IEncounterable Encounter)>();
        if (HasRadar(v)) entries.AddRange(Table("Encounters5DR", "Encounter_DreamRadar").Select(e => ("DreamRadar", e)));
        else
        {
            for (ushort species = 1; species <= 386; species++)
            {
                EvoCriteria[] chain = [new() { Species = species, LevelMin = 1, LevelMax = 100 }];
                foreach (var e in EncounterGenerator3GC.Instance.GetPossible(new PK3(), chain, GameVersion.CXD,
                             EncounterTypeGroup.Static | EncounterTypeGroup.Trade | EncounterTypeGroup.Slot))
                    entries.Add((e.GetType().Name.Contains("XD", StringComparison.Ordinal) ? "XD" : "COLO", e));
            }
            entries.AddRange(Table("Encounters3RSE", "ColoGiftsS").Select(e => ("COLO", e)));
        }
        var result = new List<Route>();
        // Choose the lowest acquisition level within each origin; retain alternate origins.
        foreach (var group in entries.GroupBy(x => (x.Source, x.Encounter.Species, x.Encounter.Form)))
        {
            var (source, e) = group.OrderBy(x => x.Encounter.LevelMin).ThenBy(x => x.Encounter.Location).First();
            string extra = e.Species switch
            {
                249 when source == "DreamRadar" => "소울실버 카트리지 필요. ",
                250 when source == "DreamRadar" => "하트골드 카트리지 필요. ",
                483 when source == "DreamRadar" => "디아루가 카트리지 필요. ",
                484 when source == "DreamRadar" => "펄기아 카트리지 필요. ",
                487 when source == "DreamRadar" => "기라티나 카트리지 필요. ",
                _ => "",
            };
            var requirement = source == "DreamRadar" ? extra + "AR 서처 포획·해금・클리어 및 전설 수령 조건은 충족한 것으로 가정. 현재 BW2 배지 수에 따라 레벨 5~40 자동 설정."
                : "원본 게임 입수·GBA 교환 해금 조건은 충족한 것으로 가정. 다크 포켓몬은 리라이브 완료 개체로 생성. 보너스 디스크 배포 제외.";
            if (source == "COLO" && e.Location == 128) requirement += " 카드 e 룸: 일본어 콜로세움 및 e리더·대응 카드 필요.";
            var id = $"{v}-linked-{source}-{e.Species}-{e.Form}";
            Templates[id] = e;
            result.Add(new(id, e.Species, e.Form, v.ToString(), RouteKind.Linked, e.LevelMin, e.LevelMax,
                e.Location, source, e.LongName, Requirement: requirement, LinkedSource: source));
        }
        return result.OrderBy(r => r.Species).ThenBy(r => r.LinkedSource).ToArray();
    }
}
