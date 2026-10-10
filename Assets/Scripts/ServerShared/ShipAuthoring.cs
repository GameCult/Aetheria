using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using GameCult.Caching;
using GameCult.Caching.MessagePack;
using MessagePack;

// A mod ship's visual package: model, anchors, schematic lines. The hull semantics (cells, hardpoints, stats) live in
// the ship's own HullData record beside this one, and HullData.Visual is the only binding between the two.
[CultDocument("aetheria.ship_authoring", "1"), MessagePackObject]
public sealed class ShipAuthoring
{
    [CultName, Key(0)] public string Id;
    // Key 1 held the embedded HullData until S1; it is retired and never reused.
    [Key(2)] public string ModelAsset;
    [Key(3)] public List<ShipAnchor> Anchors = new List<ShipAnchor>();
    [Key(4)] public List<ShipPolyline> SchematicLines = new List<ShipPolyline>();
    // The moving parts of the rig, presentation only (ruling rig-is-presentation): no joint value feeds the simulation.
    [Key(5)] public List<ShipJoint> Joints = new List<ShipJoint>();
}

// A rig joint: a bone of the model's armature. Axes are three floats per degree of freedom, a unit vector in Ship Root
// space at rest (glTF axes, +Y up); Min and Max are that degree of freedom's limits in degrees.
[MessagePackObject]
public sealed class ShipJoint
{
    // The bone's aetheria.id, which is also its GLB node id.
    [Key(0)] public string Id;
    // The nearest joint above this one in the model, or null for a root joint.
    [Key(1)] public string Parent;
    [Key(2)] public float[] Axes;
    [Key(3)] public float[] Min;
    [Key(4)] public float[] Max;
}

[MessagePackObject]
public sealed class ShipAnchor
{
    // Stable semantic identity. HullData.Hardpoints[].Transform names this ID for mounts.
    [Key(0)] public string Id;
    // One of: map-icon, hull-collider, shield, tractor, thruster-emitter, weapon-mount,
    // weapon-muzzle, or radiator-mesh.
    [Key(1)] public string Role;
    // Stable node ID in the compiled model, never a Blender display name.
    [Key(2)] public string ModelNodeId;
    // Only child roles use ParentId; e.g. a muzzle names its weapon hardpoint.
    [Key(3)] public string ParentId;
    [Key(4)] public int Order;
    // On a weapon-mount anchor: the id of the nearest joint above its node in the model; null or empty is a fixed mount.
    [Key(5)] public string Joint;
}

// Evaluated or baked Grease Pencil stroke coordinates in Blender's right-handed Z-up
// space. The runtime renderer owns its coordinate conversion and camera projection.
[MessagePackObject]
public sealed class ShipPolyline
{
    [Key(0)] public string Layer;
    [Key(1)] public string Material;
    [Key(2)] public float[] Points;
    [Key(3)] public float[] Radii;
    [Key(4)] public float[] Opacities;
    [Key(5)] public bool Cyclic;
    [Key(6)] public float[] Color;
}

public static class ShipAuthoringStore
{
    // ShipAnchor.Role's vocabulary.
    private static readonly HashSet<string> Roles = new HashSet<string>(StringComparer.Ordinal)
    {
        "map-icon", "hull-collider", "shield", "tractor", "thruster-emitter", "weapon-mount", "weapon-muzzle", "radiator-mesh"
    };

    // The anchor role a visible hardpoint's mount plays, or null for an internal hardpoint, which has no anchor.
    private static string MountRole(HardpointType type) =>
        type == HardpointType.Thruster ? "thruster-emitter" :
        type == HardpointType.Radiator ? "radiator-mesh" :
        IsWeapon(type) ? "weapon-mount" : null;

    public static bool IsWeapon(HardpointType type) =>
        type == HardpointType.Energy || type == HardpointType.Ballistic || type == HardpointType.Launcher;

    public static CultCache Open(string path, bool writable = false)
    {
        var cache = new CultCache();
        try
        {
            cache.AddBackingStore(new SingleFileMessagePackBackingStore(path, !writable), new[] { typeof(HullData), typeof(ShipAuthoring) });
            return cache;
        }
        catch
        {
            cache.Dispose();
            throw;
        }
    }

    // Writes one ship's two records under their deterministic keys, the shape Load expects.
    public static void Write(CultCache cache, HullData hull, ShipAuthoring visual)
    {
        cache.UpsertAsync(typeof(HullData), hull, ShipModCatalog.HullKey(visual.Id)).GetAwaiter().GetResult();
        cache.UpsertAsync(typeof(ShipAuthoring), visual, ShipModCatalog.AuthoringKey(visual.Id)).GetAwaiter().GetResult();
    }

    // A new mod hull that flies like a shipped one. Every member of the reference is copied through the type's own
    // MessagePack round trip, so a member added to HullData later is copied too; then the six that are the shipped ship's
    // own body and layout are reset: Name, Shape (one occupied cell, so the record loads), Hardpoints, Prefab, Schematic,
    // and Visual, which names this mod's visual record.
    public static HullData HullLike(HullData reference, string name, string shipId)
    {
        var options = CultDocumentMessagePackSerialization.OptionsFor(typeof(HullData).Assembly);
        var hull = MessagePackSerializer.Deserialize<HullData>(MessagePackSerializer.Serialize(reference, options), options);
        hull.Name = name;
        hull.Shape = new Shape();
        hull.Hardpoints = new List<HardpointData>();
        hull.Prefab = null;
        hull.Schematic = null;
        hull.Visual = new CultRecordRef<ShipAuthoring>(ShipModCatalog.AuthoringKey(shipId));
        return hull;
    }

    // The structure of a ship file, without judging its content (drafts load): exactly one hull and one visual, each at
    // its deterministic key, the hull naming that visual by its typed ref.
    public static (HullData Hull, ShipAuthoring Visual) Load(string path)
    {
        RefuseLegacyEmbeddedHull(path);
        try { return LoadRecords(path); }
        catch (MessagePackSerializationException error)
        {
            throw new InvalidOperationException($"{path}: a record does not decode as its schema: {error.Message}", error);
        }
    }

    // Key 1 of ShipAuthoring held the whole hull before S1. The type no longer has that member, so a deserialized record
    // shows nothing of it, and a file that still carries one would load as a ship silently ignoring its old hull. The
    // raw payload is the only place the old shape is visible.
    private static void RefuseLegacyEmbeddedHull(string path)
    {
        if (!File.Exists(path)) return;
        var bytes = File.ReadAllBytes(path);
        if (bytes.Length == 0) return;
        CultPersistedStoreSnapshot snapshot;
        try { snapshot = CultDocumentMessagePackSerialization.DeserializeSnapshot(bytes); }
        catch (MessagePackSerializationException error)
        {
            throw new InvalidOperationException($"{path}: not a readable ship file: {error.Message}", error);
        }
        var schema = typeof(ShipAuthoring).GetCustomAttribute<CultDocumentAttribute>().SchemaName;
        foreach (var record in snapshot.Records)
        {
            if (snapshot.SchemaCatalog.FirstOrDefault(entry => entry.SchemaId == record.SchemaId)?.SchemaName != schema) continue;
            var reader = new MessagePackReader(record.Payload);
            if (reader.NextMessagePackType != MessagePackType.Array) continue;
            var slots = reader.ReadArrayHeader();
            for (var slot = 0; slot < slots; slot++)
            {
                if (slot == RetiredHullKey && !reader.TryReadNil())
                    throw new InvalidOperationException($"{path}: {record.Key}: legacy embedded hull at retired key {RetiredHullKey} of the ship authoring record. " +
                        $"Migrate it: write the hull as its own hull record at mod-hull:<id> naming the visual, and rewrite the ship authoring record without key {RetiredHullKey}.");
                if (slot != RetiredHullKey) reader.Skip();
            }
        }
    }

    private const int RetiredHullKey = 1;

    private static (HullData Hull, ShipAuthoring Visual) LoadRecords(string path)
    {
        using var cache = Open(path);
        var hulls = cache.GetAll<HullData>().ToArray();
        var visuals = cache.GetAll<ShipAuthoring>().ToArray();
        if (visuals.Length != 1)
            throw new InvalidOperationException($"{path} must hold exactly one ship authoring record, found {visuals.Length}.");
        if (hulls.Length != 1)
            throw new InvalidOperationException($"{path} must hold exactly one hull record, found {hulls.Length}.");
        var (hull, visual) = (hulls[0], visuals[0]);
        if (!cache.TryGetHandle(hull).Value.Key.Equals(ShipModCatalog.HullKey(visual.Id)) ||
            !cache.TryGetHandle(visual).Value.Key.Equals(ShipModCatalog.AuthoringKey(visual.Id)))
            throw new InvalidOperationException($"{path}: {visual.Id}: records must be stored under {ShipModCatalog.HullKey(visual.Id).Value} and {ShipModCatalog.AuthoringKey(visual.Id).Value}.");
        return (hull, visual);
    }

    public static (HullData Hull, ShipAuthoring Visual) Read(string path)
    {
        var (hull, visual) = Load(path);
        Validate(hull, visual);
        return (hull, visual);
    }

    // A hull names one body: a Unity prefab (shipped hulls) or a ShipAuthoring record (mod ships), never both, so no
    // consumer needs a precedence rule. A whitespace-only Prefab is unset. Every path that validates or composes hulls
    // calls this.
    public static void RequireOneBody(HullData hull, string label)
    {
        if (!string.IsNullOrWhiteSpace(hull.Prefab) && hull.Visual.IsSet())
            throw new InvalidOperationException($"{label}: a hull names one body, but this one names both a Unity prefab and a visual record.");
    }

    // The one semantic check every path shares: the hull and its visual, judged together.
    public static void Validate(HullData hull, ShipAuthoring ship)
    {
        if (ship == null) throw new InvalidOperationException("Ship authoring record is null.");
        if (string.IsNullOrWhiteSpace(ship.Id)) throw new InvalidOperationException("Ship ID is required.");
        if (!(ship.Id[0] >= 'a' && ship.Id[0] <= 'z' || ship.Id[0] >= '0' && ship.Id[0] <= '9') || ship.Id.Any(c =>
                !(c >= 'a' && c <= 'z' || c >= '0' && c <= '9' || c == '.' || c == '_' || c == '-')))
            throw new InvalidOperationException($"{ship.Id}: ship ID must use lower-case ASCII letters, digits, dots, underscores, or hyphens.");
        if (hull == null) throw new InvalidOperationException($"{ship.Id}: hull data is required.");
        if (string.IsNullOrWhiteSpace(hull.Name)) throw new InvalidOperationException($"{ship.Id}: hull name is required.");
        if (hull.Shape?.Cells == null || !hull.Shape.Cells.Cast<bool>().Any(occupied => occupied))
            throw new InvalidOperationException($"{ship.Id}: schematic must contain at least one cell.");
        RequireOneBody(hull, ship.Id);
        if (!hull.Visual.Key.Equals(ShipModCatalog.AuthoringKey(ship.Id)))
            throw new InvalidOperationException($"{ship.Id}: the hull must name its visual record {ShipModCatalog.AuthoringKey(ship.Id).Value}.");
        if (string.IsNullOrWhiteSpace(ship.ModelAsset) || Path.IsPathRooted(ship.ModelAsset) ||
            ship.ModelAsset.Replace('\\', '/').Split('/').Any(part => part == ".."))
            throw new InvalidOperationException($"{ship.Id}: model asset must be a relative package path.");

        var anchors = ship.Anchors ?? throw new InvalidOperationException($"{ship.Id}: anchors are required.");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var nodeIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var anchor in anchors)
        {
            if (anchor == null || string.IsNullOrWhiteSpace(anchor.Id) || !ids.Add(anchor.Id))
                throw new InvalidOperationException($"{ship.Id}: anchor IDs must be present and unique.");
            if (string.IsNullOrWhiteSpace(anchor.ModelNodeId))
                throw new InvalidOperationException($"{ship.Id}: anchor {anchor.Id} needs a model node ID.");
            if (!nodeIds.Add(anchor.ModelNodeId))
                throw new InvalidOperationException($"{ship.Id}: model node {anchor.ModelNodeId} is claimed by more than one anchor.");
        }
        foreach (var anchor in anchors)
            if (!Roles.Contains(anchor.Role))
                throw new InvalidOperationException($"{ship.Id}: anchor {anchor.Id} has unknown role '{anchor.Role}'.");
        foreach (var role in new[] { "map-icon", "hull-collider", "shield", "tractor" })
            if (anchors.Count(anchor => anchor.Role == role) != 1)
                throw new InvalidOperationException($"{ship.Id}: exactly one {role} anchor is required.");

        var mounts = new Dictionary<string, HardpointData>(StringComparer.Ordinal);
        var occupiedHardpointCells = new HashSet<(int x, int y)>();
        foreach (var hardpoint in hull.Hardpoints ?? new List<HardpointData>())
        {
            if (hardpoint == null || string.IsNullOrWhiteSpace(hardpoint.Transform) || mounts.ContainsKey(hardpoint.Transform))
                throw new InvalidOperationException($"{ship.Id}: hardpoint IDs must be present and unique.");
            mounts.Add(hardpoint.Transform, hardpoint);
            if (!Enum.IsDefined(typeof(HardpointType), hardpoint.Type))
                throw new InvalidOperationException($"{ship.Id}: hardpoint {hardpoint.Transform} has an unknown type {(int)hardpoint.Type}.");
            if (!Enum.IsDefined(typeof(ItemRotation), hardpoint.Rotation))
                throw new InvalidOperationException($"{ship.Id}: hardpoint {hardpoint.Transform} has an unknown rotation {(int)hardpoint.Rotation}.");
            if (!float.IsFinite(hardpoint.Armor) || hardpoint.Armor < 0 || !float.IsFinite(hardpoint.FiringArc) || hardpoint.FiringArc < 0)
                throw new InvalidOperationException($"{ship.Id}: hardpoint {hardpoint.Transform} armor and firing arc must be finite and not negative.");
            if (hardpoint.Shape?.Cells == null || !hardpoint.Shape.Cells.Cast<bool>().Any(occupied => occupied))
                throw new InvalidOperationException($"{ship.Id}: hardpoint {hardpoint.Transform} has no cells.");
            for (var cellX = 0; cellX < hardpoint.Shape.Width; cellX++)
            for (var cellY = 0; cellY < hardpoint.Shape.Height; cellY++)
            {
                if (!hardpoint.Shape.Cells[cellX, cellY]) continue;
                var x = hardpoint.Position.x + cellX;
                var y = hardpoint.Position.y + cellY;
                if (x < 0 || y < 0 || x >= hull.Shape.Width || y >= hull.Shape.Height || !hull.Shape.Cells[x, y])
                    throw new InvalidOperationException($"{ship.Id}: hardpoint {hardpoint.Transform} lies outside the hull schematic.");
                if (!occupiedHardpointCells.Add((x, y)))
                    throw new InvalidOperationException($"{ship.Id}: hardpoint {hardpoint.Transform} overlaps another hardpoint.");
            }
            // Only a hardpoint that shows on the model has a mount anchor, and its type fixes the role that anchor plays.
            var mountAnchor = anchors.FirstOrDefault(anchor => anchor.Id == hardpoint.Transform);
            var mountRole = MountRole(hardpoint.Type);
            if (mountRole == null && mountAnchor != null)
                throw new InvalidOperationException($"{ship.Id}: {hardpoint.Type} hardpoint {hardpoint.Transform} is internal, so no anchor may take its id.");
            if (mountRole != null && mountAnchor == null)
                throw new InvalidOperationException($"{ship.Id}: {hardpoint.Type} hardpoint {hardpoint.Transform} has no model anchor.");
            if (mountRole != null && mountAnchor.Role != mountRole)
                throw new InvalidOperationException($"{ship.Id}: {hardpoint.Type} hardpoint {hardpoint.Transform} needs a {mountRole} anchor of that id.");
        }
        foreach (var anchor in anchors)
        {
            if (!string.IsNullOrEmpty(anchor.ParentId) && !mounts.ContainsKey(anchor.ParentId))
                throw new InvalidOperationException($"{ship.Id}: anchor {anchor.Id} has unknown hardpoint parent {anchor.ParentId}.");
            // A mount-role anchor is the mount of a hardpoint of its type, never a loose node. The hardpoint loop above has
            // already refused one whose id names a hardpoint of another type, so here it only has to name a hardpoint.
            if ((anchor.Role == "thruster-emitter" || anchor.Role == "radiator-mesh" || anchor.Role == "weapon-mount") &&
                !mounts.ContainsKey(anchor.Id))
                throw new InvalidOperationException($"{ship.Id}: {anchor.Role} anchor {anchor.Id} must be the mount of a hardpoint of its type.");
            if (anchor.Role == "weapon-muzzle" &&
                (string.IsNullOrEmpty(anchor.ParentId) || !IsWeapon(mounts[anchor.ParentId].Type)))
                throw new InvalidOperationException($"{ship.Id}: muzzle {anchor.Id} must be parented to a weapon hardpoint's mount.");
        }
        foreach (var hardpoint in mounts.Values.Where(hardpoint => IsWeapon(hardpoint.Type)))
            if (!anchors.Any(anchor => anchor.Role == "weapon-muzzle" && anchor.ParentId == hardpoint.Transform))
                throw new InvalidOperationException($"{ship.Id}: weapon hardpoint {hardpoint.Transform} needs at least one muzzle anchor.");
        ValidateRig(ship, anchors, mounts, nodeIds);

        foreach (var line in ship.SchematicLines ?? new List<ShipPolyline>())
        {
            var points = line?.Points;
            if (points == null || points.Length < 6 || points.Length % 3 != 0 ||
                points.Any(value => float.IsNaN(value) || float.IsInfinity(value)))
                throw new InvalidOperationException($"{ship.Id}: schematic line needs finite XYZ points.");
            var count = points.Length / 3;
            if (line.Radii != null && line.Radii.Length != count ||
                line.Opacities != null && line.Opacities.Length != count)
                throw new InvalidOperationException($"{ship.Id}: schematic line point attributes have the wrong length.");
            if (line.Radii != null && line.Radii.Any(value => float.IsNaN(value) || float.IsInfinity(value) || value < 0) ||
                line.Opacities != null && line.Opacities.Any(value => float.IsNaN(value) || value < 0 || value > 1))
                throw new InvalidOperationException($"{ship.Id}: schematic line radius and opacity must be finite and in range.");
            if (line.Color != null && (line.Color.Length != 4 ||
                line.Color.Any(value => float.IsNaN(value) || float.IsInfinity(value))))
                throw new InvalidOperationException($"{ship.Id}: schematic line color must be finite RGBA.");
        }
    }

    // A degree of freedom counts as yaw when its axis lies within 5 degrees of Ship Root up (glTF +Y).
    private static readonly float YawAxisCos = MathF.Cos(5f * MathF.PI / 180f);

    // The joints, which anchors ride them, and whether each driven joint's chain can sweep the arc its guns state. The
    // joints are presentation, so this judges only that the rig could point the gun where the hardpoint says it may fire.
    private static void ValidateRig(ShipAuthoring ship, List<ShipAnchor> anchors, Dictionary<string, HardpointData> mounts, HashSet<string> nodeIds)
    {
        var joints = ship.Joints ?? new List<ShipJoint>();
        var byId = new Dictionary<string, ShipJoint>(StringComparer.Ordinal);
        foreach (var joint in joints)
        {
            if (joint == null || string.IsNullOrWhiteSpace(joint.Id) || byId.ContainsKey(joint.Id))
                throw new InvalidOperationException($"{ship.Id}: joint IDs must be present and unique.");
            if (nodeIds.Contains(joint.Id))
                throw new InvalidOperationException($"{ship.Id}: joint {joint.Id} is also an anchor's model node.");
            byId.Add(joint.Id, joint);
        }
        foreach (var joint in joints)
        {
            var dofs = joint.Min?.Length ?? 0;
            if (dofs < 1 || joint.Max?.Length != dofs || joint.Axes?.Length != 3 * dofs)
                throw new InvalidOperationException($"{ship.Id}: joint {joint.Id} needs one or more degrees of freedom: Axes holds three values each, Min and Max one each.");
            for (var dof = 0; dof < dofs; dof++)
            {
                var (x, y, z) = (joint.Axes[3 * dof], joint.Axes[3 * dof + 1], joint.Axes[3 * dof + 2]);
                var (min, max) = (joint.Min[dof], joint.Max[dof]);
                if (!float.IsFinite(x) || !float.IsFinite(y) || !float.IsFinite(z) || !float.IsFinite(min) || !float.IsFinite(max))
                    throw new InvalidOperationException($"{ship.Id}: joint {joint.Id} axes and limits must be finite.");
                if (MathF.Abs(MathF.Sqrt(x * x + y * y + z * z) - 1f) > 1e-3f)
                    throw new InvalidOperationException($"{ship.Id}: joint {joint.Id} axis {dof} must be a unit vector.");
                if (min < -180f || max > 180f || min > max)
                    throw new InvalidOperationException($"{ship.Id}: joint {joint.Id} limits {dof} must satisfy -180 <= Min <= Max <= 180.");
            }
            if (!string.IsNullOrEmpty(joint.Parent) && !byId.ContainsKey(joint.Parent))
                throw new InvalidOperationException($"{ship.Id}: joint {joint.Id} has an unknown parent joint.");
        }
        foreach (var joint in joints)
        {
            var step = joint;
            for (var depth = 0; !string.IsNullOrEmpty(step.Parent); depth++)
            {
                if (depth >= joints.Count)
                    throw new InvalidOperationException($"{ship.Id}: joint {joint.Id} is on a parent cycle.");
                step = byId[step.Parent];
            }
        }

        foreach (var anchor in anchors.Where(anchor => !string.IsNullOrEmpty(anchor.Joint)))
        {
            if (anchor.Role == "thruster-emitter" || anchor.Role == "radiator-mesh")
                throw new InvalidOperationException($"{ship.Id}: {anchor.Role} anchor {anchor.Id} may not ride a joint (follow-up gimballed-thrusters).");
            if (anchor.Role != "weapon-mount")
                throw new InvalidOperationException($"{ship.Id}: {anchor.Role} anchor {anchor.Id} may not name a joint; only a weapon mount rides one.");
            if (!byId.ContainsKey(anchor.Joint))
                throw new InvalidOperationException($"{ship.Id}: weapon mount {anchor.Id} names an unknown joint.");
        }

        var ridden = anchors.Where(anchor => anchor.Role == "weapon-mount" && !string.IsNullOrEmpty(anchor.Joint))
            .Select(anchor => (Hardpoint: mounts[anchor.Id], Joint: anchor.Joint)).ToArray();
        foreach (var (hardpoint, _) in ridden)
            if (!(hardpoint.FiringArc > 0f))
                throw new InvalidOperationException($"{ship.Id}: weapon hardpoint {hardpoint.Transform} rides a joint, so it must state its firing arc above 0.");
        foreach (var group in ridden.GroupBy(mount => mount.Joint, StringComparer.Ordinal))
        {
            var lead = group.First().Hardpoint;
            foreach (var (hardpoint, _) in group)
                if (hardpoint.Rotation != lead.Rotation || hardpoint.FiringArc != lead.FiringArc)
                    throw new InvalidOperationException($"{ship.Id}: weapon hardpoints {lead.Transform} and {hardpoint.Transform} ride joint {group.Key} and must share Rotation and FiringArc.");
            var reach = YawReach(ship, group.Key);
            if (reach < lead.FiringArc)
                throw new InvalidOperationException($"{ship.Id}: joint {group.Key} chain reaches {reach} degrees of yaw, short of the {lead.FiringArc} degree firing arc of {lead.Transform}.");
        }
    }

    // The yaw a driven joint's chain can sweep: the summed range of every degree of freedom whose axis is yaw, capped at a turn.
    private static float YawReach(ShipAuthoring ship, string jointId)
    {
        var joints = ship.Joints.ToDictionary(joint => joint.Id, StringComparer.Ordinal);
        var reach = 0f;
        foreach (var joint in RigChains.Chain(ship, jointId).Select(id => joints[id]))
            for (var dof = 0; dof < joint.Min.Length; dof++)
                if (MathF.Abs(joint.Axes[3 * dof + 1]) >= YawAxisCos)
                    reach += joint.Max[dof] - joint.Min[dof];
        return MathF.Min(reach, 360f);
    }
}

// A rig's driven joints and the chains that move them. A driven joint is one a weapon-mount anchor names. Its chain runs
// from the joint up through its ancestors and stops before the nearest ancestor that is itself driven (that joint has
// its own gun, and its pose is that gun's business), listed root-first. The judge and the plan both read this one rule.
public static class RigChains
{
    public static HashSet<string> Driven(ShipAuthoring ship) =>
        new HashSet<string>(ship.Anchors.Where(anchor => anchor.Role == "weapon-mount" && !string.IsNullOrEmpty(anchor.Joint)).Select(anchor => anchor.Joint),
            StringComparer.Ordinal);

    public static string[] Chain(ShipAuthoring ship, string jointId)
    {
        var joints = ship.Joints.ToDictionary(joint => joint.Id, StringComparer.Ordinal);
        var driven = Driven(ship);
        var chain = new List<string> { jointId };
        for (var parent = joints[jointId].Parent; !string.IsNullOrEmpty(parent) && !driven.Contains(parent); parent = joints[parent].Parent)
            chain.Add(parent);
        chain.Reverse();
        return chain.ToArray();
    }
}
