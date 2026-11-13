using System;
using System.Collections.Generic;
using System.Linq;
using CultMath;
using GameCult.Caching;
using UniRx;
using Xunit;

// Zone.CargoGained and HaulFeed (aetheria-release mining 6): Extract and TryPickUp publish what entered a hold, once; the feed
// words it and counts the hold. Fixtures are Mining Cut 5a's rig.
public sealed class HaulFeedTests : IDisposable
{
    private readonly MiningCut5aTests _five = new MiningCut5aTests();
    private readonly MiningCut4bTests _rigs = new MiningCut4bTests();

    public void Dispose()
    {
        _five.Dispose();
        _rigs.Dispose();
    }

    // The 5a rig: a rock of 4 units, the shooter holding one bay of the given size; the event log records every gain.
    private (MiningCut4bTests.Rig Rig, EquippedCargoBay Bay, List<CargoGain> Gains) Mining(int w, int h, params (SimpleCommodityData, float)[] ores)
    {
        var kind = MiningCut3Tests.Kind("Asteroid", 20f);
        var rig = _rigs.Build(damage: 1000f, fuse: null, blastRadius: 0f, precision: 1000f, rockSize: .5f, velocity: 0f, kind: kind, galaxySeed: 0);
        MiningCut5aTests.Stock(rig.Scene, kind, ores);
        var bay = MiningCut5aTests.Bay(rig.Scene, rig.Shooter, w, h, "Hold");
        var gains = new List<CargoGain>();
        rig.Zone.CargoGained.Subscribe(gains.Add);
        return (rig, bay, gains);
    }

    private static CultMath.Random Dice(MiningCut4bTests.Rig rig, uint n) => SimulationDice.For(rig.Zone.DropSeed, SimulationDice.OreStream, n);

    private static void Break(MiningCut4bTests.Rig rig)
    {
        for (var shot = 0; shot < 40 && rig.Zone.ChunkExists(rig.Rock); shot++)
        {
            FireControl.Fire(rig.Gun, rig.GunItem, rig.Shooter);
            MiningCut4bTests.Run(rig, .05f);
        }
        Assert.False(rig.Zone.ChunkExists(rig.Rock));
    }

    [Fact]
    public void AnExtractionPublishesWhatWasStoredAndWhatFloated()
    {
        // Room for everything.
        var (rig, bay, gains) = Mining(4, 4, (MiningCut5aTests.Commodity("Rock"), 1f));
        Break(rig);
        var gain = Assert.Single(gains);
        Assert.Equal((GainKind.Mined, 4, 0), (gain.Kind, gain.Stored, gain.Adrift));
        Assert.Same(rig.Shooter, gain.Taker);
        Assert.Equal(4, MiningCut5aTests.Units(bay, "Rock", rig.Scene));
        Assert.NotEqual(0, gain.Lot);

        // A full hold: everything floats.
        var (full, fullBay, fullGains) = Mining(1, 1, (MiningCut5aTests.Commodity("Grit", maxStack: 1), 1f));
        Assert.True(fullBay.TryStore(new SimpleCommodity { Data = full.Scene.Cache.RefOf<ItemData>(full.Scene.Cache.GetByName<SimpleCommodityData>("Grit")), Quantity = 1 }));
        Break(full);
        var overflow = Assert.Single(fullGains);
        Assert.Equal((0, 4), (overflow.Stored, overflow.Adrift));
        Assert.Equal(4, MiningCut5aTests.Units(full.Zone.FloatingItems.Select(f => f.Item), "Grit", full.Scene));

        // A partial store: the hold takes two of four.
        var (part, partBay, partGains) = Mining(1, 1, (MiningCut5aTests.Commodity("Grit", maxStack: 2), 1f));
        Break(part);
        var split = Assert.Single(partGains);
        Assert.Equal((2, 2), (split.Stored, split.Adrift));
        Assert.Equal(2, MiningCut5aTests.Units(partBay, "Grit", part.Scene));
        Assert.Equal(2, MiningCut5aTests.Units(part.Zone.FloatingItems.Select(f => f.Item), "Grit", part.Scene));
    }

    [Fact]
    public void AGoneShooterPublishesEverythingAdrift()
    {
        var (rig, _, gains) = Mining(4, 4, (MiningCut5aTests.Commodity("Rock"), 1f));
        rig.Shooter.HeatstrokeDeath.OnNext(UniRx.Unit.Default);
        var at = rig.Zone.ChunkPose(rig.Rock.Field, rig.Rock.Index).xy;
        FireControl.Extract(rig.Zone, rig.Rock, rig.Zone.Wear(rig.Rock, 1e6f), rig.Shooter, Dice(rig, 1));
        var gain = Assert.Single(gains);
        Assert.Equal((0, 4), (gain.Stored, gain.Adrift));
    }

    [Fact]
    public void AMineAndANullShooterPublishNothing()
    {
        var (rig, _, gains) = Mining(4, 4, (MiningCut5aTests.Commodity("Rock"), 1f));
        FireControl.Extract(rig.Zone, rig.Rock, rig.Zone.Wear(rig.Rock, 1e6f), null, Dice(rig, 1));
        Assert.Empty(gains);
        Assert.Empty(rig.Zone.FloatingItems);
    }

    [Fact]
    public void AMergeIntoAnExistingStackStillPublishes()
    {
        var (rig, bay, gains) = Mining(4, 4, (MiningCut5aTests.Commodity("Rock"), 1f));
        var chunk = rig.Rock;
        FireControl.Extract(rig.Zone, chunk, rig.Zone.Wear(chunk, 1e6f), rig.Shooter, Dice(rig, 1));
        var keys = bay.Cargo.Keys.ToArray();
        // Same rock field and index, same ore, so the same lot: the second extraction merges and writes no Cargo key.
        FireControl.Extract(rig.Zone, chunk, 40f, rig.Shooter, Dice(rig, 2));
        Assert.Equal(keys, bay.Cargo.Keys.ToArray());
        Assert.Equal(2, gains.Count);
        Assert.Equal(gains[0].Lot, gains[1].Lot);
        Assert.True(gains[1].Stored > 0);
    }

    [Fact]
    public void EveryOreOfOneExtractionPublishesOnce()
    {
        var (rig, _, gains) = Mining(8, 8, (MiningCut5aTests.Commodity("Rock"), 1f), (MiningCut5aTests.Commodity("Gold"), 1f));
        rig.Zone.Settings.AsteroidYield = new ExponentialLerp { Minimum = 400f, Maximum = 400f, Exponent = 1f };
        FireControl.Extract(rig.Zone, rig.Rock, rig.Zone.Wear(rig.Rock, 1e6f), rig.Shooter, Dice(rig, 3));
        Assert.Equal(2, gains.Count);
        Assert.Equal(2, gains.Select(g => g.Lot).Distinct().Count());
        Assert.All(gains, g => Assert.True(g.Stored + g.Adrift > 0));
    }

    [Fact]
    public void APickupPublishesCollected()
    {
        var grit = MiningCut5aTests.Commodity("Grit", maxStack: 2);
        var (rig, _, gains) = Mining(1, 1, (grit, 1f));
        Break(rig);
        gains.Clear();
        var floating = rig.Zone.FloatingItems.First();
        var quantity = ((SimpleCommodity) floating.Item).Quantity;

        // A taker with no room is refused and publishes nothing.
        var packed = new Ship(rig.Scene.Items, rig.Zone, MiningCut3Tests.Mint(rig.Scene, rig.Scene.Hull), new EntitySettings());
        rig.Zone.Admit(packed, piloted: false);
        Assert.False(rig.Zone.TryPickUp(packed, floating.Id));
        Assert.Empty(gains);

        var taker = new Ship(rig.Scene.Items, rig.Zone, MiningCut3Tests.Mint(rig.Scene, rig.Scene.Hull), new EntitySettings());
        rig.Zone.Admit(taker, piloted: false);
        MiningCut5aTests.Bay(rig.Scene, taker, 4, 4, "Taker");
        Assert.True(rig.Zone.TryPickUp(taker, floating.Id));
        var gain = Assert.Single(gains);
        Assert.Equal((GainKind.Collected, quantity, 0), (gain.Kind, gain.Stored, gain.Adrift));
        Assert.Same(taker, gain.Taker);
    }

    [Fact]
    public void TheLineNamesWhatWasTakenAndWhatFloated()
    {
        var (rig, bay, _) = Mining(4, 4, (MiningCut5aTests.Commodity("Rock"), 1f));
        var items = rig.Scene.Items;
        var design = rig.Scene.Cache.RefOf<ItemData>(rig.Scene.Cache.GetByName<SimpleCommodityData>("Rock"));
        var fill = HaulFeed.HoldFill(rig.Shooter.CargoBays);
        var hold = $"hold {fill.used}/{fill.total}";

        var clean = HaulFeed.Line(new CargoGain { Taker = rig.Shooter, Design = design, Stored = 3, Kind = GainKind.Mined }, items);
        Assert.Contains("3", clean);
        Assert.Contains("Rock", clean);
        Assert.Contains(hold, clean);
        Assert.DoesNotContain("adrift", clean);

        var spill = HaulFeed.Line(new CargoGain { Taker = rig.Shooter, Design = design, Stored = 1, Adrift = 2, Kind = GainKind.Mined }, items);
        Assert.Contains("2 adrift", spill);
        Assert.Contains("Rock", spill);
        Assert.DoesNotContain("adrift", HaulFeed.Line(new CargoGain { Taker = rig.Shooter, Design = design, Stored = 2, Kind = GainKind.Collected }, items));
    }

    [Fact]
    public void HoldFillCountsOccupiedCellsAcrossBays()
    {
        var (rig, bay, _) = Mining(2, 2, (MiningCut5aTests.Commodity("Rock"), 1f));
        var second = MiningCut5aTests.Bay(rig.Scene, rig.Shooter, 3, 1, "Aft");
        Assert.Equal((0, 7), HaulFeed.HoldFill(new[] { bay, second }));
        var rock = rig.Scene.Cache.GetByName<SimpleCommodityData>("Rock");
        Assert.True(bay.TryStore(new SimpleCommodity { Data = rig.Scene.Cache.RefOf<ItemData>(rock), Quantity = 1 }));
        Assert.True(second.TryStore(new SimpleCommodity { Data = rig.Scene.Cache.RefOf<ItemData>(rock), Quantity = 1 }));
        Assert.Equal((2, 7), HaulFeed.HoldFill(new[] { bay, second }));
    }
}
