/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using CultMath;
using UniRx;
using Xunit;
using static CultMath.math;
using float2 = CultMath.float2;

// The determinism ledger (aetheria-arena, cut determinism-ledger): every seed a match reaches is stated, never the
// clock's. The item stream is seeded by whoever builds the ItemManager and by RunStart at each step of a run; the unpack
// scatter rides the zone's own dice (ruling run-seed-drops-only); a scenario has a stable Id; and one fight between two
// piloted ships is one history, pinned for the host class that records replays.
public sealed partial class RunStartTests
{
    // The two-AI fight's digest, captured on Yggdrasil. Replays are trusted only on the host class that recorded them
    // (ruling replays-per-host-class), and arenas run on Linux.
    private const string TwoAiPinnedHostClass = "linux-x64";
    // Repinned twice, and the second repin has two causes (Soul measured the steps: group 1 + seed = f19b9992, + the Follow
    // gate = ce81a426; group 2 without the seed = bf586806).
    // First for the side split: the two fighters are unaffiliated, were one side and each followed itself (1b2ffe51).
    // Then for staged knowledge (ruling repin-for-staged-knowledge), which moved it twice: the fighters are staged knowing
    // each other, so they engage at step 0 (f19b9992); and Follow now flies only on an anchor the pilot itself tracks, so
    // once the sensors drop the staged anchor the follower stops following it (ce81a426).
    private const string TwoAiPinnedDigest = "ce81a426c40495e4b4b7b7f83ad23767cc781dce5523d713b4455e0aa5b7fe33";
    private const float TwoAiSeconds = 60f;

    [Fact]
    public void ItemManagerRefusesSeedZero()
    {
        var refused = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ItemManager(0u, _cache, new ProvenanceLedger(), _items.GameplaySettings, _ => { }));
        Assert.Equal("seed", refused.ParamName);

        var seeded = new ItemManager(0xBEEFu, _cache, new ProvenanceLedger(), _items.GameplaySettings, _ => { });
        Assert.Equal(new CultMath.Random(0xBEEFu).state, seeded.Random.state);
    }

    [Fact]
    public void ItemSeedIsTheRunSeedAndStep()
    {
        var galaxy = Launch(new Scripted(false, stage => stage.Player(stage.Bare("Djinni"), float2(0, 0)), seed: 4242)).galaxy;
        Assert.Equal(4242u, galaxy.Seed);
        Assert.Equal(unchecked(4242u * 0x9E3779B1u + 1), RunStart.ItemSeed(galaxy, RunStart.ArenaStep));
        Assert.Equal(unchecked(4242u * 0x9E3779B1u + 2), RunStart.ItemSeed(galaxy, RunStart.StageStep));
        var arena = RunStart.ItemSeed(galaxy, RunStart.ArenaStep);
        var stage = RunStart.ItemSeed(galaxy, RunStart.StageStep);
        var resumed = RunStart.ItemSeed(galaxy, RunStart.ContinueStep);
        Assert.NotEqual(arena, resumed);
        Assert.NotEqual(stage, resumed);
        Assert.NotEqual(0u, RunStart.NoRunSeed);

        // The stream a new run's arena generates on is the one ItemSeed names.
        var items = new ItemManager(7u, _cache, new ProvenanceLedger(), _items.GameplaySettings, _ => { });
        RunStart.GenerateArena(items, _zoneSettings, _planetSettings, galaxy, new Scripted(false, _ => { }, seed: 4242));
        var expected = new ItemManager(arena, _cache, new ProvenanceLedger(), _items.GameplaySettings, _ => { });
        RunStart.GenerateArena(expected, _zoneSettings, _planetSettings, galaxy, new Scripted(false, _ => { }, seed: 4242));
        Assert.Equal(expected.Random.state, items.Random.state);
    }

    private const int OriginDrifters = 3;

    // The galaxy seed whose ContinueStep item seed wraps to zero: -ContinueStep times the inverse of 0x9E3779B1, mod 2^32.
    private const uint ZeroAtContinueSeed = 0xD45E720Du;

    [Fact]
    public void ItemSeedRefusesAZeroSeed()
    {
        var galaxy = Launch(new Scripted(false, stage => stage.Player(stage.Bare("Djinni"), float2(0, 0)),
            seed: ZeroAtContinueSeed)).galaxy;
        Assert.Equal(ZeroAtContinueSeed, galaxy.Seed);
        Assert.NotEqual(0u, RunStart.ItemSeed(galaxy, RunStart.ArenaStep));
        var refused = Assert.Throws<InvalidOperationException>(() => RunStart.ItemSeed(galaxy, RunStart.ContinueStep));
        Assert.Contains("zero", refused.Message);
    }

    // A pack holding three unpiloted ships at the origin besides the player, from a staged arena. More than one, so
    // each ship's own ordinal is visible: ships sharing a draw would stack on one point.
    private (ZonePack pack, Galaxy galaxy) OriginPack()
    {
        var drifters = new List<Ship>();
        var (galaxy, arena, staged, failures) = Launch(new Scripted(false, stage =>
        {
            stage.Player(stage.Bare("Djinni"), float2(-50000, -50000));
            for (var i = 0; i < OriginDrifters; i++)
                drifters.Add(stage.Place(stage.Bare("Longinus"), float2(0, 0)) as Ship);
        }));
        Assert.True(failures.Count == 0, string.Join("; ", failures));
        Assert.All(drifters, drifter =>
        {
            Assert.NotNull(drifter);
            Assert.False(drifter.IsPlayerShip);
            Assert.True(lengthsq(drifter.Position) < 1, "the fixture's drifters are packed at the origin");
        });
        return (arena.PackZone(), galaxy);
    }

    private List<CultMath.float3> Scattered(ZonePack pack, GalaxyZone galaxyZone, Galaxy galaxy)
    {
        var zone = new Zone(_items, _planetSettings, pack, galaxyZone, galaxy);
        return zone.Entities.OfType<Ship>().Where(ship => !ship.IsPlayerShip).Select(ship => ship.Position).ToList();
    }

    [Fact]
    public void ScatterIgnoresTheItemStream()
    {
        var (pack, galaxy) = OriginPack();
        _items.Random = new CultMath.Random(11);
        var first = Scattered(pack, galaxy.Entrance, galaxy);
        _items.Random = new CultMath.Random(12);
        for (var i = 0; i < 37; i++) _items.Random.NextUInt();
        var second = Scattered(pack, galaxy.Entrance, galaxy);

        Assert.NotEmpty(first);
        Assert.All(first, position => Assert.True(lengthsq(position) >= 1, "the drifter was scattered"));
        Assert.Equal(first, second);
    }

    // Each origin ship is placed by its own draw: the 1-based ordinal of its entry in the pack, on the zone's
    // ScatterStream, within half the zone's radius of the centre on every axis.
    [Fact]
    public void ScatterDrawsEachShipsOrdinalWithinHalfTheRadius()
    {
        var (pack, galaxy) = OriginPack();
        var zone = new Zone(_items, _planetSettings, pack, galaxy.Entrance, galaxy);
        Assert.Equal(pack.Entities.Count, zone.Entities.Count);

        var half = pack.Radius * .5f;
        var expected = new List<CultMath.float3>();
        var actual = new List<CultMath.float3>();
        for (var i = 0; i < zone.Entities.Count; i++)
        {
            if (zone.Entities[i] is not Ship {IsPlayerShip: false} ship) continue;
            expected.Add(SimulationDice.For(zone.CombatSeed, SimulationDice.ScatterStream, (uint) i + 1)
                .NextFloat3(float3(-half), float3(half)));
            actual.Add(ship.Position);
        }

        Assert.Equal(OriginDrifters, actual.Count);
        Assert.Equal(expected, actual);
        Assert.Equal(OriginDrifters, actual.Distinct().Count());
        Assert.All(actual, position => Assert.True(all(abs(position) <= half), $"{position} is within half the radius {half}"));
    }

    [Fact]
    public void ScatterIsPerZone()
    {
        var (pack, galaxy) = OriginPack();
        var other = galaxy.Zones.First(zone => zone != galaxy.Entrance && zone.Name != galaxy.Entrance.Name);
        var here = Scattered(pack, galaxy.Entrance, galaxy);
        var there = Scattered(pack, other, galaxy);
        Assert.NotEmpty(here);
        Assert.Equal(here.Count, there.Count);
        Assert.NotEqual(here, there);
    }

    [Fact]
    public void SimulationDiceStreamsAreDistinct()
    {
        var streams = new[]
        {
            SimulationDice.LootStream, SimulationDice.MineStream, SimulationDice.CombatSampleStream,
            SimulationDice.PatrolStream, SimulationDice.ScatterStream
        };
        Assert.Equal(streams.Length, streams.Distinct().Count());
    }

    [Fact]
    public void ScenarioIdsAreUniqueKebabAndStable()
    {
        var kebab = new Regex("^[a-z0-9]+(-[a-z0-9]+)*$");
        var every = EveryScenario.ToList();
        Assert.All(every, scenario => Assert.True(scenario.Id != null && kebab.IsMatch(scenario.Id),
            $"{scenario.GetType().Name}: Id is not lowercase kebab"));
        Assert.Equal(every.Count, every.Select(scenario => scenario.Id).Distinct().Count());
        Assert.Equal("duel", new Duel().Id);
        Assert.Equal("demo-terminus", new DemoTerminus().Id);
    }

    // Two piloted fighters about 900 apart, each approaching the other, hostile both ways; the player bare, unpiloted
    // and far from the fight. Steps TwoAiSeconds at `dt` (the game's step by default) and returns the SHA-256 of the joined
    // snapshots, the hull each fighter ends with and the sim time a hull first reaches zero (TwoAiSeconds when none does).
    private (string digest, bool bothPiloted, int hits, bool hullLost, float eastHull, float westHull, float firstKill, double stepSeconds) TwoAiFight(uint seed = GalaxySeed, float dt = SimClock.GameStep)
    {
        Ship east = null, west = null;
        var (_, arena, _, failures) = Launch(new Scripted(false, stage =>
        {
            stage.Player(stage.Bare("Djinni"), float2(-50000, -50000));
            east = stage.Place(EvLonginus(stage), float2(450, 0), facing: float2(-1, 0), piloted: true) as Ship;
            west = stage.Place(EvLonginus(stage), float2(-450, 0), facing: float2(1, 0), piloted: true) as Ship;
            stage.Approach(east, west);
            stage.Approach(west, east);
        }, seed));
        Assert.True(failures.Count == 0, string.Join("; ", failures));
        east.SetIff(west, true);
        west.SetIff(east, true);
        var hullAtStart = east.Hull.Durability + west.Hull.Durability;
        var bothPiloted = arena.Agents.Any(agent => agent.Ship == east) && arena.Agents.Any(agent => agent.Ship == west);

        var shots = new List<string>();
        var hits = 0;
        using var _ = arena.ShotResolved.Subscribe(outcome =>
        {
            shots.Add($"{outcome.ShotId}:{outcome.Result}");
            if (outcome.Hit) hits++;
        });
        var history = new StringBuilder();
        var firstKill = TwoAiSeconds;
        var steps = (int) MathF.Round(TwoAiSeconds / dt);
        var clock = System.Diagnostics.Stopwatch.StartNew();
        for (var step = 0; step < steps; step++)
        {
            arena.Update(dt);
            history.Append(Snapshot(arena, shots)).Append('\n');
            if (firstKill == TwoAiSeconds && (east.Hull.Durability <= 0 || west.Hull.Durability <= 0)) firstKill = (step + 1) * dt;
        }
        var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(history.ToString()))).ToLowerInvariant();
        return (digest, bothPiloted, hits, east.Hull.Durability + west.Hull.Durability < hullAtStart, east.Hull.Durability, west.Hull.Durability, firstKill, clock.Elapsed.TotalSeconds);
    }

    [Fact]
    public void TwoAiFightIsOneHistory()
    {
        var first = TwoAiFight();
        var second = TwoAiFight();
        Assert.True(first.bothPiloted, "both fighters are piloted");
        Assert.True(first.hits > 0, "at least one shot resolved as a hit");
        Assert.True(first.hullLost, "at least one fighter lost hull");
        Assert.Equal(first.digest, second.digest);
    }

    private static string HostClass() =>
        (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "linux"
            : RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "windows"
            : RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "osx" : "other")
        + "-" + RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant();

    [Fact]
    public void TwoAiDigestIsPinned()
    {
        var host = HostClass();
        var digest = TwoAiFight().digest;
        Assert.True(host == TwoAiPinnedHostClass,
            $"TWO-AI-DIGEST host class {host} has no pin (pinned: {TwoAiPinnedHostClass}); measured digest {digest}");
        Assert.True(digest == TwoAiPinnedDigest, $"TWO-AI-DIGEST {host} measured {digest}, pinned {TwoAiPinnedDigest}");
    }
}
