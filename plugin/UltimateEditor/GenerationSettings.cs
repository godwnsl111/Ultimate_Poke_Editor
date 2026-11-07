using PKHeX.Core;
using System.Text.Json.Serialization;

namespace UltimateEditor;

// IV order is HP / Atk / Def / SpA / SpD / Speed, never the packed game's order.
public sealed record GenerationSettings(int Shiny = -1, int Nature = -1, int Ability = -1,
    int HP = -1, int Attack = -1, int Defense = -1, int SpAttack = -1, int SpDefense = -1, int Speed = -1,
    int TimeoutSeconds = 30, int Gender = -1, DateOnly? MetDate = null)
{
    [JsonIgnore] public int[] IVs => [HP, Attack, Defense, SpAttack, SpDefense, Speed];
    [JsonIgnore] public bool IsSpecified => Gender != -1 || Shiny != -1 || Nature != -1 || Ability != -1 || IVs.Any(i => i != -1);
    public void Validate()
    {
        if (Gender is < -1 or > 2 || Shiny is < -1 or > 1 || Nature is < -1 or > 24 || Ability is < -1 or > 2 || IVs.Any(i => i is < -1 or > 31) || TimeoutSeconds is < 1 or > 120)
            throw new ArgumentException("생성 조건 범위를 확인하세요.");
        if (MetDate is { Year: < 2000 or > 2099 }) throw new ArgumentException("만난 날짜는 2000~2099년 범위여야 합니다.");
    }
    [JsonIgnore] public EncounterCriteria Criteria => new() {
        Gender = Gender < 0 ? PKHeX.Core.Gender.Random : (PKHeX.Core.Gender)Gender,
        Shiny = Shiny switch { 0 => PKHeX.Core.Shiny.Never, 1 => PKHeX.Core.Shiny.Always, _ => PKHeX.Core.Shiny.Random },
        Nature = Nature < 0 ? PKHeX.Core.Nature.Random : (Nature)Nature,
        Ability = Ability switch { 0 => AbilityPermission.OnlyFirst, 1 => AbilityPermission.OnlySecond, 2 => AbilityPermission.OnlyHidden, _ => AbilityPermission.Any12H },
        IV_HP = (sbyte)HP, IV_ATK = (sbyte)Attack, IV_DEF = (sbyte)Defense,
        IV_SPA = (sbyte)SpAttack, IV_SPD = (sbyte)SpDefense, IV_SPE = (sbyte)Speed,
    };
    public bool Matches(PKM pk) => (Gender == -1 || pk.Gender == Gender) && (Shiny == -1 || pk.IsShiny == (Shiny == 1)) &&
        (Nature == -1 || (int)pk.Nature == Nature) && (Ability == -1 || pk.AbilityNumber == (1 << Ability)) &&
        IVs.Zip(new[] { pk.IV_HP, pk.IV_ATK, pk.IV_DEF, pk.IV_SPA, pk.IV_SPD, pk.IV_SPE }).All(p => p.First == -1 || p.First == p.Second);
    [JsonIgnore] public string Summary => (!IsSpecified ? "기본값" : $"성별:{(Gender < 0 ? "무관" : Gender == 0 ? "수컷" : Gender == 1 ? "암컷" : "무성")} / 이로치:{(Shiny < 0 ? "무관" : Shiny == 1 ? "예" : "아니오")} / 성격:{(Nature < 0 ? "무관" : ((PKHeX.Core.Nature)Nature).ToString())} / 특성:{(Ability < 0 ? "무관" : Ability == 2 ? "숨겨진" : (Ability + 1).ToString())} / IV:{string.Join("/", IVs.Select(i => i < 0 ? "*" : i.ToString()))}") + (MetDate is { } date ? $" / 날짜:{date:yyyy-MM-dd}" : " / 날짜:자동");
}

public sealed record GenerationRequest(Route Route, TrainerProfile Trainer, GenerationSettings Settings);
public sealed record GenerationReply(string EntityType = "", string Data = "", string Method = "", uint? Seed = null, string? Error = null);
