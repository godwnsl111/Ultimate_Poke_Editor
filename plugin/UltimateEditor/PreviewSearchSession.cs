namespace UltimateEditor;

// An edited or failed search must never leave the previous result committable.
public sealed class PreviewSearchSession(InjectionPlan initial)
{
    public InjectionPlan Plan { get; private set; } = initial;
    public bool RequiresSearch { get; private set; } = initial.HasUnconfirmedDates;
    private readonly string[] routes = initial.Slots.Select(s => s.Route.Id).ToArray();
    public void Invalidate() => RequiresSearch = true;
    private static bool Matches(InjectionPlan plan, IReadOnlyDictionary<string, GenerationSettings> settings) =>
        plan.Slots.All(s => settings.TryGetValue(s.Route.Id, out var requested) && requested == (s.Settings ?? new GenerationSettings()));
    public void Accept(InjectionPlan result, IReadOnlyDictionary<string, GenerationSettings> requested)
    {
        Invalidate();
        if (result.HasUnconfirmedDates || !routes.SequenceEqual(result.Slots.Select(s => s.Route.Id)) || !Matches(result, requested))
            throw new InvalidOperationException("다시 검색한 결과가 미리보기의 경로·조건과 다릅니다.");
        Plan = result; RequiresSearch = false;
    }
    public InjectionPlan Confirm(IReadOnlyDictionary<string, GenerationSettings> requested)
    {
        if (RequiresSearch || Plan.HasUnconfirmedDates || !Matches(Plan, requested))
            throw new InvalidOperationException("변경한 조건으로 먼저 검색하세요. 이전 검색 결과는 반영하지 않습니다.");
        return Plan;
    }
}
