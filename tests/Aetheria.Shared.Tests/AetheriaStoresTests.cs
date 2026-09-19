using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using CultMath;
using GameCult.Caching;
using GameCult.Caching.MessagePack;
using MessagePack;
using Xunit;

// A [CultGlobal] routed to the catalog (PersonalityAttribute is a catalog home). The game has none yet, so this is
// the only way to prove a read-only catalog refuses to open without one.
[CultDocument("aetheria.tests.catalogglobal", "1"), CultGlobal, MessagePackObject]
public class TestCatalogGlobal : PersonalityAttribute { }

// Each test gets its own temp directory holding a catalog built through Open(catalogWritable: true).
public sealed class AetheriaStoresTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "aetheria-stores-" + Guid.NewGuid().ToString("N"));
    private string Catalog => Path.Combine(_root, "Aetheria.cc");
    private string Run => Path.Combine(_root, "run.cc");
    private string Player => Path.Combine(_root, "player.cc");

    public AetheriaStoresTests()
    {
        Directory.CreateDirectory(_root);
        using var cache = AetheriaStores.Open(Catalog, catalogWritable: true);
        cache.Upsert(new TestCatalogGlobal { Name = "Temperament" });
        var faction = new Faction { Name = "Adrasteia", ShortName = "ADR", PrimaryColor = new float3(1, .5f, .25f) };
        faction.Allegiance[cache.Upsert(faction)] = 1;
        cache.Upsert(faction);
        var lance = cache.Upsert(new WeaponItemData { Name = "Lance" });
        cache.Upsert(new FactionProductData { Name = "Lance", Design = new CultRecordRef<CraftedItemData>(lance.Key), Manufacturer = cache.RefOf(faction) });
        cache.Upsert(new InputLayout { Rows = new InputLayoutRow[] { new InputLayoutRowSpacer { Height = 1 } } });
        cache.FlushAsync().Wait();
    }

    public void Dispose() => Directory.Delete(_root, true);

    [Fact]
    public void OpenThenFlushLeavesEveryFileByteIdentical()
    {
        using (var cache = AetheriaStores.Open(Catalog, Run, Player))
        {
            var zone = cache.Upsert(new SavedZone { Name = "Entrance", Position = new float2(1, 2), Contents = new ZonePack() });
            cache.Upsert(new SavedGame { Zones = new[] { zone }, Factions = new[] { cache.RefOf(cache.GetAll<Faction>().Single()) } });
            cache.Upsert(new PlayerSettings());
            cache.FlushAsync().Wait();
        }

        var before = new[] { Catalog, Run, Player }.Select(Hash).ToArray();
        using (var cache = AetheriaStores.Open(Catalog, Run, Player))
        {
            Assert.NotNull(cache.GetGlobal<SavedGame>());
            Assert.NotNull(cache.GetGlobal<PlayerSettings>());
            cache.FlushAsync().Wait();
        }

        Assert.Equal(before, new[] { Catalog, Run, Player }.Select(Hash).ToArray());
    }

    [Fact]
    public void WeaponWrittenThroughGearHandleReloadsAsWeapon()
    {
        CultRecordHandle<GearData> handle;
        using (var cache = AetheriaStores.Open(Catalog, catalogWritable: true))
        {
            handle = cache.UpsertAsync<GearData>(new WeaponItemData { Name = "Pike" }).Result;
            cache.FlushAsync().Wait();
        }

        using (var cache = AetheriaStores.Open(Catalog))
            Assert.Equal("Pike", Assert.IsType<WeaponItemData>(cache.Get(handle.Key)).Name);
    }

    [Fact]
    public void CatalogRefusesWrites()
    {
        var before = Hash(Catalog);
        using (var cache = AetheriaStores.Open(Catalog))
        {
            Assert.Throws<InvalidOperationException>(() => cache.Upsert(new Faction { Name = "Usurper" }));
            cache.FlushAsync().Wait();
        }

        Assert.Equal(before, Hash(Catalog));
    }

    [Fact]
    public void SavedZoneNeverLandsInCatalog()
    {
        using (var cache = AetheriaStores.Open(Catalog, catalogWritable: true))
            Assert.Throws<InvalidOperationException>(() => cache.Upsert(new SavedZone { Name = "Stray" }));

        using (var cache = AetheriaStores.Open(Catalog, Run, catalogWritable: true))
        {
            cache.Upsert(new SavedZone { Name = "Home" });
            cache.FlushAsync().Wait();
        }

        Assert.DoesNotContain(SchemaNames(Catalog), name => name == "aetheria.savedzone");
        Assert.Contains("aetheria.savedzone", SchemaNames(Run));
    }

    [Fact]
    public void FactionIsASingletonInstance()
    {
        using var cache = AetheriaStores.Open(Catalog);
        var faction = cache.GetAll<Faction>().Single();
        Assert.Same(faction, cache.Get(cache.GetAll<FactionProductData>().Single().Manufacturer));
        Assert.Same(faction, cache.Get(faction.Allegiance.Keys.Single()));
        Assert.Same(faction, cache.GetByName<Faction>("Adrasteia"));
    }

    [Fact]
    public void MissingCatalogGlobalIsLoud()
    {
        var empty = Path.Combine(_root, "empty.cc");
        using (AetheriaStores.Open(empty, catalogWritable: true)) { }
        var error = Assert.Throws<InvalidOperationException>(() => AetheriaStores.Open(empty));
        Assert.Contains("aetheria.tests.catalogglobal", error.Message);
        using (AetheriaStores.Open(Catalog)) { }
    }

    // Writability exempts only an empty seed catalog: a populated catalog missing a global is loud even when writable.
    [Fact]
    public void WritablePopulatedCatalogMissingGlobalIsLoud()
    {
        using (var cache = AetheriaStores.Open(Catalog, catalogWritable: true))
        {
            var global = cache.GetGlobal<TestCatalogGlobal>();
            var key = cache.RefOf(global).Key;
            Assert.True(cache.Commit(batch => batch.Remove(key)));
        }

        var error = Assert.Throws<InvalidOperationException>(() => AetheriaStores.Open(Catalog, catalogWritable: true));
        Assert.Contains("aetheria.tests.catalogglobal", error.Message);
    }

    // The game opens the catalog read-only with its run and player stores, in the editor as in a build: a populated
    // catalog missing a global refuses to open.
    [Fact]
    public void GameShapedOpenMissingGlobalIsLoud()
    {
        using (var cache = AetheriaStores.Open(Catalog, catalogWritable: true))
            Assert.True(cache.Commit(batch => batch.Remove(cache.RefOf(cache.GetGlobal<TestCatalogGlobal>()).Key)));

        var error = Assert.Throws<InvalidOperationException>(() => AetheriaStores.Open(Catalog, Run, Player));
        Assert.Contains("aetheria.tests.catalogglobal", error.Message);
    }

    // --- F7 (docs/stats-and-power-cut.md, Soul pass 2026-09-19): "there are at least four [write bypasses]."
    // --- tools/AetherDb/Program.cs's Dangling, ShieldMigrate and BrownoutMigrate commands repair an existing
    // --- record in place through `cache.Commit(batch => batch.Upsert(document.GetType(), document, key))` --
    // --- CultCache's own batch API, keyed explicitly to preserve identity -- because CultRecordRefs.Upsert's own
    // --- cache.UpsertAsync(document) cannot take a caller-supplied key. That is real, not an excuse to skip
    // --- validation: all three now call CultRecordRefs.Validate(document) directly, immediately before their own
    // --- commit. This pins that the extracted method actually refuses what Upsert would have, proving the tools'
    // --- direct call is wired to the same rule rather than a hollow copy. ---
    [Fact]
    public void ValidateRefusesAnEquippableItemWithAZeroSpanHeatRangeTheSameWayUpsertWould()
    {
        var badDesign = new GearData
        {
            Name = "BadDangling", Hardpoint = HardpointType.Tool, Shape = new Shape(), Durability = 10,
            MinimumTemperature = 100, MaximumTemperature = 100, OptimalTemperature = 100, PlateauWidth = 0
        };
        var error = Assert.Throws<InvalidOperationException>(() => CultRecordRefs.Validate(badDesign));
        Assert.Contains("BadDangling", error.Message);
    }

    // Nominal-request ruling (docs/stats-and-power-cut.md, operator ruling 2026-09-19), superseding F6: this
    // direct-call path used to also catch F6's static rule (a power request stat may not carry a PowerSupply
    // term at all -- StatValidation.ValidateNoPowerSupplyOnRequest, since deleted). PowerRequest now reads a
    // request field through EquippedItem.EvaluateNominalPower, which pins that stat's own PowerSupplyFactor to 1
    // regardless of its Terms, so the term can no longer make the request depend on its own answer -- a repair
    // tool landing a curved ThrusterData.EnergyUsage is no longer any more dangerous than R-heat's own check,
    // which this still runs (see the sibling test above).
    [Fact]
    public void ValidateAcceptsAPowerRequestCarryingAPowerSupplyTermTheSameWayUpsertWould()
    {
        var curvedEnergy = new PerformanceStat { Min = 1, Max = 1, Terms = { new StatTerm { Source = StatSource.PowerSupply, Exponent = 1 } } };
        var goodDesign = new GearData
        {
            Name = "CurvedMigratedThruster", Hardpoint = HardpointType.Tool, Shape = new Shape(), Durability = 10,
            MinimumTemperature = 0, MaximumTemperature = 1000, OptimalTemperature = 280, PlateauWidth = 400,
            Behaviors = { new ThrusterData { EnergyUsage = curvedEnergy } }
        };
        var record = Record.Exception(() => CultRecordRefs.Validate(goodDesign));
        Assert.Null(record);
    }

    // A document type Validate has no opinion about (neither EquippableItemData nor ConsumableItemData) must be
    // a pure no-op, not a crash -- the three tools' change lists carry `object`, and not every migration touches
    // an equippable design (e.g. a future repair over FactionProductData).
    [Fact]
    public void ValidateIsANoOpForADocumentTypeItDoesNotGovern()
    {
        var record = Record.Exception(() => CultRecordRefs.Validate(new Faction { Name = "Unrelated" }));
        Assert.Null(record);
    }

    private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));

    private static string[] SchemaNames(string path)
    {
        var snapshot = CultDocumentMessagePackSerialization.DeserializeSnapshot(File.ReadAllBytes(path));
        return snapshot.Records
            .Select(record => snapshot.SchemaCatalog.Single(entry => entry.SchemaId == record.SchemaId).SchemaName)
            .ToArray();
    }
}
