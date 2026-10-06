using System;
using System.IO;
using System.Linq;
using System.Text;
using CultMath;

// Writes the mod package the tests, the Python round-trip check and the Unity smokes (ShipModPreview.Smoke and
// ShipModPlaySmoke) share. It is generated from ShipAuthoringTests.Fixture() so no opaque bytes are committed.
internal static class ShipFixture
{
    public static readonly string[] Nodes = { "map", "collider", "shield", "tractor", "thruster-port", "gun", "gun-muzzle" };

    // The nodes that carry the fixture mesh: the anchors the assembler reads a renderer or a collider mesh from.
    public static readonly string[] MeshNodes = { "map", "collider", "thruster-port" };

    // One tetrahedron, non-indexed (12 vertices), so a convex MeshCollider can cook it. Written by the generator.
    private static readonly float[] Tetrahedron =
    {
        0, 0, 0, 0, 1, 0, 1, 0, 0,
        0, 0, 0, 1, 0, 0, 0, 0, 1,
        0, 0, 0, 0, 0, 1, 0, 1, 0,
        1, 0, 0, 0, 1, 0, 0, 0, 1
    };

    public static byte[] MeshBytes() => Tetrahedron.SelectMany(BitConverter.GetBytes).ToArray();

    // The GLB carries a glTF scene naming every node: glTFast instantiates a scene, and a scene-less file has none.
    // Nodes in meshNodes (default MeshNodes) get the shared tetrahedron mesh, whose vertices live in the BIN chunk (MeshBytes).
    public static string GlbJson(string[] nodes, string[] meshNodes = null)
    {
        meshNodes ??= MeshNodes;
        var length = MeshBytes().Length;
        var mesh = nodes.Any(meshNodes.Contains)
            ? @",""meshes"":[{""primitives"":[{""attributes"":{""POSITION"":0}}]}]," +
              @"""accessors"":[{""bufferView"":0,""componentType"":5126,""count"":12,""type"":""VEC3"",""min"":[0,0,0],""max"":[1,1,1]}]," +
              $@"""bufferViews"":[{{""buffer"":0,""byteLength"":{length}}}],""buffers"":[{{""byteLength"":{length}}}]"
            : "";
        return @"{""asset"":{""version"":""2.0""},""scene"":0,""scenes"":[{""nodes"":[" + string.Join(",", nodes.Select((_, index) => index)) + @"]}]" + mesh + @",""nodes"":[" +
            string.Join(",", nodes.Select(node =>
                $@"{{""name"":""{node}""{(meshNodes.Contains(node) ? @",""mesh"":0" : "")},""extras"":{{""aetheria.id"":""{node}""}}}}")) + "]}";
    }

    public static byte[] Glb(string json, byte[] bin = null)
    {
        var body = Encoding.UTF8.GetBytes(json);
        var padded = new byte[(body.Length + 3) / 4 * 4];
        Array.Fill(padded, (byte)' ');
        body.CopyTo(padded, 0);
        bin ??= Array.Empty<byte>();
        var binPadded = new byte[(bin.Length + 3) / 4 * 4];
        bin.CopyTo(binPadded, 0);
        var bytes = new byte[28 + padded.Length + binPadded.Length];
        BitConverter.GetBytes(0x46546C67u).CopyTo(bytes, 0);
        BitConverter.GetBytes(2u).CopyTo(bytes, 4);
        BitConverter.GetBytes((uint)bytes.Length).CopyTo(bytes, 8);
        BitConverter.GetBytes((uint)padded.Length).CopyTo(bytes, 12);
        BitConverter.GetBytes(0x4E4F534Au).CopyTo(bytes, 16);
        padded.CopyTo(bytes, 20);
        BitConverter.GetBytes((uint)binPadded.Length).CopyTo(bytes, 20 + padded.Length);
        BitConverter.GetBytes(0x004E4942u).CopyTo(bytes, 24 + padded.Length);
        binPadded.CopyTo(bytes, 28 + padded.Length);
        return bytes;
    }

    // <modsRoot>/<id>/ship.cc plus the GLB at the record's ModelAsset path.
    public static string WritePackage(string modsRoot, string id, string hullName = "Skiff", string[] nodes = null,
        string modelAsset = "skiff.glb", Action<ShipParts> tweak = null, string[] meshNodes = null)
    {
        var ship = ShipAuthoringTests.Fixture();
        // A playable rig: the fixture thruster plus one energy weapon mount on the hull's other cell, with its muzzle.
        ship.Hull.Hardpoints.Add(new HardpointData { Type = HardpointType.Energy, Position = new int2(0, 0), Shape = new Shape(), Transform = "gun" });
        ship.Visual.Anchors.Add(new ShipAnchor { Id = "gun", Role = "weapon-mount", ModelNodeId = "gun" });
        ship.Visual.Anchors.Add(new ShipAnchor { Id = "gun.muzzle", Role = "weapon-muzzle", ModelNodeId = "gun-muzzle", ParentId = "gun" });
        tweak?.Invoke(ship);
        ship.WithId(id);
        ship.Hull.Name = hullName;
        ship.Visual.ModelAsset = modelAsset;
        var directory = Path.Combine(modsRoot, id);
        Directory.CreateDirectory(directory);
        using (var cache = ShipAuthoringStore.Open(Path.Combine(directory, "ship.cc"), writable: true))
        {
            ShipAuthoringStore.Write(cache, ship.Hull, ship.Visual);
            cache.FlushAsync().Wait();
        }
        File.WriteAllBytes(Path.Combine(directory, modelAsset), Glb(GlbJson(nodes ?? Nodes, meshNodes), MeshBytes()));
        return directory;
    }
}
