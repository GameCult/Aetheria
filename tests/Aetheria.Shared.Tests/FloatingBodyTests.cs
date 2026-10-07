using System;
using System.Collections.Generic;
using System.IO;
using CultMath;
using GameCult.Caching;
using Xunit;
using static CultMath.math;
using float2 = CultMath.float2;
using Random = CultMath.Random;

// Floating bodies (aetheria-release cut floating-bodies): the drift loot and mines share, the structured
// dice they roll with, and the zone-scoped ids they carry. The body has no consumer yet; these tests are
// its whole contract.
public sealed class FloatingBodyTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "aetheria-floatingbody-" + Guid.NewGuid().ToString("N"));
    private readonly List<CultCache> _caches = new List<CultCache>();

    public FloatingBodyTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        foreach (var c in _caches) c.Dispose();
        Directory.Delete(_root, true);
    }

    private static GameplaySettings Settings(float drag = .05f, float launchDrag = .2f, float gravity = 1f) => new GameplaySettings
    {
        FloatingBodyDrag = drag, FloatingBodyLaunchDrag = launchDrag, FloatingBodyGravity = gravity
    };

    private Zone NewZone()
    {
        var cache = AetheriaStores.Open(Path.Combine(_root, Guid.NewGuid().ToString("N") + ".cc"), catalogWritable: true);
        _caches.Add(cache);
        var settings = new GameplaySettings
        {
            DefaultEntitySettings = new EntitySettings(),
            Tiers = new[] { new RarityTier { Name = "Common", Quality = .5f, Rarity = 0, Color = new float3(1, 1, 1) } },
            QualityPriceModifier = new ExponentialLerp()
        };
        var items = new ItemManager(cache, new ProvenanceLedger(), settings, _ => { });
        return new Zone(items, new PlanetSettings(), new ZonePack(), new GalaxyZone { Name = "Test Zone", Owner = null }, null);
    }

    private static KinematicBody Run(KinematicBody b, float seconds, float dt, float2 force, GameplaySettings s)
    {
        var steps = (int) Math.Round(seconds / dt);
        for (var i = 0; i < steps; i++) b.Step(dt, force, s);
        return b;
    }

    [Fact]
    public void LaunchVelocityDecaysExponentially()
    {
        var s = Settings();
        // Speed .3: the semi-implicit step's position error is bounded by speed * dt / 2 (about .005 at dt .1), inside the 1e-2 pin.
        var start = new KinematicBody { Velocity = new float2(.18f, .24f) };
        foreach (var dt in new[] { 1f / 120f, .1f })
        {
            var b = Run(start, 3f, dt, float2.zero, s);
            Assert.Equal(.3f * exp(-s.FloatingBodyLaunchDrag * 3f), length(b.Velocity), 4);
        }

        var fine = Run(start, 3f, 1f / 120f, float2.zero, s);
        var coarse = Run(start, 3f, .1f, float2.zero, s);
        Assert.True(length(fine.Position - coarse.Position) < 1e-2f);
        // It really moved, along the launch direction.
        Assert.True(fine.Position.x > 0f && fine.Position.y > fine.Position.x);
    }

    [Fact]
    public void DriftFollowsTheForce()
    {
        var s = Settings();
        var force = new float2(.02f, -.01f);
        var b = Run(new KinematicBody(), 600f, .1f, force, s);
        Assert.Equal(force.x * s.FloatingBodyGravity / s.FloatingBodyDrag, b.Drift.x, 2);
        Assert.Equal(force.y * s.FloatingBodyGravity / s.FloatingBodyDrag, b.Drift.y, 2);
        Assert.Equal(b.Velocity + b.Drift, b.TotalVelocity);

        var noGravity = Run(new KinematicBody(), 10f, .1f, force, Settings(gravity: 0f));
        Assert.Equal(float2.zero, noGravity.Drift);
        Assert.Equal(float2.zero, noGravity.Position);

        var noForce = Run(new KinematicBody { Velocity = new float2(1f, 0f) }, 2f, .1f, float2.zero, s);
        Assert.Equal(float2.zero, noForce.Drift);
        Assert.True(noForce.Position.x > 0f && noForce.Position.y == 0f);
    }

    [Fact]
    public void OneStepMovesByLaunchPlusDrift()
    {
        var s = Settings();
        var b = new KinematicBody { Velocity = new float2(2f, 0f), Drift = new float2(0f, 1f) };
        b.Step(.5f, new float2(.4f, 0f), s);
        var velocity = 2f * exp(-s.FloatingBodyLaunchDrag * .5f);
        var drift = new float2(.4f * .5f, 1f) * exp(-s.FloatingBodyDrag * .5f);
        Assert.Equal(velocity, b.Velocity.x, 4);
        Assert.Equal(drift.x, b.Drift.x, 4);
        Assert.Equal(drift.y, b.Drift.y, 4);
        Assert.Equal((velocity + drift.x) * .5f, b.Position.x, 4);
        Assert.Equal(drift.y * .5f, b.Position.y, 4);
    }

    [Fact]
    public void ShotDiceAreUnchanged()
    {
        // fmix32 as FireControl.MixSeed wrote it, the function this cut moved.
        static uint Reference(uint seed)
        {
            seed ^= seed >> 16; seed *= 0x85ebca6bu; seed ^= seed >> 13; seed *= 0xc2b2ae35u; seed ^= seed >> 16;
            return seed;
        }
        var inputs = new List<uint> { 0u, 0xFFFFFFFFu };
        for (uint i = 1; inputs.Count < 64; i++) inputs.Add(i * 2654435761u);
        foreach (var x in inputs) Assert.Equal(Reference(x), SimulationDice.Mix(x));
        Assert.Equal(0u, SimulationDice.Mix(0u));
        Assert.Equal(0x514E28B7u, SimulationDice.Mix(1u));
    }

    [Fact]
    public void DiceStreamsAreIndependentAndRepeatable()
    {
        var loot = SimulationDice.For(1234u, SimulationDice.LootStream, 5u);
        var mine = SimulationDice.For(1234u, SimulationDice.MineStream, 5u);
        Assert.NotEqual(loot.NextUInt(), mine.NextUInt());

        var a = SimulationDice.For(1234u, SimulationDice.LootStream, 5u);
        var b = SimulationDice.For(1234u, SimulationDice.LootStream, 5u);
        for (var i = 0; i < 8; i++) Assert.Equal(a.NextUInt(), b.NextUInt());

        Assert.NotEqual(SimulationDice.For(1234u, SimulationDice.LootStream, 5u).NextUInt(), SimulationDice.For(1235u, SimulationDice.LootStream, 5u).NextUInt());
        Assert.NotEqual(SimulationDice.For(1234u, SimulationDice.LootStream, 5u).NextUInt(), SimulationDice.For(1234u, SimulationDice.LootStream, 6u).NextUInt());

        // Never seeded 0: the argument set that mixes to zero still rolls.
        var zero = SimulationDice.For(0u, 0u, 0u);
        Assert.Equal(1u, zero.state);
    }

    [Fact]
    public void NeighbouringZonesAndOrdinalsNeverShareADraw()
    {
        // Folding stream and ordinal into the zone seed with one xor would make (zone, n) and (zone ^ 1, n ^ 1)
        // the same seed; the double mix keeps them apart.
        for (uint zone = 0; zone < 64; zone++)
        for (uint n = 0; n < 16; n++)
            Assert.NotEqual(SimulationDice.For(zone, SimulationDice.LootStream, n).NextUInt(),
                SimulationDice.For(zone ^ 1u, SimulationDice.LootStream, n ^ 1u).NextUInt());
    }

    [Fact]
    public void BodyIdsAreUniqueInAZone()
    {
        var zone = NewZone();
        var seen = new HashSet<FloatingBodyId>();
        uint last = 0;
        for (var i = 0; i < 10000; i++)
        {
            var id = zone.NextBodyId();
            Assert.True(id.Value > last);
            last = id.Value;
            Assert.True(seen.Add(id));
        }
        Assert.Equal(1u, NewZone().NextBodyId().Value);
        Assert.Equal("1", new FloatingBodyId(1).ToString());
        Assert.Equal(new FloatingBodyId(7), new FloatingBodyId(7));
        Assert.NotEqual(new FloatingBodyId(7), new FloatingBodyId(8));
    }

    // The law ships felt at 3035230b, written out here from that tree's Ship.Update (a per-tick velocity add, no dt)
    // and not from Zone: the pin must not move when Zone's law does.
    private static float2 LegacyPushPerFrame(float2 f, float strength)
    {
        var m = lengthsq(f);
        if (m <= .001f) return float2.zero;
        return normalize(f) * strength * (1 / (1 - m) - 1);
    }

    [Fact]
    public void TheGravityLawPushesShipsAsItDidAtSixtyFps()
    {
        foreach (var strength in new[] { 1f, 7.5f })
        foreach (var f in new[] { new float2(.1f, 0), new float2(.2f, .1f), new float2(-.3f, .4f), new float2(.5f, -.5f), new float2(0, .7f) })
        {
            var legacy = LegacyPushPerFrame(f, strength);
            var perFrame = Zone.GravityAcceleration(f, strength) * (1f / 60f);
            var tolerance = Math.Max(1e-7f, length(legacy) * 1e-5f);
            Assert.InRange(perFrame.x, legacy.x - tolerance, legacy.x + tolerance);
            Assert.InRange(perFrame.y, legacy.y - tolerance, legacy.y + tolerance);
        }
        // Per second: halving dt halves the push, so the law holds at any tick rate.
        var a = Zone.GravityAcceleration(new float2(.3f, .1f), 2f);
        Assert.Equal(a.x * .5f, (a * (1f / 120f)).x * 60f, 5);
        // The gate: |f|^2 at or below .001 pushes nothing.
        Assert.Equal(float2.zero, Zone.GravityAcceleration(new float2(.03f, 0), 1f));
        Assert.NotEqual(float2.zero, Zone.GravityAcceleration(new float2(.0317f, 0), 1f));
    }

    [Fact]
    public void DriftMatchesLegacyFloatingItemsAtSixtyFps()
    {
        // After 5 s at 60 fps with GravityStrength 1 and force magnitude |f|^2 = m, the legacy GridObject drift
        // position (measured by Soul, 2026-10-07, off 3035230b's GridObject.Update): .05 -> 1.7371, .1 -> 7.0012,
        // .3 -> 68.55, .5 -> 231.04.
        var s = Settings();
        foreach (var (m, expected) in new[] { (.05f, 1.7371f), (.1f, 7.0012f), (.3f, 68.55f), (.5f, 231.04f) })
        {
            var f = new float2(sqrt(m), 0);
            var body = Run(new KinematicBody(), 5f, 1f / 60f, Zone.GravityAcceleration(f, 1f), s);
            Assert.InRange(body.Position.x, expected * .999f, expected * 1.001f);
            Assert.Equal(0f, body.Position.y, 4);
        }
    }

}
