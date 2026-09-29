using System;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// Focused Editor witness for the naming contract. Run with -executeMethod ShipPrefabAuthoringSmoke.Run.
public static class ShipPrefabAuthoringSmoke
{
    public static void Run()
    {
        GameObject root = null;
        try
        {
            root = new GameObject("Fixture");
            Primitive(root, "SHIP.MapIcon");
            Primitive(root, "SHIP.HullCollider");
            Child(root, "SHIP.Shield").localScale = new Vector3(2, 3, 4);
            Child(root, "SHIP.TractorBeam");
            var thruster = Child(root, "HP.Thruster.PortAft");
            Primitive(thruster.gameObject, "Emitter");
            var weapon = Child(root, "HP.Weapon.PortGun");
            Child(weapon.gameObject, "Muzzle.1");
            Child(weapon.gameObject, "Muzzle.0");
            var radiator = Child(root, "HP.Radiator.Port");
            Primitive(radiator.gameObject, "RadiatorMesh");
            Child(root, "HP.Equipment.Reactor");
            Child(root, "Pivot.2.-90.90.-80.80.60");

            ShipPrefabAuthoring.Configure(root);
            var ship = root.GetComponent<ShipInstance>();
            Require(ship != null && ship.EquipmentHardpoints.Length == 4, "equipment references");
            Require(ship.ThrusterHardpoints.Length == 1 && ship.ThrusterHardpoints[0].Emitter != null, "thruster emitter");
            Require(ship.WeaponHardpoints.Length == 1 && ship.WeaponHardpoints[0].FiringPoint.Length == 2, "weapon muzzles");
            Require(ship.WeaponHardpoints[0].FiringPoint[0].name == "Muzzle.0", "numeric muzzle order");
            Require(ship.RadiatorHardpoints.Length == 1 && ship.RadiatorHardpoints[0].Mesh != null, "radiator mesh");
            Require(ship.ArticulationPoints.Length == 1 && ship.ArticulationPoints[0].Group == 2, "articulation");
            Require(ship.HullColliders.Length == 1 && ship.HullColliders[0].GetComponent<MeshCollider>().convex, "hull collision");
            Require(ship.Shield != null && ship.Shield.GetComponent<ShieldEnvelope>() != null, "shield envelope");
            Require(ship.TractorBeam != null && ship.PingPrefab != null && ship.DestroyEffect != null, "shared assets");
            Require(ship.MapIcon != null && ship.MapIcon.gameObject.layer == LayerMask.NameToLayer("Minimap"), "map icon");

            var invalid = Child(root, "HP.Weapon.Broken");
            var refused = false;
            try { ShipPrefabAuthoring.Configure(root); }
            catch (InvalidOperationException ex) { refused = ex.Message.Contains("Muzzle"); }
            Require(refused, "invalid hierarchy refusal");
            Object.DestroyImmediate(invalid.gameObject);

            Debug.Log("ShipPrefabAuthoringSmoke: named hierarchy and refusal passed");
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            if (Application.isBatchMode) EditorApplication.Exit(1);
            else throw;
        }
        finally { if (root != null) Object.DestroyImmediate(root); }
    }

    private static Transform Child(GameObject parent, string name)
    {
        var child = new GameObject(name).transform;
        child.SetParent(parent.transform, false);
        return child;
    }

    private static GameObject Primitive(GameObject parent, string name)
    {
        var mesh = GameObject.CreatePrimitive(PrimitiveType.Cube);
        mesh.name = name;
        mesh.transform.SetParent(parent.transform, false);
        return mesh;
    }

    private static void Require(bool condition, string claim)
    {
        if (!condition) throw new InvalidOperationException($"ShipPrefabAuthoringSmoke failed: {claim}");
    }
}
