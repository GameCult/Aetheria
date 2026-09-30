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

    // A key the mods own. The shipped catalog and its authored records never reference one: a mod may be uninstalled.
    public static bool IsModKey(CultRecordKey key) =>
        key.IsSet() && (key.Value.StartsWith("mod-hull:", StringComparison.Ordinal) || key.Value.StartsWith("mod-ship:", StringComparison.Ordinal));

    // A package left out of the composition, named by its directory, with every reason it was left out.
    public sealed class Exclusion
    {
        public string Package;
        public string Reason;
        public override string ToString() => $"{Package}: {Reason}";
    }

    public sealed class Composition
    {
        // Ship IDs of the packages composed into the derived catalog, in directory order.
        public string[] Included;
        // Packages that failed on their own or collided with the catalog or with each other, in directory order.
        public Exclusion[] Excluded;
    }

    // One bad package never stops the rest. Each package is read and validated on its own; one that fails is excluded and
    // named. A collision (with the shipped catalog, or with another package's hull name) excludes every package involved,
    // since neither has a claim to the name. The derived catalog holds the shipped catalog plus the included packages.
    // Unsafe inputs and outputs (a missing catalog, a derived file that would replace a source) still throw: nothing is composed.
    public static Composition Compose(string shippedCatalog, string outputCatalog, string modsRoot)
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

        var reasons = new SortedDictionary<string, List<string>>(StringComparer.Ordinal);
        void Exclude(string package, string reason)
        {
            if (!reasons.TryGetValue(package, out var list)) reasons[package] = list = new List<string>();
            list.Add(reason);
        }
        var packages = new List<(string Directory, Package Package)>();
        foreach (var directory in Directory.GetDirectories(root).OrderBy(path => path, StringComparer.Ordinal))
        {
            var path = Path.Combine(directory, "ship.cc");
            if (!File.Exists(path)) continue;
            var name = Path.GetFileName(directory);
            // A package can fail in any way a hostile or broken file can, so its failure is quarantined whatever it is.
            try { packages.Add((name, ReadPackage(path))); }
            catch (Exception error) { Exclude(name, error.Message); }
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
            Composition composition;
            using (var cache = AetheriaStores.Open(temporary, catalogWritable: true))
            {
                // Collisions are judged against the shipped records alone, before any package is written, so the
                // verdict does not depend on directory order. CultCache indexes names per concrete type and lets the
                // last writer win, and GetByName over a base type throws on a match across subtypes: a mod hull named
                // like any shipped item, or like another mod's hull, would make name lookups wrong or throw.
                var keys = new HashSet<CultRecordKey>(cache.AllStoredDocuments.Select(record => record.Key));
                var itemNames = new HashSet<string>(cache.GetAll<ItemData>().Select(item => item.Name), StringComparer.Ordinal);
                var shipIds = new HashSet<string>(cache.GetAll<ShipAuthoring>().Select(existing => existing.Id), StringComparer.Ordinal);
                foreach (var (directory, package) in packages)
                {
                    var (hull, ship) = (package.Hull, package.Visual);
                    if (keys.Contains(HullKey(ship.Id)) || keys.Contains(AuthoringKey(ship.Id)))
                        Exclude(directory, $"a mod key collides with an existing catalog record.");
                    if (itemNames.Contains(hull.Name))
                        Exclude(directory, $"hull name '{hull.Name}' collides with an existing catalog item.");
                    if (shipIds.Contains(ship.Id))
                        Exclude(directory, $"ship ID collides with an existing catalog ship authoring record.");
                }
                foreach (var claim in packages.GroupBy(package => package.Package.Hull.Name, StringComparer.Ordinal).Where(group => group.Count() > 1))
                    foreach (var (directory, package) in claim)
                        Exclude(directory, $"hull name '{claim.Key}' is also claimed by " +
                            string.Join(", ", claim.Where(other => other.Directory != directory).Select(other => other.Directory)) + ".");

                var included = packages.Where(package => !reasons.ContainsKey(package.Directory)).Select(package => package.Package).ToArray();
                foreach (var package in included)
                    // Verbatim: the mod store's own two records, under the keys it already holds them at.
                    ShipAuthoringStore.Write(cache, package.Hull, package.Visual);
                // Shipped hulls too: the derived catalog must not hold a hull that names two bodies.
                foreach (var hull in cache.GetAll<HullData>())
                    ShipAuthoringStore.RequireOneBody(hull, hull.Name);
                cache.FlushAsync().GetAwaiter().GetResult();
                composition = new Composition
                {
                    Included = included.Select(package => package.Visual.Id).ToArray(),
                    Excluded = reasons.Select(entry => new Exclusion { Package = entry.Key, Reason = string.Join("; ", entry.Value) }).ToArray()
                };
            }
            if (File.Exists(output)) File.Replace(temporary, output, null);
            else File.Move(temporary, output);
            return composition;
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

    // The one game-side choice of catalog: the shipped file when no package is installed or none survives composition,
    // otherwise the derived file, composed afresh from the shipped one and the surviving packages. Recomposed every
    // call, so the derived file is never stale and never an authority. Excluded packages come back named, for the menu.
    public static (string Catalog, Exclusion[] Excluded) ResolveCatalog(string shippedCatalog, string derivedCatalog, string modsRoot)
    {
        if (!Directory.Exists(modsRoot) ||
            !Directory.GetDirectories(modsRoot).Any(directory => File.Exists(Path.Combine(directory, "ship.cc"))))
            return (shippedCatalog, Array.Empty<Exclusion>());
        var composition = Compose(shippedCatalog, derivedCatalog, modsRoot);
        return (composition.Included.Length == 0 ? shippedCatalog : derivedCatalog, composition.Excluded);
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
