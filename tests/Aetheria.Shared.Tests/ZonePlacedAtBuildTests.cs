using System;
using System.Collections.Generic;
using System.IO;
using CultMath;
using GameCult.Caching;
using Xunit;
using static CultMath.math;
using float2 = CultMath.float2;
using float3 = CultMath.float3;

// Aetheria gravity, target invariant zone-valid-before-tick: a zone's orbits are placed when it is built, so
// its wells, lights and body poses are right before the first Update (follow-up
// aetheria-release:follow_up:zone-orbits-unplaced-before-first-tick). The zone here is the production
// constructor reading a pack; nothing places an orbit but that constructor and Zone.Update.
public sealed class ZonePlacedAtBuildTests : IDisposable
{
    private static readonly float2 RootAt = new float2(300f, -200f);
    private const float Distance = 400f;
    private const float Period = 40f;

    private readonly string _root = Path.Combine(Path.GetTempPath(), "aetheria-zoneplaced-" + Guid.NewGuid().ToString("N"));
    private readonly List<CultCache> _caches = new List<CultCache>();

    public ZonePlacedAtBuildTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        foreach (var cache in _caches) cache.Dispose();
        Directory.Delete(_root, true);
    }

    private static GameplaySettings GameSettings() => new GameplaySettings
    {
        DefaultEntitySettings = new EntitySettings(),
        Tiers = new[] { new RarityTier { Name = "Common", Quality = .5f, Rarity = 0, Color = new float3(1, 1, 1) } },
        QualityPriceModifier = new ExponentialLerp()
    };

    private static ExponentialCurve Flat(float v) => new ExponentialCurve { Constant = v };

    // Flat space (no zone depth) with one planet whose well reaches 500 u, a fixed root orbit at RootAt and the
    // planet orbiting it at Distance from `phase`. Every orbit period is Period seconds.
    private static PlanetSettings Settings() => new PlanetSettings
    {
        OrbitPeriod = Flat(Period),
        BodyRadius = Flat(20f),
        GravityRadius = Flat(500f),
        GravityDepth = Flat(100f),
        GravityStrength = 1f,
        WaveDepth = Flat(0f), WaveRadius = Flat(0f), WaveSpeed = Flat(0f), WaveFrequency = Flat(0f),
        LightRadius = Flat(0f)
    };

    private sealed class Scene
    {
        public CultCache Cache;
        public ItemManager Items;
        public HullData Hull;
        public Zone Zone;
        public CultRecordKey PlanetOrbit;
    }

    private Scene Build(float phase, double time, bool moonFirst = false)
    {
        var dir = Path.Combine(_root, $"zone{_caches.Count}");
        Directory.CreateDirectory(dir);
        var cache = AetheriaStores.Open(Path.Combine(dir, "Aetheria.cc"), Path.Combine(dir, "run.cc"), catalogWritable: true);
        _caches.Add(cache);
        cache.Upsert(new VerseGrammar { Revision = 1 });
        var shape = new Shape(5, 5);
        foreach (var cell in shape.AllCoordinates) shape[cell] = true;
        var hull = new HullData { Name = "Skiff", HullType = HullType.Ship, Shape = shape, Durability = 10, Mass = 1000 };
        cache.Upsert(hull);
        var items = new ItemManager(cache, new ProvenanceLedger(), GameSettings(), _ => { });

        var rootOrbit = cache.Upsert(new OrbitData { FixedPosition = RootAt }).Key;
        var planetOrbit = cache.Upsert(new OrbitData
        {
            Parent = new CultRecordRef<OrbitData>(rootOrbit), Distance = Distance, Phase = phase
        }).Key;
        var moonOrbit = cache.Upsert(new OrbitData
        {
            Parent = new CultRecordRef<OrbitData>(planetOrbit), Distance = 100f
        }).Key;
        var planet = cache.Upsert(new PlanetData { Orbit = new CultRecordRef<OrbitData>(planetOrbit), Mass = 1f }).Key;
        var pack = new ZonePack
        {
            Planets = { new CultRecordRef<BodyData>(planet) },
            Radius = 5000f,
            Mass = 10000f,
            Time = time
        };
        // The same three orbits either way; only the order the pack lists them in differs.
        var listed = moonFirst ? new[] { moonOrbit, planetOrbit, rootOrbit } : new[] { rootOrbit, planetOrbit, moonOrbit };
        foreach (var key in listed) pack.Orbits.Add(new CultRecordRef<OrbitData>(key));
        var zone = new Zone(items, Settings(), pack, new GalaxyZone { Name = "Placed", Owner = null }, null);
        return new Scene { Cache = cache, Items = items, Hull = hull, Zone = zone, PlanetOrbit = planetOrbit };
    }

    [Theory]
    [InlineData(.1f, 0.0)]
    [InlineData(.35f, 0.0)]
    [InlineData(.1f, 123.0)]
    [InlineData(.35f, 123.0)]
    public void ABodysWellSitsOnItsOrbitBeforeTheFirstTick(float phase, double time)
    {
        var s = Build(phase, time);
        var orbit = s.Zone.Orbits[s.PlanetOrbit];
        var turns = (float) frac(time / orbit.Period);
        var expected = RootAt + OrbitData.Evaluate(frac(turns + phase)) * Distance;

        Assert.InRange(length(orbit.Position - expected), 0f, .01f);

        // The well pulls toward the body's place: a point 30 u east of it is pushed west.
        var force = s.Zone.GetForce(expected + new float2(30f, 0f));
        Assert.True(length(force) > 0f, "the well exerts a force");
        Assert.InRange(normalize(force).x, -1f, -.99f);
    }

    // A parent's velocity is its own orbital velocity on the first tick and every tick after, whichever order
    // the pack lists a moon and its planet in. dt differs per tick so a velocity taken from a fixed step fails.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TheFirstTickVelocityIsTheOrbitsSpeed(bool moonFirst)
    {
        var s = Build(.35f, 0.0, moonFirst);
        var expected = 2f * PI * Distance / Period;
        foreach (var dt in new[] { 1f / 60f, 1f / 30f, 1f / 60f })
        {
            s.Zone.Update(dt);
            var speed = length(s.Zone.Orbits[s.PlanetOrbit].Velocity);
            Assert.InRange(speed, expected * .99f, expected * 1.01f);
        }
    }

    [Fact]
    public void AShipSteppedAloneFeelsTheBodyWhereItIs()
    {
        var s = Build(.1f, 0.0);
        var planetAt = s.Zone.Orbits[s.PlanetOrbit].Position;
        var ship = new Ship(s.Items, s.Zone, new EquippableItem
        {
            Data = s.Cache.RefOf<ItemData>(s.Hull),
            Durability = s.Hull.Durability,
            Lot = s.Items.Lots.Add(new Lot
            {
                Design = s.Cache.RefOf<ItemData>(s.Hull), Origin = new Attributed(), Quality = .5f, Roles = new List<RoleFill>()
            })
        }, new EntitySettings());
        s.Zone.Admit(ship, piloted: false);
        var at = planetAt + new float2(0f, 60f);
        ship.Position = float3(at.x, ship.Position.y, at.y);
        ship.Velocity = float2.zero;

        ship.Update(1f / 60f); // the ship alone: no Zone.Update has run

        Assert.True(length(ship.Velocity) > 0f, "the ship is pulled");
        var toward = normalize(planetAt - at);
        Assert.True(dot(normalize(ship.Velocity), toward) > .99f, "the pull is toward the planet, not toward the origin");
    }
}
