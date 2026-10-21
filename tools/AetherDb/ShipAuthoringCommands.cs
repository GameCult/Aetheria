using GameCult.Caching;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

// Uses the existing database executable; mod authoring does not earn another binary target.
public static class ShipAuthoringCommands
{
    private const string Usage = "ship-authoring create <ship.cc> <stable-id> <name> [--like <shipped hull name>] | inspect <ship.cc> | centre <ship.cc> | validate <ship.cc> | product <ship.cc> <maker> (--name <name> [--description <text>] [--not-offered] | --remove) | price <ship.cc> <price> | stats <ship.cc> [--mass m] [--durability d] [--armor a] [--drag x] [--top-speed v] | compose <shipped.cc> <derived.cc> <mods-dir>";

    private static readonly string[] StatFlags = { "--mass", "--durability", "--armor", "--drag", "--top-speed" };

    public static int Run(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine(Usage);
            return 1;
        }
        try
        {
            if (args[0] == "compose" && args.Length == 4)
            {
                var composition = ShipModCatalog.Compose(args[1], args[2], args[3]);
                Console.WriteLine($"Composed {composition.Included.Length} mod ships into {Path.GetFullPath(args[2])}");
                foreach (var exclusion in composition.Excluded) Console.Error.WriteLine($"Excluded {exclusion}");
                return composition.Excluded.Length == 0 ? 0 : 1;
            }
            if (args[0] == "inspect" && args.Length == 2)
            {
                var (hull, ship, products) = ShipAuthoringStore.Load(args[1]);
                Console.WriteLine($"{ship.Id}: {hull.Name}, {hull.Hardpoints?.Count ?? 0} hardpoints, {ship.SchematicLines?.Count ?? 0} lines, {ship.SchematicLines?.Sum(line => line.Points?.Length / 3 ?? 0) ?? 0} points");
                foreach (var product in products) Console.WriteLine($"product {product.Manufacturer.Key.Value}: {product.Name}");
                return 0;
            }
            if (args[0] == "centre" && args.Length == 2)
            {
                // The one centre of mass Blender places a ship by: Shape.CenterOfMass in cell coordinates, as "x y".
                var centre = ShipAuthoringStore.Load(args[1]).Hull.Shape.CenterOfMass;
                Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"{centre.x:R} {centre.y:R}"));
                return 0;
            }
            if (args[0] == "validate" && args.Length == 2)
            {
                // The package's GLB is judged too once it exists (node ids, mesh roles); a draft without one is judged on
                // its records alone, and the output says which.
                var (hull, ship, products) = ShipAuthoringStore.Load(args[1]);
                var model = string.IsNullOrWhiteSpace(ship.ModelAsset) ? null
                    : Path.Combine(Path.GetDirectoryName(Path.GetFullPath(args[1])), ship.ModelAsset);
                var judged = model != null && File.Exists(model) ? "records and model" : "records only, no model yet";
                if (judged == "records and model") ShipModCatalog.ReadPackage(args[1]);
                else ShipAuthoringStore.ValidateLoaded(hull, ship, products);
                Console.WriteLine($"{ship.Id}: {hull.Name}, {hull.Hardpoints.Count} hardpoints, {ship.Anchors.Count} anchors, {ship.SchematicLines.Count} lines ({judged})");
                return 0;
            }
            if (args[0] == "create" && (args.Length == 4 || args.Length == 6 && args[4] == "--like"))
            {
                ShipAuthoringStore.RequireShipId(args[2]);
                var path = Path.GetFullPath(args[1]);
                if (File.Exists(path)) throw new InvalidOperationException($"Refusing to replace {path}");
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                var ship = new ShipAuthoring { Id = args[2], ModelAsset = "ship.glb", Anchors = new List<ShipAnchor>() };
                var hull = args.Length == 6
                    ? ShipAuthoringStore.HullLike(ShippedShipHull(args[5]), args[3], ship.Id)
                    : new HullData
                    {
                        Name = args[3],
                        Shape = new Shape(),
                        Visual = new CultRecordRef<ShipAuthoring>(ShipModCatalog.AuthoringKey(ship.Id))
                    };
                using (var cache = ShipAuthoringStore.Open(path, writable: true))
                {
                    ShipAuthoringStore.Write(cache, hull, ship);
                    cache.FlushAsync().Wait();
                }
                Console.WriteLine($"Created draft {path}; fill its model bindings and schematic in Blender.");
                return 0;
            }
            if (args[0] == "price" && args.Length == 3)
            {
                // Judged before the file is touched: a refusal leaves it byte-identical.
                if (!int.TryParse(args[2], NumberStyles.None, CultureInfo.InvariantCulture, out var price))
                    throw new InvalidOperationException("price must be a non-negative whole number.");
                var (hull, ship, _) = ShipAuthoringStore.Load(args[1]);
                hull.Price = price;
                using (var cache = ShipAuthoringStore.Open(args[1], writable: true))
                {
                    ShipAuthoringStore.Write(cache, hull, ship);
                    cache.FlushAsync().Wait();
                }
                Console.WriteLine($"{ship.Id}: price {price}.");
                return 0;
            }
            if (args[0] == "stats" && args.Length >= 2)
            {
                // Every flag is judged before the file is touched: a refusal leaves it byte-identical.
                var stats = new Dictionary<string, float>(StringComparer.Ordinal);
                for (var i = 2; i < args.Length; i += 2)
                {
                    if (Array.IndexOf(StatFlags, args[i]) < 0) throw new InvalidOperationException($"stats: unknown flag {args[i]}.");
                    if (stats.ContainsKey(args[i])) throw new InvalidOperationException($"stats: {args[i]} is given twice.");
                    if (i + 1 == args.Length) throw new InvalidOperationException($"stats: {args[i]} needs a value.");
                    if (!float.TryParse(args[i + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || !float.IsFinite(value) || value < 0)
                        throw new InvalidOperationException($"stats: {args[i]} must be a finite, non-negative number.");
                    stats[args[i]] = value;
                }
                if (stats.Count == 0) throw new InvalidOperationException("stats: name at least one stat.");
                var (hull, ship, _) = ShipAuthoringStore.Load(args[1]);
                if (stats.TryGetValue("--top-speed", out var topSpeed))
                {
                    var limits = (hull.Behaviors ?? new List<BehaviorData>()).OfType<VelocityLimitData>().ToArray();
                    if (limits.Length != 1) throw new InvalidOperationException($"{ship.Id}: --top-speed needs exactly one VelocityLimit on the hull; it has {limits.Length}.");
                    // A fixed top speed, as every shipped hull authors one (Min equal to Max); its terms are kept.
                    limits[0].TopSpeed.Min = limits[0].TopSpeed.Max = topSpeed;
                }
                if (stats.TryGetValue("--mass", out var mass)) hull.Mass = mass;
                if (stats.TryGetValue("--durability", out var durability)) hull.Durability = durability;
                if (stats.TryGetValue("--armor", out var armor)) hull.Armor = armor;
                if (stats.TryGetValue("--drag", out var drag)) hull.Drag = drag;
                using (var cache = ShipAuthoringStore.Open(args[1], writable: true))
                {
                    ShipAuthoringStore.Write(cache, hull, ship);
                    cache.FlushAsync().Wait();
                }
                Console.WriteLine($"{ship.Id}: " + string.Join(", ", stats.Select(stat => string.Create(CultureInfo.InvariantCulture, $"{stat.Key.Substring(2)} {stat.Value:R}"))) + ".");
                return 0;
            }
            if (args[0] == "product" && args.Length >= 4)
            {
                // A trailing --not-offered takes the product off the market (ruling off-market-is-unoffered-product); --remove takes none.
                var notOffered = args[args.Length - 1] == "--not-offered";
                var count = notOffered ? args.Length - 1 : args.Length;
                var remove = !notOffered && count == 4 && args[3] == "--remove";
                var named = count >= 5 && args[3] == "--name" && (count == 5 || count == 7 && args[5] == "--description");
                if (!remove && !named) { Console.Error.WriteLine(Usage); return 1; }
                var (hull, ship, products) = ShipAuthoringStore.Load(args[1]);
                var (makerKey, makerName) = ShippedFaction(args[2]);
                if (remove)
                {
                    if (!products.Any(product => product.Manufacturer.Key.Equals(makerKey)))
                        throw new InvalidOperationException($"{ship.Id}: the package holds no product by that maker.");
                }
                else if (string.IsNullOrWhiteSpace(args[4]))
                    throw new InvalidOperationException("--name: a product needs a name.");
                using (var cache = ShipAuthoringStore.Open(args[1], writable: true))
                {
                    if (remove) ShipAuthoringStore.DeleteProduct(cache, ship.Id, makerKey);
                    else
                        ShipAuthoringStore.WriteProduct(cache, ship.Id, new FactionProductData
                        {
                            Name = args[4],
                            Description = count == 7 ? args[6] : null,
                            Design = new CultRecordRef<CraftedItemData>(ShipModCatalog.HullKey(ship.Id)),
                            Manufacturer = new CultRecordRef<Faction>(makerKey),
                            Roles = hull.Roles.Select(role => new ProductRole { Role = role.Name }).ToList(),
                            NotOffered = notOffered
                        });
                    cache.FlushAsync().Wait();
                }
                Console.WriteLine($"{ship.Id}: {(remove ? "removed" : "wrote")} the product by {makerName}{(notOffered ? ", not offered" : "")}.");
                return 0;
            }
            Console.Error.WriteLine(Usage);
            return 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    // A shipped faction by name or short name, from GameData/Aetheria.cc opened read-only: exactly one, or refused.
    private static (CultRecordKey Key, string Name) ShippedFaction(string nameOrShortName)
    {
        using var catalog = AetherDb.Open().Cache;
        var matches = catalog.GetAll<Faction>()
            .Where(faction => string.Equals(faction.Name, nameOrShortName, StringComparison.Ordinal) ||
                              string.Equals(faction.ShortName, nameOrShortName, StringComparison.Ordinal)).ToArray();
        if (matches.Length != 1)
            throw new InvalidOperationException($"<maker>: the shipped catalog has {matches.Length} factions with that name or short name; exactly one is required.");
        return (catalog.TryGetHandle(matches[0]).Value.Key, matches[0].Name);
    }

    // A shipped ship hull by name, from GameData/Aetheria.cc opened read-only.
    private static HullData ShippedShipHull(string name)
    {
        using var catalog = AetherDb.Open().Cache;
        var hull = catalog.GetAll<HullData>().FirstOrDefault(candidate => candidate.Name == name) ??
            throw new InvalidOperationException($"--like: the shipped catalog has no hull named '{name}'.");
        if (hull.HullType != HullType.Ship)
            throw new InvalidOperationException($"--like: '{name}' is a {hull.HullType} hull, not a ship.");
        return hull;
    }
}
