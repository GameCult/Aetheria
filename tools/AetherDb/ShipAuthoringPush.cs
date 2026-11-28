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
    internal sealed class Request
    {
        [JsonProperty("ship", Required = Required.DisallowNull)] public string Ship;
        // Checked against the stored HullType, never written.
        [JsonProperty("hullType")] public string HullType;
        [JsonProperty("versions", Required = Required.DisallowNull)] public Dictionary<string, string> Versions;
        [JsonProperty("layout")] public LayoutSection Layout;
        [JsonProperty("visual")] public VisualSection Visual;
        [JsonProperty("lines")] public List<LineRow> Lines;
        [JsonProperty("hull")] public HullSection Hull;
        [JsonProperty("products")] public List<ProductRow> Products;
    }

    [JsonObject(MemberSerialization.OptIn)]
    internal sealed class HullSection
    {
        [JsonProperty("name", Required = Required.DisallowNull)] public string Name;
        [JsonProperty("price", Required = Required.Always)] public int Price;
        [JsonProperty("mass", Required = Required.Always)] public float Mass;
        [JsonProperty("durability", Required = Required.Always)] public float Durability;
        [JsonProperty("armor", Required = Required.Always)] public float Armor;
        [JsonProperty("drag", Required = Required.Always)] public float Drag;
        [JsonProperty("topSpeed", Required = Required.AllowNull)] public float? TopSpeed;
        [JsonProperty("kind", Required = Required.AllowNull)] public string Kind;
    }

    [JsonObject(MemberSerialization.OptIn)]
    internal sealed class ProductRow
    {
        [JsonProperty("maker", Required = Required.DisallowNull)] public string Maker;
        [JsonProperty("name", Required = Required.DisallowNull)] public string Name;
        [JsonProperty("description", Required = Required.AllowNull)] public string Description;
        [JsonProperty("offered", Required = Required.Always)] public bool Offered;
    }

    [JsonObject(MemberSerialization.OptIn)]
    internal sealed class LayoutSection
    {
        [JsonProperty("grid", Required = Required.DisallowNull)] public string Grid;
        [JsonProperty("hardpoints", Required = Required.DisallowNull)] public List<HardpointRow> Hardpoints;
    }

    [JsonObject(MemberSerialization.OptIn)]
    internal sealed class HardpointRow
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
    internal sealed class VisualSection
    {
        [JsonProperty("model", Required = Required.DisallowNull)] public string Model;
        [JsonProperty("anchors", Required = Required.DisallowNull)] public List<AnchorRow> Anchors;
    }

    [JsonObject(MemberSerialization.OptIn)]
    internal sealed class AnchorRow
    {
        [JsonProperty("id", Required = Required.DisallowNull)] public string Id;
        [JsonProperty("role", Required = Required.DisallowNull)] public string Role;
        [JsonProperty("node", Required = Required.DisallowNull)] public string Node;
        [JsonProperty("parent", Required = Required.AllowNull)] public string Parent;
        [JsonProperty("order", Required = Required.Always)] public int Order;
    }

    [JsonObject(MemberSerialization.OptIn)]
    internal sealed class LineRow
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
    internal static T Copy<T>(T value) => MessagePackSerializer.Deserialize<T>(Pack(value), PackOptions);

    // The request that, pushed back unchanged, changes nothing.
    public static int Open(string path, TextWriter output)
    {
        using var cache = ShipAuthoringStore.Open(path);
        output.WriteLine(JsonConvert.SerializeObject(Opened(cache, path), Formatting.Indented));
        return 0;
    }

    // The package's authored members as a typed request: open prints it, and the builders (stats, price, kind, product) edit
    // the one section they own and hand it to Run.
    internal static Request Opened(CultCache cache, string path)
    {
        var (hull, ship, products) = ShipAuthoringStore.Load(cache, path);
        return new Request
        {
            Ship = ship.Id,
            HullType = hull.HullType.ToString(),
            Versions = Versions(cache, ship.Id),
            Layout = new LayoutSection
            {
                Grid = Footprint(hull.Shape),
                Hardpoints = (hull.Hardpoints ?? new List<HardpointData>()).Select(hardpoint => new HardpointRow
                {
                    Element = hardpoint.Id ?? "",
                    Mount = hardpoint.Transform,
                    Kind = hardpoint.Type.ToString(),
                    Cell = new[] { hardpoint.Position.x, hardpoint.Position.y },
                    Footprint = Footprint(hardpoint.Shape),
                    Rotation = (int)hardpoint.Rotation,
                    Armor = hardpoint.Armor,
                    Arc = hardpoint.FiringArc
                }).ToList()
            },
            Visual = new VisualSection
            {
                Model = ship.ModelAsset,
                Anchors = (ship.Anchors ?? new List<ShipAnchor>()).Select(anchor => new AnchorRow
                {
                    Id = anchor.Id, Role = anchor.Role, Node = anchor.ModelNodeId, Parent = anchor.ParentId, Order = anchor.Order
                }).ToList()
            },
            Lines = (ship.SchematicLines ?? new List<ShipPolyline>()).Select(line => new LineRow
            {
                Element = line.Id ?? "", Layer = line.Layer, Material = line.Material, Points = line.Points, Radii = line.Radii,
                Opacities = line.Opacities, Cyclic = line.Cyclic, Color = line.Color
            }).ToList(),
            Hull = new HullSection
            {
                Name = hull.Name, Price = hull.Price, Mass = hull.Mass, Durability = hull.Durability, Armor = hull.Armor,
                Drag = hull.Drag, TopSpeed = TopSpeed(hull), Kind = KindText(hull)
            },
            Products = products.OrderBy(product => product.Manufacturer.Key.Value, StringComparer.Ordinal).Select(product => new ProductRow
            {
                Maker = product.Manufacturer.Key.Value, Name = product.Name, Description = product.Description, Offered = !product.NotOffered
            }).ToList()
        };
    }

    // The hull, the ship and every product record the package holds, each by the store's version token.
    private static Dictionary<string, string> Versions(CultCache cache, string shipId) =>
        new[] { ShipModCatalog.HullKey(shipId), ShipModCatalog.AuthoringKey(shipId) }
            .Concat(cache.GetAll<FactionProductData>().Select(product => cache.TryGetHandle(product).Value.Key))
            .ToDictionary(key => key.Value, key => cache.GetStored(key).StoredAt, StringComparer.Ordinal);

    // The result of a push: what the print shows and the builders read.
    internal sealed class Result
    {
        public string Outcome;
        public int Code;
        public string Message;
        public List<string> Lines = new List<string>();
        public List<string> Changed = new List<string>();
        public List<string> Stale = new List<string>();
        public string Verdict;
        public Dictionary<string, string> Versions;
    }

    // Reads a request from stdin and prints what it would change; with apply, writes it. Exit 0 dry run or applied,
    // 1 refused (the file is untouched), 2 stale (a record was written since the request's open; nothing is written).
    public static int Push(string path, TextReader requestText, bool apply, TextWriter output)
    {
        Request request;
        try
        {
            request = JsonConvert.DeserializeObject<Request>(requestText.ReadToEnd(), Strict);
        }
        catch (JsonException error) { return Refused(output, "request: " + error.Message); }
        if (request == null) return Refused(output, "request: the request is empty.");
        var result = Run(path, request, apply);
        if (result.Outcome == "refused") return Refused(output, result.Message);
        foreach (var line in result.Lines) output.WriteLine(line);
        output.WriteLine(JsonConvert.SerializeObject(new Dictionary<string, object>
        {
            ["outcome"] = result.Outcome, ["changed"] = result.Changed, ["stale"] = result.Stale, ["verdict"] = result.Verdict, ["versions"] = result.Versions
        }));
        return result.Code;
    }

    // The one core: push and the builders both come here. A refusal leaves the file untouched (Code 1, Message); a stale
    // record writes nothing (Code 2). Push judges the derived package (judge); the builders do not.
    internal static Result Run(string path, Request request, bool apply, bool judge = true)
    {
        var result = new Result();
        try
        {
            using var cache = ShipAuthoringStore.Open(path, writable: apply);
            var (hull, ship, products) = ShipAuthoringStore.Load(cache, path);
            var hullKey = ShipModCatalog.HullKey(ship.Id);
            var shipKey = ShipModCatalog.AuthoringKey(ship.Id);
            if (!string.Equals(request.Ship, ship.Id, StringComparison.Ordinal))
                throw new InvalidOperationException($"request: ship is not {ship.Id}; the request was opened from another package.");
            if (request.HullType != null && !string.Equals(request.HullType, hull.HullType.ToString(), StringComparison.Ordinal))
                throw new InvalidOperationException($"request: hullType is {request.HullType}; this package's hull is a {hull.HullType}.");

            var lines = result.Lines;
            var newHull = Copy(hull);
            var newShip = Copy(ship);
            if (request.Layout != null) DeriveLayout(request.Layout, hull, newHull, hullKey.Value, lines);
            if (request.Visual != null) DeriveVisual(request.Visual, ship, newShip, shipKey.Value, lines);
            if (request.Lines != null) DeriveLines(request.Lines, ship, newShip, shipKey.Value, lines);
            if (request.Hull != null) DeriveHull(request.Hull, hull, newHull, hullKey.Value, ship.Id, lines);
            var productWrites = request.Products != null ? DeriveProducts(request.Products, products, newHull, ship.Id, lines) : new List<Write>();

            var writes = new List<Write>();
            if (!Pack(newHull).AsSpan().SequenceEqual(Pack(hull))) writes.Add(new Write(hullKey, typeof(HullData), hull, newHull));
            if (!Pack(newShip).AsSpan().SequenceEqual(Pack(ship))) writes.Add(new Write(shipKey, typeof(ShipAuthoring), ship, newShip));
            writes.AddRange(productWrites);

            var productPrefix = "mod-product:" + ship.Id + ":";
            foreach (var held in request.Versions.Keys)
                if (held != hullKey.Value && held != shipKey.Value && !held.StartsWith(productPrefix, StringComparison.Ordinal))
                    throw new InvalidOperationException($"request: versions names {held}, which this push does not write; it may name {hullKey.Value}, {shipKey.Value} and {productPrefix}<maker>.");
            foreach (var write in writes)
                if ((write.Key.Equals(hullKey) || write.Key.Equals(shipKey)) && !request.Versions.ContainsKey(write.Key.Value))
                    throw new InvalidOperationException($"request: versions has no entry for {write.Key.Value}, which this push writes.");
            result.Changed.AddRange(writes.Select(write => write.Key.Value));

            // The products Judge sees: the stored ones with each product write applied.
            var written = productWrites.ToDictionary(write => write.Key.Value, StringComparer.Ordinal);
            var judged = products.Where(product => !written.ContainsKey(ShipModCatalog.ProductKey(ship.Id, product.Manufacturer.Key).Value))
                .Concat(productWrites.Where(write => write.Derived != null).Select(write => (FactionProductData)write.Derived)).ToArray();
            // A builder reports its own line and reads no verdict, so a plain stats or price write never opens the shipped catalog.
            if (judge) result.Verdict = Verdict(path, newHull, newShip, judged);

            // A record the request carries a version for is expected as that instance; one it carries none for (a product it
            // did not open) is expected absent.
            bool Stale(Write write) => request.Versions.TryGetValue(write.Key.Value, out var version)
                ? write.Held == null || !string.Equals(version, cache.GetStored(write.Key).StoredAt, StringComparison.Ordinal)
                : write.Held != null;
            var stale = writes.Where(Stale).Select(write => write.Key.Value).ToList();
            if (stale.Count == 0 && apply && writes.Count > 0)
            {
                var landed = Land(cache, writes);
                if (landed) cache.FlushAsync().Wait();
                else stale = writes.Select(write => write.Key.Value).ToList();
            }
            result.Stale = stale;
            result.Versions = Versions(cache, ship.Id);
            result.Code = stale.Count > 0 ? 2 : 0;
            result.Outcome = stale.Count > 0 ? "stale" : apply ? "applied" : "dry-run";
            return result;
        }
        catch (InvalidOperationException error) { return new Result { Outcome = "refused", Code = 1, Message = error.Message }; }
    }

    // A record to write: the instance the cache holds (null: the key is absent, an add) and the copy derived from it
    // (null: a removal).
    internal readonly record struct Write(CultRecordKey Key, Type Type, object Held, object Derived);

    // One commit, every record expected to still be the instance it was loaded as (or absent): a write that lands between the
    // load and this commit replaces that instance and is refused here (false), never overwritten.
    internal static bool Land(CultCache cache, IReadOnlyList<Write> writes)
    {
        try
        {
            return cache.Commit(batch =>
            {
                foreach (var write in writes)
                {
                    batch.Expect(write.Key, write.Held);
                    if (write.Derived == null) batch.Remove(write.Key);
                    else batch.Upsert(write.Type, write.Derived, write.Key);
                }
            });
        }
        // Expect refuses an instance the cache no longer holds at the key: that write landed after the load.
        catch (ArgumentException error) when (error.ParamName == "current") { return false; }
    }

    private static string Verdict(string path, HullData hull, ShipAuthoring ship, FactionProductData[] products)
    {
        try { return "accepted: " + ShipAuthoringCommands.Judge(path, hull, ship, products); }
        // The judgment's own refusals, and a model whose JSON chunk does not parse; anything else is a fault and surfaces.
        catch (Exception error) when (error is InvalidOperationException or JsonException) { return "refused: " + error.Message; }
    }

    private static int Refused(TextWriter output, string message)
    {
        output.WriteLine(JsonConvert.SerializeObject(new Dictionary<string, object> { ["outcome"] = "refused", ["message"] = message }));
        return 1;
    }

    // ---- hull: name, price, stats, top speed, station kind ------------------------------------------------------------

    // The one VelocityLimit's top speed when the hull has exactly one with a fixed speed (Min equal to Max), else null.
    private static float? TopSpeed(HullData hull)
    {
        var limits = (hull.Behaviors ?? new List<BehaviorData>()).OfType<VelocityLimitData>().ToArray();
        return limits.Length == 1 && limits[0].TopSpeed.Min == limits[0].TopSpeed.Max ? limits[0].TopSpeed.Max : null;
    }

    private static string KindText(HullData hull) => hull.Kind.IsSet() ? hull.Kind.Key.Value : null;

    private static void DeriveHull(HullSection section, HullData held, HullData derived, string key, string shipId, List<string> lines)
    {
        if (section.Price < 0) throw new InvalidOperationException("hull: price must be a whole number of at least 0.");
        foreach (var (field, value) in new[] { ("mass", section.Mass), ("durability", section.Durability), ("armor", section.Armor), ("drag", section.Drag) })
            if (!(value >= 0) || float.IsInfinity(value)) throw new InvalidOperationException($"hull: {field} must be finite and at least 0.");
        if (string.IsNullOrWhiteSpace(section.Name)) throw new InvalidOperationException("hull: name must not be blank.");
        derived.Name = section.Name;
        derived.Price = section.Price;
        derived.Mass = section.Mass;
        derived.Durability = section.Durability;
        derived.Armor = section.Armor;
        derived.Drag = section.Drag;
        if (section.TopSpeed is { } speed)
        {
            if (!float.IsFinite(speed) || speed < 0) throw new InvalidOperationException("hull: topSpeed must be null, or finite and at least 0.");
            var limits = (derived.Behaviors ??= new List<BehaviorData>()).OfType<VelocityLimitData>().ToArray();
            if (limits.Length > 1) throw new InvalidOperationException($"{shipId}: --top-speed needs at most one VelocityLimit on the hull; it has {limits.Length}.");
            // A hull made without --like has none, and this is its only way to get one: validate refuses a ship without a top speed.
            if (limits.Length == 0)
            {
                limits = new[] { new VelocityLimitData() };
                derived.Behaviors.Add(limits[0]);
            }
            // A fixed top speed, as every shipped hull authors one (Min equal to Max); its terms are kept.
            limits[0].TopSpeed.Min = limits[0].TopSpeed.Max = speed;
        }
        derived.Kind = string.IsNullOrEmpty(section.Kind) ? default : new CultRecordRef<StationKindData>(new CultRecordKey(section.Kind));
        // A kind is judged whenever the request or the stored hull names one, so an invalid stored kind is never passed as fine; a
        // kindless draft that stays kindless is not, so it still takes stats and price (and needs no catalog).
        if (derived.Kind.IsSet() || held.Kind.IsSet())
            using (var catalog = AetherDb.Open().Cache)
                if (StationKinds.HullKindFault(catalog, derived) is { } fault) throw new InvalidOperationException($"{shipId}: {fault}");
        FlatDiff(lines, key, HullFields(held), HullFields(derived));
    }

    private static (string Field, string Value)[] HullFields(HullData hull) => new[]
    {
        ("name", hull.Name), ("price", Text(hull.Price)), ("mass", Text(hull.Mass)), ("durability", Text(hull.Durability)),
        ("armor", Text(hull.Armor)), ("drag", Text(hull.Drag)), ("topSpeed", TopSpeed(hull) is { } speed ? Text(speed) : null), ("kind", KindText(hull))
    };

    // ---- products: one FactionProductData per maker ------------------------------------------------------------------

    private static List<Write> DeriveProducts(List<ProductRow> request, FactionProductData[] stored, HullData hull, string shipId, List<string> lines)
    {
        var byMaker = stored.ToDictionary(product => product.Manufacturer.Key.Value, StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var writes = new List<Write>();
        foreach (var row in request)
        {
            if (string.IsNullOrWhiteSpace(row.Name)) throw new InvalidOperationException($"products: {row.Maker}: a product needs a name.");
            // A maker that is a stored product's own key names it; any other is a shipped faction by key, name or short name.
            FactionProductData held;
            CultRecordKey maker;
            if (byMaker.TryGetValue(row.Maker, out held)) maker = held.Manufacturer.Key;
            else
            {
                maker = ShipAuthoringCommands.ShippedFaction(row.Maker).Key;
                byMaker.TryGetValue(maker.Value, out held);
            }
            if (!seen.Add(maker.Value)) throw new InvalidOperationException($"products: maker {row.Maker} names {maker.Value}, which another row names too.");
            var key = ShipModCatalog.ProductKey(shipId, maker);
            var derived = held != null ? Copy(held) : new FactionProductData
            {
                Design = new CultRecordRef<CraftedItemData>(ShipModCatalog.HullKey(shipId)),
                Manufacturer = new CultRecordRef<Faction>(maker),
                // A hull stored with no roles (Zenith, so a station made --like it) decodes with a null list.
                Roles = (hull.Roles ?? new List<ItemRole>()).Select(role => new ProductRole { Role = role.Name }).ToList()
            };
            derived.Name = row.Name;
            derived.Description = row.Description;
            derived.NotOffered = !row.Offered;
            if (held == null)
            {
                lines.Add($"{key.Value} added");
                writes.Add(new Write(key, typeof(FactionProductData), null, derived));
            }
            else if (!Pack(derived).AsSpan().SequenceEqual(Pack(held)))
            {
                FlatDiff(lines, key.Value, ProductFields(held), ProductFields(derived));
                writes.Add(new Write(key, typeof(FactionProductData), held, derived));
            }
        }
        foreach (var gone in stored.Where(product => !seen.Contains(product.Manufacturer.Key.Value)))
        {
            var key = ShipModCatalog.ProductKey(shipId, gone.Manufacturer.Key);
            lines.Add($"{key.Value} removed");
            writes.Add(new Write(key, typeof(FactionProductData), gone, null));
        }
        return writes.OrderBy(write => write.Key.Value, StringComparer.Ordinal).ToList();
    }

    private static (string Field, string Value)[] ProductFields(FactionProductData product) => new[]
    {
        ("name", product.Name), ("description", product.Description), ("offered", product.NotOffered ? "false" : "true")
    };

    private static void FlatDiff(List<string> lines, string key, (string Field, string Value)[] before, (string Field, string Value)[] after)
    {
        for (var i = 0; i < before.Length; i++)
            if (!string.Equals(before[i].Value, after[i].Value, StringComparison.Ordinal))
                lines.Add($"{key} {before[i].Field}: {before[i].Value ?? "null"} -> {after[i].Value ?? "null"}");
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
