/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using CultMath;
using GameCult.Caching;
using GameCult.Caching.MessagePack;
using UniRx;
using Xunit;

// The demo's goal (aetheria-release, cut boss-gate): the boss zone spawns its faction's boss, and the exit gate opens
// only when that boss is dead. Every input is the production one: the demo galaxy, GenerateZone, the Zone death path,
// and the run save.
public sealed partial class RunStartTests
{
    private ZonePack BossZonePack(Galaxy galaxy, ItemManager items, GalaxyZone zone, bool ambient = true, uint seed = 7)
    {
        items.Random = new CultMath.Random(seed);
        return ZoneGenerator.GenerateZone(items, _zoneSettings, _planetSettings, galaxy, zone, galaxy.IsPrelude, ambient);
    }

    private Zone ZoneOf(Galaxy galaxy, GalaxyZone zone) =>
        new Zone(_items, _planetSettings, BossZonePack(galaxy, _items, zone), zone, galaxy);

    private static Faction Zhestokost(Galaxy galaxy) => galaxy.Factions.Single(faction => faction.Name == "Zhestokost");

    private ItemManager LoggingItems(List<string> log) =>
        new ItemManager(_cache, new ProvenanceLedger(), AuthoredSettings.Load(FindRepoRoot()).Read<GameplaySettings>("GameplaySettings"), log.Add);

    private static string HullName(ItemManager items, EntityPack pack) => items.GetData(pack.Hull).Name;

    private HullData[] SoldShipHulls() => _cache.GetAll<FactionProductData>()
        .Where(product => product.Manufacturer.IsSet())
        .Select(product => _cache.Get(product.Design) as HullData)
        .Where(hull => hull != null && hull.HullType == HullType.Ship && hull.Price > 0 && !hull.Boss)
        .Distinct().ToArray();

    // A Boss hull a faction makes, by the production path: a copy of a sold ship hull (so it builds into a real ship) flagged
    // Boss under its own name and price, and one copy of that hull's product per maker. A copy rather than the shipped
    // hull itself, because flagging a shipped hull would make it a Boss hull of every maker the catalog gives it.
    private HullData MakeBoss(string name, int price, params Faction[] makers) => MakeBoss(HullType.Ship, name, price, makers);

    private HullData MakeBoss(HullType type, string name, int price, params Faction[] makers)
    {
        var options = CultDocumentMessagePackSerialization.OptionsFor(typeof(HullData).Assembly);
        T Copy<T>(T document) => MessagePack.MessagePackSerializer.Deserialize<T>(MessagePack.MessagePackSerializer.Serialize(document, options), options);

        var like = SoldShipHulls().OrderBy(hull => hull.Name, StringComparer.Ordinal).First();
        var likeProduct = _cache.GetAll<FactionProductData>().OrderBy(product => product.Name, StringComparer.Ordinal)
            .First(product => _cache.Get(product.Design) == like);
        var boss = Copy(like);
        boss.Name = name;
        boss.Price = price;
        boss.HullType = type;
        boss.Boss = true;
        boss.Prefab = null;
        boss.Visual = default;
        var bossRef = _cache.Upsert(boss);
        foreach (var maker in makers)
        {
            var product = Copy(likeProduct);
            product.Name = $"{name} by {maker.Name}";
            product.Design = new CultRecordRef<CraftedItemData>(bossRef.Key);
            product.Manufacturer = _cache.RefOf(maker);
            _cache.Upsert(product);
        }
        return _cache.Get(bossRef);
    }

    // A boss's fixed Loadout (ruling boss-gear-is-a-loadout), by the production capture: a generated fit of the hull MakeBoss
    // copied, captured as a Loadout (Loadouts.Capture), cut down to its weapons so it is smaller than any generated fit (the
    // tractor pickup and the rest are not in it), moved onto the boss hull and the maker's product of it, and stored at
    // Loadouts.KeyOf(name), the key Loadouts.Commit writes. openIf leaves the first slot whose design satisfies it design-only.
    // The shipped catalog has no Boss hull, so this and MakeBoss are fixtures.
    private Loadout MakeBossLoadout(HullData boss, Faction maker, string name, Func<EquippableItemData, bool> openIf = null)
    {
        var like = SoldShipHulls().OrderBy(hull => hull.Name, StringComparer.Ordinal).First();
        var random = new CultMath.Random(11);
        var generator = new LoadoutGenerator(ref random, _items, null, null, null, .5f);
        var ship = (Ship) EntitySerializer.Unpack(_items, null, generator.GenerateShipLoadout(hull => hull == like));
        var fit = Loadouts.Capture(_items, ship, name);
        var armed = fit.Slots.Select((slot, index) => (slot, index))
            .Where(entry => _cache.Get(entry.slot.Design).Behaviors.Any(behavior => behavior is WeaponData)).ToArray();
        Assert.NotEmpty(armed);
        Assert.True(armed.Length < fit.Slots.Count, "the generated fit needs gear besides weapons for this fixture to tell a Loadout from a fit");
        var renumbered = armed.Select((entry, number) => (entry.index, number)).ToDictionary(entry => entry.index, entry => entry.number);
        fit.WeaponGroups = fit.WeaponGroups.Select(group => group.Where(renumbered.ContainsKey).Select(index => renumbered[index]).ToArray()).ToArray();
        fit.Slots = armed.Select(entry => entry.slot).ToList();
        if (openIf != null) fit.Slots.First(slot => openIf(_cache.Get(slot.Design))).Product = default;
        fit.Hull = _cache.RefOf(boss);
        fit.HullProduct = _cache.RefOf(_cache.GetAll<FactionProductData>().Single(product => _cache.Get(product.Design) == boss && _cache.Get(product.Manufacturer) == maker));
        StoreLoadout(fit);
        return fit;
    }

    // A Loadout record at Loadouts.KeyOf(Name), the key Loadouts.Commit writes it under.
    private void StoreLoadout(Loadout loadout) =>
        Assert.True(_cache.Commit(batch => batch.Upsert(typeof(Loadout), loadout, Loadouts.KeyOf(loadout.Name))));

    // The demo's antagonist gets a Boss hull and its Loadout: the shipped catalog has neither.
    private HullData GiveAntagonistABoss(Galaxy galaxy)
    {
        var antagonist = Zhestokost(galaxy);
        var boss = MakeBoss("Antagonist Boss", 100, antagonist);
        MakeBossLoadout(boss, antagonist, "Antagonist Boss loadout");
        return boss;
    }

    // What a ship carries, cell by cell, as a Loadout would name it: position, rotation, design, product.
    private static string[] Cells(ItemManager items, EntityPack pack)
    {
        var captured = Loadouts.Capture(items, (Ship) EntitySerializer.Unpack(items, null, pack), "probe");
        return captured.Slots.OrderBy(slot => slot.Position.x).ThenBy(slot => slot.Position.y)
            .Select(slot => $"{slot.Position.x},{slot.Position.y},{slot.Rotation},{items.ItemData.Get(slot.Design).Name},{(slot.Product.IsSet() ? items.ItemData.Get(slot.Product).Name : "-")}").ToArray();
    }

    private static Faction FactionNamed(Galaxy galaxy, string name) => galaxy.Factions.Single(faction => faction.Name == name);

    private HullData PriciestSold() =>
        SoldShipHulls().OrderByDescending(hull => hull.Price).ThenBy(hull => hull.Name, StringComparer.Ordinal).First();

    private LoadoutGenerator GeneratorFor(Galaxy galaxy, ItemManager items, Faction faction, uint seed)
    {
        var random = new CultMath.Random(seed);
        return new LoadoutGenerator(ref random, items, galaxy, galaxy.Exit, faction, .5f);
    }

    // BossOf owns the boss. Zhestokost makes Boss A, B and C and a decoy ordinary hull. Loadouts sit on the decoy's product,
    // on Boss B's and C's, and two on Boss A's: only Boss A's, first by name, is the boss's, whatever order the names fall in.
    [Fact]
    public void Boss_flies_its_factions_boss_loadout()
    {
        var galaxy = DemoGalaxy(1);
        var antagonist = Zhestokost(galaxy);
        var cheapest = SoldShipHulls().Min(hull => hull.Price);
        var priciest = PriciestSold();
        Assert.True(cheapest < priciest.Price, "the catalog needs two ship hulls of different price for this test to tell them apart");

        // B and A tie on price, C is cheaper: the boss is the dearest Boss hull, ties by name.
        var decoy = new FactionProductData { Name = "Decoy", Design = new CultRecordRef<CraftedItemData>(_cache.RefOf(priciest).Key), Manufacturer = _cache.RefOf(antagonist) };
        var decoyRef = _cache.Upsert(decoy);
        var b = MakeBoss("Boss B", cheapest + 10, antagonist);
        var c = MakeBoss("Boss C", cheapest, antagonist);
        var a = MakeBoss("Boss A", cheapest + 10, antagonist);
        MakeBossLoadout(b, antagonist, "Aaa loadout of Boss B");
        MakeBossLoadout(c, antagonist, "Aab loadout of Boss C");
        MakeBossLoadout(a, antagonist, "Zz second loadout of Boss A");
        MakeBossLoadout(a, antagonist, "Zz first loadout of Boss A");
        StoreLoadout(new Loadout
        {
            Name = "Aac loadout of the decoy", Hull = _cache.RefOf(priciest),
            HullProduct = new CultRecordRef<FactionProductData>(decoyRef.Key)
        });

        Assert.Equal(new[] { "Boss A", "Boss B", "Boss C" }, LoadoutGenerator.BossHullsOf(_cache, antagonist).Select(entry => entry.hull.Name));
        var loadout = Loadouts.BossOf(_cache, antagonist, out var refusal);
        Assert.Equal(BossRefusal.None, refusal);
        Assert.Equal("Zz first loadout of Boss A", loadout.Name);

        var log = new List<string>();
        var items = LoggingItems(log);
        var boss = Assert.Single(BossZonePack(galaxy, items, galaxy.Exit).Entities, entity => entity.Boss);
        Assert.Equal("Boss A", HullName(items, boss));
        Assert.DoesNotContain(log, line => line.Contains("makes no boss hull"));
    }

    // Ruling boss-gear-is-a-loadout: the boss carries its Loadout's cells and nothing else. The generated fit of the same hull
    // carries more (a tractor pickup, consumables); none of it is on the boss. No generator draw decides it: another seed gives
    // the same cells. A design-only slot takes its brand from the context's ChooseProduct (ruling
    // loadout-product-optional-context-chooses); a pinned one keeps the product it names.
    [Fact]
    public void Boss_carries_exactly_its_loadouts_cells()
    {
        var galaxy = DemoGalaxy(1);
        var antagonist = Zhestokost(galaxy);
        var hull = MakeBoss("Boss A", 100, antagonist);
        var loadout = MakeBossLoadout(hull, antagonist, "Boss A loadout",
            openIf: design => GeneratorFor(galaxy, LoggingItems(new List<string>()), antagonist, 1).ChooseProduct(design) != null);
        Assert.True(loadout.Slots.Count > 1);
        var open = Assert.Single(loadout.Slots, slot => !slot.Product.IsSet());

        string Cell(LoadoutSlot slot, string brand) => $"{slot.Position.x},{slot.Position.y},{slot.Rotation},{_cache.Get(slot.Design).Name},{brand}";
        var pinned = loadout.Slots.Where(slot => slot != open).Select(slot => Cell(slot, _cache.Get(slot.Product).Name)).OrderBy(x => x, StringComparer.Ordinal).ToArray();
        var brands = _cache.GetAll<FactionProductData>().Where(product => product.Design.Key.Equals(open.Design.Key)).Select(product => product.Name).ToArray();

        var items = LoggingItems(new List<string>());
        var boss = Assert.Single(BossZonePack(galaxy, items, galaxy.Exit).Entities, entity => entity.Boss);
        var cells = Cells(items, boss);
        Assert.Equal(loadout.Slots.Count, cells.Length);
        Assert.Equal(pinned, cells.Where(cell => !cell.StartsWith($"{open.Position.x},{open.Position.y},")).OrderBy(x => x, StringComparer.Ordinal));
        var openCell = Assert.Single(cells, cell => cell.StartsWith($"{open.Position.x},{open.Position.y},"));
        Assert.StartsWith(Cell(open, ""), openCell);
        Assert.Contains(openCell[(openCell.LastIndexOf(',') + 1)..], brands);

        var captured = Loadouts.Capture(items, (Ship) EntitySerializer.Unpack(items, null, boss), "probe");
        Assert.Equal(_cache.Get(loadout.HullProduct).Name, _cache.Get(captured.HullProduct).Name);
        Assert.Equal(hull.Name, _cache.Get(captured.Hull).Name);
        string[][] Groups(Loadout l) => l.WeaponGroups.Select(group => group.Select(i => $"{l.Slots[i].Position.x},{l.Slots[i].Position.y}").OrderBy(x => x, StringComparer.Ordinal).ToArray()).ToArray();
        Assert.Equal(Groups(loadout).Where(group => group.Length > 0), Groups(captured).Where(group => group.Length > 0));

        // No generator draw decides the gear: another seed builds the same pinned cells. Only the open slot's brand is the context's pick.
        var otherItems = LoggingItems(new List<string>());
        var other = Assert.Single(BossZonePack(galaxy, otherItems, galaxy.Exit, seed: 8).Entities, entity => entity.Boss);
        var otherCells = Cells(otherItems, other);
        Assert.Equal(pinned, otherCells.Where(cell => !cell.StartsWith($"{open.Position.x},{open.Position.y},")).OrderBy(x => x, StringComparer.Ordinal));
    }

    // No generated fallback: the shipped catalog has no Boss hull, so Zhestokost has no boss, however many ordinary hulls it can be offered.
    [Fact]
    public void Boss_without_a_boss_hull_is_refused()
    {
        var galaxy = DemoGalaxy(1);
        var antagonist = Zhestokost(galaxy);
        Assert.Null(Loadouts.BossOf(_cache, antagonist, out var refusal));
        Assert.Equal(BossRefusal.NoBossHull, refusal);

        var log = new List<string>();
        var items = LoggingItems(log);
        var pack = BossZonePack(galaxy, items, galaxy.Exit);
        Assert.False(pack.BossSpawned);
        Assert.DoesNotContain(pack.Entities, entity => entity.Boss);
        Assert.Contains(log, line => line.Contains("Zhestokost") && line.Contains("HullData.Boss"));
        Assert.Contains(log, line => line.Contains("Galaxy.Exit") && line.Contains("exit-without-boss"));
        Assert.DoesNotContain(log, line => line.Contains("makes no boss hull"));
        Assert.Empty(LoadoutGenerator.BossHullsOf(_cache, null));
    }

    // A Boss hull whose product has no Loadout is refused typed at the derivation. The refusal names the missing field and
    // echoes no input: the canary hull name, its product's name and the record keys reach no log line.
    [Fact]
    public void A_boss_hull_without_a_loadout_is_refused()
    {
        var galaxy = DemoGalaxy(1);
        var antagonist = Zhestokost(galaxy);
        var hull = MakeBoss("Canary7f3a hull", 100, antagonist);
        var product = _cache.GetAll<FactionProductData>().Single(p => _cache.Get(p.Design) == hull);
        // A Loadout on another maker's boss product is not this boss's.
        var lucent = FactionNamed(galaxy, "Lucent Media");
        MakeBossLoadout(MakeBoss("Other boss", 50, lucent), lucent, "Other boss loadout");

        Assert.Null(Loadouts.BossOf(_cache, antagonist, out var refusal));
        Assert.Equal(BossRefusal.NoLoadout, refusal);
        var described = Loadouts.Describe(refusal);
        Assert.Contains("HullProduct", described);

        var log = new List<string>();
        var items = LoggingItems(log);
        var pack = BossZonePack(galaxy, items, galaxy.Exit);
        Assert.False(pack.BossSpawned);
        Assert.DoesNotContain(pack.Entities, entity => entity.Boss);
        Assert.Contains(log, line => line.Contains("Zhestokost") && line.Contains("HullProduct"));
        foreach (var canary in new[] { "Canary7f3a", product.Name, _cache.RefOf(product).Key.Value, _cache.RefOf(hull).Key.Value })
        {
            Assert.DoesNotContain(log, line => line.Contains(canary));
            Assert.DoesNotContain(canary, described);
        }
    }

    // The key is the product, not the hull: a Boss hull both makers sell, with a Loadout on Lucent Media's product only.
    [Fact]
    public void A_loadout_on_another_makers_product_is_not_this_bosses()
    {
        var galaxy = DemoGalaxy(1);
        var antagonist = Zhestokost(galaxy);
        var lucent = FactionNamed(galaxy, "Lucent Media");
        var shared = MakeBoss("Shared Boss", 100, antagonist, lucent);
        var loadout = MakeBossLoadout(shared, lucent, "Aaa Lucent only");

        Assert.Equal(loadout.Name, Loadouts.BossOf(_cache, lucent, out var lucentRefusal).Name);
        Assert.Equal(BossRefusal.None, lucentRefusal);
        Assert.Null(Loadouts.BossOf(_cache, antagonist, out var refusal));
        Assert.Equal(BossRefusal.NoLoadout, refusal);
        Assert.False(BossZonePack(galaxy, LoggingItems(new List<string>()), galaxy.Exit).BossSpawned);
    }

    [Fact]
    public void Another_factions_boss_hull_is_never_used()
    {
        var galaxy = DemoGalaxy(1);
        var antagonist = Zhestokost(galaxy);
        var lucent = FactionNamed(galaxy, "Lucent Media");
        // Dearer than every ordinary hull, and Lucent's alone, with its own Loadout: Zhestokost neither derives it nor falls back to it.
        var luxury = MakeBoss("Lucent Flagship", PriciestSold().Price + 1000, lucent);
        MakeBossLoadout(luxury, lucent, "Lucent Flagship loadout");

        var log = new List<string>();
        var items = LoggingItems(log);
        var pack = BossZonePack(galaxy, items, galaxy.Exit);
        Assert.False(pack.BossSpawned);
        Assert.DoesNotContain(pack.Entities, entity => entity.Boss);
        Assert.Contains(log, line => line.Contains("Zhestokost") && line.Contains("HullData.Boss"));
        Assert.Null(Loadouts.BossOf(_cache, antagonist, out var refusal));
        Assert.Equal(BossRefusal.NoBossHull, refusal);
        Assert.Empty(LoadoutGenerator.BossHullsOf(_cache, antagonist));
        Assert.Same(luxury, Assert.Single(LoadoutGenerator.BossHullsOf(_cache, lucent)).hull);
        Assert.Equal("Lucent Flagship loadout", Loadouts.BossOf(_cache, lucent, out _).Name);
    }

    [Fact]
    public void A_boss_hull_two_makers_sell_is_a_boss_of_each()
    {
        var galaxy = DemoGalaxy(1);
        var antagonist = Zhestokost(galaxy);
        var lucent = FactionNamed(galaxy, "Lucent Media");
        var shared = MakeBoss("Shared Boss", PriciestSold().Price - 1, antagonist, lucent);
        // A Boss station hull the antagonist also makes is no boss: a boss is a ship.
        MakeBoss(HullType.Station, "Boss Station", PriciestSold().Price + 1, antagonist);
        MakeBossLoadout(shared, lucent, "Aaa loadout of Lucent");
        MakeBossLoadout(shared, antagonist, "Zzz loadout of Zhestokost");

        Assert.Same(shared, Assert.Single(LoadoutGenerator.BossHullsOf(_cache, antagonist)).hull);
        Assert.Same(shared, Assert.Single(LoadoutGenerator.BossHullsOf(_cache, lucent)).hull);
        Assert.Equal("Zzz loadout of Zhestokost", Loadouts.BossOf(_cache, antagonist, out _).Name);
        Assert.Equal("Aaa loadout of Lucent", Loadouts.BossOf(_cache, lucent, out _).Name);
        var items = LoggingItems(new List<string>());
        var boss = Assert.Single(BossZonePack(galaxy, items, galaxy.Exit).Entities, entity => entity.Boss);
        Assert.Equal("Shared Boss", HullName(items, boss));
    }

    // Ruling hull-programmer-art-reads-as-stand-in: a boss hull may be an agent's stopgap with no authored model.
    [Fact]
    public void A_boss_hull_needs_no_authored_model()
    {
        var galaxy = DemoGalaxy(1);
        var antagonist = Zhestokost(galaxy);
        var bare = MakeBoss("Bare Boss", 1, antagonist);
        MakeBossLoadout(bare, antagonist, "Bare Boss loadout");
        Assert.Null(bare.Prefab);
        Assert.False(bare.Visual.IsSet());

        Assert.Same(bare, Assert.Single(LoadoutGenerator.BossHullsOf(_cache, antagonist)).hull);
        var items = LoggingItems(new List<string>());
        var boss = Assert.Single(BossZonePack(galaxy, items, galaxy.Exit).Entities, entity => entity.Boss);
        Assert.Equal("Bare Boss", HullName(items, boss));
    }

    // A Boss hull is flown only by bosses: with the cheapest price (the heaviest draw weight) and every relevant maker, it still
    // never comes out of a generation draw, including the any-manufacturer fallback and a faction no maker is allied to.
    [Fact]
    public void Boss_hulls_are_never_stock_or_traffic()
    {
        var galaxy = DemoGalaxy(1);
        var antagonist = Zhestokost(galaxy);
        var lucent = FactionNamed(galaxy, "Lucent Media");
        var bossShip = MakeBoss("Traffic Boss", 1, antagonist, lucent);
        var bossStation = MakeBoss(HullType.Station, "Stock Boss", 1, antagonist, lucent);
        var stranger = new Faction { Name = "Stranger" };
        _cache.Upsert(stranger);
        stranger = _cache.GetAll<Faction>().Single(faction => faction.Name == "Stranger");
        MakeBoss("Strangers Foe Boss", 1, lucent);
        var log = new List<string>();
        var items = LoggingItems(log);

        var drawn = 0;
        for (uint seed = 1; seed <= 20; seed++)
            foreach (var faction in new[] { antagonist, lucent, stranger, null })
            {
                var generator = GeneratorFor(galaxy, items, faction, seed);
                for (var i = 0; i < 3; i++)
                {
                    var pack = generator.GenerateShipLoadout();
                    Assert.NotNull(pack);
                    drawn++;
                    Assert.DoesNotContain(HullName(items, pack), new[] { "Traffic Boss", "Strangers Foe Boss" });
                    Assert.NotEqual("Stock Boss", generator.RandomHull(HullType.Station).design?.Name);
                }
                // Asked for by name, with the boss the only candidate: nothing is on offer, however the fallback is reached.
                Assert.Null(generator.GenerateShipLoadout(hull => hull == bossShip));
                Assert.Null(generator.GenerateShipLoadout(hull => hull.Name == "Strangers Foe Boss"));
                Assert.Null(generator.RandomHull(HullType.Station, hull => hull == bossStation).design);
            }
        Assert.Equal(20 * 4 * 3, drawn);
    }

    // PlaceFactionsMain picks through BossOf: a faction with a Boss hull and no Loadout places nothing, and the faction that
    // also has the Loadout is the one entry, though another faction sorts first.
    [Fact]
    public void Main_galaxy_bosses_are_factions_with_a_boss_loadout()
    {
        Assert.Empty(RunStart.Generate(new MainGalaxy(), Inputs(() => 1)).BossZones);

        var makers = _cache.GetAll<Faction>().OrderBy(faction => faction.Name, StringComparer.Ordinal).Take(2).ToArray();
        MakeBoss("Unarmed Boss", 100, makers[0]);
        var armed = MakeBoss("Main Boss", 100, makers[1]);
        Assert.Empty(RunStart.Generate(new MainGalaxy(), Inputs(() => 1)).BossZones);

        MakeBossLoadout(armed, makers[1], "Main Boss loadout");
        var galaxy = RunStart.Generate(new MainGalaxy(), Inputs(() => 1));
        var bossFaction = Assert.Single(galaxy.BossZones.Keys);
        Assert.Equal(makers[1].Name, bossFaction.Name);
        Assert.NotNull(Loadouts.BossOf(_cache, bossFaction, out _));
    }

    [Fact]
    public void Boss_spawns_once_in_boss_zone()
    {
        var antagonistsBoss = GiveAntagonistABoss(DemoGalaxy(DemoSeeds.First()));
        foreach (var seed in DemoSeeds.Take(3))
        {
            var galaxy = DemoGalaxy(seed);
            var antagonist = Zhestokost(galaxy);
            foreach (var zone in galaxy.Zones)
            {
                var bosses = BossZonePack(galaxy, _items, zone).Entities.Where(entity => entity.Boss).ToArray();
                if (zone != galaxy.Exit) { Assert.Empty(bosses); continue; }

                var boss = Assert.Single(bosses);
                Assert.IsType<ShipPack>(boss);
                Assert.Same(antagonist, _cache.Get(boss.Faction));
                Assert.Equal(antagonistsBoss.Name, HullName(_items, boss));
                // Generation is repeatable per zone: the same zone yields the same boss hull.
                var again = Assert.Single(BossZonePack(galaxy, _items, zone).Entities, entity => entity.Boss);
                Assert.Equal(HullName(_items, boss), HullName(_items, again));
            }
        }
    }

    // A zone that is not ambient still has its boss: the boss is the goal, not traffic.
    [Fact]
    public void Boss_spawns_in_a_zone_that_is_not_ambient()
    {
        var galaxy = DemoGalaxy(1);
        GiveAntagonistABoss(galaxy);
        var pack = BossZonePack(galaxy, _items, galaxy.Exit, ambient: false);
        Assert.Single(pack.Entities, entity => entity.Boss);
        Assert.DoesNotContain(pack.Entities, entity => !entity.Boss && entity is ShipPack);
    }

    [Fact]
    public void Exit_sealed_until_boss_dies()
    {
        var galaxy = DemoGalaxy(1);
        GiveAntagonistABoss(galaxy);
        var exit = ZoneOf(galaxy, galaxy.Exit);
        var boss = Assert.Single(exit.Entities, entity => entity.IsBoss);
        Assert.True(RunGoal.BossAlive(exit));
        Assert.False(RunGoal.ExitOpen(exit));

        // Death removes the boss from the zone (the one death path), and that alone opens the gate.
        boss.HeatstrokeDeath.OnNext(Unit.Default);
        Assert.False(exit.Entities.Any(x => ReferenceEquals(x, boss)), "the boss is removed from the exit");
        Assert.False(RunGoal.BossAlive(exit));
        Assert.True(RunGoal.ExitOpen(exit));

        // No other zone is ever an exit, boss or no boss.
        foreach (var zone in galaxy.Zones.Where(zone => zone != galaxy.Exit))
            Assert.False(RunGoal.ExitOpen(ZoneOf(galaxy, zone)));
    }

    // Continue: the mark is saved with the zone and restored, so a boss alive when the run was saved is alive after.
    [Fact]
    public void Boss_mark_survives_continue()
    {
        GiveAntagonistABoss(DemoGalaxy(1));
        var (galaxy, arena, staged, failures) = Launch(new DemoTerminus(), Inputs(() => 1));
        Assert.True(failures.Count == 0, string.Join("; ", failures));
        galaxy.Entrance.Contents = arena;
        galaxy.Exit.Contents = ZoneOf(galaxy, galaxy.Exit);
        var before = Assert.Single(galaxy.Exit.Contents.Entities, entity => entity.IsBoss);
        Assert.False(RunGoal.ExitOpen(galaxy.Exit.Contents));

        var (game, zones) = RunSave.Capture(_cache, galaxy, arena, staged.Player, new SavedActionBarBinding[0], 0);
        RunSave.Commit(_cache, game, zones, _items.Lots);
        var restored = new Galaxy(_cache, _cache.GetGlobal<SavedGame>(), _ => { });
        var exit = new Zone(_items, _planetSettings, restored.Exit.PackedContents, restored.Exit, restored);

        var after = Assert.Single(exit.Entities, entity => entity.IsBoss);
        Assert.Equal(before.Name, after.Name);
        Assert.Equal(before.Faction.Name, after.Faction.Name);
        Assert.False(RunGoal.ExitOpen(exit));
        after.HeatstrokeDeath.OnNext(Unit.Default);
        Assert.True(RunGoal.ExitOpen(exit));
    }

    // The gate fails closed. The main galaxy puts its bosses on choke points, not at its exit, so its exit zone never gets
    // one: the gate stays sealed on arrival and when every boss in the galaxy dies, because a boss that never existed is
    // not a dead one.
    [Fact]
    public void Exit_sealed_where_no_boss_was_spawned_main_galaxy()
    {
        foreach (var seed in new uint[] { 1, 2, 3, 4, 5, 6, 7, 8 })
        {
            var galaxy = RunStart.Generate(new MainGalaxy(), Inputs(() => seed));
            var exit = ZoneOf(galaxy, galaxy.Exit);
            Assert.False(RunGoal.ExitOpen(exit), $"seed {seed}: the gate is open on arrival");
            foreach (var boss in exit.Entities.Where(entity => entity.IsBoss).ToArray())
                boss.HeatstrokeDeath.OnNext(Unit.Default);
            Assert.False(RunGoal.ExitOpen(exit), $"seed {seed}: the gate is open with no boss spawned");
        }
    }

    // The same for a demo exit whose boss could not be generated: the zone logs the gap by name and the gate stays sealed.
    [Fact]
    public void Exit_sealed_and_logged_when_the_boss_cannot_be_generated()
    {
        var galaxy = DemoGalaxy(1);
        galaxy.BossZones.Clear();
        var log = new List<string>();
        var items = LoggingItems(log);
        var pack = BossZonePack(galaxy, items, galaxy.Exit);
        Assert.False(pack.BossSpawned);
        Assert.Contains(log, line => line.Contains("Galaxy.Exit") && line.Contains("exit-without-boss"));
        var exit = new Zone(items, _planetSettings, pack, galaxy.Exit, galaxy);
        Assert.False(RunGoal.ExitOpen(exit));
    }

    // The gate is the exit zone's alone. The main galaxy puts bosses on choke points that are not its exit: such a zone's
    // boss spawns and dies, and no gate opens, because the exit-zone condition is what keeps a slain boss from winning.
    [Fact]
    public void Exit_open_only_in_the_exit_zone()
    {
        var galaxy = RunStart.Generate(new MainGalaxy(), Inputs(() => 1));
        // The shipped catalog has no Boss hull, so the main galaxy places no boss: give a faction one and a boss zone that is
        // not the exit, as the generator would for a faction that makes a Boss hull.
        var faction = galaxy.Factions.First();
        MakeBossLoadout(MakeBoss("Gate Boss", 100, faction), faction, "Gate Boss loadout");
        var bossZone = galaxy.Zones.First(zone => zone != galaxy.Exit);
        galaxy.BossZones[faction] = bossZone;
        var zone = ZoneOf(galaxy, bossZone);
        Assert.True(zone.Pack.BossSpawned);
        Assert.NotEmpty(zone.Entities.Where(entity => entity.IsBoss));
        foreach (var boss in zone.Entities.Where(entity => entity.IsBoss).ToArray())
            boss.HeatstrokeDeath.OnNext(Unit.Default);
        Assert.False(RunGoal.BossAlive(zone));
        Assert.False(RunGoal.ExitOpen(zone));
    }

    // A boss Loadout whose slot does not fit gives no boss: the log names the Loadout and the fit failure, and the gate stays sealed.
    [Fact]
    public void An_unbuildable_boss_loadout_spawns_no_boss()
    {
        var galaxy = DemoGalaxy(1);
        var antagonist = Zhestokost(galaxy);
        var hull = MakeBoss("Boss A", 100, antagonist);
        var loadout = MakeBossLoadout(hull, antagonist, "Unfittable loadout");
        loadout.Slots[0].Position = new int2(999, 999);
        StoreLoadout(loadout);
        Assert.Equal("Unfittable loadout", Loadouts.BossOf(_cache, antagonist, out _).Name);

        var log = new List<string>();
        var items = LoggingItems(log);
        var pack = BossZonePack(galaxy, items, galaxy.Exit, ambient: false);
        Assert.False(pack.BossSpawned);
        Assert.DoesNotContain(pack.Entities, entity => entity.Boss);
        Assert.Contains(log, line => line.Contains("Unfittable loadout") && line.Contains("does not fit"));
        Assert.False(RunGoal.ExitOpen(new Zone(items, _planetSettings, pack, galaxy.Exit, galaxy)));
    }

    // A boss killed before the save stays dead after it, and the gate stays open: the spawn fact travels with the zone.
    [Fact]
    public void Exit_open_after_save_of_a_slain_boss()
    {
        var galaxy = DemoGalaxy(1);
        GiveAntagonistABoss(galaxy);
        var exit = ZoneOf(galaxy, galaxy.Exit);
        Assert.Single(exit.Entities, entity => entity.IsBoss).HeatstrokeDeath.OnNext(Unit.Default);
        var repacked = exit.PackZone();
        Assert.True(repacked.BossSpawned);
        Assert.DoesNotContain(repacked.Entities, entity => entity.Boss);
        Assert.True(RunGoal.ExitOpen(new Zone(_items, _planetSettings, repacked, galaxy.Exit, galaxy)));
    }

    // The gate shares no point with an adjacency wormhole: every one is farther from it than a ship's wormhole reach.
    [Fact]
    public void Exit_gate_shares_no_point_with_a_wormhole()
    {
        var ratio = float.Parse(File.ReadLines(Path.Combine(FindRepoRoot(), "Assets", "Resources", "Settings.asset"))
            .Select(line => Regex.Match(line, @"^  WormholeDistanceRatio: (.+)$")).First(match => match.Success).Groups[1].Value.Trim(),
            System.Globalization.CultureInfo.InvariantCulture);
        var reach = AuthoredSettings.Load(FindRepoRoot()).Read<GameplaySettings>("GameplaySettings").WormholeExitRadius;
        foreach (var seed in DemoSeeds)
        {
            var galaxy = DemoGalaxy(seed);
            var zone = ZoneOf(galaxy, galaxy.Exit);
            var gate = RunGoal.ExitGatePosition(zone, ratio);
            foreach (var adjacent in galaxy.Exit.AdjacentZones)
            {
                var wormhole = math.normalize(adjacent.Position - galaxy.Exit.Position) * zone.Pack.Radius * ratio;
                Assert.True(math.length(gate - wormhole) > 2 * reach, $"seed {seed}: the exit gate sits {math.length(gate - wormhole)} from a wormhole");
            }
        }
    }

    // The gate sits on the wormholes' radius, in the middle of the widest gap between neighbours, whatever the
    // neighbours' number and order: an empty ring, one, two straddling the +-pi seam, and eight spread evenly (every gap
    // under one radian). Checked against a brute-force sweep of every direction.
    [Theory]
    [InlineData(new double[0])]
    [InlineData(new[] { 40.0 })]
    [InlineData(new[] { 170.0, -170.0 })]
    [InlineData(new[] { 100.0, 10.0, 20.0, 350.0, -90.0 })]
    [InlineData(new[] { 0.0, 45.0, 90.0, 135.0, 180.0, -135.0, -90.0, -45.0 })]
    public void Exit_gate_sits_in_the_widest_gap_at_the_wormhole_radius(double[] neighbourDegrees)
    {
        var galaxy = DemoGalaxy(1);
        var zone = ZoneOf(galaxy, galaxy.Exit);
        const float ratio = 0.75f;
        var radius = zone.Pack.Radius * ratio;
        // Away from the origin and with close neighbours, so a direction taken from raw positions instead of the
        // offset from the zone cannot pass.
        var originalPosition = galaxy.Exit.Position;
        var origin = galaxy.Exit.Position = math.float2(500, -300);
        var original = galaxy.Exit.AdjacentZones;
        galaxy.Exit.AdjacentZones = neighbourDegrees
            .Select(degrees => new GalaxyZone
            {
                Position = origin + math.float2((float) Math.Cos(degrees * Math.PI / 180), (float) Math.Sin(degrees * Math.PI / 180)) * 10
            }).ToList();
        float2 gate;
        try { gate = RunGoal.ExitGatePosition(zone, ratio); }
        finally { galaxy.Exit.AdjacentZones = original; galaxy.Exit.Position = originalPosition; }

        Assert.Equal(radius, math.length(gate), 2);
        if (neighbourDegrees.Length == 0) { Assert.Equal(radius, gate.x, 2); Assert.Equal(0, gate.y, 2); return; }

        double Separation(double angle) => neighbourDegrees.Min(degrees =>
        {
            var difference = Math.Abs(angle - degrees * Math.PI / 180) % (2 * Math.PI);
            return difference > Math.PI ? 2 * Math.PI - difference : difference;
        });
        var best = Enumerable.Range(0, 7200).Max(step => Separation(step * Math.PI / 3600));
        Assert.True(Separation(Math.Atan2(gate.y, gate.x)) >= best - 0.002,
            $"the gate's nearest wormhole is {Separation(Math.Atan2(gate.y, gate.x))} rad away; the widest gap's middle is {best}");
    }

    // Two neighbours straight across leave two gaps of exactly pi: the first in ascending angle (from -pi) wins, so the
    // gate sits at +radius on y, not -radius.
    [Fact]
    public void Exit_gate_takes_the_first_of_equal_gaps()
    {
        var galaxy = DemoGalaxy(1);
        var zone = ZoneOf(galaxy, galaxy.Exit);
        const float ratio = 0.75f;
        var originalPosition = galaxy.Exit.Position;
        var origin = galaxy.Exit.Position = math.float2(500, -300);
        var original = galaxy.Exit.AdjacentZones;
        galaxy.Exit.AdjacentZones = new List<GalaxyZone>
        {
            new GalaxyZone { Position = math.float2(510, -300) },
            new GalaxyZone { Position = math.float2(490, -300) },
        };
        float2 gate;
        try { gate = RunGoal.ExitGatePosition(zone, ratio); }
        finally { galaxy.Exit.AdjacentZones = original; galaxy.Exit.Position = originalPosition; }

        Assert.Equal(0, gate.x, 3);
        Assert.Equal(zone.Pack.Radius * ratio, gate.y, 2);
    }
}
