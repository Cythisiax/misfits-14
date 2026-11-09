// #Cythisiax Added - Faction rank picker (client): asks the server which pin ranks a character has
// unlocked and sends the player's pick back. The lobby job list reads from here to draw the rank
// dropdown next to NCR / Enclave jobs.

using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Shared._Misfits.FactionRank;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._Misfits.FactionRank;

public sealed class FactionRankPreferenceSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    // Playtime keeps ticking, so cached options go stale; ask again after this long.
    private static readonly TimeSpan CacheTime = TimeSpan.FromMinutes(1);

    private readonly Dictionary<string, (FactionRankOptionsEvent Options, TimeSpan Received)> _options = new();
    private readonly HashSet<string> _requested = new();

    public event Action<string>? OptionsUpdated;

    public override void Initialize()
    {
        SubscribeNetworkEvent<FactionRankOptionsEvent>(OnOptions);
    }

    private void OnOptions(FactionRankOptionsEvent ev)
    {
        _options[ev.CharacterName] = (ev, _timing.RealTime);
        _requested.Remove(ev.CharacterName);
        OptionsUpdated?.Invoke(ev.CharacterName);
    }

    public bool TryGetOptions(string characterName, [NotNullWhen(true)] out FactionRankOptionsEvent? options)
    {
        options = null;
        var fresh = false;
        if (_options.TryGetValue(characterName, out var cached))
        {
            options = cached.Options;
            fresh = _timing.RealTime - cached.Received < CacheTime;
        }

        if (!fresh && _requested.Add(characterName))
            RaiseNetworkEvent(new RequestFactionRankOptionsEvent(characterName));

        return options != null;
    }

    public void SetRank(string characterName, string ladder, string rank)
    {
        RaiseNetworkEvent(new SetFactionRankEvent(characterName, ladder, rank));
    }

    public RankLadderPrototype? GetPinLadder(string jobId)
    {
        foreach (var ladder in _proto.EnumeratePrototypes<RankLadderPrototype>())
        {
            if (!ladder.Jobs.Any(j => j.Id == jobId))
                continue;

            return ladder.Thresholds.Any(t => _proto.TryIndex(t.Rank, out var rank) && rank.Insignia != null)
                ? ladder
                : null;
        }

        return null;
    }
}
