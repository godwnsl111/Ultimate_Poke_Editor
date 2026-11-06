// SPDX-License-Identifier: GPL-3.0-or-later
// Adapted from PokéFinder Core/Util/IVToPIDCalculator.cpp, commit
// 3624cb7737da247f22d3afa5265a0698f8be8fc1.
// Copyright (C) 2017-2024 Admiral_Fish, bumba, and EzPzStreamz.
// See vendor/pokefinder and THIRD_PARTY_NOTICES.md for source and license.
namespace UltimateEditor;

public sealed record PidCandidate(uint PID, uint Seed, ushort SuggestedSID, string Method);

public static class PokeFinderRng
{
    static uint Next(uint s, bool xd = false) => unchecked(s * (xd ? 0x343FDu : 0x41C64E6Du) + (xd ? 0x269EC3u : 0x6073u));
    static uint Prev(uint s, bool xd = false) => unchecked(s * (xd ? 0xB9B33155u : 0xEEB9EB65u) + (xd ? 0xA170F641u : 0x0A3561A1u));
    // Exhaust the unknown low 16 bits and bit 31. Same recovered states as
    // PokeFinder's lattice reversal, using a simple bounded enumeration.
    static IEnumerable<uint> Recover(int[] iv, bool gap, bool xd)
    {
        uint first = (uint)(iv[0] | iv[1] << 5 | iv[2] << 10) << 16;
        uint second = (uint)(iv[5] | iv[3] << 5 | iv[4] << 10) << 16;
        for (uint low = 0; low <= 65535; low++)
        {
            uint state = first | low;
            uint end = Next(state, xd);
            if (gap) end = Next(end, xd);
            if ((end & 0x7FFF0000) != second) continue;
            yield return state; yield return state ^ 0x80000000;
        }
    }
    public static int GameCubeAbility(uint origin) => (int)((Next(Next(Next(origin, true), true), true) >> 16) & 1);
    public static IReadOnlyList<PidCandidate> Calculate(int[] iv, int nature, ushort tid)
    {
        if (iv.Length != 6 || iv.Any(i => i is < 0 or > 31) || nature is < -1 or > 24) throw new ArgumentException("IV/PID 검색 입력이 잘못됐습니다.");
        var result = new List<PidCandidate>();
        void Add(uint pid, uint seed, string method)
        {
            if (nature >= 0 && pid % 25 != nature) return;
            result.Add(new(pid, seed, (ushort)(((pid >> 16) ^ (pid & 65535) ^ tid) & 0xFFF8), method));
        }
        foreach (uint state in Recover(iv, false, false))
        {
            uint highState = Prev(state), lowState = Prev(highState);
            uint high = highState >> 16, low = lowState >> 16;
            Add(high << 16 | low, Prev(lowState), "Method1");
            Add(low << 16 | high, Prev(lowState), "Method1Reverse");
            uint m2Low = Prev(lowState);
            Add(low << 16 | m2Low >> 16, Prev(m2Low), "Method2");
            if (low / 0x5556 != 0)
                foreach (uint threshold in new uint[] { 0, 0x32, 0x4B, 0x96, 0xC8 })
                    Add(high / 0xA3E + threshold, Prev(lowState), "CuteCharmDPPt");
            if (low % 3 != 0)
                foreach (uint threshold in new uint[] { 0, 0x32, 0x4B, 0x96, 0xC8 })
                    Add(high % 25 + threshold, Prev(lowState), "CuteCharmHGSS");
        }
        foreach (uint state in Recover(iv, true, false))
        {
            uint high = Prev(state), low = Prev(high);
            Add((high & 0xFFFF0000) | low >> 16, Prev(low), "Method4");
        }
        foreach (uint state in Recover(iv, false, true))
        {
            uint high = Next(Next(Next(state, true), true), true), low = Next(high, true);
            Add((high & 0xFFFF0000) | low >> 16, Prev(state, true), "XDColo");
        }
        return result.Distinct().ToArray();
    }
}
