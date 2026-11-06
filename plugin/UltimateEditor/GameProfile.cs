using PKHeX.Core;

namespace UltimateEditor;

public sealed record GameProfile(byte Generation, ushort MaxSpecies, EntityContext Context)
{
    public static readonly GameVersion[][] Groups = [
        [GameVersion.RD, GameVersion.GN, GameVersion.BU, GameVersion.YW],
        [GameVersion.GD, GameVersion.SI, GameVersion.C],
        [GameVersion.R, GameVersion.S, GameVersion.E], [GameVersion.FR, GameVersion.LG],
        [GameVersion.D, GameVersion.P, GameVersion.Pt], [GameVersion.HG, GameVersion.SS],
        [GameVersion.B, GameVersion.W], [GameVersion.B2, GameVersion.W2],
        [GameVersion.X, GameVersion.Y], [GameVersion.OR, GameVersion.AS],
        [GameVersion.SN, GameVersion.MN], [GameVersion.US, GameVersion.UM],
        [GameVersion.GP, GameVersion.GE], [GameVersion.SW, GameVersion.SH],
        [GameVersion.PLA], [GameVersion.BD, GameVersion.SP], [GameVersion.SL, GameVersion.VL], [GameVersion.ZA],
    ];
    public static readonly GameVersion[] Supported = Groups.SelectMany(x => x).ToArray();
    public static GameProfile For(GameVersion version)
    {
        if (!Supported.Contains(version)) throw new NotSupportedException("현재 PKHeX의 본가 출현 조회가 지원되는 정확한 게임 버전을 선택하세요.");
        var context = version.GetContextFromSaved();
        return new(context.Generation, version.GetMaxSpeciesID(), context);
    }
    public static GameVersion[] ResolveCandidates(GameVersion version) => Supported.Contains(version) ? [version]
        : Supported.Where(v => version.Contains(v)).ToArray();
    public static string DisplayName(GameVersion v) => v switch
    {
        GameVersion.RD => "레드", GameVersion.GN => "그린/블루(해외)", GameVersion.BU => "블루(일본)", GameVersion.YW => "피카츄",
        GameVersion.GD => "골드", GameVersion.SI => "실버", GameVersion.C => "크리스탈",
        GameVersion.R => "루비", GameVersion.S => "사파이어", GameVersion.E => "에메랄드",
        GameVersion.FR => "파이어레드", GameVersion.LG => "리프그린",
        GameVersion.D => "디아루가", GameVersion.P => "펄기아", GameVersion.Pt => "기라티나",
        GameVersion.HG => "하트골드", GameVersion.SS => "소울실버",
        GameVersion.B => "블랙", GameVersion.W => "화이트", GameVersion.B2 => "블랙2", GameVersion.W2 => "화이트2",
        GameVersion.X => "X", GameVersion.Y => "Y", GameVersion.OR => "오메가루비", GameVersion.AS => "알파사파이어",
        GameVersion.SN => "썬", GameVersion.MN => "문", GameVersion.US => "울트라썬", GameVersion.UM => "울트라문",
        GameVersion.GP => "레츠고 피카츄", GameVersion.GE => "레츠고 이브이",
        GameVersion.SW => "소드", GameVersion.SH => "실드", GameVersion.PLA => "레전드 아르세우스",
        GameVersion.BD => "브릴리언트 다이아몬드", GameVersion.SP => "샤이닝 펄",
        GameVersion.SL => "스칼렛", GameVersion.VL => "바이올렛", GameVersion.ZA => "레전드 Z-A",
        _ => v.ToString(),
    };
}
