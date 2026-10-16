using System;
using System.IO;
using System.Linq;
using GameCult.Caching;
using UnityEditor;
using UnityEngine;

// Batch proof that a mod ship survives the game's own path into a zone, without a scene: the derived catalog composed
// with one package, ShipModShips.Preload over it, then ShipModShips.Instantiate, which is what ZoneRenderer.LoadEntity's
// Body() calls for a hull with a Visual. A plain parent transform stands in for ZoneRoot; nothing else in LoadEntity
// touches the body before it takes its ShipInstance.
//
// The package is installed beside two siblings. The .broken one has its map-icon anchor moved onto a meshless node:
// Compose excludes it, naming the anchor. The .unimportable one has its GLB's BIN chunk dropped: the headless reader
// accepts it, so it composes, and the importer cannot build it, so Preload faults Loading with ImportFault naming it.
// RemovePackage then moves it aside and the next Compose leaves it out.
//
//   Unity -batchmode -nographics -projectPath <project> -executeMethod ShipModPlaySmoke.Run
//         -shipModPath <mods>/<id>/ship.cc [-shippedCatalog GameData/Aetheria.cc] -logFile <log>
//
// Do not pass -quit: the method is async and exits the Editor itself, 0 on success and 1 on any failure.
public static class ShipModPlaySmoke
{
    public static async void Run()
    {
        var args = Environment.GetCommandLineArgs();
        string Option(string name, string fallback = null)
        {
            var at = Array.IndexOf(args, name);
            return at >= 0 && at + 1 < args.Length ? args[at + 1] : fallback;
        }
        var packagePath = Option("-shipModPath");
        var work = Path.Combine(Path.GetTempPath(), $"ship-mod-play-smoke-{Guid.NewGuid():N}");
        var derived = Path.Combine(work, "Aetheria.modded.cc");
        var modsRoot = Path.Combine(work, "Mods");
        GameObject zone = null;
        try
        {
            if (packagePath == null) throw new InvalidOperationException("-shipModPath <package/ship.cc> is required");
            var package = ShipModCatalog.ReadPackage(packagePath);
            Install(package.Visual.Id, package, modsRoot, Path.GetFullPath(packagePath));
            var broken = InstallSibling(package, modsRoot, ".broken", ship =>
            {
                var mapIcon = ship.Anchors.Single(anchor => anchor.Role == "map-icon");
                var muzzle = ship.Anchors.First(anchor => anchor.Role == "weapon-muzzle");
                (mapIcon.ModelNodeId, muzzle.ModelNodeId) = (muzzle.ModelNodeId, mapIcon.ModelNodeId);
            }, null);
            var shipped = Path.GetFullPath(Option("-shippedCatalog", "GameData/Aetheria.cc"));
            var composition = ShipModCatalog.Compose(shipped, derived, modsRoot);
            if (!composition.Included.Contains(package.Visual.Id))
                throw new InvalidOperationException($"{package.Visual.Id} was not composed into the catalog: " + Reasons(composition));
            var excluded = composition.Excluded.SingleOrDefault(entry => entry.Package == broken.Visual.Id);
            if (composition.Included.Contains(broken.Visual.Id) || excluded == null || !excluded.Reason.Contains("map-icon anchor"))
                throw new InvalidOperationException($"{broken.Visual.Id} was not excluded for its map-icon anchor: " + Reasons(composition));

            using (var catalog = AetheriaStores.Open(derived))
            {
                await ShipModShips.Preload(catalog, modsRoot);
                HullData Hull(string name) => catalog.GetAll<HullData>().Single(candidate => candidate.Visual.IsSet() && candidate.Name == name);
                var hull = Hull(package.Hull.Name);

                zone = new GameObject("Play Smoke Zone");
                var ship = ShipModShips.Instantiate(hull, zone.transform).GetComponent<ShipInstance>();
                if (ship == null) throw new InvalidOperationException("The mod ship body has no ShipInstance.");
                var thrusters = ship.ThrusterHardpoints?.Length ?? 0;
                var weapons = ship.WeaponHardpoints?.Length ?? 0;
                if (thrusters == 0) throw new InvalidOperationException("ThrusterHardpoints is empty.");
                if (weapons == 0 || ship.WeaponHardpoints.Any(node => node.FiringPoint == null || node.FiringPoint.Length == 0))
                    throw new InvalidOperationException("WeaponHardpoints is empty or a weapon has no muzzle.");
                if (ship.HullColliders == null || ship.HullColliders.Length != 1)
                    throw new InvalidOperationException("The ship needs exactly one hull collider.");
                var collider = ship.HullColliders[0].GetComponent<MeshCollider>();
                if (collider == null || collider.sharedMesh == null || !collider.sharedMesh.isReadable || collider.sharedMesh.vertexCount == 0)
                    throw new InvalidOperationException("The hull collider is not a MeshCollider over a readable mesh.");
                if (ship.MapIcon == null || ship.MapIcon.sharedMaterial == null || ship.MapIcon.gameObject.layer != LayerMask.NameToLayer("Minimap"))
                    throw new InvalidOperationException("The map icon is missing its renderer, material or Minimap layer.");

                // Phase 2: an unimportable sibling composes, then faults the preload with ImportFault naming exactly it.
                var unimportable = InstallSibling(package, modsRoot, ".unimportable", null, DropBinChunk);
                var derivedTwo = Path.Combine(work, "Derived2", "Aetheria.modded.cc");
                var second = ShipModCatalog.Compose(shipped, derivedTwo, modsRoot);
                if (!second.Included.Contains(unimportable.Visual.Id))
                    throw new InvalidOperationException($"{unimportable.Visual.Id} was not composed: " + Reasons(second));
                ShipModShips.ImportFault fault = null;
                using (var catalogTwo = AetheriaStores.Open(derivedTwo))
                {
                    try { await ShipModShips.Preload(catalogTwo, modsRoot); }
                    catch (ShipModShips.ImportFault caught) { fault = caught; }
                }
                if (fault == null || fault.Faults.Length != 1 || fault.Faults[0].Package != unimportable.Visual.Id ||
                    !fault.Faults[0].Reason.StartsWith(unimportable.Hull.Name))
                    throw new InvalidOperationException("Loading did not fault with ImportFault naming exactly " + unimportable.Visual.Id + ".");

                // Phase 3: removal moves it aside, and the next compose leaves it out.
                var destination = ShipModCatalog.RemovePackage(modsRoot, unimportable.Visual.Id);
                if (Directory.Exists(Path.Combine(modsRoot, unimportable.Visual.Id)) ||
                    destination != Path.Combine(work, "RemovedMods", unimportable.Visual.Id) || !File.Exists(Path.Combine(destination, "ship.cc")))
                    throw new InvalidOperationException("RemovePackage did not move the package to RemovedMods.");
                var third = ShipModCatalog.Compose(shipped, Path.Combine(work, "Derived3", "Aetheria.modded.cc"), modsRoot);
                if (third.Included.Contains(unimportable.Visual.Id) || !third.Included.Contains(package.Visual.Id))
                    throw new InvalidOperationException("The compose after removal still includes the removed package, or lost the good one.");
                Debug.Log($"SHIP_MOD_PLAY_SMOKE thrusters={thrusters} weapons={weapons} collider=ok mapicon=ok excluded={broken.Visual.Id} " +
                    $"faulted={unimportable.Visual.Id} removed={Path.GetFileName(destination)}");
            }
            Clean(zone, work);
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            Clean(zone, work);
            EditorApplication.Exit(1);
        }
    }

    private static string Reasons(ShipModCatalog.Composition composition) =>
        string.Join("; ", composition.Excluded.Select(excluded => $"{excluded.Package}: {excluded.Reason}"));

    private static void Install(string id, ShipModCatalog.Package package, string modsRoot, string shipFile)
    {
        var model = Path.Combine(modsRoot, id, package.Visual.ModelAsset);
        Directory.CreateDirectory(Path.GetDirectoryName(model));
        File.Copy(package.ModelPath, model);
        if (shipFile != null) File.Copy(shipFile, Path.Combine(modsRoot, id, "ship.cc"));
    }

    // The package's ship under the id plus suffix, its records changed by tweak and its GLB rewritten in place by glb.
    // Returns the sibling's records.
    private static (HullData Hull, ShipAuthoring Visual) InstallSibling(ShipModCatalog.Package package, string modsRoot, string suffix,
        Action<ShipAuthoring> tweak, Action<string> glb)
    {
        var (hull, ship, _) = ShipAuthoringStore.Read(Path.Combine(modsRoot, package.Visual.Id, "ship.cc"));
        ship.Id += suffix;
        hull.Name += suffix;
        hull.Visual = new CultRecordRef<ShipAuthoring>(ShipModCatalog.AuthoringKey(ship.Id));
        tweak?.Invoke(ship);
        Install(ship.Id, package, modsRoot, null);
        glb?.Invoke(Path.Combine(modsRoot, ship.Id, package.Visual.ModelAsset));
        using (var cache = ShipAuthoringStore.Open(Path.Combine(modsRoot, ship.Id, "ship.cc"), writable: true))
        {
            ShipAuthoringStore.Write(cache, hull, ship);
            cache.FlushAsync().GetAwaiter().GetResult();
        }
        return (hull, ship);
    }

    // Keeps the 12-byte GLB header and the JSON chunk, drops the BIN chunk and rewrites the header's total length: the
    // headless reader needs only the JSON, the importer needs the buffer.
    private static void DropBinChunk(string path)
    {
        var bytes = File.ReadAllBytes(path);
        var end = 20 + (int)BitConverter.ToUInt32(bytes, 12);
        var kept = bytes.Take(end).ToArray();
        BitConverter.GetBytes((uint)kept.Length).CopyTo(kept, 8);
        File.WriteAllBytes(path, kept);
    }

    private static void Clean(GameObject zone, string work)
    {
        if (zone != null) UnityEngine.Object.DestroyImmediate(zone);
        try { if (Directory.Exists(work)) Directory.Delete(work, true); }
        catch (Exception error) { Debug.LogWarning($"Play smoke left {work}: {error.Message}"); }
    }
}
