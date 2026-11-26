using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using GameCult.Caching;

// economy rescale <divisor> [--mods <dir>] [apply]: one transform that divides every gear, consumable and package hull price by the
// divisor (aetheria-living-world, cut gear-money-rescale). Equippable and Consumable items are gear scale; commodities are already
// ore scale and are not touched. Each price becomes round(price / divisor), half away from zero, never below 1. The catalog goes
// through EconomyCommand.Author's trial copy and swap, the packages through ShipAuthoringStore Load and Write. The dry run prints
// every price that moves, read through EconomyPrices. Deleted in the commit after it applies.
public static class EconomyRescale
{
    public const string Usage = "economy rescale <divisor> [--mods <dir>] [apply]";

    public static int Run(string[] args, TextWriter output)
    {
        var apply = args.Contains("apply");
        string mods = null;
        var rest = new List<string>();
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] == "apply") continue;
            if (args[i] == "--mods" && i + 1 < args.Length) mods = args[++i];
            else rest.Add(args[i]);
        }
        if (rest.Count != 1 || !int.TryParse(rest[0], NumberStyles.None, CultureInfo.InvariantCulture, out var divisor) || divisor < 2)
        {
            output.WriteLine(Usage);
            return 1;
        }
        return Apply(AetherDb.FindRoot(), divisor, mods, apply, output);
    }

    // The transform on the game folder at root: dry run unless apply.
    public static int Apply(string root, int divisor, string mods, bool apply, TextWriter output)
    {
        var catalogPath = Path.Combine(root, "GameData", "Aetheria.cc");
        mods ??= Path.Combine(root, "GameData", "Mods");

        // The catalog: the transform runs on a scratch copy first, so the table below is what the real run writes.
        var scratch = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "aetherdb-rescale-" + Guid.NewGuid().ToString("N"))).FullName;
        try
        {
            var copy = Path.Combine(scratch, "Aetheria.cc");
            File.Copy(catalogPath, copy);
            new FileInfo(copy).IsReadOnly = false;
            var trialOutput = new StringWriter();
            if (EconomyCommand.Author(copy, new List<EconomyBatch> { new Divide(divisor) }, true, trialOutput) != 0)
            {
                output.WriteLine(trialOutput.ToString());
                return 1;
            }
            using (var before = AetheriaStores.Open(catalogPath))
            using (var after = AetheriaStores.Open(copy))
            {
                var was = EconomyPrices.Read(before, null);
                var now = EconomyPrices.Read(after, null);
                foreach (var (id, row) in was)
                    if (now[id].Price != row.Price) output.WriteLine($"{id}\t{row.Label}\t{row.Price}\t{now[id].Price}");
            }
        }
        finally { Directory.Delete(scratch, true); }

        var packages = Directory.Exists(mods) ? Directory.GetFiles(mods, "ship.cc", SearchOption.AllDirectories).OrderBy(p => p, StringComparer.Ordinal).ToArray() : new string[0];
        foreach (var path in packages)
        {
            var (hull, ship, _) = ShipAuthoringStore.Load(path);
            output.WriteLine($"package:{ship.Id}\tPackage hull {hull.Name}\t{hull.Price}\t{Scale(hull.Price, divisor)}");
        }
        if (!apply)
        {
            output.WriteLine("\nDry run. Pass \"apply\" to write Aetheria.cc and the packages. The purse is GameplaySettings.StartingCredits in Settings.cs.");
            return 0;
        }
        var code = EconomyCommand.Author(catalogPath, new List<EconomyBatch> { new Divide(divisor) }, true, TextWriter.Null);
        if (code != 0) return code;
        foreach (var path in packages)
        {
            var (hull, ship, _) = ShipAuthoringStore.Load(path);
            hull.Price = Scale(hull.Price, divisor);
            using var cache = ShipAuthoringStore.Open(path, writable: true);
            ShipAuthoringStore.Write(cache, hull, ship);
            cache.FlushAsync().Wait();
        }
        output.WriteLine($"\nWrote Aetheria.cc and {packages.Length} package(s).");
        return 0;
    }

    // Half away from zero, never below 1 for a priced item; an unpriced one stays unpriced.
    private static int Scale(int price, int divisor) =>
        price <= 0 ? price : Math.Max(1, (int) Math.Round(price / (double) divisor, MidpointRounding.AwayFromZero));

    private sealed class Divide : EconomyBatch
    {
        private readonly int _divisor;
        public Divide(int divisor) => _divisor = divisor;

        public override void Author(EconomyAuthor author)
        {
            var catalog = author.Catalog;
            foreach (var item in catalog.GetAll<ItemData>().Where(i => i is EquippableItemData || i is ConsumableItemData)
                         .OrderBy(i => catalog.RefOf(i).Key.Value, StringComparer.Ordinal).ToArray())
            {
                item.Price = Scale(item.Price, _divisor);
                // The record's own type, so the cache's write ownership by type holds.
                author.Put((dynamic) item, catalog.RefOf(item).Key);
            }
        }
    }
}
