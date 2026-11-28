using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using CultMath;
using GameCult.Caching;
using UniRx;

// The player loop of `economy balance --loop`: a measured mining sortie per seed, the income it implies, the minutes that income
// takes to afford each offered design, and the width of each offered gear ladder against a band. It measures and reports; it changes
// no price, yield or purse (rulings economy-numbers-are-sim-tuned-knobs, economy-tuned-by-systemic-health). Faults are report-only.
// The sortie driver teleports the ship, so it is a measuring rig that lives here and never in ServerShared.
public enum LoopOutcome
{
    // The hold filled (the first unit floated): the sortie measured what a full trip pays.
    Full,
    // The cap ended it with the ship still mining.
    Capped,
    // A stock the ship started with ran out: it cannot sustain mining. The rate is measured up to there and never enters the median.
    AmmoOut,
    // The rig measured nothing (no rock could be targeted, or it ran out of rocks early): never income.
    RigFault
}

public sealed class LoopSample
{
    public uint Seed;
    // This sample's belt in the zone's generation order, and how many belts the zone holds.
    public int Belt;
    public int Belts;
    public string BeltKind;
    public string Weapons;
    public LoopOutcome Outcome;
    public string Note;
    public int Rocks;
    public double SimSeconds;
    public int Units;
    // The ore the hold carried when the sortie ended, as mined, in key order: what a station is offered. Not printed.
    public List<(CultRecordKey Ore, int Units)> Mined = new List<(CultRecordKey, int)>();
    // The sim second the first unit floated, i.e. the hold was full; null if it never filled.
    public double? HoldFullAt;
    // What the arena's processing station paid for the hold at sortie end (TrySellOre), and what it would have paid for every ore
    // stack it wants had its bays had room (OreValue); the units it took and the units it refused for want of room.
    public int SellValue;
    public int OfferedValue;
    public int UnitsSold;
    public int UnitsRefused;
    public int AmmoSpent;
    public int Repair;

    public int Net => SellValue - AmmoSpent - Repair;
    public double RatePerMinute => SimSeconds > 0 ? Net * 60.0 / SimSeconds : 0;
    // Only a sortie that mined to a full hold or to the cap says what the loop pays.
    public bool Measured => Outcome == LoopOutcome.Full || Outcome == LoopOutcome.Capped;
}

public sealed class LoopDesign
{
    public string Key;
    public string Name;
    public int Price;
    // The design has a recipe, so its price is derived from its chain; otherwise the authored price stands.
    public bool Chained;
    public double? MinutesAtMedian;
    public double? MinutesAtBest;
    public bool Covered;
}

public sealed class LoopLadder
{
    public string Category;
    public List<LoopDesign> Designs;
    // Dearest over cheapest, for a category of two or more priced designs; null otherwise.
    public double? Width;
    // Sorties at the median net it takes to afford the dearest design here; null for a category of one design or with no measured sortie.
    public double? TopSorties;
}

public sealed class LoopReport
{
    public IReadOnlyList<LoopSample> Samples;
    // Over the measured samples only.
    public double Median;
    public double Best;
    // The median net of a measured sortie, and how many sorties it rests on.
    public double MedianNet;
    public int MeasuredSorties;
    public int Purse;
    public List<LoopLadder> Ladders;
    public List<EconomyFault> Faults;
}

public static class EconomyLoop
{
    public const string HLoop = "H-loop", HLadder = "H-ladder", HAfford = "H-afford", HAmmo = "H-ammo", HRig = "H-rig";
    // The ladder band and the afford band are rulings about weapons and gear only (ladder-band-weapons-and-gear).
    private static bool Banded(string category) => category.StartsWith("Weapon/", StringComparison.Ordinal) || category.StartsWith("Gear/", StringComparison.Ordinal);
    public static readonly uint[] DefaultSeeds = { 1, 2, 3, 69, 97 };
    // Every belt of every default seed is a sample (11 of them), so the cap is what bounds wall time.
    public const float DefaultCap = 150f;

    internal const float Step = .02f;
    private const float RockSeconds = 60f;
    private const int AmmoCheckTicks = 10;

    // One sortie per belt of the demo run of this seed, each its own sample.
    public static List<LoopSample> Sorties(string root, uint seed, float capSeconds)
    {
        var first = Sortie(root, seed, 0, capSeconds);
        var samples = new List<LoopSample> { first };
        for (var belt = 1; belt < first.Belts; belt++) samples.Add(Sortie(root, seed, belt, capSeconds));
        return samples;
    }

    // The demo run of one seed booted in a scratch of its own, with the two stations found by what they are for. Everything it
    // touches is its own: a scratch copy of the catalog, its own run store and its own ItemManager (a shared one advances its
    // Random and gave the same seed different results). Dispose closes the store and deletes the scratch.
    public static DemoArena Boot(string root, uint seed)
    {
        var scratch = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "aetherdb-loop-" + Guid.NewGuid().ToString("N"))).FullName;
        var demo = new DemoArena(scratch);
        try
        {
            // The catalog the game boots (the Pirates' station hulls are a package's), composed in the scratch, never written back.
            var composed = AetherDb.ComposeBooted(root, scratch);
            var cache = demo.Cache = AetheriaStores.Open(composed, runPath: Path.Combine(scratch, "run.cc"));
            var authored = AuthoredSettings.Load(root);
            var items = demo.Items = new ItemManager(1u, cache, new ProvenanceLedger(), authored.Read<GameplaySettings>("GameplaySettings"), _ => { });
            var zoneSettings = authored.Read<ZoneGenerationSettings>("ZoneSettings");
            var planetSettings = authored.Read<PlanetSettings>("PlanetSettings");
            var startingHull = File.ReadLines(Path.Combine(root, "Assets", "Resources", "Settings.asset"))
                .Select(line => Regex.Match(line, @"^  StartingHullName: (.+)$")).First(match => match.Success).Groups[1].Value.Trim();

            var scenario = new DemoTerminus();
            var stage = new GalaxyStage(authored.Read<SectorGenerationSettings>("SectorGenerationSettings"),
                authored.Read<SectorBackgroundSettings>("SectorBackgroundSettings"), authored.Read<SectorBackgroundSettings>("CastBackgroundSettings"),
                cache, _ => { }, null, () => seed);
            var galaxy = RunStart.Generate(scenario, stage);
            var arena = demo.Arena = new Zone(items, planetSettings, RunStart.GenerateArena(items, zoneSettings, planetSettings, galaxy, scenario), galaxy.Entrance, galaxy);
            var failures = new List<string>();
            demo.Player = RunStart.Stage(items, arena, scenario, startingHull, failures).Player;
            if (failures.Count > 0) throw new InvalidOperationException($"seed {seed}: staging failed: {string.Join("; ", failures)}");
            // The station that buys a sortie's ore and the one that holds it for onward haul, found by what they are for.
            OrbitalEntity OfKind(string kind) => arena.Entities.OfType<OrbitalEntity>().Single(entity => entity.Kind.Key.Equals(StationKindData.KeyOf(kind)));
            demo.Processing = OfKind("processing");
            demo.Zenith = OfKind("trade-hub");
            return demo;
        }
        catch
        {
            demo.Dispose();
            throw;
        }
    }

    // One sortie in the demo run of this seed on one belt: the staged ship is held forty units off each rock of the belt in index
    // order, fires every weapon at it, and stops when the first unit floats (the hold is full), when a starting stock runs out, or
    // at the cap. Everything it touches is its own: a scratch copy of the catalog, its own run store and its own ItemManager (a
    // shared one advances its Random and gave the same seed different results). atEnd, if given, sees the ship, items and the processing station as the
    // sample is taken, so a test can read the same state the sample was priced from.
    public static LoopSample Sortie(string root, uint seed, int beltIndex, float capSeconds, Action<Ship, ItemManager, Entity> atEnd = null)
    {
        if (!float.IsFinite(capSeconds) || capSeconds <= 0) throw new ArgumentOutOfRangeException(nameof(capSeconds), "the cap must be a positive, finite number of sim seconds");
        using var demo = Boot(root, seed);
        var (cache, items, arena, player, processing) = (demo.Cache, demo.Items, demo.Arena, demo.Player, demo.Processing);

        var belts = arena.Planets.Where(body => body.Value is AsteroidBeltData).ToList();
        if (beltIndex < 0 || beltIndex >= Math.Max(belts.Count, 1)) throw new ArgumentOutOfRangeException(nameof(beltIndex), $"seed {seed} has {belts.Count} belts");
        var weapons = player.Equipment.SelectMany(item => item.Behaviors.OfType<Weapon>()).ToArray();
        var sample = new LoopSample
        {
            Seed = seed, Belt = beltIndex, Belts = belts.Count, BeltKind = "none",
            Weapons = string.Join(", ", weapons.Select(w => items.GetData(w.Item.EquippableItem).Name).GroupBy(n => n).Select(g => g.Count() + "x " + g.Key))
        };
        if (belts.Count == 0)
        {
            sample.Outcome = LoopOutcome.RigFault;
            sample.Note = "the zone holds no belt";
            return sample;
        }
        var belt = belts[beltIndex];
        var beltData = (AsteroidBeltData) belt.Value;
        sample.BeltKind = cache.Get(beltData.Kind)?.Name ?? beltData.Kind.Key.Value;
        var ammoBefore = Ammunition(items, player);
        var units = 0;
        arena.CargoGained.Subscribe(gain => units += gain.Stored);

        for (var tick = 0; tick < 50; tick++) arena.Update(Step);
        var ticks = 0;
        var capTicks = (int) Math.Round(capSeconds / Step);
        var broken = 0;
        var targeted = 0;
        var ammoOut = false;
        int? fullTick = null;
        bool Stopped() => fullTick != null || ammoOut || ticks >= capTicks;
        void Tick(Action hold)
        {
            hold();
            arena.Update(Step);
            ticks++;
            if (fullTick == null && arena.FloatingItems.Count > 0) fullTick = ticks;
            if (ticks % AmmoCheckTicks == 0 && !ammoOut)
            {
                var now = Ammunition(items, player);
                ammoOut = ammoBefore.Keys.Any(data => !now.ContainsKey(data));
            }
        }

        for (var index = 0; index < beltData.Asteroids.Length && !Stopped(); index++)
        {
            var rock = new ChunkId(belt.Key, index);
            if (!arena.ChunkExists(rock)) continue;
            void Hold()
            {
                var at = arena.ChunkPose(rock.Field, rock.Index).xy;
                player.Position = new float3(at.x, player.Position.y, at.y - 40f);
                player.Velocity = float2.zero;
                player.Direction = new float2(0, 1);
                player.Aim = new float3(0, 0, 1);
            }
            Tick(Hold);
            Tick(Hold);
            if (!player.SetTarget(rock)) continue;
            targeted++;
            foreach (var weapon in weapons) weapon.Activate();
            for (var spent = 0; spent < (int) Math.Round(RockSeconds / Step) && arena.ChunkExists(rock) && !Stopped(); spent++) Tick(Hold);
            if (!arena.ChunkExists(rock)) broken++;
        }
        foreach (var weapon in weapons) weapon.Deactivate();

        var ammoAfter = Ammunition(items, player);
        var spentOnAmmo = 0L;
        foreach (var (data, (quantity, stack)) in ammoBefore)
        {
            var left = ammoAfter.TryGetValue(data, out var held) ? held.Quantity : 0;
            if (quantity > left) spentOnAmmo += StationServices.BuyPrice(items, stack, quantity - left);
        }
        sample.Rocks = broken;
        sample.SimSeconds = ticks * (double) Step;
        sample.Units = units;
        sample.HoldFullAt = fullTick == null ? null : fullTick * (double) Step;
        sample.OfferedValue = StationServices.OreValue(items, player.CargoBays, processing);
        sample.AmmoSpent = (int) Math.Min(spentOnAmmo, int.MaxValue);
        sample.Repair = StationServices.RepairCost(items, player);
        if (fullTick != null) sample.Outcome = LoopOutcome.Full;
        else if (ammoOut) { sample.Outcome = LoopOutcome.AmmoOut; sample.Note = "a starting stock ran out"; }
        else if (targeted == 0) { sample.Outcome = LoopOutcome.RigFault; sample.Note = "no rock of the belt could be targeted"; }
        else if (ticks >= capTicks) sample.Outcome = LoopOutcome.Capped;
        else { sample.Outcome = LoopOutcome.RigFault; sample.Note = "ran out of rocks before the cap"; }
        sample.Mined = player.CargoBays.SelectMany(bay => bay.Cargo.Keys.OfType<SimpleCommodity>()).Where(stack => FieldKinds.IsOre(cache, items.GetData(stack)))
            .GroupBy(stack => stack.Data.Key).OrderBy(g => g.Key.Value, StringComparer.Ordinal).Select(g => (g.Key, g.Sum(stack => stack.Quantity))).ToList();
        atEnd?.Invoke(player, items, processing);
        // The hold sells to the processing station: it pays for the units its bays take, and the rest stays aboard.
        var credits = 0;
        var sale = StationServices.TrySellOre(player.CargoBays, processing, ref credits);
        sample.SellValue = credits;
        sample.UnitsSold = sale.Sold;
        sample.UnitsRefused = sale.Offered - sale.Sold;
        return sample;
    }
    // The simple commodities the hold carries that are not ore, summed per commodity: ammunition and consumables. The sampled stack carries the item the price is read from.
    private static Dictionary<SimpleCommodityData, (int Quantity, SimpleCommodity Sample)> Ammunition(ItemManager items, Entity player)
    {
        var stacks = player.CargoBays.SelectMany(bay => bay.Cargo.Keys.OfType<SimpleCommodity>())
            .Where(stack => !FieldKinds.IsOre(items.ItemData, items.GetData(stack)));
        return stacks.GroupBy(stack => items.GetData(stack)).ToDictionary(g => g.Key, g => (g.Sum(stack => stack.Quantity), g.First()));
    }

    // The category an offered design belongs to, for the width of its ladder: the one owner of this rule.
    public static string Category(ItemData design) => design switch
    {
        HullData hull => "Hull/" + hull.HullType,
        WeaponItemData weapon => "Weapon/" + weapon.WeaponType,
        GearData gear => "Gear/" + gear.Hardpoint,
        _ => design.GetType().Name
    };

    // Rates, minutes to afford and ladder widths from the samples and the catalog: pure, recomputed every run, nothing stored.
    public static LoopReport Report(CultCache catalog, GameplaySettings settings, IReadOnlyList<LoopSample> samples)
    {
        var rates = samples.Where(s => s.Measured).Select(s => s.RatePerMinute).OrderBy(r => r).ToList();
        var median = rates.Count == 0 ? 0 : rates.Count % 2 == 1 ? rates[rates.Count / 2] : (rates[rates.Count / 2 - 1] + rates[rates.Count / 2]) / 2;
        var best = rates.Count == 0 ? 0 : rates[rates.Count - 1];
        var nets = samples.Where(s => s.Measured).Select(s => (double) s.Net).OrderBy(n => n).ToList();
        var medianNet = nets.Count == 0 ? 0 : nets.Count % 2 == 1 ? nets[nets.Count / 2] : (nets[nets.Count / 2 - 1] + nets[nets.Count / 2]) / 2;
        var derived = EconomyBalance.Prices(catalog, out _);
        double? Minutes(int price, double rate) => rate > 0 ? price / rate : (double?) null;
        double? Width(IEnumerable<double> prices)
        {
            var list = prices.Where(p => p > 0).ToList();
            return list.Count >= 2 ? list.Max() / list.Min() : (double?) null;
        }

        var ladders = EconomyChecks.OfferedDesigns(catalog).GroupBy(Category).OrderBy(g => g.Key, StringComparer.Ordinal).Select(g =>
        {
            var designs = g.Select(design =>
            {
                var key = catalog.RefOf(design).Key.Value;
                return new LoopDesign
                {
                    Key = key, Name = design.Name, Price = design.Price, Chained = derived.ContainsKey(key),
                    MinutesAtMedian = Minutes(design.Price, median), MinutesAtBest = Minutes(design.Price, best), Covered = design.Price <= settings.StartingCredits
                };
            }).OrderBy(d => d.Price).ThenBy(d => d.Key, StringComparer.Ordinal).ToList();
            return new LoopLadder
            {
                Category = g.Key, Designs = designs,
                Width = Width(designs.Select(d => (double) d.Price)),
                TopSorties = designs.Count >= 2 && medianNet > 0 ? designs.Max(d => d.Price) / medianNet : (double?) null
            };
        }).ToList();

        var faults = new List<EconomyFault>();
        foreach (var sample in samples)
        {
            var where = $"seed {sample.Seed} belt {sample.Belt + 1} of {sample.Belts}";
            if (sample.Outcome == LoopOutcome.AmmoOut)
                faults.Add(new EconomyFault(HAmmo, where, $"the fit ran out of a starting stock after {sample.SimSeconds:0.#} s ({sample.Weapons}) and cannot sustain mining."));
            else if (sample.Outcome == LoopOutcome.RigFault)
                faults.Add(new EconomyFault(HRig, where, $"the rig measured nothing: {sample.Note}."));
            else if (sample.Net <= 0)
                faults.Add(new EconomyFault(HLoop, where, $"the sortie nets {sample.Net} (sell {sample.SellValue}, ammunition {sample.AmmoSpent}, repair {sample.Repair}): no income."));
        }
        var (min, max) = (settings.EconomyLadderMin, settings.EconomyLadderMax);
        if (min > 0 || max > 0)
            foreach (var ladder in ladders.Where(l => Banded(l.Category)))
                if (ladder.Width.HasValue && ((min > 0 && ladder.Width.Value < min) || (max > 0 && ladder.Width.Value > max)))
                    faults.Add(new EconomyFault(HLadder, ladder.Category, $"the ladder is {ladder.Width.Value:0.##}x wide, outside the band {min:0.##} to {max:0.##}."));
        var (sortiesMin, sortiesMax) = (settings.EconomySortiesMin, settings.EconomySortiesMax);
        if (sortiesMin > 0 || sortiesMax > 0)
            foreach (var ladder in ladders.Where(l => Banded(l.Category) && l.TopSorties.HasValue))
            {
                var sorties = ladder.TopSorties.Value;
                if ((sortiesMin > 0 && sorties < sortiesMin) || (sortiesMax > 0 && sorties > sortiesMax))
                    faults.Add(new EconomyFault(HAfford, ladder.Category, $"the dearest design takes {sorties:0.#} sorties at the median net of {medianNet:0.#}, outside the band {sortiesMin:0.##} to {sortiesMax:0.##}; this rests on {nets.Count} measured sortie(s)."));
            }
        return new LoopReport { Samples = samples, Median = median, Best = best, MedianNet = medianNet, MeasuredSorties = nets.Count, Purse = settings.StartingCredits, Ladders = ladders, Faults = faults };
    }
}

// A booted demo run: the arena and what a sortie or a drain reads in it. Owns the scratch copy of the catalog and the run store.
public sealed class DemoArena : IDisposable
{
    private readonly string _scratch;

    public DemoArena(string scratch) => _scratch = scratch;

    public CultCache Cache;
    public ItemManager Items;
    public Zone Arena;
    public Ship Player;
    // The refinery that buys a sortie's ore, and the trade hub that holds ore for onward haul.
    public OrbitalEntity Processing, Zenith;

    public void Dispose()
    {
        (Cache as IDisposable)?.Dispose();
        try { Directory.Delete(_scratch, true); } catch (IOException) { }
    }
}
