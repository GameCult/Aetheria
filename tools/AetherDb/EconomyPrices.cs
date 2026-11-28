using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using GameCult.Caching;

// economy prices [<other Aetheria.cc>] [--mods <dir>]: the reader of every price-bearing field. ItemData.Price on each record,
// OutsideMarketRow.Price, a package's hull price (every <mods dir>/*/ship.cc, GameData/Mods by default) and the purse the code
// defaults GameplaySettings.StartingCredits to. Given a second catalog it prints old, new and ratio (old over new) per record: the
// second catalog is the old side. Read-only. A package's old price is not in the second catalog, so a package prints its price alone.
public static class EconomyPrices
{
    public const string Usage = "economy prices [<other Aetheria.cc>] [--mods <dir>]";

    public static int Run(string[] args, TextWriter output)
    {
        string other = null, mods = null;
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] == "--mods" && i + 1 < args.Length) mods = args[++i];
            else if (other == null && !args[i].StartsWith("--", StringComparison.Ordinal)) other = args[i];
            else { output.WriteLine(Usage); return 1; }
        }
        // The shipped file alone, on purpose: a package's hull price is read from its ship.cc below, so a composed catalog would list the
        // same hull twice, and the second catalog an old side is compared against is a shipped file.
        using var db = AetherDb.OpenShipped();
        var now = Read(db.Cache, mods ?? Path.Combine(db.Root, "GameData", "Mods"));
        if (other == null)
        {
            foreach (var (id, row) in now) output.WriteLine($"{id}\t{row.Label}\t{row.Price}");
            return 0;
        }
        using var then = AetheriaStores.Open(other);
        var was = Read(then, null);
        foreach (var id in now.Keys.Concat(was.Keys).Distinct().OrderBy(k => k, StringComparer.Ordinal))
        {
            var hasOld = was.TryGetValue(id, out var old);
            var hasNew = now.TryGetValue(id, out var current);
            var ratio = hasOld && hasNew && current.Price != 0 ? (old.Price / (double) current.Price).ToString("0.####", CultureInfo.InvariantCulture) : "-";
            output.WriteLine($"{id}\t{(hasNew ? current.Label : old.Label)}\t{(hasOld ? old.Price.ToString(CultureInfo.InvariantCulture) : "-")}\t{(hasNew ? current.Price.ToString(CultureInfo.InvariantCulture) : "-")}\t{ratio}");
        }
        return 0;
    }

    // Every price-bearing field, by an id that names the field and the record. mods null reads the catalog alone: no packages and no purse,
    // which the catalog does not hold.
    public static SortedDictionary<string, (string Label, long Price)> Read(CultCache catalog, string mods)
    {
        var rows = new SortedDictionary<string, (string Label, long Price)>(StringComparer.Ordinal);
        foreach (var item in catalog.GetAll<ItemData>())
            rows["item:" + catalog.RefOf(item).Key.Value] = ($"{item.GetType().Name} {item.Name}", item.Price);
        foreach (var market in catalog.GetAll<OutsideMarketData>())
            foreach (var row in market.Rows ?? new List<OutsideMarketRow>())
                if (row != null)
                    rows[$"market:{catalog.RefOf(market).Key.Value}:{row.Commodity.Key.Value}"] = ($"OutsideMarketRow {market.Name}", row.Price);
        if (mods == null) return rows;
        if (Directory.Exists(mods))
            foreach (var path in Directory.GetFiles(mods, "ship.cc", SearchOption.AllDirectories).OrderBy(p => p, StringComparer.Ordinal))
            {
                var (hull, ship, _) = ShipAuthoringStore.Load(path);
                rows["package:" + ship.Id] = ($"Package hull {hull.Name}", hull.Price);
            }
        rows["purse"] = ("GameplaySettings.StartingCredits (code default)", new GameplaySettings().StartingCredits);
        return rows;
    }
}
