// #Misfits Add - Tactical HUD toggle. Off by default. Staff opt-in to enable
// combat mode indicators, tacmap blips, and war overlay tags.
using Robust.Shared.Serialization;

namespace Content.Shared._Misfits.TacticalHUD;

/// <summary>
/// Server -> all clients. Broadcasts the current tactical HUD toggle state.
/// Sent on toggle and when a new player joins.
/// </summary>
[Serializable, NetSerializable]
public sealed class TacticalHUDToggledEvent : EntityEventArgs
{
    public bool Enabled;
}
