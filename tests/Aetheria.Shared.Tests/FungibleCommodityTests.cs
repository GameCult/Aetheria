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

// Ruling aetheria-living-world:ruling:simple-commodities-fungible (supersedes Mining Cut 5b, Q6b A): a simple commodity carries
// no lot, so units of one design merge whatever their source, Extract mints nothing, and a save from before the ruling still
// reads. Fixtures are Cut 3's belt scene and Cut 5a's bay and stock helpers.
public sealed class FungibleCommodityTests : IDisposable
{
    private readonly MiningCut3Tests _scenes = new MiningCut3Tests();
    private readonly string _root = Path.Combine(Path.GetTempPath(), "aetheria-mining5b-" + Guid.NewGuid().ToString("N"));
    private uint _ordinal;

    public FungibleCommodityTests() => Directory.CreateDirectory(_root);

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
            InfluenceScale = 1,
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

    private static SimpleCommodity[] Stacks(EquippedCargoBay bay) => bay.Cargo.Keys.OfType<SimpleCommodity>().OrderBy(s => s.Quantity).ToArray();

    private static SimpleCommodity Units(Field f, SimpleCommodityData design, int quantity) =>
        new SimpleCommodity { Data = new CultRecordRef<ItemData>(f.Scene.Cache.RefOf(design).Key), Quantity = quantity };

    [Fact]
    public void UnitsMinedFromTwoBeltsAndTwoZonesMergeIntoOneStack()
    {
        var f = Build();
        var zone = ZoneOf(f, 1);
        var ship = Shooter(f, zone);
        var bay = MiningCut5aTests.Bay(f.Scene, ship, 4, 4, "Hold");
        Mine(f, zone, ship, 0, 0);
        Mine(f, zone, ship, 1, 0);
        Assert.Equal(8, Assert.Single(Stacks(bay)).Quantity);

        var otherZone = ZoneOf(f, 2);
        var other = Shooter(f, otherZone);
        var otherBay = MiningCut5aTests.Bay(f.Scene, other, 4, 4, "Hold");
        Mine(f, otherZone, other, 0, 1);
        Assert.True(bay.TryStore(otherBay.Remove(Assert.Single(Stacks(otherBay)), 4)));
        Assert.Equal(12, Assert.Single(Stacks(bay)).Quantity);
    }

    [Fact]
    public void BoughtAndMinedUnitsOfOneDesignMerge()
    {
        var f = Build();
        var zone = ZoneOf(f, 0);
        var ship = Shooter(f, zone);
        var bay = MiningCut5aTests.Bay(f.Scene, ship, 4, 4, "Hold");
        Assert.True(bay.TryStore(Units(f, f.Ore, 5)));
        Mine(f, zone, ship, 0, 0);
        Assert.Equal(9, Assert.Single(Stacks(bay)).Quantity);

        var floating = zone.Release(Units(f, f.Ore, 4), new float2(0, 0), new float2(0, 0));
        Assert.True(zone.TryPickUp(ship, floating.Id));
        Assert.Equal(13, Assert.Single(Stacks(bay)).Quantity);
    }

    [Fact]
    public void TwoDesignsNeverMerge()
    {
        var f = Build();
        var zone = ZoneOf(f, 0);
        var ship = Shooter(f, zone);
        var shells = MiningCut5aTests.Commodity("Shells");
        f.Scene.Cache.Upsert(shells);
        var bay = MiningCut5aTests.Bay(f.Scene, ship, 4, 4, "Hold");
        Assert.True(bay.TryStore(Units(f, f.Ore, 5)));
        Assert.True(bay.TryStore(Units(f, shells, 5)));
        Assert.Equal(2, Stacks(bay).Length);

        var small = MiningCut5aTests.Bay(f.Scene, ship, 1, 1, "Small");
        Assert.True(small.TryStore(Units(f, f.Ore, 90)));
        Assert.False(small.TryFindSpace(Units(f, shells, 5), out _));
        Assert.True(small.TryFindSpace(Units(f, f.Ore, 5), out _));
        var other = Units(f, shells, 5);
        Assert.False(small.TryStore(other, new int2(0, 0)));
        Assert.Equal(90, Assert.Single(Stacks(small)).Quantity);
        Assert.True(small.TryStore(Units(f, f.Ore, 5), new int2(0, 0)));
        Assert.Equal(95, Assert.Single(Stacks(small)).Quantity);
    }

    [Fact]
    public void ExtractAddsNothingToTheLedger()
    {
        var f = Build();
        var zone = ZoneOf(f, 0);
        var ship = Shooter(f, zone);
        MiningCut5aTests.Bay(f.Scene, ship, 4, 4, "Hold");
        var lots = f.Scene.Items.Lots;
        var (count, next) = (lots.Lots.Count, lots.NextLot);
        Mine(f, zone, ship, 0, 0);
        Mine(f, zone, ship, 1, 0);
        Assert.Equal((count, next), (lots.Lots.Count, lots.NextLot));
    }

    [Fact]
    public void ASplitStackIsTheSameDesign()
    {
        var f = Build();
        var zone = ZoneOf(f, 0);
        var ship = Shooter(f, zone);
        var bay = MiningCut5aTests.Bay(f.Scene, ship, 4, 4, "Hold");
        Mine(f, zone, ship, 0, 0);
        var stack = Assert.Single(Stacks(bay));
        var part = bay.Remove(stack, 3);
        Assert.Equal((3, 1), (part.Quantity, stack.Quantity));
        Assert.Equal(stack.Data.Key, part.Data.Key);
    }

    // A saved stack above its design's MaxStack comes back as several stacks of the one design.
    [Fact]
    public void ARestoredOversizeStackSplitsByDesign()
    {
        var f = Build();
        var zone = ZoneOf(f, 0);
        var ship = Shooter(f, zone);
        var bay = MiningCut5aTests.Bay(f.Scene, ship, 4, 4, "Hold");
        for (var rock = 0; rock < 2; rock++) Mine(f, zone, ship, 0, rock);

        var pack = EntitySerializer.Pack(ship);
        f.Ore.MaxStack = 3;
        var restored = EntitySerializer.Unpack(f.Scene.Items, zone, pack);

        var stacks = restored.CargoBays.SelectMany(Stacks).ToArray();
        Assert.Equal(8, stacks.Sum(s => s.Quantity));
        Assert.True(stacks.Length >= 3);
        Assert.All(stacks, s => Assert.Equal(f.Scene.Cache.RefOf(f.Ore).Key, s.Data.Key));
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
                InfluenceScale = 1,
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

    // A crafted item with no lot is corruption and the save says so; a simple commodity has no lot and roots nothing.
    [Fact]
    public void ACraftedItemAtLotZeroFailsTheSaveLoudly()
    {
        var f = Build();
        var crafted = new EquippableItem { Data = f.Scene.Cache.RefOf<ItemData>(f.Scene.Hull), Lot = 0 };
        var thrown = Assert.Throws<InvalidOperationException>(() => SaveAndReload(new ProvenanceLedger(), crafted));
        Assert.Contains("Lot 0 is not in the provenance ledger", thrown.Message);

        var stock = new SimpleCommodity { Data = new CultRecordRef<ItemData>(f.Scene.Cache.RefOf(f.Ore).Key), Quantity = 3 };
        var savedStock = SaveAndReload(new ProvenanceLedger(), stock);
        Assert.Equal(new[] { _hullLot }, savedStock.Lots.Keys);
    }

    private static byte[] PreRulingStack(CultRecordRef<ItemData> key, MessagePackSerializerOptions options)
    {
        var buffer = new System.Buffers.ArrayBufferWriter<byte>();
        var writer = new MessagePackWriter(buffer);
        writer.WriteArrayHeader(2);
        writer.Write(0); // Union(0, SimpleCommodity)
        writer.WriteArrayHeader(12);
        MessagePackSerializer.Serialize(ref writer, key, options);
        MessagePackSerializer.Serialize(ref writer, ItemRotation.None, options);
        writer.Write(7); // Quantity
        for (var slot = 3; slot < 11; slot++) writer.WriteNil();
        writer.Write(9); // slot 11: the lot a pre-ruling mined stack named
        writer.Flush();
        return buffer.WrittenSpan.ToArray();
    }

    // A save from before the ruling: a three-slot stack from before SimpleCommodity had a lot, and a twelve-slot stack
    // whose slot 11 names one. Both read as a plain stack, and merge with a fresh stack of their design.
    [Fact]
    public void APreRulingStackReadsAndMerges()
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
        Assert.Equal((7, key.Key), (old.Quantity, old.Data.Key));

        var lotted = Assert.IsType<SimpleCommodity>(MessagePackSerializer.Deserialize<ItemInstance>(PreRulingStack(key, options), options));
        Assert.Equal((7, key.Key), (lotted.Quantity, lotted.Data.Key));

        var zone = ZoneOf(f, 0);
        var ship = Shooter(f, zone);
        var bay = MiningCut5aTests.Bay(f.Scene, ship, 1, 1, "Hold");
        Assert.True(bay.TryStore(Units(f, f.Ore, 5)));
        Assert.True(bay.TryStore(lotted));
        Assert.Equal(12, Assert.Single(Stacks(bay)).Quantity);
    }

    // A run ledger saved before the ruling holds mined-source lots (Provenance arm 2: zone, commodity, belt). They read as
    // RetiredExtracted, and a save drops them: no item roots them.
    [Fact]
    public void APreRulingExtractedLotLoadsAndCommitDropsIt()
    {
        var f = Build();
        var options = CultDocumentMessagePackSerialization.OptionsFor(typeof(ItemInstance).Assembly);
        var buffer = new System.Buffers.ArrayBufferWriter<byte>();
        var writer = new MessagePackWriter(buffer);
        writer.WriteArrayHeader(2);
        writer.Write(2);
        writer.WriteArrayHeader(3);
        writer.Write(1);
        MessagePackSerializer.Serialize(ref writer, new CultRecordRef<SimpleCommodityData>(f.Scene.Cache.RefOf(f.Ore).Key), options);
        MessagePackSerializer.Serialize(ref writer, new CultRecordRef<BodyData>(f.Scene.Belts[0]), options);
        writer.Flush();
        var origin = Assert.IsType<RetiredExtracted>(MessagePackSerializer.Deserialize<Provenance>(buffer.WrittenMemory, options));

        var ledger = new ProvenanceLedger();
        var retired = ledger.Add(new Lot { Design = default, Origin = origin, Quality = 1f, Roles = new List<RoleFill>() });
        var saved = SaveAndReload(ledger);
        Assert.DoesNotContain(retired, saved.Lots.Keys);
        Assert.Equal(new[] { _hullLot }, saved.Lots.Keys);
    }
}
