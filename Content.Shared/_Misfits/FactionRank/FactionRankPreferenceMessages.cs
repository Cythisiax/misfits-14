// #Cythisiax Added - Faction rank picker: lobby messages so a player can see which pin ranks their
// character has unlocked (playtime or leadership) and choose which one to wear. Nobody is moved up
// on their own; picking a higher rank is the player's call.

using Robust.Shared.Serialization;

namespace Content.Shared._Misfits.FactionRank;

[Serializable, NetSerializable]
public sealed class RequestFactionRankOptionsEvent(string characterName) : EntityEventArgs
{
    public readonly string CharacterName = characterName;
}

[Serializable, NetSerializable]
public sealed class SetFactionRankEvent(string characterName, string ladder, string rank) : EntityEventArgs
{
    public readonly string CharacterName = characterName;
    public readonly string Ladder = ladder;
    public readonly string Rank = rank;
}

[Serializable, NetSerializable]
public sealed class FactionRankOptionsEvent(string characterName, List<FactionRankLadderOptions> ladders) : EntityEventArgs
{
    public readonly string CharacterName = characterName;
    public readonly List<FactionRankLadderOptions> Ladders = ladders;
}

[Serializable, NetSerializable]
public sealed record FactionRankLadderOptions(
    string Ladder,
    List<string> Jobs,
    List<FactionRankOption> Ranks,
    string? Held);

[Serializable, NetSerializable]
public sealed record FactionRankOption(string Rank, bool Unlocked, int MinHours, bool Capped);
