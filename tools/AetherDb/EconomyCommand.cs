using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GameCult.Caching;
using GameCult.Caching.MessagePack;
using MessagePack;

// One batch of economy content: commodities, recipes, station kinds with stand-ins, outside-market rows and edits to existing
// items, written through EconomyAuthor. A batch is a class under tools/AetherDb/Economy/, named on the command line by its class
// name. It writes what it names and nothing else, so a revised batch converges and a rerun writes nothing.
public abstract class EconomyBatch
{
    public abstract void Author(EconomyAuthor author);
}

// What a batch writes through. Every write is compared with the stored record and counted as added, changed or unchanged; an
// unchanged record is not written.
public sealed class EconomyAuthor
{
    private static readonly MessagePackSerializerOptions Wire = CultDocumentMessagePackSerialization.OptionsFor(typeof(ItemData).Assembly);
    private readonly CultCache _catalog;
    private readonly List<(string Status, string Key)> _results = new List<(string, string)>();

    public EconomyAuthor(CultCache catalog) => _catalog = catalog;

    public IReadOnlyList<(string Status, string Key)> Results => _results;

    // The keys of the records this author added or changed.
    public IEnumerable<string> Written => _results.Where(r => r.Status != "unchanged").Select(r => r.Key);

    // The catalog as the batch has left it so far, for a batch that builds on its own earlier writes.
    public CultCache Catalog => _catalog;

    // Writes a record under a key: a new commodity, a recipe, a station kind, an outside market.
    public void Put<T>(T document, CultRecordKey key) where T : class
    {
        var held = _catalog.Get<T>(key);
        Record(key, held == null ? null : Bytes(typeof(T), held), typeof(T), document);
    }

    // Edits the one item of that name and type. A name that matches no item or several is an error the command reports.
    public void Edit<T>(string name, Action<T> edit) where T : ItemData
    {
        var named = _catalog.GetAll<T>().Where(item => item.Name == name).ToArray();
        if (named.Length != 1)
            throw new InvalidOperationException(named.Length == 0 ? $"The catalog holds no {typeof(T).Name} named '{name}'." : $"{named.Length} {typeof(T).Name} records are named '{name}'.");
        var key = _catalog.RefOf(named[0]).Key;
        var before = Bytes(named[0].GetType(), named[0]);
        edit(named[0]);
        Record(key, before, named[0].GetType(), named[0]);
    }

    private void Record(CultRecordKey key, byte[] held, Type type, object document)
    {
        var status = held == null ? "added" : held.AsSpan().SequenceEqual(Bytes(type, document)) ? "unchanged" : "changed";
        _results.Add((status, key.Value));
        if (status != "unchanged") _catalog.UpsertAsync(type, document, key).GetAwaiter().GetResult();
    }

    private static byte[] Bytes(Type type, object document) => MessagePackSerializer.Serialize(type, document, Wire);
}

// economy report | author <batch>... [apply]: the one writer of economy content and a reader of the catalog's economy report.
// EconomyChecks owns every rule; this command only calls it.
public static class EconomyCommand
{
    // The invariants whose faults on a record a batch wrote refuse it. The rest (a missing sink, no consumer demand, a commodity
    // with no recipe yet) are completeness: reported, and settled by the batch that closes the chain.
    private static readonly string[] Shape =
        { EconomyChecks.LRecipe, EconomyChecks.LLore, EconomyChecks.LProductive, EconomyChecks.LOneKindList };

    private const string Usage = "economy report | author <batch>... [apply]";

    public static int Run(string[] args)
    {
        if (args.Length > 0 && args[0] == "report")
        {
            using var db = AetherDb.Open();
            return Report(db.Cache, Console.Out);
        }
        if (args.Length > 1 && args[0] == "author")
        {
            var apply = args.Contains("apply");
            var names = args.Skip(1).Where(a => a != "apply").ToArray();
            var known = typeof(EconomyBatch).Assembly.GetTypes().Where(t => !t.IsAbstract && t.IsSubclassOf(typeof(EconomyBatch)))
                .OrderBy(t => t.Name, StringComparer.Ordinal).ToArray();
            var unknown = names.Where(n => known.All(t => !string.Equals(t.Name, n, StringComparison.OrdinalIgnoreCase))).ToArray();
            if (unknown.Length > 0 || names.Length == 0)
            {
                Console.WriteLine($"No batch named {string.Join(", ", unknown)}. Batches: {string.Join(", ", known.Select(t => t.Name))}");
                return 1;
            }
            var batches = names.Select(n => (EconomyBatch) Activator.CreateInstance(known.Single(t => string.Equals(t.Name, n, StringComparison.OrdinalIgnoreCase)))).ToList();
            return Author(Path.Combine(AetherDb.FindRoot(), "GameData", "Aetheria.cc"), batches, apply, Console.Out);
        }
        Console.WriteLine(Usage);
        return 1;
    }

    // Faults grouped by invariant, then one line per good with its depth, then one line per recipe. Fails on any fault.
    public static int Report(CultCache catalog, TextWriter output)
    {
        var report = EconomyChecks.Check(catalog);
        PrintFaults(report, output);
        PrintDepth(report, output);
        output.WriteLine();
        foreach (var recipe in catalog.GetAll<RecipeData>().OrderBy(r => catalog.RefOf(r).Key.Value, StringComparer.Ordinal))
        {
            var made = catalog.Get(recipe.Output);
            var takes = Recipes.IsRoleRecipe(catalog, recipe)
                ? string.Join(", ", Recipes.RolesOf(made).Select(r => $"{r.Count} x {r.Name}"))
                : string.Join(", ", (recipe.Inputs ?? new List<CommodityCount>()).Where(c => c != null).Select(c => $"{c.Count} x {c.Commodity.Key.Value}"));
            output.WriteLine($"{recipe.Output.Key.Value} <- {(takes.Length == 0 ? "nothing" : takes)} | {catalog.Get(recipe.StationKind)?.Name ?? "no kind"}, cycle {recipe.Cycle}, workforce {recipe.Workforce} | {recipe.LoreHook}");
        }
        return report.Faults.Count == 0 ? 0 : 1;
    }

    // Runs the batches on a copy of the catalog, prints what each record would become and the check report over the result, and
    // refuses when a shape rule faults a recipe a batch wrote. Only a passing apply opens the real file for writing, so a
    // refusal leaves it byte-identical.
    public static int Author(string catalogPath, IReadOnlyList<EconomyBatch> batches, bool apply, TextWriter output, CultDocumentRegistry registry = null)
    {
        var scratch = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "aetherdb-economy-" + Guid.NewGuid().ToString("N"))).FullName;
        try
        {
            var trial = Path.Combine(scratch, "Aetheria.cc");
            File.Copy(catalogPath, trial);
            var results = new List<(string Status, string Key)>();
            EconomyReport report;
            using (var cache = AetheriaStores.Open(trial, catalogWritable: true, registry: registry, kindlessStation: warning => output.WriteLine("warning: " + warning)))
            {
                var author = new EconomyAuthor(cache);
                try
                {
                    foreach (var batch in batches) batch.Author(author);
                    StationKinds.Validate(cache);
                }
                catch (Exception e)
                {
                    output.WriteLine($"The batch is refused: {e.Message}");
                    return 1;
                }
                results.AddRange(author.Results);
                foreach (var (status, key) in results) output.WriteLine($"{status} {key}");
                output.WriteLine($"{results.Count(r => r.Status == "added")} added, {results.Count(r => r.Status == "changed")} changed, {results.Count(r => r.Status == "unchanged")} unchanged");
                report = EconomyChecks.Check(cache);
                PrintFaults(report, output);
                PrintDepth(report, output);
                var written = new HashSet<string>(author.Written, StringComparer.Ordinal);
                var refused = report.Faults.Where(fault => Shape.Contains(fault.Invariant) && written.Contains(RecipeKeyOf(fault.Record))).ToArray();
                if (refused.Length > 0)
                {
                    output.WriteLine($"\nThe batch is refused: {refused.Length} shape faults on recipes it wrote.");
                    foreach (var fault in refused) output.WriteLine($"  {fault}");
                    return 1;
                }
            }
            if (!apply)
            {
                output.WriteLine("\nDry run. Pass \"apply\" to write Aetheria.cc.");
                return 0;
            }
            if (results.All(r => r.Status == "unchanged"))
            {
                output.WriteLine("\nNothing to write.");
                return 0;
            }
            using (var cache = AetheriaStores.Open(catalogPath, catalogWritable: true, registry: registry))
            {
                var author = new EconomyAuthor(cache);
                foreach (var batch in batches) batch.Author(author);
                cache.FlushAsync().GetAwaiter().GetResult();
            }
            output.WriteLine($"\nWrote {results.Count(r => r.Status != "unchanged")} records to Aetheria.cc");
            return 0;
        }
        finally
        {
            try { Directory.Delete(scratch, true); } catch (IOException) { }
        }
    }

    // A fault names a recipe key, or the key of the good a recipe makes.
    private static string RecipeKeyOf(string record) =>
        record.StartsWith("recipe:", StringComparison.Ordinal) ? record : RecipeData.KeyOf(new CultRecordKey(record)).Value;

    private static void PrintFaults(EconomyReport report, TextWriter output)
    {
        output.WriteLine($"{report.Faults.Count} faults");
        foreach (var group in report.Faults.GroupBy(f => f.Invariant).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            output.WriteLine($"{group.Key} ({group.Count()})");
            foreach (var fault in group) output.WriteLine($"  {fault.Record}: {fault.Message}");
        }
    }

    private static void PrintDepth(EconomyReport report, TextWriter output)
    {
        output.WriteLine("\nDepth");
        foreach (var pair in report.Depth.OrderBy(p => p.Key, StringComparer.Ordinal))
            output.WriteLine($"  {pair.Key} depth {(pair.Value.HasValue ? pair.Value.Value.ToString() : "none")}");
    }
}
