using Content.Shared._Misfits.WastelandMap;
using Robust.Server.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.Server._Misfits.WastelandMap;

/// <summary>Creates named, global, round-scoped tactical-map GPS landmarks.</summary>
public sealed class MapPinSystem : EntitySystem
{
    private const int MaxLabelLength = 64;

    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<MapPinInputComponent, MapPinNameMessage>(OnNameSubmitted);
        SubscribeLocalEvent<MapPinInputComponent, BoundUIClosedEvent>(OnPromptClosed);
        SubscribeLocalEvent<MapPinManageComponent, MapPinRemoveMessage>(OnPinRemoved);
        SubscribeLocalEvent<MapPinManageComponent, BoundUIClosedEvent>(OnManagerClosed);
    }

    public bool TryOpenManager(EntityUid actor)
    {
        if (Deleted(actor))
            return false;

        var manager = Spawn("MisfitsMapPinManager", _transform.GetMapCoordinates(actor));
        Comp<MapPinManageComponent>(manager).Creator = actor;
        _ui.OpenUi(manager, MapPinManageUiKey.Key, actor);
        UpdateManager(manager);
        return true;
    }

    /// <summary>
    /// Opens the name prompt and captures the caller's current map position. The final pin remains
    /// in the world only for this round, alongside the other runtime map landmarks.
    /// </summary>
    public bool TryOpenNamePrompt(EntityUid actor)
    {
        if (Deleted(actor))
            return false;

        var input = Spawn("MisfitsMapPinInput", _transform.GetMapCoordinates(actor));
        var component = Comp<MapPinInputComponent>(input);
        component.Creator = actor;
        component.Location = _transform.GetMapCoordinates(actor);
        _ui.OpenUi(input, MapPinUiKey.Key, actor);
        return true;
    }

    private void OnNameSubmitted(Entity<MapPinInputComponent> ent, ref MapPinNameMessage args)
    {
        if (args.Actor is not { Valid: true } actor || actor != ent.Comp.Creator)
            return;

        var label = args.Name.Trim();
        if (label.Length == 0)
            return;

        if (label.Length > MaxLabelLength)
            label = label[..MaxLabelLength].TrimEnd();

        var pin = EntityManager.SpawnEntity(null, ent.Comp.Location);
        var marker = EnsureComp<GlobalMapPinComponent>(pin);
        marker.Label = label;
        Dirty(pin, marker);

        QueueDel(ent);
    }

    private void OnPromptClosed(Entity<MapPinInputComponent> ent, ref BoundUIClosedEvent args)
    {
        if (args.UiKey.Equals(MapPinUiKey.Key))
            QueueDel(ent);
    }

    private void OnPinRemoved(Entity<MapPinManageComponent> ent, ref MapPinRemoveMessage args)
    {
        if (args.Actor is not { Valid: true } actor || actor != ent.Comp.Creator ||
            !TryGetEntity(args.Pin, out var pin) || !HasComp<GlobalMapPinComponent>(pin))
        {
            return;
        }

        QueueDel(pin);
        UpdateManager(ent);
    }

    private void OnManagerClosed(Entity<MapPinManageComponent> ent, ref BoundUIClosedEvent args)
    {
        if (args.UiKey.Equals(MapPinManageUiKey.Key))
            QueueDel(ent);
    }

    private void UpdateManager(EntityUid manager)
    {
        var pins = new List<GlobalMapPinEntry>();
        var query = EntityQueryEnumerator<GlobalMapPinComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var pin, out var transform))
        {
            if (Deleted(uid))
                continue;

            var coords = _transform.GetMapCoordinates(uid, transform);
            pins.Add(new GlobalMapPinEntry(GetNetEntity(uid), pin.Label, coords.Position.X, coords.Position.Y));
        }

        pins.Sort((left, right) => string.Compare(left.Label, right.Label, StringComparison.OrdinalIgnoreCase));
        _ui.SetUiState(manager, MapPinManageUiKey.Key, new MapPinManageState(pins.ToArray()));
    }
}

/// <summary>Round-scoped GPS landmark rendered on every WastelandMap tactical feed.</summary>
[RegisterComponent]
public sealed partial class GlobalMapPinComponent : Component
{
    public string Label = string.Empty;
}

/// <summary>Short-lived BUI host for a new map pin.</summary>
[RegisterComponent]
public sealed partial class MapPinInputComponent : Component
{
    public EntityUid Creator;
    public MapCoordinates Location;
}

/// <summary>Short-lived BUI host for the removal list.</summary>
[RegisterComponent]
public sealed partial class MapPinManageComponent : Component
{
    public EntityUid Creator;
}
