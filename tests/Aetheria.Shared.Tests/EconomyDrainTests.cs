using System;
using System.IO;
using System.Linq;
using Xunit;

// The station-drain stage of `economy balance --drain` (EconomyDrain) on the demo run of seed 1: the shipped refinery stalls Room the
// moment a sale fills it, the trade hub's silo and stock bay take ore apart, and the window by steps agrees with the window by spans.
public sealed class EconomyDrainTests
{
    private static string Root => RunStartTests.FindRepoRoot();
    private const int SortieUnits = 131;

    private static string Printed(DrainSample sample)
    {
        var text = new StringWriter();
        EconomyDrain.Print(sample, text);
        return text.ToString();
    }

    private static StationDrain Station(DrainSample sample, string role) => sample.Stations.Single(station => station.Role == role);

    [Fact]
    public void DrainIsDeterministic()
    {
        var first = Printed(EconomyDrain.Run(Root, 1, 30, 2, SortieUnits));
        var second = Printed(EconomyDrain.Run(Root, 1, 30, 2, SortieUnits));
        Assert.Equal(first, second);
        Assert.Contains("Station drain, seed 1", first);
    }

    // The sortie rig after Boot was lifted out of it, on the shipped catalog, read at the commit before the extraction.
    [Fact]
    public void SortieOutputUnchangedByBoot()
    {
        var sample = EconomyLoop.Sortie(Root, 1, 0, 150);
        Assert.Equal((131, 73, 58), (sample.Units, sample.UnitsSold, sample.UnitsRefused));
        Assert.Equal(1295, sample.SellValue);
        Assert.Equal(150, sample.SimSeconds, 3);
    }

    [Fact]
    public void WindowAgreesWithSpans()
    {
        var sample = EconomyDrain.Run(Root, 1, 120, 1, SortieUnits);
        Assert.All(sample.Stations, station => Assert.Empty(station.WindowDisagreements));
        Assert.DoesNotContain(EconomyDrain.Report(new[] { sample }), fault => fault.Invariant == EconomyDrain.HSim);

        // A facility that does not run on screen while the spans run it: the held counts part.
        var mutant = EconomyDrain.Run(Root, 1, 120, 1, SortieUnits, beforeWindow: demo =>
        {
            foreach (var unit in demo.Processing.Equipment.Where(item => item.Behaviors.OfType<Facility>().Any())) unit.Enabled.Value = false;
        });
        Assert.NotEmpty(Station(mutant, "processing").WindowDisagreements);
        Assert.Contains(EconomyDrain.Report(new[] { mutant }), fault => fault.Invariant == EconomyDrain.HSim && fault.Record == "seed 1 processing");
    }

    [Fact]
    public void AFullRefineryIsAStallFault()
    {
        var shipped = EconomyDrain.Run(Root, 1, 30, 4, SortieUnits);
        var refinery = Station(shipped, "processing");
        Assert.True(refinery.Filled);
        Assert.Equal(Stall.Room, refinery.Facilities.Single().HorizonEnd);
        Assert.NotNull(refinery.FirstRoomStall);
        var fault = Assert.Single(EconomyDrain.Report(new[] { shipped }), f => f.Invariant == EconomyDrain.HStall);
        Assert.Contains("Room", fault.Message);

        // The sale fills every cell: a bay emptied before the fill is filled again by the next sortie. With the starting gear put
        // away after the fill (its gear, eight cells) there is room for outputs: the ore drains to nothing, the block reports how long that takes, and the stall is Inputs, not a fault.
        var made = EconomyDrain.Run(Root, 1, 30, 24, SortieUnits, beforeWindow: demo =>
        {
            foreach (var bay in demo.Processing.CargoBays)
                foreach (var gear in bay.Cargo.Keys.Where(item => !(item is ICommodityStack)).ToList()) bay.Remove(gear);
        });
        var roomy = Station(made, "processing");
        Assert.Contains(roomy.Outputs, pile => pile.Hourly.Any(units => units > 0));
        Assert.NotNull(roomy.Ores[0].EmptyAt);
        Assert.Null(roomy.FirstRoomStall);
        Assert.Equal(Stall.Inputs, roomy.Facilities.Single().HorizonEnd);
        Assert.DoesNotContain(EconomyDrain.Report(new[] { made }), f => f.Invariant == EconomyDrain.HStall);
    }

[Fact]    public void DrainRefusesLoopAndStrayOptions()    {        foreach (var args in new[] { new[] { "balance", "--drain", "--loop" }, new[] { "balance", "--loop", "1", "--window", "5" }, new[] { "balance", "--drain", "--cap", "5" }, new[] { "balance", "--drain", "--horizon", "0" }, new[] { "balance", "--drain", "--window" } })        {            var output = new StringWriter();            Assert.Equal(1, EconomyCommand.Run(args, output));            Assert.Contains("balance --drain [<seed>...]", output.ToString());        }    }
    [Fact]
    public void IntakeSplitsSiloAndStock()
    {
        var sample = EconomyDrain.Run(Root, 1, 20, 1, SortieUnits);
        var zenith = Station(sample, "trade-hub");
        var silo = zenith.Bays.Single(bay => bay.Silo);
        var stock = zenith.Bays.Single(bay => !bay.Silo);
        Assert.Equal(("Nickel-Iron", 640), silo.Capacity.First(c => c.Ore == "Nickel-Iron"));
        Assert.Equal(("Nickel-Iron", 80), stock.Capacity.First(c => c.Ore == "Nickel-Iron"));
        Assert.True(silo.Taken > 0 && stock.Taken > 0);
        Assert.Equal(zenith.Sorties.Sum(sale => sale.Sold), silo.Taken + stock.Taken);
    }
}
