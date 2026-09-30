using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Batch proof that a mod ship survives the game's own path into a zone, without a scene: the derived catalog composed
// with one package, ShipModShips.Preload over it, then ShipModShips.Instantiate, which is what ZoneRenderer.LoadEntity's
// Body() calls for a hull with a Visual. A plain parent transform stands in for ZoneRoot; nothing else in LoadEntity
// touches the body before it takes its ShipInstance.
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
        var derived = Path.Combine(Path.GetTempPath(), $"ship-mod-play-smoke-{Guid.NewGuid():N}.cc");
        GameObject zone = null;
        try
        {
            if (packagePath == null) throw new InvalidOperationException("-shipModPath <package/ship.cc> is required");
            var package = ShipModCatalog.ReadPackage(packagePath);
            var modsRoot = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetFullPath(packagePath)));
            var composition = ShipModCatalog.Compose(Path.GetFullPath(Option("-shippedCatalog", "GameData/Aetheria.cc")), derived, modsRoot);
            if (!composition.Included.Contains(package.Visual.Id))
                throw new InvalidOperationException($"{package.Visual.Id} was not composed into the catalog: " +
                    string.Join("; ", composition.Excluded.Select(excluded => $"{excluded.Package}: {excluded.Reason}")));

            using (var catalog = AetheriaStores.Open(derived))
            {
                await ShipModShips.Preload(catalog, modsRoot);
                var hull = catalog.GetAll<HullData>().Single(candidate => candidate.Visual.IsSet() && candidate.Name == package.Hull.Name);

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
                Debug.Log($"SHIP_MOD_PLAY_SMOKE thrusters={thrusters} weapons={weapons} collider=ok mapicon=ok");
            }
            if (zone != null) UnityEngine.Object.DestroyImmediate(zone);
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            if (zone != null) UnityEngine.Object.DestroyImmediate(zone);
            EditorApplication.Exit(1);
        }
    }
}
