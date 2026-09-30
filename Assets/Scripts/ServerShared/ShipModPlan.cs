using System;
using System.Collections.Generic;
using System.Linq;

// Which anchor fills which slot of a ship's component arrays. Pure over (hull, visual) so the Unity assembler only
// follows it: the hull's hardpoint type decides what a mount is, the anchor's role decides which node plays the part.
public sealed class ShipModPlan
{
    public string MapIcon;
    public string HullCollider;
    public string Shield;
    public string Tractor;
    // Every hull hardpoint's mount id, in hardpoint order: the anchor node each one is matched to by name.
    public string[] Equipment;
    // Mount ids whose anchor (role thruster-emitter / radiator-mesh) is the emitter / mesh node itself.
    public string[] Thrusters;
    public string[] Radiators;
    // A weapon mount's muzzle anchors, by Order then id.
    public (string Mount, string[] Muzzles)[] Weapons;

    // Anchors with role "articulation" become ordinary mount nodes: ArticulationPoint's limits are not authorable until
    // pivots move onto HullData, so no ArticulationPoint is planned.
    public static ShipModPlan Build(HullData hull, ShipAuthoring visual)
    {
        ShipAuthoringStore.Validate(hull, visual);
        var hardpoints = hull.Hardpoints ?? new List<HardpointData>();
        var byMount = hardpoints.ToDictionary(hardpoint => hardpoint.Transform, StringComparer.Ordinal);
        var role = visual.Anchors.ToDictionary(anchor => anchor.Id, anchor => anchor.Role, StringComparer.Ordinal);
        string Only(string wanted) => visual.Anchors.Single(anchor => anchor.Role == wanted).Id;

        foreach (var anchor in visual.Anchors.Where(anchor => anchor.Role == "thruster-emitter" || anchor.Role == "radiator-mesh"))
        {
            var wanted = anchor.Role == "thruster-emitter" ? HardpointType.Thruster : HardpointType.Radiator;
            if (!byMount.TryGetValue(anchor.Id, out var mount) || mount.Type != wanted)
                throw new InvalidOperationException($"{visual.Id}: {anchor.Role} anchor {anchor.Id} must be a {wanted} hardpoint's mount.");
        }
        string[] Mounts(HardpointType type, string wantedRole) => hardpoints.Where(hardpoint => hardpoint.Type == type).Select(hardpoint =>
            role[hardpoint.Transform] == wantedRole ? hardpoint.Transform :
            throw new InvalidOperationException($"{visual.Id}: {type} hardpoint {hardpoint.Transform} needs a {wantedRole} anchor of that id.")).ToArray();

        var muzzles = visual.Anchors.Where(anchor => anchor.Role == "weapon-muzzle")
            .OrderBy(anchor => anchor.Order).ThenBy(anchor => anchor.Id, StringComparer.Ordinal).ToArray();
        foreach (var muzzle in muzzles)
            if (string.IsNullOrEmpty(muzzle.ParentId) || !IsWeapon(byMount[muzzle.ParentId].Type))
                throw new InvalidOperationException($"{visual.Id}: muzzle {muzzle.Id} must be parented to a weapon hardpoint's mount.");
        var weapons = hardpoints.Where(hardpoint => IsWeapon(hardpoint.Type)).Select(hardpoint => (
            hardpoint.Transform,
            muzzles.Where(muzzle => muzzle.ParentId == hardpoint.Transform).Select(muzzle => muzzle.Id).ToArray())).ToArray();
        foreach (var (mount, ids) in weapons)
            if (ids.Length == 0)
                throw new InvalidOperationException($"{visual.Id}: weapon hardpoint {mount} needs at least one muzzle anchor.");

        return new ShipModPlan
        {
            MapIcon = Only("map-icon"),
            HullCollider = Only("hull-collider"),
            Shield = Only("shield"),
            Tractor = Only("tractor"),
            Equipment = hardpoints.Select(hardpoint => hardpoint.Transform).ToArray(),
            Thrusters = Mounts(HardpointType.Thruster, "thruster-emitter"),
            Radiators = Mounts(HardpointType.Radiator, "radiator-mesh"),
            Weapons = weapons
        };
    }

    private static bool IsWeapon(HardpointType type) =>
        type == HardpointType.Energy || type == HardpointType.Ballistic || type == HardpointType.Launcher;
}
