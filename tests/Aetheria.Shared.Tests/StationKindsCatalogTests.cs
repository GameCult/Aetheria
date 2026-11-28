using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GameCult.Caching;
using Xunit;

public sealed class StationKindsCatalogTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "aetheria-stationkindscmd-" + Guid.NewGuid().ToString("N"));

    public StationKindsCatalogTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, true);

    private static readonly string[] Ids =
    {
        "mining", "volatiles", "agriculture", "processing", "fabrication", "shipyard", "depot", "trade-hub", "residential",
        "habitat-support", "urban", "administrative", "power", "research", "salvage", "black-market", "waystation"
    };

    // The catalog this command first runs on holds a station hull with no kind. The game's Open refuses it; the tool's Open
    // names the hull and goes on, so the command can give it one: Zenith gets the ruled kind, a kind it already has is kept.
    [Fact]
    public void ZenithGetsTheRuledKindAndAKindedOneIsKept()
    {
        var path = Path.Combine(_root, "Zenith.cc");
        using (var seed = AetheriaStores.Open(path, catalogWritable: true))
        {
            seed.Upsert(new VerseGrammar { Revision = 1 });
            seed.Upsert(new InputLayout { Rows = new InputLayoutRow[] { new InputLayoutRowSpacer { Height = 1 } } });
            seed.Upsert(new HullData { Name = "Zenith", HullType = HullType.Station, Shape = new Shape(2, 2), Price = 1 });
            seed.Upsert(new HullData { Name = "Cutter", HullType = HullType.Ship, Shape = new Shape(2, 2), Price = 1 });
            seed.FlushAsync().Wait();
        }
        Assert.Throws<InvalidOperationException>(() => AetheriaStores.Open(path));

        var named = new List<string>();
        var dry = new StringWriter();
        using (var cache = AetheriaStores.Open(path, kindlessStation: named.Add)) StationKindsCatalog.Run(cache, false, dry);
        Assert.Contains("Zenith", Assert.Single(named));
        Assert.Contains("Zenith: kind trade-hub", dry.ToString());

        var applied = new StringWriter();
        using (var cache = AetheriaStores.Open(path, catalogWritable: true, kindlessStation: _ => { }))
        {
            StationKindsCatalog.Run(cache, true, applied);
            cache.FlushAsync().Wait();
        }
        Assert.Contains("and Zenith's kind", applied.ToString());
        using (var cache = AetheriaStores.Open(path))
        {
            Assert.Equal(StationKindData.KeyOf("trade-hub").Value, cache.GetAll<HullData>().Single(h => h.Name == "Zenith").Kind.Key.Value);
            Assert.False(cache.GetAll<HullData>().Single(h => h.Name == "Cutter").Kind.IsSet());
        }

        var again = new StringWriter();
        using (var cache = AetheriaStores.Open(path, catalogWritable: true, kindlessStation: _ => { })) StationKindsCatalog.Run(cache, true, again);
        Assert.Contains("Zenith: already has a kind, kept", again.ToString());
        Assert.Contains("Nothing to author", again.ToString());
    }

    private string SeedRoot(params string[] kindlessStations)
    {
        var root = Path.Combine(_root, "root" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "GameData"));
        using var seed = AetheriaStores.Open(Path.Combine(root, "GameData", "Aetheria.cc"), catalogWritable: true);
        seed.Upsert(new VerseGrammar { Revision = 1 });
        seed.Upsert(new InputLayout { Rows = new InputLayoutRow[] { new InputLayoutRowSpacer { Height = 1 } } });
        foreach (var name in kindlessStations) seed.Upsert(new HullData { Name = name, HullType = HullType.Station, Shape = new Shape(2, 2), Price = 1 });
        seed.Upsert(new HullData { Name = "Cutter", HullType = HullType.Ship, Shape = new Shape(2, 2), Price = 1 });
        seed.FlushAsync().Wait();
        return root;
    }

    // One kindless station hull does not lock the owner out of the catalog: the tool opens it, names the hull, and the
    // command that sets kinds is reachable. The game's own Open still refuses until every station hull has a kind.
    [Fact]
    public void AKindlessStationHullDoesNotLockTheToolOut()
    {
        var root = SeedRoot("Zenith", "Second Station");
        var catalog = Path.Combine(root, "GameData", "Aetheria.cc");
        Assert.Throws<InvalidOperationException>(() => AetheriaStores.Open(catalog));

        var warned = new List<string>();
        var applied = new StringWriter();
        using (var db = AetherDb.OpenShipped(catalogWritable: true, root: root, warn: warned.Add))
        {
            Assert.Contains(warned, line => line.Contains("Zenith"));
            Assert.Contains(warned, line => line.Contains("Second Station"));
            Assert.Equal(0, StationKindsCatalog.Run(db.Cache, true, applied));
            Assert.Contains("Second Station", applied.ToString());
            db.Cache.FlushAsync().Wait();
        }
        Assert.Throws<InvalidOperationException>(() => AetheriaStores.Open(catalog));

        using (var db = AetherDb.OpenShipped(catalogWritable: true, root: root, warn: _ => { }))
        {
            var dry = new StringWriter();
            Assert.Equal(0, StationKindsCatalog.SetHullKind(db.Cache, "Second Station", "mining", false, dry));
            Assert.Contains("Dry run", dry.ToString());
            Assert.False(db.Cache.GetAll<HullData>().Single(h => h.Name == "Second Station").Kind.IsSet());
            var set = new StringWriter();
            Assert.Equal(0, StationKindsCatalog.SetHullKind(db.Cache, "Second Station", "mining", true, set));
            db.Cache.FlushAsync().Wait();
        }
        using var strict = AetheriaStores.Open(catalog);
        Assert.Equal(StationKindData.KeyOf("mining").Value, strict.GetAll<HullData>().Single(h => h.Name == "Second Station").Kind.Key.Value);
    }

    [Fact]
    public void SetHullKindRefusesWhatIsNotAStationHullOrAHeldKind()
    {
        var root = SeedRoot("Zenith");
        using var db = AetherDb.OpenShipped(catalogWritable: true, root: root, warn: _ => { });
        StationKindsCatalog.Run(db.Cache, true, new StringWriter());
        string Refusal(string hull, string kind)
        {
            var output = new StringWriter();
            Assert.Equal(1, StationKindsCatalog.SetHullKind(db.Cache, hull, kind, true, output));
            return output.ToString();
        }
        Assert.Contains("no station kind", Refusal("Zenith", "nowhere"));
        Assert.Contains("no station hull", Refusal("Cutter", "mining"));
        Assert.Contains("no station hull", Refusal("Missing", "mining"));
        Assert.False(db.Cache.GetAll<HullData>().Single(h => h.Name == "Cutter").Kind.IsSet());
    }

    // A writable catalog holding a solid 6x6 station hull (interior cells 1..4) with one Reactor row, a Ship hull, and a facility design.
    private CultCache HubCatalog(out CultRecordKey hubKey)
    {
        var cache = AetheriaStores.Open(Path.Combine(_root, "hub" + Guid.NewGuid().ToString("N") + ".cc"), catalogWritable: true);
        cache.Upsert(new VerseGrammar { Revision = 1 });
        cache.Upsert(new InputLayout { Rows = new InputLayoutRow[] { new InputLayoutRowSpacer { Height = 1 } } });
        var kind = new CultRecordRef<StationKindData>(cache.Upsert(new StationKindData { Name = "Hub" }).Key);
        var shape = new Shape(6, 6);
        foreach (var cell in shape.AllCoordinates) shape[cell] = true;
        var reactor = new Shape(2, 2);
        foreach (var cell in reactor.AllCoordinates) reactor[cell] = true;
        hubKey = cache.Upsert(new HullData
        {
            Name = "Hub", HullType = HullType.Station, Kind = kind, Shape = shape, Price = 1,
            Hardpoints = { new HardpointData { Type = HardpointType.Reactor, Position = new CultMath.int2(1, 1), Shape = reactor, Transform = "R.1", Armor = 2 } }
        }).Key;
        cache.Upsert(new HullData { Name = "Cutter", HullType = HullType.Ship, Shape = shape, Price = 1 });
        cache.Upsert(new GearData { Name = "Works", Hardpoint = HardpointType.Facility, Shape = reactor, Price = 1, Behaviors = { new FacilityData() } });
        return cache;
    }

    private static string Rows(HullData hull) =>
        string.Join(" ", hull.Hardpoints.Select(row => $"{row.Transform}:{row.Type}@{row.Position.x},{row.Position.y}:{row.Shape.Width}x{row.Shape.Height}:{row.Armor}"));

    [Fact]
    public void SetFacilityRowsSetsRowsAndARerunWritesNothing()
    {
        using var cache = HubCatalog(out var key);
        var before = Rows(cache.Get<HullData>(key));
        var dry = new StringWriter();
        Assert.Equal(0, StationKindsCatalog.SetFacilityRows(cache, "Hub", new[] { "3,3:##/##", "1,3:#/#" }, false, dry));
        Assert.Contains("Dry run", dry.ToString());
        Assert.Contains("Works", dry.ToString());
        Assert.Equal(before, Rows(cache.Get<HullData>(key)));
        Assert.Equal(0, StationKindsCatalog.SetFacilityRows(cache, "Hub", new[] { "3,3:##/##", "1,3:#/#" }, true, new StringWriter()));
        var after = Rows(cache.Get<HullData>(key));
        Assert.Equal(before + " Fa.1:Facility@3,3:2x2:0 Fa.2:Facility@1,3:1x2:0", after);
        // The facility design takes the 2x2 row and no other.
        var set = cache.Get<HullData>(key).Hardpoints.Where(row => row.Type == HardpointType.Facility).ToArray();
        Assert.True(set[0].Takes(cache.GetByName<GearData>("Works")));
        Assert.False(set[1].Takes(cache.GetByName<GearData>("Works")));
        var stored = cache.GetStored(key).StoredAt;
        var again = new StringWriter();
        Assert.Equal(0, StationKindsCatalog.SetFacilityRows(cache, "Hub", new[] { "3,3:##/##", "1,3:#/#" }, true, again));
        Assert.Contains("No change", again.ToString());
        Assert.Equal(stored, cache.GetStored(key).StoredAt);
        // A call with no rows clears the Facility rows and keeps every other row as it was.
        Assert.Equal(0, StationKindsCatalog.SetFacilityRows(cache, "Hub", new string[0], true, new StringWriter()));
        Assert.Equal(before, Rows(cache.Get<HullData>(key)));
    }

    [Fact]
    public void SetFacilityRowsRefusesWhatItCannotPlaceAndLeavesTheRowsEqual()
    {
        using var cache = HubCatalog(out var key);
        Assert.Equal(0, StationKindsCatalog.SetFacilityRows(cache, "Hub", new[] { "3,3:##/##" }, true, new StringWriter()));
        var before = Rows(cache.Get<HullData>(key));
        string Refusal(string hull, params string[] rows)
        {
            var output = new StringWriter();
            Assert.Equal(1, StationKindsCatalog.SetFacilityRows(cache, hull, rows, true, output));
            Assert.Equal(before, Rows(cache.Get<HullData>(key)));
            return output.ToString();
        }
        Assert.Contains("only a station hull", Refusal("Cutter", "1,1:#"));
        Assert.Contains("exactly one is required", Refusal("Missing", "1,1:#"));
        Assert.Contains("outside Hub's interior", Refusal("Hub", "4,4:##/##"));
        Assert.Contains("outside Hub's interior", Refusal("Hub", "0,0:#"));
        Assert.Contains("overlaps another row", Refusal("Hub", "2,2:#"));
        Assert.Contains("overlaps another row", Refusal("Hub", "3,3:#", "3,3:#"));
        Assert.Contains("is not a row", Refusal("Hub", "3,3"));
        Assert.Contains("is not a row", Refusal("Hub", "-1,3:#"));
        Assert.Contains("rows of # and .", Refusal("Hub", "3,3:#x/##"));
    }

    [Fact]
    public void DryRunWritesNothingApplyIsIdempotent()
    {
        var path = Path.Combine(_root, "Aetheria.cc");
        using (var seed = AetheriaStores.Open(path, catalogWritable: true))
        {
            seed.Upsert(new VerseGrammar { Revision = 1 });
            seed.Upsert(new InputLayout { Rows = new InputLayoutRow[] { new InputLayoutRowSpacer { Height = 1 } } });
            seed.FlushAsync().Wait();
        }
        var before = File.ReadAllBytes(path);

        using (var cache = AetheriaStores.Open(path, catalogWritable: true))
        {
            var dry = new StringWriter();
            Assert.Equal(0, StationKindsCatalog.Run(cache, false, dry));
            cache.FlushAsync().Wait();
            Assert.Equal(17, dry.ToString().Split((char) 10).Count(line => line.TrimEnd().EndsWith("authors")));
            Assert.Contains("Dry run", dry.ToString());
            Assert.Contains("Zenith: no such hull", dry.ToString());
            Assert.Empty(cache.GetAll<StationKindData>());
        }
        Assert.Equal(before, File.ReadAllBytes(path));

        using (var cache = AetheriaStores.Open(path, catalogWritable: true))
        {
            var output = new StringWriter();
            Assert.Equal(0, StationKindsCatalog.Run(cache, true, output));
            cache.FlushAsync().Wait();
            Assert.Contains("Authored 17 station kinds in", output.ToString());
            var kinds = cache.GetAll<StationKindData>().ToArray();
            Assert.Equal(Ids.Select(id => StationKindData.KeyOf(id).Value).OrderBy(key => key, StringComparer.Ordinal),
                kinds.Select(kind => cache.RefOf(kind).Key.Value).OrderBy(key => key, StringComparer.Ordinal));
            Assert.All(kinds, kind => { Assert.False(string.IsNullOrWhiteSpace(kind.Name)); Assert.False(string.IsNullOrWhiteSpace(kind.Description)); });
        }
        var applied = File.ReadAllBytes(path);

        using (var cache = AetheriaStores.Open(path, catalogWritable: true))
        {
            var output = new StringWriter();
            StationKindsCatalog.Run(cache, true, output);
            cache.FlushAsync().Wait();
            Assert.Contains("Nothing to author", output.ToString());
            Assert.Equal(17, output.ToString().Split((char) 10).Count(line => line.TrimEnd().EndsWith("kept")));
            Assert.Equal(17, cache.GetAll<StationKindData>().Count());
        }
        Assert.Equal(applied, File.ReadAllBytes(path));
    }
}
