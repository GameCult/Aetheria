using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MessagePack;
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
        var stats = ConsoleProbe.Capture(() => ShipAuthoringCommands.Run(new[] { "stats", path, "--mass", "1" }), out _, out var statsError);
        Assert.True(stats == 0, statsError);
        var after = File.ReadAllBytes(path);
        var (code, _, outcome) = Push(path, request, apply: true);
        Assert.Equal(2, code);
        Assert.Equal("stale", (string)outcome["outcome"]);
        Assert.Equal(new[] { "mod-hull:mod.skiff", "mod-ship:mod.skiff" }, outcome["stale"].Select(key => (string)key).OrderBy(key => key, StringComparer.Ordinal));
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
}
