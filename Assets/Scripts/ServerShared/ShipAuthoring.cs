using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GameCult.Caching;
using GameCult.Caching.MessagePack;
using MessagePack;

// A mod ship's authoring record. Hull is the existing simulation design, embedded here so a
// proof ship has one writable source; the shipped catalog contains no copy of that ship.
[CultDocument("aetheria.ship_authoring", "1"), MessagePackObject]
public sealed class ShipAuthoring
{
    [CultName, Key(0)] public string Id;
    [Key(1)] public HullData Hull;
    [Key(2)] public string ModelAsset;
    [Key(3)] public List<ShipAnchor> Anchors = new List<ShipAnchor>();
    [Key(4)] public List<ShipPolyline> SchematicLines = new List<ShipPolyline>();
}

[MessagePackObject]
public sealed class ShipAnchor
{
    // Stable semantic identity. HullData.Hardpoints[].Transform names this ID for mounts.
    [Key(0)] public string Id;
    // One of: map-icon, hull-collider, shield, tractor, thruster-emitter,
    // weapon-muzzle, radiator-mesh, or articulation.
    [Key(1)] public string Role;
    // Stable node ID in the compiled model, never a Blender display name.
    [Key(2)] public string ModelNodeId;
    // Only child roles use ParentId; e.g. a muzzle names its weapon hardpoint.
    [Key(3)] public string ParentId;
    [Key(4)] public int Order;
}

// Evaluated or baked Grease Pencil stroke coordinates in Blender's right-handed Z-up
// space. The runtime renderer owns its coordinate conversion and camera projection.
[MessagePackObject]
public sealed class ShipPolyline
{
    [Key(0)] public string Layer;
    [Key(1)] public string Material;
    [Key(2)] public float[] Points;
    [Key(3)] public float[] Radii;
    [Key(4)] public float[] Opacities;
    [Key(5)] public bool Cyclic;
    [Key(6)] public float[] Color;
}

public static class ShipAuthoringStore
{
    public static CultCache Open(string path, bool writable = false)
    {
        var cache = new CultCache();
        try
        {
            cache.AddBackingStore(new SingleFileMessagePackBackingStore(path, !writable), new[] { typeof(ShipAuthoring) });
            return cache;
        }
        catch
        {
            cache.Dispose();
            throw;
        }
    }

    public static ShipAuthoring Read(string path)
    {
        using var cache = Open(path);
        var records = cache.GetAll<ShipAuthoring>().ToArray();
        if (records.Length != 1)
            throw new InvalidOperationException($"{path} must hold exactly one ship authoring record, found {records.Length}.");
        Validate(records[0]);
        return records[0];
    }

    public static void Validate(ShipAuthoring ship)
    {
        if (ship == null) throw new InvalidOperationException("Ship authoring record is null.");
        if (string.IsNullOrWhiteSpace(ship.Id)) throw new InvalidOperationException("Ship ID is required.");
        if (!(ship.Id[0] >= 'a' && ship.Id[0] <= 'z' || ship.Id[0] >= '0' && ship.Id[0] <= '9') || ship.Id.Any(c =>
                !(c >= 'a' && c <= 'z' || c >= '0' && c <= '9' || c == '.' || c == '_' || c == '-')))
            throw new InvalidOperationException($"{ship.Id}: ship ID must use lower-case ASCII letters, digits, dots, underscores, or hyphens.");
        var hull = ship.Hull ?? throw new InvalidOperationException($"{ship.Id}: hull data is required.");
        if (string.IsNullOrWhiteSpace(hull.Name)) throw new InvalidOperationException($"{ship.Id}: hull name is required.");
        if (hull.Shape?.Cells == null || hull.Shape.Width < 1 || hull.Shape.Height < 1 ||
            !hull.Shape.Cells.Cast<bool>().Any(occupied => occupied))
            throw new InvalidOperationException($"{ship.Id}: schematic must contain at least one cell.");
        if (!string.IsNullOrEmpty(hull.Prefab))
            throw new InvalidOperationException($"{ship.Id}: a mod ship cannot name a Unity prefab.");
        if (string.IsNullOrWhiteSpace(ship.ModelAsset) || Path.IsPathRooted(ship.ModelAsset) ||
            ship.ModelAsset.Replace('\\', '/').Split('/').Any(part => part == ".."))
            throw new InvalidOperationException($"{ship.Id}: model asset must be a relative package path.");

        var anchors = ship.Anchors ?? throw new InvalidOperationException($"{ship.Id}: anchors are required.");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var nodeIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var anchor in anchors)
        {
            if (anchor == null || string.IsNullOrWhiteSpace(anchor.Id) || !ids.Add(anchor.Id))
                throw new InvalidOperationException($"{ship.Id}: anchor IDs must be present and unique.");
            if (string.IsNullOrWhiteSpace(anchor.ModelNodeId))
                throw new InvalidOperationException($"{ship.Id}: anchor {anchor.Id} needs a model node ID.");
            if (!nodeIds.Add(anchor.ModelNodeId))
                throw new InvalidOperationException($"{ship.Id}: model node {anchor.ModelNodeId} is claimed by more than one anchor.");
        }
        foreach (var role in new[] { "map-icon", "hull-collider", "shield", "tractor" })
            if (anchors.Count(anchor => anchor.Role == role) != 1)
                throw new InvalidOperationException($"{ship.Id}: exactly one {role} anchor is required.");

        var mounts = new HashSet<string>(StringComparer.Ordinal);
        var occupiedHardpointCells = new HashSet<(int x, int y)>();
        foreach (var hardpoint in hull.Hardpoints ?? new List<HardpointData>())
        {
            if (hardpoint == null || string.IsNullOrWhiteSpace(hardpoint.Transform) ||
                !mounts.Add(hardpoint.Transform))
                throw new InvalidOperationException($"{ship.Id}: hardpoint IDs must be present and unique.");
            if (hardpoint.Shape?.Cells == null || !hardpoint.Shape.Cells.Cast<bool>().Any(occupied => occupied))
                throw new InvalidOperationException($"{ship.Id}: hardpoint {hardpoint.Transform} has no cells.");
            for (var cellX = 0; cellX < hardpoint.Shape.Width; cellX++)
            for (var cellY = 0; cellY < hardpoint.Shape.Height; cellY++)
            {
                if (!hardpoint.Shape.Cells[cellX, cellY]) continue;
                var x = hardpoint.Position.x + cellX;
                var y = hardpoint.Position.y + cellY;
                if (x < 0 || y < 0 || x >= hull.Shape.Width || y >= hull.Shape.Height || !hull.Shape.Cells[x, y])
                    throw new InvalidOperationException($"{ship.Id}: hardpoint {hardpoint.Transform} lies outside the hull schematic.");
                if (!occupiedHardpointCells.Add((x, y)))
                    throw new InvalidOperationException($"{ship.Id}: hardpoint {hardpoint.Transform} overlaps another hardpoint.");
            }
            if (!anchors.Any(anchor => anchor.Id == hardpoint.Transform))
                throw new InvalidOperationException($"{ship.Id}: hardpoint {hardpoint.Transform} has no model anchor.");
        }
        foreach (var anchor in anchors.Where(anchor => !string.IsNullOrEmpty(anchor.ParentId)))
            if (!mounts.Contains(anchor.ParentId))
                throw new InvalidOperationException($"{ship.Id}: anchor {anchor.Id} has unknown hardpoint parent {anchor.ParentId}.");

        foreach (var line in ship.SchematicLines ?? new List<ShipPolyline>())
        {
            var points = line?.Points;
            if (points == null || points.Length < 6 || points.Length % 3 != 0 ||
                points.Any(value => float.IsNaN(value) || float.IsInfinity(value)))
                throw new InvalidOperationException($"{ship.Id}: schematic line needs finite XYZ points.");
            var count = points.Length / 3;
            if (line.Radii != null && line.Radii.Length != count ||
                line.Opacities != null && line.Opacities.Length != count)
                throw new InvalidOperationException($"{ship.Id}: schematic line point attributes have the wrong length.");
            if (line.Radii != null && line.Radii.Any(value => float.IsNaN(value) || float.IsInfinity(value) || value < 0) ||
                line.Opacities != null && line.Opacities.Any(value => float.IsNaN(value) || float.IsInfinity(value) || value < 0 || value > 1))
                throw new InvalidOperationException($"{ship.Id}: schematic line radius and opacity must be finite and in range.");
            if (line.Color != null && (line.Color.Length != 4 ||
                line.Color.Any(value => float.IsNaN(value) || float.IsInfinity(value))))
                throw new InvalidOperationException($"{ship.Id}: schematic line color must be finite RGBA.");
        }
    }
}
