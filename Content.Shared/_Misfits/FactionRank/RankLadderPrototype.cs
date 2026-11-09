// #Cythisiax Added - Rank ladder: replaces ncrRankPinProgression. Maps a group of jobs to the ranks
// they earn by playtime; thresholds point at factionRank prototypes instead of pin entities.
// Tracker is a playtime tracker ID, or a department ID when departmentTracker is true (sums every
// job in it). Job-based factions (Rangers, Legion, BoS) use one threshold at 0, so the job slot
// is the rank and it gets stamped on their ID tag.

using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Shared._Misfits.FactionRank;

[Prototype("rankLadder")]
public sealed partial class RankLadderPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public List<ProtoId<JobPrototype>> Jobs = new();

    [DataField]
    public string Tracker = string.Empty;

    [DataField]
    public bool DepartmentTracker;

    [DataField(required: true)]
    public List<RankLadderThreshold> Thresholds = new();
}

[DataDefinition]
public sealed partial class RankLadderThreshold
{
    [DataField]
    public int Min;

    [DataField(required: true)]
    public ProtoId<FactionRankPrototype> Rank;
}
