using System;
using Content.Server.Warps;
using Content.Shared._Misfits.Warps;
using Content.Shared.StepTrigger.Systems;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._Misfits.Warps;

/// <summary>
/// Bridges the native StepTrigger and Warper systems for mapper-placed walk-over warp points.
/// WarperSystem remains responsible for all movement, including cross-map safety, pulls and
/// followers; this system only decides when a player has entered an endpoint.
/// </summary>
public sealed class AutoWarperSystem : EntitySystem
{
    private static readonly TimeSpan ArrivalCooldown = TimeSpan.FromSeconds(0.75);
    private static readonly TimeSpan TravelDuration = TimeSpan.FromSeconds(2.5);
    private static readonly TimeSpan FadeOutDuration = TimeSpan.FromSeconds(0.25);

    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly WarperSystem _warper = default!;
    [Dependency] private readonly WarpPointSystem _warpPoints = default!;
    private readonly Dictionary<EntityUid, (EntityUid Destination, TimeSpan Due)> _pendingWarps = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<AutoWarperComponent, StepTriggerAttemptEvent>(OnStepTriggerAttempt);
        SubscribeLocalEvent<AutoWarperComponent, StepTriggeredOffEvent>(OnSteppedOnto);
    }

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        foreach (var (traveller, pending) in _pendingWarps.ToArray())
        {
            if (now < pending.Due)
                continue;

            _pendingWarps.Remove(traveller);
            if (!Deleted(traveller))
                _warper.WarpEntityTo(traveller, pending.Destination);
        }
    }

    private void OnStepTriggerAttempt(Entity<AutoWarperComponent> ent, ref StepTriggerAttemptEvent args)
    {
        if (!HasComp<ActorComponent>(args.Tripper) ||
            (TryComp<AutoWarperCooldownComponent>(args.Tripper, out var cooldown) &&
             cooldown.ExpiresAt > _timing.CurTime))
        {
            args.Cancelled = true;
            return;
        }

        args.Continue = true;
    }

    private void OnSteppedOnto(Entity<AutoWarperComponent> ent, ref StepTriggeredOffEvent args)
    {
        // These endpoints are for player traversal, not loose items or NPCs.
        if (!TryComp<ActorComponent>(args.Tripper, out var actor) ||
            (TryComp<AutoWarperCooldownComponent>(args.Tripper, out var cooldown) &&
             cooldown.ExpiresAt > _timing.CurTime) ||
            string.IsNullOrWhiteSpace(ent.Comp.DestinationId))
        {
            return;
        }

        var destination = _warpPoints.FindWarpPoint(ent.Comp.DestinationId);
        if (destination == null)
            return;

        var arrivalGuard = EnsureComp<AutoWarperCooldownComponent>(args.Tripper);
        arrivalGuard.ExpiresAt = _timing.CurTime + TravelDuration + ArrivalCooldown;
        _pendingWarps[args.Tripper] = (destination.Value, _timing.CurTime + TravelDuration);
        // The final quarter second occurs after the server moves the traveller, so the player
        // fades back in only once they have reached the other endpoint.
        RaiseNetworkEvent(new AutoWarperTravelEvent((float) (TravelDuration + FadeOutDuration).TotalSeconds), actor.PlayerSession.Channel);
    }
}

/// <summary>Enables walk-over triggering toward a native WarpPoint.</summary>
[RegisterComponent]
public sealed partial class AutoWarperComponent : Component
{
    [DataField(required: true)]
    public string DestinationId = string.Empty;
}

/// <summary>Prevents a traveller from immediately warping back on arrival.</summary>
[RegisterComponent]
public sealed partial class AutoWarperCooldownComponent : Component
{
    public TimeSpan ExpiresAt;
}
