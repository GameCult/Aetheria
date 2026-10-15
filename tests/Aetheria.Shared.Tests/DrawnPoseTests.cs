/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

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

// Every drawn pose is evaluated at one drawn time, T = Zone.ExactTime + Clock.Lead (DrawAhead.Time). The operator's report:
// coasting beside a planet and looking at it, the planet jumped back and forth every frame, because the planet was drawn at
// the last step's orbit while the ship was drawn a lead ahead of its last step. Each rail below is spelled here from the
// field law outside the core (a = c / d, so T = 2 pi d / sqrt(c)), never by calling the production rail; the zone is built
// by the production constructor and stepped by Zone.Update, and the clock is the production SimClock.
public sealed class DrawnPoseTests : IDisposable
{
    // Well strength is the body's mass (c = mass), with a small constant core, so every period is spelled from masses
    // chosen here. The sun's c = 400 puts its planet's rail speed at sqrt(c) = 20 u/s at any distance.
    private const float SunMass = 400f, PlanetMass = 25f, MoonMass = 4f;
    private const float PlanetDistance = 1000f, PlanetPhase = .3f;
    private const float MoonDistance = 60f, MoonPhase = .1f;
    private const float StationDistance = 3f, StationPhase = .4f;
    private const float GridOffset = 2.5f;

    private readonly string _root = Path.Combine(Path.GetTempPath(), "aetheria-drawnpose-" + Guid.NewGuid().ToString("N"));
    private readonly List<CultCache> _caches = new List<CultCache>();

    public DrawnPoseTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        foreach (var cache in _caches) cache.Dispose();
        Directory.Delete(_root, true);
    }

    private sealed class World
    {
        public Zone Zone;
        public ItemManager Items;
        public EquippableItem Hull;
        public CultRecordKey PlanetOrbit, MoonOrbit, StationOrbit, Belt;
        public Asteroid[] Asteroids;
        public float HeightExaggeration => Zone.Settings.HeightExaggeration;
    }

    private static PlanetSettings Curves() => new PlanetSettings
    {
        BodyRadius = new ExponentialCurve(),
        WellStrength = new ExponentialCurve { Multiplier = 1f, Exponent = 1f },
        RocheLimit = new ExponentialCurve { Constant = 2f },
        AsteroidSize = new ExponentialLerp { Minimum = 2f, Maximum = 9f, Exponent = 1.7f },
        AsteroidHitpoints = new ExponentialLerp { Minimum = 10f, Maximum = 300f, Exponent = 2f },
        AsteroidRespawnTime = new ExponentialLerp { Minimum = 5f, Maximum = 60f, Exponent = 1f }
    };

    // A sun on a parentless orbit, a planet round it, a moon round the planet, a bodyless station rail close round the planet,
    // and a belt round the planet; a hull to ride the field with.
    private World Build()
    {
        var dir = Path.Combine(_root, $"world{_caches.Count}");
        Directory.CreateDirectory(dir);
        var catalog = Path.Combine(dir, "Aetheria.cc");
        var shape = new Shape(3, 3);
        foreach (var cell in shape.AllCoordinates) shape[cell] = true;
        using (var build = AetheriaStores.Open(catalog, catalogWritable: true))
        {
            build.Upsert(new VerseGrammar { Revision = 1 });
            build.Upsert(new HullData { Name = "TestHull", HullType = HullType.Ship, Shape = shape, Durability = 1, Mass = 1000, GridOffset = GridOffset });
            build.FlushAsync().Wait();
        }

        var cache = AetheriaStores.Open(catalog, Path.Combine(dir, "run.cc"), catalogWritable: true);
        _caches.Add(cache);
        var ledger = new ProvenanceLedger();
        ledger.Lots[1] = new Lot { Origin = new Attributed(), Quality = 1 };
        var items = new ItemManager(cache, ledger, new GameplaySettings
        {
            DefaultEntitySettings = new EntitySettings(),
            Tiers = new[] { new RarityTier { Name = "Common", Quality = .5f, Rarity = 0, Color = new float3(1, 1, 1) } },
            QualityPriceModifier = new ExponentialLerp()
        }, _ => { });

        CultRecordKey Orbit(CultRecordKey parent, float distance, float phase)
        {
            var data = new OrbitData { Distance = distance, Phase = phase };
            if (parent.IsSet()) data.Parent = new CultRecordRef<OrbitData>(parent);
            return cache.Upsert(data).Key;
        }

        CultRecordKey Body(CultRecordKey orbit, string name, float mass) =>
            cache.Upsert(new PlanetData { Name = name, Orbit = new CultRecordRef<OrbitData>(orbit), Mass = mass }).Key;

        var sunOrbit = Orbit(default, 0f, 0f);
        var planetOrbit = Orbit(sunOrbit, PlanetDistance, PlanetPhase);
        var moonOrbit = Orbit(planetOrbit, MoonDistance, MoonPhase);
        var stationOrbit = Orbit(planetOrbit, StationDistance, StationPhase);
        var beltOrbit = Orbit(planetOrbit, 400f, .42f);
        var asteroids = Enumerable.Range(0, 6).Select(i => new Asteroid
        {
            Distance = 120f + i * 11.3f, Phase = frac(i * .6180339887f), Size = frac(i * .37f + .1f), RotationSpeed = .15f + i * .037f
        }).ToArray();
        var belt = cache.Upsert(new AsteroidBeltData { Orbit = new CultRecordRef<OrbitData>(beltOrbit), Asteroids = asteroids }).Key;

        var pack = new ZonePack { Radius = 5000f, Mass = 10000f, Time = 0 };
        foreach (var orbit in new[] { sunOrbit, planetOrbit, moonOrbit, stationOrbit, beltOrbit })
            pack.Orbits.Add(new CultRecordRef<OrbitData>(orbit));
        foreach (var body in new[] { Body(sunOrbit, "Sun", SunMass), Body(planetOrbit, "Planet", PlanetMass), Body(moonOrbit, "Moon", MoonMass), belt })
            pack.Planets.Add(new CultRecordRef<BodyData>(body));

        var zone = new Zone(items, Curves(), pack, new GalaxyZone { Name = "DrawnPose", Owner = null }, null);
        var hull = new EquippableItem { Data = items.ItemData.RefOf<ItemData>(items.ItemData.GetByName<HullData>("TestHull")), Durability = 1, Lot = 1 };
        return new World { Zone = zone, Items = items, Hull = hull, PlanetOrbit = planetOrbit, MoonOrbit = moonOrbit, StationOrbit = stationOrbit, Belt = belt, Asteroids = asteroids };
    }

    // ---- The rails, spelled from the field law ----

    private static float2 Around(float2 center, double time, float distance, float phase, float strength)
    {
        var period = 2 * PI * distance / sqrt(strength);
        var wrapped = (float) frac(time / period);
        return center + OrbitData.Evaluate(frac(wrapped + phase)) * distance;
    }

    private static float2 PlanetAt(double time) => Around(float2.zero, time, PlanetDistance, PlanetPhase, SunMass);
    private static float2 MoonAt(double time) => Around(PlanetAt(time), time, MoonDistance, MoonPhase, PlanetMass);
    private static float2 StationAt(double time) => Around(PlanetAt(time), time, StationDistance, StationPhase, PlanetMass);

    // The field at a point at a time, with each well where its rail puts it then.
    private static float FieldAt(World w, float2 point, double time)
    {
        var wells = new (float2 At, float Strength)[] { (float2.zero, SunMass), (PlanetAt(time), PlanetMass), (MoonAt(time), MoonMass) };
        var sum = 0f;
        foreach (var (at, strength) in wells)
            sum += GravityField.Potential(strength, 2f, length(point - at)) - GravityField.Potential(strength, 2f, w.Zone.Pack.Radius);
        return sum / w.HeightExaggeration;
    }

    private static void Step(World w, float seconds, int times)
    {
        for (var i = 0; i < times; i++) w.Zone.Update(seconds);
    }

    // ---- The tests ----

    // The operator's report, from the production clock: a ship coasting at its planet's step velocity, 60 u beside it, seen
    // across display frames. Frames are 1/144 s (the report), and 75, 165 and 240 Hz, where a clamp or a fraction of the
    // lead would also show; at 60 Hz Lead is always 0 and could not tell. The planet minus the ship must not swing in either
    // world axis (0.306 u along the motion when the planet is drawn at the step's orbit).
    [Theory]
    [InlineData(144)]
    [InlineData(75)]
    [InlineData(165)]
    [InlineData(240)]
    public void ACoMovingShipSeesAStillPlanetAcrossFrames(int displayHz)
    {
        var w = Build();
        var ship = new Ship(w.Items, w.Zone, w.Hull, new EntitySettings());
        var clock = new SimClock(1f / 60f);
        var xs = new List<float>();
        var ys = new List<float>();
        var leads = new List<float>();

        for (var frame = 0; frame < displayHz * 2; frame++)
        {
            clock.Advance(1f / displayHz, dt =>
            {
                w.Zone.Update(dt);
                var rail = w.Zone.Orbits[w.PlanetOrbit];
                ship.Position = float3(rail.Position.x + 60f, 0, rail.Position.y);
                ship.Velocity = rail.Velocity;
            });
            if (clock.Steps == 0) continue;
            var lead = clock.Lead;
            var relative = DrawAhead.Orbit(w.Zone, w.PlanetOrbit, lead) - DrawAhead.Position(ship, lead).xz;
            xs.Add(relative.x);
            ys.Add(relative.y);
            leads.Add(lead);
        }

        Assert.True(xs.Count > displayHz, "the run covered its frames");
        Assert.True(leads.Max() > .005f, $"{displayHz} Hz: the frames fall far enough past a step to tell a lead from none");
        Assert.True(xs.Max() - xs.Min() < .005f, $"{displayHz} Hz: the planet swings {xs.Max() - xs.Min():F4} u in x against the ship");
        Assert.True(ys.Max() - ys.Min() < .005f, $"{displayHz} Hz: the planet swings {ys.Max() - ys.Min():F4} u in z against the ship");
    }

    [Fact]
    public void APlanetIsDrawnAtItsOrbitAtTheDrawnTime()
    {
        var w = Build();
        Step(w, 1f / 60f, 803);
        foreach (var lead in new[] { 0f, .004f, .0123f, .0166f })
        {
            var time = w.Zone.ExactTime + lead;
            AssertNear(PlanetAt(time), DrawAhead.Orbit(w.Zone, w.PlanetOrbit, lead), 1e-3f, $"planet at lead {lead}");
            AssertNear(MoonAt(time), DrawAhead.Orbit(w.Zone, w.MoonOrbit, lead), 1e-3f, $"moon (chained through the planet) at lead {lead}");
        }

        Assert.Equal(w.Zone.Orbits[w.PlanetOrbit].Position, DrawAhead.Orbit(w.Zone, w.PlanetOrbit, 0f));
        Assert.Equal(w.Zone.Orbits[w.MoonOrbit].Position, DrawAhead.Orbit(w.Zone, w.MoonOrbit, 0f));
        Assert.True(length(DrawAhead.Orbit(w.Zone, w.PlanetOrbit, .0166f) - w.Zone.Orbits[w.PlanetOrbit].Position) > .1f, "the planet really moves over a lead");
    }

    [Fact]
    public void OrbitPositionAtWritesNothing()
    {
        var w = Build();
        Step(w, 1f / 60f, 100);
        var keys = w.Zone.Orbits.Keys.ToArray();
        var positions = keys.Select(k => w.Zone.Orbits[k].Position).ToArray();
        var velocities = keys.Select(k => w.Zone.Orbits[k].Velocity).ToArray();
        var probes = new[] { float2(1100f, 20f), float2(980f, -40f), float2.zero };
        var heights = probes.Select(p => w.Zone.GetHeight(p)).ToArray();

        var later = w.Zone.ExactTime + .5;
        foreach (var key in keys) w.Zone.OrbitPositionAt(key, later);
        foreach (var p in probes) w.Zone.GetHeight(p, later);
        w.Zone.ChunkPose(w.Belt, 0, later);

        for (var i = 0; i < keys.Length; i++)
        {
            Assert.Equal(positions[i], w.Zone.Orbits[keys[i]].Position);
            Assert.Equal(velocities[i], w.Zone.Orbits[keys[i]].Velocity);
            Assert.Equal(positions[i], w.Zone.OrbitPositionAt(keys[i], w.Zone.ExactTime));
        }

        for (var i = 0; i < probes.Length; i++) Assert.Equal(heights[i], w.Zone.GetHeight(probes[i]));
        Assert.True(length(w.Zone.OrbitPositionAt(w.PlanetOrbit, later) - positions[Array.IndexOf(keys, w.PlanetOrbit)]) > 1f, "the later time is a different place");
    }

    [Fact]
    public void TheDrawnFieldMovesWithTheDrawnWell()
    {
        var w = Build();
        Step(w, 1f / 60f, 500);
        var probe = PlanetAt(w.Zone.ExactTime) + float2(30f, 0);
        Assert.Equal(w.Zone.GetHeight(probe), w.Zone.GetHeight(probe, w.Zone.ExactTime));

        const float lead = .0166f;
        var time = w.Zone.ExactTime + lead;
        var drawn = DrawAhead.Orbit(w.Zone, w.PlanetOrbit, lead) + float2(30f, 0);
        Assert.Equal(FieldAt(w, drawn, time), DrawAhead.Height(w.Zone, drawn, lead), 3);
        Assert.True(abs(DrawAhead.Height(w.Zone, drawn, lead) - w.Zone.GetHeight(drawn)) > .01f, "the field has moved with its well over a lead");

        // A ship that stands still 40 u from the planet rides the field of the drawn well, plus its hull's offset.
        var ship = new Ship(w.Items, w.Zone, w.Hull, new EntitySettings()) { Position = float3(PlanetAt(w.Zone.ExactTime).x + 40f, 0, PlanetAt(w.Zone.ExactTime).y) };
        var drawnShip = DrawAhead.Position(ship, lead);
        Assert.Equal(FieldAt(w, drawnShip.xz, time) + GridOffset, drawnShip.y, 3);
        Assert.Equal(ship.RideHeight(drawnShip.xz, time), drawnShip.y, 4);
        Assert.Equal(w.Zone.GetHeight(ship.Position.xz) + GridOffset, ship.RideHeight(ship.Position.xz), 4);
    }

    [Fact]
    public void AStationIsDrawnOnItsOrbit()
    {
        var w = Build();
        const float step = .1f;
        Step(w, step, 40);
        var station = new OrbitalEntity(w.Items, w.Zone, w.Hull, w.StationOrbit, new EntitySettings());
        var rail = w.Zone.Orbits[w.StationOrbit];
        station.Position = float3(rail.Position.x, 0, rail.Position.y);
        station.Velocity = rail.Velocity;
        const float lead = .08f;

        var drawn = DrawAhead.Position(station, lead);

        AssertNear(StationAt(w.Zone.ExactTime + lead), drawn.xz, 2e-3f, "the station on its rail at the drawn time");
        AssertNear(DrawAhead.Orbit(w.Zone, w.StationOrbit, lead), drawn.xz, 1e-6f, "the same point DrawAhead.Orbit gives");
        Assert.True(length(station.Position.xz + station.Velocity * lead - drawn.xz) > .01f, "a tight rail curves away from its straight extrapolation");
        Assert.Equal(w.Zone.GetHeight(drawn.xz, w.Zone.ExactTime + lead) + GridOffset, drawn.y, 4);
    }

    [Fact]
    public void ABeltIsDrawnAtTheDrawnTime()
    {
        var w = Build();
        Step(w, 1f / 60f, 301);
        var time = w.Zone.ExactTime + .01;
        var atStep = new float4[w.Asteroids.Length];
        var drawn = new float4[w.Asteroids.Length];
        w.Zone.EvaluateBelt(w.Belt, atStep, w.Zone.ExactTime);
        w.Zone.EvaluateBelt(w.Belt, drawn, time);

        var parent = PlanetAt(time);
        for (var i = 0; i < w.Asteroids.Length; i++)
        {
            var asteroid = w.Asteroids[i];
            var period = 2 * PI * asteroid.Distance / sqrt(PlanetMass);
            var expected = OrbitData.Evaluate((float) frac(time / period + asteroid.Phase)) * asteroid.Distance + parent;
            AssertNear(expected, drawn[i].xy, 1e-3f, $"chunk {i} drawn");
            Assert.Equal(w.Zone.ChunkPose(w.Belt, i, time), drawn[i]);
            Assert.Equal(w.Zone.ChunkPose(w.Belt, i, w.Zone.ExactTime), atStep[i]);
            Assert.Equal(w.Zone.ChunkPose(w.Belt, i), atStep[i]);
            Assert.True(length(drawn[i].xy - atStep[i].xy) > .01f, $"chunk {i} has moved over the lead");
        }
    }

    private static void AssertNear(float2 expected, float2 actual, float eps, string what) =>
        Assert.True(length(expected - actual) <= eps, $"{what}: expected {expected}, got {actual} (eps {eps})");
}
