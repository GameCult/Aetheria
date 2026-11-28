using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using GameCult.Caching;
using GameCult.Caching.MessagePack;
using MessagePack;

// What a station's intake and drain look like in the demo run of one seed (ruling station-intake-few-sorties: a station takes ore
// for a few sorties, then fills, and installed facilities work it down). Measurement only: it reads a booted copy of the catalog,
// fills the two demo stations with the seed's median sortie hold through the shipped sale (StationServices.TrySellOre), then reads
// the drain twice. On screen for a bounded window, stepping the arena; and by Production.Advance spans on inactive copies for the
// window (which must agree with the steps: H-drain-sim) and for the long horizon (the same rule, FacilityProductionTests.
// OneSpanEqualsManySteps). Writes no catalog, changes no setting: every record it touches is in the booted scratch.

// One bay of a station before any sale: its usable cells, the free ones, and the units of each wanted ore it takes at that ore's MaxStack.
public sealed class BayIntake
{
    public string Name;
    public bool Silo;
    public int Cells, Free;
    // Units of wanted ore the bay holds after the fill.
    public int Taken;
    public List<(string Ore, int Units)> Capacity = new List<(string, int)>();
}

// One sortie's hold offered to the station through the shipped sale.
public sealed class SortieSale
{
    public string Ore;
    public int Held, Sold;
    public int Refused => Held - Sold;
}

public sealed class FacilityDrain
{
    public string Name;
    // One line per recipe the facility runs: output, cycle, inputs.
    public List<string> RunList = new List<string>();
    // Steps of the on-screen window per Stall, and the Stall the last span of the horizon ended in.
    public SortedDictionary<Stall, int> WindowSteps = new SortedDictionary<Stall, int>();
    public Stall HorizonEnd;
    // The ores some recipe of this facility draws that the station still holds when the horizon ends.
    public List<string> OresLeft = new List<string>();
}

public sealed class OreDrain
{
    public string Ore;
    // Units held after the fill and after each sim hour of the horizon (index 0 is the fill).
    public List<int> Hourly = new List<int>();
    // Sim seconds until none is held, or null with the facilities' last Stall.
    public double? EmptyAt;
    public string NeverBecause = "";
}

public sealed class OutputPile
{
    public string Metal;
    public List<int> Hourly = new List<int>();
}

public sealed class StationDrain
{
    public string Role, Name, Kind;
    public List<BayIntake> Bays = new List<BayIntake>();
    // The sorties to the first refusal, or all of them when the cap on sorties ended it.
    public List<SortieSale> Sorties = new List<SortieSale>();
    public bool Filled;
    public List<FacilityDrain> Facilities = new List<FacilityDrain>();
    public List<OreDrain> Ores = new List<OreDrain>();
    public List<OutputPile> Outputs = new List<OutputPile>();
    // Sim seconds into the horizon at the end of the first span any facility ended stalled Room, or null.
    public double? FirstRoomStall;
    // Units held of every design after the window by steps and by spans, where they differ.
    public List<(string Design, int Steps, int Spans)> WindowDisagreements = new List<(string, int, int)>();
}

public sealed class DrainSample
{
    public uint Seed;
    public int SortieUnits;
    public double WindowSeconds, HorizonHours;
    public List<StationDrain> Stations = new List<StationDrain>();
}

public static class EconomyDrain
{
    public const string HStall = "H-drain-stall", HSim = "H-drain-sim";
    public const double DefaultWindowSeconds = 600, DefaultHorizonHours = 24;
    // One span of the off-screen path, in sim seconds: the resolution of every time the horizon reports.
    public const double Span = 60;
    private const int MaxSorties = 500;

    // The seed's median sortie hold, in units: the median over the sorties that mined to a full hold or the cap.
    public static int SortieUnits(IReadOnlyList<LoopSample> samples)
    {
        var units = samples.Where(s => s.Measured).Select(s => s.Units).OrderBy(u => u).ToList();
        return units.Count == 0 ? 0 : units[units.Count / 2];
    }

    // beforeFill sees the booted arena before any sale (a test empties a bay there); beforeWindow sees it after the fill and before
    // the window is read by steps or by spans (a test switches a facility off there).
    public static DrainSample Run(string root, uint seed, double windowSeconds, double horizonHours, int sortieUnits,
        Action<DemoArena> beforeFill = null, Action<DemoArena> beforeWindow = null)
    {
        if (!double.IsFinite(windowSeconds) || windowSeconds <= 0) throw new ArgumentOutOfRangeException(nameof(windowSeconds), "the window must be a positive, finite number of sim seconds");
        if (!double.IsFinite(horizonHours) || horizonHours <= 0) throw new ArgumentOutOfRangeException(nameof(horizonHours), "the horizon must be a positive, finite number of sim hours");
        if (sortieUnits < 1) throw new ArgumentOutOfRangeException(nameof(sortieUnits), "a sortie holds at least one unit");
        using var demo = EconomyLoop.Boot(root, seed);
        beforeFill?.Invoke(demo);
        var catalog = demo.Cache;
        var sample = new DrainSample { Seed = seed, SortieUnits = sortieUnits, WindowSeconds = windowSeconds, HorizonHours = horizonHours };
        var stations = new[] { ("processing", demo.Processing), ("trade-hub", demo.Zenith) };
        var runs = new List<StationRun>();
        foreach (var (role, station) in stations)
        {
            var drain = new StationDrain { Role = role, Name = station.Name, Kind = station.Kind.Key.Value };
            var ores = WantedOres(demo, station);
            foreach (var bay in station.CargoBays) drain.Bays.Add(Intake(demo, station, bay, ores));
            Fill(demo, station, ores, sortieUnits, drain);
            for (var at = 0; at < drain.Bays.Count; at++)
                drain.Bays[at].Taken = station.CargoBays.ElementAt(at).Cargo.Keys.OfType<ICommodityStack>().Where(stack => ores.Contains(((ItemInstance) stack).Data.Key)).Sum(stack => stack.Quantity);
            sample.Stations.Add(drain);
            runs.Add(new StationRun(demo, station, drain, ores));
        }
        beforeWindow?.Invoke(demo);
        foreach (var run in runs) run.Copy();

        // The window on screen: the arena steps, every facility's Stall is read after each step.
        var steps = (int) Math.Round(windowSeconds / EconomyLoop.Step);
        foreach (var run in runs) run.Live = run.Station.Equipment.SelectMany(unit => unit.Behaviors.OfType<Facility>().Select(facility => (unit, facility))).ToArray();
        for (var step = 0; step < steps; step++)
        {
            demo.Arena.Update(EconomyLoop.Step);
            foreach (var run in runs)
                for (var at = 0; at < run.Live.Length; at++)
                {
                    var tally = run.Drain.Facilities[at].WindowSteps;
                    var stall = run.Live[at].facility.Stall;
                    tally[stall] = tally.TryGetValue(stall, out var n) ? n + 1 : 1;
                }
        }
        foreach (var run in runs)
        {
            var onScreen = Held(run.Station);
            run.Spans(run.WindowCopy, steps * (double) EconomyLoop.Step);
            var spanned = Held(run.WindowCopy);
            foreach (var design in onScreen.Keys.Union(spanned.Keys).OrderBy(k => k, StringComparer.Ordinal))
            {
                onScreen.TryGetValue(design, out var a);
                spanned.TryGetValue(design, out var b);
                if (a != b) run.Drain.WindowDisagreements.Add((Name(catalog, design), a, b));
            }
        }

        // The horizon by spans on a copy of the filled station, sampled each sim hour.
        foreach (var run in runs) run.Horizon(horizonHours);
        return sample;
    }

    // The faults of a set of samples: a facility that ends the horizon stalled with ore it draws still held, and a window that steps
    // and spans read differently.
    public static List<EconomyFault> Report(IReadOnlyList<DrainSample> samples)
    {
        var faults = new List<EconomyFault>();
        foreach (var sample in samples)
            foreach (var station in sample.Stations)
            {
                var where = $"seed {sample.Seed} {station.Role}";
                foreach (var facility in station.Facilities.Where(f => f.OresLeft.Count > 0 && (f.HorizonEnd == Stall.Room || f.HorizonEnd == Stall.Inputs)))
                    faults.Add(new EconomyFault(HStall, where, $"{facility.Name} ends the {sample.HorizonHours:0.##} h horizon stalled {facility.HorizonEnd} with {string.Join(", ", facility.OresLeft)} still held: the station does not drain what it took."));
                if (station.WindowDisagreements.Count > 0)
                    faults.Add(new EconomyFault(HSim, where, $"the {sample.WindowSeconds:0.##} s window by steps and by spans disagree on {string.Join(", ", station.WindowDisagreements.Select(d => $"{d.Design} {d.Steps} against {d.Spans}"))}."));
            }
        return faults;
    }

    public static void Print(DrainSample sample, TextWriter output)
    {
        var inv = CultureInfo.InvariantCulture;
        output.WriteLine(string.Format(inv, "\nStation drain, seed {0} (a sortie holds {1} units; window {2:0.##} s on screen and by spans; horizon {3:0.##} h by spans of {4:0} s)", sample.Seed, sample.SortieUnits, sample.WindowSeconds, sample.HorizonHours, Span));
        foreach (var station in sample.Stations)
        {
            output.WriteLine($"  {station.Role} {station.Name} ({station.Kind})");
            foreach (var bay in station.Bays)
                output.WriteLine($"    bay {bay.Name} | {(bay.Silo ? "silo" : "stock")} | cells {bay.Cells} | free {bay.Free} | ore units at MaxStack: {(bay.Capacity.Count == 0 ? "none wanted" : string.Join("; ", bay.Capacity.GroupBy(c => c.Units).Select(g => $"{g.Key} each of {string.Join(", ", g.Select(c => c.Ore))}")))} | wanted ore held after the fill: {bay.Taken}");
            output.WriteLine(station.Sorties.Count == 0
                ? "    sorties to fill: none (the station wants no ore)"
                : $"    sorties to fill: {station.Sorties.Count}{(station.Filled ? "" : "+ (the cap on sorties ended it)")} | per sortie (ore held sold refused): {string.Join("; ", station.Sorties.Select(s => $"{s.Ore} {s.Held} {s.Sold} {s.Refused}"))}");
            foreach (var facility in station.Facilities)
            {
                output.WriteLine($"    facility {facility.Name}: {facility.RunList.Count} recipes");
                foreach (var line in facility.RunList) output.WriteLine("      " + line);
                var total = facility.WindowSteps.Values.Sum();
                output.WriteLine($"      window stalls: {(total == 0 ? "no steps" : string.Join(", ", facility.WindowSteps.Select(kv => string.Format(inv, "{0} {1:0.#}%", kv.Key, 100.0 * kv.Value / total))))} | horizon ends {facility.HorizonEnd}");
            }
            output.WriteLine(station.WindowDisagreements.Count == 0
                ? "    window by steps and by spans: held counts agree"
                : $"    window by steps and by spans: DISAGREE on {string.Join(", ", station.WindowDisagreements.Select(d => $"{d.Design} {d.Steps} against {d.Spans}"))}");
            foreach (var ore in station.Ores)
            {
                var drained = ore.Hourly[0] - ore.Hourly[ore.Hourly.Count - 1];
                var hours = (ore.EmptyAt ?? sample.HorizonHours * 3600) / 3600;
                output.WriteLine(string.Format(inv, "    ore {0}: {1} held after the fill, {2:0.##} per sim hour drained, full to empty {3} | per hour {4}", ore.Ore, ore.Hourly[0], hours > 0 ? drained / hours : 0,
                    ore.EmptyAt.HasValue ? ore.EmptyAt.Value.ToString("0", inv) + " sim s (" + (ore.EmptyAt.Value / 3600).ToString("0.##", inv) + " h)" : "never (" + ore.NeverBecause + ")", string.Join(" ", ore.Hourly)));
            }
            foreach (var pile in station.Outputs.Where(p => p.Hourly.Any(units => units > 0)))
                output.WriteLine($"    output {pile.Metal}: units held per hour {string.Join(" ", pile.Hourly)}");
            var empty = station.Outputs.Where(p => p.Hourly.All(units => units == 0)).Select(p => p.Metal).ToList();
            if (empty.Count > 0) output.WriteLine($"    outputs held at 0 all horizon: {string.Join(", ", empty)}");
            if (station.Outputs.Count > 0)
                output.WriteLine(station.FirstRoomStall.HasValue
                    ? string.Format(inv, "    first Room stall at {0:0} sim s ({1:0.##} h)", station.FirstRoomStall.Value, station.FirstRoomStall.Value / 3600)
                    : "    no Room stall in the horizon");
        }
    }

    // The ores the station wants, in key order: what a sortie can bring that it will take.
    private static List<CultRecordKey> WantedOres(DemoArena demo, Entity station) =>
        StationWants.Of(station).Where(key => FieldKinds.IsOre(demo.Cache, demo.Cache.Get<ItemData>(key))).OrderBy(key => key.Value, StringComparer.Ordinal).ToList();

    private static string Name(CultCache catalog, string key) => catalog.Get<ItemData>(new CultRecordKey(key))?.Name ?? key;

    // One bay as it is booted: capacity for each wanted ore is measured on an inactive copy of the station, so the sale below
    // never moves it.
    private static BayIntake Intake(DemoArena demo, Entity station, EquippedCargoBay bay, List<CultRecordKey> ores)
    {
        var shape = bay.Data.InteriorShape;
        var cells = shape.AllCoordinates.Count(v => shape[v]);
        var used = shape.AllCoordinates.Count(v => shape[v] && bay.Occupancy[v.x, v.y] != null);
        var intake = new BayIntake { Name = bay.Name, Silo = bay.Data is SiloData, Cells = cells, Free = cells - used };
        var at = station.CargoBays.ToList().IndexOf(bay);
        foreach (var ore in ores)
        {
            var copy = Copy(demo, station).CargoBays.ElementAt(at);
            var maxStack = demo.Cache.Get<SimpleCommodityData>(ore).MaxStack;
            var units = 0;
            while (copy.TryStore(new SimpleCommodity { Data = new CultRecordRef<ItemData>(ore), Quantity = maxStack })) units += maxStack;
            // The last, partial stack: halve the quantity until one fits.
            for (var quantity = maxStack / 2; quantity >= 1; quantity /= 2)
                while (copy.TryStore(new SimpleCommodity { Data = new CultRecordRef<ItemData>(ore), Quantity = quantity })) units += quantity;
            intake.Capacity.Add((demo.Cache.Get<ItemData>(ore).Name, units));
        }
        return intake;
    }

    // Sorties to fill: the seed's median sortie hold (one ore a sortie, the wanted ores in turn) sold through the shipped sale until
    // a sortie sells less than it brought. The hold is the player's own; what the sale leaves aboard is put away.
    private static void Fill(DemoArena demo, Entity station, List<CultRecordKey> ores, int sortieUnits, StationDrain drain)
    {
        if (ores.Count == 0) return;
        var player = demo.Player;
        for (var sortie = 0; sortie < MaxSorties; sortie++)
        {
            var ore = ores[sortie % ores.Count];
            var maxStack = demo.Cache.Get<SimpleCommodityData>(ore).MaxStack;
            var held = 0;
            for (var left = sortieUnits; left > 0;)
            {
                var quantity = Math.Min(left, maxStack);
                if (!player.CargoBays.Any(bay => bay.TryStore(new SimpleCommodity { Data = new CultRecordRef<ItemData>(ore), Quantity = quantity }))) break;
                held += quantity;
                left -= quantity;
            }
            if (held == 0) throw new InvalidOperationException($"seed {drain.Name}: the player's hold takes no {ore.Value}");
            var credits = 0;
            var result = StationServices.TrySellOre(player.CargoBays, station, ref credits);
            drain.Sorties.Add(new SortieSale { Ore = demo.Cache.Get<ItemData>(ore).Name, Held = held, Sold = result.Sold });
            foreach (var bay in player.CargoBays)
                foreach (var stack in bay.Cargo.Keys.OfType<SimpleCommodity>().Where(s => s.Data.Key.Equals(ore)).ToList())
                    bay.Remove(stack);
            if (result.Sold < held) { drain.Filled = true; return; }
        }
    }

    // An inactive copy of a station as it stands, through the bytes a save would hold: Pack alone shares the stack objects with the
    // live station, so a copy's sale or take would move the original's units.
    internal static Entity Copy(DemoArena demo, Entity station)
    {
        var options = CultDocumentMessagePackSerialization.OptionsFor(typeof(HullData).Assembly);
        var bytes = MessagePackSerializer.Serialize(EntitySerializer.Pack(station), options);
        return EntitySerializer.Unpack(demo.Items, demo.Arena, MessagePackSerializer.Deserialize<EntityPack>(bytes, options));
    }

    private static Dictionary<string, int> Held(Entity entity)
    {
        var held = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var bay in entity.CargoBays)
            foreach (var stack in bay.Cargo.Keys.OfType<ICommodityStack>())
            {
                var key = ((ItemInstance) stack).Data.Key.Value;
                held[key] = held.TryGetValue(key, out var n) ? n + stack.Quantity : stack.Quantity;
            }
        return held;
    }

    // One station through the window and the horizon: the live one on screen, and inactive copies of it as it stood after the fill.
    private sealed class StationRun
    {
        public readonly DemoArena Demo;
        public readonly Entity Station;
        public readonly StationDrain Drain;
        public readonly List<CultRecordKey> Ores;
        public Entity WindowCopy, HorizonCopy;
        public (EquippedItem unit, Facility facility)[] Live = Array.Empty<(EquippedItem, Facility)>();

        public StationRun(DemoArena demo, Entity station, StationDrain drain, List<CultRecordKey> ores)
        {
            Demo = demo;
            Station = station;
            Drain = drain;
            Ores = ores;
        }

        public void Copy()
        {
            WindowCopy = EconomyDrain.Copy(Demo, Station);
            HorizonCopy = EconomyDrain.Copy(Demo, Station);
            foreach (var (unit, facility) in Facilities(Station))
            {
                var design = Demo.Items.GetData(unit.EquippableItem);
                var drain = new FacilityDrain { Name = design.Name };
                foreach (var recipe in Production.RunList(Demo.Cache, Demo.Cache.RefOf(design).Key))
                    drain.RunList.Add($"{Demo.Cache.Get(recipe.Output).Name} x{recipe.OutputCount} | cycle {recipe.Cycle:0.##} s | takes {string.Join(", ", Production.Draws(Demo.Cache, recipe).Select(key => Name(Demo.Cache, key.Value)))}");
                Drain.Facilities.Add(drain);
            }
        }

        private static IEnumerable<(EquippedItem unit, Facility facility)> Facilities(Entity entity) =>
            entity.Equipment.SelectMany(unit => unit.Behaviors.OfType<Facility>().Select(facility => (unit, facility)));

        // The copy's facilities, each advanced through `seconds` of sim time in spans of Span, in equipment order. Returns the Stall
        // each ended in; each sees the sim time and the stalls after every span.
        public Stall[] Spans(Entity copy, double seconds, Action<double, Stall[]> each = null)
        {
            var catalog = Demo.Cache;
            var hold = new EntityHold(copy);
            var units = copy.Equipment.Where(unit => unit.Behaviors.OfType<Facility>().Any()).Select(unit => unit.EquippableItem).ToArray();
            var last = new Stall[units.Length];
            for (var at = 0.0; at < seconds - 1e-9;)
            {
                var span = Math.Min(Span, seconds - at);
                for (var i = 0; i < units.Length; i++)
                {
                    var (run, stall) = Production.Advance(catalog, Production.RunList(catalog, units[i].Data.Key), units[i].Run, span, hold);
                    units[i].Run = run;
                    last[i] = stall;
                }
                at += span;
                each?.Invoke(at, last);
            }
            return last;
        }

        public void Horizon(double hours)
        {
            var catalog = Demo.Cache;
            var hold = new EntityHold(HorizonCopy);
            var facilities = Facilities(HorizonCopy).Select(f => f.unit).ToArray();
            var outputs = facilities.SelectMany(unit => Production.RunList(catalog, unit.EquippableItem.Data.Key)).Select(r => r.Output.Key).Distinct().OrderBy(k => k.Value, StringComparer.Ordinal).ToList();
            var oreKeys = Ores.Where(ore => hold.Held(ore) > 0).ToList();
            foreach (var ore in oreKeys) Drain.Ores.Add(new OreDrain { Ore = Name(catalog, ore.Value) });
            foreach (var metal in outputs) Drain.Outputs.Add(new OutputPile { Metal = Name(catalog, metal.Value) });
            void Sample()
            {
                for (var i = 0; i < oreKeys.Count; i++) Drain.Ores[i].Hourly.Add(hold.Held(oreKeys[i]));
                for (var i = 0; i < outputs.Count; i++) Drain.Outputs[i].Hourly.Add(hold.Held(outputs[i]));
            }
            Sample();
            var nextHour = 3600.0;
            var ends = Spans(HorizonCopy, hours * 3600, (at, stalls) =>
            {
                if (!Drain.FirstRoomStall.HasValue && stalls.Any(stall => stall == Stall.Room)) Drain.FirstRoomStall = at;
                for (var i = 0; i < oreKeys.Count; i++)
                    if (!Drain.Ores[i].EmptyAt.HasValue && hold.Held(oreKeys[i]) == 0) Drain.Ores[i].EmptyAt = at;
                if (at >= nextHour - 1e-9) { Sample(); nextHour += 3600; }
            });
            HashSet<CultRecordKey> Draws(EquippedItem unit) => Production.RunList(catalog, unit.EquippableItem.Data.Key).SelectMany(recipe => Production.Draws(catalog, recipe)).ToHashSet();
            for (var i = 0; i < ends.Length; i++)
            {
                var draws = Draws(facilities[i]);
                Drain.Facilities[i].HorizonEnd = ends[i];
                Drain.Facilities[i].OresLeft = oreKeys.Where(ore => draws.Contains(ore) && hold.Held(ore) > 0).Select(ore => Name(catalog, ore.Value)).ToList();
            }
            for (var i = 0; i < oreKeys.Count; i++)
            {
                if (Drain.Ores[i].EmptyAt.HasValue) continue;
                var drawers = Enumerable.Range(0, facilities.Length).Where(f => Draws(facilities[f]).Contains(oreKeys[i])).ToList();
                Drain.Ores[i].NeverBecause = drawers.Count == 0 ? "no facility draws it" : string.Join("/", drawers.Select(f => ends[f]).Distinct());
            }
        }
    }
}
