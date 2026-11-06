using System.Security.Cryptography;
using PKHeX.Core;

namespace UltimateEditor;

public sealed record PlannedSlot(Route Route, int Box, int Slot, PKM Pokemon, GenerationSettings? Settings = null, string Generator = "PKHeX 기본", uint? Seed = null);

// Plans only modify private clones. Live writes happen in Commit, after a stale-state check.
public sealed class InjectionPlan
{
    private readonly SaveFile target;
    private readonly SaveFile before;
    private readonly SaveFile prepared;
    private readonly byte[] beforeHash;
    private byte[]? committedHash;
    private readonly bool wasEdited;
    public IReadOnlyList<PlannedSlot> Slots { get; }
    public bool Committed { get; private set; }

    private InjectionPlan(SaveFile live, SaveFile original, SaveFile staged, IReadOnlyList<PlannedSlot> slots)
    {
        target = live; before = original; prepared = staged; Slots = slots;
        beforeHash = Fingerprint(original); wasEdited = live.State.Edited;
    }

    public static SaveFile CloneSave(SaveFile save)
    {
        var clone = save.Clone();
        // Gen3 CloneInternal does not preserve the user-resolved paired edition.
        if (clone.Version != save.Version) clone.Version = save.Version;
        if (clone is SAV3FRLG frlg && save.Version is GameVersion.FR or GameVersion.LG) frlg.ResetPersonal(save.Version);
        return clone;
    }

    public static byte[] Fingerprint(SaveFile save) => SHA256.HashData(CloneSave(save).Write().ToArray()
        .Concat(new[] { (byte)save.Version, (byte)save.Context }).ToArray());

    public static IReadOnlyList<int> EmptySlots(SaveFile save, int startBox)
    {
        if (save.BoxCount <= 0 || save.BoxSlotCount <= 0) throw new InvalidOperationException("박스 저장 공간이 없습니다.");
        if (startBox < 0 || startBox >= save.BoxCount) throw new ArgumentOutOfRangeException(nameof(startBox));
        var canonical = new HashSet<int>();
        for (int i = save.NextOpenBoxSlot(); i >= 0; i = save.NextOpenBoxSlot(i)) canonical.Add(i);
        var result = new List<int>();
        var blank = save.BlankPKM;
        for (int offset = 0; offset < save.BoxCount; offset++)
        {
            int box = (startBox + offset) % save.BoxCount;
            for (int slot = 0; slot < save.BoxSlotCount; slot++)
            {
                int index = box * save.BoxSlotCount + slot;
                if (!canonical.Contains(index) || save.IsBoxSlotLocked(index) || save.IsBoxSlotOverwriteProtected(index)) continue;
                var pk = save.GetBoxSlotAtIndex(index);
                if (pk.Species != 0 || pk.IsEgg) continue;
                var data = pk.Data[..pk.SIZE_STORED];
                // Be conservative: unexplained bytes in an apparently empty slot are preserved.
                if (data.IndexOfAnyExcept((byte)0) >= 0 && !data.SequenceEqual(blank.Data[..blank.SIZE_STORED])) continue;
                result.Add(index);
            }
        }
        return result;
    }

    public static InjectionPlan Prepare(SaveFile live, GameVersion baseVersion, IReadOnlyList<Route> routes,
        IReadOnlyDictionary<GameVersion, TrainerProfile> trainers, int startBox, CancellationToken token = default,
        SaveFile? snapshot = null, bool prerequisitesReviewed = false, IReadOnlyDictionary<string, byte>? tradeLevels = null, IReadOnlyDictionary<GameVersion, TrainerProfile>? linkedTrainers = null, IReadOnlyDictionary<string, GenerationSettings>? generationSettings = null)
    {
        if (routes.Count == 0) throw new ArgumentException("추가할 포켓몬을 선택하세요.");
        if (!GameProfile.ResolveCandidates(live.Version).Contains(baseVersion)) throw new ArgumentException("열린 세이브와 기본 버전이 다릅니다.");
        if (routes.Select(r => r.Id).Distinct().Count() != routes.Count) throw new ArgumentException("동일한 경로/배포 카드는 한 번만 선택하세요.");
        if (routes.Where(r => r.Kind is not (RouteKind.Distribution or RouteKind.Linked)).GroupBy(r => (r.Species, r.Form)).Any(g => g.Count() > 1))
            throw new ArgumentException("같은 종/폼의 입수 경로는 하나만 선택하세요.");
        var original = snapshot ?? CloneSave(live);
        var staged = CloneSave(original);
        var empty = EmptySlots(staged, startBox);
        if (empty.Count < routes.Count) throw new InvalidOperationException($"빈 슬롯이 부족합니다. 필요 {routes.Count}칸 / 사용 가능 {empty.Count}칸. 아무것도 반영하지 않았습니다.");
        var sourceTrainers = new Dictionary<GameVersion, ITrainerInfo> { [baseVersion] = new SimpleTrainerInfo(original, baseVersion) };
        foreach (var version in routes.Select(r => Enum.Parse<GameVersion>(r.Version)).Distinct().Where(v => v != baseVersion))
        {
            if (!MirrorCatalog.Partners(baseVersion).Contains(version) || !trainers.TryGetValue(version, out var tr))
                throw new ArgumentException($"{GameProfile.DisplayName(version)} 트레이너 정보가 필요합니다.");
            if (tr.Version != version.ToString() || string.IsNullOrWhiteSpace(tr.OT) || tr.Gender is < 0 or > 1)
                throw new ArgumentException("상대 버전 트레이너 정보가 올바르지 않습니다.");
            var profile = GameProfile.For(version);
            if (profile.Generation is 6 or 7 && (tr.Country is null || tr.Region is null || tr.ConsoleRegion is null))
                throw new ArgumentException("6·7세대 상대 트레이너는 국가·지역 정보를 위해 '상대 세이브에서 가져오기'를 사용하세요.");
            sourceTrainers[version] = new SimpleTrainerInfo(version) { OT = tr.OT, TID16 = tr.TID, SID16 = tr.SID,
                Gender = (byte)tr.Gender, Language = tr.Language, Country = tr.Country ?? 0,
                Region = tr.Region ?? 0, ConsoleRegion = tr.ConsoleRegion ?? 0 };
        }
        var slots = new List<PlannedSlot>();
        for (int i = 0; i < routes.Count; i++)
        {
            token.ThrowIfCancellationRequested();
            var route = routes[i];
            if (route.Kind is not (RouteKind.Wild or RouteKind.Gift or RouteKind.GiftEgg or RouteKind.Trade or RouteKind.Event or RouteKind.Distribution or RouteKind.Linked))
                throw new ArgumentException("지원하지 않는 입수 경로입니다.");
            if (route.NeedsReview && !prerequisitesReviewed)
                throw new ArgumentException($"{route.Species}: 선행 조건을 확인해야 합니다. {route.Requirement}");
            if (route.Form >= EncounterUtil.FormDynamic)
                throw new ArgumentException($"{route.Species}: 동적 폼은 아직 주입할 수 없습니다.");
            var encounter = EncounterCatalog.GetTemplate(route);
            var creationTrainer = sourceTrainers[Enum.Parse<GameVersion>(route.Version)];
            if (route.LinkedSource is "COLO" or "XD")
            {
                var source = Enum.Parse<GameVersion>(route.LinkedSource);
                if (linkedTrainers is null || !linkedTrainers.TryGetValue(source, out var tr) || tr.Version != source.ToString() || string.IsNullOrWhiteSpace(tr.OT) || tr.Gender != 0 || tr.Language is not (1 or 2 or 3 or 4 or 5 or 7))
                    throw new ArgumentException("콜로세움/XD 원본 트레이너 OT·TID·SID·언어가 필요합니다.");
                if (encounter is EncounterShadow3Colo && route.Location == 128 && tr.Language != 1) throw new ArgumentException("콜로세움 카드 e 룸은 일본어 원본만 지원합니다.");
                creationTrainer = new SimpleTrainerInfo(GameVersion.CXD) { OT = tr.OT, TID16 = tr.TID, SID16 = tr.SID, Gender = 0, Language = tr.Language };
            }
            var version = Enum.Parse<GameVersion>(route.Version);
            AcquisitionRequirements.CheckLanguage(route, sourceTrainers[version].Language);
            if (encounter is DataMysteryGift gift) DistributionCatalog.CheckLanguage(gift, sourceTrainers[version].Language);
            byte tradeLevel = route.MinLevel;
            if (route.Kind == RouteKind.Trade)
            {
                if (tradeLevels is not null && tradeLevels.TryGetValue(route.Id, out var chosen)) tradeLevel = chosen;
                else if (!route.TradeLevelKnown) throw new ArgumentException("이 NPC 교환은 교환 레벨을 직접 입력해야 합니다.");
                if (tradeLevel < route.MinLevel || tradeLevel > route.MaxLevel) throw new ArgumentException("교환 레벨이 허용 범위 밖입니다.");
            }
            var settings = generationSettings is not null && generationSettings.TryGetValue(route.Id, out var configured) ? configured : new GenerationSettings();
            settings.Validate();
            string generator = "PKHeX 기본"; uint? generationSeed = null;
            PKM? accepted = null;
            string failure = "생성 실패";
            for (int attempt = 0; attempt < 3; attempt++)
            {
                token.ThrowIfCancellationRequested();
                PKM generated;
                if (settings.IsSpecified)
                {
                    var result = GenerationWorkerClient.Generate(route, creationTrainer, settings, token);
                    generated = result.Pokemon; generator = result.Method; generationSeed = result.Seed;
                }
                else generated = encounter.ConvertToPKM(creationTrainer, EncounterCriteria.Unrestricted);
                if (route.LinkedSource is "COLO" or "XD") generated = generated switch { CK3 ck => ck.ConvertToPK3(), XK3 xk => xk.ConvertToPK3(), _ => throw new InvalidOperationException("GC 원본 형식이 아닙니다.") };
                if (route.LinkedSource == "DreamRadar")
                {
                    if (original is not SAV5B2W2 b2w2) throw new ArgumentException("드림 AR 서처는 BW2 전용입니다.");
                    int badges = System.Numerics.BitOperations.PopCount((uint)b2w2.Misc.Badges & 255);
                    byte level = (byte)(badges == 0 ? 5 : ((badges + 1) / 2) * 10);
                    generated.CurrentLevel = level; generated.MetLevel = level;
                    EncounterUtil.SetEncounterMoves(generated, version, level);
                }
                if (generated.GetType() != staged.PKMType) throw new NotSupportedException("이 입수 경로와 세이브의 개체 형식이 다릅니다.");
                if (generated.Species != route.Species || generated.Form != route.Form ||
                    route.Kind is not (RouteKind.Distribution or RouteKind.Linked) && !generated.Version.Contains(version) && !version.Contains(generated.Version))
                    throw new InvalidOperationException("생성된 종·폼·출신 버전이 선택한 경로와 다릅니다.");
                if (route.Kind == RouteKind.Trade)
                {
                    // Gen3/4 trade templates encode a breeding-permitted minimum. Preserve PID/OT/IV,
                    // but obtain at the reviewed no-breeding level and refresh only non-fixed moves.
                    if (generated.Format <= 4)
                    {
                        generated.CurrentLevel = tradeLevel;
                        if (generated.Format == 3 || encounter is EncounterTrade4PID { MetLocation: 0 }) generated.MetLevel = tradeLevel;
                        if (encounter is not IMoveset { Moves.HasMoves: true }) EncounterUtil.SetEncounterMoves(generated, version, tradeLevel);
                    }
                    else if (generated.CurrentLevel != tradeLevel)
                        throw new ArgumentException("이 세대 NPC 교환은 템플릿의 고정 수령 레벨을 사용해야 합니다.");
                }
                staged.AdaptToSaveFile(generated, false, EntityImportOption.Enable);
                generated.RefreshChecksum();
                var legality = new LegalityAnalysis(generated);
                if (generated.Valid && legality.Valid && settings.Matches(generated)) { accepted = generated; break; }
                failure = !settings.Matches(generated) ? "생성 결과가 지정 조건과 다릅니다. 조건은 임의로 완화하지 않았습니다." : legality.Report();
            }
            if (accepted is null) throw new InvalidOperationException($"{route.Species} ({route.Version}) 적법성 검사 실패:\n{failure}");
            int index = empty[i];
            staged.SetBoxSlotAtIndex(accepted, index, new(EntityImportOption.Disable, EntityImportOption.Enable, EntityImportOption.Enable));
            var stored = staged.GetBoxSlotAtIndex(index);
            Validate(stored, route, settings);
            slots.Add(new(route, index / staged.BoxSlotCount, index % staged.BoxSlotCount, stored, settings, generator, generationSeed));
        }
        token.ThrowIfCancellationRequested();
        // Serializing clones also validates game-specific checksums and split storage (notably Gen3).
        var serialized = staged.Write().ToArray();
        var parsed = SaveUtil.GetSaveFile(serialized) ?? throw new InvalidOperationException("수정 복제본 재판독에 실패했습니다.");
        if (!parsed.ChecksumsValid) throw new InvalidOperationException("수정 복제본 체크섬 검사 실패.");
        var destinations = empty.Take(routes.Count).ToHashSet();
        for (int i = 0; i < original.SlotCount; i++)
            if (!destinations.Contains(i) && !SlotBytes(original.GetBoxSlotAtIndex(i)).SequenceEqual(SlotBytes(parsed.GetBoxSlotAtIndex(i))))
                throw new InvalidOperationException($"기존 슬롯 {i + 1} 보존 검사 실패. 반영하지 않습니다.");
        foreach (var slot in slots) Validate(parsed.GetBoxSlotAtIndex(slot.Box, slot.Slot), slot.Route, slot.Settings);
        return new(live, original, staged, slots);
    }

    private static byte[] SlotBytes(PKM pk) => pk.Data[..pk.SIZE_STORED].ToArray()
        .Concat(pk.NicknameTrash.ToArray()).Concat(pk.OriginalTrainerTrash.ToArray()).ToArray();
    private static void Validate(PKM pk, Route route, GenerationSettings? settings = null)
    {
        if (pk.Species != route.Species || pk.Form != route.Form || !pk.Valid || settings is not null && !settings.Matches(pk))
            throw new InvalidOperationException("박스 재판독에서 개체 정보가 일치하지 않습니다.");
        var legality = new LegalityAnalysis(pk);
        if (!legality.Valid) throw new InvalidOperationException($"박스 반영 후 적법성 실패:\n{legality.Report()}");
    }

    public void Commit(SaveFile current)
    {
        if (Committed) throw new InvalidOperationException("이미 반영한 계획입니다.");
        if (!ReferenceEquals(current, target) || !Fingerprint(current).SequenceEqual(beforeHash))
            throw new InvalidOperationException("미리보기 이후 세이브가 바뀌었습니다. 다시 미리보기를 만드세요.");
        try
        {
            current.CopyChangesFrom(prepared);
            if (!Fingerprint(current).SequenceEqual(Fingerprint(prepared))) throw new InvalidOperationException("열린 세이브 반영 결과 검증 실패.");
            committedHash = Fingerprint(current); Committed = true;
        }
        catch { current.CopyChangesFrom(before); current.State.Edited = wasEdited; throw; }
    }

    public void Undo(SaveFile current)
    {
        if (!Committed || !ReferenceEquals(current, target) || !Fingerprint(current).SequenceEqual(committedHash!))
            throw new InvalidOperationException("이번 주입 후 세이브가 바뀌어 자동 취소할 수 없습니다.");
        current.CopyChangesFrom(before); current.State.Edited = wasEdited; Committed = false;
    }
}
