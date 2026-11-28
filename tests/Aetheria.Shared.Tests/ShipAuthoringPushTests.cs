using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MessagePack;
using GameCult.Caching;
using GameCult.Caching.MessagePack;
using Newtonsoft.Json.Linq;
using Xunit;

// ship-authoring open and push (AetherDb): one request vocabulary, dry run by default, apply under a per-record version
// check that refuses a stale record. These call ShipAuthoringPush directly with readers and writers, as the add-on's pipe does.
[Collection("process working directory")]
public sealed class ShipAuthoringPushTests : IDisposable
{
    private readonly TempDirectory _root = new TempDirectory();
    private readonly string _previous = Environment.CurrentDirectory;
    private string Mods => Path.Combine(_root.Path, "GameData", "Mods");

    public ShipAuthoringPushTests()
    {
        Directory.CreateDirectory(Mods);
        Environment.CurrentDirectory = _root.Path;
    }

    public void Dispose()
    {
        Environment.CurrentDirectory = _previous;
        _root.Dispose();
    }

    // A valid package with an element-id-carrying thruster row (Row set), a gun, and the fixture's line.
    private string Package(string id = "mod.skiff") =>
        Path.Combine(ShipFixture.WritePackage(Mods, id, tweak: ship => ship.Hull.Hardpoints[0].Row = "port-row"), "ship.cc");

    private static string Open(string path)
    {
        var output = new StringWriter();
        Assert.Equal(0, ShipAuthoringPush.Open(path, output));
        return output.ToString();
    }

    private static (int Code, string[] Lines, JObject Outcome) Push(string path, string request, bool apply = false)
    {
        var output = new StringWriter();
        var code = ShipAuthoringPush.Push(path, new StringReader(request), apply, output);
        var lines = output.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(line => line.TrimEnd('\r')).ToArray();
        return (code, lines.Take(lines.Length - 1).ToArray(), Parse(lines.Last()));
    }

    // Versions are strings; the default reader would turn them into dates and write them back reformatted.
    private static JObject Parse(string text)
    {
        using var reader = new Newtonsoft.Json.JsonTextReader(new StringReader(text)) { DateParseHandling = Newtonsoft.Json.DateParseHandling.None };
        return JObject.Load(reader);
    }

    private static string Edit(string request, Action<JObject> change)
    {
        var json = Parse(request);
        change(json);
        return json.ToString();
    }

    private static string[] Listing(string path) => Directory.GetFiles(Path.GetDirectoryName(path)).Select(Path.GetFileName).OrderBy(name => name, StringComparer.Ordinal).ToArray();

    private static byte[] Pack<T>(T value) => MessagePackSerializer.Serialize(value, CultDocumentMessagePackSerialization.OptionsFor(typeof(HullData).Assembly));

    private static JToken Hardpoint(JObject request, string mount) => request["layout"]["hardpoints"].First(row => (string)row["mount"] == mount);

    // Moves the gun, retargets an anchor and drops the line: the three changes the dry run and the apply must agree on.
    private static string ThreeChanges(string request) => Edit(request, json =>
    {
        Hardpoint(json, "gun")["cell"] = new JArray(1, 1);
        json["visual"]["anchors"].First(anchor => (string)anchor["id"] == "map")["node"] = "collider";
        json["lines"] = new JArray();
    });

    [Fact]
    public void OpenThenPushIsNoChange()
    {
        var copy = Path.Combine(_root.Path, "nakovalnya");
        Directory.CreateDirectory(copy);
        foreach (var file in Directory.GetFiles(Path.Combine(RunStartTests.FindRepoRoot(), "GameData", "Mods", "aetheria.nakovalnya")))
            File.Copy(file, Path.Combine(copy, Path.GetFileName(file)));
        foreach (var path in new[] { Package(), Path.Combine(copy, "ship.cc") })
        {
            var request = Open(path);
            var before = File.ReadAllBytes(path);
            var files = Listing(path);
            foreach (var apply in new[] { false, true })
            {
                var (code, changes, outcome) = Push(path, request, apply);
                Assert.Equal(0, code);
                Assert.Empty(changes);
                Assert.Equal(apply ? "applied" : "dry-run", (string)outcome["outcome"]);
                Assert.Empty(outcome["changed"]);
                Assert.Equal(before, File.ReadAllBytes(path));
                Assert.Equal(files, Listing(path));
            }
        }
    }

    [Fact]
    public void DryRunWritesNothing()
    {
        var path = Package();
        var request = ThreeChanges(Open(path));
        var before = File.ReadAllBytes(path);
        var files = Listing(path);
        var (code, changes, outcome) = Push(path, request);
        Assert.True(code == 0, outcome.ToString());
        Assert.Equal("dry-run", (string)outcome["outcome"]);
        Assert.Equal(3, changes.Length);
        Assert.Contains(changes, line => line.StartsWith("mod-hull:mod.skiff hardpoint[") && line.EndsWith("cell: 0,0 -> 1,1"));
        Assert.Contains(changes, line => line == "mod-ship:mod.skiff anchor[map] node: map -> collider");
        Assert.Contains(changes, line => line.StartsWith("mod-ship:mod.skiff line[") && line.EndsWith("removed"));
        Assert.Equal(before, File.ReadAllBytes(path));
        Assert.Equal(files, Listing(path));
    }

    // An anchor the request adds is a whole anchor: the Id is the one the validator and the node binding read.
    [Fact]
    public void AnAddedAnchorKeepsItsId()
    {
        var path = Package();
        var request = Edit(Open(path), json => ((JArray)json["visual"]["anchors"]).Add(
            new JObject { ["id"] = "extra", ["role"] = "map-icon", ["node"] = "extra", ["parent"] = null, ["order"] = 0 }));
        var (code, changes, outcome) = Push(path, request, apply: true);
        Assert.True(code == 0, outcome.ToString());
        Assert.Contains("mod-ship:mod.skiff anchor[extra] added", changes);
        Assert.Contains(Parse(Open(path))["visual"]["anchors"], anchor => (string)anchor["id"] == "extra");
    }

    [Fact]
    public void ApplyWritesTheDryRunResult()
    {
        var path = Package();
        var opened = Open(path);
        var request = ThreeChanges(opened);
        var (_, dryChanges, _) = Push(path, request);
        var (code, changes, outcome) = Push(path, request, apply: true);
        Assert.Equal(0, code);
        Assert.Equal("applied", (string)outcome["outcome"]);
        Assert.Equal(dryChanges, changes);

        var reopened = Parse(Open(path));
        var gun = Hardpoint(reopened, "gun");
        Assert.Equal(new[] { 1, 1 }, gun["cell"].Select(cell => (int)cell));
        Assert.Equal((string)Hardpoint(Parse(opened), "gun")["element"], (string)gun["element"]);
        Assert.Equal("collider", (string)reopened["visual"]["anchors"].First(anchor => (string)anchor["id"] == "map")["node"]);
        Assert.Empty(reopened["lines"]);
        // The thruster was not touched: its element id and the Row no section authors survive.
        var thruster = ShipAuthoringStore.Load(path).Hull.Hardpoints.Single(hardpoint => hardpoint.Transform == "thruster.port");
        Assert.Equal("port-row", thruster.Row);
        Assert.Equal((string)Hardpoint(Parse(opened), "thruster.port")["element"], thruster.Id);
        Assert.Equal(reopened["versions"].ToString(), outcome["versions"].ToString());
        Assert.Equal(new[] { "mod-hull:mod.skiff", "mod-ship:mod.skiff" }, outcome["changed"].Select(key => (string)key).OrderBy(key => key, StringComparer.Ordinal));
    }

    [Fact]
    public void StaleVersionRefused()
    {
        var path = Package();
        var request = ThreeChanges(Open(path));
        // stats is a builder over the push core: it writes the hull record alone, so the ship record is still current.
        var stats = ConsoleProbe.Capture(() => ShipAuthoringCommands.Run(new[] { "stats", path, "--mass", "1" }), out _, out var statsError);
        Assert.True(stats == 0, statsError);
        var after = File.ReadAllBytes(path);
        var (code, _, outcome) = Push(path, request, apply: true);
        Assert.Equal(2, code);
        Assert.Equal("stale", (string)outcome["outcome"]);
        Assert.Equal(new[] { "mod-hull:mod.skiff" }, outcome["stale"].Select(key => (string)key).OrderBy(key => key, StringComparer.Ordinal));
        Assert.Equal(after, File.ReadAllBytes(path));
    }

    [Fact]
    public void ElementMatching()
    {
        var path = Package();
        var request = Open(path);
        var thruster = (string)Hardpoint(Parse(request), "thruster.port")["element"];

        var added = Edit(request, json =>
        {
            var row = (JObject)Hardpoint(json, "gun").DeepClone();
            row["element"] = "";
            row["mount"] = "gun.second";
            row["cell"] = new JArray(1, 1);
            ((JArray)json["layout"]["hardpoints"]).Add(row);
            // The one with no element is the only change that mints; the gun's own row stays.
        });
        Assert.Equal(0, Push(path, added, apply: true).Code);
        var hull = ShipAuthoringStore.Load(path).Hull;
        Assert.Equal(3, hull.Hardpoints.Count);
        var second = hull.Hardpoints.Single(hardpoint => hardpoint.Transform == "gun.second");
        Assert.False(string.IsNullOrEmpty(second.Id));
        Assert.Equal(3, hull.Hardpoints.Select(hardpoint => hardpoint.Id).Distinct().Count());

        var removed = Edit(Open(path), json => ((JArray)json["layout"]["hardpoints"]).First(row => (string)row["mount"] == "gun.second").Remove());
        Assert.Equal(0, Push(path, removed, apply: true).Code);
        Assert.DoesNotContain(ShipAuthoringStore.Load(path).Hull.Hardpoints, hardpoint => hardpoint.Transform == "gun.second");

        var renamed = Edit(Open(path), json => Hardpoint(json, "thruster.port")["mount"] = "thruster.renamed");
        var (code, changes, _) = Push(path, renamed, apply: true);
        Assert.Equal(0, code);
        Assert.Equal($"mod-hull:mod.skiff hardpoint[{thruster}] mount: thruster.port -> thruster.renamed", Assert.Single(changes));
        Assert.Equal(thruster, ShipAuthoringStore.Load(path).Hull.Hardpoints.Single(hardpoint => hardpoint.Transform == "thruster.renamed").Id);

        var before = File.ReadAllBytes(path);
        var unknown = Edit(Open(path), json => Hardpoint(json, "gun")["element"] = "000000000000");
        var refused = Push(path, unknown, apply: true);
        Assert.Equal(1, refused.Code);
        Assert.Contains("000000000000", (string)refused.Outcome["message"]);
        Assert.Equal(before, File.ReadAllBytes(path));
    }

    [Fact]
    public void StructuralRefusals()
    {
        var path = Package();
        var open = Open(path);
        var cases = new (string Name, string Request, string Names)[]
        {
            ("unknown key", Edit(open, json => json["surprise"] = 1), "surprise"),
            ("missing version", Edit(open, json => { Hardpoint(json, "gun")["armor"] = 5; ((JObject)json["versions"]).Remove("mod-hull:mod.skiff"); }), "mod-hull:mod.skiff"),
            ("missing ship", Edit(open, json => json.Remove("ship")), "ship"),
            ("missing hardpoint key", Edit(open, json => ((JObject)Hardpoint(json, "gun")).Remove("arc")), "arc"),
            ("grid 33 wide", Edit(open, json => json["layout"]["grid"] = new string('#', 33)), "grid"),
            ("grid 33 high", Edit(open, json => json["layout"]["grid"] = string.Join("/", Enumerable.Repeat("#", 33))), "grid"),
            ("empty grid", Edit(open, json => json["layout"]["grid"] = ".."), "grid"),
            ("ragged grid", Edit(open, json => json["layout"]["grid"] = "##/#"), "grid"),
            ("bad footprint", Edit(open, json => Hardpoint(json, "gun")["footprint"] = "#x"), "footprint"),
            ("empty footprint", Edit(open, json => Hardpoint(json, "gun")["footprint"] = "."), "footprint"),
            ("unknown kind", Edit(open, json => Hardpoint(json, "gun")["kind"] = "Laser"), "kind"),
            ("rotation 4", Edit(open, json => Hardpoint(json, "gun")["rotation"] = 4), "rotation"),
            ("negative armor", Edit(open, json => Hardpoint(json, "gun")["armor"] = -1), "armor"),
            ("arc 361", Edit(open, json => Hardpoint(json, "gun")["arc"] = 361), "arc"),
            ("duplicate mount", Edit(open, json => Hardpoint(json, "gun")["mount"] = "thruster.port"), "thruster.port"),
            ("duplicate anchor", Edit(open, json => json["visual"]["anchors"][1]["id"] = "map"), "map"),
            ("empty anchor id", Edit(open, json => json["visual"]["anchors"][1]["id"] = ""), "anchor"),
            ("wrong ship", Edit(open, json => json["ship"] = "mod.other"), "mod.skiff"),
        };
        foreach (var (name, request, names) in cases)
        {
            var before = File.ReadAllBytes(path);
            var files = Listing(path);
            foreach (var apply in new[] { false, true })
            {
                var (code, _, outcome) = Push(path, request, apply);
                Assert.True(code == 1, $"{name}: exit {code}");
                Assert.Equal("refused", (string)outcome["outcome"]);
                Assert.True(((string)outcome["message"]).Contains(names, StringComparison.Ordinal), $"{name}: {outcome["message"]}");
                Assert.Equal(before, File.ReadAllBytes(path));
                Assert.Equal(files, Listing(path));
            }
        }
    }

    [Fact]
    public void VerdictIsNotAGate()
    {
        var path = Package();
        // A draft with rows and no anchors: the thruster and the gun are mounts the model cannot bind.
        var request = Edit(Open(path), json => json["visual"]["anchors"] = new JArray());
        var (code, _, outcome) = Push(path, request, apply: true);
        Assert.Equal(0, code);
        Assert.Equal("applied", (string)outcome["outcome"]);
        var verdict = (string)outcome["verdict"];
        Assert.StartsWith("refused: ", verdict);
        var validated = ConsoleProbe.Capture(() => ShipAuthoringCommands.Run(new[] { "validate", path }), out _, out var error);
        Assert.Equal(1, validated);
        Assert.Equal(error.Trim(), verdict.Substring("refused: ".Length));
        Assert.Empty(ShipAuthoringStore.Load(path).Visual.Anchors);
    }

    [Fact]
    public void UnauthoredMembersCarried()
    {
        var path = Package();
        var (hull, ship, _) = ShipAuthoringStore.Load(path);
        var options = CultDocumentMessagePackSerialization.OptionsFor(typeof(HullData).Assembly);
        byte[] Pack<T>(T value) => MessagePackSerializer.Serialize(value, options);
        var carried = new object[] { hull.Mass, hull.Price, hull.Name, hull.Durability, hull.Armor, hull.Drag, hull.Roles, hull.Behaviors, ship.Joints };
        var before = carried.Select(member => Convert.ToHexString(Pack(member))).ToArray();

        var request = Edit(Open(path), json => Hardpoint(json, "gun")["armor"] = 5);
        Assert.Equal(0, Push(path, request, apply: true).Code);
        var (hull2, ship2, _) = ShipAuthoringStore.Load(path);
        var after = new object[] { hull2.Mass, hull2.Price, hull2.Name, hull2.Durability, hull2.Armor, hull2.Drag, hull2.Roles, hull2.Behaviors, ship2.Joints }
            .Select(member => Convert.ToHexString(Pack(member))).ToArray();
        Assert.Equal(before, after);
        Assert.Equal(5f, hull2.Hardpoints.Single(hardpoint => hardpoint.Transform == "gun").Armor);
        Assert.Equal("port-row", hull2.Hardpoints.Single(hardpoint => hardpoint.Transform == "thruster.port").Row);
    }

    // The Expect half of the compare-exchange: a write that lands between the load and the commit, through whatever
    // path, replaces the instance the commit expects. The commit is refused and the concurrent write is what the store holds.
    [Fact]
    public void ConcurrentWriteBetweenLoadAndCommitIsRefused()
    {
        var path = Package();
        using (var cache = ShipAuthoringStore.Open(path, writable: true))
        {
            var (hull, _, _) = ShipAuthoringStore.Load(cache, path);
            var key = ShipModCatalog.HullKey("mod.skiff");
            var options = CultDocumentMessagePackSerialization.OptionsFor(typeof(HullData).Assembly);
            HullData Mutated(float mass)
            {
                var copy = MessagePackSerializer.Deserialize<HullData>(MessagePackSerializer.Serialize(hull, options), options);
                copy.Mass = mass;
                return copy;
            }
            var mine = Mutated(111);
            var concurrent = Mutated(222);
            cache.UpsertAsync(typeof(HullData), concurrent, key).GetAwaiter().GetResult();
            var writes = new[] { new ShipAuthoringPush.Write(key, typeof(HullData), hull, mine) };
            Assert.False(ShipAuthoringPush.Land(cache, writes));
            Assert.Same(concurrent, cache.Get(key));
            cache.FlushAsync().Wait();
        }
        Assert.Equal(222f, ShipAuthoringStore.Load(path).Hull.Mass);
    }

    [Theory]
    [InlineData("")]
    [InlineData("null")]
    [InlineData("   ")]
    public void EmptyRequestIsRefusedWithAnOutcome(string body)
    {
        var path = Package();
        var before = File.ReadAllBytes(path);
        var files = Listing(path);
        foreach (var apply in new[] { false, true })
        {
            var (code, _, outcome) = Push(path, body, apply);
            Assert.Equal(1, code);
            Assert.Equal("refused", (string)outcome["outcome"]);
            Assert.Contains("request", (string)outcome["message"]);
            Assert.Equal(before, File.ReadAllBytes(path));
            Assert.Equal(files, Listing(path));
        }
    }

    // A model file that does not read as a GLB is the package's fault, not the request's: the verdict reports it, the
    // draft still applies, and nothing escapes.
    [Theory]
    [InlineData("badjson")]
    [InlineData("truncated")]
    [InlineData("notglb")]
    public void CorruptModelIsAVerdictNotACrash(string kind)
    {
        var path = Path.Combine(ShipFixture.WritePackage(Mods, "mod.skiff", tweak: ship => ship.Hull.Roles = new List<ItemRole> { new ItemRole { Name = "frame" } }), "ship.cc");
        var model = Path.Combine(Path.GetDirectoryName(path), "skiff.glb");
        var good = File.ReadAllBytes(model);
        // Control: the same request on the intact package is accepted, so the refusal below is the model's.
        var intact = Push(path, Edit(Open(path), json => Hardpoint(json, "gun")["armor"] = 6), apply: false);
        Assert.True(((string)intact.Outcome["verdict"]).StartsWith("accepted: records and model"), (string)intact.Outcome["verdict"]);
        File.WriteAllBytes(model, kind switch
        {
            "badjson" => ShipFixture.Glb("{not json"),
            "truncated" => good.Take(30).ToArray(),
            _ => new byte[] { 1, 2, 3 }
        });
        var request = Edit(Open(path), json => Hardpoint(json, "gun")["armor"] = 7);
        var (code, _, outcome) = Push(path, request, apply: true);
        Assert.Equal(0, code);
        Assert.Equal("applied", (string)outcome["outcome"]);
        Assert.StartsWith("refused: ", (string)outcome["verdict"]);
        Assert.Equal(7f, ShipAuthoringStore.Load(path).Hull.Hardpoints.Single(hardpoint => hardpoint.Transform == "gun").Armor);
    }

    // ---- hull and products --------------------------------------------------------------------------------------------

    // A shipped catalog the commands find from the working directory: factions to make products for (TWN is ambiguous)
    // and one station kind.
    private (CultRecordKey Acme, CultRecordKey TwinA) Catalog()
    {
        var shipped = Path.Combine(_root.Path, "GameData", "Aetheria.cc");
        Directory.CreateDirectory(Path.GetDirectoryName(shipped));
        using var cache = AetheriaStores.Open(shipped, catalogWritable: true);
        cache.Upsert(new VerseGrammar { Revision = 1 });
        var acme = cache.Upsert(new Faction { Name = "Acme", ShortName = "ACM" }).Key;
        cache.Upsert(new Faction { Name = "Rival", ShortName = "RVL" });
        var twin = cache.Upsert(new Faction { Name = "Twin A", ShortName = "TWN" }).Key;
        cache.Upsert(new Faction { Name = "Twin B", ShortName = "TWN" });
        cache.UpsertAsync(typeof(StationKindData), new StationKindData { Name = "Depot" }, StationKindData.KeyOf("depot")).GetAwaiter().GetResult();
        cache.FlushAsync().Wait();
        return (acme, twin);
    }

    private static (int Code, string Output, string Error) Command(params string[] args)
    {
        var code = ConsoleProbe.Capture(() => ShipAuthoringCommands.Run(args), out var output, out var error);
        return (code, output, error);
    }

    private static JToken Hull(JObject request) => request["hull"];

    private static void RefusedUntouched(string path, string request, string names)
    {
        var before = File.ReadAllBytes(path);
        var files = Listing(path);
        foreach (var apply in new[] { false, true })
        {
            var (code, _, outcome) = Push(path, request, apply);
            Assert.True(code == 1, $"{names}: exit {code}: {outcome}");
            Assert.Equal("refused", (string)outcome["outcome"]);
            Assert.True(((string)outcome["message"]).Contains(names, StringComparison.Ordinal), $"{names}: {outcome["message"]}");
            Assert.Equal(before, File.ReadAllBytes(path));
            Assert.Equal(files, Listing(path));
        }
    }

    [Fact]
    public void HullFieldsRoundTripAndApply()
    {
        var path = Package();
        var opened = Open(path);
        var request = Edit(opened, json => { Hull(json)["name"] = "Renamed"; Hull(json)["price"] = 777; Hull(json)["mass"] = 4321.5f; });
        var before = File.ReadAllBytes(path);
        var (code, changes, outcome) = Push(path, request);
        Assert.Equal(0, code);
        Assert.Equal(3, changes.Length);
        Assert.Contains(changes, line => line == "mod-hull:mod.skiff name: Skiff -> Renamed");
        Assert.Contains(changes, line => line.StartsWith("mod-hull:mod.skiff price: ") && line.EndsWith(" -> 777"));
        Assert.Contains(changes, line => line.StartsWith("mod-hull:mod.skiff mass: ") && line.EndsWith(" -> 4321.5"));
        Assert.Equal(new[] { "mod-hull:mod.skiff" }, outcome["changed"].Select(key => (string)key));
        Assert.Equal(before, File.ReadAllBytes(path));

        var (applyCode, applied, applyOutcome) = Push(path, request, apply: true);
        Assert.Equal(0, applyCode);
        Assert.Equal(changes, applied);
        var hull = ShipAuthoringStore.Load(path).Hull;
        Assert.Equal(("Renamed", 777, 4321.5f), (hull.Name, hull.Price, hull.Mass));
        var reopened = Parse(Open(path));
        Assert.Equal("Renamed", (string)Hull(reopened)["name"]);
        Assert.Equal(777, (int)Hull(reopened)["price"]);
        // The ship record was not written: its version token is the one open printed.
        Assert.Equal((string)Parse(opened)["versions"]["mod-ship:mod.skiff"], (string)reopened["versions"]["mod-ship:mod.skiff"]);
        Assert.NotEqual((string)Parse(opened)["versions"]["mod-hull:mod.skiff"], (string)reopened["versions"]["mod-hull:mod.skiff"]);

        RefusedUntouched(path, Edit(Open(path), json => Hull(json)["name"] = ""), "name");
        RefusedUntouched(path, Edit(Open(path), json => Hull(json)["name"] = "  "), "name");
        RefusedUntouched(path, Edit(Open(path), json => Hull(json)["price"] = -1), "price");
        RefusedUntouched(path, Edit(Open(path), json => Hull(json)["price"] = 1.5), "price");
        RefusedUntouched(path, Edit(Open(path), json => Hull(json)["mass"] = -1), "mass");
        RefusedUntouched(path, Edit(Open(path), json => Hull(json)["drag"] = -.5), "drag");
        RefusedUntouched(path, Edit(Open(path), json => Hull(json)["topSpeed"] = -1), "topSpeed");
        RefusedUntouched(path, Edit(Open(path), json => ((JObject)Hull(json)).Remove("armor")), "armor");
    }

    [Fact]
    public void TopSpeedRules()
    {
        var path = Package();
        var behaviors = Pack(ShipAuthoringStore.Load(path).Hull.Behaviors);
        var opened = Parse(Open(path));
        Assert.Equal(JTokenType.Float, Hull(opened)["topSpeed"].Type);

        // Null leaves the behaviors byte-equal.
        var (code, changes, _) = Push(path, Edit(Open(path), json => { Hull(json)["topSpeed"] = null; Hull(json)["mass"] = 9; }), apply: true);
        Assert.Equal(0, code);
        Assert.Single(changes);
        Assert.Equal(behaviors, Pack(ShipAuthoringStore.Load(path).Hull.Behaviors));

        // A number sets Min = Max on the one VelocityLimit.
        Assert.Equal(0, Push(path, Edit(Open(path), json => Hull(json)["topSpeed"] = 42.5), apply: true).Code);
        var limit = ShipAuthoringStore.Load(path).Hull.Behaviors.OfType<VelocityLimitData>().Single().TopSpeed;
        Assert.Equal((42.5f, 42.5f), (limit.Min, limit.Max));
        Assert.Equal(42.5, (double)Hull(Parse(Open(path)))["topSpeed"]);

        // A hull with no VelocityLimit gets one.
        var bare = Path.Combine(ShipFixture.WritePackage(Mods, "mod.bare", tweak: ship => ship.Hull.Behaviors.Clear()), "ship.cc");
        Assert.Equal(JTokenType.Null, Hull(Parse(Open(bare)))["topSpeed"].Type);
        Assert.Equal(0, Push(bare, Edit(Open(bare), json => Hull(json)["topSpeed"] = 60), apply: true).Code);
        var added = ShipAuthoringStore.Load(bare).Hull.Behaviors.OfType<VelocityLimitData>().Single().TopSpeed;
        Assert.Equal((60f, 60f), (added.Min, added.Max));

        // Two leave no one top speed to set; open prints null for them and for a limit whose Min differs from its Max.
        var twin = Path.Combine(ShipFixture.WritePackage(Mods, "mod.twin", tweak: ship => ship.Hull.Behaviors.Add(new VelocityLimitData())), "ship.cc");
        Assert.Equal(JTokenType.Null, Hull(Parse(Open(twin)))["topSpeed"].Type);
        RefusedUntouched(twin, Edit(Open(twin), json => Hull(json)["topSpeed"] = 50), "at most one VelocityLimit");
        var ranged = Path.Combine(ShipFixture.WritePackage(Mods, "mod.ranged", tweak: ship =>
        {
            var speed = ship.Hull.Behaviors.OfType<VelocityLimitData>().Single().TopSpeed;
            speed.Min = 1;
            speed.Max = 2;
        }), "ship.cc");
        Assert.Equal(JTokenType.Null, Hull(Parse(Open(ranged)))["topSpeed"].Type);
    }

    [Fact]
    public void ProductsAddChangeRemove()
    {
        var (acme, _) = Catalog();
        var path = Package();
        var opened = Open(path);
        Assert.Empty(Parse(opened)["products"]);
        var added = Edit(opened, json => ((JArray)json["products"]).Add(new JObject { ["maker"] = "ACM", ["name"] = "Acme Skiff", ["description"] = "Reliable.", ["offered"] = true }));
        var productKey = ShipModCatalog.ProductKey("mod.skiff", acme).Value;
        var (code, changes, outcome) = Push(path, added, apply: true);
        Assert.True(code == 0, outcome.ToString());
        Assert.Equal(new[] { productKey + " added" }, changes);
        Assert.Equal(new[] { productKey }, outcome["changed"].Select(key => (string)key));
        Assert.NotNull(outcome["versions"][productKey]);
        var (hull, _, products) = ShipAuthoringStore.Load(path);
        var product = Assert.Single(products);
        Assert.Equal(ShipModCatalog.HullKey("mod.skiff"), product.Design.Key);
        Assert.Equal(acme, product.Manufacturer.Key);
        Assert.Equal(hull.Roles.Select(role => role.Name), product.Roles.Select(role => role.Role));
        Assert.Equal(("Acme Skiff", "Reliable.", false), (product.Name, product.Description, product.NotOffered));

        // A changed name keeps the roles and the assembly no section authors.
        using (var cache = ShipAuthoringStore.Open(path, writable: true))
        {
            var held = Pack(product);
            var custom = ShipAuthoringPush.Copy(product);
            custom.Roles = new List<ProductRole> { new ProductRole { Role = "special", Mean = .9f } };
            custom.Assembly = new AssemblyDistribution { Mean = .8f };
            ShipAuthoringStore.WriteProduct(cache, "mod.skiff", custom);
            cache.FlushAsync().Wait();
            Assert.NotEqual(held, Pack(custom));
        }
        var opened2 = Open(path);
        Assert.Equal(acme.Value, (string)Parse(opened2)["products"][0]["maker"]);
        var renamed = Edit(opened2, json => { json["products"][0]["name"] = "Acme Racer"; json["products"][0]["offered"] = false; });
        var (renameCode, renameChanges, _) = Push(path, renamed, apply: true);
        Assert.Equal(0, renameCode);
        Assert.Equal(new[] { productKey + " name: Acme Skiff -> Acme Racer", productKey + " offered: true -> false" }, renameChanges);
        var kept = Assert.Single(ShipAuthoringStore.Load(path).Products);
        Assert.Equal(("Acme Racer", true), (kept.Name, kept.NotOffered));
        Assert.Equal("special", Assert.Single(kept.Roles).Role);
        Assert.Equal(.8f, kept.Assembly.Mean);

        // A dropped row removes the record, with a hull change in the same commit.
        var removed = Edit(Open(path), json => { ((JArray)json["products"]).Clear(); Hull(json)["price"] = 5; });
        var (removeCode, removeChanges, removeOutcome) = Push(path, removed, apply: true);
        Assert.Equal(0, removeCode);
        Assert.Contains(productKey + " removed", removeChanges);
        Assert.Equal(new[] { "mod-hull:mod.skiff", productKey }, removeOutcome["changed"].Select(key => (string)key).OrderBy(key => key, StringComparer.Ordinal));
        Assert.Empty(ShipAuthoringStore.Load(path).Products);
    }

    [Fact]
    public void ProductMakerResolution()
    {
        var (_, twinA) = Catalog();
        var path = Package();
        ProductRowEdit(path, "Nobody", "X", "0 factions");
        ProductRowEdit(path, "TWN", "X", "2 factions");
        ProductRowEdit(path, "ACM", " ", "needs a name");
        RefusedUntouched(path, Edit(Open(path), json =>
        {
            ((JArray)json["products"]).Add(new JObject { ["maker"] = "ACM", ["name"] = "A", ["description"] = null, ["offered"] = true });
            ((JArray)json["products"]).Add(new JObject { ["maker"] = "Acme", ["name"] = "B", ["description"] = null, ["offered"] = true });
        }), "Acme");

        // A stored maker's key names its product with no resolution through the catalog's names (TWN is ambiguous there).
        using (var cache = ShipAuthoringStore.Open(path, writable: true))
        {
            ShipAuthoringStore.WriteProduct(cache, "mod.skiff", new FactionProductData
            {
                Name = "Twin Skiff",
                Design = new CultRecordRef<CraftedItemData>(ShipModCatalog.HullKey("mod.skiff")),
                Manufacturer = new CultRecordRef<Faction>(twinA)
            });
            cache.FlushAsync().Wait();
        }
        var request = Edit(Open(path), json => json["products"][0]["name"] = "Twin Racer");
        Assert.Equal(twinA.Value, (string)Parse(request)["products"][0]["maker"]);
        Assert.Equal(0, Push(path, request, apply: true).Code);
        Assert.Equal("Twin Racer", Assert.Single(ShipAuthoringStore.Load(path).Products).Name);
    }

    private void ProductRowEdit(string path, string maker, string name, string message) =>
        RefusedUntouched(path, Edit(Open(path), json => ((JArray)json["products"]).Add(
            new JObject { ["maker"] = maker, ["name"] = name, ["description"] = null, ["offered"] = true })), message);

    // A new product's key is expected absent: one written since the open makes the push stale.
    [Fact]
    public void NewProductExpectsAbsent()
    {
        var (acme, _) = Catalog();
        var path = Package();
        var request = Edit(Open(path), json => ((JArray)json["products"]).Add(new JObject { ["maker"] = "ACM", ["name"] = "Mine", ["description"] = null, ["offered"] = true }));
        var (code, _, error) = Command("product", path, "ACM", "--name", "Theirs");
        Assert.True(code == 0, error);
        var after = File.ReadAllBytes(path);
        var (pushCode, _, outcome) = Push(path, request, apply: true);
        Assert.Equal(2, pushCode);
        Assert.Equal("stale", (string)outcome["outcome"]);
        Assert.Equal(new[] { ShipModCatalog.ProductKey("mod.skiff", acme).Value }, outcome["stale"].Select(key => (string)key));
        Assert.Equal(after, File.ReadAllBytes(path));
    }

    [Fact]
    public void HullTypeChecked()
    {
        var path = Package();
        var open = Open(path);
        Assert.Equal("Ship", (string)Parse(open)["hullType"]);
        RefusedUntouched(path, Edit(open, json => json["hullType"] = "Station"), "hullType");
        var without = Edit(open, json => json.Remove("hullType"));
        Assert.Equal(0, Push(path, without, apply: true).Code);
    }

    [Fact]
    public void KindInHullSection()
    {
        Catalog();
        var target = Path.Combine(ShipFixture.WritePackage(Mods, "probe.depot", "Probe Depot", tweak: ship => ship.Hull.HullType = HullType.Station), "ship.cc");
        Assert.Equal(JTokenType.Null, Hull(Parse(Open(target)))["kind"].Type);

        // A stats push on a kindless station applies: the kind is only judged when it changes.
        Assert.Equal(0, Push(target, Edit(Open(target), json => Hull(json)["mass"] = 11), apply: true).Code);
        Assert.Equal(11f, ShipAuthoringStore.Load(target).Hull.Mass);
        Assert.Equal(0, Command("price", target, "12").Code);

        RefusedUntouched(target, Edit(Open(target), json => Hull(json)["kind"] = "station-kind:nowhere"), "names no station kind");
        var (code, _, error) = Command("kind", target, "depot");
        Assert.True(code == 0, error);
        Assert.Equal("station-kind:depot", (string)Hull(Parse(Open(target)))["kind"]);
        // A station with no kind is refused: clearing one is.
        RefusedUntouched(target, Edit(Open(target), json => Hull(json)["kind"] = null), "has no Kind");

        var ship = Package();
        RefusedUntouched(ship, Edit(Open(ship), json => Hull(json)["kind"] = "station-kind:depot"), "only a station hull has one");
    }

    // A stored kind that names no station kind is never passed as fine: it is judged whenever the request or the hull names one,
    // by push and by the kind command alike.
    [Fact]
    public void AnInvalidStoredKindIsNeverAccepted()
    {
        Catalog();
        var target = Path.Combine(ShipFixture.WritePackage(Mods, "probe.bad", "Probe Bad", tweak: ship =>
        {
            ship.Hull.HullType = HullType.Station;
            ship.Hull.Kind = new CultRecordRef<StationKindData>(StationKindData.KeyOf("nowhere"));
        }), "ship.cc");
        var before = File.ReadAllBytes(target);
        var (code, _, error) = Command("kind", target, "nowhere");
        Assert.Equal(1, code);
        Assert.Contains("names no station kind", error);
        RefusedUntouched(target, Edit(Open(target), json => Hull(json)["mass"] = 11), "names no station kind");
        Assert.Equal(before, File.ReadAllBytes(target));
    }

    // price and stats on a valid product package need no shipped catalog: it is opened only for a product's maker or a kind.
    [Fact]
    public void PriceAndStatsNeedNoGameData()
    {
        var path = Path.Combine(ShipFixture.WritePackage(Mods, "probe.sold", "Probe Sold"), "ship.cc");
        using (var cache = ShipAuthoringStore.Open(path, writable: true))
        {
            ShipAuthoringStore.WriteProduct(cache, "probe.sold", new FactionProductData
            {
                Name = "Sold",
                Design = new CultRecordRef<CraftedItemData>(ShipModCatalog.HullKey("probe.sold")),
                Manufacturer = new CultRecordRef<Faction>(new CultRecordKey("faction:maker"))
            });
            cache.FlushAsync().Wait();
        }
        Assert.False(File.Exists(Path.Combine(_root.Path, "GameData", "Aetheria.cc")));
        var (code, _, error) = Command("price", path, "300");
        Assert.True(code == 0, error);
        (code, _, error) = Command("stats", path, "--mass", "7", "--top-speed", "33");
        Assert.True(code == 0, error);
        var (hull, _, products) = ShipAuthoringStore.Load(path);
        Assert.Equal((300, 7f), (hull.Price, hull.Mass));
        Assert.Single(products);
    }
}
