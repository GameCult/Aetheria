using GameCult.Caching;
using CultMath;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

// `ship-authoring starter`: a complete, flyable mod package for authors with no Blender at hand. It writes the package
// (<mods>/<id>/ship.cc and ship.glb) with the stats of a shipped hull, a small hull grid, a working rig (two thrusters,
// a radiator, two energy guns, the internal modules) and a boxed model that carries every anchor the rig needs. The
// model is a stand-in an author replaces through Blender and Package; the records are judged by the same validator and
// reader the game boots with, so a starter that is written is a starter the game loads.
public static class ShipStarterCommand
{
    // Hull grid, x across, y from the stern (0) to the bow. The mount rows below say what sits in each cell.
    private const int Width = 4, Height = 6;

    private sealed class Part
    {
        public string Id, Role, Parent;
        public HardpointType? Mount;
        public int2 Cell, Size = new int2(1, 1);
        public float3 Position;
        // Box half-extents; null for an empty node. Scale is the node scale, used by the shield envelope.
        public float3? Half;
        public float3? Scale;
    }

    public static string Write(string modsRoot, string id, string name, HullData like)
    {
        ShipAuthoringStore.RequireShipId(id);
        var directory = Path.Combine(Path.GetFullPath(modsRoot), id);
        var path = Path.Combine(directory, "ship.cc");
        if (File.Exists(path) || File.Exists(Path.Combine(directory, "ship.glb")))
            throw new InvalidOperationException($"Refusing to replace the package in {directory}");

        // Metres, nose toward -Y, up +Z, as the Blender add-on's kit frame has it; the GLB writer converts to glTF's Y-up.
        var parts = new[]
        {
            new Part { Id = "map", Role = "map-icon", Half = new float3(3f, 5f, .05f) },
            new Part { Id = "collider", Role = "hull-collider", Half = new float3(3f, 5f, 1f) },
            new Part { Id = "shield", Role = "shield", Scale = new float3(4f, 6f, 2.5f) },
            new Part { Id = "tractor", Role = "tractor", Position = new float3(0f, -5f, 0f) },
            new Part { Id = "thruster.port", Role = "thruster-emitter", Mount = HardpointType.Thruster, Cell = new int2(0, 0), Size = new int2(2, 1), Position = new float3(2.2f, 5.4f, 0f), Half = new float3(.4f, .6f, .4f) },
            new Part { Id = "thruster.starboard", Role = "thruster-emitter", Mount = HardpointType.Thruster, Cell = new int2(2, 0), Size = new int2(2, 1), Position = new float3(-2.2f, 5.4f, 0f), Half = new float3(.4f, .6f, .4f) },
            new Part { Id = "radiator", Role = "radiator-mesh", Mount = HardpointType.Radiator, Cell = new int2(1, 1), Size = new int2(2, 1), Position = new float3(0f, 2f, 1.1f), Half = new float3(1.5f, .8f, .08f) },
            new Part { Id = "gun.port", Role = "weapon-mount", Mount = HardpointType.Energy, Cell = new int2(0, 5), Position = new float3(1.8f, -3f, 1.1f) },
            new Part { Id = "gun.port.muzzle", Role = "weapon-muzzle", Parent = "gun.port", Position = new float3(1.8f, -4f, 1.1f) },
            new Part { Id = "gun.starboard", Role = "weapon-mount", Mount = HardpointType.Energy, Cell = new int2(3, 5), Position = new float3(-1.8f, -3f, 1.1f) },
            new Part { Id = "gun.starboard.muzzle", Role = "weapon-muzzle", Parent = "gun.starboard", Position = new float3(-1.8f, -4f, 1.1f) },
        };
        // Internal modules show on no model, so they have a cell and no anchor.
        var internals = new[]
        {
            ("reactor", HardpointType.Reactor, new int2(1, 2), new int2(2, 2)), ("shield", HardpointType.Shield, new int2(3, 4), new int2(1, 1)),
            ("control", HardpointType.ControlModule, new int2(1, 4), new int2(2, 2)), ("sensors", HardpointType.Sensors, new int2(0, 4), new int2(1, 1)),
        };

        var ship = new ShipAuthoring
        {
            Id = id,
            ModelAsset = "ship.glb",
            Anchors = parts.Select(part => new ShipAnchor { Id = part.Id, Role = part.Role, ModelNodeId = part.Id, ParentId = part.Parent }).ToList()
        };
        var hull = ShipAuthoringStore.HullLike(like, name, id);
        hull.Shape = new Shape(Width, Height);
        for (var x = 0; x < Width; x++)
        for (var y = 0; y < Height; y++)
            hull.Shape.Cells[x, y] = true;
        hull.Hardpoints = parts.Where(part => part.Mount != null)
            .Select(part => (part.Id, part.Mount.Value, part.Cell, part.Size))
            .Concat(internals.Select(row => (Id: "internal." + row.Item1, Mount: row.Item2, Cell: row.Item3, Size: row.Item4)))
            .Select(row => new HardpointData
            {
                Type = row.Item2, Position = row.Item3, Shape = Block(row.Item4), Transform = row.Item1,
                // The grid's stern is y = 0 and its bow the far row: a drive there pushes the ship toward the bow, which the
                // authoring grid spells Reversed (as Djinni's stern drives are).
                Rotation = row.Item2 == HardpointType.Thruster ? ItemRotation.Reversed : ItemRotation.None
            }).ToList();
        ShipAuthoringStore.Validate(hull, ship);

        Directory.CreateDirectory(directory);
        File.WriteAllBytes(Path.Combine(directory, ship.ModelAsset), Glb(parts));
        using (var cache = ShipAuthoringStore.Open(path, writable: true))
        {
            ShipAuthoringStore.Write(cache, hull, ship);
            cache.FlushAsync().Wait();
        }
        // The reader the game boots with, over the written package: the GLB's node ids and mesh roles are judged too.
        ShipModCatalog.ReadPackage(path);
        return path;
    }

    private static Shape Block(int2 size)
    {
        var shape = new Shape(size.x, size.y);
        foreach (var cell in shape.AllCoordinates) shape[cell] = true;
        return shape;
    }

    // One GLB holding every part as a root-level node, each box with its own vertices (a convex collider cooks from it
    // as it is), and each muzzle a child of its mount. Node ids travel in extras.aetheria.id, as Package writes them.
    private static byte[] Glb(Part[] parts)
    {
        var bin = new List<byte>();
        var views = new List<string>();
        var accessors = new List<string>();
        var meshes = new List<string>();
        var meshOf = new Dictionary<Part, int>();

        int View(byte[] bytes, int target)
        {
            while (bin.Count % 4 != 0) bin.Add(0);
            views.Add($@"{{""buffer"":0,""byteOffset"":{bin.Count},""byteLength"":{bytes.Length},""target"":{target}}}");
            bin.AddRange(bytes);
            return views.Count - 1;
        }
        string F(float value) => value.ToString("R", CultureInfo.InvariantCulture);

        foreach (var part in parts.Where(part => part.Half != null))
        {
            var (hx, hy, hz) = (part.Half.Value.x, part.Half.Value.y, part.Half.Value.z);
            var positions = new List<float>();
            var normals = new List<float>();
            var indices = new List<ushort>();
            // Six faces, outward normals, in the Blender frame; Gltf() turns each into glTF's.
            foreach (var (axis, sign) in new[] { (0, 1), (0, -1), (1, 1), (1, -1), (2, 1), (2, -1) })
            {
                var u = (axis + 1) % 3;
                var v = (axis + 2) % 3;
                var half = new[] { hx, hy, hz };
                var first = (ushort)(positions.Count / 3);
                foreach (var (a, b) in new[] { (-1, -1), (1, -1), (1, 1), (-1, 1) })
                {
                    var corner = new float[3];
                    corner[axis] = sign * half[axis];
                    corner[u] = a * half[u];
                    corner[v] = b * half[v];
                    positions.AddRange(Gltf(corner[0], corner[1], corner[2]));
                    var normal = new float[3];
                    normal[axis] = sign;
                    normals.AddRange(Gltf(normal[0], normal[1], normal[2]));
                }
                // The (axis, u, v) frame is right-handed, so the corner order above winds counter-clockwise seen from +axis.
                var order = sign > 0 ? new[] { 0, 1, 2, 0, 2, 3 } : new[] { 0, 2, 1, 0, 3, 2 };
                indices.AddRange(order.Select(i => (ushort)(first + i)));
            }
            var positionView = View(positions.SelectMany(BitConverter.GetBytes).ToArray(), 34962);
            var normalView = View(normals.SelectMany(BitConverter.GetBytes).ToArray(), 34962);
            var indexView = View(indices.SelectMany(BitConverter.GetBytes).ToArray(), 34963);
            var low = Gltf(-hx, -hy, -hz);
            var high = Gltf(hx, hy, hz);
            var box = new[] { Math.Min(low[0], high[0]), Math.Min(low[1], high[1]), Math.Min(low[2], high[2]), Math.Max(low[0], high[0]), Math.Max(low[1], high[1]), Math.Max(low[2], high[2]) };
            accessors.Add($@"{{""bufferView"":{positionView},""componentType"":5126,""count"":{positions.Count / 3},""type"":""VEC3"",""min"":[{F(box[0])},{F(box[1])},{F(box[2])}],""max"":[{F(box[3])},{F(box[4])},{F(box[5])}]}}");
            accessors.Add($@"{{""bufferView"":{normalView},""componentType"":5126,""count"":{normals.Count / 3},""type"":""VEC3""}}");
            accessors.Add($@"{{""bufferView"":{indexView},""componentType"":5123,""count"":{indices.Count},""type"":""SCALAR""}}");
            meshOf[part] = meshes.Count;
            meshes.Add($@"{{""primitives"":[{{""attributes"":{{""POSITION"":{accessors.Count - 3},""NORMAL"":{accessors.Count - 2}}},""indices"":{accessors.Count - 1}}}]}}");
        }

        // A muzzle is a child of its mount, so its translation is relative to the mount's.
        var byId = parts.ToDictionary(part => part.Id, StringComparer.Ordinal);
        var nodeIndex = parts.Select((part, index) => (part.Id, index)).ToDictionary(pair => pair.Id, pair => pair.index, StringComparer.Ordinal);
        string Node(Part part)
        {
            var translation = part.Parent == null ? part.Position : part.Position - byId[part.Parent].Position;
            var g = Gltf(translation.x, translation.y, translation.z);
            var json = new StringBuilder($@"{{""name"":""{part.Id}""");
            if (meshOf.TryGetValue(part, out var mesh)) json.Append($@",""mesh"":{mesh}");
            if (part.Scale != null) { var s = Gltf(part.Scale.Value.x, part.Scale.Value.y, part.Scale.Value.z); json.Append($@",""scale"":[{F(Math.Abs(s[0]))},{F(Math.Abs(s[1]))},{F(Math.Abs(s[2]))}]"); }
            json.Append($@",""translation"":[{F(g[0])},{F(g[1])},{F(g[2])}]");
            var children = parts.Where(child => child.Parent == part.Id).Select(child => nodeIndex[child.Id]).ToArray();
            if (children.Length > 0) json.Append($@",""children"":[{string.Join(",", children)}]");
            json.Append($@",""extras"":{{""aetheria.id"":""{part.Id}""}}}}");
            return json.ToString();
        }
        var roots = parts.Where(part => part.Parent == null).Select(part => nodeIndex[part.Id]);
        var document = $@"{{""asset"":{{""version"":""2.0"",""generator"":""AetherDb ship-authoring starter""}},""scene"":0,""scenes"":[{{""nodes"":[{string.Join(",", roots)}]}}]," +
            $@"""nodes"":[{string.Join(",", parts.Select(Node))}],""meshes"":[{string.Join(",", meshes)}],""accessors"":[{string.Join(",", accessors)}]," +
            $@"""bufferViews"":[{string.Join(",", views)}],""buffers"":[{{""byteLength"":{bin.Count + (4 - bin.Count % 4) % 4}}}]}}";
        return Container(document, bin.ToArray());
    }

    // Blender (x right, y back, z up) to glTF (x right, y up, z toward the viewer): the exporter's +Y-up conversion.
    private static float[] Gltf(float x, float y, float z) => new[] { x, z, -y };

    private static byte[] Container(string json, byte[] bin)
    {
        byte[] Pad(byte[] bytes, byte fill)
        {
            var padded = new byte[(bytes.Length + 3) / 4 * 4];
            Array.Fill(padded, fill);
            bytes.CopyTo(padded, 0);
            return padded;
        }
        var body = Pad(Encoding.UTF8.GetBytes(json), (byte)' ');
        var data = Pad(bin, 0);
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write(0x46546C67u);
        writer.Write(2u);
        writer.Write((uint)(28 + body.Length + data.Length));
        writer.Write((uint)body.Length);
        writer.Write(0x4E4F534Au);
        writer.Write(body);
        writer.Write((uint)data.Length);
        writer.Write(0x004E4942u);
        writer.Write(data);
        return stream.ToArray();
    }
}
