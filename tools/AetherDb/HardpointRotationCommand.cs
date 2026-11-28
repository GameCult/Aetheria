using System;
using System.IO;
using System.Linq;
using GameCult.Caching;

// Sets one hardpoint's rotation on one catalog hull: hardpoint-rotation "<hull>" <x>,<y> <None|CounterClockwise|Reversed|Clockwise> [apply].
// The hull is named exactly once in the catalog and the hardpoint is the one at that position, or nothing is touched. It prints
// the designs that Take the mount before and after (HardpointData.Takes, the one fit rule). Dry run unless apply; a rotation
// the mount already has writes nothing.
public static class HardpointRotationCommand
{
    public const string Usage = "hardpoint-rotation \"<hull>\" <x>,<y> <None|CounterClockwise|Reversed|Clockwise> [apply]";

    public static int Run(string[] args)
    {
        var apply = args.Contains("apply");
        var positional = args.Where(a => a != "apply").ToArray();
        using var db = AetherDb.OpenShipped(catalogWritable: apply);
        var code = Run(db.Cache, positional, apply, Console.Out);
        if (apply && code == 0) db.Cache.FlushAsync().GetAwaiter().GetResult();
        return code;
    }

    public static int Run(CultCache cache, string[] positional, bool apply, TextWriter output)
    {
        var coordinates = positional.Length == 3 ? positional[1].Split(',') : null;
        if (coordinates == null || coordinates.Length != 2 || !int.TryParse(coordinates[0], out var x) || !int.TryParse(coordinates[1], out var y)
            || !Enum.TryParse<ItemRotation>(positional[2], out var rotation) || !Enum.IsDefined(typeof(ItemRotation), rotation))
        {
            output.WriteLine(Usage);
            return 1;
        }
        var hulls = cache.GetAll<HullData>().Where(h => h.Name == positional[0]).ToArray();
        if (hulls.Length != 1)
        {
            output.WriteLine($"The catalog holds {hulls.Length} hulls named '{positional[0]}'; exactly one is required.");
            return 1;
        }
        var hull = hulls[0];
        var mounts = hull.Hardpoints.Where(h => h.Position.x == x && h.Position.y == y).ToArray();
        if (mounts.Length != 1)
        {
            output.WriteLine($"{hull.Name} has {mounts.Length} hardpoints at ({x},{y}); exactly one is required.");
            return 1;
        }
        var mount = mounts[0];
        var designs = cache.GetAll<EquippableItemData>().ToArray();
        string Takers() => string.Join(", ", designs.Where(mount.Takes).Select(d => d.Name).OrderBy(n => n, StringComparer.Ordinal));
        var before = mount.Rotation;
        output.WriteLine($"{hull.Name} ({x},{y}) {mount.Type} {mount.Shape.Width}x{mount.Shape.Height}: rotation {before} [{Takers()}]");
        if (before == rotation)
        {
            output.WriteLine("No change.");
            return 0;
        }
        mount.Rotation = rotation;
        output.WriteLine($"{hull.Name} ({x},{y}) {mount.Type} {mount.Shape.Width}x{mount.Shape.Height}: rotation {rotation} [{Takers()}]");
        if (!apply)
        {
            mount.Rotation = before;
            output.WriteLine("\nDry run. Pass \"apply\" to set it.");
            return 0;
        }
        cache.Upsert(hull);
        output.WriteLine($"\nSet {hull.Name}'s hardpoint ({x},{y}) rotation in Aetheria.cc");
        return 0;
    }
}
