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

// What a batch writes through. The command compares the trial catalog with the original on disk, so a write here is only recorded
// by key, in the order the batch made it.
public sealed class EconomyAuthor
{
    private readonly CultCache _catalog;
    private readonly List<string> _touched = new List<string>();

    public EconomyAuthor(CultCache catalog) => _catalog = catalog;

    // The keys this author wrote, in order, once each.
    public IReadOnlyList<string> Touched => _touched;

    // The catalog as the batch has left it so far, for a batch that builds on its own earlier writes.
    public CultCache Catalog => _catalog;

    // Writes a record under a key: a new commodity, a recipe, a station kind, an outside market.
    public void Put<T>(T document, CultRecordKey key) where T : class => Write(typeof(T), document, key);

    // Edits the one item of that name and type. A name that matches no item or several is an error the command reports.
    public void Edit<T>(string name, Action<T> edit) where T : ItemData
    {
        var named = _catalog.GetAll<T>().Where(item => item.Name == name).ToArray();
        if (named.Length != 1)
            throw new InvalidOperationException(named.Length == 0 ? $"The catalog holds no {typeof(T).Name} named '{name}'." : $"{named.Length} {typeof(T).Name} records are named '{name}'.");
        edit(named[0]);
        Write(named[0].GetType(), named[0], _catalog.RefOf(named[0]).Key);
    }

    private void Write(Type type, object document, CultRecordKey key)
    {
        if (!_touched.Contains(key.Value)) _touched.Add(key.Value);
        _catalog.UpsertAsync(type, document, key).GetAwaiter().GetResult();
    }
}

// economy report | author <batch>... [apply]: the one writer of economy content and a reader of the catalog's economy report.
// EconomyChecks owns every rule; this command only calls it.
public static class EconomyCommand
{
    // The invariants whose faults the batches introduce refuse them. The rest (a missing sink, no consumer demand, a commodity
    // with no recipe yet) are completeness: reported, and settled by the batch that closes the chain.
    private static readonly string[] Shape =
        { EconomyChecks.LRecipe, EconomyChecks.LLore, EconomyChecks.LProductive, EconomyChecks.LOneKindList };

    // A batch is compiled code under tools/AetherDb/Economy/ and runs with the operator's authority: reviewed code, never an input.
    private const string Usage = "economy report | author <batch>... [apply]\nA batch is reviewed code: it runs on a trial copy and must write only through EconomyAuthor.";

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

    // Runs the batches once, on a copy of the catalog, and checks that copy. What apply writes is that checked copy, swapped in
    // atomically, so a refusal leaves Aetheria.cc byte-identical. A batch is compiled code and is reviewed as code: it is trusted not
    // to touch the real file, and the command refuses by name if the file changed under it. Added, changed and unchanged are read by
    // comparing the original and the copy on disk. A shape fault the batches introduce (present after, absent before, by rule and
    // record) refuses; faults the catalog already held print and do not.
    public static int Author(string catalogPath, IReadOnlyList<EconomyBatch> batches, bool apply, TextWriter output, CultDocumentRegistry registry = null)
    {
        var scratch = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "aetherdb-economy-" + Guid.NewGuid().ToString("N"))).FullName;
        try
        {
            var trial = Path.Combine(scratch, "Aetheria.cc");
            var fingerprint = Fingerprint(catalogPath);
            File.Copy(catalogPath, trial);
            Dictionary<string, EconomyFault> held;
            HashSet<string> recipesHeld;
            Dictionary<string, byte[]> original;
            using (var cache = AetheriaStores.Open(catalogPath, registry: registry))
            {
                held = new Dictionary<string, EconomyFault>(StringComparer.Ordinal);
                foreach (var fault in EconomyChecks.Check(cache).Faults) held[Identity(fault)] = fault;
                recipesHeld = new HashSet<string>(cache.GetAll<RecipeData>().Select(r => cache.RefOf(r).Key.Value), StringComparer.Ordinal);
                original = Records(cache);
            }
            var touched = new List<string>();
            int written;
            using (var cache = AetheriaStores.Open(trial, catalogWritable: true, registry: registry, kindlessStation: warning => output.WriteLine("warning: " + warning)))
            {
                var author = new EconomyAuthor(cache);
                var running = "";
                try
                {
                    foreach (var batch in batches)
                    {
                        running = batch.GetType().Name;
                        batch.Author(author);
                    }
                    running = "";
                    StationKinds.Validate(cache);
                }
                catch (Exception e)
                {
                    output.WriteLine(running.Length == 0 ? $"The batches are refused: {e.Message}" : $"The batch {running} is refused: {e.Message}");
                    return 1;
                }
                touched.AddRange(author.Touched);
                cache.FlushAsync().GetAwaiter().GetResult();
            }
            if (Fingerprint(catalogPath) != fingerprint)
            {
                output.WriteLine("The batches are refused: Aetheria.cc changed during the trial. Batch code must write only through EconomyAuthor.");
                return 1;
            }
            using (var cache = AetheriaStores.Open(trial, registry: registry))
            {
                var trialRecords = Records(cache);
                var status = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var (id, bytes) in trialRecords)
                    if (!original.TryGetValue(id, out var was)) status[KeyOf(id)] = "added";
                    else if (!was.AsSpan().SequenceEqual(bytes)) status[KeyOf(id)] = "changed";
                foreach (var id in original.Keys)
                    if (!trialRecords.ContainsKey(id)) status[KeyOf(id)] = "removed";
                var listed = touched.Concat(status.Keys.OrderBy(k => k, StringComparer.Ordinal).Where(k => !touched.Contains(k))).ToList();
                foreach (var key in listed) output.WriteLine($"{(status.TryGetValue(key, out var s) ? s : "unchanged")} {key}");
                output.WriteLine($"{status.Values.Count(v => v == "added")} added, {status.Values.Count(v => v == "changed")} changed, {status.Values.Count(v => v == "removed")} removed, {listed.Count - status.Count} unchanged");
                var report = EconomyChecks.Check(cache);
                PrintFaults(report, output);
                PrintDepth(report, output);
                var introduced = report.Faults.Where(fault => Shape.Contains(fault.Invariant) && Introduced(fault, held, recipesHeld)).ToArray();
                if (introduced.Length > 0)
                {
                    output.WriteLine($"\nThe batch is refused: {introduced.Length} shape faults the batches introduce.");
                    foreach (var fault in introduced) output.WriteLine($"  {fault}");
                    return 1;
                }
                written = status.Count;
                if (status.Count == 0 || !apply)
                {
                    output.WriteLine(status.Count == 0 ? "\nNothing to write." : "\nDry run. Pass \"apply\" to write Aetheria.cc.");
                    return 0;
                }
            }
            var staged = catalogPath + ".economy-" + Guid.NewGuid().ToString("N");
            try
            {
                if (new FileInfo(catalogPath).IsReadOnly)
                {
                    output.WriteLine("
The batches are refused: Aetheria.cc is read-only. Clear the read-only flag, or check it out, and run again. The catalog is unchanged.");
                    return 1;
                }
                File.Copy(trial, staged);
                if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(staged, File.GetUnixFileMode(catalogPath));
                File.Replace(staged, catalogPath, null);
            }
            catch (Exception e) when (e is UnauthorizedAccessException || e is IOException)
            {
                output.WriteLine($"
The batches are refused: Aetheria.cc could not be replaced ({e.Message}). Something holds the file: close Unity and any other editor or tool that has it open, then run again. The catalog is unchanged.");
                return 1;
            }
            finally { if (File.Exists(staged)) File.Delete(staged); }
            output.WriteLine($"\nWrote {written} records to Aetheria.cc");
            return 0;
        }
        finally
        {
            try { Directory.Delete(scratch, true); } catch (IOException) { }
        }
    }

    // A fault is the same fault by rule, record and message, so fixing one fault on a record cannot hide a new one there. L-productive
    // is one fault per cycle: its identity is the cycle's member set, and the same cycle with a higher radius is a worse fault.
    private static string Identity(EconomyFault fault) =>
        fault.Members != null ? fault.Invariant + "|" + string.Join(",", fault.Members) : fault.Invariant + "|" + fault.Record + "|" + fault.Message;

    private static bool Introduced(EconomyFault fault, Dictionary<string, EconomyFault> held, HashSet<string> recipesHeld)
    {
        // "No recipe" is pending for a commodity that had none; it refuses where the batch took away a recipe the catalog held.
        if (fault.Pending) return recipesHeld.Contains(RecipeData.KeyOf(new CultRecordKey(fault.Record)).Value);
        if (!held.TryGetValue(Identity(fault), out var was)) return true;
        return fault.Members != null && fault.Radius > was.Radius + 1e-9;
    }

    private static readonly MessagePackSerializerOptions Wire = CultDocumentMessagePackSerialization.OptionsFor(typeof(ItemData).Assembly);

    private static string Fingerprint(string path) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path)));

    // Every record in the catalog as its wire bytes, under "schema|key".
    private static Dictionary<string, byte[]> Records(CultCache catalog) =>
        catalog.AllStoredDocuments.ToDictionary(d => d.Descriptor.SchemaName + "|" + d.Key.Value, d => MessagePackSerializer.Serialize(d.Document.GetType(), d.Document, Wire), StringComparer.Ordinal);

    private static string KeyOf(string id) => id.Substring(id.IndexOf('|') + 1);

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
