// Misfits Change - Shows a red exclamation mark above players while combat mode (Num1) is active.
// Misfits Tweak - Removed NPC aggro exclamation mark; icon now only shows for non-NPC entities (players).
// Misfits Tweak - Gated behind tactical HUD toggle (off by default, staff opt-in).
using Content.Client.NPC.HTN;
using Content.Client._Misfits.TacticalHUD;
// using Content.Shared._Misfits.Sound; // Misfits Tweak - AggroSoundComponent subscription removed; NPCs no longer show aggro icon on aggro
using Content.Shared.CombatMode;
using Content.Shared.StatusIcon;
using Content.Shared.StatusIcon.Components;
using Robust.Shared.Prototypes;

namespace Content.Client._Misfits.StatusIcon;

/// <summary>
/// Shows a red exclamation mark status icon above any non-NPC entity (i.e. players)
/// with <see cref="CombatModeComponent"/> while combat mode is toggled on via Num1.
/// Visible to all nearby players — no HUD equipment required.
/// NPC aggro exclamation mark intentionally removed per Misfits design.
/// Gated behind the tactical HUD toggle (off by default, staff opt-in via /tacticalhud).
/// </summary>
public sealed class ShowAggroIconSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly TacticalHUDClientSystem _tacticalHUD = default!;

    public override void Initialize()
    {
        base.Initialize();
        // Misfits Tweak - AggroSoundComponent handler removed; NPCs should not show exclamation mark on aggro
        // SubscribeLocalEvent<AggroSoundComponent, GetStatusIconsEvent>(OnGetAggroStatusIcons);
        SubscribeLocalEvent<CombatModeComponent, GetStatusIconsEvent>(OnGetCombatModeStatusIcons);
    }

    // Misfits Tweak - Kept for reference but no longer subscribed; removing NPC aggro icon
    // private void OnGetAggroStatusIcons(EntityUid uid, AggroSoundComponent comp, ref GetStatusIconsEvent ev)
    // {
    //     if (comp.CooldownRemaining <= 0f)
    //         return;
    //
    //     if (_prototype.TryIndex<FactionIconPrototype>("N14AggroIcon", out var icon))
    //         ev.StatusIcons.Add(icon);
    // }

    private void OnGetCombatModeStatusIcons(EntityUid uid, CombatModeComponent comp, ref GetStatusIconsEvent ev)
    {
        if (!comp.IsInCombatMode)
            return;

        // Misfits Tweak - Only show the combat mode exclamation mark for players, not NPCs
        if (HasComp<HTNComponent>(uid))
            return;

        // Misfits Tweak - Gated behind staff toggle (off by default)
        if (!_tacticalHUD.IsEnabled)
            return;

        if (_prototype.TryIndex<FactionIconPrototype>("N14AggroIcon", out var icon))
            ev.StatusIcons.Add(icon);
    }
}
