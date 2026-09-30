using GameCult.Caching;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

// Uses the existing database executable; mod authoring does not earn another binary target.
public static class ShipAuthoringCommands
{
    public static int Run(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("ship-authoring create <ship.cc> <stable-id> <name> | inspect <ship.cc> | validate <ship.cc> | compose <shipped.cc> <derived.cc> <mods-dir>");
            return 1;
        }
        try
        {
            if (args[0] == "compose" && args.Length == 4)
            {
                var count = ShipModCatalog.Compose(args[1], args[2], args[3]);
                Console.WriteLine($"Composed {count} mod ships into {Path.GetFullPath(args[2])}");
                return 0;
            }
            if (args[0] == "inspect" && args.Length == 2)
            {
                var (hull, ship) = ShipAuthoringStore.Load(args[1]);
                Console.WriteLine($"{ship.Id}: {hull.Name}, {hull.Hardpoints?.Count ?? 0} hardpoints, {ship.SchematicLines?.Count ?? 0} lines, {ship.SchematicLines?.Sum(line => line.Points?.Length / 3 ?? 0) ?? 0} points");
                return 0;
            }
            if (args[0] == "validate" && args.Length == 2)
            {
                var (hull, ship) = ShipAuthoringStore.Read(args[1]);
                Console.WriteLine($"{ship.Id}: {hull.Name}, {hull.Hardpoints.Count} hardpoints, {ship.SchematicLines.Count} lines");
                return 0;
            }
            if (args[0] == "create" && args.Length == 4)
            {
                var path = Path.GetFullPath(args[1]);
                if (File.Exists(path)) throw new InvalidOperationException($"Refusing to replace {path}");
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                var ship = new ShipAuthoring { Id = args[2], ModelAsset = "ship.glb", Anchors = new List<ShipAnchor>() };
                var hull = new HullData
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
            Console.Error.WriteLine("ship-authoring create <ship.cc> <stable-id> <name> | inspect <ship.cc> | validate <ship.cc> | compose <shipped.cc> <derived.cc> <mods-dir>");
            return 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }
}
