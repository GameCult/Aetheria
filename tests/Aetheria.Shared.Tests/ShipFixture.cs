using System;
using System.IO;
using System.Linq;
using System.Text;

// Writes the mod package the tests, the Python round-trip check and the Unity smoke (ShipModPreview.Smoke) share. It is
// generated from ShipAuthoringTests.Fixture() so no opaque bytes are committed.
internal static class ShipFixture
{
    public static readonly string[] Nodes = { "map", "collider", "shield", "tractor", "thruster-port" };

    // The GLB carries a glTF scene naming every node: glTFast instantiates a scene, and a scene-less file has none.
    public static string GlbJson(string[] nodes) =>
        @"{""asset"":{""version"":""2.0""},""scene"":0,""scenes"":[{""nodes"":[" + string.Join(",", nodes.Select((_, index) => index)) + @"]}],""nodes"":[" +
        string.Join(",", nodes.Select(node => $@"{{""name"":""{node}"",""extras"":{{""aetheria.id"":""{node}""}}}}")) + "]}";

    public static byte[] Glb(string json)
    {
        var body = Encoding.UTF8.GetBytes(json);
        var padded = new byte[(body.Length + 3) / 4 * 4];
        Array.Fill(padded, (byte)' ');
        body.CopyTo(padded, 0);
        var bytes = new byte[20 + padded.Length + 8];
        BitConverter.GetBytes(0x46546C67u).CopyTo(bytes, 0);
        BitConverter.GetBytes(2u).CopyTo(bytes, 4);
        BitConverter.GetBytes((uint)bytes.Length).CopyTo(bytes, 8);
        BitConverter.GetBytes((uint)padded.Length).CopyTo(bytes, 12);
        BitConverter.GetBytes(0x4E4F534Au).CopyTo(bytes, 16);
        padded.CopyTo(bytes, 20);
        BitConverter.GetBytes(0u).CopyTo(bytes, 20 + padded.Length);
        BitConverter.GetBytes(0x004E4942u).CopyTo(bytes, 24 + padded.Length);
        return bytes;
    }

    // <modsRoot>/<id>/ship.cc plus the GLB at the record's ModelAsset path.
    public static string WritePackage(string modsRoot, string id, string hullName = "Skiff", string[] nodes = null,
        string modelAsset = "skiff.glb", Action<ShipParts> tweak = null)
    {
        var ship = ShipAuthoringTests.Fixture();
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
        File.WriteAllBytes(Path.Combine(directory, modelAsset), Glb(GlbJson(nodes ?? Nodes)));
        return directory;
    }
}
