namespace UltimateEditor;

public static class TrainerIdCodec
{
    public static (ushort TID, ushort SID) FromDisplay(string tid, string sid, bool modern)
    {
        if (!uint.TryParse(tid, out var t) || !uint.TryParse(sid, out var s))
            throw new ArgumentException("PKHeX에 표시되는 TID와 SID를 모두 입력하거나 상대 세이브에서 가져오세요.");
        if (!modern)
        {
            if (t > ushort.MaxValue || s > ushort.MaxValue) throw new ArgumentException("TID/SID는 0~65535 범위입니다.");
            return ((ushort)t, (ushort)s);
        }
        var id = (ulong)s * 1_000_000 + t;
        if (t > 999999 || id > uint.MaxValue)
            throw new ArgumentException("표시 TID는 0~999999, 표시 SID와 합친 값은 32비트 범위여야 합니다.");
        return ((ushort)(id & 0xFFFF), (ushort)(id >> 16));
    }
}
