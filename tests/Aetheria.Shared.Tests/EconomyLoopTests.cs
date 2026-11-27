using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

// The player-loop section of `economy balance --loop` (EconomyLoop): rates from hand samples, minutes to afford, the ladder band and
// the H faults, on fixture catalogs whose numbers are solved by hand. The sortie itself runs on the shipped catalog.
public sealed class EconomyLoopTests
{
    private static LoopSample Sample(uint seed, int sell, int ammo, int repair, double seconds) =>
        new LoopSample { Seed = seed, BeltKind = "belt", Weapons = "gun", SimSeconds = seconds, SellValue = sell, AmmoSpent = ammo, Repair = repair };

    private static GameplaySettings Settings(float min = 0, float max = 0, int purse = 100000) =>
        new GameplaySettings { StartingCredits = purse, EconomyLadderMin = min, EconomyLadderMax = max };

    // A gear in `hardpoint` offered at `price`, with a recipe so it is an offered design.
    private static void Offer(EconomyFixture f, string id, HardpointType hardpoint, int price)
    {
        var key = f.Gear(id, new[] { ("Plate", 1) });
        f.EditItem<GearData>(key, g => { g.Hardpoint = hardpoint; g.Price = price; });
    }

    private static LoopLadder Ladder(LoopReport report, string category) => report.Ladders.Single(l => l.Category == category);

    [Fact]
    public void MinutesToAffordIsPriceOverRate()
    {
        using var f = EconomyFixture.Healthy();
        Offer(f, "cannon", HardpointType.Tool, 600);
        // Gross 1000 a minute, and net 150 a minute once ammunition and repair are paid: the rate is net, per sim-minute.
        var report = EconomyLoop.Report(f.Cache, Settings(), new[] { Sample(1, 1000, 0, 0, 60), Sample(2, 500, 100, 100, 120) });
        Assert.Equal(575, report.Median, 9);
        Assert.Equal(1000, report.Best, 9);
        var cannon = Ladder(report, "Gear/Tool").Designs.Single(d => d.Key == "gear:cannon");
        Assert.Equal(600 / 575.0, cannon.MinutesAtMedian.Value, 9);
        Assert.Equal(0.6, cannon.MinutesAtBest.Value, 9);
        Assert.True(cannon.Covered);
    }

    [Fact]
    public void ADesignDearerThanThePurseIsNotCovered()
    {
        using var f = EconomyFixture.Healthy();
        Offer(f, "cannon", HardpointType.Tool, 600);
        var report = EconomyLoop.Report(f.Cache, Settings(purse: 599), new[] { Sample(1, 1000, 0, 0, 60) });
        Assert.False(Ladder(report, "Gear/Tool").Designs.Single(d => d.Key == "gear:cannon").Covered);
    }

    [Fact]
    public void UnsetLadderBandRaisesNoLadderFault()
    {
        using var f = EconomyFixture.Healthy();
        Offer(f, "cheap", HardpointType.Reactor, 10);
        Offer(f, "dear", HardpointType.Reactor, 1000);
        // A settings object nobody tuned: its band is what the field defaults to.
        var report = EconomyLoop.Report(f.Cache, new GameplaySettings(), new[] { Sample(1, 1000, 0, 0, 60) });
        Assert.Equal(100, Ladder(report, "Gear/Reactor").WidthAuthored.Value, 9);
        Assert.DoesNotContain(report.Faults, x => x.Invariant == EconomyLoop.HLadder);
    }

    [Fact]
    public void LadderOutsideBandIsAFault()
    {
        using var f = EconomyFixture.Healthy();
        // 2x, 40x, 20x, and the two edges of the band, 10x and 30x; Sensors holds one design alone.
        foreach (var (hardpoint, low, high) in new[] { (HardpointType.Tool, 100, 200), (HardpointType.Thruster, 50, 2000), (HardpointType.Reactor, 100, 2000),
                     (HardpointType.Radiator, 100, 1000), (HardpointType.Ballistic, 100, 3000) })
        {
            Offer(f, hardpoint + "-low", hardpoint, low);
            Offer(f, hardpoint + "-high", hardpoint, high);
        }
        Offer(f, "Sensors-only", HardpointType.Sensors, 5000);
        // The fixture's own laser is a Tool at price 1: move it into the ladder it should not narrow.
        f.EditItem<GearData>("gear:laser", g => { g.Hardpoint = HardpointType.Energy; g.Price = 7; });
        var report = EconomyLoop.Report(f.Cache, Settings(10, 30), new[] { Sample(1, 1000, 0, 0, 60) });
        var faulted = report.Faults.Where(x => x.Invariant == EconomyLoop.HLadder).Select(x => x.Record).OrderBy(x => x, StringComparer.Ordinal).ToArray();
        Assert.Equal(new[] { "Gear/Thruster", "Gear/Tool" }, faulted);
        Assert.Null(Ladder(report, "Gear/Sensors").WidthAuthored);
        Assert.Null(Ladder(report, "Gear/Energy").WidthAuthored);
    }

    [Fact]
    public void ACategoryIsTheHullTypeTheWeaponTypeTheHardpointOrTheRecordType()
    {
        using var f = EconomyFixture.Healthy();
        Offer(f, "beam", HardpointType.Energy, 100);
        var gun = f.Cache.Get<ItemData>(new GameCult.Caching.CultRecordKey("gear:beam"));
        Assert.Equal("Gear/Energy", EconomyLoop.Category(gun));
        Assert.Equal("Hull/Ship", EconomyLoop.Category(f.Cache.Get<ItemData>(new GameCult.Caching.CultRecordKey("hull:skiff"))));
        Assert.Equal("Weapon/Laser", EconomyLoop.Category(new WeaponItemData { WeaponType = WeaponType.Laser, Hardpoint = HardpointType.Energy }));
        Assert.Equal("CargoBayData", EconomyLoop.Category(new CargoBayData()));
    }

    [Fact]
    public void NetLossSortieIsHLoop()
    {
        using var f = EconomyFixture.Healthy();
        // Gross positive in all three; net 0, -1 and 1: only the last earns anything.
        var report = EconomyLoop.Report(f.Cache, Settings(), new[] { Sample(1, 100, 60, 40, 60), Sample(2, 100, 60, 41, 60), Sample(3, 100, 60, 39, 60) });
        var faults = report.Faults.Where(x => x.Invariant == EconomyLoop.HLoop).Select(x => x.Record).OrderBy(x => x, StringComparer.Ordinal).ToArray();
        Assert.Equal(new[] { "seed 1", "seed 2" }, faults);
    }

    [Fact]
    public void DesignPricesOnlyWhenAsked()
    {
        using var f = EconomyFixture.Healthy();
        var plain = EconomyBalance.Prices(f.Cache, out _);
        Assert.Equal(new[] { "commodity:plate" }, plain.Keys.OrderBy(k => k, StringComparer.Ordinal).ToArray());
        var withDesigns = EconomyBalance.Prices(f.Cache, out _, designs: true);
        Assert.Equal(new[] { "commodity:plate", "gear:laser", "hull:skiff" }, withDesigns.Keys.OrderBy(k => k, StringComparer.Ordinal).ToArray());
        Assert.Equal(plain["commodity:plate"], withDesigns["commodity:plate"]);
        // Laser takes two plates and skiff one, with no labour on either recipe.
        Assert.Equal(2 * plain["commodity:plate"], withDesigns["gear:laser"], 9);
        Assert.Equal(plain["commodity:plate"], withDesigns["hull:skiff"], 9);
    }

    [Fact]
    public void SortieLeavesTheCatalogAndMinesSomething()
    {
        var root = RunStartTests.FindRepoRoot();
        var catalog = Path.Combine(root, "GameData", "Aetheria.cc");
        var before = System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(catalog));
        var sample = EconomyLoop.Sortie(root, 1, 60);
        Assert.True(sample.Units > 0);
        Assert.True(sample.Rocks > 0);
        Assert.Equal(60, sample.SimSeconds, 1);
        Assert.Equal(before, System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(catalog)));
    }

    [Fact]
    public void BalanceWithoutLoopIsUnchanged()
    {
        using var db = AetherDb.Open();
        var spread = AuthoredSettings.Load(db.Root).Read<GameplaySettings>("GameplaySettings").EconomyWageSpread;
        var expected = new StringWriter();
        var expectedCode = EconomyCommand.Balance(db.Cache, spread, expected);
        var output = new StringWriter();
        var code = EconomyCommand.Run(new[] { "balance" }, output);
        Assert.Equal(expectedCode, code);
        Assert.Equal(expected.ToString(), output.ToString());
        Assert.DoesNotContain("Player loop", output.ToString());
    }

    [Fact]
    public void BalanceLoopPrintsTheLoopAndTheLaddersAndLetsTheirFaultsFailTheCommand()
    {
        using var db = AetherDb.Open();
        var settings = AuthoredSettings.Load(db.Root).Read<GameplaySettings>("GameplaySettings");
        var report = EconomyLoop.Report(db.Cache, settings, new[] { Sample(7, 100, 100, 0, 60) });
        var plain = new StringWriter();
        EconomyCommand.Balance(db.Cache, settings.EconomyWageSpread, plain);
        var output = new StringWriter();
        Assert.Equal(1, EconomyCommand.Balance(db.Cache, settings.EconomyWageSpread, output, report));
        var text = output.ToString();
        Assert.StartsWith(plain.ToString().Split('\n')[0], text);
        Assert.Contains("H-loop (1)", text);
        Assert.Contains("Player loop (", text);
        Assert.Contains("Ladders (", text);
        Assert.Contains("  Gear/Reactor: ", text);
    }

    [Theory]
    [InlineData("--cap", "5")]
    [InlineData("--loop", "x")]
    [InlineData("--loop", "--cap")]
    [InlineData("--bogus")]
    public void BalanceRefusesWhatItCannotRead(params string[] words)
    {
        var output = new StringWriter();
        Assert.Equal(1, EconomyCommand.Run(new[] { "balance" }.Concat(words).ToArray(), output));
        Assert.Contains("balance [--loop", output.ToString());
    }
}
