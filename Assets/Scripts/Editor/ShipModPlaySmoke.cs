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
// The package is installed beside a broken sibling: the same ship under another id, with its map-icon anchor moved onto
// its first muzzle's meshless node. The sibling composes, because no compose-time check reads meshes, and then fails
// assembly. The smoke proves the preload skips it and the good package still spawns.
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
            var broken = InstallWithBrokenSibling(packagePath, package, modsRoot);
            var composition = ShipModCatalog.Compose(Path.GetFullPath(Option("-shippedCatalog", "GameData/Aetheria.cc")), derived, modsRoot);
            foreach (var id in new[] { package.Visual.Id, broken.Visual.Id })
                if (!composition.Included.Contains(id))
                    throw new InvalidOperationException($"{id} was not composed into the catalog: " +
                        string.Join("; ", composition.Excluded.Select(excluded => $"{excluded.Package}: {excluded.Reason}")));

            using (var catalog = AetheriaStores.Open(derived))
            {
                await ShipModShips.Preload(catalog, modsRoot);
                HullData Hull(string name) => catalog.GetAll<HullData>().Single(candidate => candidate.Visual.IsSet() && candidate.Name == name);
                var hull = Hull(package.Hull.Name);

                // The broken sibling has no prototype, and Loading did not fault for it (await would have thrown).
                var refused = false;
                try { ShipModShips.Instantiate(Hull(broken.Hull.Name), null); }
                catch (InvalidOperationException error) { refused = error.Message.Contains("no mod ship prototype"); }
                if (!refused) throw new InvalidOperationException($"{broken.Visual.Id} assembled, or was refused for the wrong reason.");

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
                Debug.Log($"SHIP_MOD_PLAY_SMOKE thrusters={thrusters} weapons={weapons} collider=ok mapicon=ok skipped={broken.Visual.Id}");
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

    // Copies the package into modsRoot and writes its broken sibling beside it. Returns the sibling's records.
    private static (HullData Hull, ShipAuthoring Visual) InstallWithBrokenSibling(string packagePath, ShipModCatalog.Package package, string modsRoot)
    {
        void Install(string id, string shipFile)
        {
            var model = Path.Combine(modsRoot, id, package.Visual.ModelAsset);
            Directory.CreateDirectory(Path.GetDirectoryName(model));
            File.Copy(package.ModelPath, model);
            if (shipFile != null) File.Copy(shipFile, Path.Combine(modsRoot, id, "ship.cc"));
        }
        Install(package.Visual.Id, Path.GetFullPath(packagePath));

        var (hull, ship) = ShipAuthoringStore.Read(Path.Combine(modsRoot, package.Visual.Id, "ship.cc"));
        ship.Id += ".broken";
        hull.Name += " Broken";
        hull.Visual = new CultRecordRef<ShipAuthoring>(ShipModCatalog.AuthoringKey(ship.Id));
        var mapIcon = ship.Anchors.Single(anchor => anchor.Role == "map-icon");
        var muzzle = ship.Anchors.First(anchor => anchor.Role == "weapon-muzzle");
        (mapIcon.ModelNodeId, muzzle.ModelNodeId) = (muzzle.ModelNodeId, mapIcon.ModelNodeId);
        Install(ship.Id, null);
        using (var cache = ShipAuthoringStore.Open(Path.Combine(modsRoot, ship.Id, "ship.cc"), writable: true))
        {
            ShipAuthoringStore.Write(cache, hull, ship);
            cache.FlushAsync().GetAwaiter().GetResult();
        }
        return (hull, ship);
    }

    private static void Clean(GameObject zone, string work)
    {
        if (zone != null) UnityEngine.Object.DestroyImmediate(zone);
        try { if (Directory.Exists(work)) Directory.Delete(work, true); }
        catch (Exception error) { Debug.LogWarning($"Play smoke left {work}: {error.Message}"); }
    }
}
