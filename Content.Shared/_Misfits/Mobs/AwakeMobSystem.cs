// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Bed.Sleep;
using Content.Shared._Misfits.Genetics.Mutations;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Robust.Shared.Timing;


namespace Content.Shared._Misfits.Mobs;

[RegisterComponent, NetworkedComponent]
public sealed partial class AwakeMobComponent : Component;

public sealed class AwakeMobSystem : EntitySystem
{
    [Dependency] private IGameTiming _time = default!;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MobStateChangedEvent>(OnStateChanged);
        SubscribeLocalEvent<MutatableComponent, SleepStateChangedEvent>(OnSleepStateChanged);
    }

    private void OnStateChanged(MobStateChangedEvent args) => Refresh(args.Target, args.NewMobState);
    private void OnSleepStateChanged(Entity<MutatableComponent> ent, ref SleepStateChangedEvent args)
    {
        //prevent debug enumeration error when applied on prediction reset
        if (_time.ApplyingState)
            return;
        if (args.FellAsleep)
            RemComp<AwakeMobComponent>(ent);
        else if (TryComp<MobStateComponent>(ent, out var state) && state.CurrentState == MobState.Alive)
            EnsureComp<AwakeMobComponent>(ent);
    }

    private void Refresh(EntityUid uid, MobState state)
    {
        //prevent debug enumeration error when applied on prediction reset
        if (_time.ApplyingState)
            return;
        if (state == MobState.Alive && !HasComp<SleepingComponent>(uid))
            EnsureComp<AwakeMobComponent>(uid);
        else
            RemComp<AwakeMobComponent>(uid);
    }
}
