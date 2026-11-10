// #Cythisiax Added - Water plant: clean water is meant to come from one place in the wastes.
// Each repaired treatment machine slowly fills its own tank, but only while it is powered.
// Cut the power or smash the machine and the water stops. Repairing a broken machine is a
// construction graph on the prototype, so there is nothing for that in here.

using Content.Server.Chemistry.Containers.EntitySystems;
using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Robust.Shared.Timing;

namespace Content.Server._Misfits.WaterPlant;

public sealed class WaterPurifierSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly PowerReceiverSystem _power = default!;
    [Dependency] private readonly SolutionContainerSystem _solutions = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<WaterPurifierComponent, ExaminedEvent>(OnExamined);
    }

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<WaterPurifierComponent, ApcPowerReceiverComponent, SolutionContainerManagerComponent>();
        while (query.MoveNext(out var uid, out var purifier, out var receiver, out var manager))
        {
            if (now < purifier.NextUpdate)
                continue;

            purifier.NextUpdate = now + purifier.Interval;
            purifier.Running = _power.IsPowered(uid, receiver);
            if (!purifier.Running ||
                !_solutions.ResolveSolution((uid, manager), purifier.Solution, ref purifier.SolutionRef, out var solution))
            {
                continue;
            }

            var amount = FixedPoint2.Min(solution.AvailableVolume, purifier.Amount);
            if (amount > FixedPoint2.Zero)
                _solutions.TryAddReagent(purifier.SolutionRef.Value, purifier.Reagent, amount);
        }
    }

    private void OnExamined(Entity<WaterPurifierComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        args.PushMarkup(Loc.GetString(_power.IsPowered(ent.Owner)
            ? "n14-water-purifier-examine-running"
            : "n14-water-purifier-examine-unpowered"));
    }
}
