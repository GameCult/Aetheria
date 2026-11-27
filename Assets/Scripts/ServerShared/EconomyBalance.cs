using System;
using System.Collections.Generic;
using System.Linq;
using GameCult.Caching;

// One recipe read as a chain link. Wage is the implied credits per worker-second at the authored prices; null when the link
// is unpriced (an input, any filler of a role, or the output at Price 0) or has no labour. Cost is the derived cost of one output unit.
public sealed class BalanceLink
{
    public string Output, Name, StationKind;
    public double Labour, Price, Cost;
    public double? Wage;
    public double? WageRatio;
    // Why Wage is null: "output" (the output is at Price 0), "input" (an input, upkeep or role filler is at Price 0), "labour" (no labour);
    // null when priced. The first that applies, in that order.
    public string Unpriced;
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

// The static economy balance: a steady-state input-output reading of the chain (Leontief 1936; Sraffa 1960 zero-profit prices under
// one wage; Samuelson 1951: with labour the only primary factor the cheapest filler of a role is demand-independent; Lewis 1954: the
// extraction sector sets the going wage). Pure: reads the catalog, writes nothing. ItemData.Price stays the one price the game charges;
// for a made commodity it is the price Prices derives, which economy author writes. Balance's faults are report-only.
public static class EconomyBalance
{
    public const string HMargin = "H-margin", HWage = "H-wage", HConverge = "H-converge";
    private const int SweepCap = 10000, SettleWindow = 100;

    // The stored price a derived price becomes: whole credits, at least 1.
    public static int Rounded(double derived) => (int) Math.Max(1, Math.Round(derived, MidpointRounding.AwayFromZero));

    // The words for goods whose price does not settle.
    public static string Unsettled(CultCache catalog, IEnumerable<string> keys) =>
        $"No price settles within {SweepCap} sweeps: these goods sit on a cycle of recipes whose requirements do not shrink: " +
        string.Join(", ", keys.Select(k => catalog.Get<ItemData>(new CultRecordKey(k))?.Name ?? k));

    private static double Price(CultCache catalog, string key) => catalog.Get<ItemData>(new CultRecordKey(key))?.Price ?? 0;

    // Authored-price wage per link; the cheapest filler fills a role, and a role with any filler at Price 0 leaves the link unpriced.
    private static List<BalanceLink> Links(CultCache catalog, List<EconomyChecks.Column> columns)
    {
        double? CheapestPriced(string[] fillers) => fillers.Length == 0 || fillers.Any(f => Price(catalog, f) <= 0) ? (double?) null : fillers.Min(f => Price(catalog, f));
        var links = new List<BalanceLink>();
        foreach (var c in columns)
        {
            var link = new BalanceLink { Output = c.Output, Name = catalog.Get(c.Recipe.Output)?.Name ?? c.Output, Labour = c.Labour, Price = Price(catalog, c.Output), StationKind = catalog.Get(c.Recipe.StationKind)?.Name ?? "no kind" };
            var cost = 0.0;
            var inputsPriced = true;
            foreach (var (commodity, count) in c.Inputs.Concat(c.Upkeep))
            {
                if (count <= 0) continue;
                var p = Price(catalog, commodity);
                if (p <= 0) inputsPriced = false;
                cost += count * p;
            }
            foreach (var slot in c.Slots)
            {
                if (slot.Count <= 0) continue;
                var p = CheapestPriced(slot.Fillers);
                if (p == null) inputsPriced = false;
                else cost += slot.Count * p.Value;
            }
            link.Unpriced = link.Price <= 0 ? "output" : !inputsPriced ? "input" : c.Labour <= 0 ? "labour" : null;
            if (link.Unpriced == null) link.Wage = (c.OutputCount * link.Price - cost) / c.Labour;
            links.Add(link);
        }
        return links;
    }

    // The going wage: the median implied wage of the priced raw links, 0 when none is priced.
    private static double RawWage(CultCache catalog, List<EconomyChecks.Column> columns, List<BalanceLink> links)
    {
        var wages = links.Where((l, i) => l.Wage.HasValue && columns[i].IsCommodity && Recipes.IsRaw(catalog, columns[i].Recipe)).Select(l => l.Wage.Value).OrderBy(w => w).ToList();
        return wages.Count == 0 ? 0 : wages.Count % 2 == 1 ? wages[wages.Count / 2] : (wages[wages.Count / 2 - 1] + wages[wages.Count / 2]) / 2;
    }

    public static double Wage(CultCache catalog)
    {
        var columns = EconomyChecks.Columns(catalog);
        return RawWage(catalog, columns, Links(catalog, columns));
    }

    // Value iteration from zero over every recipe at one wage. A good in fixedOutputs, or with no recipe, holds its authored price.
    // Two recipes for one output: the cheaper sets its price, as a role takes its cheapest filler.
    private static Dictionary<string, double> Sweep(CultCache catalog, List<EconomyChecks.Column> columns, double wage, ISet<string> fixedOutputs, out List<string> unsettled)
    {
        var derived = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var c in columns) if (!fixedOutputs.Contains(c.Output)) derived[c.Output] = 0;
        double Cost(string key) => derived.TryGetValue(key, out var v) ? v : Price(catalog, key);
        var converged = false;
        // The last sweep each good moved in: a cycle can move one good per sweep, so the last sweep alone would name half of it.
        var lastMoved = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var sweep = 0; sweep < SweepCap && !converged; sweep++)
        {
            converged = true;
            var best = new Dictionary<string, double>(StringComparer.Ordinal);
            foreach (var c in columns.Where(c => !fixedOutputs.Contains(c.Output)))
            {
                var sum = wage * c.Labour;
                foreach (var (commodity, count) in c.Inputs.Concat(c.Upkeep)) if (count > 0) sum += count * Cost(commodity);
                foreach (var slot in c.Slots)
                    if (slot.Count > 0 && slot.Fillers.Length > 0) sum += slot.Count * slot.Fillers.Min(Cost);
                var next = sum / c.OutputCount;
                if (!best.TryGetValue(c.Output, out var held) || next < held) best[c.Output] = next;
            }
            foreach (var (output, next) in best)
            {
                // A price that is not a finite number is not settled, however little it moved.
                if (!double.IsFinite(next) || Math.Abs(next - derived[output]) > 1e-12 * Math.Max(1, Math.Abs(next))) { converged = false; lastMoved[output] = sweep; }
                derived[output] = next;
            }
        }
        unsettled = converged ? new List<string>() : lastMoved.Where(p => p.Value >= SweepCap - SettleWindow).Select(p => p.Key).OrderBy(k => k, StringComparer.Ordinal).ToList();
        return derived;
    }

    // The price of every made commodity (a commodity whose recipe is not raw) at the going wage: its inputs, upkeep and cheapest
    // role fillers at their prices, plus the wage times its labour, over its output count. Raw and recipeless goods keep their
    // authored price; designs are not priced here unless asked. Unsettled names the goods still moving when the sweep gave up: a cycle of recipes
    // whose requirements do not shrink has no price, so none of the values are a price.
    // With designs, the derived price of every design output that has a recipe is returned too: a reading for the report, never a
    // price anything writes.
    public static Dictionary<string, double> Prices(CultCache catalog, out List<string> unsettled, bool designs = false)
    {
        var columns = EconomyChecks.Columns(catalog);
        var raw = new HashSet<string>(columns.Where(c => Recipes.IsRaw(catalog, c.Recipe)).Select(c => c.Output), StringComparer.Ordinal);
        var derived = Sweep(catalog, columns, RawWage(catalog, columns, Links(catalog, columns)), raw, out unsettled);
        return columns.Where(c => (c.IsCommodity || designs) && !raw.Contains(c.Output)).Select(c => c.Output).Distinct(StringComparer.Ordinal)
            .ToDictionary(key => key, key => derived[key], StringComparer.Ordinal);
    }

    public static BalanceReport Balance(CultCache catalog, double wageSpread = 2)
    {
        var columns = EconomyChecks.Columns(catalog);
        var links = Links(catalog, columns);
        var reference = RawWage(catalog, columns, links);
        foreach (var l in links) l.WageRatio = reference > 0 ? l.Wage / reference : null;

        // Derived cost: nothing fixed, so an ore costs its labour; belt rows show price minus cost, the rent.
        var derived = Sweep(catalog, columns, reference, new HashSet<string>(), out var unsettled);
        var converged = unsettled.Count == 0;
        double Cost(string key) => derived.TryGetValue(key, out var v) ? v : Price(catalog, key);
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
                var yield = new BeltYield { Ore = ore, Share = share, Price = Price(catalog, ore), Cost = Cost(ore), WageRatio = links.FirstOrDefault(l => l.Output == ore)?.WageRatio };
                row.Price += share * yield.Price;
                row.Cost += share * yield.Cost;
                row.Yields.Add(yield);
            }
            belts.Add(row);
        }
        return new BalanceReport { WageReference = reference, Links = links, Faults = faults, Belts = belts };
    }
}
