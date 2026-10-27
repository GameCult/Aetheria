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

    // A fault only because the chain has not written the record yet (a commodity with no recipe). Reported, never a refusal.
    public readonly bool Pending;

    // L-productive only: every good the carrying cycle holds (sorted) and the spectral radius. The fault's identity is the member
    // set; a radius that rises is a worse fault of the same identity.
    public readonly IReadOnlyList<string> Members;
    public readonly double Radius;

    public EconomyFault(string invariant, string record, string message, bool pending = false, IReadOnlyList<string> members = null, double radius = 0)
    {
        Members = members;
        Radius = radius;
        Pending = pending;
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

    private static IEnumerable<(string Key, RecipeData Recipe)> StoredRecipes(CultCache catalog) =>
        catalog.GetAll<RecipeData>().Select(recipe => (catalog.RefOf(recipe).Key.Value, recipe)).OrderBy(pair => pair.Item1, StringComparer.Ordinal);

    // Every rule but Recipe reads recipes through here, so a list a record holds as null (or with a null entry) reads as the list
    // without it; Recipe alone names the malformation. The stored document is never changed.
    private static IEnumerable<(string Key, RecipeData Recipe)> AllRecipes(CultCache catalog) =>
        StoredRecipes(catalog).Select(pair => (pair.Key, Sound(pair.Recipe)));

    private static RecipeData Sound(RecipeData recipe) =>
        recipe.Inputs != null && recipe.Upkeep != null && recipe.Fills != null && recipe.RoleInputs != null
            && !recipe.Inputs.Contains(null) && !recipe.Upkeep.Contains(null) && !recipe.Fills.Contains(null) && !recipe.RoleInputs.Contains(null)
            ? recipe
            : new RecipeData
            {
                Output = recipe.Output, OutputCount = recipe.OutputCount, Cycle = recipe.Cycle, Workforce = recipe.Workforce,
                StationKind = recipe.StationKind, LoreHook = recipe.LoreHook,
                Inputs = (recipe.Inputs ?? new List<CommodityCount>()).Where(c => c != null).ToList(),
                Upkeep = (recipe.Upkeep ?? new List<CommodityCount>()).Where(c => c != null).ToList(),
                Fills = (recipe.Fills ?? new List<string>()).Where(f => f != null).ToList(),
                RoleInputs = (recipe.RoleInputs ?? new List<ItemRole>()).Where(r => r != null).ToList()
            };

    // The designs some faction offers, but a Station or Turret hull, distinct and ordered by key: the designs that need a recipe.
    private static List<ItemData> OfferedDesigns(CultCache catalog)
    {
        var designs = new Dictionary<string, ItemData>(StringComparer.Ordinal);
        foreach (var product in catalog.GetAll<FactionProductData>().Where(p => LoadoutGenerator.IsOffered(catalog, p)))
        {
            var design = catalog.Get(product.Design);
            if (design == null || (design is HullData hull && (hull.HullType == HullType.Station || hull.HullType == HullType.Turret))) continue;
            designs.TryAdd(catalog.RefOf(design).Key.Value, design);
        }
        return designs.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => pair.Value).ToList();
    }

    // The roles some design demands: those of every offered design and of every design a recipe makes, recipe or no.
    private static HashSet<string> DesignRoles(CultCache catalog) =>
        new HashSet<string>(OfferedDesigns(catalog).Concat(AllRecipes(catalog).Select(p => catalog.Get(p.Recipe.Output)).Where(o => o != null && !Recipes.IsCommodity(o)))
            .SelectMany(design => Recipes.RolesOf(design)).Select(role => role.Name), StringComparer.Ordinal);

    private static IEnumerable<ItemData> Commodities(CultCache catalog) =>
        catalog.GetAll<ItemData>().Where(Recipes.IsCommodity).OrderBy(item => catalog.RefOf(item).Key.Value, StringComparer.Ordinal);

    // Every commodity and every offered design (but a Station or Turret hull) has a recipe, and each recipe is well formed.
    public static List<EconomyFault> Recipe(CultCache catalog)
    {
        var faults = new List<EconomyFault>();
        void Add(string record, string message) => faults.Add(new EconomyFault(LRecipe, record, message));
        foreach (var item in Commodities(catalog))
            if (Recipes.RecipeOf(catalog, item) == null)
                faults.Add(new EconomyFault(LRecipe, catalog.RefOf(item).Key.Value, $"Commodity '{item.Name}' has no recipe.", pending: true));
        foreach (var design in OfferedDesigns(catalog))
            if (Recipes.RecipeOf(catalog, design) == null)
                faults.Add(new EconomyFault(LRecipe, catalog.RefOf(design).Key.Value, $"Offered design '{design.Name}' has no recipe.", pending: true));
        foreach (var market in catalog.GetAll<OutsideMarketData>())
        {
            var marketKey = catalog.RefOf(market).Key.Value;
            if (market.Rows == null) Add(marketKey, "Rows is null; an outside market holds a list, empty when it trades nothing.");
            else if (market.Rows.Contains(null)) Add(marketKey, "Rows holds a null entry.");
        }
        var kindsPlaced = new HashSet<string>(catalog.GetAll<Faction>().SelectMany(f => StationKinds.KindsOf(catalog, f)).Select(k => k.Key.Value), StringComparer.Ordinal);
        foreach (var (key, stored) in StoredRecipes(catalog))
        {
            var recipe = Sound(stored);
            foreach (var (list, name) in new[] { (stored.Inputs, "Inputs"), (stored.Upkeep, "Upkeep") })
                if (list == null) Add(key, $"{name} is null; a recipe holds a list, empty when it takes nothing.");
                else if (list.Contains(null)) Add(key, $"{name} holds a null entry.");
            if (stored.Fills == null) Add(key, "Fills is null; a recipe holds a list, empty when it fills no role.");
            else if (stored.Fills.Contains(null)) Add(key, "Fills holds a null entry.");
            if (stored.RoleInputs == null) Add(key, "RoleInputs is null; a recipe holds a list, empty when it takes no role.");
            else if (stored.RoleInputs.Contains(null)) Add(key, "RoleInputs holds a null entry.");
            var output = catalog.Get(recipe.Output);
            if (output == null) { Add(key, "Output names no item this catalog holds."); continue; }
            if (key != RecipeData.KeyOf(recipe.Output.Key).Value)
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
            if (roles.Count > 0 && recipe.RoleInputs.Count > 0) Add(key, "A role recipe has no RoleInputs; its role inputs are its design's roles.");
            foreach (var role in Recipes.RoleInputsOf(catalog, recipe))
            {
                if (string.IsNullOrWhiteSpace(role.Name)) { Add(key, "A role input has an empty name."); continue; }
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
        var rows = catalog.GetAll<OutsideMarketData>().SelectMany(market => market.Rows ?? new List<OutsideMarketRow>()).Where(r => r != null).ToList();
        return (new HashSet<string>(rows.Where(r => r.Sells).Select(r => r.Commodity.Key.Value), StringComparer.Ordinal),
            new HashSet<string>(rows.Where(r => r.Buys).Select(r => r.Commodity.Key.Value), StringComparer.Ordinal));
    }

    // The commodities an offered design's weapon draws as ammunition. One reader, shared by the sink and the consumer-demand rules.
    private static HashSet<string> Drawn(CultCache catalog) =>
        new HashSet<string>(catalog.GetAll<FactionProductData>().Where(p => LoadoutGenerator.IsOffered(catalog, p))
            .Select(p => catalog.Get(p.Design) as EquippableItemData).Where(design => design?.Behaviors != null)
            .SelectMany(design => design.Behaviors.OfType<WeaponData>()).Where(weapon => weapon.AmmoType.IsSet())
            .Select(weapon => weapon.AmmoType.Key.Value), StringComparer.Ordinal);

    // Every commodity has a source (a grounded chain, or a gate that sells it) and a sink (a recipe takes it or a role it fills, a role
    // some design (offered, or made by a recipe) names, a weapon some offered design carries draws it, or a gate that buys it). A consumer good's sink is judged once needs
    // tables exist.
    public static List<EconomyFault> SourceSink(CultCache catalog)
    {
        var faults = new List<EconomyFault>();
        var (sells, buys) = Gate(catalog);
        var drawn = Drawn(catalog);
        var grounded = Depths(catalog, sells);
        var recipes = AllRecipes(catalog).ToList();
        var used = new HashSet<string>(recipes.SelectMany(p => p.Recipe.Inputs.Concat(p.Recipe.Upkeep)).Select(c => c.Commodity.Key.Value), StringComparer.Ordinal);
        var rolesNamed = DesignRoles(catalog);
        rolesNamed.UnionWith(recipes.SelectMany(p => Recipes.RoleInputsOf(catalog, p.Recipe)).Select(r => r.Name));
        foreach (var (key, recipe) in recipes)
        {
            var output = catalog.Get(recipe.Output);
            if (!Recipes.IsCommodity(output)) continue;
            var outputKey = recipe.Output.Key.Value;
            if (!grounded.TryGetValue(outputKey, out var depth) || depth == null)
                faults.Add(new EconomyFault(LSource, key, $"'{output.Name}' has no source: no chain of recipes reaches raw goods and no gate sells it."));
            if (Recipes.IsConsumerGood(catalog, recipe)) continue;
            if (!used.Contains(outputKey) && !recipe.Fills.Any(rolesNamed.Contains) && !drawn.Contains(outputKey) && !buys.Contains(outputKey))
                faults.Add(new EconomyFault(LSource, key, $"'{output.Name}' has no sink: no recipe takes it or a role it fills, no design names a role it fills, no weapon draws it, no gate buys it."));
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
        foreach (var role in Recipes.RoleInputsOf(catalog, recipe))
        {
            int? shallowest = null;
            foreach (var filler in Recipes.Fillers(catalog, role.Name))
                if (depth.TryGetValue(filler.Value, out var d) && (shallowest == null || d < shallowest)) shallowest = d;
            if (shallowest == null) return null;
            deepest = Math.Max(deepest, shallowest.Value);
        }
        return deepest + 1;
    }

    // Hawkins-Simon over commodities plus one labour good, at the worst single filler of every role slot (a recipe's role inputs): the spectral radius
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
            foreach (var role in Recipes.RoleInputsOf(catalog, recipe))
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
        var faults = new List<EconomyFault>();
        string Name(int i) => i == labour ? "labour" : names[i];
        foreach (var (rho, component) in Radii(final, n))
        {
            if (rho < 1 - Margin) continue;
            var order = CycleOrder(final, n, component);
            faults.Add(new EconomyFault(LProductive, Name(order[0]),
                $"The goods cannot sustain themselves: spectral radius {rho:0.######} is not below 1, carried by {string.Join(" -> ", order.Select(Name))}.",
                members: order.Select(Name).OrderBy(name => name, StringComparer.Ordinal).ToArray(), radius: rho));
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

    // The spectral radius of every strongly connected component of a nonnegative matrix, each by power iteration on A + I
    // (primitive when irreducible). An edge j -> i is a nonzero A[i, j]. A component with no cycle has radius 0.
    private static List<(double Rho, List<int> Component)> Radii(double[,] a, int n)
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
        var radii = new List<(double, List<int>)>();
        for (var start = 0; start < n; start++)
        {
            if (done[start]) continue;
            var component = Enumerable.Range(0, n).Where(i => reach[start, i] && reach[i, start]).ToList();
            foreach (var i in component) done[i] = true;
            var rho = Irreducible(a, component);
            if (double.IsNaN(rho)) rho = double.PositiveInfinity;
            radii.Add((rho, component));
        }
        return radii;
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

    // A processed good owes a consumer use when it is shared (rulings consumer-demand-shared-only, draw-is-one-use). Its uses are
    // the gear or hull roles, and the rounds some weapon draws, that the goods downstream of it (itself included) fill or are; a
    // good used as upkeep is shared too. Shared means two or more such uses, or upkeep. A shared good must lead, through the
    // chain (an input, upkeep or role-input edge: a role input is an edge from each of its fillers), into a consumer good or a role one names. A good whose every use is one gear role is exempt like its final assembly.
    // Derived from the recipe graph on every check; depth and distance play no part. Raw goods and the gate count for nothing.
    public static List<EconomyFault> ConsumerDemand(CultCache catalog)
    {
        var recipes = AllRecipes(catalog).Where(p => catalog.Get(p.Recipe.Output) != null).ToDictionary(p => p.Recipe.Output.Key.Value, p => p.Recipe, StringComparer.Ordinal);
        var drawn = Drawn(catalog);
        bool IsCommodityRecipe(RecipeData r) => Recipes.IsCommodity(catalog.Get(r.Output));
        var gearRoles = DesignRoles(catalog);
        var upkeep = new HashSet<string>(recipes.Values.SelectMany(r => r.Upkeep).Select(c => c.Commodity.Key.Value), StringComparer.Ordinal);
        var takenBy = recipes.Values.Where(IsCommodityRecipe)
            .SelectMany(r => r.Inputs.Concat(r.Upkeep).Select(input => input.Commodity.Key.Value)
                .Concat(Recipes.RoleInputsOf(catalog, r).SelectMany(role => Recipes.Fillers(catalog, role.Name)).Select(filler => filler.Value))
                .Select(input => (Input: input, Output: r.Output.Key.Value)))
            .ToLookup(edge => edge.Input, edge => edge.Output, StringComparer.Ordinal);
        var faults = new List<EconomyFault>();
        foreach (var key in recipes.Keys.OrderBy(k => k, StringComparer.Ordinal))
        {
            var recipe = recipes[key];
            if (!IsCommodityRecipe(recipe) || Recipes.IsRaw(catalog, recipe) || Recipes.IsConsumerGood(catalog, recipe)) continue;
            var reached = new HashSet<string>(StringComparer.Ordinal) { key };
            var open = new Stack<string>(reached);
            while (open.Count > 0)
                foreach (var next in takenBy[open.Pop()])
                    if (reached.Add(next)) open.Push(next);
            var members = reached.Select(k => recipes[k]).ToList();
            if (members.Any(r => Recipes.IsConsumerGood(catalog, r))) continue;
            var uses = members.SelectMany(r => r.Fills).Where(gearRoles.Contains).Distinct(StringComparer.Ordinal).OrderBy(role => role, StringComparer.Ordinal)
                .Concat(reached.Where(drawn.Contains).OrderBy(k => k, StringComparer.Ordinal).Select(k => catalog.Get(recipes[k].Output).Name)).ToList();
            var asUpkeep = reached.Any(upkeep.Contains);
            if (asUpkeep) uses.Add("upkeep");
            if (uses.Count < 2 && !asUpkeep) continue;
            var list = uses.Count == 1 ? uses[0] : string.Join(", ", uses.Take(uses.Count - 1)) + " and " + uses[uses.Count - 1];
            faults.Add(new EconomyFault(LDemand, RecipeData.KeyOf(new CultRecordKey(key)).Value,
                $"'{catalog.Get(recipe.Output).Name}' leads into {list} but no use leads into a consumer good."));
        }
        return faults;
    }
}
