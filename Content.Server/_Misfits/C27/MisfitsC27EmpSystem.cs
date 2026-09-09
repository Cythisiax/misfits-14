using Content.Server.Emp;
using Content.Shared._Misfits.C27;
using Content.Shared.Popups;

// #Misfits Add - Server EMP handler for the C-27 humanoid robot species.
namespace Content.Server._Misfits.C27;

// EMP battery drain remains handled by the existing battery and power-cell subscribers.
// C-27s may still be disabled, but EMP pulses deliberately deal no direct Shock damage.
public sealed class MisfitsC27EmpSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MisfitsC27Component, EmpPulseEvent>(OnEmpPulse);
    }

    private void OnEmpPulse(Entity<MisfitsC27Component> ent, ref EmpPulseEvent args)
    {
        // Mark Affected so the EMP visual effect spawns over the chassis.
        args.Affected = true;

        // Optional PA-style stun: sets the EmpDisabled component so the mob is locked out of
        // interactions for the pulse duration. EmpSystem.DoEmpEffects handles the actual
        // EnsureComp<EmpDisabledComponent> when args.Disabled is true.
        if (ent.Comp.ApplyEmpStun)
            args.Disabled = true;

        _popup.PopupEntity(Loc.GetString("c27-emp-hit"), ent, ent);
    }
}
