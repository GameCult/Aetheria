/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.Linq;
using CultMath;
using GameCult.Caching;
using Xunit;
using static CultMath.math;

// Mining Cut 5a (docs/mining-cut-refresh.md): a worn rock pays the shooter in ore at the wear site, from the shot's own
// dice; what does not fit in the hold floats at the rock. The fixtures are Cut 3's belt scene and Cut 4b's rig.
public sealed class MiningCut5aTests : IDisposable
{
    private readonly MiningCut4bTests _rigs = new MiningCut4bTests();
    private readonly MiningCut3Tests _scenes = new MiningCut3Tests();

    public void Dispose()
    {
        _rigs.Dispose();
        _scenes.Dispose();
    }

    private static SimpleCommodityData Commodity(string name, int maxStack = 100) =>
        new SimpleCommodityData { Name = name, Shape = new Shape(), Price = 5, MaxStack = maxStack };

    // Registers the ores and the kind's yields in the scene's catalog (the kind refs need the stored commodities).
    private static void Stock(MiningCut3Tests.Scene scene, FieldKindData kind, params (SimpleCommodityData Ore, float Abundance)[] yields)
    {
        kind.Yields.Clear();
        foreach (var (ore, abundance) in yields)
        {
            scene.Cache.Upsert(ore);
            kind.Yields.Add(new FieldYield { Commodity = scene.Cache.RefOf(ore), Abundance = abundance });
        }
        scene.Cache.Upsert(kind);
    }

    private EquippedCargoBay Bay(MiningCut3Tests.Scene scene, Ship ship, int width, int height, string name)
    {
        var shape = new Shape(width, height);
        foreach (var cell in shape.AllCoordinates) shape[cell] = true;
        var data = new CargoBayData { Name = name, Shape = new Shape(), InteriorShape = shape, Price = 1, Durability = 4 };
        scene.Cache.Upsert(data);
        var bay = new EquippedCargoBay(scene.Items, MiningCut3Tests.Mint(scene, data), new int2(1, 1), ship, name);
        ship.CargoBays.Add(bay);
        return bay;
    }

    private static int Units(IEnumerable<ItemInstance> items, string ore, MiningCut3Tests.Scene scene) =>
        items.OfType<SimpleCommodity>().Where(c => scene.Items.GetData(c).Name == ore).Sum(c => c.Quantity);

    private static int Units(EquippedCargoBay bay, string ore, MiningCut3Tests.Scene scene) => Units(bay.Cargo.Keys, ore, scene);

    // A rig whose rock (size .5: 4 units, whole) yields `ores`, shooter holding one bay.
    private (MiningCut4bTests.Rig Rig, EquippedCargoBay Bay) Mining(int bayWidth = 4, int bayHeight = 4, float damage = 1000f,
        WeaponFuse? fuse = null, float velocity = 0f, params (SimpleCommodityData, float)[] ores)
    {
        var kind = MiningCut3Tests.Kind("Asteroid", 20f);
        var rig = _rigs.Build(damage: damage, fuse: fuse, blastRadius: fuse == null ? 0f : 6f, precision: 1000f, rockSize: .5f, velocity: velocity, kind: kind);
        Stock(rig.Scene, kind, ores.Length > 0 ? ores : new[] { (Commodity("Rock"), 1f) });
        return (rig, Bay(rig.Scene, rig.Shooter, bayWidth, bayHeight, "Hold"));
    }

    private static int Hits(MiningCut4bTests.Rig rig, int cap = 40)
    {
        for (var shot = 0; shot < cap && !rig.Resolved.Any(o => o.Hit); shot++)
        {
            // A broken rock cannot be targeted again: the first hit ends the loop.
            FireControl.Fire(rig.Gun, rig.GunItem, rig.Shooter);
            MiningCut4bTests.Run(rig, .05f);
        }
        return rig.Resolved.Count(o => o.Hit);
    }

    private static float Hitpoints(MiningCut4bTests.Rig rig) =>
        rig.Zone.Settings.AsteroidHitpoints.Evaluate(rig.Zone.AsteroidBelts[rig.Rock.Field].Data.Asteroids[rig.Rock.Index].Size);

    [Fact]
    public void WearReturnsWhatTheRockAbsorbed()
    {
        var (rig, _) = Mining();
        var hp = Hitpoints(rig);
        Assert.Equal(hp * .3f, rig.Zone.Wear(rig.Rock, hp * .3f), 3);
        // The breaking hit absorbs only what the rock had left.
        Assert.Equal(hp * .7f, rig.Zone.Wear(rig.Rock, hp * 5f), 3);
        Assert.False(rig.Zone.ChunkExists(rig.Rock));
        Assert.Equal(0f, rig.Zone.Wear(rig.Rock, hp));
    }

    // Units owed are absorbed/hitpoints x AsteroidYield(size): a rock pays its whole yield however many hits break it, so
    // the mean over many rocks does not depend on how the damage arrives.
    [Fact]
    public void ARockYieldsItsAuthoredTotalWhateverBreaksIt()
    {
        const int rocks = 300;
        var ore = Commodity("Rock");
        var kind = MiningCut3Tests.Kind("Asteroid", 4f);
        var scene = _scenes.BuildScene(new[] { kind }, MiningCut3Tests.Belt(kind,
            Enumerable.Range(0, rocks).Select(i => MiningCut3Tests.Rock(150f + i * .1f, i / (float) rocks, .3f)).ToArray()));
        Stock(scene, kind, (ore, 1f));
        var ship = new Ship(scene.Items, scene.Zone, MiningCut3Tests.Mint(scene, scene.Hull), new EntitySettings());
        scene.Zone.Admit(ship, piloted: false);
        var bay = Bay(scene, ship, 8, 8, "Hold");
        var settings = scene.Zone.Settings;
        var hp = settings.AsteroidHitpoints.Evaluate(.3f);
        var owed = settings.AsteroidYield.Evaluate(.3f);

        uint ordinal = 0;
        void Hit(int rock, float fraction) =>
            FireControl.Extract(scene.Zone, new ChunkId(scene.Belts[0], rock), scene.Zone.Wear(new ChunkId(scene.Belts[0], rock), hp * fraction),
                ship, SimulationDice.For(scene.Zone.DropSeed, SimulationDice.OreStream, ++ordinal));

        for (var rock = 0; rock < rocks / 2; rock++) Hit(rock, 2f);
        var wholeHits = Units(bay.Cargo.Keys.Concat(scene.Zone.FloatingItems.Select(f => f.Item)), "Rock", scene);
        for (var rock = rocks / 2; rock < rocks; rock++) for (var part = 0; part < 4; part++) Hit(rock, .25f);
        var total = Units(bay.Cargo.Keys.Concat(scene.Zone.FloatingItems.Select(f => f.Item)), "Rock", scene);
        var quartered = total - wholeHits;

        // owed = 3.2 per rock; 150 rocks: mean 480, sigma about 4.9 whole, 4.9 quartered.
        Assert.InRange(wholeHits, 480 - 30, 480 + 30);
        Assert.InRange(quartered, 480 - 30, 480 + 30);
        Assert.Equal(3.2f, owed, 3);
    }

    [Fact]
    public void ADirectHitPaysTheShooter()
    {
        var (rig, bay) = Mining(ores: new[] { (Commodity("Rock"), 1f), (Commodity("Gold"), 1f) });
        var yields = FieldKinds.Yield(rig.Zone.AsteroidBelts[rig.Rock.Field].Data.Kind, default, rig.Scene.Cache);
        Assert.Equal(2, yields.Length);
        Assert.Equal(1, Hits(rig));
        Assert.False(rig.Zone.ChunkExists(rig.Rock));
        Assert.Equal(4, Units(bay, "Rock", rig.Scene) + Units(bay, "Gold", rig.Scene));
        Assert.Empty(rig.Zone.FloatingItems);
    }

    [Fact]
    public void ABlastPaysTheShooter()
    {
        var (rig, bay) = Mining(fuse: WeaponFuse.Contact, ores: new[] { (Commodity("Rock"), 1f) });
        Hits(rig);
        Assert.False(rig.Zone.ChunkExists(rig.Rock));
        Assert.Equal(4, Units(bay, "Rock", rig.Scene));
    }

    [Fact]
    public void AMineWearsButYieldsNothing()
    {
        var (rig, bay) = Mining();
        var at = rig.Zone.ChunkPose(rig.Rock.Field, rig.Rock.Index).xy;
        FireControl.Detonate(rig.Zone, at, 6f, 1000f, DamageType.Kinetic);
        Assert.False(rig.Zone.ChunkExists(rig.Rock));
        Assert.Empty(bay.Cargo);
        Assert.Empty(rig.Zone.FloatingItems);
    }

    // Ruling mining-overflow-floats: what the hold cannot take floats at the rock, where another ship can pick it up.
    [Fact]
    public void AFullHoldReleasesTheOverflow()
    {
        var grit = Commodity("Grit", maxStack: 2);
        var (rig, bay) = Mining(bayWidth: 1, bayHeight: 1, ores: new[] { (grit, 1f) });
        Hits(rig);

        Assert.Equal(2, Units(bay, "Grit", rig.Scene));
        var floating = Assert.Single(rig.Zone.FloatingItems);
        Assert.Equal(2, Assert.IsType<SimpleCommodity>(floating.Item).Quantity);
        var rock = rig.Zone.ChunkPose(rig.Rock.Field, rig.Rock.Index).xy;
        Assert.True(length(floating.Body.Position - rock) < 1f, "the overflow leaves from the rock");

        var taker = new Ship(rig.Scene.Items, rig.Zone, MiningCut3Tests.Mint(rig.Scene, rig.Scene.Hull), new EntitySettings());
        rig.Zone.Admit(taker, piloted: false);
        var takerBay = Bay(rig.Scene, taker, 4, 4, "Taker");
        Assert.True(rig.Zone.TryPickUp(taker, floating.Id));
        Assert.Equal(2, Units(takerBay, "Grit", rig.Scene));
    }

    [Fact]
    public void AGoneShooterReleasesEverything()
    {
        var (rig, bay) = Mining(velocity: 50f);
        FireControl.Fire(rig.Gun, rig.GunItem, rig.Shooter);
        rig.Shooter.HeatstrokeDeath.OnNext(UniRx.Unit.Default);
        MiningCut4bTests.Run(rig, 3f);

        Assert.Empty(bay.Cargo);
        Assert.Equal(4, Units(rig.Zone.FloatingItems.Select(f => f.Item), "Rock", rig.Scene));
    }

    // Two zones of one pack share every key, so the same rocks wear in each: the ore is a function of the dice alone.
    private (MiningCut3Tests.Scene Scene, Func<Zone> NewZone, Ship Shooter) Field(int rocks, float size, float variance = .5f, float yield = 4f)
    {
        var kind = MiningCut3Tests.Kind("Asteroid", 4f);
        var scene = _scenes.BuildScene(new[] { kind }, MiningCut3Tests.Belt(kind,
            Enumerable.Range(0, rocks).Select(i => MiningCut3Tests.Rock(150f + i, i / (float) rocks, size)).ToArray()));
        Stock(scene, kind, (Commodity("Rock"), 1f), (Commodity("Gold"), 1f));
        var ship = new Ship(scene.Items, scene.Zone, MiningCut3Tests.Mint(scene, scene.Hull), new EntitySettings());
        Zone NewZone()
        {
            var settings = MiningCut3Tests.PlanetSettings();
            settings.ChunkCompositionVariance = variance;
            settings.AsteroidYield = new ExponentialLerp { Minimum = yield, Maximum = yield, Exponent = 1f };
            return new Zone(scene.Items, settings, scene.Pack, new GalaxyZone { Name = "MiningCut3", Owner = null }, null);
        }
        return (scene, NewZone, ship);
    }

    // Every rock broken once; the shooter is not in the zone, so everything floats and the floats are the record.
    private static (int Rock, int Gold)[] Break(MiningCut3Tests.Scene scene, Zone zone, Ship ship, int rocks, uint seed) =>
        Enumerable.Range(0, rocks).Select(rock =>
        {
            var chunk = new ChunkId(scene.Belts[0], rock);
            var before = zone.FloatingItems.Select(f => f.Item).ToArray();
            FireControl.Extract(zone, chunk, zone.Wear(chunk, 1e6f), ship, SimulationDice.For(seed, SimulationDice.OreStream, (uint) rock));
            var added = zone.FloatingItems.Select(f => f.Item).Except(before).ToArray();
            return (Units(added, "Rock", scene), Units(added, "Gold", scene));
        }).ToArray();

    [Fact]
    public void TheSameShotGivesTheSameOre()
    {
        var (scene, newZone, ship) = Field(20, .5f);
        var first = Break(scene, newZone(), ship, 20, 11);
        Assert.Equal(first, Break(scene, newZone(), ship, 20, 11));
        Assert.NotEqual(first, Break(scene, newZone(), ship, 20, 12));
    }

    // Composition is derived from the rock's identity every call: variance 0 reads the kind's ratio, variance above 0 gives
    // rocks different mixes, and nothing stores it (a second zone of the pack reads the same mixes from the dice and keys).
    [Fact]
    public void CompositionVariesByRockAndIsDerived()
    {
        var (scene, even, ship) = Field(12, 1f, variance: 0f, yield: 600f);
        // Equal weights, 600 units a rock: even splits within sampling noise (sigma about 12).
        Assert.All(Break(scene, even(), ship, 12, 5), m => Assert.InRange(m.Rock, 240, 360));

        var (scene2, varied, ship2) = Field(12, 1f, variance: 2f, yield: 600f);
        var mixes = Break(scene2, varied(), ship2, 12, 5).Select(m => m.Rock).ToArray();
        Assert.True(mixes.Max() - mixes.Min() > 100, "rocks differ in mix when variance is above zero");
        Assert.Equal(mixes, Break(scene2, varied(), ship2, 12, 5).Select(m => m.Rock));
    }
}
