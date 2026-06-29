// #Misfits Add - Client-side tactical HUD toggle receiver.
// Listens for TacticalHUDToggledEvent from server and exposes IsEnabled
// for other client systems to check before rendering visual indicators.

using Content.Shared._Misfits.TacticalHUD;

namespace Content.Client._Misfits.TacticalHUD;

/// <summary>
/// Receives the tactical HUD toggle state from the server.
/// Other client systems inject this to check <see cref="IsEnabled"/>
/// before showing combat indicators, war overlay, or tacmap blips.
/// </summary>
public sealed class TacticalHUDClientSystem : EntitySystem
{
    /// <summary>
    /// Whether tactical HUD features are enabled. False by default (opt-in).
    /// </summary>
    public bool IsEnabled { get; private set; }

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<TacticalHUDToggledEvent>(OnToggled);
    }

    private void OnToggled(TacticalHUDToggledEvent ev)
    {
        IsEnabled = ev.Enabled;
    }
}
