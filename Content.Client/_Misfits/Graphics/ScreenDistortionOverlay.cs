// #Cythisiax Added - Screen distortion overlay: one full-screen draw of screen_distortion.swsl
// below FOV, so a ring or shimmer behind a wall gets blacked out by FOV like everything else and
// never gives away where something blew up. Each frame it gathers the live shockwaves and haze
// from ScreenDistortionSystem, plus haze for anything standing on fire (HeatHazeComponent, or
// FireVisuals.OnFire for burning mobs and lit N14 bonfires), converts them to viewport pixels and
// pushes them into the shader. When nothing is on screen BeforeDraw bails and we skip the screen
// copy entirely, so this costs nothing outside of fights and campfires.

using System.Numerics;
using Content.Client.Atmos.Components;
using Content.Shared._Misfits.Graphics;
using Content.Shared.Atmos;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;

namespace Content.Client._Misfits.Graphics;

public sealed class ScreenDistortionOverlay : Overlay
{
    [Dependency] private readonly IEntityManager _entMan = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowFOV;
    public override bool RequestScreenTexture => true;

    private const int MaxShock = ScreenDistortionSystem.MaxShockwaves;
    private const int MaxHaze = ScreenDistortionSystem.MaxHaze;

    private const float ShockFalloffK = 2.2f;
    private const float ShockAberration = 0.35f;
    private const float ShockPushPx = 22f;
    private const float ShockRim = 0.35f;
    private const float HazeStrengthPx = 7f;
    private const float HazeRiseSpeed = 1.6f;
    private const float HazeNoiseScale = 2.2f;

    private readonly ShaderInstance _shader;
    private readonly ScreenDistortionSystem _system;
    private readonly SharedTransformSystem _transform;
    private readonly SharedAppearanceSystem _appearance;

    private readonly Vector2[] _shockPos = new Vector2[MaxShock];
    private readonly float[] _shockRadius = new float[MaxShock];
    private readonly float[] _shockBand = new float[MaxShock];
    private readonly float[] _shockStrength = new float[MaxShock];
    private readonly float[] _shockPush = new float[MaxShock];
    private readonly float[] _shockRim = new float[MaxShock];
    private readonly Color[] _shockRimColor = new Color[MaxShock];
    private int _shockCount;

    private readonly Vector2[] _hazePos = new Vector2[MaxHaze];
    private readonly float[] _hazeInner = new float[MaxHaze];
    private readonly float[] _hazeOuter = new float[MaxHaze];
    private readonly float[] _hazeStrength = new float[MaxHaze];
    private int _hazeCount;

    private readonly List<HazeCandidate> _hazeCandidates = new();

    private float _pxPerTile;
    private float _flashWhite;
    private float _flashDesaturate;

    public ScreenDistortionOverlay()
    {
        IoCManager.InjectDependencies(this);
        _shader = _proto.Index<ShaderPrototype>("MisfitsScreenDistortion").InstanceUnique();
        _system = _entMan.System<ScreenDistortionSystem>();
        _transform = _entMan.System<SharedTransformSystem>();
        _appearance = _entMan.System<SharedAppearanceSystem>();
        ZIndex = 100;
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        _shockCount = 0;
        _hazeCount = 0;
        _flashWhite = 0f;
        _flashDesaturate = 0f;

        if (args.Viewport.Eye == null)
            return false;

        var viewport = args.Viewport;
        var eyePos = viewport.Eye.Position.Position;
        _pxPerTile = (viewport.WorldToLocal(eyePos + Vector2.UnitX) - viewport.WorldToLocal(eyePos)).Length();
        if (_pxPerTile <= 0.01f)
            return false;

        if (_system.FlashEnabled)
            (_flashWhite, _flashDesaturate) = _system.GetFlash();

        if (_system.DistortionEnabled)
        {
            GatherShockwaves(args);
            GatherHaze(args);
        }

        return _shockCount > 0 || _hazeCount > 0 || _flashWhite > 0.001f || _flashDesaturate > 0.001f;
    }

    private void GatherShockwaves(in OverlayDrawArgs args)
    {
        var now = _system.Now;
        foreach (var shock in _system.Shockwaves)
        {
            if (_shockCount >= MaxShock)
                break;
            if (shock.Center.MapId != args.MapId)
                continue;

            var t = Math.Clamp((float) (now - shock.Start).TotalSeconds / shock.Duration, 0f, 1f);
            var strength = shock.Strength * MathF.Pow(1f - t, 1.5f);
            if (strength <= 0.001f)
                continue;

            var radius = shock.MaxRadius * (1f - MathF.Pow(1f - t, 2.2f));
            var band = (0.8f + 0.05f * shock.MaxRadius) * (0.6f + 0.4f * t);

            if (!InView(args, shock.Center.Position, radius + band))
                continue;

            var i = _shockCount++;
            _shockPos[i] = ToFrag(args, shock.Center.Position);
            _shockRadius[i] = radius * _pxPerTile;
            _shockBand[i] = band * _pxPerTile;
            _shockStrength[i] = strength;
            _shockPush[i] = ShockPushPx * _pxPerTile / EyeManager.PixelsPerMeter;
            _shockRim[i] = ShockRim;
            _shockRimColor[i] = shock.Rim;
        }
    }

    private void GatherHaze(in OverlayDrawArgs args)
    {
        _hazeCandidates.Clear();
        var center = args.WorldAABB.Center;
        var now = _system.Now;

        foreach (var haze in _system.Hazes)
        {
            if (haze.Center.MapId != args.MapId)
                continue;

            var t = Math.Clamp((float) (now - haze.Start).TotalSeconds / haze.Duration, 0f, 1f);
            AddCandidate(args, center, haze.Center.Position, haze.Inner, haze.Outer, haze.Strength * (1f - t * t));
        }

        var hazeQuery = _entMan.EntityQueryEnumerator<HeatHazeComponent, TransformComponent>();
        while (hazeQuery.MoveNext(out var uid, out var comp, out var xform))
        {
            if (xform.MapID != args.MapId)
                continue;
            if (_entMan.HasComponent<FireVisualsComponent>(uid)
                && (!_appearance.TryGetData<bool>(uid, FireVisuals.OnFire, out var lit) || !lit))
            {
                continue;
            }

            AddCandidate(args, center, _transform.GetWorldPosition(xform), comp.InnerRadius, comp.OuterRadius, comp.Strength);
        }

        var fireQuery = _entMan.EntityQueryEnumerator<FireVisualsComponent, AppearanceComponent, TransformComponent>();
        while (fireQuery.MoveNext(out var uid, out _, out var appearance, out var xform))
        {
            if (xform.MapID != args.MapId || _entMan.HasComponent<HeatHazeComponent>(uid))
                continue;
            if (!_appearance.TryGetData<bool>(uid, FireVisuals.OnFire, out var onFire, appearance) || !onFire)
                continue;

            _appearance.TryGetData<float>(uid, FireVisuals.FireStacks, out var stacks, appearance);
            var strength = Math.Clamp(0.4f + stacks * 0.08f, 0.4f, 1f);
            var outer = Math.Clamp(1.2f + stacks * 0.1f, 1.2f, 2.4f);
            AddCandidate(args, center, _transform.GetWorldPosition(xform), 0.3f, outer, strength);
        }

        if (_hazeCandidates.Count > MaxHaze)
            _hazeCandidates.Sort((a, b) => a.DistanceSquared.CompareTo(b.DistanceSquared));

        foreach (var candidate in _hazeCandidates)
        {
            if (_hazeCount >= MaxHaze)
                break;

            var i = _hazeCount++;
            _hazePos[i] = ToFrag(args, candidate.Position);
            _hazeInner[i] = candidate.Inner * _pxPerTile;
            _hazeOuter[i] = candidate.Outer * _pxPerTile;
            _hazeStrength[i] = candidate.Strength;
        }
    }

    private void AddCandidate(in OverlayDrawArgs args, Vector2 viewCenter, Vector2 position, float inner, float outer, float strength)
    {
        if (strength <= 0.001f || outer <= inner || !InView(args, position, outer))
            return;

        _hazeCandidates.Add(new HazeCandidate(position, inner, outer, strength, (position - viewCenter).LengthSquared()));
    }

    private static bool InView(in OverlayDrawArgs args, Vector2 position, float reach)
    {
        return (position - args.WorldAABB.ClosestPoint(position)).LengthSquared() <= reach * reach;
    }

    // Viewport local pixels with Y flipped, which is what FRAGCOORD sees (same trick as SingularityOverlay).
    private static Vector2 ToFrag(in OverlayDrawArgs args, Vector2 world)
    {
        var local = args.Viewport.WorldToLocal(world);
        local.Y = args.Viewport.Size.Y - local.Y;
        return local;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (ScreenTexture == null)
            return;

        _shader.SetParameter("SCREEN_TEXTURE", ScreenTexture);
        _shader.SetParameter("pxPerTile", _pxPerTile);

        _shader.SetParameter("shockCount", _shockCount);
        _shader.SetParameter("shockPos", _shockPos);
        _shader.SetParameter("shockRadius", _shockRadius);
        _shader.SetParameter("shockBand", _shockBand);
        _shader.SetParameter("shockStrength", _shockStrength);
        _shader.SetParameter("shockPush", _shockPush);
        _shader.SetParameter("shockRim", _shockRim);
        _shader.SetParameter("shockRimColor", _shockRimColor);
        _shader.SetParameter("shockFalloffK", ShockFalloffK);
        _shader.SetParameter("shockAberration", ShockAberration);

        _shader.SetParameter("hazeCount", _hazeCount);
        _shader.SetParameter("hazePos", _hazePos);
        _shader.SetParameter("hazeInner", _hazeInner);
        _shader.SetParameter("hazeOuter", _hazeOuter);
        _shader.SetParameter("hazeStrength", _hazeStrength);
        _shader.SetParameter("hazeStrengthPx", HazeStrengthPx * _pxPerTile / EyeManager.PixelsPerMeter);
        _shader.SetParameter("hazeRiseSpeed", HazeRiseSpeed);
        _shader.SetParameter("hazeNoiseScale", HazeNoiseScale);

        _shader.SetParameter("flashWhite", _flashWhite);
        _shader.SetParameter("flashDesaturate", _flashDesaturate);

        var handle = args.WorldHandle;
        handle.UseShader(_shader);
        handle.DrawRect(args.WorldAABB, Color.White);
        handle.UseShader(null);
    }

    private readonly record struct HazeCandidate(Vector2 Position, float Inner, float Outer, float Strength, float DistanceSquared);
}
