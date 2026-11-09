// #Cythisiax Added - Faction rank component: sits on an ID tag and holds the rank stamped on it at
// spawn. Only used by factions without pins (Rangers, Legion, BoS), where the ID tag is what gives
// the prefix. Pin factions don't need it, the worn pin maps straight to its rank.

using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Misfits.FactionRank;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FactionRankComponent : Component
{
    [DataField, AutoNetworkedField]
    public ProtoId<FactionRankPrototype>? Rank;
}
