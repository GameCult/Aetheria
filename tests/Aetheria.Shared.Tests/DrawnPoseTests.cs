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
// field law inside a node's outermost child (the node pulls as its body alone: a = c d / (d^2 + eps^2), T = 2 pi sqrt(d / a)),
// never by calling the production rail; the zone is built
// by the production constructor and stepped by Zone.Update, and the clock is the production SimClock.
public sealed class DrawnPoseTests : IDisposable
{
    // Well strength is the body's mass (c = mass), with a small constant core, so every period is spelled from masses
    // chosen here. The sun's c = 400 puts its planet's rail speed at sqrt(c) = 20 u/s at any distance.
    private const float SunMass = 400f, PlanetMass = 25f, MoonMass = 4f;
    private const float PlanetDistance = 1000f, PlanetPhase = .3f;
    private const float MoonDistance = 60f, MoonPhase = .1f;
    private const float StationDistance = 3f, StationPhase = .4f;
    private const float ShipLayerHeight = 2.5f;
    private const float OrbitalLayerHeight = 7.5f;
    private const float FloaterLayerHeight = -1.25f;
    // The sun's rail is parentless and off-centre, the shape ZoneGenerator gives a cluster root (FixedPosition = cluster centre).
    private static readonly float2 SunCentre = new float2(700f, -300f);

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
            build.Upsert(new HullData { Name = "TestHull", HullType = HullType.Ship, Shape = shape, Durability = 1, Mass = 1000 });
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
            QualityPriceModifier = new ExponentialLerp(),
            ShipLayerHeight = ShipLayerHeight, OrbitalLayerHeight = OrbitalLayerHeight, FloaterLayerHeight = FloaterLayerHeight
        }, _ => { });

        CultRecordKey Orbit(CultRecordKey parent, float distance, float phase, float2 fixedPosition = default)
        {
            var data = new OrbitData { Distance = distance, Phase = phase, FixedPosition = fixedPosition };
            if (parent.IsSet()) data.Parent = new CultRecordRef<OrbitData>(parent);
            return cache.Upsert(data).Key;
        }

        CultRecordKey Body(CultRecordKey orbit, string name, float mass) =>
            cache.Upsert(new PlanetData { Name = name, Orbit = new CultRecordRef<OrbitData>(orbit), Mass = mass }).Key;

        var sunOrbit = Orbit(default, 0f, 0f, SunCentre);
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

    // T = 2 pi sqrt(d / a), a the Plummer pull of a body alone (softening 2) at d.
    private static float PeriodAt(float distance, float strength) => 2 * PI * sqrt(distance / (strength * distance / (distance * distance + 4f)));

    private static float2 Around(float2 center, double time, float distance, float phase, float strength)
    {
        var period = PeriodAt(distance, strength);
        var wrapped = (float) frac(time / period);
        return center + OrbitData.Evaluate(frac(wrapped + phase)) * distance;
    }

    private static float2 PlanetAt(double time) => Around(SunCentre, time, PlanetDistance, PlanetPhase, SunMass);
    private static float2 MoonAt(double time) => Around(PlanetAt(time), time, MoonDistance, MoonPhase, PlanetMass);
    private static float2 StationAt(double time) => Around(PlanetAt(time), time, StationDistance, StationPhase, PlanetMass);

    // The field at a point at a time: the zone's terms (WellTree owns which exist), each at the position this test spells
    // for its node's rail at that time.
    private static float FieldAt(World w, float2 point, double time)
    {
        float2 At(CultRecordKey node) => node.Equals(w.PlanetOrbit) ? PlanetAt(time) : node.Equals(w.MoonOrbit) ? MoonAt(time) : SunCentre;
        var sum = 0f;
        foreach (var term in w.Zone.Wells.Terms)
            sum += term.Strength * GravityField.Potential(term.Softening, term.Inner, term.Outer, length(point - At(term.Node))) - term.Reference;
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

    // The chain rule (a root at its FixedPosition, a child at its parent's node plus its offset) holds at the step's time
    // (the memo) and at a drawn time (the rail) alike: an off-centre root is the same place either way, and the planet
    // round it follows it.
    [Fact]
    public void ARootSitsAtItsFixedPositionAtEveryTime()
    {
        var w = Build();
        var sunOrbit = w.Zone.Orbits.Single(o => !o.Value.Data.Parent.IsSet()).Key;
        Step(w, 1f / 60f, 200);
        foreach (var lead in new[] { 0f, .004f, .0166f })
        {
            AssertNear(SunCentre, DrawAhead.Orbit(w.Zone, sunOrbit, lead), 1e-3f, $"root at lead {lead}");
            AssertNear(PlanetAt(w.Zone.ExactTime + lead), DrawAhead.Orbit(w.Zone, w.PlanetOrbit, lead), 1e-3f, $"planet round the root at lead {lead}");
        }

        Assert.True(length(w.Zone.Orbits[w.PlanetOrbit].Position - SunCentre) > PlanetDistance - 1f, "the planet circles the off-centre root, not the origin");
    }

    [Fact]
    public void OrbitPositionAtWritesNothing()
    {
        var w = Build();
        Step(w, 1f / 60f, 100);
        var keys = w.Zone.Orbits.Keys.ToArray();
        var positions = keys.Select(k => w.Zone.Orbits[k].Position).ToArray();
        var velocities = keys.Select(k => w.Zone.Orbits[k].Velocity).ToArray();
        var probes = new[] { float2(1100f, 20f), float2(980f, -40f), SunCentre };
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
        Assert.Equal(FieldAt(w, drawnShip.xz, time) + ShipLayerHeight, drawnShip.y, 3);
        Assert.Equal(ship.RideHeight(drawnShip.xz, time), drawnShip.y, 4);
        Assert.Equal(w.Zone.GetHeight(ship.Position.xz) + ShipLayerHeight, ship.RideHeight(ship.Position.xz), 4);
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
        Assert.Equal(w.Zone.GetHeight(drawn.xz, w.Zone.ExactTime + lead) + OrbitalLayerHeight, drawn.y, 4);
    }

    // The generators make orbital entities with no orbit set (Loadout, LoadoutGenerator): a rail-less one is an ordinary
    // integrated body, drawn at its step plus velocity times lead, never at the origin an unset rail would give.
    [Fact]
    public void AnOrbitalEntityWithoutAnOrbitIsDrawnWhereItIs()
    {
        var w = Build();
        Step(w, 1f / 60f, 30);
        var station = new OrbitalEntity(w.Items, w.Zone, w.Hull, default, new EntitySettings())
        {
            Position = float3(520f, 0, -140f),
            Velocity = float2(3f, 4f)
        };
        const float lead = .01f;

        var drawn = DrawAhead.Position(station, lead);

        AssertNear(float2(520f, -140f) + float2(3f, 4f) * lead, drawn.xz, 1e-4f, "an unrailed station");
        Assert.Equal(station.RideHeight(drawn.xz, w.Zone.ExactTime + lead), drawn.y, 4);
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
            var period = PeriodAt(asteroid.Distance, PlanetMass);
            var expected = OrbitData.Evaluate((float) frac(time / period + asteroid.Phase)) * asteroid.Distance + parent;
            AssertNear(expected, drawn[i].xy, 1e-3f, $"chunk {i} drawn");
            Assert.Equal(w.Zone.ChunkPose(w.Belt, i, time), drawn[i]);
            Assert.Equal(w.Zone.ChunkPose(w.Belt, i, w.Zone.ExactTime), atStep[i]);
            Assert.Equal(w.Zone.ChunkPose(w.Belt, i), atStep[i]);
            Assert.True(length(drawn[i].xy - atStep[i].xy) > .01f, $"chunk {i} has moved over the lead");
        }
    }

    // The reticle (DrawAhead.Chunk) and the renderer's per-rock instance (DrawAhead.ChunkAt on EvaluateBelt's drawn pose)
    // are one height: the field at the rock's planar position at the drawn time, plus the floater layer, less its drawn radius, so its top
    // is at the layer. Rulings asteroids-on-floater-layer, asteroid-tops-at-layer.
    [Fact]
    public void AChunkIsDrawnWithItsTopOnTheFloaterLayerOverTheField()
    {
        var w = Build();
        Step(w, 1f / 60f, 301);
        const float lead = .01f;
        var time = w.Zone.ExactTime + lead;
        var drawn = new float4[w.Asteroids.Length];
        w.Zone.EvaluateBelt(w.Belt, drawn, time);
        var settings = w.Items.GameplaySettings;

        var deep = 0;
        for (var i = 0; i < w.Asteroids.Length; i++)
        {
            var reticle = DrawAhead.Chunk(w.Zone, settings, new ChunkId(w.Belt, i), lead);
            var field = FieldAt(w, drawn[i].xy, time);
            AssertNear(drawn[i].xy, reticle.xz, 1e-3f, $"chunk {i} reticle xz");
            var radius = w.Zone.ChunkRadius(new ChunkId(w.Belt, i));
            Assert.True(radius > 2f, $"chunk {i} has a radius that any scaling of it would move");
            Assert.Equal(radius, drawn[i].w, 4);
            Assert.Equal(field + FloaterLayerHeight - radius, reticle.y, 3);
            Assert.Equal(field + FloaterLayerHeight, reticle.y + radius, 3);
            Assert.Equal(reticle.y, DrawAhead.ChunkAt(w.Zone, settings, drawn[i], lead).y, 4);
            if (abs(field) > 1f) deep++;
        }
        Assert.True(deep > 0, "some rock sits where the field is more than a unit from zero, so a dropped field fails");
    }

    // Ruling rock-meshes-by-size: a mesh is scaled by the gameplay radius over its own measured radius, so the drawn extent (scale x mesh
    // radius) is the gameplay radius and the drawn top is the floater layer. Mesh radii as measured in the project (0.96 to 2.43 around a median of 1.5).
    [Theory]
    [InlineData(0.958f)]
    [InlineData(1.502f)]
    [InlineData(2.430f)]
    public void ARockIsDrawnAtItsGameplayRadiusWhateverItsMeshRadius(float meshRadius)
    {
        var w = Build();
        Step(w, 1f / 60f, 301);
        const float lead = .01f;
        var time = w.Zone.ExactTime + lead;
        var drawn = new float4[w.Asteroids.Length];
        w.Zone.EvaluateBelt(w.Belt, drawn, time);
        var settings = w.Items.GameplaySettings;
        for (var i = 0; i < drawn.Length; i++)
        {
            var scale = DrawAhead.RockScale(drawn[i].w, meshRadius);
            Assert.Equal(w.Zone.ChunkRadius(new ChunkId(w.Belt, i)), scale * meshRadius, 3);
            Assert.Equal(FieldAt(w, drawn[i].xy, time) + FloaterLayerHeight, DrawAhead.ChunkAt(w.Zone, settings, drawn[i], lead).y + scale * meshRadius, 3);
        }
    }

    // A mesh candidate as the renderer holds it: a name and a measured radius, in the renderer's own order.
    private record Candidate(string Name, float Radius);

    private static DrawAhead.BeltDraw<Candidate> List(float[] sizes, params Candidate[] candidates) =>
        new(sizes, candidates, c => c.Radius, candidates.Length, "belt");

    private static float4[] Poses(params float[] radii) => radii.Select(r => float4(0, 0, 0, r)).ToArray();

    private static int[] MeshOf(DrawAhead.BeltDraw<Candidate> list, int rocks)
    {
        var meshOf = new int[rocks];
        for (var m = 0; m < list.ByMesh.Length; m++)
            foreach (var d in list.ByMesh[m]) meshOf[d.Rock] = m;
        return meshOf;
    }

    // Biggest rock, biggest mesh; smallest, smallest; sizes from the belt's own authored asteroids.
    [Fact]
    public void RocksRankedBySizeTakeMeshesRankedByRadius()
    {
        var w = Build();
        var belt = w.Zone.AsteroidBelts[w.Belt];
        var sizes = Enumerable.Range(0, w.Asteroids.Length).Select(i => belt.UndamagedSize(i, w.Zone.Settings)).ToArray();
        var list = DrawAhead.DrawListOf(belt, w.Zone.Settings, new[] { new Candidate("b", 3f), new Candidate("s", 1f), new Candidate("m", 2f) }, c => c.Radius, 3, w.Belt);
        var meshOf = MeshOf(list, sizes.Length);
        Assert.Equal(sizes.Length, list.ByMesh.Sum(r => r.Length));
        Assert.Equal(new[] { "s", "m", "b" }, list.Meshes.Select(c => c.Name).ToArray());
        Assert.Equal(2, meshOf[Array.IndexOf(sizes, sizes.Max())]);
        Assert.Equal(0, meshOf[Array.IndexOf(sizes, sizes.Min())]);
        for (var a = 0; a < sizes.Length; a++)
            for (var b = 0; b < sizes.Length; b++)
                if (sizes[a] < sizes[b]) Assert.True(meshOf[a] <= meshOf[b], $"rock {a} (size {sizes[a]}) is smaller than rock {b} ({sizes[b]}) and takes a bigger mesh");
    }

    // The renderer's draw list: more rocks than meshes, unequal radii, unsorted candidates and unsorted sizes, so a scale taken by rock index, a
    // kept list left unsorted, a reversed kept order and a slot or mesh assigned by rock index all fail. Sizes by rank: 1 (rock 1), 2 (3), 4 (2), 5 (5), 7 (4), 9 (0).
    [Fact]
    public void TheDrawListGivesEachRockItsMeshAndItsScale()
    {
        var list = List(new[] { 9f, 1f, 4f, 2f, 7f, 5f }, new Candidate("big", 2.4f), new Candidate("small", 1.0f), new Candidate("mid", 1.5f));
        var byMesh = list.Draw(Poses(10f, 11f, 12f, 13f, 14f, 15f));
        Assert.Equal(new[] { "small", "mid", "big" }, list.Meshes.Select(c => c.Name).ToArray());
        Assert.Equal(new[] { 1, 3 }, byMesh[0].Select(d => d.Rock).ToArray());
        Assert.Equal(new[] { 2, 5 }, byMesh[1].Select(d => d.Rock).ToArray());
        Assert.Equal(new[] { 0, 4 }, byMesh[2].Select(d => d.Rock).ToArray());
        Assert.Equal(new[] { 11f / 1.0f, 13f / 1.0f }, byMesh[0].Select(d => d.Scale).ToArray());
        Assert.Equal(new[] { 12f / 1.5f, 15f / 1.5f }, byMesh[1].Select(d => d.Scale).ToArray());
        Assert.Equal(new[] { 10f / 2.4f, 14f / 2.4f }, byMesh[2].Select(d => d.Scale).ToArray());
    }

    // Worn rocks are drawn at their current radius on the mesh the unworn size gave them; the list reads the poses each call.
    [Fact]
    public void TheDrawListScalesByTheCurrentRadiusOnTheSameMesh()
    {
        var list = List(new[] { 3f, 1f, 2f }, new Candidate("a", 2f), new Candidate("b", 4f));
        var first = list.Draw(Poses(8f, 8f, 8f)).Select(r => r.Select(d => d.Rock).ToArray()).ToArray();
        var worn = list.Draw(Poses(2f, 8f, 8f));
        Assert.Equal(first, worn.Select(r => r.Select(d => d.Rock).ToArray()).ToArray());
        var rock0 = worn.SelectMany((r, m) => r.Select(d => (d.Rock, m, d.Scale))).Single(d => d.Rock == 0);
        Assert.Equal((1, 2f / 4f), (rock0.m, rock0.Scale));
    }

    [Fact]
    public void EqualRocksTakeMeshesByIndex()
    {
        int[] Meshes(float[] sizes, params float[] radii) =>
            MeshOf(List(sizes, radii.Select((r, i) => new Candidate("c" + i, r)).ToArray()), sizes.Length);
        Assert.Equal(new[] { 0, 0, 1, 1, 2, 2, 2 }, Meshes(new[] { 5f, 5f, 5f, 5f, 5f, 5f, 5f }, 1f, 2f, 3f));
        Assert.Equal(new[] { 2, 0, 2, 1, 2 }, Meshes(new[] { 9f, 1f, 4f, 2f, 7f }, 1f, 2f, 3f));
        Assert.Equal(new[] { 0, 1, 1 }, Meshes(new[] { 1f, 2f, 3f }, 1f, 2f));
        Assert.Equal(new[] { 0, 0, 1, 1 }, Meshes(new[] { 1f, 2f, 3f, 4f }, 1f, 2f));
        Assert.Equal(new[] { 0, 1 }, Meshes(new[] { 1f, 2f }, 1f, 2f, 3f, 4f, 5f));
        Assert.Equal(new[] { 0, 0, 1, 1, 2, 2, 2 }, Meshes(new[] { 5f, 5f, 5f, 5f, 5f, 5f, 5f }, 4f, 4f, 4f));
    }

    // A mesh with no measured radius is refused at load, naming the field and the mesh, never an infinite scale.
    [Theory]
    [InlineData(0f)]
    [InlineData(-1f)]
    [InlineData(float.NaN)]
    public void AMeshWithoutARadiusIsRefusedByName(float radius)
    {
        var refused = Assert.Throws<ArgumentException>(() => List(new[] { 1f, 2f }, new Candidate("ok", 1.5f), new Candidate("bad", radius)));
        Assert.Contains("InstancedMesh.Radius", refused.Message);
        Assert.Contains("#1", refused.Message);
        Assert.Contains("InstancedMesh.Radius", Assert.Throws<ArgumentException>(() => DrawAhead.RockScale(1f, radius)).Message);
    }

    // Finding equal-radius-tie-unpinned: meshes of equal radius rank by candidate index, the first listed first.
    [Fact]
    public void EqualRadiusMeshesRankByCandidateIndex()
    {
        string[] Names(params Candidate[] candidates) => List(new[] { 1f, 2f, 3f }, candidates).Meshes.Select(c => c.Name).ToArray();
        Assert.Equal(new[] { "x", "y", "z" }, Names(new Candidate("x", 2f), new Candidate("y", 2f), new Candidate("z", 2f)));
        Assert.Equal(new[] { "small", "x", "y" }, Names(new Candidate("x", 3f), new Candidate("y", 3f), new Candidate("small", 1f)));
        Assert.Equal(new[] { "y", "x" }, Names(new Candidate("y", 2f), new Candidate("x", 2f)));
    }

    // Finding empty-candidates-divide-by-zero: no candidates, or a kept count below one, is a refusal naming the field.
    [Fact]
    public void NoCandidatesOrNoKeptCountIsRefusedByFieldName()
    {
        Assert.Contains("AsteroidMeshes", Assert.Throws<ArgumentException>(() => new DrawAhead.BeltDraw<Candidate>(new[] { 1f }, new Candidate[0], c => c.Radius, 5, "belt")).Message);
        foreach (var keep in new[] { 0, -1 })
            Assert.Contains("AsteroidMeshCount", Assert.Throws<ArgumentException>(() => new DrawAhead.BeltDraw<Candidate>(new[] { 1f }, new[] { new Candidate("a", 1f) }, c => c.Radius, keep, "belt")).Message);
    }

    // Finding renderer-random-mesh-prune: which meshes a belt keeps is a function of the belt's key, the candidates and the kept count alone.
    [Fact]
    public void TheKeptMeshesAreAFunctionOfTheBeltKey()
    {
        var candidates = Enumerable.Range(0, 8).Select(i => new Candidate("c" + i, 1f + i)).ToArray();
        string[] Kept(string key, int keep) =>
            new DrawAhead.BeltDraw<Candidate>(new[] { 1f, 2f, 3f }, candidates, c => c.Radius, keep, key).Meshes.Select(c => c.Name).ToArray();
        var keys = Enumerable.Range(0, 40).Select(i => "belt-" + i).ToArray();
        foreach (var key in keys)
        {
            var kept = Kept(key, 3);
            Assert.Equal(3, kept.Distinct().Count());
            Assert.Equal(kept, Kept(key, 3));
        }
        Assert.True(keys.Select(k => string.Join(",", Kept(k, 3).OrderBy(n => n))).Distinct().Count() >= 8, "belts keep different meshes");
        foreach (var candidate in candidates)
            Assert.Contains(keys, k => Kept(k, 3).Contains(candidate.Name));
        Assert.Equal(candidates.Select(c => c.Name).ToArray(), Kept("belt-0", 8));
        Assert.Equal(candidates.Select(c => c.Name).ToArray(), Kept("belt-0", 20));
    }

    // A mined rock keeps its mesh and only shrinks: the assignment reads the unworn size.
    [Fact]
    public void AMinedRockKeepsItsMesh()
    {
        var w = Build();
        var belt = w.Zone.AsteroidBelts[w.Belt];
        var candidates = new[] { new Candidate("a", 1f), new Candidate("b", 2f), new Candidate("c", 3f) };
        int[] Meshes() => MeshOf(DrawAhead.DrawListOf(belt, w.Zone.Settings, candidates, c => c.Radius, candidates.Length, w.Belt), w.Asteroids.Length);
        var before = Meshes();
        var chunk = new ChunkId(w.Belt, 5);
        var hitpoints = w.Zone.Settings.AsteroidHitpoints.Evaluate(w.Asteroids[5].Size);
        var unworn = w.Zone.ChunkRadius(chunk);
        Assert.False(w.Zone.Wear(chunk, hitpoints * .9f));
        Assert.True(w.Zone.ChunkRadius(chunk) < unworn * .5f, "the rock has shrunk to less than half its size");
        Assert.Equal(before, Meshes());
    }

    // Where the renderer decides a rock's height and scale it calls DrawAhead, and the reticle calls the same function: no inline copy of the rock
    // height in any of the three sites (finding owner-not-pinned). The renderer and the HUD are Unity-only, so this reads their source.
    [Fact]
    public void TheRendererAndTheReticleReadTheOneRockHeight()
    {
        var root = RunStartTests.FindRepoRoot();
        string Source(string path) => File.ReadAllText(Path.Combine(root, path));
        var renderer = Source("Assets/Scripts/Zone Display/ZoneRenderer.cs");
        var manager = Source("Assets/Scripts/Gameplay/ActionGameManager.cs");
        var drawAhead = Source("Assets/Scripts/ServerShared/DrawAhead.cs");
        Assert.Contains("DrawAhead.ChunkAt(", renderer);
        // The renderer holds no mesh choice, sort or index arithmetic: it iterates DrawAhead's list.
        Assert.Contains("DrawAhead.DrawListOf(", renderer);
        Assert.Matches(@"var byMesh = draw\.Draw\(transforms\);\s*for \(int m = 0;", renderer);
        Assert.Contains("transforms[rock.Rock]", renderer);
        Assert.Contains("Vector3.one * rock.Scale", renderer);
        Assert.Contains("DrawInstanced(draw.Meshes[m].Mesh, draw.Meshes[m].Material, matrices[m])", renderer);
        Assert.Contains("matrices[m][k] =", renderer);
        Assert.Contains("byMesh[m][k]", renderer);
        Assert.DoesNotContain("RockScale(", renderer);
        Assert.DoesNotContain(".Sort(", renderer);
        Assert.DoesNotContain("OrderBy", renderer);
        Assert.DoesNotContain("meshOf", renderer);
        Assert.DoesNotContain("slotOf", renderer);
        Assert.DoesNotContain("perMesh", renderer);
        Assert.DoesNotContain("FloaterHeight(", renderer);
        Assert.DoesNotContain("Vector3.one * transform.w", renderer);
        Assert.Contains("DrawAhead.Chunk(", manager);
        Assert.DoesNotContain("FloaterHeight(", manager);
        var start = drawAhead.IndexOf("public static float3 Chunk(", StringComparison.Ordinal);
        Assert.True(start >= 0, "DrawAhead.Chunk exists");
        var chunk = drawAhead.Substring(start, drawAhead.IndexOf(';', start) - start);
        Assert.Contains("ChunkAt(", chunk);
        Assert.DoesNotContain("FloaterHeight", chunk);
        Assert.DoesNotContain("pose.w", chunk);
    }

    // The term the shader takes sits on its node's rail at the drawn time: the lowering copies DrawAhead.Orbit of the term's
    // node and decides nothing, so a drawn term and the drawn planet share one pose at a non-zero lead.
    [Fact]
    public void ATermIsLoweredAtItsNodesDrawnRail()
    {
        var w = Build();
        Step(w, 1f / 60f, 803);
        Assert.Contains(w.Zone.Wells.Terms, t => t.Node.Equals(w.PlanetOrbit));
        Assert.Contains(w.Zone.Wells.Terms, t => t.Node.Equals(w.MoonOrbit));
        foreach (var lead in new[] { 0f, .004f, .0166f })
        {
            var time = w.Zone.ExactTime + lead;
            foreach (var term in w.Zone.Wells.Terms)
            {
                var at = term.Node.Equals(w.PlanetOrbit) ? PlanetAt(time) : term.Node.Equals(w.MoonOrbit) ? MoonAt(time) : SunCentre;
                AssertNear(at, DrawAhead.Orbit(w.Zone, term.Node, lead), 1e-3f, $"term of a body at lead {lead}");
            }
        }

        Assert.True(length(DrawAhead.Orbit(w.Zone, w.PlanetOrbit, .0166f) - DrawAhead.Orbit(w.Zone, w.PlanetOrbit, 0f)) > .1f, "the term moves over a lead");
        AssertNear(w.Zone.Orbits[w.PlanetOrbit].Position, DrawAhead.Orbit(w.Zone, w.PlanetOrbit, 0f), 0f, "at lead 0 the term is the step's memo");
    }

    // A HUD marker round the ship the camera follows (an incoming lock, a contact's bearing, a ping's flash) is anchored on
    // that ship's drawn position, so it holds still against the drawn hull at every lead. A marker anchored on the step's
    // position slides v * lead off it and snaps back each step.
    [Fact]
    public void AMarkerAtABearingIsAnchoredOnTheDrawnShip()
    {
        var w = Build();
        Step(w, 1f / 60f, 30);
        var ship = new OrbitalEntity(w.Items, w.Zone, w.Hull, default, new EntitySettings())
        {
            Position = float3(520f, 0, -140f),
            Velocity = float2(12f, -5f)
        };
        var bearing = normalize(float2(.6f, .8f));
        foreach (var lead in new[] { 0f, .004f, .0166f })
        {
            var drawn = DrawAhead.Position(ship, lead);
            var marker = DrawAhead.AtBearing(ship, bearing, 7f, lead);
            AssertNear(float2(520f, -140f) + float2(12f, -5f) * lead + bearing * 7f, marker.xz, 1e-4f, $"marker at lead {lead}");
            Assert.Equal(drawn.y, marker.y, 4);
            AssertNear(drawn.xz, marker.xz - bearing * 7f, 1e-4f, $"the marker's anchor is the drawn ship at lead {lead}");
        }
    }

    // An orbit whose parent the zone does not hold is reported when a rail is evaluated at a drawn time.
    [Fact]
    public void AMissingParentOrbitIsLoggedAtADrawnTime()
    {
        var w = Build();
        Step(w, 1f / 60f, 30);
        var log = new List<string>();
        w.Zone.Log = log.Add;
        w.Zone.Orbits.Remove(w.PlanetOrbit);
        foreach (var lead in new[] { .004f, .5f })
        {
            log.Clear();
            w.Zone.OrbitPositionAt(w.MoonOrbit, w.Zone.ExactTime + lead);
            Assert.Contains("Requested orbit is not part of this zone!", log);
        }
    }

    private static void AssertNear(float2 expected, float2 actual, float eps, string what) =>
        Assert.True(length(expected - actual) <= eps, $"{what}: expected {expected}, got {actual} (eps {eps})");
}
