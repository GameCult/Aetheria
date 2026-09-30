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
    public sealed class Package
    {
        public HullData Hull;
        public ShipAuthoring Visual;
        public string ModelPath;
        public Dictionary<string, uint> NodeIndices;
    }

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

        var packages = new List<Package>();
        foreach (var directory in Directory.GetDirectories(root).OrderBy(path => path, StringComparer.Ordinal))
        {
            var path = Path.Combine(directory, "ship.cc");
            if (!File.Exists(path)) continue;
            // ReadPackage pins each ID to its directory name, so IDs within one mods root are already unique.
            packages.Add(ReadPackage(path));
        }

        Directory.CreateDirectory(Path.GetDirectoryName(output));
        // The store keeps a lock file beside every file it opens, so the working copy gets its own directory and the
        // whole directory goes away, whatever the store left in it.
        var workspace = output + "." + Guid.NewGuid().ToString("N") + ".tmp";
        var temporary = Path.Combine(workspace, "catalog.cc");
        try
        {
            Directory.CreateDirectory(workspace);
            File.Copy(source, temporary);
            using (var cache = AetheriaStores.Open(temporary, catalogWritable: true))
            {
                foreach (var package in packages)
                {
                    var (hull, ship) = (package.Hull, package.Visual);
                    var hullKey = HullKey(ship.Id);
                    var authoringKey = AuthoringKey(ship.Id);
                    if (cache.AllStoredDocuments.Any(record => record.Key.Equals(hullKey) || record.Key.Equals(authoringKey)))
                        throw new InvalidOperationException($"{ship.Id}: a mod key collides with an existing catalog record.");
                    // CultCache indexes names per concrete type and lets the last writer win, and GetByName over a base
                    // type throws on a match across subtypes. A mod hull named like any shipped item, or another mod's
                    // hull (upserted earlier in this loop), would make name lookups wrong or throw.
                    if (cache.GetAll<ItemData>().Any(item => string.Equals(item.Name, hull.Name, StringComparison.Ordinal)))
                        throw new InvalidOperationException($"{ship.Id}: hull name '{hull.Name}' collides with an existing catalog item.");
                    if (cache.GetAll<ShipAuthoring>().Any(existing => string.Equals(existing.Id, ship.Id, StringComparison.Ordinal)))
                        throw new InvalidOperationException($"{ship.Id}: ship ID collides with an existing catalog ship authoring record.");
                    // Verbatim: the mod store's own two records, under the keys it already holds them at.
                    ShipAuthoringStore.Write(cache, hull, ship);
                }
                // Shipped hulls too: the derived catalog must not hold a hull that names two bodies.
                foreach (var hull in cache.GetAll<HullData>())
                    ShipAuthoringStore.RequireOneBody(hull, hull.Name);
                cache.FlushAsync().GetAwaiter().GetResult();
            }
            if (File.Exists(output)) File.Replace(temporary, output, null);
            else File.Move(temporary, output);
            return packages.Count;
        }
        finally
        {
            if (Directory.Exists(workspace)) Directory.Delete(workspace, true);
        }
    }

    public static Package ReadPackage(string shipPath)
    {
        var path = Path.GetFullPath(shipPath);
        var (hull, ship) = ShipAuthoringStore.Read(path);
        return Bind(hull, ship, Path.GetDirectoryName(path), path);
    }

    // The package a catalog hull names: its two records come from the catalog, and only the GLB comes from the mod
    // directory (<modsRoot>/<id>/<ModelAsset>). Gameplay reads a mod ship through this, never through its ship.cc.
    public static Package PackageOf(CultCache catalog, HullData hull, string modsRoot)
    {
        var ship = catalog.Get(hull.Visual) ??
            throw new InvalidOperationException($"{hull.Name}: the catalog holds no visual record {hull.Visual.Key.Value}.");
        ShipAuthoringStore.Validate(hull, ship);
        var directory = Path.Combine(Path.GetFullPath(modsRoot), ship.Id);
        return Bind(hull, ship, directory, Path.Combine(directory, "ship.cc"));
    }

    // The one game-side choice of catalog: the shipped file when the mods root holds no package, otherwise the derived
    // file, composed afresh from the shipped one and the packages. Recomposed every call, so the derived file is never
    // stale and never an authority.
    public static string ResolveCatalog(string shippedCatalog, string derivedCatalog, string modsRoot)
    {
        if (!Directory.Exists(modsRoot) ||
            !Directory.GetDirectories(modsRoot).Any(directory => File.Exists(Path.Combine(directory, "ship.cc"))))
            return shippedCatalog;
        Compose(shippedCatalog, derivedCatalog, modsRoot);
        return derivedCatalog;
    }

    private static Package Bind(HullData hull, ShipAuthoring ship, string directory, string path)
    {
        if (!string.Equals(Path.GetFileName(directory), ship.Id, StringComparison.Ordinal))
            throw new InvalidOperationException($"{path}: ship ID must match its package directory name.");
        var modelPath = Path.GetFullPath(Path.Combine(directory, ship.ModelAsset));
        // Containment is ShipAuthoringStore.Validate's job: it refuses rooted and `..` model paths before this line.
        if (!File.Exists(modelPath) || !string.Equals(Path.GetExtension(modelPath), ".glb", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"{ship.Id}: model asset must name an existing GLB inside its package.");
        var modelNodes = ReadNodeIds(modelPath);
        foreach (var anchor in ship.Anchors)
            if (!modelNodes.ContainsKey(anchor.ModelNodeId))
                throw new InvalidOperationException($"{ship.Id}: model has no node with aetheria.id={anchor.ModelNodeId} for anchor {anchor.Id}.");
        CultRecordRefs.Validate(hull);
        return new Package { Hull = hull, Visual = ship, ModelPath = modelPath, NodeIndices = modelNodes };
    }

    // GLB is the xenos asset boundary. Only its node extras are read here; geometry belongs to the runtime importer.
    public static Dictionary<string, uint> ReadNodeIds(string path)
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
        var ids = new Dictionary<string, uint>(StringComparer.Ordinal);
        var nodes = json["nodes"]?.OfType<JObject>() ?? Enumerable.Empty<JObject>();
        uint index = 0;
        foreach (var node in nodes)
        {
            var id = (string)node["extras"]?["aetheria.id"];
            if (string.IsNullOrEmpty(id)) { index++; continue; }
            if (!ids.TryAdd(id, index))
                throw new InvalidOperationException($"{path}: duplicate GLB node aetheria.id={id}.");
            index++;
        }
        return ids;
    }
}
