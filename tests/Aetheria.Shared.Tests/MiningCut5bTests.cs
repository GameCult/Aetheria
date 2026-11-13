/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CultMath;
using GameCult.Caching;
using GameCult.Caching.MessagePack;
using MessagePack;
using Xunit;

// Mining Cut 5b (docs/mining-cut-refresh.md, Cut 5, Q6b = A): a mined unit carries an Extracted lot naming the zone, the belt
// and the ore; stacks merge, split, restore and save by lot. Fixtures are Cut 3's belt scene and Cut 5a's bay and stock helpers.
public sealed class MiningCut5bTests : IDisposable
{
    private readonly MiningCut3Tests _scenes = new MiningCut3Tests();
    private readonly string _root = Path.Combine(Path.GetTempPath(), "aetheria-mining5b-" + Guid.NewGuid().ToString("N"));
    private uint _ordinal;

    public MiningCut5bTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        _scenes.Dispose();
        Directory.Delete(_root, true);
    }

    private sealed class Field
    {
        public MiningCut3Tests.Scene Scene;
        public Galaxy Galaxy;
        public SimpleCommodityData Ore;
    }

    // Two belts of rocks (size .5: 4 whole units a rock), one ore, in a galaxy of three zones.
    private Field Build()
    {
        var ore = MiningCut5aTests.Commodity("Rock");
        var kind = MiningCut3Tests.Kind("Asteroid", 4f);
        var scene = _scenes.BuildScene(new[] { kind },
            MiningCut3Tests.Belt(kind, MiningCut3Tests.Rock(150f, 0f), MiningCut3Tests.Rock(155f, .3f)),
            MiningCut3Tests.Belt(kind, MiningCut3Tests.Rock(160f, .6f)));
        MiningCut5aTests.Stock(scene, kind, (ore, 1f));
        var game = new SavedGame
        {
            Seed = 5, Factions = new CultRecordRef<Faction>[0], Relationships = new FactionRelationship[0],
            HomeZones = new Dictionary<int, int>(), BossZones = new Dictionary<int, int>(), DiscoveredZones = new[] { 0 },
            ActionBarBindings = new SavedActionBarBinding[0], Entrance = 0, Exit = -1
        };
        var zones = Enumerable.Range(0, 3)
            .Select(i => new SavedZone { Name = $"Z{i}", AdjacentZones = new int[0], Factions = new int[0], Owner = -1 }).ToArray();
        RunSave.Commit(scene.Cache, game, zones, new ProvenanceLedger());
        return new Field { Scene = scene, Galaxy = new Galaxy(scene.Cache, scene.Cache.GetGlobal<SavedGame>(), _ => { }), Ore = ore };
    }

    private static Zone ZoneOf(Field f, int index)
    {
        var settings = MiningCut3Tests.PlanetSettings();
        settings.AsteroidYield = new ExponentialLerp { Minimum = 4f, Maximum = 4f, Exponent = 1f };
        return index < 0
            ? new Zone(f.Scene.Items, settings, f.Scene.Pack, new GalaxyZone { Name = "Lone", Owner = null }, null)
            : new Zone(f.Scene.Items, settings, f.Scene.Pack, f.Galaxy.Zones[index], f.Galaxy);
    }

    private Ship Shooter(Field f, Zone zone)
    {
        var ship = new Ship(f.Scene.Items, zone, MiningCut3Tests.Mint(f.Scene, f.Scene.Hull), new EntitySettings());
        zone.Admit(ship, piloted: false);
        return ship;
    }

    private void Mine(Field f, Zone zone, Ship ship, int belt, int rock)
    {
        var chunk = new ChunkId(f.Scene.Belts[belt], rock);
        FireControl.Extract(zone, chunk, zone.Wear(chunk, 1e6f), ship, SimulationDice.For(zone.DropSeed, SimulationDice.OreStream, ++_ordinal));
    }

    private static SimpleCommodity[] Stacks(EquippedCargoBay bay) => bay.Cargo.Keys.OfType<SimpleCommodity>().OrderBy(s => s.Lot).ToArray();

    [Fact]
    public void MinedUnitsCarryAnExtractedLotNamingZoneBeltAndOre()
    {
        var f = Build();
        var zone = ZoneOf(f, 2);
        var ship = Shooter(f, zone);
        var bay = MiningCut5aTests.Bay(f.Scene, ship, 4, 4, "Hold");
        Mine(f, zone, ship, 1, 0);

        var stack = Assert.Single(Stacks(bay));
        Assert.Equal(4, stack.Quantity);
        Assert.NotEqual(0, stack.Lot);
        var lot = f.Scene.Items.GetLot(stack);
        var origin = Assert.IsType<Extracted>(lot.Origin);
        Assert.Equal(2, origin.Zone);
        Assert.Equal(f.Scene.Belts[1], origin.Body.Key);
        Assert.Equal(stack.Data.Key, origin.Commodity.Key);
        Assert.Equal(stack.Data.Key, lot.Design.Key);
        Assert.Equal(1f, lot.Quality);
        Assert.Equal(default, lot.Product);
    }

    [Fact]
    public void AZoneWithNoGalaxyIsMinus1()
    {
        var f = Build();
        var zone = ZoneOf(f, -1);
        var ship = Shooter(f, zone);
        var bay = MiningCut5aTests.Bay(f.Scene, ship, 4, 4, "Hold");
        Mine(f, zone, ship, 0, 0);
        Assert.Equal(-1, Assert.IsType<Extracted>(f.Scene.Items.GetLot(Assert.Single(Stacks(bay))).Origin).Zone);
    }

    [Fact]
    public void UnitsFromOneSourceStack_UnitsFromTwoSourcesDoNot()
    {
        var f = Build();
        var zone = ZoneOf(f, 1);
        var ship = Shooter(f, zone);
        var bay = MiningCut5aTests.Bay(f.Scene, ship, 4, 4, "Hold");
        Mine(f, zone, ship, 0, 0);
        Mine(f, zone, ship, 0, 1);
        var one = Assert.Single(Stacks(bay));
        Assert.Equal(8, one.Quantity);

        Mine(f, zone, ship, 1, 0);
        var stacks = Stacks(bay);
        Assert.Equal(new[] { 8, 4 }, stacks.Select(s => s.Quantity));
        Assert.NotEqual(stacks[0].Lot, stacks[1].Lot);
        Assert.Equal(2, f.Scene.Items.Lots.Lots.Values.Count(l => l.Origin is Extracted));
        // Two lots of one ore still total one commodity for the market and the hold.
        Assert.Equal(12, MiningCut5aTests.Units(bay, "Rock", f.Scene));
    }

    // TryFindSpace and the at-coordinate store both look for a stack to top up: a stack of another lot is not one.
    [Fact]
    public void ASearchAndAStoreIgnoreAStackOfAnotherLot()
    {
        var f = Build();
        var zone = ZoneOf(f, 0);
        var ship = Shooter(f, zone);
        var bay = MiningCut5aTests.Bay(f.Scene, ship, 1, 1, "Hold");
        var key = new CultRecordRef<ItemData>(f.Scene.Cache.RefOf(f.Ore).Key);
        SimpleCommodity Units(int lot, int quantity) => new SimpleCommodity { Data = key, Lot = lot, Quantity = quantity };

        Assert.True(bay.TryStore(Units(7, 90)));
        Assert.False(bay.TryFindSpace(Units(8, 5), out _));
        Assert.True(bay.TryFindSpace(Units(7, 5), out _));
        var other = Units(8, 5);
        Assert.False(bay.TryStore(other, new int2(0, 0)));
        Assert.Equal(90, Assert.Single(bay.Cargo.Keys.OfType<SimpleCommodity>()).Quantity);
        Assert.Equal(5, other.Quantity);
        Assert.True(bay.TryStore(Units(7, 5), new int2(0, 0)));
        Assert.Equal(95, Assert.Single(bay.Cargo.Keys.OfType<SimpleCommodity>()).Quantity);
    }

    [Fact]
    public void ASplitStackKeepsItsLot()
    {
        var f = Build();
        var zone = ZoneOf(f, 0);
        var ship = Shooter(f, zone);
        var bay = MiningCut5aTests.Bay(f.Scene, ship, 4, 4, "Hold");
        Mine(f, zone, ship, 0, 0);
        var stack = Assert.Single(Stacks(bay));

        var part = bay.Remove(stack, 3);
        Assert.Equal(3, part.Quantity);
        Assert.Equal(stack.Lot, part.Lot);
        Assert.NotEqual(0, part.Lot);
        Assert.Equal(1, stack.Quantity);
    }

    // A saved stack above its design's MaxStack comes back as several stacks, every one of the saved lot.
    [Fact]
    public void ARestoredOversizeStackKeepsItsLot()
    {
        var f = Build();
        var zone = ZoneOf(f, 0);
        var ship = Shooter(f, zone);
        var bay = MiningCut5aTests.Bay(f.Scene, ship, 4, 4, "Hold");
        for (var rock = 0; rock < 2; rock++) Mine(f, zone, ship, 0, rock);
        var lot = Assert.Single(Stacks(bay)).Lot;

        var pack = EntitySerializer.Pack(ship);
        f.Ore.MaxStack = 3;
        var restored = EntitySerializer.Unpack(f.Scene.Items, zone, pack);

        var stacks = restored.CargoBays.SelectMany(Stacks).ToArray();
        Assert.Equal(8, stacks.Sum(s => s.Quantity));
        Assert.True(stacks.Length >= 3);
        Assert.All(stacks, s => Assert.Equal(lot, s.Lot));
    }

    private CultCache Open() => AetheriaStores.Open(Path.Combine(_root, "Aetheria.cc"), Path.Combine(_root, "run.cc"), Path.Combine(_root, "player.cc"));

    private static ShipPack PackHolding(int hullLot, params ItemInstance[] stacks) => new ShipPack
    {
        Name = "ship", Hull = new EquippableItem { Lot = hullLot },
        Equipment = Array.Empty<(int2, EquippableItem)>(), CargoBays = Array.Empty<(int2, EquippableItem)>(),
        DockingBays = Array.Empty<(int2, EquippableItem)>(),
        CargoContents = new[] { stacks.Select((s, i) => (new int2(i, 0), s)).ToArray() },
        DockingBayContents = Array.Empty<(int2, ItemInstance)[]>(), Children = Array.Empty<EntityPack>(),
        Temperature = new float[0, 0], Armor = new float[0, 0], Conductivity = new bool2[0, 0],
        DockingBayAssignments = Array.Empty<int>(), Settings = new EntitySettings(), WeaponGroups = Array.Empty<int[]>()
    };

    // The ship's hull is a crafted item and roots like any other, so the fixture gives it a lot of its own.
    private int _hullLot;

    // The ledger as the run file holds it after a save of ships holding `held`.
    private ProvenanceLedger SaveAndReload(ProvenanceLedger live, params ItemInstance[] held)
    {
        _hullLot = live.Add(new Lot { Design = default, Origin = new Attributed(), Quality = 1f, Roles = new List<RoleFill>() });
        using (var cache = AetheriaStores.Open(Path.Combine(_root, "Aetheria.cc"), catalogWritable: true))
        {
            cache.Commit(batch => batch.Upsert(new VerseGrammar { Revision = 1 }));
        }
        using (var cache = Open())
        {
            var game = new SavedGame
            {
                Factions = new CultRecordRef<Faction>[0], Relationships = new FactionRelationship[0],
                HomeZones = new Dictionary<int, int>(), BossZones = new Dictionary<int, int>(), DiscoveredZones = new[] { 0 },
                ActionBarBindings = new SavedActionBarBinding[0], Exit = -1
            };
            var zone = new SavedZone
            {
                Name = "Zone 0", AdjacentZones = Array.Empty<int>(), Factions = Array.Empty<int>(), Owner = -1,
                Contents = new ZonePack { Entities = new List<EntityPack> { PackHolding(_hullLot, held) } }
            };
            RunSave.Commit(cache, game, new[] { zone }, live);
        }
        using (var cache = Open()) return RunSave.Lots(cache);
    }

    // An ore the draw never picks mints no lot: the ledger gains nothing for it, only for the ore that was drawn.
    [Fact]
    public void AnOreThatDrawsZeroUnitsMintsNoLot()
    {
        var f = Build();
        var dud = MiningCut5aTests.Commodity("Dud");
        MiningCut5aTests.Stock(f.Scene, f.Scene.Kinds[0], (f.Ore, 1f), (dud, 1e-12f));
        var zone = ZoneOf(f, 0);
        var ship = Shooter(f, zone);
        var bay = MiningCut5aTests.Bay(f.Scene, ship, 4, 4, "Hold");
        var before = f.Scene.Items.Lots.Lots.Count;
        Mine(f, zone, ship, 0, 0);

        Assert.Equal(4, MiningCut5aTests.Units(bay, "Rock", f.Scene));
        Assert.Equal(0, MiningCut5aTests.Units(bay, "Dud", f.Scene));
        Assert.Equal(before + 1, f.Scene.Items.Lots.Lots.Count);
        Assert.Equal(1, f.Scene.Items.Lots.Lots.Values.Count(l => l.Origin is Extracted));
    }

    // A crafted item with no lot is corruption and the save says so; a SimpleCommodity at Lot 0 is ordinary stock.
    [Fact]
    public void ACraftedItemAtLotZeroFailsTheSaveLoudly()
    {
        var f = Build();
        var crafted = new EquippableItem { Data = f.Scene.Cache.RefOf<ItemData>(f.Scene.Hull), Lot = 0 };
        var thrown = Assert.Throws<InvalidOperationException>(() => SaveAndReload(new ProvenanceLedger(), crafted));
        Assert.Contains("Lot 0 is not in the provenance ledger", thrown.Message);

        var stock = new SimpleCommodity { Data = new CultRecordRef<ItemData>(f.Scene.Cache.RefOf(f.Ore).Key), Quantity = 3, Lot = 0 };
        var savedStock = SaveAndReload(new ProvenanceLedger(), stock);
        Assert.Equal(new[] { _hullLot }, savedStock.Lots.Keys);
    }

    [Fact]
    public void AMinedStackSurvivesSaveGc()
    {
        var f = Build();
        var zone = ZoneOf(f, 1);
        var ship = Shooter(f, zone);
        var bay = MiningCut5aTests.Bay(f.Scene, ship, 4, 4, "Hold");
        Mine(f, zone, ship, 0, 0);
        Mine(f, zone, ship, 1, 0);
        var held = Stacks(bay);
        Assert.Equal(2, held.Length);
        // A third lot nobody holds: floated and gone, or never collected.
        var orphan = f.Scene.Items.ExtractedLot(1, new CultRecordKey("elsewhere"), f.Ore);

        var saved = SaveAndReload(f.Scene.Items.Lots, held);

        Assert.Equal(held.Select(s => s.Lot).Append(_hullLot).OrderBy(i => i), saved.Lots.Keys.OrderBy(i => i));
        Assert.DoesNotContain(orphan, saved.Lots.Keys);
        var origin = Assert.IsType<Extracted>(saved[held[0].Lot].Origin);
        Assert.Equal(1, origin.Zone);
        Assert.Equal(f.Scene.Belts[0], origin.Body.Key);
    }

    [Fact]
    public void TheLotIndexIsRebuiltOnContinue()
    {
        var f = Build();
        var zone = ZoneOf(f, 1);
        var ship = Shooter(f, zone);
        var bay = MiningCut5aTests.Bay(f.Scene, ship, 4, 4, "Hold");
        Mine(f, zone, ship, 0, 0);
        var held = Assert.Single(Stacks(bay));

        var saved = SaveAndReload(f.Scene.Items.Lots, held);
        var continued = new ItemManager(1u, f.Scene.Cache, saved, RunSaveTests.TestSettings(), _ => { });

        Assert.Equal(held.Lot, continued.ExtractedLot(1, f.Scene.Belts[0], f.Ore));
        Assert.Equal(2, saved.Lots.Count); // the mined lot and the hull
        var next = continued.ExtractedLot(1, f.Scene.Belts[1], f.Ore);
        Assert.NotEqual(held.Lot, next);
        Assert.Equal(3, saved.Lots.Count);
        // Zone and ore tell lots apart as the belt does.
        Assert.NotEqual(next, continued.ExtractedLot(2, f.Scene.Belts[1], f.Ore));
    }

    // A save from before the field reached SimpleCommodity: Data, Rotation, Quantity and nothing else.
    [Fact]
    public void OldSavesReadLotZero()
    {
        var f = Build();
        var options = CultDocumentMessagePackSerialization.OptionsFor(typeof(ItemInstance).Assembly);
        var key = new CultRecordRef<ItemData>(f.Scene.Cache.RefOf(f.Ore).Key);
        var buffer = new System.Buffers.ArrayBufferWriter<byte>();
        var writer = new MessagePackWriter(buffer);
        writer.WriteArrayHeader(2);
        writer.Write(0); // Union(0, SimpleCommodity)
        writer.WriteArrayHeader(3);
        MessagePackSerializer.Serialize(ref writer, key, options);
        MessagePackSerializer.Serialize(ref writer, ItemRotation.None, options);
        writer.Write(7);
        writer.Flush();

        var old = Assert.IsType<SimpleCommodity>(MessagePackSerializer.Deserialize<ItemInstance>(buffer.WrittenMemory, options));
        Assert.Equal(7, old.Quantity);
        Assert.Equal(0, old.Lot);
        Assert.Equal(key.Key, old.Data.Key);

        // A lot rides the wire; a Lot-0 stack is no root, so the old stack saves no lot.
        var back = MessagePackSerializer.Deserialize<ItemInstance>(MessagePackSerializer.Serialize<ItemInstance>(
            new SimpleCommodity { Data = key, Quantity = 2, Lot = 9 }, options), options);
        Assert.Equal(9, back.Lot);
        var savedOld = SaveAndReload(new ProvenanceLedger(), old);
        Assert.Equal(new[] { _hullLot }, savedOld.Lots.Keys); // the hull only
    }
}
