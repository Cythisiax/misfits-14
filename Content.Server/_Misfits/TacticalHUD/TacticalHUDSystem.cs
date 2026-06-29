// #Misfits Add - Server-side tactical HUD toggle. Off by default.
// Staff must opt-in via /tacticalhud command to enable combat mode indicators,
// tacmap blips, and war overlay tags.

using Content.Shared._Misfits.TacticalHUD;
using Robust.Server.Player;
using Robust.Shared.Console;
using Robust.Shared.Player;

namespace Content.Server._Misfits.TacticalHUD;

/// <summary>
/// Stores the tactical HUD enabled flag and handles the admin toggle command.
/// When enabled, combat mode indicators, tacmap blips, and war overlay tags are visible.
/// When disabled (default), all three are suppressed.
/// </summary>
public sealed class TacticalHUDSystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IConsoleHost _conHost = default!;

    /// <summary>
    /// Whether tactical HUD features are enabled. False by default (opt-in).
    /// </summary>
    public bool IsEnabled { get; private set; }

    public override void Initialize()
    {
        base.Initialize();
        _conHost.RegisterCommand("tacticalhud",
            "Toggle tactical HUD features (combat indicators, tacmap blips, war overlay). Admin only.",
            "tacticalhud",
            CmdToggleTacticalHUD);

        // Send initial state to newly connected players.
        SubscribeLocalEvent<PlayerStatusChangedEvent>(OnPlayerStatusChanged);
    }

    private void OnPlayerStatusChanged(PlayerStatusChangedEvent ev)
    {
        // When a player transitions to InGame, sync the current toggle state.
        if (ev.NewStatus == SessionStatus.InGame)
        {
            RaiseNetworkEvent(new TacticalHUDToggledEvent { Enabled = IsEnabled },
                ev.Session);
        }
    }

    [AdminCommand(AdminFlags.Admin)]
    private void CmdToggleTacticalHUD(IConsoleShell shell, string argStr, string[] args)
    {
        IsEnabled = !IsEnabled;
        var msg = IsEnabled
            ? "Tactical HUD features ENABLED (combat indicators, tacmap, war overlay)."
            : "Tactical HUD features DISABLED (combat indicators, tacmap, war overlay).";

        RaiseNetworkEvent(new TacticalHUDToggledEvent { Enabled = IsEnabled });

        shell.WriteLine(msg);
    }
}
