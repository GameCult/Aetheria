using System;
using System.Collections.Generic;
using System.Linq;
using GameCult.Caching;

// What the economy checks read beyond the catalog: the setting's factors on recipe terms (each at least 1) and the units of a
// commodity one worker consumes per second of work. Neutral by default; later cuts pass real values.
public sealed class EconomyLoad
{
    public Dictionary<string, double> LabourNeeds = new Dictionary<string, double>();
    public double InputFactor = 1;
    public double WorkforceFactor = 1;
    public double UpkeepFactor = 1;
}

public sealed class EconomyFault
{
    public readonly string Invariant;
    public readonly string Record;
    public readonly string Message;

    public EconomyFault(string invariant, string record, string message)
    {
        Invariant = invariant;
        Record = record;
        Message = message;
    }

    public override string ToString() => $"{Invariant} {Record}: {Message}";
}

public sealed class EconomyReport
{
    public readonly IReadOnlyList<EconomyFault> Faults;
    // Production steps to each good, by the good's record key; null when no finite chain reaches it. Read by reviewers, never a refusal.
    public readonly IReadOnlyDictionary<string, int?> Depth;

    public EconomyReport(IReadOnlyList<EconomyFault> faults, IReadOnlyDictionary<string, int?> depth)
    {
        Faults = faults;
        Depth = depth;
    }
}

// The one owner of the catalog's economy invariants. Pure: draws nothing, reads no clock, writes nothing. One method per rule.
public static class EconomyChecks
{
    public const string LRecipe = "L-recipe", LOneKindList = "L-one-kind-list", LLore = "L-lore", LSource = "L-source-sink", LProductive = "L-productive", LDemand = "L-consumer-demand";
    private const double Margin = 1e-9;

    public static EconomyReport Check(CultCache catalog, EconomyLoad load = null)
    {
        var faults = new List<EconomyFault>();
        faults.AddRange(Recipe(catalog));
        faults.AddRange(Lore(catalog));
        faults.AddRange(SourceSink(catalog));
        faults.AddRange(Productive(catalog, load));
        faults.AddRange(ConsumerDemand(catalog));
        return new EconomyReport(faults, Deep(catalog));
    }

    private static IEnumerable<(string Key, RecipeData Recipe)> AllRecipes(CultCache catalog) =>
        catalog.GetAll<RecipeData>().Select(recipe => (catalog.RefOf(recipe).Key.Value, recipe)).OrderBy(pair => pair.Item1, StringComparer.Ordinal);

    private static IEnumerable<ItemData> Commodities(CultCache catalog) =>
        catalog.GetAll<ItemData>().Where(Recipes.IsCommodity).OrderBy(item => catalog.RefOf(item).Key.Value, StringComparer.Ordinal);

    // Every commodity and every offered design (but a Station or Turret hull) has a recipe, and each recipe is well formed.
    public static List<EconomyFault> Recipe(CultCache catalog)
    {
        var faults = new List<EconomyFault>();
        void Add(string record, string message) => faults.Add(new EconomyFault(LRecipe, record, message));
        foreach (var item in Commodities(catalog))
            if (Recipes.RecipeOf(catalog, item) == null)
                Add(catalog.RefOf(item).Key.Value, $"Commodity '{item.Name}' has no recipe.");
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var product in catalog.GetAll<FactionProductData>().Where(p => LoadoutGenerator.IsOffered(catalog, p)))
        {
            var design = catalog.Get(product.Design);
            if (design == null || (design is HullData hull && (hull.HullType == HullType.Station || hull.HullType == HullType.Turret))) continue;
            var key = catalog.RefOf(design).Key.Value;
            if (seen.Add(key) && Recipes.RecipeOf(catalog, design) == null)
                Add(key, $"Offered design '{design.Name}' has no recipe.");
        }
        var kindsPlaced = new HashSet<string>(catalog.GetAll<Faction>().SelectMany(f => StationKinds.KindsOf(catalog, f)).Select(k => k.Key.Value), StringComparer.Ordinal);
        foreach (var (key, recipe) in AllRecipes(catalog))
        {
            var output = catalog.Get(recipe.Output);
            if (output == null) { Add(key, "Output names no item this catalog holds."); continue; }
            if (!catalog.RefOf(recipe).Key.Equals(RecipeData.KeyOf(recipe.Output.Key)))
                Add(key, "The recipe's key is not recipe:<output key>; one recipe per output is its key.");
            if (recipe.OutputCount <= 0) Add(key, "OutputCount is not above zero.");
            if (!float.IsFinite(recipe.Cycle) || recipe.Cycle <= 0) Add(key, "Cycle is not a finite number above zero.");
            if (recipe.Workforce < 0) Add(key, "Workforce is below zero.");
            foreach (var (list, name) in new[] { (recipe.Inputs, "Inputs"), (recipe.Upkeep, "Upkeep") })
                for (var i = 0; i < list.Count; i++)
                {
                    if (list[i].Count <= 0) Add(key, $"{name}[{i}] count is not above zero.");
                    if (!Recipes.IsCommodity(catalog.Get(list[i].Commodity))) Add(key, $"{name}[{i}] names no commodity; a recipe takes only commodities.");
                }
            var kind = catalog.Get(recipe.StationKind);
            if (kind == null) Add(key, "StationKind names no station kind this catalog holds.");
            else if (!kindsPlaced.Contains(recipe.StationKind.Key.Value))
                faults.Add(new EconomyFault(LOneKindList, key, $"No faction places station kind '{kind.Name}', so nothing can run this recipe."));
            var roles = Recipes.RolesOf(output);
            if (!Recipes.IsCommodity(output))
            {
                if (recipe.Fills.Count > 0) Add(key, "Fills is set on a design; only a commodity fills a role.");
                if (roles.Count == 0) Add(key, "A design's recipe needs a design with at least one role.");
            }
            if (roles.Count > 0 && recipe.Inputs.Count > 0) Add(key, "A role recipe has no Inputs; its inputs are its design's roles.");
            foreach (var role in roles)
            {
                if (role.Count < 1) Add(key, $"Role '{role.Name}' count is below one.");
                if (Recipes.Fillers(catalog, role.Name).Length == 0) Add(key, $"Role '{role.Name}' has no commodity whose recipe fills it.");
            }
        }
        return faults;
    }

    // A commodity's lore hook is present. What it says is the operator's review.
    public static List<EconomyFault> Lore(CultCache catalog) =>
        AllRecipes(catalog).Where(pair => Recipes.IsCommodity(catalog.Get(pair.Recipe.Output)) && string.IsNullOrWhiteSpace(pair.Recipe.LoreHook))
            .Select(pair => new EconomyFault(LLore, pair.Key, "A commodity's recipe needs a lore hook.")).ToList();

    private static (HashSet<string> Sells, HashSet<string> Buys) Gate(CultCache catalog)
    {
        var rows = catalog.GetAll<OutsideMarketData>().SelectMany(market => market.Rows).ToList();
        return (new HashSet<string>(rows.Where(r => r.Sells).Select(r => r.Commodity.Key.Value), StringComparer.Ordinal),
            new HashSet<string>(rows.Where(r => r.Buys).Select(r => r.Commodity.Key.Value), StringComparer.Ordinal));
    }

    // Every commodity has a source (a grounded chain, or a gate that sells it) and a sink (a recipe uses it, a role some design
    // names, or a gate that buys it). A consumer good's sink is judged once needs tables exist.
    public static List<EconomyFault> SourceSink(CultCache catalog)
    {
        var faults = new List<EconomyFault>();
        var (sells, buys) = Gate(catalog);
        var grounded = Depths(catalog, sells);
        var recipes = AllRecipes(catalog).ToList();
        var used = new HashSet<string>(recipes.SelectMany(p => p.Recipe.Inputs.Concat(p.Recipe.Upkeep)).Select(c => c.Commodity.Key.Value), StringComparer.Ordinal);
        var rolesNamed = new HashSet<string>(recipes.Where(p => Recipes.IsRoleRecipe(catalog, p.Recipe))
            .SelectMany(p => Recipes.RolesOf(catalog.Get(p.Recipe.Output))).Select(r => r.Name), StringComparer.Ordinal);
        foreach (var (key, recipe) in recipes)
        {
            var output = catalog.Get(recipe.Output);
            if (!Recipes.IsCommodity(output)) continue;
            var outputKey = recipe.Output.Key.Value;
            if (!grounded.TryGetValue(outputKey, out var depth) || depth == null)
                faults.Add(new EconomyFault(LSource, key, $"'{output.Name}' has no source: no chain of recipes reaches raw goods and no gate sells it."));
            if (Recipes.IsConsumerGood(catalog, recipe)) continue;
            if (!used.Contains(outputKey) && !recipe.Fills.Any(rolesNamed.Contains) && !buys.Contains(outputKey))
                faults.Add(new EconomyFault(LSource, key, $"'{output.Name}' has no sink: no recipe uses it, no design names a role it fills, no gate buys it."));
        }
        return faults;
    }

    // Production steps to each good: raw goods 0, a recipe's output one more than its deepest input or role, a role as deep as
    // its shallowest filler (Knuth's least fixpoint over AND and OR nodes). Null for a good no finite chain reaches. Reported for
    // review; it refuses nothing.
    public static IReadOnlyDictionary<string, int?> Deep(CultCache catalog) => Depths(catalog, new HashSet<string>());

    private static Dictionary<string, int?> Depths(CultCache catalog, ISet<string> seeds)
    {
        var depth = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var seed in seeds) depth[seed] = 0;
        var recipes = AllRecipes(catalog).Where(p => catalog.Get(p.Recipe.Output) != null).ToList();
        bool changed;
        do
        {
            changed = false;
            foreach (var (_, recipe) in recipes)
            {
                var candidate = Candidate(catalog, recipe, depth);
                var key = recipe.Output.Key.Value;
                if (candidate == null || (depth.TryGetValue(key, out var known) && known <= candidate)) continue;
                depth[key] = candidate.Value;
                changed = true;
            }
        } while (changed);
        var result = new Dictionary<string, int?>(StringComparer.Ordinal);
        foreach (var item in Commodities(catalog)) result[catalog.RefOf(item).Key.Value] = null;
        foreach (var (_, recipe) in recipes) result[recipe.Output.Key.Value] = null;
        foreach (var pair in depth) result[pair.Key] = pair.Value;
        return result;
    }

    private static int? Candidate(CultCache catalog, RecipeData recipe, Dictionary<string, int> depth)
    {
        if (Recipes.IsRaw(catalog, recipe)) return 0;
        var deepest = 0;
        foreach (var input in recipe.Inputs)
        {
            if (!depth.TryGetValue(input.Commodity.Key.Value, out var d)) return null;
            deepest = Math.Max(deepest, d);
        }
        foreach (var role in Recipes.RolesOf(catalog.Get(recipe.Output)))
        {
            int? shallowest = null;
            foreach (var filler in Recipes.Fillers(catalog, role.Name))
                if (depth.TryGetValue(filler.Value, out var d) && (shallowest == null || d < shallowest)) shallowest = d;
            if (shallowest == null) return null;
            deepest = Math.Max(deepest, shallowest.Value);
        }
        return deepest + 1;
    }

    // Hawkins-Simon over commodities plus one labour good, at the worst single filler of every role slot: the spectral radius
    // of the requirements matrix stays below one however the roles are filled. The worst fill is the spectral simplex
    // (Nesterov and Protasov 2013) on the matrix nudged strictly positive, then the radius of the chosen matrix is taken exactly.
    public static List<EconomyFault> Productive(CultCache catalog, EconomyLoad load = null)
    {
        load = load ?? new EconomyLoad();
        if (!(load.InputFactor >= 1) || !(load.WorkforceFactor >= 1) || !(load.UpkeepFactor >= 1))
            throw new ArgumentException("Setting factors are at least 1.", nameof(load));
        var columns = AllRecipes(catalog).Where(p => Recipes.IsCommodity(catalog.Get(p.Recipe.Output)) && p.Recipe.OutputCount > 0 && float.IsFinite(p.Recipe.Cycle)).ToList();
        var names = columns.Select(p => p.Recipe.Output.Key.Value).ToList();
        var index = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < names.Count; i++) index[names[i]] = i;
        var n = names.Count + 1;
        var labour = names.Count;
        var fixedTerms = new double[n, n];
        var slots = new List<(int Column, double Amount, int[] Fillers)>();
        for (var j = 0; j < columns.Count; j++)
        {
            var recipe = columns[j].Recipe;
            foreach (var input in recipe.Inputs)
                if (index.TryGetValue(input.Commodity.Key.Value, out var row) && input.Count > 0) fixedTerms[row, j] += input.Count * load.InputFactor / recipe.OutputCount;
            foreach (var upkeep in recipe.Upkeep)
                if (index.TryGetValue(upkeep.Commodity.Key.Value, out var row) && upkeep.Count > 0) fixedTerms[row, j] += upkeep.Count * load.UpkeepFactor / recipe.OutputCount;
            fixedTerms[labour, j] = recipe.Workforce * (double) recipe.Cycle * load.WorkforceFactor / recipe.OutputCount;
            foreach (var role in Recipes.RolesOf(catalog.Get(recipe.Output)))
            {
                var fillers = Recipes.Fillers(catalog, role.Name).Where(k => index.ContainsKey(k.Value)).Select(k => index[k.Value]).ToArray();
                if (fillers.Length > 0 && role.Count > 0) slots.Add((j, role.Count * load.InputFactor / recipe.OutputCount, fillers));
            }
        }
        foreach (var need in load.LabourNeeds)
            if (index.TryGetValue(need.Key, out var row)) fixedTerms[row, labour] = need.Value;

        double[,] Build(int[] choice, double nudge)
        {
            var a = new double[n, n];
            for (var r = 0; r < n; r++)
                for (var c = 0; c < n; c++)
                    a[r, c] = fixedTerms[r, c] + nudge;
            for (var s = 0; s < slots.Count; s++) a[slots[s].Fillers[choice[s]], slots[s].Column] += slots[s].Amount;
            return a;
        }

        var pick = new int[slots.Count];
        for (var round = 0; round < 500; round++)
        {
            var left = LeftPerron(Build(pick, 1e-12), n);
            var moved = false;
            for (var s = 0; s < slots.Count; s++)
            {
                var best = pick[s];
                for (var f = 0; f < slots[s].Fillers.Length; f++)
                    if (left[slots[s].Fillers[f]] > left[slots[s].Fillers[best]] * (1 + 1e-9)) best = f;
                if (best != pick[s]) { pick[s] = best; moved = true; }
            }
            if (!moved) break;
        }

        var final = Build(pick, 0);
        var (rho, component) = Radius(final, n);
        var faults = new List<EconomyFault>();
        if (rho >= 1 - Margin)
        {
            string Name(int i) => i == labour ? "labour" : names[i];
            var order = CycleOrder(final, n, component);
            faults.Add(new EconomyFault(LProductive, Name(order[0]),
                $"The goods cannot sustain themselves: spectral radius {rho:0.######} is not below 1, carried by {string.Join(" -> ", order.Select(Name))}."));
        }
        return faults;
    }

    // Left Perron vector of a strictly positive matrix by power iteration on A + I (aperiodic, so a pure cycle converges too).
    private static double[] LeftPerron(double[,] a, int n)
    {
        var u = Enumerable.Repeat(1.0 / n, n).ToArray();
        for (var iteration = 0; iteration < 20000; iteration++)
        {
            var next = new double[n];
            for (var c = 0; c < n; c++)
                for (var r = 0; r < n; r++)
                    next[c] += u[r] * (a[r, c] + (r == c ? 1 : 0));
            var norm = next.Sum();
            var delta = 0.0;
            for (var c = 0; c < n; c++) { next[c] /= norm; delta = Math.Max(delta, Math.Abs(next[c] - u[c])); }
            u = next;
            if (delta < 1e-15) break;
        }
        return u;
    }

    // The spectral radius of a nonnegative matrix: the largest over its strongly connected components that hold a cycle, each by
    // power iteration on A + I (primitive when irreducible). An edge j -> i is a nonzero A[i, j]. Also the component carrying it.
    private static (double Rho, List<int> Component) Radius(double[,] a, int n)
    {
        var reach = new bool[n, n];
        for (var i = 0; i < n; i++)
            for (var j = 0; j < n; j++)
                reach[i, j] = i == j || a[j, i] > 0;
        for (var k = 0; k < n; k++)
            for (var i = 0; i < n; i++)
                if (reach[i, k])
                    for (var j = 0; j < n; j++)
                        if (reach[k, j]) reach[i, j] = true;
        var done = new bool[n];
        var best = 0.0;
        var bestComponent = new List<int>();
        for (var start = 0; start < n; start++)
        {
            if (done[start]) continue;
            var component = Enumerable.Range(0, n).Where(i => reach[start, i] && reach[i, start]).ToList();
            foreach (var i in component) done[i] = true;
            var rho = Irreducible(a, component);
            if (rho > best) { best = rho; bestComponent = component; }
        }
        return (best, bestComponent);
    }

    private static double Irreducible(double[,] a, List<int> nodes)
    {
        var m = nodes.Count;
        var x = Enumerable.Repeat(1.0, m).ToArray();
        var lambda = 0.0;
        for (var iteration = 0; iteration < 200000; iteration++)
        {
            var next = new double[m];
            for (var r = 0; r < m; r++)
            {
                next[r] = x[r];
                for (var c = 0; c < m; c++) next[r] += a[nodes[r], nodes[c]] * x[c];
            }
            var norm = next.Max();
            var moved = 0.0;
            for (var r = 0; r < m; r++) { next[r] /= norm; moved = Math.Max(moved, Math.Abs(next[r] - x[r])); }
            lambda = norm;
            x = next;
            if (moved < 1e-15) break;
        }
        return lambda - 1;
    }

    // The component's goods in cycle order: breadth-first from its first good along "needs" edges, so a simple cycle reads
    // around once and a tangled component reads outward from where it starts.
    private static List<int> CycleOrder(double[,] a, int n, List<int> component)
    {
        var inside = new HashSet<int>(component);
        var order = new List<int> { component[0] };
        var seen = new HashSet<int> { component[0] };
        for (var at = 0; at < order.Count; at++)
            for (var i = 0; i < n; i++)
                if (a[i, order[at]] > 0 && inside.Contains(i) && seen.Add(i)) order.Add(i);
        return order;
    }

    // Every processed commodity two or three steps below a gear or hull (its role fillers are one step, the gear-specific final
    // assemblies, exempt) fills a role some consumer good names, so the shared layer is shared. Raw goods and the gate do not count.
    public static List<EconomyFault> ConsumerDemand(CultCache catalog)
    {
        var recipes = AllRecipes(catalog).Where(p => catalog.Get(p.Recipe.Output) != null).ToDictionary(p => p.Recipe.Output.Key.Value, p => p.Recipe, StringComparer.Ordinal);
        var consumerRoles = new HashSet<string>(recipes.Values.Where(r => Recipes.IsConsumerGood(catalog, r))
            .SelectMany(r => Recipes.RolesOf(catalog.Get(r.Output))).Select(role => role.Name), StringComparer.Ordinal);
        HashSet<string> Below(IEnumerable<string> keys)
        {
            var next = new HashSet<string>(StringComparer.Ordinal);
            foreach (var key in keys)
            {
                if (!recipes.TryGetValue(key, out var recipe)) continue;
                foreach (var input in recipe.Inputs) next.Add(input.Commodity.Key.Value);
                foreach (var role in Recipes.RolesOf(catalog.Get(recipe.Output)))
                    foreach (var filler in Recipes.Fillers(catalog, role.Name)) next.Add(filler.Value);
            }
            return next;
        }
        var deep = new HashSet<string>(StringComparer.Ordinal);
        foreach (var key in recipes.Where(p => !Recipes.IsCommodity(catalog.Get(p.Value.Output)) && Recipes.IsRoleRecipe(catalog, p.Value)).Select(p => p.Key))
        {
            var distance2 = Below(Below(new[] { key }));
            deep.UnionWith(distance2);
            deep.UnionWith(Below(distance2));
        }
        return deep.OrderBy(key => key, StringComparer.Ordinal)
            .Where(key => recipes.TryGetValue(key, out var recipe) && Recipes.IsCommodity(catalog.Get(recipe.Output)) && !Recipes.IsRaw(catalog, recipe) && !recipe.Fills.Any(consumerRoles.Contains))
            .Select(key => new EconomyFault(LDemand, RecipeData.KeyOf(new CultRecordKey(key)).Value,
                $"'{catalog.Get(recipes[key].Output).Name}' is a processed good below a gear or hull that fills no role a consumer good names."))
            .ToList();
    }
}
