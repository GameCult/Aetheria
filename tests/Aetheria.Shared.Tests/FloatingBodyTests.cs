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
        var start = new KinematicBody { Velocity = new float2(.3f, .4f) };
        foreach (var dt in new[] { 1f / 120f, .1f })
        {
            var b = Run(start, 3f, dt, float2.zero, s);
            Assert.Equal(.5f * exp(-s.FloatingBodyLaunchDrag * 3f), length(b.Velocity), 4);
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
        Assert.NotEqual(0u, zero.state);
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
}
