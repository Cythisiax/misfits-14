// #Cythisiax Added - Water plant: the tuning for one repaired water treatment machine. It only
// makes clean water while it has power, into its own tank that people drain with a canteen
// or bucket. Change the rate and the reagent on the prototype, not in here.

using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Server._Misfits.WaterPlant;

[RegisterComponent]
public sealed partial class WaterPurifierComponent : Component
{
    [DataField] public string Solution = "well";
    [DataField] public ProtoId<ReagentPrototype> Reagent = "Water";
    [DataField] public FixedPoint2 Amount = 1;
    [DataField] public TimeSpan Interval = TimeSpan.FromSeconds(1);

    public TimeSpan NextUpdate;
    public bool Running;
    public Entity<SolutionComponent>? SolutionRef;
}
