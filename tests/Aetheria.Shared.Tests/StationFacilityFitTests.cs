using System;
using System.Collections.Generic;
using System.Linq;
using GameCult.Caching;
using Xunit;

// Facility rows first (aetheria-living-world, cut station-facility-fit): a generated station installs its kind's Facilities on its
// Facility rows, from LoadoutGenerator.EquipFacilities, before the docking bay, the heater or any interior gear. The tests read the
// owners (ruling census-tests-read-the-owners): StationKinds names the served kinds and each kind's Facilities.
public sealed class StationFacilityFitTests
{
    private static CultRecordRef<StationKindData> Kind(string id) => new CultRecordRef<StationKindData>(StationKindData.KeyOf(id));

    // One station of the kind from this maker's generator, seeded; the entity unpacked from its pack.
    private static (ItemManager Items, Entity Station) Generate(CultCache cache, Faction maker, CultRecordRef<StationKindData> kind, uint seed)
    {
        var items = new ItemManager(1u, cache, new ProvenanceLedger(), RunSaveTests.TestSettings(), _ => { });
        var random = new CultMath.Random(seed);
        var generator = new LoadoutGenerator(ref random, items, null, null, maker, .5f);
        var pack = generator.GenerateStationLoadout(kind);
        Assert.NotNull(pack);
        return (items, EntitySerializer.Unpack(items, null, pack));
    }

    // Every (faction, fieldable kind) pair of the catalog with the kind's Facilities, over a few seeds so every hull of the kind is drawn.
    private static IEnumerable<(Faction Maker, CultRecordRef<StationKindData> Kind, uint Seed)> Cases(CultCache cache) =>
        cache.GetAll<Faction>().SelectMany(faction => StationKinds.FieldableKindsOf(cache, faction)
            .Where(kind => cache.Get(kind).Facilities.Count > 0)
            .SelectMany(kind => Enumerable.Range(1, 6).Select(seed => (faction, kind, (uint) seed))));

    private static string[] FacilityKeys(Entity station) => station.Equipment.Where(item => item.Data.HardpointType == HardpointType.Facility)
        .Select(item => item.Data.Name).OrderBy(name => name, StringComparer.Ordinal).ToArray();

    [Fact]
    public void EveryServedKindGetsItsFacilities()
    {
        using var cache = RestoredHullsTests.OpenCatalog();
        var cases = Cases(cache).ToArray();
        Assert.NotEmpty(cases);
        foreach (var (maker, kind, seed) in cases)
        {
            var (items, station) = Generate(cache, maker, kind, seed);
            var wanted = cache.Get(kind).Facilities.Select(facility => cache.Get(facility).Name).OrderBy(name => name, StringComparer.Ordinal).ToArray();
            Assert.Equal(wanted, FacilityKeys(station));
            Assert.All(station.Equipment.Where(item => item.Data.HardpointType == HardpointType.Facility),
                item => Assert.Equal(HardpointType.Facility, item.Hardpoint.Type));
        }
    }

    [Fact]
    public void NoInteriorGearSitsOnAFacilityRow()
    {
        using var cache = RestoredHullsTests.OpenCatalog();
        foreach (var (maker, kind, seed) in Cases(cache))
        {
            var (_, station) = Generate(cache, maker, kind, seed);
            foreach (var tool in station.Equipment.Where(item => item.Data.HardpointType == HardpointType.Tool))
                foreach (var cell in tool.Data.Shape.Coordinates)
                {
                    var at = tool.Position + tool.Data.Shape.Rotate(cell, tool.EquippableItem.Rotation);
                    Assert.NotEqual(HardpointType.Facility, station.Hardpoints[at.x, at.y]?.Type);
                }
        }
    }

    // The hull row of a kind with no second row: an in-memory second facility has nowhere to go and generation names why.
    [Fact]
    public void AKindWithNoRowFailsByName()
    {
        using var cache = RestoredHullsTests.OpenCatalog();
        var mining = cache.Get(Kind("mining"));
        var extra = cache.Get(Kind("processing")).Facilities.Single();
        mining.Facilities.Add(extra);
        var maker = cache.GetAll<Faction>().First(faction => StationKinds.FieldableKindsOf(cache, faction).Any(kind => kind.Key.Equals(Kind("mining").Key)));
        var error = Assert.Throws<InvalidLoadoutException>(() => Generate(cache, maker, Kind("mining"), 1));
        Assert.Contains(mining.Name, error.Message);
        Assert.Contains(cache.Get(extra).Name, error.Message);
        Assert.Contains("station hull", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FacilitiesSurviveThePack()
    {
        using var cache = RestoredHullsTests.OpenCatalog();
        var (maker, kind, seed) = Cases(cache).First();
        var items = new ItemManager(1u, cache, new ProvenanceLedger(), RunSaveTests.TestSettings(), _ => { });
        var random = new CultMath.Random(seed);
        var pack = new LoadoutGenerator(ref random, items, null, null, maker, .5f).GenerateStationLoadout(kind);
        var before = EntitySerializer.Unpack(items, null, pack);
        var again = EntitySerializer.Unpack(items, null, EntitySerializer.Pack(before) as OrbitalEntityPack);
        Assert.NotEmpty(FacilityKeys(before));
        Assert.Equal(FacilityKeys(before), FacilityKeys(again));
        Assert.All(again.Equipment.Where(item => item.Data.HardpointType == HardpointType.Facility),
            item => Assert.Equal(HardpointType.Facility, item.Hardpoint.Type));
    }

    // The facility step takes nothing from the generator's Random: the same seed with the kind's Facilities emptied draws the same
    // station hull, docking bay and heater products.
    [Fact]
    public void EquipFacilitiesDrawsNothing()
    {
        using var with = RestoredHullsTests.OpenCatalog();
        using var without = RestoredHullsTests.OpenCatalog();
        foreach (var (maker, kind, seed) in Cases(with).Where(entry => entry.Seed <= 3))
        {
            without.Get(kind).Facilities.Clear();
            var makerWithout = without.Get(with.RefOf(maker));
            var (itemsA, a) = Generate(with, maker, kind, seed);
            var (itemsB, b) = Generate(without, makerWithout, kind, seed);
            Assert.Equal(a.HullData.Name, b.HullData.Name);
            string Brand(ItemManager items, Entity station) => string.Join("|", station.Equipment
                .Where(item => item.Data is DockingBayData || item.Data is GearData gear && LoadoutGenerator.IsHeater(gear))
                .Select(item => item.Data.Name + ":" + items.ItemData.Get(item.Lot.Product)?.Name).OrderBy(text => text, StringComparer.Ordinal));
            Assert.Equal(Brand(itemsA, a), Brand(itemsB, b));
        }
    }
}
