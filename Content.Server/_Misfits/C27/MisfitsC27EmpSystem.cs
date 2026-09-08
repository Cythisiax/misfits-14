using Content.Server.Emp;
using Content.Shared._Misfits.C27;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Popups;
using Robust.Shared.Prototypes;

// #Misfits Add - Server EMP handler for the C-27 humanoid robot species. Subscribes to
// EmpPulseEvent on entities carrying MisfitsC27Component and applies Shock damage scaled by
// the pulse's energy budget plus the configured stun. Server-only because EMP damage and
// status effects are authoritative on the server.
namespace Content.Server._Misfits.C27;

// #Misfits Add - C-27 humanoid robot EMP handler. Spec: EMP pulses drain power cells AND inflict
// posibrain damage; optional PA-style stun. We model the posibrain damage as Shock damage to the
// chassis (the brain is an organ inside the body — damaging the mob propagates through the
// damageable). Battery drain is left to the existing Battery / power-cell EmpPulseEvent
// subscribers — if the C-27 ever gets a power cell slot, it will already be handled.
public sealed class MisfitsC27EmpSystem : EntitySystem
{
    // #Misfits Tweak - EMP energy is measured in joules for battery draining. Our pulse grenade
    // uses millions of joules, so its damage multiplier is capped. Without this cap a single
    // grenade deals five-digit damage, tears off every limb, and effectively removes the C-27
    // from the round instead of serving as a severe anti-robot weapon.
    private const float MaxEmpEnergyMultiplier = 15f;

    private static readonly ProtoId<DamageTypePrototype> ShockDamage = "Shock";

    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MisfitsC27Component, EmpPulseEvent>(OnEmpPulse);
    }

    private void OnEmpPulse(Entity<MisfitsC27Component> ent, ref EmpPulseEvent args)
    {
        // Scale damage by pulse energy: a stronger EMP fries the posibrain harder. The cap keeps
        // the pulse grenade at 100 Shock with the defaults while a chemical EMP deals 87.5.
        var energyMultiplier = MathF.Min(args.EnergyConsumption / 1000f, MaxEmpEnergyMultiplier);
        var totalShock = ent.Comp.EmpShockDamage + ent.Comp.EmpDamagePerKiloJoule * energyMultiplier;

        // Build one damage packet so the body system can distribute the Shock normally.
        if (_proto.TryIndex(ShockDamage, out var shockProto))
        {
            var damage = new DamageSpecifier(shockProto, totalShock);
            _damageable.TryChangeDamage(ent, damage, ignoreResistances: true, origin: null);
        }

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
