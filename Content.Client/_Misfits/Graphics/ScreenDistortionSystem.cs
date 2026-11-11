// #Cythisiax Added - Screen distortion: keeps track of live shockwave rings, short-lived heat haze
// and the explosion flash, and hands them to ScreenDistortionOverlay each frame. Explosions call
// AddExplosion from ExplosionOverlaySystem; anything else can call AddShockwave / AddHeatHaze /
// AddFlash directly (grenades, artillery, whatever wants a thump). Standing fires come in through
// HeatHazeComponent and the FireVisuals appearance data inside the overlay, not through here.
// Everything is off when misfits.screen_distortion is off or reduced motion is on, and the flash
// has its own toggle (misfits.screen_flash) for people who don't want white-outs.

using Content.Shared._Misfits.CCVar;
using Content.Shared.CCVar;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Client._Misfits.Graphics;

public sealed class ScreenDistortionSystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlays = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public const int MaxShockwaves = 8;
    public const int MaxHaze = 16;

    private const float FlashWhiteSeconds = 0.35f;
    private const float FlashDesaturateSeconds = 0.9f;

    internal readonly List<Shockwave> Shockwaves = new();
    internal readonly List<TransientHaze> Hazes = new();

    private bool _distortionCvar = true;
    private bool _flashCvar = true;
    private bool _reducedMotion;

    private TimeSpan _flashStart;
    private float _flashWhitePeak;
    private float _flashDesaturatePeak;

    public bool DistortionEnabled => _distortionCvar && !_reducedMotion;
    public bool FlashEnabled => _flashCvar && !_reducedMotion;

    public override void Initialize()
    {
        base.Initialize();

        Subs.CVar(_cfg, GraphicsCVars.ScreenDistortion, v => _distortionCvar = v, true);
        Subs.CVar(_cfg, GraphicsCVars.ScreenFlash, v => _flashCvar = v, true);
        Subs.CVar(_cfg, CCVars.ReducedMotion, v => _reducedMotion = v, true);

        _overlays.AddOverlay(new ScreenDistortionOverlay());
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlays.RemoveOverlay<ScreenDistortionOverlay>();
    }

    public void AddShockwave(MapCoordinates center, float maxRadius, float duration, float strength, Color rim)
    {
        if (!DistortionEnabled || center.MapId == MapId.Nullspace || duration <= 0f)
            return;

        if (Shockwaves.Count >= MaxShockwaves * 2)
            Shockwaves.RemoveAt(0);

        Shockwaves.Add(new Shockwave(center, _timing.RealTime, maxRadius, duration, Math.Clamp(strength, 0f, 1f), rim));
    }

    public void AddHeatHaze(MapCoordinates center, float innerRadius, float outerRadius, float strength, float duration)
    {
        if (!DistortionEnabled || center.MapId == MapId.Nullspace || duration <= 0f)
            return;

        if (Hazes.Count >= MaxHaze)
            Hazes.RemoveAt(0);

        Hazes.Add(new TransientHaze(center, _timing.RealTime, innerRadius, outerRadius, Math.Clamp(strength, 0f, 1f), duration));
    }

    public void AddFlash(float white, float desaturate)
    {
        if (!FlashEnabled)
            return;

        var (curWhite, curDesat) = GetFlash();
        _flashStart = _timing.RealTime;
        _flashWhitePeak = Math.Clamp(Math.Max(curWhite, white), 0f, 1f);
        _flashDesaturatePeak = Math.Clamp(Math.Max(curDesat, desaturate), 0f, 1f);
    }

    public void AddExplosion(MapCoordinates epicenter, int iterations, float totalIntensity, Color color)
    {
        if (iterations <= 0)
            return;

        var radius = Math.Clamp(iterations * 1.5f + 2f, 3f, 24f);
        var strength = Math.Clamp(0.35f + 0.12f * MathF.Log2(1f + totalIntensity), 0.35f, 1f);

        AddShockwave(epicenter, radius, 0.45f + 0.04f * radius, strength, color);
        AddHeatHaze(epicenter, 0f, radius * 0.6f, strength, 3f + radius * 0.1f);

        if (_player.LocalEntity is not { } local)
            return;

        var playerPos = _transform.GetMapCoordinates(local);
        if (playerPos.MapId != epicenter.MapId)
            return;

        var near = 1f - (playerPos.Position - epicenter.Position).Length() / (radius * 1.5f);
        if (near <= 0f)
            return;

        AddFlash(0.55f * strength * near, 0.6f * strength * near);
    }

    internal (float White, float Desaturate) GetFlash()
    {
        if (_flashWhitePeak <= 0f && _flashDesaturatePeak <= 0f)
            return (0f, 0f);

        var t = (float) (_timing.RealTime - _flashStart).TotalSeconds;
        var white = Math.Max(0f, 1f - t / FlashWhiteSeconds);
        var desat = Math.Max(0f, 1f - t / FlashDesaturateSeconds);
        return (_flashWhitePeak * white * white, _flashDesaturatePeak * desat);
    }

    internal TimeSpan Now => _timing.RealTime;

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var now = _timing.RealTime;
        Shockwaves.RemoveAll(s => (now - s.Start).TotalSeconds >= s.Duration);
        Hazes.RemoveAll(h => (now - h.Start).TotalSeconds >= h.Duration);

        var (white, desat) = GetFlash();
        if (white <= 0f && desat <= 0f)
        {
            _flashWhitePeak = 0f;
            _flashDesaturatePeak = 0f;
        }

        if (!DistortionEnabled)
        {
            Shockwaves.Clear();
            Hazes.Clear();
        }
    }

    internal readonly record struct Shockwave(MapCoordinates Center, TimeSpan Start, float MaxRadius, float Duration, float Strength, Color Rim);

    internal readonly record struct TransientHaze(MapCoordinates Center, TimeSpan Start, float Inner, float Outer, float Strength, float Duration);
}
