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
    // Mount ids whose anchor (role thruster-emitter / radiator-mesh) is the emitter / mesh node itself.
    public string[] Thrusters;
    public string[] Radiators;
    // Each weapon mount (role weapon-mount) and its muzzle anchors, by Order then id.
    public (string Mount, string[] Muzzles)[] Weapons;

    // Maps a pair Validate accepts; every rule about which anchor may play which part is Validate's, so this refuses nothing.
    public static ShipModPlan Build(HullData hull, ShipAuthoring visual)
    {
        ShipAuthoringStore.Validate(hull, visual);
        var hardpoints = hull.Hardpoints ?? new List<HardpointData>();
        string Only(string wanted) => visual.Anchors.Single(anchor => anchor.Role == wanted).Id;
        string[] Mounts(HardpointType type) => hardpoints.Where(hardpoint => hardpoint.Type == type).Select(hardpoint => hardpoint.Transform).ToArray();
        var muzzles = visual.Anchors.Where(anchor => anchor.Role == "weapon-muzzle")
            .OrderBy(anchor => anchor.Order).ThenBy(anchor => anchor.Id, StringComparer.Ordinal).ToArray();

        return new ShipModPlan
        {
            MapIcon = Only("map-icon"),
            HullCollider = Only("hull-collider"),
            Shield = Only("shield"),
            Tractor = Only("tractor"),
            Thrusters = Mounts(HardpointType.Thruster),
            Radiators = Mounts(HardpointType.Radiator),
            Weapons = hardpoints.Where(hardpoint => ShipAuthoringStore.IsWeapon(hardpoint.Type)).Select(hardpoint => (
                hardpoint.Transform,
                muzzles.Where(muzzle => muzzle.ParentId == hardpoint.Transform).Select(muzzle => muzzle.Id).ToArray())).ToArray()
        };
    }
}
