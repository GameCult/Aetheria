using GameCult.Caching;
using GameCult.Caching.MessagePack;
using MessagePack;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using int2 = CultMath.int2;

// ship-authoring open and push: one request vocabulary for the hull layout, the visual anchors and the schematic lines.
// open prints a package's authored members as a request; push reads a request, derives the records it implies from the
// stored ones, prints each change, and under `apply` writes the changed records in one commit that expects every record
// still to be the instance it was loaded as. The version token is each record's public StoredAt (every write re-mints
// it), so a record written since open is stale. Dry run is the default (ruling agents-dry-run-human-applies).
public static class ShipAuthoringPush
{
    // A request is open's output and is parsed strictly: an unknown key and a missing key are refused by name.
    [JsonObject(MemberSerialization.OptIn)]
    private sealed class Request
    {
        [JsonProperty("ship", Required = Required.DisallowNull)] public string Ship;
        [JsonProperty("versions", Required = Required.DisallowNull)] public Dictionary<string, string> Versions;
        [JsonProperty("layout")] public LayoutSection Layout;
        [JsonProperty("visual")] public VisualSection Visual;
        [JsonProperty("lines")] public List<LineRow> Lines;
    }

    [JsonObject(MemberSerialization.OptIn)]
    private sealed class LayoutSection
    {
        [JsonProperty("grid", Required = Required.DisallowNull)] public string Grid;
        [JsonProperty("hardpoints", Required = Required.DisallowNull)] public List<HardpointRow> Hardpoints;
    }

    [JsonObject(MemberSerialization.OptIn)]
    private sealed class HardpointRow
    {
        [JsonProperty("element", Required = Required.DisallowNull)] public string Element;
        [JsonProperty("mount", Required = Required.DisallowNull)] public string Mount;
        [JsonProperty("kind", Required = Required.DisallowNull)] public string Kind;
        [JsonProperty("cell", Required = Required.DisallowNull)] public int[] Cell;
        [JsonProperty("footprint", Required = Required.DisallowNull)] public string Footprint;
        [JsonProperty("rotation", Required = Required.Always)] public int Rotation;
        [JsonProperty("armor", Required = Required.Always)] public float Armor;
        [JsonProperty("arc", Required = Required.Always)] public float Arc;
    }

    [JsonObject(MemberSerialization.OptIn)]
    private sealed class VisualSection
    {
        [JsonProperty("model", Required = Required.DisallowNull)] public string Model;
        [JsonProperty("anchors", Required = Required.DisallowNull)] public List<AnchorRow> Anchors;
    }

    [JsonObject(MemberSerialization.OptIn)]
    private sealed class AnchorRow
    {
        [JsonProperty("id", Required = Required.DisallowNull)] public string Id;
        [JsonProperty("role", Required = Required.DisallowNull)] public string Role;
        [JsonProperty("node", Required = Required.DisallowNull)] public string Node;
        [JsonProperty("parent", Required = Required.AllowNull)] public string Parent;
        [JsonProperty("order", Required = Required.Always)] public int Order;
    }

    [JsonObject(MemberSerialization.OptIn)]
    private sealed class LineRow
    {
        [JsonProperty("element", Required = Required.DisallowNull)] public string Element;
        [JsonProperty("layer", Required = Required.AllowNull)] public string Layer;
        [JsonProperty("material", Required = Required.AllowNull)] public string Material;
        [JsonProperty("points", Required = Required.AllowNull)] public float[] Points;
        [JsonProperty("radii", Required = Required.AllowNull)] public float[] Radii;
        [JsonProperty("opacities", Required = Required.AllowNull)] public float[] Opacities;
        [JsonProperty("cyclic", Required = Required.Always)] public bool Cyclic;
        [JsonProperty("color", Required = Required.AllowNull)] public float[] Color;
    }

    private const int MaxCells = 32;

    private static readonly JsonSerializerSettings Strict = new JsonSerializerSettings
    {
        MissingMemberHandling = MissingMemberHandling.Error,
        DateParseHandling = DateParseHandling.None
    };

    private static MessagePackSerializerOptions PackOptions => CultDocumentMessagePackSerialization.OptionsFor(typeof(HullData).Assembly);

    private static byte[] Pack<T>(T value) => MessagePackSerializer.Serialize(value, PackOptions);

    // A deep copy by the type's own MessagePack round trip, as HullLike copies a hull: a member added later is copied too.
    private static T Copy<T>(T value) => MessagePackSerializer.Deserialize<T>(Pack(value), PackOptions);

    // The request that, pushed back unchanged, changes nothing.
    public static int Open(string path, TextWriter output)
    {
        using var cache = ShipAuthoringStore.Open(path);
        var (hull, ship, _) = ShipAuthoringStore.Load(cache, path);
        var request = new Dictionary<string, object>
        {
            ["ship"] = ship.Id,
            ["versions"] = Versions(cache, ship.Id),
            ["layout"] = new Dictionary<string, object>
            {
                ["grid"] = Footprint(hull.Shape),
                ["hardpoints"] = (hull.Hardpoints ?? new List<HardpointData>()).Select(hardpoint => new Dictionary<string, object>
                {
                    ["element"] = hardpoint.Id ?? "",
                    ["mount"] = hardpoint.Transform,
                    ["kind"] = hardpoint.Type.ToString(),
                    ["cell"] = new[] { hardpoint.Position.x, hardpoint.Position.y },
                    ["footprint"] = Footprint(hardpoint.Shape),
                    ["rotation"] = (int)hardpoint.Rotation,
                    ["armor"] = hardpoint.Armor,
                    ["arc"] = hardpoint.FiringArc
                }).ToList()
            },
            ["visual"] = new Dictionary<string, object>
            {
                ["model"] = ship.ModelAsset,
                ["anchors"] = (ship.Anchors ?? new List<ShipAnchor>()).Select(anchor => new Dictionary<string, object>
                {
                    ["id"] = anchor.Id,
                    ["role"] = anchor.Role,
                    ["node"] = anchor.ModelNodeId,
                    ["parent"] = anchor.ParentId,
                    ["order"] = anchor.Order
                }).ToList()
            },
            ["lines"] = (ship.SchematicLines ?? new List<ShipPolyline>()).Select(line => new Dictionary<string, object>
            {
                ["element"] = line.Id ?? "",
                ["layer"] = line.Layer,
                ["material"] = line.Material,
                ["points"] = line.Points,
                ["radii"] = line.Radii,
                ["opacities"] = line.Opacities,
                ["cyclic"] = line.Cyclic,
                ["color"] = line.Color
            }).ToList()
        };
        output.WriteLine(JsonConvert.SerializeObject(request, Formatting.Indented));
        return 0;
    }

    private static Dictionary<string, string> Versions(CultCache cache, string shipId) =>
        new[] { ShipModCatalog.HullKey(shipId), ShipModCatalog.AuthoringKey(shipId) }
            .ToDictionary(key => key.Value, key => cache.GetStored(key).StoredAt, StringComparer.Ordinal);

    // Reads a request from stdin and prints what it would change; with apply, writes it. Exit 0 dry run or applied,
    // 1 refused (the file is untouched), 2 stale (a record was written since the request's open; nothing is written).
    public static int Push(string path, TextReader requestText, bool apply, TextWriter output)
    {
        var lines = new List<string>();
        var changed = new List<string>();
        Request request;
        try
        {
            request = JsonConvert.DeserializeObject<Request>(requestText.ReadToEnd(), Strict) ??
                throw new InvalidOperationException("The request is empty.");
        }
        catch (JsonException error) { return Refused(output, "request: " + error.Message); }
        try
        {
            using var cache = ShipAuthoringStore.Open(path, writable: apply);
            var (hull, ship, products) = ShipAuthoringStore.Load(cache, path);
            var hullKey = ShipModCatalog.HullKey(ship.Id);
            var shipKey = ShipModCatalog.AuthoringKey(ship.Id);
            if (!string.Equals(request.Ship, ship.Id, StringComparison.Ordinal))
                throw new InvalidOperationException($"request: ship is not {ship.Id}; the request was opened from another package.");

            var newHull = Copy(hull);
            var newShip = Copy(ship);
            if (request.Layout != null) DeriveLayout(request.Layout, hull, newHull, hullKey.Value, lines);
            if (request.Visual != null) DeriveVisual(request.Visual, ship, newShip, shipKey.Value, lines);
            if (request.Lines != null) DeriveLines(request.Lines, ship, newShip, shipKey.Value, lines);

            var writes = new List<(CultRecordKey Key, Type Type, object Held, object Derived)>();
            if (!Pack(newHull).AsSpan().SequenceEqual(Pack(hull))) writes.Add((hullKey, typeof(HullData), hull, newHull));
            if (!Pack(newShip).AsSpan().SequenceEqual(Pack(ship))) writes.Add((shipKey, typeof(ShipAuthoring), ship, newShip));

            foreach (var held in request.Versions.Keys)
                if (held != hullKey.Value && held != shipKey.Value)
                    throw new InvalidOperationException($"request: versions names {held}, which this push does not write; it may name {hullKey.Value} and {shipKey.Value}.");
            foreach (var write in writes)
                if (!request.Versions.ContainsKey(write.Key.Value))
                    throw new InvalidOperationException($"request: versions has no entry for {write.Key.Value}, which this push writes.");
            changed.AddRange(writes.Select(write => write.Key.Value));

            var verdict = Verdict(path, newHull, newShip, products);
            foreach (var line in lines) output.WriteLine(line);

            var stale = writes.Where(write => !string.Equals(request.Versions[write.Key.Value], cache.GetStored(write.Key).StoredAt, StringComparison.Ordinal))
                .Select(write => write.Key.Value).ToList();
            if (stale.Count == 0 && apply && writes.Count > 0)
            {
                // One commit, every record expected to still be the instance it was loaded as: a write that lands between this
                // load and the commit is refused by the cache, whatever its storedAt said.
                var landed = cache.Commit(batch =>
                {
                    foreach (var write in writes)
                    {
                        batch.Expect(write.Key, write.Held);
                        batch.Upsert(write.Type, write.Derived, write.Key);
                    }
                });
                if (landed) cache.FlushAsync().Wait();
                else stale = writes.Select(write => write.Key.Value).ToList();
            }
            if (stale.Count > 0) return Outcome(output, "stale", changed, stale, verdict, Versions(cache, ship.Id), 2);
            return Outcome(output, apply ? "applied" : "dry-run", changed, stale, verdict, Versions(cache, ship.Id), 0);
        }
        catch (InvalidOperationException error) { return Refused(output, error.Message); }
    }

    private static string Verdict(string path, HullData hull, ShipAuthoring ship, FactionProductData[] products)
    {
        try { return "accepted: " + ShipAuthoringCommands.Judge(path, hull, ship, products); }
        catch (InvalidOperationException error) { return "refused: " + error.Message; }
    }

    private static int Outcome(TextWriter output, string outcome, List<string> changed, List<string> stale, string verdict,
        Dictionary<string, string> versions, int code)
    {
        output.WriteLine(JsonConvert.SerializeObject(new Dictionary<string, object>
        {
            ["outcome"] = outcome, ["changed"] = changed, ["stale"] = stale, ["verdict"] = verdict, ["versions"] = versions
        }));
        return code;
    }

    private static int Refused(TextWriter output, string message)
    {
        output.WriteLine(JsonConvert.SerializeObject(new Dictionary<string, object> { ["outcome"] = "refused", ["message"] = message }));
        return 1;
    }

    // ---- layout: HullData.Shape and HullData.Hardpoints --------------------------------------------------------------

    private static void DeriveLayout(LayoutSection layout, HullData held, HullData derived, string key, List<string> lines)
    {
        var grid = ParseFootprint(layout.Grid, "layout.grid");
        derived.Shape = grid;
        var oldGrid = Footprint(held.Shape);
        if (Footprint(grid) != oldGrid) lines.Add($"{key} grid: {oldGrid} -> {Footprint(grid)}");

        var stored = (held.Hardpoints ?? new List<HardpointData>()).ToDictionary(hardpoint => hardpoint.Id, StringComparer.Ordinal);
        var mounts = new HashSet<string>(StringComparer.Ordinal);
        var elements = new HashSet<string>(StringComparer.Ordinal);
        var rows = new List<HardpointData>();
        foreach (var row in layout.Hardpoints)
        {
            if (string.IsNullOrWhiteSpace(row.Mount)) throw new InvalidOperationException("layout: every hardpoint needs a mount id.");
            if (!mounts.Add(row.Mount)) throw new InvalidOperationException($"layout: mount id {row.Mount} is used twice.");
            if (!Enum.GetNames(typeof(HardpointType)).Contains(row.Kind, StringComparer.Ordinal))
                throw new InvalidOperationException($"layout: {row.Mount}: kind {row.Kind} is not a hardpoint type ({string.Join(", ", Enum.GetNames(typeof(HardpointType)))}).");
            if (row.Rotation < 0 || row.Rotation > 3) throw new InvalidOperationException($"layout: {row.Mount}: rotation {row.Rotation} is not 0..3.");
            if (!(row.Armor >= 0) || float.IsInfinity(row.Armor)) throw new InvalidOperationException($"layout: {row.Mount}: armor must be finite and at least 0.");
            if (!(row.Arc >= 0 && row.Arc <= 360)) throw new InvalidOperationException($"layout: {row.Mount}: arc must be finite, 0 to 360.");
            if (row.Cell.Length != 2 || row.Cell[0] < 0 || row.Cell[1] < 0) throw new InvalidOperationException($"layout: {row.Mount}: cell is [x, y], whole numbers of at least 0.");
            var footprint = ParseFootprint(row.Footprint, $"layout: {row.Mount}: footprint");
            HardpointData target;
            if (row.Element == "")
                target = new HardpointData();
            else
            {
                if (!elements.Add(row.Element)) throw new InvalidOperationException($"layout: element {row.Element} is used twice.");
                if (!stored.TryGetValue(row.Element, out var found))
                    throw new InvalidOperationException($"layout: element {row.Element} is not a hardpoint of this hull.");
                target = Copy(found);
            }
            target.Type = Enum.Parse<HardpointType>(row.Kind);
            target.Position = new int2(row.Cell[0], row.Cell[1]);
            target.Shape = footprint;
            target.Transform = row.Mount;
            target.Rotation = (ItemRotation)row.Rotation;
            target.Armor = row.Armor;
            target.FiringArc = row.Arc;
            rows.Add(target);
            if (row.Element == "") lines.Add($"{key} hardpoint[{row.Mount}] added");
            else Diff(lines, key, "hardpoint", row.Element, HardpointFields(stored[row.Element]), HardpointFields(target));
        }
        foreach (var gone in stored.Values.Where(hardpoint => !elements.Contains(hardpoint.Id)))
            lines.Add($"{key} hardpoint[{gone.Id}] removed");
        derived.Hardpoints = rows;
    }

    private static (string Field, string Value)[] HardpointFields(HardpointData hardpoint) => new[]
    {
        ("mount", hardpoint.Transform), ("kind", hardpoint.Type.ToString()),
        ("cell", Text(hardpoint.Position.x) + "," + Text(hardpoint.Position.y)), ("footprint", Footprint(hardpoint.Shape)),
        ("rotation", Text((int)hardpoint.Rotation)), ("armor", Text(hardpoint.Armor)), ("arc", Text(hardpoint.FiringArc))
    };

    // The footprint text of a shape: rows of # and . joined by /, the top row highest y.
    private static string Footprint(Shape shape)
    {
        var rows = new string[shape.Height];
        for (var y = 0; y < shape.Height; y++)
            rows[shape.Height - 1 - y] = new string(Enumerable.Range(0, shape.Width).Select(x => shape.Cells[x, y] ? '#' : '.').ToArray());
        return string.Join("/", rows);
    }

    private static Shape ParseFootprint(string text, string label)
    {
        var rows = text.Split('/');
        var width = rows[0].Length;
        if (width < 1 || width > MaxCells || rows.Length > MaxCells)
            throw new InvalidOperationException($"{label}: must be 1..{MaxCells} cells wide and high.");
        if (rows.Any(row => row.Length != width || row.Any(c => c != '#' && c != '.')))
            throw new InvalidOperationException($"{label}: rows of # and . of one width, separated by /.");
        if (!rows.Any(row => row.Contains('#'))) throw new InvalidOperationException($"{label}: needs an occupied cell.");
        var shape = new Shape(width, rows.Length);
        for (var y = 0; y < rows.Length; y++)
            for (var x = 0; x < width; x++)
                shape.Cells[x, y] = rows[rows.Length - 1 - y][x] == '#';
        return shape;
    }

    // ---- visual: ShipAuthoring.ModelAsset and Anchors ----------------------------------------------------------------

    private static void DeriveVisual(VisualSection visual, ShipAuthoring held, ShipAuthoring derived, string key, List<string> lines)
    {
        if (held.ModelAsset != visual.Model) lines.Add($"{key} model: {held.ModelAsset} -> {visual.Model}");
        derived.ModelAsset = visual.Model;
        var stored = (held.Anchors ?? new List<ShipAnchor>()).ToDictionary(anchor => anchor.Id, StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var rows = new List<ShipAnchor>();
        foreach (var row in visual.Anchors)
        {
            if (string.IsNullOrWhiteSpace(row.Id)) throw new InvalidOperationException("visual: every anchor needs an id.");
            if (!seen.Add(row.Id)) throw new InvalidOperationException($"visual: anchor id {row.Id} is used twice.");
            // Joint is not authored here and is carried by the anchor's identity; so is its element id.
            var target = stored.TryGetValue(row.Id, out var found) ? Copy(found) : new ShipAnchor { ElementId = row.Id };
            target.Role = row.Role;
            target.ModelNodeId = row.Node;
            target.ParentId = row.Parent;
            target.Order = row.Order;
            rows.Add(target);
            if (found == null) lines.Add($"{key} anchor[{row.Id}] added");
            else Diff(lines, key, "anchor", row.Id, AnchorFields(found), AnchorFields(target));
        }
        foreach (var gone in stored.Keys.Where(id => !seen.Contains(id))) lines.Add($"{key} anchor[{gone}] removed");
        derived.Anchors = rows;
    }

    private static (string Field, string Value)[] AnchorFields(ShipAnchor anchor) => new[]
    {
        ("role", anchor.Role), ("node", anchor.ModelNodeId), ("parent", anchor.ParentId), ("order", Text(anchor.Order))
    };

    // ---- lines: ShipAuthoring.SchematicLines -------------------------------------------------------------------------

    private static void DeriveLines(List<LineRow> request, ShipAuthoring held, ShipAuthoring derived, string key, List<string> lines)
    {
        var stored = (held.SchematicLines ?? new List<ShipPolyline>()).ToDictionary(line => line.Id, StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var rows = new List<ShipPolyline>();
        foreach (var row in request)
        {
            ShipPolyline found = null;
            if (row.Element != "")
            {
                if (!seen.Add(row.Element)) throw new InvalidOperationException($"lines: element {row.Element} is used twice.");
                if (!stored.TryGetValue(row.Element, out found))
                    throw new InvalidOperationException($"lines: element {row.Element} is not a line of this ship.");
            }
            var target = new ShipPolyline
            {
                Id = row.Element, Layer = row.Layer, Material = row.Material, Points = row.Points, Radii = row.Radii,
                Opacities = row.Opacities, Cyclic = row.Cyclic, Color = row.Color
            };
            rows.Add(target);
            if (found == null) lines.Add($"{key} line[] added");
            else Diff(lines, key, "line", row.Element, LineFields(found), LineFields(target));
        }
        foreach (var gone in stored.Keys.Where(id => !seen.Contains(id))) lines.Add($"{key} line[{gone}] removed");
        derived.SchematicLines = rows;
    }

    private static (string Field, string Value)[] LineFields(ShipPolyline line) => new[]
    {
        ("layer", line.Layer), ("material", line.Material), ("cyclic", line.Cyclic ? "true" : "false"),
        ("points", Floats(line.Points)), ("radii", Floats(line.Radii)), ("opacities", Floats(line.Opacities)), ("color", Floats(line.Color))
    };

    // ---- output ------------------------------------------------------------------------------------------------------

    private static void Diff(List<string> lines, string key, string member, string element,
        (string Field, string Value)[] before, (string Field, string Value)[] after)
    {
        for (var i = 0; i < before.Length; i++)
            if (!string.Equals(before[i].Value, after[i].Value, StringComparison.Ordinal))
                lines.Add($"{key} {member}[{element}] {before[i].Field}: {before[i].Value ?? "null"} -> {after[i].Value ?? "null"}");
    }

    private static string Text(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static string Text(float value) => value.ToString("R", CultureInfo.InvariantCulture);

    // A float list is long; the line says how many values, and the contents when the list is short.
    private static string Floats(float[] values) =>
        values == null ? null : values.Length <= 6 ? "[" + string.Join(",", values.Select(Text)) + "]" : $"{values.Length} values {Digest(values)}";

    // A cheap content tag so two long lists of equal length differ in the diff; not a revision or a version token.
    private static string Digest(float[] values)
    {
        var hash = 17;
        foreach (var value in values) hash = unchecked(hash * 31 + BitConverter.SingleToInt32Bits(value));
        return hash.ToString("x8", CultureInfo.InvariantCulture);
    }
}
