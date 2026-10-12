using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CultMath;
using GameCult.Caching;
using GameCult.Caching.MessagePack;
using UniRx;
using Xunit;
using static CultMath.math;
using float2 = CultMath.float2;
using float3 = CultMath.float3;
using int2 = CultMath.int2;

// Hull contact in the sim (aetheria-release cut sim-contact; ruling unity-presents-only-r3): Zone.Update steps
// Contacts, which exchanges the normal velocity of two touching hull discs and publishes the contact. Every test
// drives Zone.Update, the path the game takes. SchematicCellSize is 2, so a solid 5x5 hull has a radius of
// 2.5 * sqrt(2) * 2 = 7.07 and two such hulls ten apart touch.
public sealed class ContactTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "aetheria-contacts-" + Guid.NewGuid().ToString("N"));
    private readonly List<CultCache> _caches = new List<CultCache>();

    public ContactTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        foreach (var c in _caches) c.Dispose();
        Directory.Delete(_root, true);
    }

    private static Shape Solid(int w, int h)
    {
        var shape = new Shape(w, h);
        foreach (var cell in shape.AllCoordinates) shape[cell] = true;
        return shape;
    }

    private static Shape Ell()
    {
        var shape = new Shape(5, 5);
        for (var i = 0; i < 5; i++) { shape[new int2(i, 0)] = true; shape[new int2(0, i)] = true; }
        return shape;
    }

    private static Shape ShippedLonginus()
    {
        var cache = new CultCache(TestCatalog.Registry());
        var store = new SingleFileMessagePackBackingStore(TestCatalog.Repo, true);
        cache.AddBackingStore(store, AetheriaStores.CatalogTypes);
        try { return cache.GetByName<HullData>("Longinus").Shape; }
        finally { cache.Dispose(); }
    }

    private sealed class Lab
    {
        public ItemManager Items;
        public Zone Zone;
        public readonly List<ContactEvent> Events = new List<ContactEvent>();
        public int Lot = 1;

        public Ship Add(string hull, float3 at, float2 velocity)
        {
            var hullRef = Items.ItemData.RefOf<ItemData>(Items.ItemData.GetByName<HullData>(hull));
            var ship = new Ship(Items, Zone, new EquippableItem { Data = hullRef, Durability = 1000000, Lot = Lot++ }, new EntitySettings());
            Zone.Entities.Add(ship);
            ship.Activate();
            ship.Position = at;
            ship.Velocity = velocity;
            return ship;
        }

        public void Step() => Zone.Update(.01f);
    }

    private Lab Build()
    {
        var settings = new GameplaySettings
        {
            DefaultEntitySettings = new EntitySettings(),
            Tiers = new[] { new RarityTier { Name = "Common", Quality = .5f, Rarity = 0, Color = new float3(1, 1, 1) } },
            QualityPriceModifier = new ExponentialLerp(),
            TargetDetectionInfoThreshold = .1f, TargetArmorInfoThreshold = .2f, TargetGearInfoThreshold = .8f,
            FiringArc = 170, CommitHorizon = .5f, SchematicCellSize = 2f
        };
        var cache = AetheriaStores.Open(Path.Combine(_root, Guid.NewGuid().ToString("N") + ".cc"), catalogWritable: true);
        _caches.Add(cache);
        cache.Upsert(new VerseGrammar { Revision = 1 });
        HullData Hull(string name, Shape shape, float mass) => new HullData
        {
            Name = name, HullType = HullType.Ship, Shape = shape, Durability = 1000000, Mass = mass, Armor = 0,
            Hardpoints = { new HardpointData { Type = HardpointType.Sensors, Position = new int2(0, 0), Shape = new Shape() } }
        };
        cache.Upsert(Hull("Solid", Solid(5, 5), 1000));
        cache.Upsert(Hull("Heavy", Solid(5, 5), 1000000));
        cache.Upsert(Hull("Ell", Ell(), 1000));
        cache.Upsert(Hull("Longinus", ShippedLonginus(), 1000));
        cache.FlushAsync().Wait();

        var ledger = new ProvenanceLedger();
        for (var i = 1; i <= 50; i++) ledger.Lots[i] = new Lot { Origin = new Attributed(), Quality = 1 };
        var items = new ItemManager(cache, ledger, settings, _ => { });
        var zone = new Zone(items, new PlanetSettings(), new ZonePack { Radius = 0 }, new GalaxyZone { Name = "Contacts", Owner = null }, null);
        var lab = new Lab { Items = items, Zone = zone };
        zone.Contacted.Subscribe(lab.Events.Add);
        return lab;
    }

    // The unit normal as the step saw it: from the first body's final position to the second's.
    private static float2 NormalOf(Entity a, Entity b) => normalize(b.Position.xz - a.Position.xz);

    // The formula is the deleted ShieldManager.PostCollisionVelocity verbatim (cut sim-contact moves it, it does
    // not change it): with equal masses a body ends at half the other's incoming normal velocity.
    [Fact]
    public void HeadOnEqualMassesTakeHalfTheOthersNormalVelocity()
    {
        var lab = Build();
        var a = lab.Add("Solid", float3(0, 0, 0), float2(10, 0));
        var b = lab.Add("Solid", float3(10, 0, 0), float2(-10, 0));
        lab.Step();

        var n = NormalOf(a, b);
        Assert.Equal(-5f, dot(n, a.Velocity), 4);
        Assert.Equal(5f, dot(n, b.Velocity), 4);
        var contact = Assert.Single(lab.Events);
        Assert.Equal(new[] { a, b }, new[] { contact.A, contact.B });
        Assert.Equal(1f, length(contact.Normal), 5);
        Assert.Equal(n.x, contact.Normal.x, 5);
        // The contact point sits on A's disc, towards B.
        Assert.Equal(7.0711f, length(contact.Point - a.Position.xz), 2);
    }

    [Fact]
    public void GlancingContactKeepsTangent()
    {
        var lab = Build();
        var a = lab.Add("Solid", float3(0, 0, 0), float2(10, 0));
        var b = lab.Add("Solid", float3(7.07f, 0, 7.07f), float2(0, 0));
        lab.Step();

        var n = NormalOf(a, b);
        var t = float2(-n.y, n.x);
        // A's incoming velocity (10, 0) has after the step the tangent component it had before, and so does B's (zero).
        Assert.Equal(10f * t.x, dot(t, a.Velocity), 4);
        Assert.Equal(0f, dot(t, b.Velocity), 4);
        // Only the normal components changed: A handed half its incoming normal component 10 * n.x to B.
        Assert.Equal(0f, dot(n, a.Velocity), 4);
        Assert.Equal(5f * n.x, dot(n, b.Velocity), 4);
        Assert.Single(lab.Events);
    }

    [Fact]
    public void OpeningPairsAreUntouched()
    {
        var lab = Build();
        var a = lab.Add("Solid", float3(0, 0, 0), float2(-5, 0));
        var b = lab.Add("Solid", float3(10, 0, 0), float2(5, 0));
        lab.Step();
        Assert.Equal(-5f, a.Velocity.x, 4);
        Assert.Equal(5f, b.Velocity.x, 4);
        Assert.Empty(lab.Events);
    }

    [Fact]
    public void OneEventPerContact()
    {
        var lab = Build();
        lab.Add("Solid", float3(0, 0, 0), float2(10, 0));
        lab.Add("Solid", float3(10, 0, 0), float2(-10, 0));
        lab.Step();
        Assert.Single(lab.Events);
        // The exchange left the pair opening: the next step publishes nothing.
        lab.Step();
        Assert.Single(lab.Events);
    }

    [Fact]
    public void ADistantPairNeverTouches()
    {
        var lab = Build();
        lab.Add("Solid", float3(0, 0, 0), float2(10, 0));
        lab.Add("Solid", float3(15f, 0, 0), float2(-10, 0));
        lab.Step();
        Assert.Empty(lab.Events);
    }

    // Today's formula for a ship against a 1000x heavier body: the ship's normal velocity becomes
    // (v1 (m1 - m2) + m2 v2) / (m1 + m2) = 10 * -999000 / 1001000, the heavy body's 10 * 1000 / 1001000.
    [Fact]
    public void MassRatioIsTodaysFormula()
    {
        var lab = Build();
        var ship = lab.Add("Solid", float3(0, 0, 0), float2(10, 0));
        var station = lab.Add("Heavy", float3(10, 0, 0), float2(0, 0));
        Assert.Equal(1000f, ship.Mass);
        Assert.Equal(1000000f, station.Mass);
        lab.Step();

        var n = NormalOf(ship, station);
        Assert.Equal(10f * -999000f / 1001000f, dot(n, ship.Velocity), 3);
        Assert.Equal(10f * 1000f / 1001000f, dot(n, station.Velocity), 4);
    }

    [Fact]
    public void ADockedHullTakesNoPartInContact()
    {
        var lab = Build();
        var carrier = lab.Add("Solid", float3(0, 0, 0), float2(0, 0));
        var docked = lab.Add("Solid", float3(0, 0, 0), float2(0, 0));
        docked.SetParent(carrier);
        lab.Step();
        Assert.Empty(lab.Events);
    }

    [Theory]
    [InlineData("Ell")]
    [InlineData("Longinus")]
    public void RadiusBoundsEveryCell(string hull)
    {
        var lab = Build();
        var ship = lab.Add(hull, float3(0, 0, 0), float2(0, 0));
        var shape = ship.HullData.Shape;
        var com = shape.CenterOfMass;
        var radius = Contacts.Radius(ship);

        var far = 0f;
        foreach (var c in shape.Coordinates)
            foreach (var (dx, dy) in new[] { (-.5f, -.5f), (.5f, -.5f), (-.5f, .5f), (.5f, .5f) })
            {
                var d = length(float2(c.x + dx, c.y + dy) - com) * 2f;
                Assert.True(d <= radius + 1e-4f, $"a corner of {hull} lies {d} from the centre of mass, past Radius {radius}");
                far = max(far, d);
            }
        Assert.Equal(radius, far, 3);
    }
}
