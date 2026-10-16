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
            cache.AddBackingStore(new SingleFileMessagePackBackingStore(path, !writable), new[] { typeof(HullData), typeof(ShipAuthoring), typeof(FactionProductData) });
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

    // A package's product by one maker. These two are the only product writers; the key is the ship and the maker, so a
    // maker has at most one product of the hull and writing again replaces it.
    public static void WriteProduct(CultCache cache, string shipId, FactionProductData product) =>
        cache.UpsertAsync(typeof(FactionProductData), product, ShipModCatalog.ProductKey(shipId, product.Manufacturer.Key)).GetAwaiter().GetResult();

    public static bool DeleteProduct(CultCache cache, string shipId, CultRecordKey maker) =>
        cache.Remove(ShipModCatalog.ProductKey(shipId, maker));

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
    // its deterministic key, the hull naming that visual by its typed ref, and the hull's products, each at its maker's key.
    public static (HullData Hull, ShipAuthoring Visual, FactionProductData[] Products) Load(string path)
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

    private static (HullData Hull, ShipAuthoring Visual, FactionProductData[] Products) LoadRecords(string path)
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
        var products = cache.GetAll<FactionProductData>().ToArray();
        foreach (var product in products)
        {
            if (!product.Manufacturer.IsSet() || ShipModCatalog.IsModKey(product.Manufacturer.Key))
                throw new InvalidOperationException($"{path}: {visual.Id}: a product needs a manufacturer that is a shipped faction.");
            var productKey = ShipModCatalog.ProductKey(visual.Id, product.Manufacturer.Key);
            if (!cache.TryGetHandle(product).Value.Key.Equals(productKey))
                throw new InvalidOperationException($"{path}: {visual.Id}: a product must be stored at {productKey.Value}.");
            if (!product.Design.IsSet() || !product.Design.Key.Equals(ShipModCatalog.HullKey(visual.Id)))
                throw new InvalidOperationException($"{path}: {visual.Id}: a product must name the package hull {ShipModCatalog.HullKey(visual.Id).Value} as its design.");
        }
        return (hull, visual, products);
    }

    public static (HullData Hull, ShipAuthoring Visual, FactionProductData[] Products) Read(string path)
    {
        var (hull, visual, products) = Load(path);
        ValidateLoaded(hull, visual, products);
        return (hull, visual, products);
    }

    // Every judgement a loaded package's records face, in one place: Read, and validate on a draft with no model yet, call it.
    public static void ValidateLoaded(HullData hull, ShipAuthoring visual, FactionProductData[] products)
    {
        Validate(hull, visual);
        ValidateProducts(hull, visual, products);
    }

    // What a package may sell: every product named, no name twice, and a price above 0, because generation skips price 0.
    private static void ValidateProducts(HullData hull, ShipAuthoring visual, FactionProductData[] products)
    {
        if (products.Length == 0) return;
        if (products.Any(product => string.IsNullOrWhiteSpace(product.Name)))
            throw new InvalidOperationException($"{visual.Id}: every product needs a name.");
        if (products.Select(product => product.Name).Distinct(StringComparer.Ordinal).Count() != products.Length)
            throw new InvalidOperationException($"{visual.Id}: product names must be unique within the package.");
        if (hull.Price <= 0)
            throw new InvalidOperationException($"{visual.Id}: a hull with products needs a price above 0 (generation skips price 0).");
    }

    // A hull names one body: a Unity prefab (shipped hulls) or a ShipAuthoring record (mod ships), never both, so no
    // consumer needs a precedence rule. A whitespace-only Prefab is unset. Every path that validates or composes hulls
    // calls this.
    public static void RequireOneBody(HullData hull, string label)
    {
        if (!string.IsNullOrWhiteSpace(hull.Prefab) && hull.Visual.IsSet())
            throw new InvalidOperationException($"{label}: a hull names one body, but this one names both a Unity prefab and a visual record.");
    }

    // The one ship-ID rule, shared by Validate and ship-authoring create. The ID becomes a folder name on every platform, so
    // Windows device names (alone or before a dot) and a trailing dot are refused too. The messages name the rule, never the value.
    public static void RequireShipId(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) throw new InvalidOperationException("Ship ID is required.");
        if (!(id[0] >= 'a' && id[0] <= 'z' || id[0] >= '0' && id[0] <= '9') || id.Any(c =>
                !(c >= 'a' && c <= 'z' || c >= '0' && c <= '9' || c == '.' || c == '_' || c == '-')))
            throw new InvalidOperationException("Ship ID must use lower-case ASCII letters, digits, dots, underscores, or hyphens.");
        if (id[id.Length - 1] == '.') throw new InvalidOperationException("Ship ID must not end with a dot.");
        var stem = id.Split('.')[0];
        if (stem is "con" or "prn" or "aux" or "nul"
            || stem.Length == 4 && (stem.StartsWith("com") || stem.StartsWith("lpt")) && stem[3] >= '1' && stem[3] <= '9')
            throw new InvalidOperationException("Ship ID must not be a Windows device name.");
    }

    // The one semantic check every path shares: the hull and its visual, judged together.
    public static void Validate(HullData hull, ShipAuthoring ship)
    {
        if (ship == null) throw new InvalidOperationException("Ship authoring record is null.");
        RequireShipId(ship.Id);
        if (hull == null) throw new InvalidOperationException($"{ship.Id}: hull data is required.");
        if (string.IsNullOrWhiteSpace(hull.Name)) throw new InvalidOperationException($"{ship.Id}: hull name is required.");
        if (hull.Shape?.Cells == null || !hull.Shape.Cells.Cast<bool>().Any(occupied => occupied))
            throw new InvalidOperationException($"{ship.Id}: schematic must contain at least one cell.");
        RequireOneBody(hull, ship.Id);
        if (hull.HullType == HullType.Ship && !(hull.Behaviors ?? new List<BehaviorData>()).Any(behavior => behavior is VelocityLimitData))
            throw new InvalidOperationException($"{ship.Id}: a ship hull needs a top speed (VelocityLimit).");
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
            // A thruster's gimbal range is the judge's alone, a row of one included: no thruster swings further than a turn.
            if (hardpoint.Type == HardpointType.Thruster && hardpoint.FiringArc > 360f)
                throw new InvalidOperationException($"{ship.Id}: thruster hardpoint {hardpoint.Transform} firing arc may be at most 360 degrees.");
            if (!string.IsNullOrEmpty(hardpoint.Row) && hardpoint.Type != HardpointType.Thruster)
                throw new InvalidOperationException($"{ship.Id}: hardpoint {hardpoint.Transform} is a {hardpoint.Type} hardpoint, and only a Thruster hardpoint may state a row.");
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
        foreach (var row in mounts.Values.Where(hardpoint => hardpoint.Type == HardpointType.Thruster && !string.IsNullOrEmpty(hardpoint.Row))
                     .GroupBy(hardpoint => hardpoint.Row, StringComparer.Ordinal))
        {
            var lead = row.First();
            foreach (var hardpoint in row)
                if (hardpoint.Rotation != lead.Rotation || hardpoint.FiringArc != lead.FiringArc)
                    throw new InvalidOperationException($"{ship.Id}: thruster hardpoints {lead.Transform} and {hardpoint.Transform} are in row {row.Key} and must share Rotation and FiringArc.");
            if (!(lead.FiringArc > 0f))
                throw new InvalidOperationException($"{ship.Id}: thruster row {row.Key} must state a gimbal FiringArc above 0.");
        }
        foreach (var anchor in anchors)
        {
            if (!string.IsNullOrEmpty(anchor.ParentId) && !mounts.ContainsKey(anchor.ParentId))
                throw new InvalidOperationException($"{ship.Id}: anchor {anchor.Id} has unknown hardpoint parent {anchor.ParentId}.");
            // A mount-role anchor is the mount of a hardpoint of its type, never a loose node. The hardpoint loop above has
            // already refused one whose id names a hardpoint of another type, so here it only has to name a hardpoint.
            if ((RigChains.Actuates(anchor.Role) || anchor.Role == "radiator-mesh") && !mounts.ContainsKey(anchor.Id))
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
            if (anchor.Role == "radiator-mesh")
                throw new InvalidOperationException($"{ship.Id}: {anchor.Role} anchor {anchor.Id} may not ride a joint.");
            if (!RigChains.Actuates(anchor.Role))
                throw new InvalidOperationException($"{ship.Id}: {anchor.Role} anchor {anchor.Id} may not name a joint; only a weapon mount or a thruster emitter rides one.");
            if (!byId.ContainsKey(anchor.Joint))
                throw new InvalidOperationException($"{ship.Id}: {anchor.Role} anchor {anchor.Id} names an unknown joint.");
        }

        // A joint has one actuator (ruling one-joint-one-actuator), and RigChains.Drives is the only place that says what drives a
        // joint: a joint carrying two actuators, or whose reach passes through a joint another actuator drives, is refused here,
        // whatever the two actuators are.
        var drives = RigChains.Drives(ship).Select(drive => (drive.Joint, Hardpoint: mounts[drive.Anchor.Id])).ToArray();
        foreach (var onJoint in drives.GroupBy(drive => drive.Joint, StringComparer.Ordinal))
        {
            var lead = onJoint.First().Hardpoint;
            var other = onJoint.Select(drive => drive.Hardpoint).FirstOrDefault(hardpoint => RigChains.Actuator(hardpoint) != RigChains.Actuator(lead));
            if (other != null)
                throw new InvalidOperationException($"{ship.Id}: joint {onJoint.Key} carries more than one actuator ({lead.Transform} and {other.Transform}); a joint has one actuator.");
        }
        var driven = RigChains.Driven(ship);
        foreach (var joint in driven)
        {
            var through = RigChains.Chain(ship, joint).SkipLast(1).FirstOrDefault(driven.Contains);
            if (through != null)
                throw new InvalidOperationException($"{ship.Id}: joint {joint} reaches through joint {through}, which another actuator drives; a joint has one actuator.");
        }

        var ridden = drives.Where(drive => IsWeapon(drive.Hardpoint.Type)).ToArray();
        foreach (var (_, hardpoint) in ridden)
            if (!(hardpoint.FiringArc > 0f))
                throw new InvalidOperationException($"{ship.Id}: weapon hardpoint {hardpoint.Transform} rides a joint, so it must state its firing arc above 0.");
        foreach (var group in ridden.GroupBy(mount => mount.Joint, StringComparer.Ordinal))
        {
            var lead = group.First().Hardpoint;
            foreach (var (_, hardpoint) in group)
                if (hardpoint.Rotation != lead.Rotation || hardpoint.FiringArc != lead.FiringArc)
                    throw new InvalidOperationException($"{ship.Id}: weapon hardpoints {lead.Transform} and {hardpoint.Transform} ride joint {group.Key} and must share Rotation and FiringArc.");
            var reach = YawReach(ship, group.Key);
            if (reach < lead.FiringArc)
                throw new InvalidOperationException($"{ship.Id}: joint {group.Key} chain reaches {reach} degrees of yaw, short of the {lead.FiringArc} degree firing arc of {lead.Transform}.");
        }

        // A thruster row is a gimbal: its hardpoints' stated FiringArc is how far the row swings, so a row on a joint rides it
        // whole, and the joint's chain must be able to sweep that arc. A hardpoint with no Row is a row of one.
        var thrusters = mounts.Values.Where(hardpoint => hardpoint.Type == HardpointType.Thruster).ToArray();
        var tilted = drives.Where(drive => drive.Hardpoint.Type == HardpointType.Thruster).ToArray();
        foreach (var (_, hardpoint) in tilted)
            if (!(hardpoint.FiringArc > 0f))
                throw new InvalidOperationException($"{ship.Id}: thruster hardpoint {hardpoint.Transform} rides a joint, so it must state its firing arc above 0; a fixed thruster may not ride a joint.");
        foreach (var onJoint in tilted.GroupBy(drive => drive.Joint, StringComparer.Ordinal))
        {
            var lead = onJoint.First().Hardpoint;
            var members = string.IsNullOrEmpty(lead.Row) ? new[] { lead } : thrusters.Where(hardpoint => hardpoint.Row == lead.Row).ToArray();
            foreach (var member in members)
                if (!tilted.Any(drive => drive.Hardpoint == member && drive.Joint == onJoint.Key))
                    throw new InvalidOperationException($"{ship.Id}: thruster hardpoint {member.Transform} is in the row of {lead.Transform} on joint {onJoint.Key} and must ride it too; a row rides one joint whole.");
            var reach = YawReach(ship, onJoint.Key);
            if (reach < lead.FiringArc)
                throw new InvalidOperationException($"{ship.Id}: joint {onJoint.Key} chain reaches {reach} degrees of yaw, short of the {lead.FiringArc} degree gimbal arc of {lead.Transform}.");
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

// A rig's actuators and the joints they drive. An actuator is a weapon mount or a thruster emitter that names a joint; a gun
// group (every gun on a joint) or a thruster row is one actuator, and a joint has at most one (ruling one-joint-one-actuator).
// This is the only place that says which anchors are actuators, so the judge, the binder and the plan all follow it.
public static class RigChains
{
    public static bool Actuates(string role) => role == "weapon-mount" || role == "thruster-emitter";

    // Every actuator anchor that names a joint, with the joint it drives.
    public static IEnumerable<(string Joint, ShipAnchor Anchor)> Drives(ShipAuthoring ship) =>
        ship.Anchors.Where(anchor => Actuates(anchor.Role) && !string.IsNullOrEmpty(anchor.Joint)).Select(anchor => (anchor.Joint, anchor));

    public static HashSet<string> Driven(ShipAuthoring ship) =>
        new HashSet<string>(Drives(ship).Select(drive => drive.Joint), StringComparer.Ordinal);

    // The actuator a mount belongs to: all guns on a joint are one, a thruster is its row, and a thruster of no row is its own.
    public static string Actuator(HardpointData hardpoint) =>
        ShipAuthoringStore.IsWeapon(hardpoint.Type) ? "weapon" : string.IsNullOrEmpty(hardpoint.Row) ? "thruster:" + hardpoint.Transform : "row:" + hardpoint.Row;

    // The joint and its ancestors, root-first. The judge refuses a driven joint whose ancestors another actuator drives,
    // so for an accepted ship this is the whole chain the actuator's reach is summed over.
    public static string[] Chain(ShipAuthoring ship, string jointId)
    {
        var joints = ship.Joints.ToDictionary(joint => joint.Id, StringComparer.Ordinal);
        var chain = new List<string> { jointId };
        for (var parent = joints[jointId].Parent; !string.IsNullOrEmpty(parent); parent = joints[parent].Parent)
            chain.Add(parent);
        chain.Reverse();
        return chain.ToArray();
    }
}
