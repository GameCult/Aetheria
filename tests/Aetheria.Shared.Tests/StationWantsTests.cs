/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.Linq;
using GameCult.Caching;
using Xunit;
using float2 = CultMath.float2;

// What a station buys on the shipped catalog (aetheria-living-world, cut station-sale-by-wants; rulings
// installed-gear-decides-what-a-station-buys, transit-depots-buy-for-onward-haul, demo-zenith-ore-depot, no-market-absorption):
// a recipe facility's draws, a silo's haul order, the silo as a bay, the order as a run record, and the room generated stock leaves.
public sealed partial class RunStartTests
{
    // A hold that holds `units` of every design but the excluded ones.
    private sealed class FullHold : IProductionHold
    {
        private readonly HashSet<CultRecordKey> _without;
        public FullHold(IEnumerable<CultRecordKey> without = null) => _without = new HashSet<CultRecordKey>(without ?? Array.Empty<CultRecordKey>());
        public int Held(CultRecordKey design) => _without.Contains(design) ? 0 : 1000;
        public bool TryTake(IReadOnlyList<(CultRecordKey design, int units)> units) => true;
        public bool TryPut(CultRecordKey design, int units) => true;
        public int Room(CultRecordKey design) => int.MaxValue;
    }

    private IEnumerable<CultRecordKey> ShippedFacilityDesigns() =>
        _cache.GetAll<StationKindData>().SelectMany(kind => kind.Facilities).Select(facility => facility.Key).Distinct()
            .Where(key => StationKinds.IsRecipeFacility(_cache.Get(new CultRecordRef<ItemData>(key))));

    // Production.Draws names every design Take can take: with every good in the hold, and with each filler of each role the only one.
    [Fact]
    public void DrawsNamesEveryDesignTakeTakes()
    {
        var checkedRecipes = 0;
        foreach (var facility in ShippedFacilityDesigns())
            foreach (var recipe in Production.RunList(_cache, facility))
            {
                var draws = Production.Draws(_cache, recipe);
                var all = Production.Take(_cache, recipe, new FullHold());
                Assert.NotNull(all);
                Assert.All(all, entry => Assert.Contains(entry.design, draws));
                foreach (var role in Recipes.RoleInputsOf(_cache, recipe))
                {
                    var fillers = Recipes.Fillers(_cache, role.Name);
                    foreach (var only in fillers)
                    {
                        var taken = Production.Take(_cache, recipe, new FullHold(fillers.Where(filler => !filler.Equals(only))));
                        Assert.NotNull(taken);
                        Assert.All(taken, entry => Assert.Contains(entry.design, draws));
                        Assert.Contains(taken, entry => entry.design.Equals(only));
                    }
                }
                checkedRecipes++;
            }
        Assert.True(checkedRecipes > 10, $"{checkedRecipes} recipes checked");
    }

    // The wants of the Pirates' refinery are the draws of its run list and nothing else: Gun Forging makes a compound, so it is on the
    // facility's RunOn list but not its run list, and what only it draws is not wanted.
    [Fact]
    public void AFacilityThatRunsNothingWantsNothing()
    {
        var (_, arena, _, failures) = Launch(new DemoTerminus(), Inputs(() => 1));
        Assert.True(failures.Count == 0, string.Join("; ", failures));
        var refinery = StationOfKind(arena, "processing");
        var design = refinery.Equipment.Select(equipped => _items.GetData(equipped.EquippableItem)).Single(data => StationKinds.IsRecipeFacility(data));
        var key = _cache.RefOf(design).Key;
        var run = Production.RunList(_cache, key);
        var drawn = new HashSet<CultRecordKey>(run.SelectMany(recipe => Production.Draws(_cache, recipe)));
        Assert.Equal(drawn.OrderBy(good => good.Value, StringComparer.Ordinal), StationWants.Of(refinery).OrderBy(good => good.Value, StringComparer.Ordinal));

        var skipped = Recipes.RunOn(_cache, key).Except(run).ToArray();
        Assert.NotEmpty(skipped);
        var onlySkipped = skipped.SelectMany(recipe => Production.Draws(_cache, recipe)).Where(good => !drawn.Contains(good)).ToArray();
        Assert.NotEmpty(onlySkipped);
        Assert.Empty(StationWants.Of(refinery).Intersect(onlySkipped));
    }

    private OrbitalEntity DemoStation(string kind, uint seed = 1)
    {
        var (_, arena, _, failures) = Launch(new DemoTerminus(), Inputs(() => seed));
        Assert.True(failures.Count == 0, string.Join("; ", failures));
        return StationOfKind(arena, kind);
    }

    // Zenith's silo is a bay on its Facility row, in CargoBays and not in Equipment; it survives a pack and restore, takes ore and
    // refuses gear and consumables, and generated gear stock never lands in it.
    [Fact]
    public void ASiloIsABayOnItsFacilityRowAndHoldsBulkOnly()
    {
        var (_, arena, _, failures) = Launch(new DemoTerminus(), Inputs(() => 1));
        Assert.True(failures.Count == 0, string.Join("; ", failures));
        var zenith = StationOfKind(arena, "trade-hub");
        var silo = Assert.Single(zenith.CargoBays, bay => bay.Data is SiloData);
        Assert.NotNull(silo.Hardpoint);
        Assert.Equal(HardpointType.Facility, silo.Hardpoint.Type);
        Assert.DoesNotContain(zenith.Equipment, equipped => equipped.Data is SiloData);
        Assert.Empty(silo.Cargo);
        Assert.Contains(zenith.CargoBays, bay => !(bay.Data is SiloData) && bay.Cargo.Count > 0);

        var ore = StationWants.Of(zenith).OrderBy(good => good.Value, StringComparer.Ordinal).First();
        Assert.True(silo.TryStore(new SimpleCommodity { Data = new CultRecordRef<ItemData>(ore), Quantity = 3 }));
        var gear = _cache.GetAll<GearData>().First(data => data.HardpointType == HardpointType.Sensors);
        var instance = _items.CreateInstance(_cache.GetAll<FactionProductData>().First(product => product.Design.Key.Equals(_cache.RefOf(gear).Key)));
        Assert.False(silo.TryStore(instance));
        Assert.False(silo.TryFindSpace(instance));

        var restored = EntitySerializer.Unpack(_items, arena, EntitySerializer.Pack(zenith));
        var restoredSilo = Assert.Single(restored.CargoBays, bay => bay.Data is SiloData);
        Assert.Equal(3, restoredSilo.Count(ore));
        Assert.Equal(zenith.CargoBays.Count, restored.CargoBays.Count);
    }

    // Zenith buys ore for its haul order: with the order removed, or the silo removed, it buys none.
    [Fact]
    public void ZenithBuysOnlyTheHaulOrdersGoods()
    {
        var zenith = DemoStation("trade-hub");
        var order = Assert.Single(_cache.GetAll<HaulOrderData>());
        Assert.Equal(order.Goods.Select(good => good.Key).OrderBy(good => good.Value, StringComparer.Ordinal), StationWants.Of(zenith).OrderBy(good => good.Value, StringComparer.Ordinal));
        Assert.NotEmpty(order.Goods);
        Assert.All(order.Goods, good => Assert.True(FieldKinds.IsOre(_cache, _cache.Get(good))));

        // The same station without its silo (a copy, since a placed station is active) wants nothing: its goods are its silo's.
        var copy = (OrbitalEntity) EntitySerializer.Unpack(_items, zenith.Zone, EntitySerializer.Pack(zenith));
        Assert.Equal(StationWants.Of(zenith).Count, StationWants.Of(copy).Count);
        Assert.NotNull(copy.TryUnequip(copy.CargoBays.Single(bay => bay.Data is SiloData)));
        Assert.Empty(StationWants.Of(copy));

        var key = _cache.AllStoredDocuments.Single(stored => stored.Descriptor.DocumentType == typeof(HaulOrderData)).Key;
        _cache.Commit(batch => batch.Remove(key));
        Assert.Empty(StationWants.Of(zenith));
    }

    // The staged order is a run record: Clear and a discarded Simulation remove it, and a refused stage writes none.
    [Fact]
    public void HaulOrderGoesWithTheRun()
    {
        Assert.True(RunSave.IsRunRecord(typeof(HaulOrderData)));
        var (started, simulation) = RunSave.Beside(_cache, () =>
        {
            var (_, _, _, failures) = Launch(new DemoTerminus(), Inputs(() => 1));
            Assert.True(failures.Count == 0, string.Join("; ", failures));
            return new object();
        });
        Assert.NotNull(started);
        Assert.Single(_cache.GetAll<HaulOrderData>());
        simulation.Discard(_cache);
        Assert.Empty(_cache.GetAll<HaulOrderData>());

        Launch(new DemoTerminus(), Inputs(() => 2));
        Assert.Single(_cache.GetAll<HaulOrderData>());
        RunSave.Clear(_cache);
        Assert.Empty(_cache.GetAll<HaulOrderData>());

        // A stage that queues an order and then fails writes nothing.
        var (_, _, staged, refused) = Launch(new Scripted(false, stage =>
        {
            var station = stage.Arena.Entities.OfType<OrbitalEntity>().First();
            stage.HaulOrder("refused", station, station, new[] { new CultRecordRef<ItemData>(new CultRecordKey("commodity:iron")) }, "scenario:test");
            stage.Station("no-such-kind");
            stage.Player(stage.Generated(""), float2.zero);
        }), Inputs());
        Assert.Null(staged);
        Assert.Contains(refused, failure => failure.Contains("no-such-kind"));
        Assert.DoesNotContain(refused, failure => failure.Contains("orbit"));
        Assert.Empty(_cache.GetAll<HaulOrderData>());
    }

    // Generated gear stock stops at half the stock bay: every demo station keeps at least (1 - StationStockShare) of its cells free.
    [Fact]
    public void GeneratedStockLeavesRoom()
    {
        foreach (var seed in new uint[] { 1, 2, 3, 4, 5, 69, 97 })
        {
            var (_, arena, _, failures) = Launch(new DemoTerminus(), Inputs(() => seed));
            Assert.True(failures.Count == 0, $"seed {seed}: " + string.Join("; ", failures));
            foreach (var station in arena.Entities.OfType<OrbitalEntity>().Where(entity => entity.HullData.HullType == HullType.Station))
            {
                var bay = station.CargoBays.First(candidate => !(candidate.Data is SiloData));
                var cells = bay.Data.InteriorShape.Coordinates.Length;
                var free = bay.Data.InteriorShape.Coordinates.Count(cell => bay.Occupancy[cell.x, cell.y] == null);
                var floor = cells - (int) (cells * LoadoutGenerator.StationStockShare);
                Assert.True(free >= floor, $"seed {seed} {station.Name}: {free} of {cells} cells free, at least {floor} wanted");
            }
        }
    }
}
