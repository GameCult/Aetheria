using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GameCult.Caching;
using Newtonsoft.Json.Linq;
using System.Text;

// Builds the one catalog snapshot CultCache needs. Sources remain the shipped catalog and each
// mod-owned ship.cc; this output is disposable and never becomes an authoring surface.
public static class ShipModCatalog
{
    public static CultRecordKey HullKey(string id) => new CultRecordKey("mod-hull:" + id);
    public static CultRecordKey AuthoringKey(string id) => new CultRecordKey("mod-ship:" + id);

    public static int Compose(string shippedCatalog, string outputCatalog, string modsRoot)
    {
        var source = Path.GetFullPath(shippedCatalog);
        var output = Path.GetFullPath(outputCatalog);
        var root = Path.GetFullPath(modsRoot);
        if (!File.Exists(source)) throw new FileNotFoundException("Shipped catalog is missing", source);
        if (!Directory.Exists(root)) throw new DirectoryNotFoundException($"Mod root is missing: {root}");
        if (string.Equals(source, output, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The derived catalog cannot replace the shipped catalog.");
        if (output.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The derived catalog cannot replace a mod source or asset.");

        var ships = new List<ShipAuthoring>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var directory in Directory.GetDirectories(root).OrderBy(path => path, StringComparer.Ordinal))
        {
            var path = Path.Combine(directory, "ship.cc");
            if (!File.Exists(path)) continue;
            var ship = ShipAuthoringStore.Read(path);
            if (!string.Equals(Path.GetFileName(directory), ship.Id, StringComparison.Ordinal) ||
                !ids.Add(ship.Id))
                throw new InvalidOperationException($"{path}: ship ID must uniquely match its package directory name.");
            var modelPath = Path.GetFullPath(Path.Combine(directory, ship.ModelAsset));
            if (!modelPath.StartsWith(directory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                !File.Exists(modelPath) || !string.Equals(Path.GetExtension(modelPath), ".glb", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"{ship.Id}: model asset must name an existing GLB inside its package.");
            var modelNodes = ReadNodeIds(modelPath);
            foreach (var anchor in ship.Anchors)
                if (!modelNodes.Contains(anchor.ModelNodeId))
                    throw new InvalidOperationException($"{ship.Id}: model has no node with aetheria.id={anchor.ModelNodeId} for anchor {anchor.Id}.");
            CultRecordRefs.Validate(ship.Hull);
            ships.Add(ship);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(output));
        var temporary = output + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.Copy(source, temporary);
            using (var cache = AetheriaStores.Open(temporary, catalogWritable: true))
            {
                foreach (var ship in ships)
                {
                    var hullKey = HullKey(ship.Id);
                    var authoringKey = AuthoringKey(ship.Id);
                    if (cache.AllStoredDocuments.Any(record => record.Key.Equals(hullKey) || record.Key.Equals(authoringKey)))
                        throw new InvalidOperationException($"{ship.Id}: a mod key collides with an existing catalog record.");
                    cache.UpsertAsync(typeof(HullData), ship.Hull, hullKey).GetAwaiter().GetResult();
                    cache.UpsertAsync(typeof(ShipAuthoring), ship, authoringKey).GetAwaiter().GetResult();
                }
                cache.FlushAsync().GetAwaiter().GetResult();
            }
            if (File.Exists(output)) File.Replace(temporary, output, null);
            else File.Move(temporary, output);
            return ships.Count;
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    // GLB is the xenos asset boundary. Only its node extras are read here; geometry belongs to the runtime importer.
    public static HashSet<string> ReadNodeIds(string path)
    {
        using var stream = File.OpenRead(path);
        using var reader = new BinaryReader(stream);
        if (stream.Length < 20 || reader.ReadUInt32() != 0x46546C67 || reader.ReadUInt32() != 2 ||
            reader.ReadUInt32() != stream.Length)
            throw new InvalidOperationException($"{path}: invalid GLB 2 header.");
        var chunkLength = reader.ReadUInt32();
        if (reader.ReadUInt32() != 0x4E4F534A || chunkLength > stream.Length - stream.Position)
            throw new InvalidOperationException($"{path}: missing GLB JSON chunk.");
        var json = JObject.Parse(Encoding.UTF8.GetString(reader.ReadBytes((int)chunkLength)));
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in json["nodes"]?.OfType<JObject>() ?? Enumerable.Empty<JObject>())
        {
            var id = (string)node["extras"]?["aetheria.id"];
            if (string.IsNullOrEmpty(id)) continue;
            if (!ids.Add(id))
                throw new InvalidOperationException($"{path}: duplicate GLB node aetheria.id={id}.");
        }
        return ids;
    }
}
