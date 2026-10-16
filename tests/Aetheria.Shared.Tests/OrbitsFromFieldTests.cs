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

// Aetheria gravity, orbits-from-field: every rail's period is derived from the field it orbits in (WellTree.PullAround).
// Round a body that is the body's own pull inside its outermost child; round a bodyless node a ring member also feels each
// other member's own pull at their chord, projected toward the node. The zones here are hand-built packs read by the
// production constructor, with the shipped well curves (c = 45.08 m^.2, eps = 7.32 m^.25). Each expected period is
// spelled here as T = 2 pi sqrt(d / a), a composed from GravityField.Pull and c and eps read from the zone's own bodies.
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

    // The pull of a body's well as one body, at distance d: c times the band term's pull with no band (GravityField.Pull).
    private static float Pull(float c, float roche, float d) => c * GravityField.Pull(roche, float.PositiveInfinity, float.PositiveInfinity, d);
    private static float Pull(Zone zone, CultRecordKey body, float d) => Pull(C(zone, body), Core(zone, body), d);

    // A mass the zone has no body of (a family's total): its well from the authored curves, as the law reads them.
    private static float FamilyPull(float mass, float d) => Pull(Curves().WellStrength.Evaluate(mass), Curves().RocheLimit.Evaluate(mass), d);

    private static float PeriodUnder(float pull, float distance) => 2 * PI * sqrt(distance / pull);

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

        AssertPeriod(PeriodUnder(Pull(zone, planet, 150f), 150f), zone.Orbits[moonOrbit].Period, "the moon, in its planet's field");
        AssertPeriod(PeriodUnder(Pull(zone, sun, 3000f), 3000f), zone.Orbits[planetOrbit].Period, "the planet, in its sun's field");
    }

    [Fact]
    public void AChildInsideItsParentsRocheLimitOrbitsInThePlummerPull()
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

        AssertPeriod(PeriodUnder(Pull(zone, planet, coreRadius * .3f), coreRadius * .3f), zone.Orbits[near].Period, "the inner child");
        AssertPeriod(PeriodUnder(Pull(zone, planet, coreRadius * .7f), coreRadius * .7f), zone.Orbits[far].Period, "the outer child");
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

        AssertPeriod(PeriodUnder(Pull(zone, sun, 3000f), 3000f), zone.Orbits[outerOrbit].Period, "the outer planet, with a heavy planet inside it");
    }

    // A binary on a bodyless node: a member feels its partner's own pull at their chord (twice the ring's radius), toward
    // the node, and nothing of the node itself.
    [Fact]
    public void ABinaryMemberFeelsItsPartnerAtTheChord()
    {
        var w = NewWorld();
        var node = w.Orbit(default, 0f);
        var a = w.Orbit(node, 400f, 0f);
        var heavy = w.Orbit(node, 400f, .5f);
        var bodyA = w.Body(a, 100f);
        var bodyHeavy = w.Body(heavy, 2000f);
        var zone = w.Build();

        Assert.True(C(zone, bodyHeavy) > 1.5f * C(zone, bodyA), "fixture: unequal members");
        AssertPeriod(PeriodUnder(Pull(zone, bodyHeavy, 800f), 400f), zone.Orbits[a].Period, "the light member");
        AssertPeriod(PeriodUnder(Pull(zone, bodyA, 800f), 400f), zone.Orbits[heavy].Period, "the heavy member");
    }

    // Conservation: the outer body feels one well of the whole mass inside it, c(M_pair + moon), not the sum of strengths.
    [Fact]
    public void AnOuterBodyFeelsTheWholePairAndItsMoons()
    {
        var w = NewWorld();
        var node = w.Orbit(default, 0f);
        var m1 = w.Orbit(node, 400f, 0f);
        var m2 = w.Orbit(node, 400f, .5f);
        var moon = w.Orbit(m1, 40f);
        w.Body(m1, 100f);
        w.Body(m2, 300f);
        w.Body(moon, 5f);
        var outer = w.Orbit(node, 3000f);
        w.Body(outer, 50f);
        var zone = w.Build();

        AssertPeriod(PeriodUnder(FamilyPull(405f, 3000f), 3000f), zone.Orbits[outer].Period, "the outer body, round the pair and the first one's moon");
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
        AssertPeriod(PeriodUnder(Pull(zone, inner, 500f), 500f), zone.Orbits[planet2].Period, "the planet");
        Assert.Equal(zone.Orbits[planet2].Period, zone.Orbits[companion2].Period);
    }

    // A station on a binary's ring, with nothing inside, feels what the pair's members feel (the mean of what each does),
    // and so does one a hair off the ring's distance but within its window.
    [Theory]
    [InlineData(0f)]
    [InlineData(.05f)]
    [InlineData(-.05f)]
    public void ACompanionOnABinarysRingTakesTheMembersPeriod(float offset)
    {
        var w = NewWorld();
        var node = w.Orbit(default, 0f);
        var light = w.Orbit(node, 400f, 0f);
        var heavy = w.Orbit(node, 400f, .5f);
        var lightBody = w.Body(light, 100f);
        var heavyBody = w.Body(heavy, 2000f);
        var companion = w.Orbit(node, 400f + offset, .25f);
        var zone = w.Build();

        Assert.True(C(zone, heavyBody) > 1.5f * C(zone, lightBody), "fixture: unequal members");
        AssertPeriod(PeriodUnder((Pull(zone, lightBody, 800f) + Pull(zone, heavyBody, 800f)) / 2, 400f + offset), zone.Orbits[companion].Period,
            "the companion, between its two members");

        // An equal pair: the companion has the members' period.
        w = NewWorld();
        node = w.Orbit(default, 0f);
        var a = w.Orbit(node, 400f, 0f);
        var b = w.Orbit(node, 400f, .5f);
        w.Body(a, 300f);
        w.Body(b, 300f);
        companion = w.Orbit(node, 400f, .25f);
        zone = w.Build();
        Assert.True(float.IsFinite(zone.Orbits[a].Period));
        AssertPeriod(zone.Orbits[a].Period, zone.Orbits[companion].Period, "the companion, against the first member");
        AssertPeriod(zone.Orbits[b].Period, zone.Orbits[companion].Period, "the companion, against the second member");
    }

    // The ring is the 0.1 u window about the innermost massive distance: a member just inside it is a ring partner (felt at
    // the chord), one just outside it is not a partner and the rail has the node's own terms alone, which give a
    // ring's own member next to no pull.
    [Theory]
    [InlineData(-.09f, true)]
    [InlineData(.09f, true)]
    [InlineData(-.11f, false)]
    public void TheRingWindowIsATenthOfAUnit(float partnerOffset, bool onRing)
    {
        var w = NewWorld();
        var node = w.Orbit(default, 0f);
        var rail = w.Orbit(node, 400f, 0f);
        var partner = w.Orbit(node, 400f + partnerOffset, .5f);
        w.Body(rail, 100f);
        var partnerBody = w.Body(partner, 2000f);
        var zone = w.Build();

        if (onRing)
            AssertPeriod(PeriodUnder(Pull(zone, partnerBody, 800f + partnerOffset), 400f), zone.Orbits[rail].Period,
                "the rail, with a partner at " + partnerOffset);
        else
            Assert.True(zone.Orbits[rail].Period > 1e6f, "a body outside the window is no partner: the rail has no pull of any size");
    }

    [Fact]
    public void AMemberJustBeyondTheWindowIsOutsideTheRail()
    {
        var w = NewWorld();
        var node = w.Orbit(default, 0f);
        var rail = w.Orbit(node, 400f, 0f);
        var beyond = w.Orbit(node, 400.11f, .5f);
        w.Body(rail, 100f);
        w.Body(beyond, 2000f);
        var zone = w.Build();

        Assert.True(float.IsPositiveInfinity(zone.Orbits[rail].Period), "a body beyond the rail does not pull it");
    }

    // A bodyless node's children read only that node's own terms and their ring partners: the well of the body the node
    // itself orbits does not enter, and a lone body on the node has no pull.
    [Fact]
    public void ABodylessNodesChildrenNeverReadItsParentsWell()
    {
        var w = NewWorld();
        var sunOrbit = w.Orbit(default, 0f);
        w.Body(sunOrbit, 5000f);
        var node = w.Orbit(sunOrbit, 3000f);
        var alone = w.Orbit(node, 200f, 0f);
        w.Body(alone, 100f);
        var zone = w.Build();
        Assert.True(float.IsPositiveInfinity(zone.Orbits[alone].Period), "a lone body on a bodyless node has no ring partner and no field");

        w = NewWorld();
        sunOrbit = w.Orbit(default, 0f);
        w.Body(sunOrbit, 5000f);
        node = w.Orbit(sunOrbit, 3000f);
        var a = w.Orbit(node, 200f, 0f);
        var b = w.Orbit(node, 200f, .5f);
        var bodyA = w.Body(a, 100f);
        var bodyB = w.Body(b, 300f);
        zone = w.Build();
        AssertPeriod(PeriodUnder(Pull(zone, bodyB, 400f), 200f), zone.Orbits[a].Period, "the first member, under its partner alone");
        AssertPeriod(PeriodUnder(Pull(zone, bodyA, 400f), 200f), zone.Orbits[b].Period, "the second member, under its partner alone");
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
