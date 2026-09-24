/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.IO;
using CultMath;
using GameCult.Caching;
using Xunit;
using static CultMath.math;
using float2 = CultMath.float2;

// Cut 1 (docs/mining-cut.md): belts stop being simulated every tick on a threaded double buffer and become a
// pure function of zone time, evaluated on request. These tests pin that: a chunk's pose is exactly the
// closed-form orbit at the zone's current time (no staleness), it does not depend on how many ticks it took to
// get there (no stored state), and the size rule moved off the deleted per-tick pass unchanged. Every expected
// value below is computed independently, by the closed form written in this file -- never by calling
// AsteroidBelt.Pose/Size, the code under test.
public sealed class MiningCut1Tests : IDisposable
{
    private int _fixtureCount;
    private readonly string _root = Path.Combine(Path.GetTempPath(), "aetheria-miningcut1-" + Guid.NewGuid().ToString("N"));

    public MiningCut1Tests() => Directory.CreateDirectory(_root);
    public void Dispose() => Directory.Delete(_root, true);

    private static GameplaySettings GameSettings() => new GameplaySettings
    {
        DefaultEntitySettings = new EntitySettings(),
        Tiers = new[] { new RarityTier { Name = "Common", Quality = .5f, Rarity = 0, Color = new float3(1, 1, 1) } },
        QualityPriceModifier = new ExponentialLerp()
    };

    // Authored-shape curves, not defaults: a default ExponentialLerp/ExponentialCurve evaluates to a flat
    // constant everywhere, which would hide a broken formula (every asteroid or every damage state would read
    // alike). These give period, size and hitpoints real, distinct curvature over the fixture's distance and
    // damage range.
    private static PlanetSettings Settings() => new PlanetSettings
    {
        OrbitPeriod = new ExponentialCurve { Multiplier = 0.6f, Exponent = 1.3f, Constant = 40f },
        AsteroidSize = new ExponentialLerp { Minimum = 2f, Maximum = 9f, Exponent = 1.7f },
        AsteroidHitpoints = new ExponentialLerp { Minimum = 10f, Maximum = 300f, Exponent = 2f },
        AsteroidRespawnTime = new ExponentialLerp { Minimum = 5f, Maximum = 60f, Exponent = 1f }
    };

    private const int AsteroidCount = 24;
    private const float RootDistance = 4000f;
    private const float RootPhase = 0.17f;

    // A belt of AsteroidCount asteroids at distinct distances, phases, size parameters and rotation speeds,
    // orbiting a parent (RootDistance/RootPhase) that is itself moving -- so a test that reads the parent's
    // motion through ChunkPose is exercising real recursion, not a fixed point. Each call opens its own
    // catalog, so two zones built from this fixture never share cache state.
    private (Zone zone, CultRecordKey beltKey, Asteroid[] asteroids, PlanetSettings settings) BuildFixture()
    {
        var dir = Path.Combine(_root, $"cat{_fixtureCount++}");
        Directory.CreateDirectory(dir);
        var catalog = Path.Combine(dir, "Aetheria.cc");
        var run = Path.Combine(dir, "run.cc");
        // OrbitData and BodyData (AsteroidBeltData) are run types (AetheriaStores.RunTypes), not catalog types:
        // they need a run store to write into.
        var cache = AetheriaStores.Open(catalog, run, catalogWritable: true);
        var items = new ItemManager(cache, new ProvenanceLedger(), GameSettings(), _ => { });
        var settings = Settings();

        var rootOrbit = new OrbitData { Distance = RootDistance, Phase = RootPhase };
        var rootKey = cache.Upsert(rootOrbit).Key;

        var beltOrbit = new OrbitData { Parent = new CultRecordRef<OrbitData>(rootKey), Distance = 900f, Phase = 0.42f };
        var beltOrbitKey = cache.Upsert(beltOrbit).Key;

        var asteroids = new Asteroid[AsteroidCount];
        for (var i = 0; i < AsteroidCount; i++)
        {
            asteroids[i] = new Asteroid
            {
                Distance = 120f + i * 11.3f,
                Phase = frac(i * 0.6180339887f),
                Size = frac(i * 0.37f + 0.1f),
                RotationSpeed = 0.15f + i * 0.037f
            };
        }

        var beltData = new AsteroidBeltData { Orbit = new CultRecordRef<OrbitData>(beltOrbitKey), Asteroids = asteroids };
        var beltKey = cache.Upsert(beltData).Key;

        var pack = new ZonePack
        {
            Orbits = { new CultRecordRef<OrbitData>(rootKey), new CultRecordRef<OrbitData>(beltOrbitKey) },
            Planets = { new CultRecordRef<BodyData>(beltKey) },
            Radius = 5000f,
            Mass = 10000f,
            Time = 0
        };

        var zone = new Zone(items, settings, pack, new GalaxyZone { Name = "MiningCut1", Owner = null }, null);
        return (zone, beltKey, asteroids, settings);
    }

    // The formula of Zone.GetOrbitPosition (unchanged by this cut), reimplemented here so the expected value
    // never calls production orbit code.
    private static float2 OrbitPosition(double time, float distance, float phase, PlanetSettings settings, float2 fixedPosition = default)
    {
        var period = settings.OrbitPeriod.Evaluate(distance);
        if (period <= .01f) return fixedPosition;
        var wrapped = (float) frac(time / period);
        return OrbitData.Evaluate(frac(wrapped + phase)) * distance + fixedPosition;
    }

    // The formula of AsteroidBelt.Pose (Zone.cs:269-271 before this cut), reimplemented independently.
    private static float4 ExpectedPose(double time, Asteroid asteroid, float2 parentPosition, PlanetSettings settings, float size)
    {
        var rot = (float) (time * asteroid.RotationSpeed % (PI * 2));
        var pos = OrbitData.Evaluate((float) frac(time / settings.OrbitPeriod.Evaluate(asteroid.Distance) + asteroid.Phase))
                  * asteroid.Distance + parentPosition;
        return float4(pos.x, pos.y, rot, size);
    }

    private static void AssertClose(float expected, float actual, float eps, string what)
    {
        Assert.True(Math.Abs(expected - actual) <= eps, $"{what}: expected {expected}, got {actual} (eps {eps})");
    }

    [Fact]
    public void ChunkPoseIsTheOrbitAtZoneTime()
    {
        var (zone, beltKey, asteroids, settings) = BuildFixture();

        // Uneven dt, so a tick-count-based or double-buffered implementation would disagree with a pure
        // function of the accumulated time.
        var dts = new[] { 0.13f, 0.07f, 0.31f, 0.02f, 0.5f, 0.11f, 0.19f, 0.08f, 0.44f, 0.03f, 0.27f, 0.09f };
        double time = 0;
        foreach (var dt in dts)
        {
            zone.Update(dt);
            time += dt;
        }

        var parentPosition = OrbitPosition(time, RootDistance, RootPhase, settings);

        for (var i = 0; i < asteroids.Length; i++)
        {
            var expected = ExpectedPose(time, asteroids[i], parentPosition, settings, settings.AsteroidSize.Evaluate(asteroids[i].Size));
            var actual = zone.ChunkPose(beltKey, i);
            AssertClose(expected.x, actual.x, 0.01f, $"asteroid {i} x");
            AssertClose(expected.y, actual.y, 0.01f, $"asteroid {i} y");
            AssertClose(expected.z, actual.z, 0.001f, $"asteroid {i} rotation");
            AssertClose(expected.w, actual.w, 0.001f, $"asteroid {i} size");
        }
    }

    [Fact]
    public void ChunkPoseDoesNotDependOnUpdateCadence()
    {
        var (zoneOneStep, beltKeyA, asteroids, settings) = BuildFixture();
        zoneOneStep.Update(1.0f);

        var (zoneManySteps, beltKeyB, _, _) = BuildFixture();
        for (var i = 0; i < 60; i++)
            zoneManySteps.Update(1f / 60f);

        for (var i = 0; i < asteroids.Length; i++)
        {
            var a = zoneOneStep.ChunkPose(beltKeyA, i);
            var b = zoneManySteps.ChunkPose(beltKeyB, i);
            // Float summation of 60 steps of 1/60 drifts from 1.0 by a few ULPs; the pose is a pure function
            // of that (nearly identical) time, not of how many ticks produced it, so a loose position epsilon
            // catches a cadence-dependent implementation without chasing float noise.
            AssertClose(a.x, b.x, 0.05f, $"asteroid {i} x");
            AssertClose(a.y, b.y, 0.05f, $"asteroid {i} y");
            AssertClose(a.z, b.z, 0.01f, $"asteroid {i} rotation");
            AssertClose(a.w, b.w, 0.0001f, $"asteroid {i} size");
        }
    }

    [Fact]
    public void DamagedChunkShrinksAndABrokenOneHasNoSize()
    {
        var (zone, beltKey, asteroids, settings) = BuildFixture();
        var belt = zone.AsteroidBelts[beltKey];

        const int undamagedIndex = 5;
        const int damagedIndex = 9;
        const int brokenIndex = 14;

        var undamagedExpected = settings.AsteroidSize.Evaluate(asteroids[undamagedIndex].Size);
        Assert.Equal(undamagedExpected, belt.Size(undamagedIndex, settings), 4);

        var hp = settings.AsteroidHitpoints.Evaluate(asteroids[damagedIndex].Size);
        var damage = hp * 0.6f;
        belt.Damage[damagedIndex] = damage;
        var damagedExpected = settings.AsteroidSize.Evaluate((hp - damage) / hp * asteroids[damagedIndex].Size);
        var damagedActual = belt.Size(damagedIndex, settings);
        Assert.Equal(damagedExpected, damagedActual, 4);
        Assert.True(damagedActual < settings.AsteroidSize.Evaluate(asteroids[damagedIndex].Size),
            "a damaged asteroid must be smaller than its undamaged size");

        belt.RespawnTimers[brokenIndex] = settings.AsteroidRespawnTime.Evaluate(asteroids[brokenIndex].Size);
        Assert.Equal(0f, belt.Size(brokenIndex, settings));
    }

    // Stryker gap (2026-09-25 Stryker pass on this cut): nothing called Zone.EvaluateBelt or
    // AsteroidBelt.Evaluate, the renderer's own path (ZoneRenderer.cs:452-478), leaving both at NoCoverage.
    // This does not re-derive the orbit formula (that is ChunkPoseIsTheOrbitAtZoneTime's job); it pins that the
    // batch path is the same function as the per-index one, per index, over every asteroid -- so the renderer's
    // buffer can never quietly diverge into a second copy of the pose formula.
    [Fact]
    public void EvaluateBeltFillsEveryAsteroidExactlyAsChunkPoseWould()
    {
        var (zone, beltKey, asteroids, _) = BuildFixture();
        zone.Update(0.37f);

        var buffer = new float4[asteroids.Length];
        zone.EvaluateBelt(beltKey, buffer);

        Assert.Equal(asteroids.Length, buffer.Length);
        for (var i = 0; i < asteroids.Length; i++)
        {
            var expected = zone.ChunkPose(beltKey, i);
            Assert.Equal(expected.x, buffer[i].x, 4);
            Assert.Equal(expected.y, buffer[i].y, 4);
            Assert.Equal(expected.z, buffer[i].z, 4);
            Assert.Equal(expected.w, buffer[i].w, 4);
        }
    }

    // Stryker gap: AsteroidBelt.Radius (Zone.cs, AsteroidBelt constructor) had no assertion, so Max survived a
    // Min mutation. The renderer's visibility bounds (ZoneRenderer.cs:452) and minimap mesh (:663) both use it
    // as the belt's farthest asteroid; a Min there would cull or mis-bound the belt.
    [Fact]
    public void BeltRadiusIsTheFarthestAsteroidsDistance()
    {
        var (zone, beltKey, asteroids, _) = BuildFixture();

        var expectedRadius = float.MinValue;
        foreach (var a in asteroids)
            if (a.Distance > expectedRadius) expectedRadius = a.Distance;

        Assert.Equal(expectedRadius, zone.AsteroidBelts[beltKey].Radius, 4);
    }
}
