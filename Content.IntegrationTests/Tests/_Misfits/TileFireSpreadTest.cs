// #Cythisiax Added - Tile fire levels/spread: a grown fire jumps onto grass but not plating, a water
// puddle puts a fire out, and blood only puts out fires below bloodClearLevel.

using System.Linq;
using Content.Server.Fluids.EntitySystems;
using Content.Shared._Misfits.Fire;
using Content.Shared.Chemistry.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests._Misfits;

[TestFixture]
public sealed class TileFireSpreadTest
{
    private const string FireProto = "MisfitsTileFire";

    private static EntityUid SpawnFire(IEntityManager entities, IGameTiming timing, EntityUid grid, Vector2i tile, int level, bool spreads)
    {
        var fire = entities.SpawnEntity(FireProto, new EntityCoordinates(grid, tile.X + 0.5f, tile.Y + 0.5f));
        var comp = entities.GetComponent<TileFireComponent>(fire);
        comp.Level = level;
        comp.GrowChance = 0f;
        comp.Spreads = spreads;
        comp.SpreadChance = 1f;
        comp.Duration = TimeSpan.FromMinutes(5);
        comp.NextStep = timing.CurTime;
        return fire;
    }

    private static bool FireAt(IEntityManager entities, SharedMapSystem maps, Entity<MapGridComponent> grid, Vector2i tile)
    {
        return maps.GetAnchoredEntities(grid.Owner, grid.Comp, tile).Any(e => entities.HasComponent<TileFireComponent>(e));
    }

    [Test]
    public async Task FireSpreadsOverGrassAndIsDoused()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.ResolveDependency<IEntityManager>();
        var tileDefs = server.ResolveDependency<ITileDefinitionManager>();
        var timing = server.ResolveDependency<IGameTiming>();
        var maps = entities.System<SharedMapSystem>();
        var puddles = entities.System<PuddleSystem>();
        var grid = map.Grid;

        EntityUid douseFire = default, weakBloodFire = default, strongBloodFire = default;

        await server.WaitPost(() =>
        {
            var grass = new Tile(tileDefs["FloorGrass"].TileId);
            var plating = new Tile(tileDefs["Plating"].TileId);

            // spread row: plating | grass(fire) | grass
            maps.SetTile(grid, new Vector2i(-1, 0), plating);
            maps.SetTile(grid, new Vector2i(0, 0), grass);
            maps.SetTile(grid, new Vector2i(1, 0), grass);
            SpawnFire(entities, timing, grid, new Vector2i(0, 0), 7, true);

            // plating run for the douse checks; kept connected so the grid doesn't split
            for (var x = 2; x <= 16; x++)
                maps.SetTile(grid, new Vector2i(x, 0), plating);

            puddles.TrySpillAt(new EntityCoordinates(grid, 10.5f, 0.5f), new Solution("Water", 20), out _, sound: false);
            puddles.TrySpillAt(new EntityCoordinates(grid, 13.5f, 0.5f), new Solution("Blood", 20), out _, sound: false);
            puddles.TrySpillAt(new EntityCoordinates(grid, 16.5f, 0.5f), new Solution("Blood", 20), out _, sound: false);

            douseFire = SpawnFire(entities, timing, grid, new Vector2i(10, 0), 7, false);
            weakBloodFire = SpawnFire(entities, timing, grid, new Vector2i(13, 0), 3, false);
            strongBloodFire = SpawnFire(entities, timing, grid, new Vector2i(16, 0), 7, false);
        });

        await pair.RunTicksSync(10);

        await server.WaitAssertion(() =>
        {
            var tileRef = maps.GetTileRef(grid, new Vector2i(10, 0));
            Assert.Multiple(() =>
            {
            Assert.That(puddles.TryGetPuddle(tileRef, out _), Is.True, "water puddle should exist");
            Assert.That(FireAt(entities, maps, grid, new Vector2i(1, 0)), Is.True, "fire should spread onto grass");
            Assert.That(FireAt(entities, maps, grid, new Vector2i(-1, 0)), Is.False, "fire should not spread onto plating");
            Assert.That(entities.Deleted(douseFire), Is.True, "water puddle should put the fire out");
            Assert.That(entities.Deleted(weakBloodFire), Is.True, "blood should put out a level 3 fire");
            Assert.That(entities.Deleted(strongBloodFire), Is.False, "blood should not put out a level 7 fire");
            });
        });

        await pair.CleanReturnAsync();
    }
}
