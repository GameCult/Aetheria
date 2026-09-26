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
            Console.Error.WriteLine("ship-authoring create <ship.cc> <stable-id> <name> | inspect <ship.cc> | validate <ship.cc>");
            return 1;
        }
        try
        {
            if (args[0] == "inspect" && args.Length == 2)
            {
                using var cache = ShipAuthoringStore.Open(args[1]);
                var records = cache.GetAll<ShipAuthoring>().ToArray();
                if (records.Length != 1) throw new InvalidOperationException($"Expected one ship, found {records.Length}.");
                var ship = records[0];
                Console.WriteLine($"{ship.Id}: {ship.Hull?.Name}, {ship.Hull?.Hardpoints?.Count ?? 0} hardpoints, {ship.SchematicLines?.Count ?? 0} lines, {ship.SchematicLines?.Sum(line => line.Points?.Length / 3 ?? 0) ?? 0} points");
                return 0;
            }
            if (args[0] == "validate" && args.Length == 2)
            {
                var ship = ShipAuthoringStore.Read(args[1]);
                Console.WriteLine($"{ship.Id}: {ship.Hull.Name}, {ship.Hull.Hardpoints.Count} hardpoints, {ship.SchematicLines.Count} lines");
                return 0;
            }
            if (args[0] == "create" && args.Length == 4)
            {
                var path = Path.GetFullPath(args[1]);
                if (File.Exists(path)) throw new InvalidOperationException($"Refusing to replace {path}");
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                var ship = new ShipAuthoring
                {
                    Id = args[2],
                    Hull = new HullData { Name = args[3], Shape = new Shape() },
                    ModelAsset = "ship.glb",
                    Anchors = new List<ShipAnchor>()
                };
                using (var cache = ShipAuthoringStore.Open(path, writable: true))
                {
                    cache.Upsert(ship);
                    cache.FlushAsync().Wait();
                }
                Console.WriteLine($"Created draft {path}; fill its model bindings and schematic in Blender.");
                return 0;
            }
            Console.Error.WriteLine("ship-authoring create <ship.cc> <stable-id> <name> | inspect <ship.cc> | validate <ship.cc>");
            return 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }
}
