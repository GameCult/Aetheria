using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CultMath;
using GameCult.Caching;
using Xunit;
using static CultMath.math;
using float2 = CultMath.float2;
using float3 = CultMath.float3;

// Aetheria gravity, orbits-from-field: every rail's period is derived from the field it orbits in. Round a body it is
// that body's own well; round a bodyless node it is the ring law (the strength inside the rail, plus half of what shares
// it). The zones here are hand-built packs read by the production constructor, with the shipped well curves
// (c = 45.08 m^.2, R = 7.32 m^.25). Each expected period is spelled here from the field law outside the core, T = 2 pi d / sqrt(c),
// with c and R read from the zone's own bodies and every distance outside the core.
public sealed class OrbitsFromFieldTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "aetheria-orbitsfield-" + Guid.NewGuid().ToString("N"));
    private readonly List<CultCache> _caches = new List<CultCache>();

    public OrbitsFromFieldTests() => Directory.CreateDirectory(_root);

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

    private static PlanetSettings Curves() => new PlanetSettings
    {
        WellStrength = new ExponentialCurve { Exponent = .2f, Multiplier = 45.08f },
        RocheLimit = new ExponentialCurve { Exponent = .25f, Multiplier = 7.32f },
        BodyRadius = new ExponentialCurve { Exponent = .25f, Multiplier = 3f },
        LightRadius = new ExponentialCurve()
    };

    private sealed class World
    {
        public CultCache Cache;
        public ItemManager Items;
        public ZonePack Pack = new ZonePack { Radius = 20000f, Mass = 10000f };

        public CultRecordKey Orbit(CultRecordKey parent, float distance, float phase = 0f)
        {
            var data = new OrbitData { Distance = distance, Phase = phase };
            if (parent.IsSet()) data.Parent = new CultRecordRef<OrbitData>(parent);
            var key = Cache.Upsert(data).Key;
            Pack.Orbits.Add(new CultRecordRef<OrbitData>(key));
            return key;
        }

        public CultRecordKey Body(CultRecordKey orbit, float mass)
        {
            var key = Cache.Upsert(new PlanetData { Name = "Body", Orbit = new CultRecordRef<OrbitData>(orbit), Mass = mass }).Key;
            Pack.Planets.Add(new CultRecordRef<BodyData>(key));
            return key;
        }

        public Zone Build() => new Zone(Items, Curves(), Pack, new GalaxyZone { Name = "OrbitsFromField", Owner = null }, null);
    }

    private World NewWorld()
    {
        var dir = Path.Combine(_root, $"world{_caches.Count}");
        Directory.CreateDirectory(dir);
        var cache = AetheriaStores.Open(Path.Combine(dir, "Aetheria.cc"), Path.Combine(dir, "run.cc"), catalogWritable: true);
        _caches.Add(cache);
        return new World { Cache = cache, Items = new ItemManager(cache, new ProvenanceLedger(), GameSettings(), _ => { }) };
    }

    private static float C(Zone zone, CultRecordKey body) => zone.PlanetInstances[body].WellStrength;
    private static float Core(Zone zone, CultRecordKey body) => zone.PlanetInstances[body].RocheLimit;
    private static float Outside(float distance, float c) => 2 * PI * distance / sqrt(c);

    private static void AssertPeriod(float expected, float actual, string what) =>
        Assert.True(Math.Abs(actual / expected - 1f) <= 1e-4f, $"{what}: expected {expected}, got {actual}");

    [Fact]
    public void AMoonsPeriodIsItsPlanetsFieldAlone()
    {
        var w = NewWorld();
        var sunOrbit = w.Orbit(default, 0f);
        var sun = w.Body(sunOrbit, 5000f);
        var planetOrbit = w.Orbit(sunOrbit, 3000f);
        var planet = w.Body(planetOrbit, 100f);
        var moonOrbit = w.Orbit(planetOrbit, 150f);
        w.Body(moonOrbit, 1f);
        var zone = w.Build();

        Assert.True(Core(zone, planet) < 150f && Core(zone, sun) < 3000f, "fixture: both rails run outside the core");
        AssertPeriod(Outside(150f, C(zone, planet)), zone.Orbits[moonOrbit].Period, "the moon, in its planet's field");
        AssertPeriod(Outside(3000f, C(zone, sun)), zone.Orbits[planetOrbit].Period, "the planet, in its sun's field");
    }

    [Fact]
    public void AChildInsideItsParentsCoreOrbitsAsASolidBody()
    {
        var w = NewWorld();
        var sunOrbit = w.Orbit(default, 0f);
        w.Body(sunOrbit, 5000f);
        var planetOrbit = w.Orbit(sunOrbit, 3000f);
        var planet = w.Body(planetOrbit, 100f);
        var coreRadius = Curves().RocheLimit.Evaluate(100f);
        var near = w.Orbit(planetOrbit, coreRadius * .3f);
        var far = w.Orbit(planetOrbit, coreRadius * .7f);
        var zone = w.Build();

        var solid = 2 * PI * coreRadius / sqrt(C(zone, planet)); // a = c d / R^2, so T = 2 pi R / sqrt(c) at every d inside
        AssertPeriod(solid, zone.Orbits[near].Period, "the inner child");
        AssertPeriod(solid, zone.Orbits[far].Period, "the outer child");
    }

    [Fact]
    public void AnInnerSiblingDoesNotSpeedABodysRail()
    {
        var w = NewWorld();
        var sunOrbit = w.Orbit(default, 0f);
        var sun = w.Body(sunOrbit, 5000f);
        var innerOrbit = w.Orbit(sunOrbit, 800f);
        w.Body(innerOrbit, 20000f);
        var outerOrbit = w.Orbit(sunOrbit, 3000f);
        w.Body(outerOrbit, 100f);
        var zone = w.Build();

        AssertPeriod(Outside(3000f, C(zone, sun)), zone.Orbits[outerOrbit].Period, "the outer planet, with a heavy planet inside it");
    }

    [Fact]
    public void ABinaryOrbitsUnderHalfItsPartnersPull()
    {
        var w = NewWorld();
        var node = w.Orbit(default, 0f);
        var a = w.Orbit(node, 400f, 0f);
        var heavy = w.Orbit(node, 400f, .5f);
        var bodyA = w.Body(a, 100f);
        var bodyHeavy = w.Body(heavy, 2000f);
        var zone = w.Build();

        Assert.True(C(zone, bodyHeavy) > 1.5f * C(zone, bodyA), "fixture: unequal members");
        AssertPeriod(Outside(400f, C(zone, bodyHeavy) / 2), zone.Orbits[a].Period, "the light member");
        AssertPeriod(Outside(400f, C(zone, bodyA) / 2), zone.Orbits[heavy].Period, "the heavy member");
    }

    [Fact]
    public void AnOuterBodyFeelsTheWholePairAndItsMoons()
    {
        var w = NewWorld();
        var node = w.Orbit(default, 0f);
        var m1 = w.Orbit(node, 400f, 0f);
        var m2 = w.Orbit(node, 400f, .5f);
        var moon = w.Orbit(m1, 40f);
        var body1 = w.Body(m1, 100f);
        var body2 = w.Body(m2, 300f);
        var moonBody = w.Body(moon, 5f);
        var outer = w.Orbit(node, 3000f);
        w.Body(outer, 50f);
        var zone = w.Build();

        var whole = C(zone, body1) + C(zone, moonBody) + C(zone, body2);
        AssertPeriod(Outside(3000f, whole), zone.Orbits[outer].Period, "the outer body, round the pair and the first one's moon");
    }

    [Fact]
    public void ACompanionKeepsItsBodysPeriod()
    {
        // Under a body: a bodyless orbit at the planet's distance round the same sun.
        var w = NewWorld();
        var sunOrbit = w.Orbit(default, 0f);
        w.Body(sunOrbit, 5000f);
        var planetOrbit = w.Orbit(sunOrbit, 2000f, 0f);
        w.Body(planetOrbit, 100f);
        var companion = w.Orbit(sunOrbit, 2000f, 1f / 6f);
        var zone = w.Build();
        Assert.True(float.IsFinite(zone.Orbits[planetOrbit].Period));
        Assert.Equal(zone.Orbits[planetOrbit].Period, zone.Orbits[companion].Period);

        // Under a bodyless node: the same, round a node whose only field is an inner member's.
        w = NewWorld();
        var node = w.Orbit(default, 0f);
        var innerOrbit = w.Orbit(node, 100f);
        var inner = w.Body(innerOrbit, 3000f);
        var planet2 = w.Orbit(node, 500f, 0f);
        w.Body(planet2, 100f);
        var companion2 = w.Orbit(node, 500f, 1f / 6f);
        zone = w.Build();
        AssertPeriod(Outside(500f, C(zone, inner)), zone.Orbits[planet2].Period, "the planet");
        Assert.Equal(zone.Orbits[planet2].Period, zone.Orbits[companion2].Period);
    }

    [Fact]
    public void AnOrbitWithNoFieldStandsStill()
    {
        var w = NewWorld();
        var node = w.Orbit(default, 0f);
        var lonely = w.Orbit(node, 300f, .25f);
        var fixedOne = w.Orbit(default, 50f, .25f);
        var zone = w.Build();

        Assert.True(float.IsPositiveInfinity(zone.Orbits[lonely].Period), "no field to circle in");
        Assert.True(float.IsPositiveInfinity(zone.Orbits[fixedOne].Period), "a parentless orbit has nothing to circle");
        var before = (zone.Orbits[lonely].Position, zone.Orbits[fixedOne].Position);
        foreach (var dt in new[] { .5f, 3f, 70f, 5000f })
            zone.Update(dt);
        Assert.Equal(before, (zone.Orbits[lonely].Position, zone.Orbits[fixedOne].Position));
        Assert.True(length(zone.Orbits[lonely].Velocity) == 0f && length(zone.Orbits[fixedOne].Velocity) == 0f);
    }

    // Generated zones, by the production generator: every orbit with a parent has a finite period and moves against its
    // parent across an Update, and the seeds held a bodyless node with two members (a root rosette) and a pair on one ring.
    [Fact]
    public void EveryGeneratedOrbitMoves()
    {
        var rosettes = 0;
        var binaries = 0;
        for (uint seed = 1; seed <= 12; seed++)
        {
            var dir = Path.Combine(_root, $"galaxy{seed}");
            Directory.CreateDirectory(dir);
            var catalog = Path.Combine(dir, "Aetheria.cc");
            File.Copy(TestCatalog.Repo, catalog);
            var cache = new CultCache(TestCatalog.Registry());
            _caches.Add(cache);
            cache.AddBackingStore(new GameCult.Caching.MessagePack.SingleFileMessagePackBackingStore(catalog), AetheriaStores.CatalogTypes);
            cache.AddBackingStore(new GameCult.Caching.MessagePack.SingleFileMessagePackBackingStore(Path.Combine(dir, "run.cc")), AetheriaStores.RunTypes);
            var authored = AuthoredSettings.Load(RunStartTests.FindRepoRoot());
            var tutorial = authored.Read<TutorialGenerationSettings>("TutorialGenerationSettings");
            var galaxy = new Galaxy(tutorial, authored.Read<SectorBackgroundSettings>("TutorialBackgroundSettings"),
                authored.Read<NameGeneratorSettings>("NameGeneratorSettings"), cache, new PlayerSettings(),
                Directory.CreateDirectory(Path.Combine(dir, "Narrative")), _ => { }, null, seed);
            var items = new ItemManager(cache, new ProvenanceLedger(), authored.Read<GameplaySettings>("GameplaySettings"), _ => { });
            var pack = ZoneGenerator.GenerateZone(items, authored.Read<ZoneGenerationSettings>("ZoneSettings"), galaxy, galaxy.Entrance,
                galaxy.IsPrelude, false);
            var zone = new Zone(items, authored.Read<PlanetSettings>("PlanetSettings"), pack, galaxy.Entrance, galaxy);

            var hosting = zone.Planets.Values.Select(body => body.Orbit.Key).ToHashSet();
            foreach (var key in zone.Orbits.Keys.Where(k => !hosting.Contains(k)))
            {
                var members = zone.Orbits.Where(o => o.Value.Data.Parent.IsSet() && o.Value.Data.Parent.Key.Equals(key) && hosting.Contains(o.Key)).ToList();
                if (members.Count >= 2) rosettes++;
                if (members.Any(a => members.Any(b => !a.Key.Equals(b.Key) && Math.Abs(a.Value.Data.Distance - b.Value.Data.Distance) < .1f))) binaries++;
            }

            zone.Update(.02f);
            var before = zone.Orbits.ToDictionary(o => o.Key, o => o.Value.Position - ParentAt(zone, o.Value));
            zone.Update(.5f);
            foreach (var orbit in zone.Orbits.Where(o => o.Value.Data.Parent.IsSet()))
            {
                Assert.True(float.IsFinite(orbit.Value.Period), $"seed {seed}: an orbit with a parent has no finite period");
                Assert.True(lengthsq(orbit.Value.Position - ParentAt(zone, orbit.Value) - before[orbit.Key]) > 0f,
                    $"seed {seed}: an orbit did not move against its parent (period {orbit.Value.Period}, distance {orbit.Value.Data.Distance})");
            }
        }
        Assert.True(rosettes > 0, "the seeds hold a bodyless node with two bodied members");
        Assert.True(binaries > 0, "the seeds hold two bodied orbits on one ring");
    }

    private static float2 ParentAt(Zone zone, Orbit orbit) =>
        orbit.Data.Parent.IsSet() ? zone.Orbits[orbit.Data.Parent.Key].Position : orbit.Data.FixedPosition;
}
