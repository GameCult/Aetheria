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
public sealed class LoopSample
{
    public uint Seed;
    public string BeltKind;
    public string Weapons;
    public int Rocks;
    public double SimSeconds;
    public int Units;
    // The sim second the first unit floated, i.e. the hold was full; null if it never filled.
    public double? HoldFullAt;
    public int SellValue;
    public int AmmoSpent;
    public int Repair;

    public int Net => SellValue - AmmoSpent - Repair;
    public double RatePerMinute => SimSeconds > 0 ? Net * 60.0 / SimSeconds : 0;
}

public sealed class LoopDesign
{
    public string Key;
    public string Name;
    public int Price;
    public double? Derived;
    public double? MinutesAtMedian;
    public double? MinutesAtBest;
    public bool Covered;
}

public sealed class LoopLadder
{
    public string Category;
    public List<LoopDesign> Designs;
    // Dearest over cheapest, for a category of two or more priced designs; null otherwise.
    public double? WidthAuthored;
    public double? WidthDerived;
}

public sealed class LoopReport
{
    public IReadOnlyList<LoopSample> Samples;
    public double Median;
    public double Best;
    public int Purse;
    public List<LoopLadder> Ladders;
    public List<EconomyFault> Faults;
}

public static class EconomyLoop
{
    public const string HLoop = "H-loop", HLadder = "H-ladder";
    public static readonly uint[] DefaultSeeds = { 1, 2, 3, 69, 97 };
    public const float DefaultCap = 300f;

    private const float Step = .02f;
    private const float RockSeconds = 60f;

    // One sortie in the demo run of this seed: the staged ship is held forty units off each rock of the arena's belt in index order,
    // fires every weapon at it, and stops when the first unit floats (the hold is full) or at the cap. Everything it touches is its
    // own: a scratch copy of the catalog, its own run store and its own ItemManager (a shared one advances its Random and gave the
    // same seed different results).
    public static LoopSample Sortie(string root, uint seed, float capSeconds)
    {
        var scratch = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "aetherdb-loop-" + Guid.NewGuid().ToString("N"))).FullName;
        try
        {
            var catalog = Path.Combine(scratch, "Aetheria.cc");
            File.Copy(Path.Combine(root, "GameData", "Aetheria.cc"), catalog);
            new FileInfo(catalog).IsReadOnly = false;
            using var cache = AetheriaStores.Open(catalog, runPath: Path.Combine(scratch, "run.cc"));
            var authored = AuthoredSettings.Load(root);
            var items = new ItemManager(1u, cache, new ProvenanceLedger(), authored.Read<GameplaySettings>("GameplaySettings"), _ => { });
            var zoneSettings = authored.Read<ZoneGenerationSettings>("ZoneSettings");
            var planetSettings = authored.Read<PlanetSettings>("PlanetSettings");
            var startingHull = File.ReadLines(Path.Combine(root, "Assets", "Resources", "Settings.asset"))
                .Select(line => Regex.Match(line, @"^  StartingHullName: (.+)$")).First(match => match.Success).Groups[1].Value.Trim();

            var scenario = new DemoTerminus();
            var stage = new GalaxyStage(authored.Read<SectorGenerationSettings>("SectorGenerationSettings"),
                authored.Read<SectorBackgroundSettings>("SectorBackgroundSettings"), authored.Read<SectorBackgroundSettings>("CastBackgroundSettings"),
                cache, _ => { }, null, () => seed);
            var galaxy = RunStart.Generate(scenario, stage);
            var arena = new Zone(items, planetSettings, RunStart.GenerateArena(items, zoneSettings, planetSettings, galaxy, scenario), galaxy.Entrance, galaxy);
            var failures = new List<string>();
            var player = RunStart.Stage(items, arena, scenario, startingHull, failures).Player;
            if (failures.Count > 0) throw new InvalidOperationException($"seed {seed}: staging failed: {string.Join("; ", failures)}");

            var belt = arena.Planets.First(body => body.Value is AsteroidBeltData);
            var rocks = ((AsteroidBeltData) belt.Value).Asteroids.Length;
            var weapons = player.Equipment.SelectMany(item => item.Behaviors.OfType<Weapon>()).ToArray();
            var ammoBefore = Ammunition(items, player);
            var units = 0;
            arena.CargoGained.Subscribe(gain => units += gain.Stored);

            for (var tick = 0; tick < 50; tick++) arena.Update(Step);
            var ticks = 0;
            var capTicks = (int) Math.Round(capSeconds / Step);
            var broken = 0;
            int? fullTick = null;
            bool Stopped() => fullTick != null || ticks >= capTicks;
            void Tick(Action hold) { hold(); arena.Update(Step); ticks++; if (fullTick == null && arena.FloatingItems.Count > 0) fullTick = ticks; }

            for (var index = 0; index < rocks && !Stopped(); index++)
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
                foreach (var weapon in weapons) weapon.Activate();
                for (var spent = 0; spent < (int) Math.Round(RockSeconds / Step) && arena.ChunkExists(rock) && !Stopped(); spent++) Tick(Hold);
                if (!arena.ChunkExists(rock)) broken++;
            }
            foreach (var weapon in weapons) weapon.Deactivate();

            var ammoAfter = Ammunition(items, player);
            var spentOnAmmo = 0L;
            foreach (var (data, (quantity, sample)) in ammoBefore)
            {
                var left = ammoAfter.TryGetValue(data, out var held) ? held.Quantity : 0;
                if (quantity > left) spentOnAmmo += StationServices.BuyPrice(items, sample, quantity - left);
            }
            return new LoopSample
            {
                Seed = seed,
                BeltKind = cache.Get(((AsteroidBeltData) belt.Value).Kind)?.Name ?? ((AsteroidBeltData) belt.Value).Kind.Key.Value,
                Weapons = string.Join(", ", weapons.Select(w => items.GetData(w.Item.EquippableItem).Name).GroupBy(n => n).Select(g => g.Count() + "x " + g.Key)),
                Rocks = broken,
                SimSeconds = ticks * (double) Step,
                Units = units,
                HoldFullAt = fullTick == null ? null : fullTick * (double) Step,
                SellValue = StationServices.OreValue(items, player.CargoBays),
                AmmoSpent = (int) Math.Min(spentOnAmmo, int.MaxValue),
                Repair = StationServices.RepairCost(items, player)
            };
        }
        finally
        {
            try { Directory.Delete(scratch, true); } catch (IOException) { }
        }
    }

    // The simple commodities the hold carries that were not mined (a mined stack names an Extracted lot), summed per commodity: ammunition
    // and consumables. The sampled stack carries the item the price is read from.
    private static Dictionary<SimpleCommodityData, (int Quantity, SimpleCommodity Sample)> Ammunition(ItemManager items, Entity player)
    {
        var stacks = player.CargoBays.SelectMany(bay => bay.Cargo.Keys.OfType<SimpleCommodity>())
            .Where(stack => stack.Lot == 0 || !(items.GetLot(stack).Origin is Extracted));
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
        var rates = samples.Select(s => s.RatePerMinute).OrderBy(r => r).ToList();
        var median = rates.Count == 0 ? 0 : rates.Count % 2 == 1 ? rates[rates.Count / 2] : (rates[rates.Count / 2 - 1] + rates[rates.Count / 2]) / 2;
        var best = rates.Count == 0 ? 0 : rates[rates.Count - 1];
        var derived = EconomyBalance.Prices(catalog, out _, designs: true);
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
                    Key = key, Name = design.Name, Price = design.Price, Derived = derived.TryGetValue(key, out var d) ? d : (double?) null,
                    MinutesAtMedian = Minutes(design.Price, median), MinutesAtBest = Minutes(design.Price, best), Covered = design.Price <= settings.StartingCredits
                };
            }).OrderBy(d => d.Price).ThenBy(d => d.Key, StringComparer.Ordinal).ToList();
            return new LoopLadder
            {
                Category = g.Key, Designs = designs,
                WidthAuthored = Width(designs.Select(d => (double) d.Price)),
                WidthDerived = Width(designs.Where(d => d.Derived.HasValue).Select(d => d.Derived.Value))
            };
        }).ToList();

        var faults = new List<EconomyFault>();
        foreach (var sample in samples.Where(s => s.Net <= 0))
            faults.Add(new EconomyFault(HLoop, "seed " + sample.Seed, $"the sortie nets {sample.Net} (sell {sample.SellValue}, ammunition {sample.AmmoSpent}, repair {sample.Repair}): no income."));
        var (min, max) = (settings.EconomyLadderMin, settings.EconomyLadderMax);
        if (min > 0 || max > 0)
            foreach (var ladder in ladders)
                foreach (var (width, which) in new[] { (ladder.WidthAuthored, "authored"), (ladder.WidthDerived, "derived") })
                    if (width.HasValue && ((min > 0 && width.Value < min) || (max > 0 && width.Value > max)))
                        faults.Add(new EconomyFault(HLadder, ladder.Category, $"the {which} ladder is {width.Value:0.##}x wide, outside the band {min:0.##} to {max:0.##}."));
        return new LoopReport { Samples = samples, Median = median, Best = best, Purse = settings.StartingCredits, Ladders = ladders, Faults = faults };
    }
}
