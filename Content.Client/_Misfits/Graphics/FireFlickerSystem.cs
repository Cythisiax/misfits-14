// #Cythisiax Added - Fire flicker: drives every PointLight that has FireFlickerComponent, and hands the
// component to the client-side light FireVisuals spawns for anything on fire (that light had a TODO for
// exactly this). The light's own energy/radius are treated as the base; if something else changes them
// (server state, FireVisuals growing with fire stacks) we notice the value isn't the one we last wrote
// and take it as the new base. Time is server CurTime done in doubles, because floats lose the sine
// phase after a few hours of uptime. misfits.fire_flicker off puts every light back at its base.

using Content.Client.Atmos.Components;
using Content.Shared._Misfits.CCVar;
using Content.Shared._Misfits.Graphics;
using Robust.Client.GameObjects;
using Robust.Shared.Configuration;
using Robust.Shared.Timing;

namespace Content.Client._Misfits.Graphics;

public sealed class FireFlickerSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly PointLightSystem _lights = default!;

    private const double DipWindowSeconds = 0.5;
    private const float MinPowerScale = 0.05f;
    private const float MinRangeScale = 0.5f;
    private const double WanderRatio = 1.618;

    private bool _enabled = true;

    public override void Initialize()
    {
        base.Initialize();
        Subs.CVar(_cfg, GraphicsCVars.FireFlicker, OnToggle, true);
    }

    private void OnToggle(bool enabled)
    {
        _enabled = enabled;
        if (enabled)
            return;

        var query = EntityQueryEnumerator<FireFlickerComponent, PointLightComponent>();
        while (query.MoveNext(out var uid, out var flicker, out var light))
        {
            if (float.IsNaN(flicker.LastEnergy))
                continue;

            _lights.SetEnergy(uid, flicker.BaseEnergy, light);
            _lights.SetRadius(uid, flicker.BaseRadius, light);
            flicker.LastEnergy = float.NaN;
            flicker.LastRadius = float.NaN;
        }
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        if (!_enabled)
            return;

        AdoptFireVisualLights();

        var time = _timing.CurTime.TotalSeconds;
        var query = EntityQueryEnumerator<FireFlickerComponent, PointLightComponent>();
        while (query.MoveNext(out var uid, out var flicker, out var light))
        {
            if (!light.Enabled)
                continue;

            if (!flicker.Seeded)
            {
                flicker.Seed = (uint) GetNetEntity(uid).Id;
                flicker.Seeded = true;
            }

            if (float.IsNaN(flicker.LastEnergy) || !MathHelper.CloseToPercent(light.Energy, flicker.LastEnergy))
                flicker.BaseEnergy = light.Energy;
            if (float.IsNaN(flicker.LastRadius) || !MathHelper.CloseToPercent(light.Radius, flicker.LastRadius))
                flicker.BaseRadius = light.Radius;

            Sample(flicker, time, out var power, out var range);

            flicker.LastEnergy = flicker.BaseEnergy * power;
            flicker.LastRadius = flicker.BaseRadius * range;
            _lights.SetEnergy(uid, flicker.LastEnergy, light);
            _lights.SetRadius(uid, flicker.LastRadius, light);

            // SetX skips near-identical values, so read back what the light actually holds
            flicker.LastEnergy = light.Energy;
            flicker.LastRadius = light.Radius;
        }
    }

    private void AdoptFireVisualLights()
    {
        var query = EntityQueryEnumerator<FireVisualsComponent>();
        while (query.MoveNext(out var owner, out var visuals))
        {
            if (visuals.LightEntity is not { } lightEnt || HasComp<FireFlickerComponent>(lightEnt) || TerminatingOrDeleted(lightEnt))
                continue;

            var flicker = AddComp<FireFlickerComponent>(lightEnt);
            if (TryComp<FireFlickerComponent>(owner, out var spec))
            {
                flicker.Amount = spec.Amount;
                flicker.Speed = spec.Speed;
                flicker.DipChance = spec.DipChance;
                flicker.DipDepth = spec.DipDepth;
                flicker.StepAmount = spec.StepAmount;
                flicker.StepHoldMinSeconds = spec.StepHoldMinSeconds;
                flicker.StepHoldMaxSeconds = spec.StepHoldMaxSeconds;
                flicker.Steps = spec.Steps;
            }

            // the spawned light is client-side, so seed from the burning entity to match other clients
            flicker.Seed = (uint) GetNetEntity(owner).Id;
            flicker.Seeded = true;
        }
    }

    public static void Sample(FireFlickerComponent f, double t, out float powerScale, out float rangeScale)
    {
        var phase1 = Hash(f.Seed) * 1.4629181E-09;
        var phase2 = Hash(f.Seed ^ 0x9E3779B9u) * 1.4629181E-09;
        var theta = t * f.Speed * Math.Tau;
        var wander = (float) (0.5 + 0.25 * Math.Sin(theta + phase1) + 0.25 * Math.Sin(theta * WanderRatio + phase2));
        var dip = DipEnvelope(f, t);
        var step = StepEnvelope(f, t);

        powerScale = MathF.Max((1f - f.Amount * wander - f.DipDepth * dip) * step, MinPowerScale);
        rangeScale = MathF.Max(1f - 0.5f * f.DipDepth * dip, MinRangeScale);
    }

    private static float DipEnvelope(FireFlickerComponent f, double t)
    {
        if (f.DipChance <= 0f)
            return 0f;

        var window = (long) Math.Floor(t / DipWindowSeconds);
        var roll = (Hash(f.Seed ^ unchecked((uint) (window * -1640531535L))) & 0xFFFF) * (1f / 65536f);
        if (roll >= f.DipChance)
            return 0f;

        return (float) Math.Sin((t / DipWindowSeconds - window) * Math.PI);
    }

    private static float StepEnvelope(FireFlickerComponent f, double t)
    {
        if (f.StepAmount <= 0f || f.Steps <= 0 || f.StepHoldMaxSeconds <= 0f)
            return 1f;

        var holdMax = (double) f.StepHoldMaxSeconds;
        var jitterSpan = 1.0 - f.StepHoldMinSeconds / holdMax;
        var window = (long) Math.Floor(t / holdMax);
        var boundary = (window + jitterSpan * Norm(Hash(f.Seed ^ unchecked((uint) (window * -2048144789L))))) * holdMax;
        var held = t < boundary ? window - 1 : window;
        var level = Hash(f.Seed ^ 0xA511E9B3u ^ unchecked((uint) (held * -1640531535L))) % (uint) (f.Steps + 1);
        return 1f - f.StepAmount * level / f.Steps;
    }

    private static double Norm(uint hash)
    {
        return (hash & 0xFFFFFF) * 5.9604645E-08;
    }

    private static uint Hash(uint x)
    {
        unchecked
        {
            x ^= x >> 16;
            x *= 2146121005u;
            x ^= x >> 15;
            x *= 2221713035u;
            x ^= x >> 16;
            return x;
        }
    }
}
