// #Cythisiax Edited - Tile fire: fires now have a level (1 to maxLevel) and a fuel budget, ported from the
// Gruntz FireDef ladder. Every stepInterval a fire may grow a level; once it's down to its last
// dyingFraction of fuel it shrinks instead. From spreadAtLevel up it can jump to the 4 neighbouring
// tiles if there's something to burn there: burnable flooring (grass, wood, carpet, hay), loose
// flammable junk, or a fuel puddle. Fuel puddles go up regardless of level or chance (oil flash) and
// get soaked into the new fire as extra burn time. Rain on an open tile cuts the spread chance,
// a water puddle puts a fire out, and blood only puts out fires below bloodClearLevel. Ground that
// burnt down is marked scorched on the grid for a while so grass fires move on as a front. Damage and
// fire stacks scale with level / referenceLevel, so the old numbers still mean "an average fire".
// Hard cap of MaxFires burning at once so a dry map can't run away with the server.

using Content.Server.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Chemistry.Components;
using Content.Server.Chemistry.TileReactions;
using Content.Server.Fluids.EntitySystems;
using Content.Server.Weather;
using Content.Shared._Misfits.Fire;
using Content.Shared._RMC.Stubs;
using Content.Shared.Atmos;
using Content.Shared.Chemistry.Components;
using Content.Shared.Clothing.EntitySystems;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Content.Shared.Fluids.Components;
using Content.Shared.Inventory;
using Content.Shared.Maps;
using Content.Shared.Mobs.Components;
using Content.Shared.Weather;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Misfits.Fire;

public sealed class TileFireSystem : EntitySystem
{
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly FlammableSystem _flammable = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solution = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly ITileDefinitionManager _tileDefs = default!;
    [Dependency] private readonly PuddleSystem _puddle = default!;
    [Dependency] private readonly WeatherSystem _weather = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    private static readonly ProtoId<ReagentPrototype> WaterReagent = "Water";
    private static readonly ProtoId<DamageTypePrototype> HeatDamage = "Heat";

    public const int MaxFires = 300;

    private static readonly string[] BurnableTileWords = { "Grass", "Wood", "Carpet", "Hay", "Leaf", "Leaves", "Straw", "Plank", "Bush" };
    private static readonly string[] UnburnableTileWords = { "Astro", "Burnt" };
    // explicit on purpose: BaseAlcohol carries FlammableTileReaction, so every spilt beer would flash like fuel
    private static readonly string[] FuelReagents = { "WeldingFuel", "Ethanol", "Napalm", "Oil", "Plasma", "Tritium", "Thermite", "Phlogiston", "ChlorineTrifluoride" };

    private static readonly Vector2i[] Neighbours = { new(1, 0), new(-1, 0), new(0, 1), new(0, -1) };

    private EntityQuery<FlammableComponent> _flammableQuery;
    private EntityQuery<InventoryComponent> _inventoryQuery;
    private EntityQuery<SolutionContainerManagerComponent> _solutionManagerQuery;
    private EntityQuery<VaporComponent> _vaporQuery;
    private EntityQuery<TileFireComponent> _fireQuery;
    private EntityQuery<MobStateComponent> _mobQuery;

    private readonly Dictionary<int, bool> _burnableTiles = new();
    private readonly Dictionary<string, ReagentKind> _reagentKinds = new();
    private readonly List<SpreadRequest> _pendingSpread = new();
    private readonly HashSet<(EntityUid, Vector2i)> _pendingTiles = new();

    [Flags]
    private enum ReagentKind : byte
    {
        None = 0,
        Fuel = 1,
        Douse = 2,
        Blood = 4,
    }

    private record struct PuddleState(float Fuel, float Douse, float Blood);

    private record struct SpreadRequest(EntProtoId Prototype, EntityUid Grid, Vector2i Tile, int Level, float FuelFraction);

    public override void Initialize()
    {
        _flammableQuery = GetEntityQuery<FlammableComponent>();
        _inventoryQuery = GetEntityQuery<InventoryComponent>();
        _solutionManagerQuery = GetEntityQuery<SolutionContainerManagerComponent>();
        _vaporQuery = GetEntityQuery<VaporComponent>();
        _fireQuery = GetEntityQuery<TileFireComponent>();
        _mobQuery = GetEntityQuery<MobStateComponent>();

        SubscribeLocalEvent<TileFireComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        _reagentKinds.Clear();
        _burnableTiles.Clear();
    }

    private void OnMapInit(Entity<TileFireComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.SpawnedAt = _timing.CurTime;
        ent.Comp.NextStep = _timing.CurTime + ent.Comp.StepInterval;
        if (ent.Comp.Level <= 0)
            ent.Comp.Level = ent.Comp.StartLevel;
        ent.Comp.Level = Math.Clamp(ent.Comp.Level, 1, ent.Comp.MaxLevel);

        if (TryGetTile(ent.Owner, out _, out _, out var tileRef))
            SoakFuelPuddle(ent, tileRef);

        Dirty(ent);
        UpdateVisualStage(ent);
    }

    private void TryExtinguishFromVapor(Entity<TileFireComponent> fire, EntityUid vapor)
    {
        if (!_solutionManagerQuery.TryComp(vapor, out var manager))
            return;

        foreach (var (_, solution) in _solution.EnumerateSolutions((vapor, manager)))
        {
            if (!solution.Comp.Solution.ContainsPrototype(WaterReagent))
                continue;

            if (fire.Comp.ExtinguishInstantly)
            {
                QueueDel(fire);
                return;
            }

            fire.Comp.Duration -= TimeSpan.FromSeconds(fire.Comp.VaporExtinguishSeconds);
            Dirty(fire);
            UpdateVisualStage(fire);
            return;
        }
    }

    private void UpdateVisualStage(Entity<TileFireComponent> fire)
    {
        var elapsed = _timing.CurTime - fire.Comp.SpawnedAt;
        if (!fire.Comp.Eternal && elapsed >= fire.Comp.Duration)
            return;

        TileFireVisuals visual;
        if (elapsed < fire.Comp.BigFireDuration)
        {
            visual = TileFireVisuals.Four;
        }
        else
        {
            var frac = fire.Comp.Level / (float) Math.Max(1, fire.Comp.MaxLevel);
            visual = frac switch
            {
                <= 0.25f => TileFireVisuals.One,
                <= 0.5f => TileFireVisuals.Two,
                <= 0.75f => TileFireVisuals.Three,
                _ => TileFireVisuals.Four,
            };
        }

        _appearance.SetData(fire, TileFireLayers.Base, visual);
    }

    private float LevelScale(TileFireComponent fire)
    {
        return fire.Level / (float) Math.Max(1, fire.ReferenceLevel);
    }

    private void ProcessTouch(Entity<TileFireComponent> fire, EntityUid target)
    {
        var touch = EnsureComp<TileFireTouchComponent>(target);
        if (_timing.CurTime < touch.NextTouchAt)
            return;

        touch.NextTouchAt = _timing.CurTime + fire.Comp.TouchCooldown;
        Dirty(target, touch);

        var scale = LevelScale(fire.Comp);

        if (_flammableQuery.TryComp(target, out var flammable))
        {
            _flammable.AdjustFireStacks(target, fire.Comp.FireStacks * scale, flammable);
            _flammable.Ignite(target, fire.Owner, flammable);
        }

        if (fire.Comp.StandingDamage <= 0)
            return;

        var damage = fire.Comp.StandingDamage * scale;
        if (_flammableQuery.TryComp(target, out var burnable) && !burnable.IgnoreFireProtection)
        {
            var ev = new GetFireProtectionEvent();
            RaiseLocalEvent(target, ref ev);

            if (_inventoryQuery.TryComp(target, out var inventory))
                _inventory.RelayEvent((target, inventory), ref ev);

            damage *= ev.Multiplier;
        }

        if (damage <= 0)
            return;

        var spec = new DamageSpecifier(_prototype.Index(HeatDamage), damage);
        _damageable.TryChangeDamage(target, spec, interruptsDoAfters: false);
    }

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;

        var fireCount = 0;
        var counter = EntityQueryEnumerator<TileFireComponent>();
        while (counter.MoveNext(out _, out _))
        {
            fireCount++;
        }

        var query = EntityQueryEnumerator<TileFireComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var fire, out var xform))
        {
            var elapsed = now - fire.SpawnedAt;
            if (!fire.Eternal && elapsed >= fire.Duration)
            {
                Scorch(uid, fire);
                QueueDel(uid);
                continue;
            }

            if (now >= fire.NextStep)
            {
                fire.NextStep = now + fire.StepInterval;
                if (!Step((uid, fire), ref fireCount))
                    continue;
            }

            UpdateVisualStage((uid, fire));

            foreach (var entity in xform.Coordinates.GetEntitiesInTile(LookupFlags.Dynamic, _lookup))
            {
                if (entity == uid)
                    continue;

                if (_vaporQuery.HasComp(entity))
                {
                    TryExtinguishFromVapor((uid, fire), entity);
                    continue;
                }

                ProcessTouch((uid, fire), entity);
            }
        }

        foreach (var req in _pendingSpread)
        {
            if (!TryComp<MapGridComponent>(req.Grid, out var grid) || HasFireAt(req.Grid, grid, req.Tile))
                continue;

            var child = Spawn(req.Prototype, _map.GridTileToLocal(req.Grid, grid, req.Tile));
            if (!_fireQuery.TryComp(child, out var childFire))
                continue;

            childFire.Level = Math.Clamp(req.Level, 1, childFire.MaxLevel);
            childFire.Duration *= req.FuelFraction;
            childFire.BigFireDuration = TimeSpan.Zero;
            Dirty(child, childFire);
            UpdateVisualStage((child, childFire));
        }

        _pendingSpread.Clear();
        _pendingTiles.Clear();
    }

    // returns false when the fire was put out this step
    private bool Step(Entity<TileFireComponent> fire, ref int fireCount)
    {
        if (!TryGetTile(fire.Owner, out var gridUid, out var grid, out var tileRef))
            return true;

        var comp = fire.Comp;
        var puddle = GetPuddleState(tileRef);
        if (puddle.Douse >= comp.DouseVolume || (puddle.Blood >= comp.DouseVolume && comp.Level < comp.BloodClearLevel))
        {
            QueueDel(fire);
            return false;
        }

        if (puddle.Fuel > 0f)
            SoakFuelPuddle(fire, tileRef);

        var oldLevel = comp.Level;
        var remaining = comp.Duration - (_timing.CurTime - comp.SpawnedAt);
        if (!comp.Eternal && remaining < comp.Duration * comp.DyingFraction)
            comp.Level = Math.Max(1, comp.Level - 1);
        else if (comp.Level < comp.MaxLevel && _random.Prob(comp.GrowChance))
            comp.Level++;

        if (comp.Level != oldLevel)
            Dirty(fire);

        if (comp.Spreads && fireCount < MaxFires)
            QueueSpread(fire, gridUid, grid, tileRef, ref fireCount);

        return true;
    }

    private void QueueSpread(Entity<TileFireComponent> fire, EntityUid gridUid, MapGridComponent grid, TileRef tileRef, ref int fireCount)
    {
        var comp = fire.Comp;
        EntProtoId proto;
        if (comp.SpreadPrototype is { } spreadProto)
            proto = spreadProto;
        else if (MetaData(fire).EntityPrototype is { } own)
            proto = own.ID;
        else
            return;

        var chance = comp.SpreadChance * (IsRainedOn(fire, gridUid, grid, tileRef, comp) ? comp.RainSpreadMultiplier : 1f);

        foreach (var offset in Neighbours)
        {
            var index = tileRef.GridIndices + offset;
            if (_pendingTiles.Contains((gridUid, index)))
                continue;

            if (!_map.TryGetTileRef(gridUid, grid, index, out var target) || target.Tile.IsEmpty)
                continue;

            if (HasFireAt(gridUid, grid, index) || target.IsBlockedTurf(false, _lookup))
                continue;

            var puddle = GetPuddleState(target);
            if (puddle.Douse >= comp.DouseVolume || (puddle.Blood >= comp.DouseVolume && comp.Level < comp.BloodClearLevel))
                continue;

            var flash = puddle.Fuel >= 1f;
            if (!flash)
            {
                if (comp.Level < comp.SpreadAtLevel || !_random.Prob(chance))
                    continue;

                if (IsScorched(gridUid, index) || !HasGroundFuel(target))
                    continue;
            }

            var level = flash ? Math.Max(1, comp.Level - 1) : 1;
            _pendingSpread.Add(new SpreadRequest(proto, gridUid, index, level, comp.SpreadFuelFraction));
            _pendingTiles.Add((gridUid, index));

            if (++fireCount >= MaxFires)
                return;
        }
    }

    private bool TryGetTile(EntityUid uid, out EntityUid gridUid, out MapGridComponent grid, out TileRef tileRef)
    {
        gridUid = default;
        grid = default!;
        tileRef = default;

        var xform = Transform(uid);
        if (xform.GridUid is not { } g || !TryComp(g, out MapGridComponent? gridComp))
            return false;

        gridUid = g;
        grid = gridComp;
        return _map.TryGetTileRef(g, gridComp, xform.Coordinates, out tileRef);
    }

    private bool HasFireAt(EntityUid gridUid, MapGridComponent grid, Vector2i index)
    {
        var anchored = _map.GetAnchoredEntitiesEnumerator(gridUid, grid, index);
        while (anchored.MoveNext(out var ent))
        {
            if (_fireQuery.HasComp(ent.Value))
                return true;
        }

        return false;
    }

    private bool HasGroundFuel(TileRef tile)
    {
        if (IsBurnableTile(tile))
            return true;

        foreach (var ent in tile.GetEntitiesInTile(LookupFlags.Static | LookupFlags.Dynamic | LookupFlags.Sundries, _lookup))
        {
            if (_flammableQuery.HasComp(ent) && !_mobQuery.HasComp(ent) && !_fireQuery.HasComp(ent))
                return true;
        }

        return false;
    }

    private bool IsBurnableTile(TileRef tile)
    {
        var typeId = tile.Tile.TypeId;
        if (_burnableTiles.TryGetValue(typeId, out var burnable))
            return burnable;

        var id = _tileDefs[typeId].ID;
        burnable = false;
        foreach (var word in BurnableTileWords)
        {
            if (id.Contains(word, StringComparison.OrdinalIgnoreCase))
            {
                burnable = true;
                break;
            }
        }

        foreach (var word in UnburnableTileWords)
        {
            if (id.Contains(word, StringComparison.OrdinalIgnoreCase))
                burnable = false;
        }

        _burnableTiles[typeId] = burnable;
        return burnable;
    }

    private bool IsRainedOn(EntityUid fire, EntityUid gridUid, MapGridComponent grid, TileRef tile, TileFireComponent comp)
    {
        if (comp.RainSpreadMultiplier >= 1f || Transform(fire).MapUid is not { } mapUid)
            return false;

        if (!TryComp<WeatherComponent>(mapUid, out var weather) || weather.Weather.Count == 0)
            return false;

        var wet = false;
        foreach (var proto in weather.Weather.Keys)
        {
            if (comp.WetWeathers.Contains(proto.Id))
            {
                wet = true;
                break;
            }
        }

        return wet && _weather.CanWeatherAffect(gridUid, grid, tile);
    }

    private bool IsScorched(EntityUid gridUid, Vector2i index)
    {
        if (!TryComp<TileFireScorchComponent>(gridUid, out var scorch) || !scorch.BurntUntil.TryGetValue(index, out var until))
            return false;

        if (_timing.CurTime < until)
            return true;

        scorch.BurntUntil.Remove(index);
        return false;
    }

    private void Scorch(EntityUid uid, TileFireComponent fire)
    {
        if (fire.ScorchSeconds <= 0f || !TryGetTile(uid, out var gridUid, out _, out var tileRef))
            return;

        var scorch = EnsureComp<TileFireScorchComponent>(gridUid);
        scorch.BurntUntil[tileRef.GridIndices] = _timing.CurTime + TimeSpan.FromSeconds(fire.ScorchSeconds);
    }

    private PuddleState GetPuddleState(TileRef tile)
    {
        if (!TryGetPuddleSolution(tile, out _, out var solution))
            return default;

        var state = new PuddleState();
        foreach (var (reagent, quantity) in solution.Contents)
        {
            var kind = GetReagentKind(reagent.Prototype);
            var amount = quantity.Float();
            if ((kind & ReagentKind.Fuel) != 0)
                state.Fuel += amount;
            if ((kind & ReagentKind.Douse) != 0)
                state.Douse += amount;
            if ((kind & ReagentKind.Blood) != 0)
                state.Blood += amount;
        }

        return state;
    }

    private bool TryGetPuddleSolution(TileRef tile, out Entity<SolutionComponent> soln, out Solution solution)
    {
        soln = default;
        solution = default!;

        if (!_puddle.TryGetPuddle(tile, out var puddleUid) || !TryComp<PuddleComponent>(puddleUid, out var puddle))
            return false;

        if (!_solution.ResolveSolution(puddleUid, puddle.SolutionName, ref puddle.Solution, out var resolved))
            return false;

        soln = puddle.Solution.Value;
        solution = resolved;
        return true;
    }

    // burns off the fuel reagents in this tile's puddle and turns them into extra burn time
    private void SoakFuelPuddle(Entity<TileFireComponent> fire, TileRef tile)
    {
        if (!TryGetPuddleSolution(tile, out var soln, out var solution))
            return;

        var fuel = 0f;
        var toRemove = new List<ReagentQuantity>();
        foreach (var reagent in solution.Contents)
        {
            if ((GetReagentKind(reagent.Reagent.Prototype) & ReagentKind.Fuel) == 0)
                continue;

            fuel += reagent.Quantity.Float();
            toRemove.Add(reagent);
        }

        if (fuel <= 0f)
            return;

        foreach (var reagent in toRemove)
        {
            _solution.RemoveReagent(soln, reagent);
        }

        var bonus = MathF.Min(fuel * fire.Comp.FuelSecondsPerUnit, fire.Comp.MaxFuelBonusSeconds);
        fire.Comp.Duration += TimeSpan.FromSeconds(bonus);
        Dirty(fire);
    }

    private ReagentKind GetReagentKind(string id)
    {
        if (_reagentKinds.TryGetValue(id, out var kind))
            return kind;

        kind = ReagentKind.None;
        if (_prototype.TryIndex<ReagentPrototype>(id, out var reagent))
        {
            if (Array.IndexOf(FuelReagents, id) >= 0)
                kind |= ReagentKind.Fuel;

            if (id == WaterReagent.Id || reagent.TileReactions.Exists(r => r is ExtinguishTileReaction))
                kind |= ReagentKind.Douse;

            if (id.Contains("Blood", StringComparison.Ordinal))
                kind |= ReagentKind.Blood;
        }

        _reagentKinds[id] = kind;
        return kind;
    }
}
