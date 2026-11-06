using PKHeX.Core;

namespace UltimateEditor;

public static class EncounterAdapter
{
    public static RouteKind Classify(IEncounterable e) => e switch
    {
        // External services/accessories are not direct in-game acquisition.
        EncounterTrade4RanchGift or EncounterStatic4Pokewalker or EncounterStatic5Radar or EncounterStatic5Entree => RouteKind.Unknown,
        EncounterSlot1 or EncounterSlot2 or EncounterSlot3 or EncounterSlot4 or EncounterSlot5 or
        EncounterSlot6XY or EncounterSlot6AO or EncounterSlot7 or EncounterSlot7b or EncounterSlot8 or
        EncounterSlot8a or EncounterSlot8b or EncounterSlot9 or EncounterSlot9a => RouteKind.Wild,
        EncounterTrade1 or EncounterTrade2 or EncounterTrade3 or EncounterTrade4PID or EncounterTrade5BW or
        EncounterTrade5B2W2 or EncounterTrade6 or EncounterTrade7 or EncounterTrade7b or EncounterTrade8 or
        EncounterTrade8b or EncounterTrade9 or EncounterTrade9a => RouteKind.Trade,
        EncounterGift9a => RouteKind.Gift,
        IEncounterEgg => RouteKind.BredEgg,
        EncounterStatic1 s => Gen1Gift(s.Species) ? RouteKind.Gift : RouteKind.Wild,
        EncounterStatic2 s => s.IsEgg ? RouteKind.GiftEgg : Gen2Gift(s.Species) ? RouteKind.Gift : RouteKind.Wild,
        EncounterStatic3 or EncounterStatic4 or EncounterStatic5 or EncounterStatic5N or EncounterStatic6 or
        EncounterStatic7 or EncounterStatic7b or EncounterStatic8 or EncounterStatic8a or EncounterStatic8b or
        EncounterStatic9 => e.IsEgg ? RouteKind.GiftEgg : e.FixedBall != Ball.None ? RouteKind.Gift : RouteKind.Wild,
        EncounterStatic8N or EncounterStatic8U or EncounterStatic9a or
        EncounterTera9 or EncounterFixed9 => RouteKind.Wild,
        EncounterStatic8NC or EncounterStatic8ND or EncounterDist9 or EncounterMight9 or EncounterOutbreak9 => RouteKind.Event,
        // Online distribution raids remain unverified event data rather than ordinary local raids.
        _ => RouteKind.Unknown,
    };

    private static bool Gen1Gift(ushort s) => s is 1 or 4 or 7 or 25 or 30 or 33 or 35 or 36 or 37 or 40 or 63
        or 106 or 107 or 116 or 123 or 127 or 129 or 131 or 133 or 137 or 138 or 140 or 142 or 147 or 148;
    private static bool Gen2Gift(ushort s) => s is 21 or 25 or 35 or 63 or 104 or 122 or 127 or 133 or 137 or 147
        or 152 or 155 or 158 or 175 or 202 or 213 or 236;

    public static string Method(IEncounterable e) => e switch
    {
        EncounterSlot1 s => s.Type.ToString(), EncounterSlot2 s => s.Type.ToString(),
        EncounterSlot3 s => s.Type.ToString(), EncounterSlot4 s => s.Type.ToString(),
        EncounterSlot5 s => s.Type.ToString(), EncounterSlot6XY s => s.Type.ToString(),
        EncounterSlot6AO s => s.Type.ToString(), EncounterSlot7 s => s.Type.ToString(),
        _ => e.GetType().Name,
    };
}
