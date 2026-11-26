using System;
using System.Collections.Generic;
using System.Linq;
using GameCult.Caching;

// One recipe read as a chain link. Wage is the implied credits per worker-second at the authored prices; null when the link
// is unpriced (an input, filler or output at Price 0) or has no labour. Cost is the derived cost of one output unit.
public sealed class BalanceLink
{
    public string Output, Name, StationKind;
    public double Labour, Price, Cost;
    public double? Wage;
    public double? WageRatio;
}

// One ore a kind of belt holds: its share of the kind's abundance, its extraction link's wage over the median, its price and cost.
public sealed class BeltYield
{
    public string Ore;
    public double Share, Price, Cost;
    public double? WageRatio;
}

public sealed class BeltRow
{
    public string Kind;
    public List<BeltYield> Yields = new List<BeltYield>();
    public double Price, Cost;
}

public sealed class BalanceReport
{
    public double WageReference;
    public List<BalanceLink> Links;
    public List<EconomyFault> Faults;
    public List<BeltRow> Belts;
}

// The static economy balance: a steady-state input-output reading of the authored chain (Leontief 1936; Sraffa 1960 zero-profit
// prices under one wage; Samuelson 1951: with labour the only primary factor the cheapest filler of a role is demand-independent).
// Pure: reads the catalog, writes nothing. Its faults are report-only and never a refusal; the authored ItemData.Price stays the
// only price the game charges.
public static class EconomyBalance
{
    public const string HMargin = "H-margin", HWage = "H-wage", HConverge = "H-converge";
    private const int SweepCap = 10000;

    public static BalanceReport Balance(CultCache catalog, double wageSpread = 2)
    {
        var columns = EconomyChecks.Columns(catalog);
        double Price(string key) => catalog.Get<ItemData>(new CultRecordKey(key))?.Price ?? 0;

        // Authored-price wage per link; the cheapest priced filler fills a role.
        double? CheapestPriced(string[] fillers) => fillers.Select(Price).Where(p => p > 0).Select(p => (double?) p).Min();
        var links = new List<BalanceLink>();
        foreach (var c in columns)
        {
            var link = new BalanceLink { Output = c.Output, Name = catalog.Get(c.Recipe.Output)?.Name ?? c.Output, Labour = c.Labour, Price = Price(c.Output), StationKind = catalog.Get(c.Recipe.StationKind)?.Name ?? "no kind" };
            var cost = 0.0;
            var priced = link.Price > 0 && c.Labour > 0;
            foreach (var (commodity, count) in c.Inputs.Concat(c.Upkeep))
            {
                if (count <= 0) continue;
                var p = Price(commodity);
                if (p <= 0) priced = false;
                cost += count * p;
            }
            foreach (var slot in c.Slots)
            {
                if (slot.Count <= 0) continue;
                var p = CheapestPriced(slot.Fillers);
                if (p == null) priced = false;
                else cost += slot.Count * p.Value;
            }
            if (priced) link.Wage = (c.OutputCount * link.Price - cost) / c.Labour;
            links.Add(link);
        }

        var wages = links.Where(l => l.Wage.HasValue).Select(l => l.Wage.Value).OrderBy(w => w).ToList();
        var reference = wages.Count == 0 ? 0 : wages.Count % 2 == 1 ? wages[wages.Count / 2] : (wages[wages.Count / 2 - 1] + wages[wages.Count / 2]) / 2;
        foreach (var l in links) l.WageRatio = reference > 0 ? l.Wage / reference : null;

        // Derived cost: value iteration from zero. A good with no recipe holds its authored price as a stand-in.
        var derived = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var c in columns) derived[c.Output] = 0;
        double Cost(string key) => derived.TryGetValue(key, out var v) ? v : Price(key);
        var converged = false;
        for (var sweep = 0; sweep < SweepCap && !converged; sweep++)
        {
            converged = true;
            foreach (var c in columns)
            {
                var sum = reference * c.Labour;
                foreach (var (commodity, count) in c.Inputs.Concat(c.Upkeep)) if (count > 0) sum += count * Cost(commodity);
                foreach (var slot in c.Slots)
                    if (slot.Count > 0 && slot.Fillers.Length > 0) sum += slot.Count * slot.Fillers.Min(Cost);
                var next = sum / c.OutputCount;
                if (Math.Abs(next - derived[c.Output]) > 1e-12 * Math.Max(1, Math.Abs(next))) converged = false;
                derived[c.Output] = next;
            }
        }
        foreach (var l in links) l.Cost = derived[l.Output];

        var faults = new List<EconomyFault>();
        if (!converged) faults.Add(new EconomyFault(HConverge, "(chain)", $"Derived cost did not settle within {SweepCap} sweeps."));
        foreach (var l in links.Where(l => l.Wage.HasValue))
        {
            if (l.Wage.Value <= 0)
                faults.Add(new EconomyFault(HMargin, l.Output, $"{l.Name} sells at or below its inputs: implied wage {l.Wage.Value:0.#####} per worker-second."));
            else if (reference > 0 && (l.Wage.Value < reference / wageSpread || l.Wage.Value > reference * wageSpread))
                faults.Add(new EconomyFault(HWage, l.Output, $"{l.Name}: implied wage {l.Wage.Value:0.#####} is {l.WageRatio:0.##}x the median {reference:0.#####}, outside the spread of {wageSpread:0.##}."));
        }

        var belts = new List<BeltRow>();
        foreach (var kind in catalog.GetAll<FieldKindData>().OrderBy(k => k.Name, StringComparer.Ordinal))
        {
            var row = new BeltRow { Kind = kind.Name };
            var total = kind.Yields.Where(y => y.Abundance > 0).Sum(y => (double) y.Abundance);
            foreach (var y in kind.Yields.Where(y => y.Abundance > 0 && total > 0))
            {
                var ore = y.Commodity.Key.Value;
                var share = y.Abundance / total;
                var yield = new BeltYield { Ore = ore, Share = share, Price = Price(ore), Cost = Cost(ore), WageRatio = links.FirstOrDefault(l => l.Output == ore)?.WageRatio };
                row.Price += share * yield.Price;
                row.Cost += share * yield.Cost;
                row.Yields.Add(yield);
            }
            belts.Add(row);
        }
        return new BalanceReport { WageReference = reference, Links = links, Faults = faults, Belts = belts };
    }
}
