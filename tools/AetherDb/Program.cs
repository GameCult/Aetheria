/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MessagePack;

// Commands over the game database, run with: dotnet run --project tools/AetherDb -- <command>
public static class Program
{
    public static int Main(string[] args)
    {
        var command = args.FirstOrDefault() ?? "help";
        switch (command)
        {
            case "doctor": return Doctor();
            case "census": return Census();
            case "station-fit": return StationFit();
            case "migrate-products": return MigrateProducts(args.Contains("apply"));
            default:
                Console.WriteLine("commands: doctor, census, station-fit, migrate-products [apply]");
                return 1;
        }
    }

    // Every entry in the database deserializes, and what types it holds.
    private static int Doctor()
    {
        var db = AetherDb.Open();
        var bytes = File.ReadAllBytes(Path.Combine(db.Root, "GameData", "AetherDB.msgpack"));
        var entries = MessagePackSerializer.Deserialize<DatabaseEntry[]>(bytes);
        var nulls = entries.Select((e, i) => (e, i)).Where(x => x.e == null).Select(x => x.i).ToArray();
        Console.WriteLine($"{bytes.Length} bytes, {entries.Length} entries, {db.Cache.AllEntries.Count()} through the cache");
        Console.WriteLine(nulls.Length == 0 ? "no entries failed to deserialize" : $"FAILED at indices: {string.Join(", ", nulls)}");
        foreach (var group in entries.Where(e => e != null).GroupBy(e => e.GetType().Name).OrderBy(g => g.Key))
            Console.WriteLine($"  {group.Key}: {group.Count()}");
        return nulls.Length;
    }

    // Designs by kind and manufacturer, designs no product sells, and products with authored role quality.
    private static int Census()
    {
        var db = AetherDb.Open();
        var items = db.Cache.GetAll<EquippableItemData>().ToArray();
        var products = db.Cache.GetAll<FactionProductData>().ToArray();

        var kinds = new Dictionary<string, List<string>>();
        foreach (var item in items)
        {
            var kind = item is HullData hull ? $"Hull/{hull.HullType}"
                : item is DockingBayData ? "DockingBay"
                : item is CargoBayData ? "CargoBay"
                : item is WeaponItemData weapon ? $"Weapon/{weapon.HardpointType}"
                : item.HardpointType.ToString();
            if (!kinds.TryGetValue(kind, out var makers)) kinds[kind] = makers = new List<string>();
            makers.Add(db.Cache.Get<Faction>(item.Manufacturer)?.ShortName ?? "(none)");
        }

        Console.WriteLine($"{items.Length} designs, {products.Length} products\n");
        foreach (var kind in kinds.OrderBy(k => k.Key))
            Console.WriteLine($"{kind.Key,-22} {kind.Value.Count,3}  " + string.Join(", ",
                kind.Value.GroupBy(m => m).OrderByDescending(g => g.Count()).Select(g => $"{g.Key}:{g.Count()}")));

        var orphans = items.Where(i => !products.Any(p => p.Design == i.ID)).ToArray();
        Console.WriteLine($"\n{orphans.Length} designs no product sells, so they cannot spawn:");
        foreach (var item in orphans) Console.WriteLine($"  {item.Name}");

        var authored = products.Where(p => p.Roles != null && p.Roles.Count > 0).ToArray();
        Console.WriteLine($"\n{authored.Length} products carry role quality:");
        foreach (var product in authored)
            Console.WriteLine($"  {product.Name,-28} by {db.Cache.Get<Faction>(product.Manufacturer)?.ShortName ?? "(none)",-12} " +
                string.Join(", ", product.Roles.Select(r => $"{r.Role} {r.Mean:0.00}±{r.StandardDeviation:0.00}")));
        if (authored.Length == 0) Console.WriteLine("  none yet: add roles to a design, then set each product's means");
        return 0;
    }

    // Some docking bay fits every station hull, which LoadoutGenerator.GenerateStationLoadout requires.
    // Worst case: hardpoint gear is equipped first, leaving only the non-hardpoint interior free.
    private static int StationFit()
    {
        var db = AetherDb.Open();
        var bays = db.Cache.GetAll<DockingBayData>().ToArray();
        var failures = 0;
        foreach (var hull in db.Cache.GetAll<HullData>().Where(h => h.HullType == HullType.Station))
        {
            var worstCase = new Shape(hull.Shape.Width, hull.Shape.Height);
            foreach (var v in hull.InteriorCells.Coordinates) worstCase[v] = true;
            foreach (var hardpoint in hull.Hardpoints)
                foreach (var v in hardpoint.Shape.Coordinates)
                    worstCase[hardpoint.Position + v] = false;

            var fitting = bays.Where(b => b.Shape.FitsWithin(worstCase, out _, out _)).Select(b => b.Name).ToArray();
            if (fitting.Length == 0) failures++;
            Console.WriteLine($"{hull.Name}: {(fitting.Length == 0 ? "NO docking bay fits" : string.Join(", ", fitting))}");
        }
        return failures;
    }

    // Mints a product for every design that still carries a manufacturer, copying its name and description.
    private static int MigrateProducts(bool apply)
    {
        var db = AetherDb.Open();
        var products = db.Cache.GetAll<FactionProductData>().ToList();
        var designs = db.Cache.GetAll<CraftedItemData>()
            .Where(d => d.Manufacturer != Guid.Empty)
            .Where(d => !products.Any(p => p.Design == d.ID && p.Manufacturer == d.Manufacturer))
            .ToArray();

        Console.WriteLine($"{products.Count} products exist; {designs.Length} designs need one\n");
        foreach (var design in designs)
        {
            var product = new FactionProductData
            {
                Name = design.Name,
                Description = design.Description,
                Design = design.ID,
                Manufacturer = design.Manufacturer,
                // A design authored before roles existed has none; its product gets rows when roles are added
                Roles = (design.Roles ?? new List<ItemRole>()).Select(r => new ProductRole { Role = r.Name }).ToList()
            };
            Console.WriteLine($"  {product.Name,-28} by {db.Cache.Get<Faction>(product.Manufacturer)?.ShortName ?? "(unknown)"}");
            if (apply) db.Cache.Add(product);
        }

        if (apply && designs.Length > 0)
        {
            db.Save();
            Console.WriteLine($"\nSaved {designs.Length} products to AetherDB.msgpack");
        }
        else if (!apply) Console.WriteLine("\nDry run. Pass \"apply\" to write these to the database.");
        return 0;
    }
}
