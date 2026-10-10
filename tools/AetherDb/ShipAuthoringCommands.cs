using GameCult.Caching;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

// Uses the existing database executable; mod authoring does not earn another binary target.
public static class ShipAuthoringCommands
{
    private const string Usage = "ship-authoring create <ship.cc> <stable-id> <name> [--like <shipped hull name>] | inspect <ship.cc> | validate <ship.cc> | compose <shipped.cc> <derived.cc> <mods-dir>";

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
                var (hull, ship) = ShipAuthoringStore.Load(args[1]);
                Console.WriteLine($"{ship.Id}: {hull.Name}, {hull.Hardpoints?.Count ?? 0} hardpoints, {ship.SchematicLines?.Count ?? 0} lines, {ship.SchematicLines?.Sum(line => line.Points?.Length / 3 ?? 0) ?? 0} points");
                return 0;
            }
            if (args[0] == "validate" && args.Length == 2)
            {
                // The package's GLB is judged too once it exists (node ids, mesh roles); a draft without one is judged on
                // its records alone, and the output says which.
                var (hull, ship) = ShipAuthoringStore.Load(args[1]);
                var model = string.IsNullOrWhiteSpace(ship.ModelAsset) ? null
                    : Path.Combine(Path.GetDirectoryName(Path.GetFullPath(args[1])), ship.ModelAsset);
                var judged = model != null && File.Exists(model) ? "records and model" : "records only, no model yet";
                if (judged == "records and model") ShipModCatalog.ReadPackage(args[1]);
                else ShipAuthoringStore.Validate(hull, ship);
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
            Console.Error.WriteLine(Usage);
            return 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
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
