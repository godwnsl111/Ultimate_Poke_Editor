using PKHeX.Core;
using System.Globalization;

namespace UltimateEditor;

public sealed record DistributionDateInfo(DateOnly? CardDate, DateOnly? Start, DateOnly? End, DateOnly? Suggested, string Source, bool NeedsExplicitDate = false)
{
    public string Period => Start is { } start ? $"{start:yyyy-MM-dd} ~ {(End is { } end ? end.ToString("yyyy-MM-dd") : "종료일 미기록")}" : "기간 미기록";
    public string CardDateText => CardDate?.ToString("yyyy-MM-dd") ?? "미기록";
    public string SuggestedText => Suggested?.ToString("yyyy-MM-dd") ?? (NeedsExplicitDate ? "날짜 직접 입력 필요" : "기본 생성 날짜 유지");
}

public static class DistributionDates
{
    public static DateOnly? ParseInput(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim() == "자동") return null;
        if (!DateOnly.TryParseExact(value.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) || date.Year is < 2000 or > 2099)
            throw new ArgumentException("날짜는 2000~2099년의 yyyy-MM-dd 형식으로 입력하거나 '자동'으로 두세요.");
        return date;
    }
    public static DistributionDateInfo Describe(DataMysteryGift gift)
    {
        DateOnly? stored = gift switch { PCD g => g.Gift.PK.MetDate, PGF g => g.Date, WC6 g => g.Date, WC7 g => g.Date, WB7 g => g.Date, _ => null };
        bool known = TryWindow(gift, out var window);
        return known ? new(stored, window.Start, window.End, window.GetGenerateDate(), "PKHeX 수령 가능 기간")
            : new(stored, null, null, stored, stored is null ? (gift is WC6 or WC7 ? "원본 날짜 없음 · 날짜 직접 입력 필요" : "날짜 정보 없음") : "카드 기록일 (공식 배포 기간 미확인)", stored is null && gift is (WC6 or WC7));
    }
    public static bool TryWindow(DataMysteryGift gift, out DistributionWindow window)
    {
        window = default;
        return gift switch {
            WB7 g => g.GetDistributionWindow(out window), WC8 g => g.GetDistributionWindow(out window),
            WA8 g => g.GetDistributionWindow(out window), WB8 g => g.GetDistributionWindow(out window),
            WC9 g => g.GetDistributionWindow(out window), WA9 g => g.GetDistributionWindow(out window), _ => false,
        };
    }
    public static DateOnly? Expected(GenerationSettings settings, DistributionDateInfo? info) => settings.MetDate ?? info?.Suggested;
    public static void Apply(PKM pk, IEncounterable encounter, GenerationSettings settings, DistributionDateInfo? info)
    {
        var date = Expected(settings, info);
        if (date is null)
        {
            if (info?.NeedsExplicitDate == true) throw new ArgumentException("이 배포 카드 원본에는 날짜가 없습니다. 오늘 날짜를 배포일로 대입하지 않습니다. 미리보기의 '요청 날짜'에 확인한 과거 수령일을 입력하세요.");
            return;
        }
        if (pk.Format < 4) throw new ArgumentException("이 세대의 개체 형식에는 만난 날짜가 저장되지 않습니다.");
        if (date.Value.Year is < 2000 or > 2099) throw new ArgumentException("만난 날짜는 2000~2099년 범위여야 합니다.");
        if (info?.Start is { } start && (date < start || info.End is { } end && date > end))
            throw new ArgumentException($"만난 날짜가 이 배포의 PKHeX 수령 가능 기간 밖입니다: {info.Period}");
        // A gift egg's acquisition date is EggMetDate, not an invented hatch date.
        if (pk.IsEgg) pk.EggMetDate = date;
        pk.MetDate = date;
        if (pk is PB7 pb && !pk.IsEgg) pb.ReceivedDate = date;
    }
    public static bool Matches(PKM pk, GenerationSettings settings, DistributionDateInfo? info)
    {
        var expected = Expected(settings, info);
        return expected is null ? info?.NeedsExplicitDate != true : pk.MetDate == expected && (!pk.IsEgg || pk.EggMetDate == expected);
    }
}
