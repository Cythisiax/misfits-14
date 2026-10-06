using System;
using System.Numerics;
using Content.Client.Resources;
using Content.Shared._Misfits.Warps;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.Maths;
using Robust.Shared.Timing;

namespace Content.Client._Misfits.Warps;

public sealed class AutoWarperTravelClientSystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlays = default!;
    [Dependency] private readonly IResourceCache _resources = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    private AutoWarperTravelOverlay? _overlay;

    public override void Initialize()
    {
        SubscribeNetworkEvent<AutoWarperTravelEvent>(OnTravel);
    }

    public override void Shutdown()
    {
        if (_overlay != null)
            _overlays.RemoveOverlay(_overlay);
    }

    private void OnTravel(AutoWarperTravelEvent args)
    {
        if (_overlay == null)
        {
            _overlay = new AutoWarperTravelOverlay(_resources, _timing);
            _overlays.AddOverlay(_overlay);
        }

        _overlay.StartedAt = _timing.CurTime;
        _overlay.EndsAt = _timing.CurTime + TimeSpan.FromSeconds(args.DurationSeconds);
    }
}

internal sealed class AutoWarperTravelOverlay : Overlay
{
    private readonly Font _font;
    private readonly IGameTiming _timing;
    public override OverlaySpace Space => OverlaySpace.ScreenSpace;
    public TimeSpan StartedAt;
    public TimeSpan EndsAt;

    public AutoWarperTravelOverlay(IResourceCache resources, IGameTiming timing)
    {
        _font = resources.GetFont("/Fonts/NotoSans/NotoSans-Regular.ttf", 22);
        _timing = timing;
        ZIndex = 1000;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var now = _timing.CurTime;
        if (now >= EndsAt)
            return;

        var elapsed = (float) (now - StartedAt).TotalSeconds;
        var remaining = (float) (EndsAt - now).TotalSeconds;
        var alpha = MathF.Min(1f, MathF.Min(elapsed / 0.25f, remaining / 0.25f));
        var bounds = args.ViewportBounds;
        args.ScreenHandle.DrawRect(bounds, Color.Black.WithAlpha(alpha));

        var text = ". . . Traveling.";
        var size = args.ScreenHandle.GetDimensions(_font, text, 1f);
        var position = new Vector2((bounds.Width - size.X) / 2f, (bounds.Height - size.Y) / 2f);
        args.ScreenHandle.DrawString(_font, position, text, Color.White.WithAlpha(alpha));
    }
}
