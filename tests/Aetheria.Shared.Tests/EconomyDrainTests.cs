using System;
using System.IO;
using System.Linq;
using GameCult.Caching;
using Xunit;

// The station-drain stage of `economy balance --drain` (EconomyDrain) on the demo run of seed 1: the shipped refinery stalls Room the
// moment a sale fills it, the trade hub's silo and stock bay take ore apart, and the window by steps agrees with the window by spans.
public sealed class EconomyDrainTests
{
    private static string Root => RunStartTests.FindRepoRoot();

    // The base loop's sorties of seed 1, mined once: the one belt of the seed, capped, 131 units of mixed ore.
    private static readonly Lazy<LoopSample[]> Seed1 = new Lazy<LoopSample[]>(() => EconomyLoop.Sorties(RunStartTests.FindRepoRoot(), 1, 150).ToArray());

    private static string Printed(DrainSample sample)
    {
        var text = new StringWriter();
        EconomyDrain.Print(sample, text);
        return text.ToString();
    }

    private static StationDrain Station(DrainSample sample, string role) => sample.Stations.Single(station => station.Role == role);

    // Room for outputs: the refinery's starting gear put away once the sale has filled the bay.
    private static void PutGearAway(DemoArena demo)
    {
        foreach (var bay in demo.Processing.CargoBays)
            foreach (var gear in bay.Cargo.Keys.Where(item => !(item is ICommodityStack)).ToList()) bay.Remove(gear);
    }

    [Fact]
    public void DrainIsDeterministic()
    {
        var first = Printed(EconomyDrain.Run(Root, 1, 30, 2, Seed1.Value));
        var second = Printed(EconomyDrain.Run(Root, 1, 30, 2, Seed1.Value));
        Assert.Equal(first, second);
        Assert.Contains("Station drain, seed 1", first);
    }

    // The sortie rig after Boot was lifted out of it, on the shipped catalog, read at the commit before the extraction.
    [Fact]
    public void SortieOutputUnchangedByBoot()
    {
        var sample = Seed1.Value.Single();
        Assert.Equal((131, 73, 58), (sample.Units, sample.UnitsSold, sample.UnitsRefused));
        Assert.Equal(1295, sample.SellValue);
        Assert.Equal(150, sample.SimSeconds, 3);
        Assert.Equal(131, sample.Mined.Sum(m => m.Units));
    }

    // A drain copy shares no object with the live station: EntitySerializer.Pack alone puts the live stacks in the pack, so a copy
    // made from it would move the original's units when it sells or takes (Soul s1, copy-isolation-unpinned).
    [Fact]
    public void ACopyNeverSharesObjectsWithTheLiveStation()
    {
        using var demo = EconomyLoop.Boot(Root, 1);
        var ore = demo.Cache.GetAll<SimpleCommodityData>().First(design => FieldKinds.IsOre(demo.Cache, design));
        Assert.True(demo.Zenith.CargoBays.Any(bay => bay.TryStore(new SimpleCommodity { Data = new CultRecordRef<ItemData>(demo.Cache.RefOf(ore).Key), Quantity = 5 })));
        var live = demo.Zenith.CargoBays.SelectMany(bay => bay.Cargo.Keys).ToList();
        Assert.Contains(live, item => item is ICommodityStack);

        var copy = EconomyDrain.Copy(demo, demo.Zenith);
        var copied = copy.CargoBays.SelectMany(bay => bay.Cargo.Keys).ToList();
        Assert.Equal(live.Count, copied.Count);
        Assert.DoesNotContain(copied, item => live.Any(held => ReferenceEquals(held, item)));

        long Units(Entity entity) => entity.CargoBays.SelectMany(bay => bay.Cargo.Keys.OfType<ICommodityStack>()).Sum(stack => (long) stack.Quantity);
        var before = Units(demo.Zenith);
        foreach (var bay in copy.CargoBays)
            foreach (var stack in bay.Cargo.Keys.OfType<ICommodityStack>().ToList()) bay.Remove((ItemInstance) stack);
        Assert.Equal(before, Units(demo.Zenith));
        Assert.Equal(0, Units(copy));
    }

    [Fact]
    public void WindowAgreesWithSpans()
    {
        // Long enough for the first cycle to end: Room is inside the window, not only the horizon (Soul s1, window-stall-vacuous).
        var sample = EconomyDrain.Run(Root, 1, 700, 1, Seed1.Value);
        Assert.All(sample.Stations, station => Assert.Empty(station.WindowDisagreements));
        Assert.DoesNotContain(EconomyDrain.Report(new[] { sample }), fault => fault.Invariant == EconomyDrain.HSim);
        Assert.True(Station(sample, "processing").Facilities.Single().WindowSteps.TryGetValue(Stall.Room, out var roomSteps) && roomSteps > 0);

        // A facility that does not run on screen while the spans run it: the held counts and the run part.
        var off = EconomyDrain.Run(Root, 1, 120, 1, Seed1.Value, beforeWindow: demo =>
        {
            foreach (var unit in demo.Processing.Equipment.Where(item => item.Behaviors.OfType<Facility>().Any())) unit.Enabled.Value = false;
        });
        Assert.NotEmpty(Station(off, "processing").WindowDisagreements);
        Assert.Contains(EconomyDrain.Report(new[] { off }), fault => fault.Invariant == EconomyDrain.HSim && fault.Record == "seed 1 processing");
    }

    // The screen one step ahead of the span path, which no held count sees: a facility with room has the same units held and a run
    // 0.02 s further on (Soul s1, hsim-blind-to-time-skew).
    [Fact]
    public void WindowSeesATimeSkew()
    {
        var even = EconomyDrain.Run(Root, 1, 120, 1, Seed1.Value, beforeWindow: PutGearAway);
        Assert.Empty(Station(even, "processing").WindowDisagreements);

        var skewed = EconomyDrain.Run(Root, 1, 120, 1, Seed1.Value, beforeWindow: PutGearAway, onStep: (demo, step) =>
        {
            if (step == 0) demo.Arena.Update(EconomyLoop.Step);
        });
        var parted = Station(skewed, "processing").WindowDisagreements;
        Assert.Single(parted);
        Assert.Contains("run", parted[0]);
        Assert.Contains(EconomyDrain.Report(new[] { skewed }), fault => fault.Invariant == EconomyDrain.HSim);
    }

    [Fact]
    public void AFullRefineryIsAStallFault()
    {
        var shipped = EconomyDrain.Run(Root, 1, 30, 4, Seed1.Value);
        var refinery = Station(shipped, "processing");
        Assert.True(refinery.Filled);
        Assert.Equal(Stall.Room, refinery.Facilities.Single().HorizonEnd);
        Assert.NotNull(refinery.FirstRoomStall);
        var fault = Assert.Single(EconomyDrain.Report(new[] { shipped }), f => f.Invariant == EconomyDrain.HStall);
        Assert.Contains("Room", fault.Message);

        // The sale fills every cell: a bay emptied before the fill is filled again by the next sortie. With the starting gear put
        // away after the fill (eight cells) there is room for outputs: the ore drains to nothing, the block reports how long that
        // takes, and the stall is Inputs, not a fault.
        var made = EconomyDrain.Run(Root, 1, 30, 24, Seed1.Value, beforeWindow: PutGearAway);
        var roomy = Station(made, "processing");
        Assert.Contains(roomy.Outputs, pile => pile.Hourly.Any(units => units > 0));
        Assert.NotNull(roomy.Ores[0].EmptyAt);
        Assert.Null(roomy.FirstRoomStall);
        Assert.Equal(Stall.Inputs, roomy.Facilities.Single().HorizonEnd);
        Assert.DoesNotContain(EconomyDrain.Report(new[] { made }), f => f.Invariant == EconomyDrain.HStall);
    }

    // A sortie the base loop faults is a fault of its own and no sortie of the fill count (Soul s1, fill-count-artifact).
    [Fact]
    public void AFaultedSortieIsReportedAndLeftOutOfTheFill()
    {
        var mined = Seed1.Value.Single().Mined;
        var ammoOut = new LoopSample { Seed = 1, Belt = 0, Belts = 2, BeltKind = "Carbonaceous", Outcome = LoopOutcome.AmmoOut, SimSeconds = 12.4, Units = 18, Mined = mined };
        var rig = new LoopSample { Seed = 1, Belt = 1, Belts = 2, BeltKind = "Metallic", Outcome = LoopOutcome.RigFault, Note = "no rock of the belt could be targeted" };

        var none = EconomyDrain.Run(Root, 1, 20, 1, new[] { ammoOut, rig });
        Assert.Equal(0, none.Usable);
        Assert.Equal(2, none.Excluded.Count);
        Assert.All(none.Stations, station => Assert.Empty(station.Sorties));
        Assert.Equal(2, EconomyDrain.Report(new[] { none }).Count(fault => fault.Invariant == EconomyDrain.HSortie));
        Assert.Contains("sorties usable 0 of 2", Printed(none));

        var some = EconomyDrain.Run(Root, 1, 20, 1, new[] { ammoOut }.Concat(Seed1.Value).ToArray());
        Assert.Equal(1, some.Usable);
        Assert.Single(EconomyDrain.Report(new[] { some }), fault => fault.Invariant == EconomyDrain.HSortie);
        Assert.Equal(EconomyDrain.Run(Root, 1, 20, 1, Seed1.Value).Stations[1].Sorties.Count, some.Stations[1].Sorties.Count);
    }

    [Fact]
    public void DrainRefusesLoopAndStrayOptions()
    {
        var refused = new[]
        {
            new[] { "balance", "--drain", "--loop" },
            new[] { "balance", "--loop", "1", "--window", "5" },
            new[] { "balance", "--drain", "--cap", "5" },
            new[] { "balance", "--drain", "--horizon", "0" },
            new[] { "balance", "--drain", "--window" },
            new[] { "balance", "--loop", "1", "--cap", "1e39" }
        };
        foreach (var args in refused)
        {
            var output = new StringWriter();
            Assert.Equal(1, EconomyCommand.Run(args, output));
            Assert.Contains("balance --drain [<seed>...]", output.ToString());
        }
    }

    [Fact]
    public void IntakeSplitsSiloAndStock()
    {
        var sample = EconomyDrain.Run(Root, 1, 20, 1, Seed1.Value);
        var zenith = Station(sample, "trade-hub");
        var silo = zenith.Bays.Single(bay => bay.Silo);
        var stock = zenith.Bays.Single(bay => !bay.Silo);
        Assert.Equal(("Nickel-Iron", 640), silo.Capacity.First(c => c.Ore == "Nickel-Iron"));
        Assert.Equal(("Nickel-Iron", 80), stock.Capacity.First(c => c.Ore == "Nickel-Iron"));
        // Onward-haul ore goes to the silo only (ore-haul-run): the stock bay holds room for it but takes none.
        Assert.True(silo.Taken > 0);
        Assert.Equal(0, stock.Taken);
        Assert.Equal(zenith.Sorties.Sum(sale => sale.Sold), silo.Taken);
        Assert.Equal(1, sample.Parked);
    }
}
