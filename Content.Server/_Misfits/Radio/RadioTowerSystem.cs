using Content.Shared._Misfits.Radio;
using Content.Shared.Verbs;
using Robust.Shared.GameObjects;

namespace Content.Server._Misfits.Radio;

/// <summary>
/// Lets players bring map-placed radio towers online for the current round.
/// </summary>
public sealed class RadioTowerSystem : EntitySystem
{
    public override void Initialize()
    {
        SubscribeLocalEvent<RadioTowerComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
    }

    private void OnGetVerbs(Entity<RadioTowerComponent> tower, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (tower.Comp.Activated || !args.CanAccess || !args.CanInteract)
            return;

        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("n14-radio-tower-activate-verb"),
            Act = () => Activate(tower),
        });
    }

    private void Activate(Entity<RadioTowerComponent> tower)
    {
        if (tower.Comp.Activated || Deleted(tower))
            return;

        tower.Comp.Activated = true;
        Dirty(tower);
    }
}
