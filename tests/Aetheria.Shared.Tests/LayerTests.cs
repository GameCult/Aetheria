using System;
using System.IO;
using System.Linq;
using System.Reflection;
using CultMath;
using GameCult.Caching;
using GameCult.Caching.MessagePack;
using Xunit;
using static CultMath.math;
using float2 = CultMath.float2;
using float3 = CultMath.float3;
using int2 = CultMath.int2;

// Layers (aetheria-gravity cut orbital-layering; ruling orbitals-above-floaters-below): GameplaySettings' three
// heights decide where each kind of thing sits above the field. A ship rides the ship plane, every orbital entity
// (station, turret) the orbital layer above it, a floater (loot, mine) the floater layer below. No hull carries a
// height of its own.
public sealed class LayerTests : IDisposable
{
    private const float ShipHeight = 2.5f, OrbitalHeight = 9f, FloaterHeight = -1.25f;

    private readonly string _root = Path.Combine(Path.GetTempPath(), "aetheria-layers-" + Guid.NewGuid().ToString("N"));
    private readonly CultCache _cache;

    public LayerTests()
    {
        Directory.CreateDirectory(_root);
        _cache = AetheriaStores.Open(Path.Combine(_root, "layers.cc"), catalogWritable: true);
    }

    public void Dispose()
    {
        _cache.Dispose();
        Directory.Delete(_root, true);
    }

    private static GameplaySettings Settings() => new GameplaySettings
    {
        DefaultEntitySettings = new EntitySettings(),
        Tiers = new[] { new RarityTier { Name = "Common", Quality = .5f, Color = new float3(1, 1, 1) } },
        QualityPriceModifier = new ExponentialLerp(),
        ShipLayerHeight = ShipHeight, OrbitalLayerHeight = OrbitalHeight, FloaterLayerHeight = FloaterHeight
    };

    private (ItemManager Items, Zone Zone) Build()
    {
        // Hull sizes and masses are spread so no kind coincides with a size or a mass: a layer chosen by either fails.
        Shape Solid(int side)
        {
            var shape = new Shape(side, side);
            foreach (var cell in shape.AllCoordinates) shape[cell] = true;
            return shape;
        }
        _cache.Upsert(new VerseGrammar { Revision = 1 });
        _cache.Upsert(new HullData { Name = "Ship", HullType = HullType.Ship, Shape = Solid(5), Durability = 1, Mass = 1000 });
        _cache.Upsert(new HullData { Name = "Station", HullType = HullType.Station, Shape = Solid(3), Durability = 1, Mass = 1000000 });
        _cache.Upsert(new HullData { Name = "Turret", HullType = HullType.Turret, Shape = Solid(7), Durability = 1, Mass = 50 });
        _cache.FlushAsync().Wait();
        var ledger = new ProvenanceLedger();
        for (var i = 1; i <= 3; i++) ledger.Lots[i] = new Lot { Origin = new Attributed(), Quality = 1 };
        var items = new ItemManager(1u, _cache, ledger, Settings(), _ => { });
        var zone = new Zone(items, new PlanetSettings(), new ZonePack { Radius = 0 }, new GalaxyZone { Name = "Layers", Owner = null }, null);
        return (items, zone);
    }

    private static EquippableItem Hull(ItemManager items, string name, int lot) => new EquippableItem
    {
        Data = items.ItemData.RefOf<ItemData>(items.ItemData.GetByName<HullData>(name)), Durability = 1, Lot = lot
    };

    [Fact]
    public void EachKindRidesItsLayer()
    {
        var (items, zone) = Build();
        var ship = new Ship(items, zone, Hull(items, "Ship", 1), new EntitySettings());
        var station = new OrbitalEntity(items, zone, Hull(items, "Station", 2), default, new EntitySettings());
        var turret = new OrbitalEntity(items, zone, Hull(items, "Turret", 3), default, new EntitySettings());
        foreach (var e in new Entity[] { ship, station, turret }) zone.Admit(e, piloted: false);
        ship.Position = float3(40, 0, 10);
        station.Position = float3(-30, 0, 20);
        turret.Position = float3(5, 0, -60);

        zone.Update(.1f);

        Assert.Equal(zone.GetHeight(ship.Position.xz) + ShipHeight, ship.Position.y);
        Assert.Equal(zone.GetHeight(station.Position.xz) + OrbitalHeight, station.Position.y);
        Assert.Equal(zone.GetHeight(turret.Position.xz) + OrbitalHeight, turret.Position.y);
        const float lead = .05f;
        foreach (var (e, layer) in new (Entity, float)[] { (ship, ShipHeight), (station, OrbitalHeight), (turret, OrbitalHeight) })
        {
            var drawn = DrawAhead.Position(e, lead);
            Assert.Equal(zone.GetHeight(drawn.xz, DrawAhead.Time(zone, lead)) + layer, drawn.y);
        }
    }

    [Fact]
    public void OrbitalsAboveShipsAboveFloaters()
    {
        var authored = AuthoredSettings.Load(RunStartTests.FindRepoRoot()).Read<GameplaySettings>("GameplaySettings");
        Assert.True(authored.OrbitalLayerHeight > authored.ShipLayerHeight);
        Assert.True(authored.ShipLayerHeight > authored.FloaterLayerHeight);
    }

    [Fact]
    public void AFloaterSitsOnTheFloaterLayer()
    {
        var (items, zone) = Build();
        var settings = items.GameplaySettings;
        foreach (var at in new[] { float2(0, 0), float2(37, -12) })
            foreach (var lead in new[] { 0f, .05f })
                Assert.Equal(DrawAhead.Height(zone, at, lead) + FloaterHeight, DrawAhead.FloaterHeight(zone, settings, at, lead));
    }

    [Fact]
    public void HullKey26IsRetired()
    {
        var keys = new System.Collections.Generic.HashSet<int>();
        for (var t = typeof(HullData); t != null; t = t.BaseType)
            foreach (var m in t.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                foreach (var k in m.GetCustomAttributes(typeof(MessagePack.KeyAttribute), false).Cast<MessagePack.KeyAttribute>())
                    if (k.IntKey.HasValue) keys.Add(k.IntKey.Value);
        Assert.NotEmpty(keys);
        Assert.DoesNotContain(26, keys);

        using var shipped = new CultCache(TestCatalog.Registry());
        shipped.AddBackingStore(new SingleFileMessagePackBackingStore(TestCatalog.Repo, true), AetheriaStores.CatalogTypes);
        Assert.NotEmpty(shipped.GetAll<HullData>());
    }
}
