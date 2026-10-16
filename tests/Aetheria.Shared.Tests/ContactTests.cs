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

// Hull contact in the sim (aetheria-release cut sim-contact; rulings unity-presents-only-r3 and
// ship-bounce-restitution-setting): Zone.Update steps Contacts, which applies the restitution impulse along the
// normal of two touching, closing ship discs and publishes the contact. Contact is ship on ship: orbital entities
// sit above the ship plane and take no part in it (ruling aetheria-gravity:orbitals-above-floaters-below). Every test
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
            Zone.Admit(ship, piloted: false);
            ship.Position = at;
            ship.Velocity = velocity;
            return ship;
        }

        // A station or turret: an orbital entity with no orbit, so it stays where it is put.
        public OrbitalEntity AddOrbital(string hull, float3 at, float2 velocity)
        {
            var hullRef = Items.ItemData.RefOf<ItemData>(Items.ItemData.GetByName<HullData>(hull));
            var orbital = new OrbitalEntity(Items, Zone, new EquippableItem { Data = hullRef, Durability = 1000000, Lot = Lot++ }, default, new EntitySettings());
            Zone.Admit(orbital, piloted: false);
            orbital.Position = at;
            orbital.Velocity = velocity;
            return orbital;
        }

        public void Step() => Zone.Update(.01f);
    }

    private Lab Build(float restitution = .5f)
    {
        var settings = new GameplaySettings
        {
            DefaultEntitySettings = new EntitySettings(),
            Tiers = new[] { new RarityTier { Name = "Common", Quality = .5f, Rarity = 0, Color = new float3(1, 1, 1) } },
            QualityPriceModifier = new ExponentialLerp(),
            TargetDetectionInfoThreshold = .1f, TargetGearInfoThreshold = .8f,
            FiringArc = 170, CommitHorizon = .5f, SchematicCellSize = 2f, ContactRestitution = restitution
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
        cache.Upsert(Hull("Big", Solid(7, 7), 1000));
        cache.Upsert(Hull("Mid", Solid(5, 5), 3000));
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

    private static float2 Momentum(params Entity[] es)
    {
        var p = float2(0, 0);
        foreach (var e in es) p += e.Mass * e.Velocity;
        return p;
    }

    // An oblique contact between unequal masses with both ships moving; `shift` is added to both incoming
    // velocities (a moving frame).
    private (Ship a, Ship b, float2 closingBefore, Lab lab) Oblique(float e, float2 shift)
    {
        var lab = Build(e);
        var va = float2(8, 3) + shift;
        var vb = float2(-4, -2) + shift;
        var a = lab.Add("Solid", float3(0, 0, 0), va);
        var b = lab.Add("Mid", float3(7, 0, 7), vb);
        lab.Step();
        return (a, b, vb - va, lab);
    }

    // Restitution: the speed at which a pair separates along the normal is e times the speed at which it closed.
    [Theory]
    [InlineData(0f)]
    [InlineData(.5f)]
    [InlineData(1f)]
    public void SeparatingNormalSpeedIsRestitutionTimesClosingSpeed(float e)
    {
        var (a, b, relBefore, lab) = Oblique(e, float2(0, 0));
        var n = NormalOf(a, b);
        var closing = -dot(n, relBefore);
        Assert.True(closing > 0f);
        Assert.Equal(e * closing, dot(n, b.Velocity - a.Velocity), 3);
        Assert.Single(lab.Events);
    }

    // The same bounce at a closing speed far below and far above the others: restitution is the setting at any speed.
    [Theory]
    [InlineData(.5f, 2f)]
    [InlineData(.5f, 40f)]
    [InlineData(1f, 2f)]
    [InlineData(1f, 40f)]
    [InlineData(0f, 2f)]
    public void RestitutionHoldsAtEveryClosingSpeed(float e, float speedEach)
    {
        var lab = Build(e);
        var a = lab.Add("Solid", float3(0, 0, 0), float2(speedEach, 0));
        var b = lab.Add("Heavy", float3(10, 0, 0), float2(-speedEach, 0));
        lab.Step();
        var n = NormalOf(a, b);
        Assert.Equal(e * 2f * speedEach * n.x, dot(n, b.Velocity - a.Velocity), 3);
        Assert.Single(lab.Events);
    }

    // Momentum is conserved for unequal masses, an oblique contact and both ships moving.
    [Theory]
    [InlineData(0f)]
    [InlineData(.5f)]
    [InlineData(1f)]
    public void ContactConservesMomentum(float e)
    {
        var lab = Build(e);
        var a = lab.Add("Solid", float3(0, 0, 0), float2(8, 3));
        var b = lab.Add("Mid", float3(7, 0, 7), float2(-4, -2));
        var before = Momentum(a, b);
        lab.Step();
        Assert.Single(lab.Events);
        var after = Momentum(a, b);
        Assert.Equal(before.x, after.x, 1);
        Assert.Equal(before.y, after.y, 1);
        Assert.True(length(a.Velocity - float2(8, 3)) > 1f, "the contact moved the first ship");
    }

    // A frame moving with the pair changes nothing: add the same velocity to both and each outcome shifts by exactly it.
    [Theory]
    [InlineData(.5f)]
    [InlineData(1f)]
    public void OutcomeShiftsByTheFrameVelocity(float e)
    {
        var shift = float2(25, -40);
        var (a0, b0, _, _) = Oblique(e, float2(0, 0));
        var (a1, b1, _, lab1) = Oblique(e, shift);
        Assert.Single(lab1.Events);
        Assert.Equal(a0.Velocity.x + shift.x, a1.Velocity.x, 2);
        Assert.Equal(a0.Velocity.y + shift.y, a1.Velocity.y, 2);
        Assert.Equal(b0.Velocity.x + shift.x, b1.Velocity.x, 2);
        Assert.Equal(b0.Velocity.y + shift.y, b1.Velocity.y, 2);
    }

    // Two equal ships in a formation at 100 drift together at a closing speed of 1: they bounce apart at e, and
    // the convoy keeps its momentum.
    [Fact]
    public void FormationBumpKeepsTheConvoyMoving()
    {
        var lab = Build(.5f);
        var a = lab.Add("Solid", float3(0, 0, 0), float2(101, 0));
        var b = lab.Add("Solid", float3(10, 0, 0), float2(100, 0));
        var before = Momentum(a, b);
        lab.Step();
        Assert.Single(lab.Events);
        Assert.Equal(before.x, Momentum(a, b).x, 1);
        Assert.True(a.Velocity.x > 99f && b.Velocity.x > 99f);
    }

    [Fact]
    public void HeadOnEqualMassesAtFullRestitutionSwapNormalVelocity()
    {
        var lab = Build(1f);
        var a = lab.Add("Solid", float3(0, 0, 0), float2(10, 0));
        var b = lab.Add("Solid", float3(10, 0, 0), float2(-10, 0));
        lab.Step();

        var n = NormalOf(a, b);
        Assert.Equal(-10f, dot(n, a.Velocity), 3);
        Assert.Equal(10f, dot(n, b.Velocity), 3);
        var contact = Assert.Single(lab.Events);
        Assert.Equal(new[] { a, b }, new[] { contact.A, contact.B });
        Assert.Equal(1f, length(contact.Normal), 5);
        Assert.Equal(n.x, contact.Normal.x, 5);
        // The contact point sits on A's disc, towards B.
        Assert.Equal(7.0711f, length(contact.Point - a.Position.xz), 2);
    }

    // The point is on A's disc, at A's radius, not B's: a small hull A against a larger hull B.
    [Fact]
    public void ContactPointLiesOnTheFirstHullsDisc()
    {
        var lab = Build();
        var a = lab.Add("Solid", float3(0, 0, 0), float2(10, 0));
        var b = lab.Add("Big", float3(12, 0, 0), float2(0, 0));
        lab.Step();
        Assert.True(Contacts.Radius(b) > Contacts.Radius(a) + 1f);
        var contact = Assert.Single(lab.Events);
        Assert.Equal(Contacts.Radius(a), length(contact.Point - a.Position.xz), 3);
    }

    // Only the normal component changes: both bodies keep the tangent component they entered with.
    [Fact]
    public void BothBodiesKeepTheirTangentVelocity()
    {
        var lab = Build();
        var a = lab.Add("Solid", float3(0, 0, 0), float2(10, 0));
        var b = lab.Add("Mid", float3(7.07f, 0, 7.07f), float2(0, 3));
        lab.Step();

        var n = NormalOf(a, b);
        var t = float2(-n.y, n.x);
        Assert.Equal(dot(t, float2(10, 0)), dot(t, a.Velocity), 4);
        Assert.Equal(dot(t, float2(0, 3)), dot(t, b.Velocity), 4);
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

    // A pair that has separated is not bounced again: a heavy ship catching a light one is thrown clear by one
    // contact, and the next ticks (the hulls still overlap, now opening) change and publish nothing.
    [Fact]
    public void NoImpulseWhileSeparating()
    {
        var lab = Build(.5f);
        var a = lab.Add("Heavy", float3(0, 0, 0), float2(110, 0));
        var b = lab.Add("Solid", float3(10, 0, 0), float2(100, 0));
        lab.Step();
        var n = NormalOf(a, b);
        Assert.True(dot(n, b.Velocity - a.Velocity) > 0f, "the bounce left the pair opening");
        var (va, vb) = (a.Velocity, b.Velocity);
        for (var i = 0; i < 5; i++) lab.Step();
        Assert.Single(lab.Events);
        Assert.Equal(va.x, a.Velocity.x, 3);
        Assert.Equal(vb.x, b.Velocity.x, 3);
    }

    [Fact]
    public void OneEventPerContact()
    {
        var lab = Build();
        lab.Add("Solid", float3(0, 0, 0), float2(10, 0));
        lab.Add("Solid", float3(10, 0, 0), float2(-10, 0));
        lab.Step();
        Assert.Single(lab.Events);
        // The bounce left the pair opening: the next step publishes nothing.
        lab.Step();
        Assert.Single(lab.Events);
    }

    // A pair closing slowly still touches: there is no speed below which hulls pass through each other.
    [Fact]
    public void SlowClosingStillBounces()
    {
        var lab = Build(.5f);
        var a = lab.Add("Solid", float3(0, 0, 0), float2(2, 0));
        var b = lab.Add("Solid", float3(10, 0, 0), float2(-2, 0));
        lab.Step();
        var n = NormalOf(a, b);
        Assert.Single(lab.Events);
        Assert.Equal(.5f * 4f * n.x, dot(n, b.Velocity - a.Velocity), 3);
    }

    // The tiniest closing speed still bounces too.
    [Fact]
    public void BarelyClosingStillBounces()
    {
        var lab = Build(1f);
        lab.Add("Solid", float3(0, 0, 0), float2(.1f, 0));
        lab.Add("Solid", float3(10, 0, 0), float2(0, 0));
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

    // A ship against a 1000x heavier ship: the heavy ship takes (1 + e) m / (m + M) of the ship's closing speed
    // and the ship rebounds with the rest of its momentum.
    [Fact]
    public void AShipAgainstAHeavyHullRebounds()
    {
        var lab = Build(.5f);
        var ship = lab.Add("Solid", float3(0, 0, 0), float2(10, 0));
        var heavy = lab.Add("Heavy", float3(10, 0, 0), float2(0, 0));
        Assert.Equal(1000f, ship.Mass);
        Assert.Equal(1000000f, heavy.Mass);
        lab.Step();

        var n = NormalOf(ship, heavy);
        var closing = 10f * n.x;
        Assert.Equal(1.5f * closing * 1000f / 1001000f, dot(n, heavy.Velocity), 4);
        Assert.Equal(closing - 1.5f * closing * 1000000f / 1001000f, dot(n, ship.Velocity), 3);
    }

    // An inactive hull is not in the zone: a ship flying through it is untouched.
    [Fact]
    public void AnInactiveHullTakesNoPartInContact()
    {
        var lab = Build();
        var a = lab.Add("Solid", float3(0, 0, 0), float2(10, 0));
        var b = lab.Add("Solid", float3(10, 0, 0), float2(-10, 0));
        b.Deactivate();
        lab.Step();
        Assert.Empty(lab.Events);
        Assert.Equal(10f, a.Velocity.x, 4);
        Assert.Equal(-10f, b.Velocity.x, 4);
    }

    // Orbital entities live above the ship plane (ruling orbitals-above-floaters-below): a ship closing on a station
    // and on a turret flies through both, and nothing is published.
    [Fact]
    public void AShipFliesThroughAStationAndATurret()
    {
        var lab = Build();
        var ship = lab.Add("Solid", float3(0, 0, 0), float2(10, 0));
        var station = lab.AddOrbital("Heavy", float3(10, 0, 0), float2(0, 0));
        // Behind the ship and faster than it, so it closes on the ship as the station does.
        var turret = lab.AddOrbital("Solid", float3(-10, 0, 0), float2(30, 0));
        lab.Step();
        Assert.Empty(lab.Events);
        Assert.Equal(float2(10, 0), ship.Velocity);
        Assert.Equal(float2(0, 0), station.Velocity);
        Assert.Equal(float2(30, 0), turret.Velocity);
    }

    // An orbital overlapping a colliding pair changes nothing about the pair (ruling ship-bounce-restitution-setting):
    // one contact, between the two ships, with the restitution split of the pair alone.
    [Fact]
    public void TwoShipsStillBounceBesideAnOrbital()
    {
        var lab = Build(.5f);
        var a = lab.Add("Solid", float3(0, 0, 0), float2(10, 0));
        var b = lab.Add("Solid", float3(10, 0, 0), float2(-10, 0));
        lab.AddOrbital("Heavy", float3(5, 0, 0), float2(0, 0));
        lab.Step();

        var contact = Assert.Single(lab.Events);
        Assert.Contains(a, new[] { contact.A, contact.B });
        Assert.Contains(b, new[] { contact.A, contact.B });
        var n = NormalOf(a, b);
        Assert.Equal(.5f * 20f * n.x, dot(n, b.Velocity - a.Velocity), 3);
    }

    // Pairs resolve one after another in list order, so a body touching two others in one tick can leave the pile
    // differently when the list is reversed (order independence is not claimed); what the order does not change is
    // the momentum of the pile and the two contacts it publishes.
    [Fact]
    public void ThreeBodyContactConservesMomentumInEitherListOrder()
    {
        foreach (var reversed in new[] { false, true })
        {
            var lab = Build(.5f);
            var specs = new[] { ("Solid", float3(0, 0, 0), float2(10, 0)), ("Mid", float3(10, 0, 0), float2(0, 0)), ("Solid", float3(20, 0, 0), float2(-10, 0)) };
            var ships = new Ship[3];
            foreach (var i in reversed ? new[] { 2, 1, 0 } : new[] { 0, 1, 2 })
                ships[i] = lab.Add(specs[i].Item1, specs[i].Item2, specs[i].Item3);
            lab.Step();
            Assert.Equal(2, lab.Events.Count);
            var p = Momentum(ships);
            Assert.Equal(0f, p.x, 1);
            Assert.Equal(0f, p.y, 1);
        }
    }

    // A docked hull rides its carrier's pose and velocity, so it is never a second body at the carrier's place: a
    // ship closing on the carrier makes one contact, with the carrier. (The visitor is the heavy hull: it keeps
    // closing after the first exchange, so a second contact with the docked hull would show.)
    [Fact]
    public void ADockedHullTakesNoPartInContact()
    {
        var lab = Build();
        var carrier = lab.Add("Solid", float3(0, 0, 0), float2(0, 0));
        var docked = lab.Add("Solid", float3(0, 0, 0), float2(0, 0));
        docked.SetParent(carrier);
        var visitor = lab.Add("Heavy", float3(10, 0, 0), float2(-10, 0));
        lab.Step();

        var contact = Assert.Single(lab.Events);
        Assert.Equal(new[] { carrier, visitor }, new[] { contact.A, contact.B });
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
