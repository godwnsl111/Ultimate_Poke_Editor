using PKHeX.Core;

namespace UltimateEditor;

// Runs in a disposable worker: some native converters perform unbounded RNG
// searches when exact criteria are impossible. The parent enforces the deadline.
public static class GenerationEngine
{
    public static GenerationReply Generate(GenerationRequest request)
    {
        var (route, trainer, settings) = request;
        settings.Validate();
        var encounter = EncounterCatalog.GetTemplate(route);
        if (encounter.Generation < 3 && settings.IsSpecified)
            throw new ArgumentException("1·2세대에는 PID·성격·특성이 없습니다. 현재 생성 조건은 3세대 이후에 사용하세요.");
        if (settings.Gender != -1)
        {
            var blank = EntityBlank.GetBlank(encounter.Context);
            blank.Version = Enum.Parse<GameVersion>(route.Version); blank.Species = route.Species; blank.Form = route.Form;
            var pi = blank.PersonalInfo;
            int fixedGender = pi.Genderless ? 2 : pi.OnlyMale ? 0 : pi.OnlyFemale ? 1 : -1;
            if (fixedGender != -1 && fixedGender != settings.Gender || settings.Gender == 2 && fixedGender != 2 ||
                encounter is IFixedGender fixedEncounter && fixedEncounter.IsFixedGender && fixedEncounter.Gender != settings.Gender)
                throw new ArgumentException("이 종·폼 또는 입수 경로에서는 요청한 포켓몬 성별을 얻을 수 없습니다.");
        }
        if (encounter.Generation < 5 && settings.Ability == 2) throw new ArgumentException("이 세대에는 숨겨진 특성이 없습니다.");
        if (encounter.Shiny == Shiny.Never && settings.Shiny == 1)
            throw new ArgumentException("이 입수 경로는 이로치 잠금입니다.");
        if (encounter.Shiny.IsShiny() && settings.Shiny == 0)
            throw new ArgumentException("이 입수 경로는 이로치 고정입니다.");
        var info = new SimpleTrainerInfo(route.LinkedSource is "COLO" or "XD" ? GameVersion.CXD : Enum.Parse<GameVersion>(trainer.Version)) {
            OT = trainer.OT, TID16 = trainer.TID, SID16 = trainer.SID, Language = trainer.Language, Gender = (byte)trainer.Gender,
            Country = trainer.Country ?? 0, Region = trainer.Region ?? 0, ConsoleRegion = trainer.ConsoleRegion ?? 0,
        };
        if (encounter is DataMysteryGift gift) DistributionCatalog.CheckLanguage(gift, trainer.Language);

        // The upstream random Method H converter filters shininess before the
        // first nature match. A real encounter stops at that match, shiny or not.
        // Search whole encounters instead, with the worker enforcing the deadline.
        if (encounter is EncounterSlot3 wild && wild.Species != 201 && (settings.Shiny == 1 || settings.IVs.Count(i => i >= 0) < 3))
        {
            var pk = (PK3)wild.ConvertToPKM(info, EncounterCriteria.Unrestricted);
            if (settings.Shiny == 1)
            {
                uint origin = GenerateShinyWild3(wild, pk, settings);
                return Reply(pk, "Method H 이로치 PID 역산·프레임 검증", origin);
            }
            GenerateWild3(wild, pk, settings);
            return Reply(pk, "Method H 조우 프레임 검증", MethodFinder.Analyze(pk).OriginSeed);
        }

        bool reversed = encounter.Generation is 3 or 4 && settings.IVs.Count(i => i >= 0) >= 3 &&
            route.Kind != RouteKind.Distribution && route.Kind != RouteKind.Trade;
        if (reversed)
        {
            var baseline = encounter.ConvertToPKM(info, EncounterCriteria.Unrestricted);
            CheckTrainer(baseline, encounter, route, info);
            foreach (var ivs in CompleteIVs(settings.IVs))
            foreach (var candidate in PokeFinderRng.Calculate(ivs, settings.Nature, baseline.TID16))
            {
                bool gc = baseline is CK3 or XK3;
                if (gc != (candidate.Method == "XDColo")) continue;
                if (encounter.Generation == 3 && candidate.Method.StartsWith("CuteCharm")) continue;
                var pk = baseline.Clone();
                pk.PID = candidate.PID;
                pk.IV_HP = ivs[0]; pk.IV_ATK = ivs[1]; pk.IV_DEF = ivs[2];
                pk.IV_SPA = ivs[3]; pk.IV_SPD = ivs[4]; pk.IV_SPE = ivs[5];
                pk.Gender = EntityGender.GetFromPIDAndRatio(pk.PID, pk.PersonalInfo.Gender);
                pk.RefreshAbility(gc ? PokeFinderRng.GameCubeAbility(candidate.Seed) : (int)(pk.PID & 1));
                if (!settings.Matches(pk) || pk.Species != route.Species || pk.Form != route.Form) continue;
                // Slot RNG also chooses the encounter level. Try the route's actual
                // range, lowest first, rather than retaining an unrelated random level.
                bool variableSlot = encounter is EncounterSlot3 or EncounterSlot4 or EncounterSlot3XD;
                for (int level = variableSlot ? route.MinLevel : pk.MetLevel; level <= (variableSlot ? route.MaxLevel : pk.MetLevel); level++)
                {
                    if (variableSlot) pk.CurrentLevel = pk.MetLevel = (byte)level;
                    if (IsLegal(pk)) return Reply(pk, "PokeFinder " + candidate.Method, candidate.Seed);
                }
            }
            throw new ArgumentException("지정한 IV·성격·성별·이로치·특성에 맞는 PokeFinder PID 후보 중 선택 경로의 적법성을 통과한 결과가 없습니다. IV 일부를 무관으로 바꾸거나 조건을 조정하세요. (보조 디스크 Channel 방식 제외)");
        }
        // Keep the native event/raid/GC locks. Never force values on a generated
        // Pokemon merely because the converter ignored a requested constraint.
        for (int i = 0; i < 32; i++)
        {
            var pk = encounter.ConvertToPKM(info, settings.Criteria);
            NormalizeTradeBall(pk, encounter);
            CheckTrainer(pk, encounter, route, info);
            if (pk is CK3 or XK3)
            {
                var correlation = MethodFinder.Analyze(pk);
                if (correlation.Type is PIDType.CXD or PIDType.CXDAnti)
                    pk.RefreshAbility(PokeFinderRng.GameCubeAbility(correlation.OriginSeed));
            }
            if (settings.Matches(pk) && IsLegal(pk)) return Reply(pk, "PKHeX 경로별 RNG", null);
        }
        throw new ArgumentException("이 입수 경로의 고정값 또는 RNG 제약으로 지정 조건을 만족하지 못했습니다. 조건은 임의로 완화하지 않았습니다.");
    }
    private static void CheckTrainer(PKM pk, IEncounterable encounter, Route route, ITrainerInfo trainer)
    {
        if (route.Kind is RouteKind.Distribution or RouteKind.Trade || encounter is IFixedTrainer { IsFixedTrainer: true }) return;
        if (pk.ID32 != trainer.ID32) throw new ArgumentException("이 경로의 RNG 생성기가 원본 TID/SID와 다른 결과를 반환했습니다. 원본 ID를 변경하는 결과는 채택하지 않습니다. 조건을 조정하세요.");
    }
    public static void NormalizeTradeBall(PKM pk, IEncounterable encounter)
    {
        // The pinned converter assigns Ball before Version, leaving HG/SS's
        // extended ball byte unset. Reapply the encounter's fixed ball after it.
        if (pk is PK4 { HGSS: true } && encounter is EncounterTrade4PID trade)
            pk.Ball = (byte)trade.FixedBall;
    }
    private static IEnumerable<int[]> CompleteIVs(int[] input)
    {
        int[] ivs = (int[])input.Clone();
        int[] unset = Enumerable.Range(0, 6).Where(i => ivs[i] < 0).ToArray();
        int combinations = 1 << (5 * unset.Length);
        for (int value = 0; value < combinations; value++)
        {
            int remaining = value;
            foreach (int index in unset) { ivs[index] = remaining & 31; remaining >>= 5; }
            yield return ivs;
        }
    }
    private static bool IsLegal(PKM pk)
    {
        var native = pk switch { CK3 ck => (PKM)ck.ConvertToPK3(), XK3 xk => xk.ConvertToPK3(), _ => pk };
        native.RefreshChecksum();
        var analysis = new LegalityAnalysis(native);
        return analysis.Valid && analysis.Info.FrameMatches && analysis.Info.PIDIVMatches;
    }
    private static void GenerateWild3(EncounterSlot3 encounter, PK3 pk, GenerationSettings settings)
    {
        uint seed = Util.Rand32();
        var (min, max) = SlotMethodH.GetRange(encounter.Type, encounter.SlotNumber);
        while (true)
        {
            if (MethodH.IsEncounterCheckApplicable(encounter.Type) &&
                !MethodH.CheckEncounterActivation(encounter, seed, LeadRequired.None, out _))
            { seed = LCRNG.Next(seed); continue; }
            uint slot = LCRNG.Next16(ref seed) % 100;
            if (slot < min || slot > max) continue;
            uint levelRoll = LCRNG.Next16(ref seed);
            uint nature = LCRNG.Next16(ref seed) % 25;
            if (settings.Nature >= 0 && settings.Nature != nature) continue;
            uint pid;
            do
            {
                uint low = LCRNG.Next16(ref seed), high = LCRNG.Next16(ref seed);
                pid = low | high << 16;
            } while (pid % 25 != nature);
            // Never continue the PID loop after the first nature match.
            uint ivs = ClassicEraRNG.GetSequentialIVs(ref seed);
            pk.PID = pid; pk.IV32 = ivs;
            pk.RefreshAbility((int)(pid & 1));
            pk.CurrentLevel = pk.MetLevel = (byte)MethodH.GetRandomLevel(encounter, levelRoll, LeadRequired.None);
            if (settings.Matches(pk) && IsLegal(pk)) return;
        }
    }
    private static uint GenerateShinyWild3(EncounterSlot3 encounter, PK3 pk, GenerationSettings settings)
    {
        // With fixed TID/SID there are only 65,536 * 8 shiny PIDs.
        // Recover their RNG origins, filter IVs, then verify the actual encounter.
        Span<uint> seeds = stackalloc uint[16];
        uint start = Util.Rand32() & 65535;
        int[] requestedIVs = settings.IVs;
        for (uint offset = 0; offset < 65536; offset++)
        {
            uint low = (start + offset) & 65535;
            for (uint xor = 0; xor < 8; xor++)
            {
                uint high = low ^ pk.TID16 ^ pk.SID16 ^ xor;
                uint pid = low | high << 16;
                if (settings.Nature >= 0 && pid % 25 != settings.Nature ||
                    settings.Ability >= 0 && (pid & 1) != settings.Ability) continue;
                byte gender = EntityGender.GetFromPIDAndRatio(pid, pk.PersonalInfo.Gender);
                if (settings.Gender >= 0 && gender != settings.Gender) continue;
                int count = LCRNGReversal.GetSeeds(seeds, low << 16, high << 16);
                for (int i = 0; i < count; i++)
                {
                    uint origin = seeds[i];
                    // H1 (ABCD), H2 (AB-DE), H4 (ABC-E).
                    for (int method = 0; method < 3; method++)
                    {
                        uint state = LCRNG.Next2(origin);
                        if (method == 1) state = LCRNG.Next(state);
                        uint iv1 = LCRNG.Next16(ref state) & 32767;
                        if (method == 2) state = LCRNG.Next(state);
                        uint iv2 = LCRNG.Next16(ref state) & 32767;
                        uint packed = iv1 | iv2 << 15;
                        if (!MatchesIVs(packed, requestedIVs)) continue;
                        var lead = MethodH.GetSeed(encounter, origin, pk.E, gender);
                        if (!lead.IsValid) continue;
                        pk.PID = pid; pk.IV32 = packed; pk.RefreshAbility((int)(pid & 1));
                        uint levelRoll = MethodH.SkipToLevelRand(encounter, lead.Seed) >> 16;
                        pk.CurrentLevel = pk.MetLevel = (byte)MethodH.GetRandomLevel(encounter, levelRoll, lead.Lead);
                        if (settings.Matches(pk) && IsLegal(pk)) return origin;
                    }
                }
            }
        }
        throw new ArgumentException("해당 TID/SID와 선택한 야생 경로에서 이로치·성격·성별·특성·IV 조건을 함께 만족하는 H1/H2/H4 결과가 없습니다. 조건 일부를 무관으로 바꾸세요.");
    }
    private static bool MatchesIVs(uint packed, int[] ivs)
    {
        ReadOnlySpan<int> shifts = [0, 5, 10, 20, 25, 15];
        for (int i = 0; i < 6; i++)
            if (ivs[i] >= 0 && (packed >> shifts[i] & 31) != ivs[i]) return false;
        return true;
    }
    private static GenerationReply Reply(PKM pk, string method, uint? seed)
    {
        pk.RefreshChecksum();
        return new(pk.GetType().Name, Convert.ToBase64String(pk.Data), method, seed);
    }
}
