using Robust.Shared.GameStates;

namespace Content.Shared.Ghost.Components;

// #Cythisiax Added - NuBody brains transfer minds through this upstream ghost-move contract.
[RegisterComponent, NetworkedComponent]
public sealed partial class GhostOnMoveComponent : Component
{
    [DataField]
    public bool CanReturn = true;

    [DataField]
    public bool MustBeDead;
}
