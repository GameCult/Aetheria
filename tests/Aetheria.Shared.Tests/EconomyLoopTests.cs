using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

// The player-loop section of `economy balance --loop` (EconomyLoop): rates from hand samples, minutes to afford, the ladder band and
// the H faults, on fixture catalogs whose numbers are solved by hand. The sortie itself runs on the shipped catalog.
public sealed class EconomyLoopTests
{
    private static LoopSample Sample(uint seed, int sell, int ammo, int repair, double seconds, LoopOutcome outcome = LoopOutcome.Capped) =>
        new LoopSample { Seed = seed, Belts = 1, BeltKind = "belt", Weapons = "gun", SimSeconds = seconds, SellValue = sell, AmmoSpent = ammo, Repair = repair, Outcome = outcome, Note = "why" };

    private static GameplaySettings Settings(float min = 0, float max = 0, int purse = 100000, float sortiesMin = 0, float sortiesMax = 0) =>
        new GameplaySettings { StartingCredits = purse, EconomyLadderMin = min, EconomyLadderMax = max, EconomySortiesMin = sortiesMin, EconomySortiesMax = sortiesMax };

    // A gear in `hardpoint` offered at `price`, with a recipe so it is an offered design.
    private static void Offer(EconomyFixture f, string id, HardpointType hardpoint, int price, int plates = 1)
    {
        var key = f.Gear(id, new[] { ("Plate", plates) });
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
        // A design priced exactly at the purse is covered.
        report = EconomyLoop.Report(f.Cache, Settings(purse: 600), new[] { Sample(1, 1000, 0, 0, 60) });
        Assert.True(Ladder(report, "Gear/Tool").Designs.Single(d => d.Key == "gear:cannon").Covered);
    }

    [Fact]
    public void ABandWithOnlyOneSideFaultsOnlyThatSide()
    {
        using var f = EconomyFixture.Healthy();
        Offer(f, "cheap", HardpointType.Reactor, 10);
        Offer(f, "dear", HardpointType.Reactor, 1000);
        var samples = new[] { Sample(1, 1000, 0, 0, 60) };
        // 100x against a floor of 10 alone, and against a ceiling of 30 alone.
        Assert.DoesNotContain(EconomyLoop.Report(f.Cache, Settings(10, 0), samples).Faults, x => x.Record == "Gear/Reactor");
        Assert.Contains(EconomyLoop.Report(f.Cache, Settings(0, 30), samples).Faults, x => x.Record == "Gear/Reactor");
        Assert.Contains(EconomyLoop.Report(f.Cache, Settings(200, 0), samples).Faults, x => x.Record == "Gear/Reactor");
        Assert.DoesNotContain(EconomyLoop.Report(f.Cache, Settings(0, 200), samples).Faults, x => x.Record == "Gear/Reactor");
    }

    // Ruling ladder-band-weapons-and-gear: hulls, bays, consumables and control modules carry no tier band.
    [Fact]
    public void TheLadderBandCoversWeaponsAndGearOnly()
    {
        using var f = EconomyFixture.Healthy();
        Offer(f, "cheap", HardpointType.Reactor, 10);
        Offer(f, "dear", HardpointType.Reactor, 1000);
        var hull = f.Hull("big", HullType.Ship, new[] { ("Plate", 1) });
        f.EditItem<HullData>(hull, h => h.Price = 100000);
        var report = EconomyLoop.Report(f.Cache, Settings(10, 30), new[] { Sample(1, 1000, 0, 0, 60) });
        Assert.True(Ladder(report, "Hull/Ship").Width > 30);
        Assert.Equal(new[] { "Gear/Reactor" }, report.Faults.Where(x => x.Invariant == EconomyLoop.HLadder).Select(x => x.Record).ToArray());
    }

    // Rulings top-tier-per-item and top-gear-within-one-region-run: about 10-20 sorties at the median net buys the dearest item.
    [Fact]
    public void TheAffordBandFaultsADearestDesignOutsideTenToTwentySorties()
    {
        using var f = EconomyFixture.Healthy();
        Offer(f, "cheap", HardpointType.Reactor, 10);
        Offer(f, "top", HardpointType.Reactor, 1500);
        // One sortie nets 100: the top design takes 15 sorties.
        var samples = new[] { Sample(1, 100, 0, 0, 60) };
        var report = EconomyLoop.Report(f.Cache, Settings(sortiesMin: 10, sortiesMax: 20), samples);
        Assert.Equal(15, Ladder(report, "Gear/Reactor").TopSorties.Value, 9);
        Assert.DoesNotContain(report.Faults, x => x.Invariant == EconomyLoop.HAfford);
        Assert.Contains(EconomyLoop.Report(f.Cache, Settings(sortiesMin: 16, sortiesMax: 20), samples).Faults, x => x.Invariant == EconomyLoop.HAfford && x.Record == "Gear/Reactor");
        var low = Assert.Single(EconomyLoop.Report(f.Cache, Settings(sortiesMin: 10, sortiesMax: 14), samples).Faults, x => x.Invariant == EconomyLoop.HAfford);
        Assert.Contains("1 measured sortie", low.Message);
        Assert.DoesNotContain(EconomyLoop.Report(f.Cache, Settings(), samples).Faults, x => x.Invariant == EconomyLoop.HAfford);
    }

    // The shipped bands on the shipped catalog: the ballistic ladder and its top gun sit inside them, on the one measured sortie
    // (seed 1, Metallic belt: sell 1734, repair 183, 150 s) the harness has. Kills a bill trimmed out of the ruled band.
    [Fact]
    public void TheBallisticLadderAndItsTopGunSitInsideTheShippedBands()
    {
        using var db = AetherDb.OpenShipped();
        var settings = new GameplaySettings();
        Assert.Equal((10f, 30f, 10f, 20f), (settings.EconomyLadderMin, settings.EconomyLadderMax, settings.EconomySortiesMin, settings.EconomySortiesMax));
        var report = EconomyLoop.Report(db.Cache, settings, new[] { Sample(1, 1734, 0, 183, 150) });
        var ladder = Ladder(report, "Weapon/ExplosivelyPropelled");
        Assert.InRange(ladder.Width.Value, 10, 30);
        Assert.InRange(ladder.TopSorties.Value, 10, 20);
        Assert.DoesNotContain(report.Faults, x => x.Record == "Weapon/ExplosivelyPropelled");
    }

    [Fact]
    public void UnsetLadderBandRaisesNoLadderFault()
    {
        using var f = EconomyFixture.Healthy();
        Offer(f, "cheap", HardpointType.Reactor, 10);
        Offer(f, "dear", HardpointType.Reactor, 1000);
        // A settings object nobody tuned: its band is what the field defaults to.
        var report = EconomyLoop.Report(f.Cache, Settings(), new[] { Sample(1, 1000, 0, 0, 60) });
        Assert.Equal(100, Ladder(report, "Gear/Reactor").Width.Value, 9);
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
        Assert.Null(Ladder(report, "Gear/Sensors").Width);
        Assert.Null(Ladder(report, "Gear/Energy").Width);
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
        Assert.Equal(new[] { "seed 1 belt 1 of 1", "seed 2 belt 1 of 1" }, faults);
    }

    [Fact]
    public void AmmoOutAndRigFaultSortiesAreOutcomesNotIdleIncome()
    {
        using var f = EconomyFixture.Healthy();
        var report = EconomyLoop.Report(f.Cache, Settings(), new[]
        {
            Sample(1, 1000, 0, 0, 60), Sample(2, 3000, 0, 0, 60),
            // Strongly negative if they were folded in; neither is a rate.
            Sample(3, 10, 500, 0, 12, LoopOutcome.AmmoOut), Sample(4, 0, 0, 4, 1, LoopOutcome.RigFault)
        });
        Assert.Equal(2000, report.Median, 9);
        Assert.Equal(3000, report.Best, 9);
        Assert.Equal(new[] { "H-ammo", "H-rig" }, report.Faults.Select(x => x.Invariant).OrderBy(x => x, StringComparer.Ordinal).ToArray());
        Assert.DoesNotContain(report.Faults, x => x.Invariant == EconomyLoop.HLoop);
    }

    [Fact]
    public void WithNoMeasuredSortieThereIsNoRateAndNoMinutes()
    {
        using var f = EconomyFixture.Healthy();
        var report = EconomyLoop.Report(f.Cache, Settings(), new[] { Sample(3, 10, 500, 0, 12, LoopOutcome.AmmoOut) });
        Assert.Equal(0, report.Median);
        Assert.All(report.Ladders.SelectMany(l => l.Designs), d => Assert.Null(d.MinutesAtMedian));
    }

    // Kills: Prices leaving out a design that has a recipe, or pricing one that has none.
    [Fact]
    public void PricesCoverEveryDesignWithARecipe()
    {
        using var f = EconomyFixture.Healthy();
        var prices = EconomyBalance.Prices(f.Cache, out _);
        Assert.Equal(new[] { "commodity:plate", "gear:laser", "hull:skiff" }, prices.Keys.OrderBy(k => k, StringComparer.Ordinal).ToArray());
        // Laser takes two plates and skiff one, with no labour on either recipe.
        Assert.Equal(2 * prices["commodity:plate"], prices["gear:laser"], 9);
        Assert.Equal(prices["commodity:plate"], prices["hull:skiff"], 9);
        f.Gear("bare", new (string, int)[0], recipe: false);
        Assert.DoesNotContain("gear:bare", EconomyBalance.Prices(f.Cache, out _).Keys);
    }

    private static string Root => RunStartTests.FindRepoRoot();

    // The price the catalog authors for a simple commodity of that name.
    private static int PriceOf(string name)
    {
        using var db = AetherDb.OpenShipped();
        return db.Cache.GetAll<SimpleCommodityData>().Single(c => c.Name == name).Price;
    }

    [Fact]
    public void SortieLeavesTheCatalogAndMinesSomething()
    {
        var catalog = Path.Combine(Root, "GameData", "Aetheria.cc");
        var before = System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(catalog));
        var sample = EconomyLoop.Sortie(Root, 1, 0, 60);
        Assert.True(sample.Units > 0);
        Assert.True(sample.Rocks > 0);
        Assert.Equal(60, sample.SimSeconds, 1);
        Assert.Equal(LoopOutcome.Capped, sample.Outcome);
        Assert.Equal(before, System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(catalog)));
    }

    // Follow-up sortie-determinism-pin: a seed fixes the sortie. Each call generates its own run store, so the keys the cache
    // mints differ between the two; ore, target order and rock meshes must not read them (zone-body-sites).
    [Fact]
    public void ASortieIsAFunctionOfItsSeed()
    {
        var first = EconomyLoop.Sortie(Root, 1, 0, 60);
        var second = EconomyLoop.Sortie(Root, 1, 0, 60);
        Assert.Equal(first.BeltKind, second.BeltKind);
        Assert.Equal(first.Weapons, second.Weapons);
        Assert.Equal(first.Outcome, second.Outcome);
        Assert.Equal(first.Rocks, second.Rocks);
        Assert.Equal(first.Units, second.Units);
        Assert.Equal(first.HoldFullAt, second.HoldFullAt);
        Assert.Equal(first.SellValue, second.SellValue);
        Assert.Equal(first.AmmoSpent, second.AmmoSpent);
        Assert.Equal(first.Repair, second.Repair);
        Assert.Equal(first.SimSeconds, second.SimSeconds);
    }

    [Fact]
    public void AFullHoldSortieIsPricedFromTheShipItEndedWith()
    {
        int ore = -1, repair = -1;
        var sample = EconomyLoop.Sortie(Root, 1, 0, 300, (ship, items, processing) =>
        {
            ore = StationServices.OreValue(items, ship.CargoBays, processing);
            repair = StationServices.RepairCost(items, ship);
        });
        Assert.Equal(LoopOutcome.Full, sample.Outcome);
        Assert.True(sample.HoldFullAt > 0 && sample.HoldFullAt <= sample.SimSeconds);
        Assert.True(sample.SellValue > 0);
        // The station pays for the units its bays took: what it would pay for all of them, less what it refused for want of room.
        Assert.Equal(ore, sample.OfferedValue);
        Assert.True(sample.SellValue <= sample.OfferedValue);
        Assert.Equal(sample.Units, sample.UnitsSold + sample.UnitsRefused);
        if (sample.UnitsRefused == 0) Assert.Equal(sample.OfferedValue, sample.SellValue);
        Assert.True(sample.Repair > 0);
        Assert.Equal(repair, sample.Repair);
        // Laser fit: no ammunition is spent.
        Assert.Equal(0, sample.AmmoSpent);
    }

    [Fact]
    public void ASortieThatSpendsItsStarterAmmunitionEndsAmmoOutAndPaysForIt()
    {
        var ammoLeft = -1;
        var sample = EconomyLoop.Sortie(Root, 2, 0, 150, (ship, items, _) =>
            ammoLeft = ship.CargoBays.SelectMany(bay => bay.Cargo.Keys.OfType<SimpleCommodity>()).Where(c => items.GetData(c).Name == "Ammo").Sum(c => c.Quantity));
        Assert.Equal(LoopOutcome.AmmoOut, sample.Outcome);
        Assert.Equal(0, ammoLeft);
        // The Cell Blaster fit starts with 3 Ammo, bought back at the catalog's price.
        Assert.Equal(3 * PriceOf("Ammo"), sample.AmmoSpent);
        Assert.True(sample.SimSeconds < 150);
    }

    [Fact]
    public void EveryBeltOfTheZoneIsItsOwnSampleAndAnUnlitOneIsARigFault()
    {
        var samples = EconomyLoop.Sorties(Root, 2, 5);
        Assert.Equal(2, samples.Count);
        Assert.All(samples, x => Assert.Equal(2, x.Belts));
        Assert.Equal(new[] { 0, 1 }, samples.Select(x => x.Belt).ToArray());
        Assert.NotEqual(samples[0].BeltKind, samples[1].BeltKind);
        // Seed 2's second belt is in darkness: no rock can be targeted, and that is not income.
        Assert.Equal(LoopOutcome.RigFault, samples[1].Outcome);
        Assert.Equal(0, samples[1].Units);
    }

    [Fact]
    public void ANonFiniteOrNonPositiveCapIsRefused()
    {
        foreach (var cap in new[] { float.NaN, 0f, -1f, float.PositiveInfinity })
            Assert.Throws<ArgumentOutOfRangeException>(() => EconomyLoop.Sortie(Root, 1, 0, cap));
        foreach (var word in new[] { "NaN", "0", "-1", "Infinity" })
        {
            var output = new StringWriter();
            Assert.Equal(1, EconomyCommand.Run(new[] { "balance", "--loop", "1", "--cap", word }, output));
            Assert.Contains("balance [--loop", output.ToString());
        }
    }

    [Fact]
    public void BalanceWithoutLoopIsUnchanged()
    {
        // The committed output of economy balance at the commit before --loop existed, on the shipped catalog.
        var golden = File.ReadAllText(Path.Combine(Root, "tests", "Aetheria.Shared.Tests", "Golden", "economy-balance-base.txt")).Replace("\r\n", "\n");
        var output = new StringWriter();
        EconomyCommand.Run(new[] { "balance" }, output);
        Assert.Equal(golden, output.ToString().Replace("\r\n", "\n"));
    }

    [Fact]
    public void BalanceLoopPrintsTheLoopAndTheLaddersAndLetsTheirFaultsFailTheCommand()
    {
        using var db = AetherDb.OpenShipped();
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
