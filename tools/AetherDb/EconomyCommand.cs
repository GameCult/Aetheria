using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GameCult.Caching;
using GameCult.Caching.MessagePack;
using MessagePack;

// One batch of economy content: commodities, recipes, station kinds with stand-ins, outside-market rows and edits to existing
// items, written through EconomyAuthor. A batch is a class under tools/AetherDb/Economy/, named on the command line by its class
// name. It writes what it names and nothing else, so a revised batch converges to the same records; a rerun rewrites any recipe that has inputs as new bytes (their elements carry cache-minted ids), and only `author` with no batch is certain to print Nothing to write.
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

    // Sets one item's price, written under the type it is held as.
    public void SetPrice(string key, int price)
    {
        var item = _catalog.Get<ItemData>(new CultRecordKey(key));
        item.Price = price;
        Write(item.GetType(), item, new CultRecordKey(key));
    }

    private void Write(Type type, object document, CultRecordKey key)
    {
        if (!_touched.Contains(key.Value)) _touched.Add(key.Value);
        // A cache refuses to write one type onto a key another type holds (write ownership by written type): a batch that turns
        // a compound good into a raw one removes the old record first.
        var held = _catalog.Get(key);
        if (held != null && held.GetType() != type) _catalog.Remove(key);
        _catalog.UpsertAsync(type, document, key).GetAwaiter().GetResult();
    }
}

// economy report | author <batch>... [apply] | prices: the one writer of economy content and a reader of the catalog's economy report.
// EconomyChecks owns every rule; this command only calls it.
public static class EconomyCommand
{
    // The invariants whose faults the batches introduce refuse them. The rest (a missing sink, no consumer demand, a commodity
    // with no recipe yet) are completeness: reported, and settled by the batch that closes the chain.
    private static readonly string[] Shape =
        { EconomyChecks.LRecipe, EconomyChecks.LLore, EconomyChecks.LProductive, EconomyChecks.LOneKindList, EconomyChecks.LWeights, EconomyChecks.LShape, EconomyChecks.LPrice };

    // A batch is compiled code under tools/AetherDb/Economy/ and runs with the operator's authority: reviewed code, never an input.
    private const string Usage = "economy report | author [<batch>...] [apply] | " + EconomyPrices.Usage + " | balance [--loop [<seed>...]] [--cap <sim seconds>] | balance --drain [<seed>...] [--window <sim seconds>] [--horizon <sim hours>]\nbalance reads the chain and changes nothing; --loop adds the player loop (a mining sortie per seed, default seeds 1 2 3 69 97, every belt of each, cap 150 sim seconds a sortie) and the offered gear ladders; --drain, not in the same call, adds a station block per seed: sorties to fill, the window (default 600 sim s) on screen and by spans, and the horizon (default 24 sim h) by spans. A batch is reviewed code: it runs on a trial copy and must write only through EconomyAuthor. After the batches, author reprices every made good (a commodity, or a design with a recipe) to the price its chain derives; with no batch it reprices alone.";

    public static int Run(string[] args, TextWriter output)
    {
        if (args.Length > 0 && args[0] == "report")
        {
            using var db = AetherDb.OpenBooted();
            return Report(db.Cache, output);
        }
        if (args.Length > 0 && args[0] == "prices") return EconomyPrices.Run(args.Skip(1).ToArray(), output);
        if (args.Length > 0 && args[0] == "balance")
        {
            using var db = AetherDb.OpenBooted();
            var settings = AuthoredSettings.Load(db.Root).Read<GameplaySettings>("GameplaySettings");
            var rest = args.Skip(1).ToList();
            bool Option(string name, out double value)
            {
                value = 0;
                var at = rest.IndexOf(name);
                if (at < 0) return false;
                if (at + 1 >= rest.Count || !double.TryParse(rest[at + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out value) || !double.IsFinite(value) || value <= 0)
                    throw new FormatException(name);
                rest.RemoveRange(at, 2);
                return true;
            }
            double cap, window, horizon;
            bool capped, windowed, horizoned;
            try { (capped, windowed, horizoned) = (Option("--cap", out cap), Option("--window", out window), Option("--horizon", out horizon)); }
            catch (FormatException)
            {
                output.WriteLine(Usage);
                return 1;
            }
            // A cap the sortie takes as a float: a finite double too big for one is as bad as a word.
            if (capped && !float.IsFinite((float) cap))
            {
                output.WriteLine(Usage);
                return 1;
            }
            if (!capped) cap = EconomyLoop.DefaultCap;
            if (!windowed) window = EconomyDrain.DefaultWindowSeconds;
            if (!horizoned) horizon = EconomyDrain.DefaultHorizonHours;
            var loopAt = rest.IndexOf("--loop");
            var drainAt = rest.IndexOf("--drain");
            var seeds = new List<uint>();
            var flagAt = Math.Max(loopAt, drainAt);
            if (flagAt >= 0)
                foreach (var word in rest.Skip(flagAt + 1))
                {
                    if (!uint.TryParse(word, out var seed)) { output.WriteLine(Usage); return 1; }
                    seeds.Add(seed);
                }
            // One of the two stages a call, its seeds last; each option belongs to its own stage.
            if ((flagAt >= 0 ? rest.Count - 1 - seeds.Count : rest.Count) != 0 || (loopAt >= 0 && drainAt >= 0) || (capped && loopAt < 0) || ((windowed || horizoned) && drainAt < 0))
            {
                output.WriteLine(Usage);
                return 1;
            }
            var seedList = seeds.Count == 0 ? EconomyLoop.DefaultSeeds : seeds.ToArray();
            LoopReport loop = null;
            if (loopAt >= 0)
                loop = EconomyLoop.Report(db.Cache, settings, seedList.SelectMany(seed => EconomyLoop.Sorties(db.Root, seed, (float) cap)).ToList());
            List<DrainSample> drain = null;
            if (drainAt >= 0)
                drain = seedList.Select(seed => EconomyDrain.Run(db.Root, seed, window, horizon, EconomyLoop.Sorties(db.Root, seed, EconomyLoop.DefaultCap))).ToList();
            return Balance(db.Cache, settings.EconomyWageSpread, output, loop, drain);
        }
        if (args.Length > 0 && args[0] == "author")
        {
            var apply = args.Contains("apply");
            var names = args.Skip(1).Where(a => a != "apply").ToArray();
            var known = typeof(EconomyBatch).Assembly.GetTypes().Where(t => !t.IsAbstract && t.IsSubclassOf(typeof(EconomyBatch)))
                .OrderBy(t => t.Name, StringComparer.Ordinal).ToArray();
            var unknown = names.Where(n => known.All(t => !string.Equals(t.Name, n, StringComparison.OrdinalIgnoreCase))).ToArray();
            if (unknown.Length > 0)
            {
                output.WriteLine($"{unknown.Length} of the names given match no batch. Batches: {string.Join(", ", known.Select(t => t.Name))}");
                return 1;
            }
            var batches = names.Select(n => (EconomyBatch) Activator.CreateInstance(known.Single(t => string.Equals(t.Name, n, StringComparison.OrdinalIgnoreCase)))).ToList();
            return Author(Path.Combine(AetherDb.FindRoot(), "GameData", "Aetheria.cc"), batches, apply, output);
        }
        output.WriteLine(Usage);
        return 1;
    }

    // The static balance: the median implied wage, the H faults, one line per chain link by wage over the median, then the belts.
    // Report-only: nothing here is a Shape invariant. Exit 1 when any fault.
    public static int Balance(CultCache catalog, double wageSpread, TextWriter output, LoopReport loop = null, IReadOnlyList<DrainSample> drain = null)
    {
        var report = EconomyBalance.Balance(catalog, wageSpread);
        var faults = report.Faults.Concat(loop?.Faults ?? new List<EconomyFault>()).Concat(drain == null ? new List<EconomyFault>() : EconomyDrain.Report(drain)).ToList();
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        output.WriteLine($"median implied wage {report.WageReference.ToString("0.#####", inv)} per worker-second, spread {wageSpread.ToString(inv)}, {report.Links.Count} links, {report.Links.Count(l => l.Wage == null)} unpriced");
        output.WriteLine($"{faults.Count} faults");
        foreach (var group in faults.GroupBy(f => f.Invariant).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            output.WriteLine($"{group.Key} ({group.Count()})");
            foreach (var fault in group) output.WriteLine($"  {fault.Record}: {fault.Message}");
        }
        output.WriteLine("\nLinks by wage over the median (output | station kind | labour | price | derived cost | wage | ratio)");
        foreach (var l in report.Links.OrderBy(l => l.WageRatio ?? double.PositiveInfinity).ThenBy(l => l.Output, StringComparer.Ordinal))
            output.WriteLine(string.Format(inv, "  {0} ({7}) | {1} | {2:0.#} | {3:0.##} | {4:0.##} | {5} | {6}", l.Output, l.StationKind, l.Labour, l.Price, l.Cost,
                l.Wage.HasValue ? l.Wage.Value.ToString("0.#####", inv) : "unpriced " + l.Unpriced, l.WageRatio.HasValue ? l.WageRatio.Value.ToString("0.##", inv) + "x" : "-", l.Name));
        output.WriteLine("\nBelts (kind: abundance-weighted price and derived cost per unit; each ore: share, extraction wage ratio, price, cost)");
        foreach (var belt in report.Belts)
        {
            output.WriteLine(string.Format(inv, "  {0}: price {1:0.##}, cost {2:0.##}", belt.Kind, belt.Price, belt.Cost));
            foreach (var y in belt.Yields)
                output.WriteLine(string.Format(inv, "    {0} {1:P0} | wage {2} | price {3:0.##} | cost {4:0.##}", y.Ore, y.Share, y.WageRatio.HasValue ? y.WageRatio.Value.ToString("0.##", inv) + "x" : "-", y.Price, y.Cost));
        }
        if (loop != null) PrintLoop(loop, output);
        if (drain != null) foreach (var sample in drain) EconomyDrain.Print(sample, output);
        return faults.Count == 0 ? 0 : 1;
    }

    // The player loop and the ladders, as numbers: a line per sortie, the median and best rate and the purse, then each category's
    // width and its designs with the minutes of income each takes to afford. No verdict beyond the faults the report raised.
    private static void PrintLoop(LoopReport loop, TextWriter output)
    {
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        output.WriteLine("\nPlayer loop (seed belt | kind | weapons | outcome | rocks | sim s | hold full at | units | sold | refused | offered value | sell | ammo | repair | net per minute)");
        foreach (var s in loop.Samples)
            output.WriteLine(string.Format(inv, "  {0} {11}/{12} | {1} | {2} | {13} | {3} | {4:0.#} | {5} | {6} | {14} | {15} | {16} | {7} | {8} | {9} | {10:0.#}", s.Seed, s.BeltKind, s.Weapons, s.Rocks, s.SimSeconds,
                s.HoldFullAt.HasValue ? s.HoldFullAt.Value.ToString("0.#", inv) + " s" : "never", s.Units, s.SellValue, s.AmmoSpent, s.Repair, s.RatePerMinute, s.Belt + 1, s.Belts, s.Outcome, s.UnitsSold, s.UnitsRefused, s.OfferedValue));
        output.WriteLine(string.Format(inv, "  median {0:0.#} per minute, best {1:0.#} per minute over the {3} sorties that mined to a full hold or the cap; purse {2}", loop.Median, loop.Best, loop.Purse, loop.Samples.Count(s => s.Measured)));
        output.WriteLine(string.Format(inv, "  median net per sortie {0:0.#} (the afford band counts sorties of this net, resting on {1} measured sorties)", loop.MedianNet, loop.MeasuredSorties));
        string Minutes(double? m) => m.HasValue ? m.Value.ToString("0.#", inv) : "-";
        string Width(double? w) => w.HasValue ? w.Value.ToString("0.##", inv) + "x" : "-";
        output.WriteLine("\nLadders (category: width; each design: price | priced from | minutes at median | minutes at best | purse covers)");
        foreach (var ladder in loop.Ladders)
        {
            output.WriteLine($"  {ladder.Category}: {Width(ladder.Width)}{(ladder.TopSorties.HasValue ? $", top {ladder.TopSorties.Value.ToString("0.#", inv)} sorties" : "")}");
            foreach (var d in ladder.Designs)
                output.WriteLine(string.Format(inv, "    {0} ({1}) | {2} | {3} | {4} | {5} | {6}", d.Name, d.Key, d.Price, d.Chained ? "chain" : "authored",
                    Minutes(d.MinutesAtMedian), Minutes(d.MinutesAtBest), d.Covered ? "yes" : "no"));
        }
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
            var takes = string.Join(", ", (recipe.Inputs ?? new List<CommodityCount>()).Where(c => c != null).Select(c => $"{c.Count} x {c.Commodity.Key.Value}")
                .Concat(Recipes.RoleInputsOf(catalog, recipe).Where(r => r != null).Select(r => $"{r.Count} x {r.Name}")));
            output.WriteLine($"{recipe.Output.Key.Value} <- {(takes.Length == 0 ? "nothing" : takes)} | {catalog.Get(recipe.StationKind)?.Name ?? "no kind"}, cycle {recipe.Cycle}, workforce {recipe.Workforce} | {recipe.LoreHook}");
        }
        return report.Faults.Count == 0 ? 0 : 1;
    }

    // Runs the batches once, on a copy of the catalog, and checks that copy. What apply writes is that checked copy, swapped in
    // atomically, so a refusal leaves Aetheria.cc byte-identical. A batch is compiled code and is reviewed as code: it is trusted not
    // to touch the real file, and the command refuses by name if the file changed under it. Added, changed and unchanged are read by
    // comparing the original and the copy on disk. A shape fault the batches introduce (present after, absent before, by rule and
    // record) refuses; faults the catalog already held print and do not. The trial copy lives under scratchRoot (the temp folder unless a caller
    // that audits it names a private one).
    public static int Author(string catalogPath, IReadOnlyList<EconomyBatch> batches, bool apply, TextWriter output, CultDocumentRegistry registry = null, string scratchRoot = null)
    {
        if (apply && new FileInfo(catalogPath).IsReadOnly)
        {
            output.WriteLine("The batches are refused: Aetheria.cc is read-only. Clear the read-only flag, or check it out, and run again. The catalog is unchanged.");
            return 1;
        }
        var scratch = Directory.CreateDirectory(Path.Combine(scratchRoot ?? Path.GetTempPath(), "aetherdb-economy-" + Guid.NewGuid().ToString("N"))).FullName;
        try
        {
            var trial = Path.Combine(scratch, "Aetheria.cc");
            string fingerprint;
            Dictionary<string, EconomyFault> held;
            HashSet<string> recipesHeld;
            Dictionary<string, byte[]> original;
            try
            {
                fingerprint = Fingerprint(catalogPath);
                File.Copy(catalogPath, trial);
                new FileInfo(trial).IsReadOnly = false;
                using var cache = AetheriaStores.Open(catalogPath, registry: registry);
                held = new Dictionary<string, EconomyFault>(StringComparer.Ordinal);
                foreach (var fault in EconomyChecks.Check(cache).Faults) held[Identity(fault)] = fault;
                recipesHeld = new HashSet<string>(cache.GetAll<RecipeData>().Select(r => cache.RefOf(r).Key.Value), StringComparer.Ordinal);
                original = Records(cache);
            }
            catch (Exception e) when (e is UnauthorizedAccessException || e is IOException)
            {
                output.WriteLine(Held("read", e));
                return 1;
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
                    Reprice(author);
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
                File.Copy(trial, staged);
                if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(staged, File.GetUnixFileMode(catalogPath));
                File.Replace(staged, catalogPath, null);
            }
            catch (Exception e) when (e is UnauthorizedAccessException || e is IOException)
            {
                output.WriteLine($"\nThe batches are refused: Aetheria.cc could not be replaced ({e.Message}). Something holds the file: close Unity and any other editor or tool that has it open, then run again. The catalog is unchanged.");
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

    // Every made good (a commodity, or a design with a recipe) takes the price its chain derives. The one writer of a made price:
    // batches may set any, this overwrites it.
    private static void Reprice(EconomyAuthor author)
    {
        var prices = EconomyBalance.Prices(author.Catalog, out var unsettled);
        if (unsettled.Count > 0) throw new InvalidOperationException(EconomyBalance.Unsettled(author.Catalog, unsettled));
        foreach (var (key, derived) in prices.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            var price = EconomyBalance.Rounded(derived);
            if (author.Catalog.Get<ItemData>(new CultRecordKey(key))?.Price is int held && held != price) author.SetPrice(key, price);
        }
    }

    // Something holds Aetheria.cc open: a refusal that names the holder to close, never an exception.
    private static string Held(string verb, Exception e) =>
        $"The batches are refused: Aetheria.cc could not be {verb} ({e.Message}). Something holds the file: close Unity and any other editor or tool that has it open, then run again. The catalog is unchanged.";

    // A held cycle's radius may rise by this fraction without being a worse fault: it is float noise from rebuilding the same graph.
    private const double RiseNoise = 1e-12;

    // A fault is the same fault by rule, record and message, so fixing one fault on a record cannot hide a new one there. L-productive
    // is one fault per cycle: its identity is the cycle's member set, and the same cycle with a higher radius is a worse fault.
    private static string Identity(EconomyFault fault) =>
        fault.Members != null ? fault.Invariant + "|" + string.Join(",", fault.Members) : fault.Invariant + "|" + fault.Record + "|" + fault.Message;

    private static bool Introduced(EconomyFault fault, Dictionary<string, EconomyFault> held, HashSet<string> recipesHeld)
    {
        // "No recipe" is pending for a commodity that had none; it refuses where the batch took away a recipe the catalog held.
        if (fault.Pending) return recipesHeld.Contains(RecipeData.KeyOf(new CultRecordKey(fault.Record)).Value);
        if (!held.TryGetValue(Identity(fault), out var was)) return true;
        return fault.Members != null && fault.Radius > was.Radius * (1 + RiseNoise);
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
