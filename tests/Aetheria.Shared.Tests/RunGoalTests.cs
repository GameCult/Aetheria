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
// only when that boss is dead. The boss flies its faction's boss Loadout (cut boss-loadout). Every input is the production one: the demo galaxy, GenerateZone, the Zone death path,
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
        .Where(hull => hull != null && hull.HullType == HullType.Ship && hull.Price > 0)
        .Distinct().ToArray();

    // A hull no product makes, so the market never offers it: a MessagePack copy of the first sold ship hull by name, renamed. It has
    // no authored model, which a boss does not need. Stored once; asking again returns the same record.
    private HullData BossTestHull()
    {
        var existing = _cache.GetAll<HullData>().FirstOrDefault(hull => hull.Name == "Boss Test Hull");
        if (existing != null) return existing;
        var options = CultDocumentMessagePackSerialization.OptionsFor(typeof(HullData).Assembly);
        var like = SoldShipHulls().OrderBy(hull => hull.Name, StringComparer.Ordinal).First();
        var boss = MessagePack.MessagePackSerializer.Deserialize<HullData>(MessagePack.MessagePackSerializer.Serialize(like, options), options);
        boss.Name = "Boss Test Hull";
        boss.Prefab = null;
        boss.Visual = default;
        return _cache.Get(_cache.Upsert(boss));
    }

    // A Loadout record at Loadouts.KeyOf(Name), the key Loadouts.Commit writes it under.
    private void StoreLoadout(Loadout loadout) =>
        Assert.True(_cache.Commit(batch => batch.Upsert(typeof(Loadout), loadout, Loadouts.KeyOf(loadout.Name))));

    // A boss's fixed Loadout (ruling boss-gear-is-a-loadout), by the production capture: a seed-11 generated fit of the first sold
    // ship hull by name, captured (Loadouts.Capture), cut down to its weapons so it is smaller than any generated fit (the tractor
    // pickup and the rest are not in it), moved onto the given hull with no hull product, and stored under its name.
    private Loadout MakeBossLoadout(HullData hull, string name)
    {
        var like = SoldShipHulls().OrderBy(h => h.Name, StringComparer.Ordinal).First();
        var random = new CultMath.Random(11);
        var generator = new LoadoutGenerator(ref random, _items, null, null, null, .5f);
        var ship = (Ship) EntitySerializer.Unpack(_items, null, generator.GenerateShipLoadout(h => h == like));
        var fit = Loadouts.Capture(_items, ship, name);
        var armed = fit.Slots.Select((slot, index) => (slot, index))
            .Where(entry => _cache.Get(entry.slot.Design).Behaviors.Any(behavior => behavior is WeaponData)).ToArray();
        Assert.NotEmpty(armed);
        Assert.True(armed.Length < fit.Slots.Count, "the generated fit needs gear besides weapons for this fixture to tell a Loadout from a fit");
        var renumbered = armed.Select((entry, number) => (entry.index, number)).ToDictionary(entry => entry.index, entry => entry.number);
        fit.WeaponGroups = fit.WeaponGroups.Select(group => group.Where(renumbered.ContainsKey).Select(index => renumbered[index]).ToArray()).ToArray();
        fit.Slots = armed.Select(entry => entry.slot).ToList();
        fit.Hull = _cache.RefOf(hull);
        fit.HullProduct = default;
        StoreLoadout(fit);
        return fit;
    }

    private static void SetBoss(Faction faction, Loadout loadout) => faction.BossLoadout = new CultRecordRef<Loadout>(Loadouts.KeyOf(loadout.Name));

    // The demo's antagonist gets a boss Loadout on a hull nothing sells: the shipped catalog authors none.
    private Loadout GiveAntagonistABoss(Galaxy galaxy)
    {
        var boss = MakeBossLoadout(BossTestHull(), "Antagonist Boss");
        SetBoss(Zhestokost(galaxy), boss);
        return boss;
    }

    // What a ship carries, cell by cell, as a Loadout would name it: position, rotation, design, product.
    private static string[] Cells(ItemManager items, EntityPack pack)
    {
        var captured = Loadouts.Capture(items, (Ship) EntitySerializer.Unpack(items, null, pack), "probe");
        return captured.Slots.OrderBy(slot => slot.Position.x).ThenBy(slot => slot.Position.y)
            .Select(slot => $"{slot.Position.x},{slot.Position.y},{slot.Rotation},{items.ItemData.Get(slot.Design).Name},{(slot.Product.IsSet() ? items.ItemData.Get(slot.Product).Name : "-")}").ToArray();
    }

    private string[] LoadoutCells(Loadout loadout) =>
        loadout.Slots.OrderBy(slot => slot.Position.x).ThenBy(slot => slot.Position.y)
            .Select(slot => $"{slot.Position.x},{slot.Position.y},{slot.Rotation},{_cache.Get(slot.Design).Name},{(slot.Product.IsSet() ? _cache.Get(slot.Product).Name : "-")}").ToArray();

    private LoadoutGenerator GeneratorFor(Galaxy galaxy, ItemManager items, Faction faction, uint seed)
    {
        var random = new CultMath.Random(seed);
        return new LoadoutGenerator(ref random, items, galaxy, galaxy.Exit, faction, .5f);
    }

    // What the pack holds, in order, by hull and faction: two zone packs that drew the same stream hold the same list.
    private static string[] Roster(ItemManager items, ZonePack pack) =>
        pack.Entities.Select(entity => $"{HullName(items, entity)}|{(entity.Faction.IsSet() ? entity.Faction.Key.ToString() : "-")}").ToArray();

    [Fact]
    public void Boss_spawns_once_in_boss_zone()
    {
        foreach (var seed in DemoSeeds.Take(3))
        {
            var galaxy = DemoGalaxy(seed);
            GiveAntagonistABoss(galaxy);
            var antagonist = Zhestokost(galaxy);
            foreach (var zone in galaxy.Zones)
            {
                var bosses = BossZonePack(galaxy, _items, zone).Entities.Where(entity => entity.Boss).ToArray();
                if (zone != galaxy.Exit) { Assert.Empty(bosses); continue; }

                var boss = Assert.Single(bosses);
                Assert.IsType<ShipPack>(boss);
                Assert.Same(antagonist, _cache.Get(boss.Faction));
                Assert.Equal("Boss Test Hull", HullName(_items, boss));
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

    // Faction.BossLoadout owns the boss. A decoy Loadout sorts before it by name and by key, so a resolver that takes the first
    // Loadout in the cache, or any Loadout but the referenced one, flies the wrong hull.
    [Fact]
    public void Boss_flies_its_factions_boss_loadout()
    {
        var galaxy = DemoGalaxy(1);
        var antagonist = Zhestokost(galaxy);
        var sold = SoldShipHulls().OrderBy(hull => hull.Name, StringComparer.Ordinal).First();
        StoreLoadout(new Loadout { Name = "Aaa Decoy", Hull = _cache.RefOf(sold) });
        var loadout = GiveAntagonistABoss(galaxy);

        Assert.Same(_cache.Get(antagonist.BossLoadout), Loadouts.BossOf(_cache, antagonist, out var refusal));
        Assert.Equal(BossRefusal.None, refusal);
        Assert.Equal("Antagonist Boss", loadout.Name);
        var boss = Assert.Single(BossZonePack(galaxy, _items, galaxy.Exit).Entities, entity => entity.Boss);
        Assert.IsType<ShipPack>(boss);
        Assert.Same(antagonist, _cache.Get(boss.Faction));
        Assert.Equal("Boss Test Hull", HullName(_items, boss));
        var again = Assert.Single(BossZonePack(galaxy, _items, galaxy.Exit).Entities, entity => entity.Boss);
        Assert.Equal(Cells(_items, boss), Cells(_items, again));
    }

    // Ruling boss-gear-is-a-loadout: the boss carries exactly its Loadout, nothing drawn. The Loadout is weapons only, so any
    // pickup, extra gear, cargo or consumable on the boss is a generator pass that should not run.
    [Fact]
    public void Boss_carries_exactly_its_loadouts_cells()
    {
        var galaxy = DemoGalaxy(1);
        var loadout = GiveAntagonistABoss(galaxy);
        Assert.True(loadout.Slots.Count > 1);
        Assert.All(loadout.Slots, slot => Assert.True(slot.Product.IsSet(), "a captured fit pins every product"));

        foreach (var seed in new uint[] { 7, 8 })
        {
            var items = LoggingItems(new List<string>());
            var boss = Assert.Single(BossZonePack(galaxy, items, galaxy.Exit, seed: seed).Entities, entity => entity.Boss);
            Assert.Equal(LoadoutCells(loadout), Cells(items, boss));
            Assert.All(boss.Equipment, entry => Assert.DoesNotContain(items.GetData(entry.item).Behaviors, behavior => behavior is PickupData));
            Assert.Empty(boss.CargoContents.SelectMany(bay => bay));
            Assert.Empty(boss.DockingBayContents.SelectMany(bay => bay));

            var captured = Loadouts.Capture(items, (Ship) EntitySerializer.Unpack(items, null, boss), "probe");
            string[][] Groups(Loadout l) => l.WeaponGroups.Select(group => group.Select(i => $"{l.Slots[i].Position.x},{l.Slots[i].Position.y}").OrderBy(x => x, StringComparer.Ordinal).ToArray()).Where(group => group.Length > 0).ToArray();
            Assert.Equal(Groups(loadout), Groups(captured));
        }
    }

    // Ruling loadout-product-optional-context-chooses: a design-only slot takes the brand the boss's faction can get. The design has
    // an outsider's product that sorts first by name, so a chooser that takes the first product by name builds the wrong brand.
    [Fact]
    public void A_design_only_boss_slot_takes_a_brand_its_faction_can_get()
    {
        var galaxy = DemoGalaxy(1);
        var antagonist = Zhestokost(galaxy);
        var loadout = GiveAntagonistABoss(galaxy);
        var outfitter = GeneratorFor(galaxy, _items, antagonist, 1);
        var open = loadout.Slots.First(slot => _cache.GetAll<FactionProductData>().Any(product => product.Design.Key.Equals(slot.Design.Key) && outfitter.IsAvailable(product)));
        var template = _cache.GetAll<FactionProductData>().First(product => product.Design.Key.Equals(open.Design.Key));
        open.Product = default;
        StoreLoadout(loadout);

        var options = CultDocumentMessagePackSerialization.OptionsFor(typeof(HullData).Assembly);
        var outsider = MessagePack.MessagePackSerializer.Deserialize<FactionProductData>(MessagePack.MessagePackSerializer.Serialize(template, options), options);
        outsider.Name = "Aaa Outsider";
        foreach (var faction in _cache.GetAll<Faction>().OrderBy(faction => faction.Name, StringComparer.Ordinal))
        {
            outsider.Manufacturer = _cache.RefOf(faction);
            if (!outfitter.IsAvailable(outsider)) break;
        }
        Assert.False(outfitter.IsAvailable(outsider), "the outsider's maker is neither Zhestokost nor in its allegiance");
        _cache.Upsert(outsider);

        var gettable = _cache.GetAll<FactionProductData>().Where(product => product.Design.Key.Equals(open.Design.Key) && outfitter.IsAvailable(product)).Select(product => product.Name).ToArray();
        Assert.NotEmpty(gettable);
        foreach (var seed in new uint[] { 1, 2, 3, 4, 5 })
        {
            var items = LoggingItems(new List<string>());
            var boss = Assert.Single(BossZonePack(galaxy, items, galaxy.Exit, seed: seed).Entities, entity => entity.Boss);
            var cell = Assert.Single(Cells(items, boss), c => c.StartsWith($"{open.Position.x},{open.Position.y},"));
            var brand = cell.Split(',').Last();
            Assert.NotEqual("Aaa Outsider", brand);
            Assert.Contains(brand, gettable);
        }
    }

    // No generated fallback (rulings boss-gear-is-a-loadout, demo-boss-hull-invented): the shipped catalog authors no boss Loadout,
    // so Zhestokost has no boss. The refusal happens before anything is drawn.
    [Fact]
    public void Boss_without_a_boss_loadout_is_refused()
    {
        var galaxy = DemoGalaxy(1);
        var antagonist = Zhestokost(galaxy);
        Assert.Null(Loadouts.BossOf(_cache, antagonist, out var refusal));
        Assert.Equal(BossRefusal.Unset, refusal);

        var log = new List<string>();
        var items = LoggingItems(log);
        var pack = BossZonePack(galaxy, items, galaxy.Exit);
        Assert.False(pack.BossSpawned);
        Assert.DoesNotContain(pack.Entities, entity => entity.Boss);
        Assert.Contains(log, line => line.Contains("Zhestokost has no boss") && line.Contains("sets no Faction.BossLoadout"));
        Assert.Contains(log, line => line.Contains("Galaxy.Exit") && line.Contains("exit-without-boss"));
        Assert.False(RunGoal.ExitOpen(new Zone(items, _planetSettings, pack, galaxy.Exit, galaxy)));

        // Nothing was drawn for the refused boss: the zone is the zone with no boss mapped at all.
        galaxy.BossZones.Clear();
        var bare = BossZonePack(galaxy, LoggingItems(new List<string>()), galaxy.Exit);
        Assert.NotEmpty(bare.Entities);
        Assert.Equal(Roster(items, bare), Roster(items, pack));
    }

    // A reference to a Loadout the catalog does not hold is refused like no reference, and the log names the field, not the value.
    [Fact]
    public void A_missing_boss_loadout_is_refused()
    {
        var galaxy = DemoGalaxy(1);
        var antagonist = Zhestokost(galaxy);
        antagonist.BossLoadout = new CultRecordRef<Loadout>(Loadouts.KeyOf("No Such Loadout"));
        Assert.Null(Loadouts.BossOf(_cache, antagonist, out var refusal));
        Assert.Equal(BossRefusal.Missing, refusal);

        var log = new List<string>();
        var items = LoggingItems(log);
        var pack = BossZonePack(galaxy, items, galaxy.Exit);
        Assert.False(pack.BossSpawned);
        Assert.DoesNotContain(pack.Entities, entity => entity.Boss);
        Assert.Contains(log, line => line.Contains("Zhestokost has no boss") && line.Contains("names a Faction.BossLoadout that is not in the catalog"));
        Assert.DoesNotContain(log, line => line.Contains("No Such Loadout") || line.Contains("sets no"));
        Assert.DoesNotContain(log, line => line.Contains("Zhestokost has no boss") && line.Contains("No Such"));
    }

    // A boss Loadout whose slots do not fit gives no boss: the log names the Loadout and every fit failure, and the gate stays sealed.
    [Fact]
    public void An_unbuildable_boss_loadout_spawns_no_boss()
    {
        var galaxy = DemoGalaxy(1);
        var loadout = GiveAntagonistABoss(galaxy);
        loadout.Slots[0].Position = new int2(999, 999);
        loadout.Slots[1].Position = new int2(998, 998);
        StoreLoadout(loadout);

        var log = new List<string>();
        var items = LoggingItems(log);
        var pack = BossZonePack(galaxy, items, galaxy.Exit, ambient: false);
        Assert.False(pack.BossSpawned);
        Assert.DoesNotContain(pack.Entities, entity => entity.Boss);
        var refusal = Assert.Single(log, line => line.Contains("Antagonist Boss") && line.Contains("does not fit"));
        Assert.Equal(2, Regex.Matches(refusal, "does not fit").Count);
        Assert.Contains("; ", refusal);
        Assert.False(RunGoal.ExitOpen(new Zone(items, _planetSettings, pack, galaxy.Exit, galaxy)));
    }

    // The existing market rule (ruling off-market-is-general): an item is on offer iff a product of it is. A hull no product makes
    // is never drawn, stocked or chosen, yet the boss whose Loadout names it flies it.
    [Fact]
    public void A_hull_no_product_makes_is_never_on_offer()
    {
        var galaxy = DemoGalaxy(1);
        var hull = BossTestHull();
        GiveAntagonistABoss(galaxy);
        Assert.DoesNotContain(hull, LoadoutGenerator.SoldHulls(_cache));
        var items = LoggingItems(new List<string>());

        for (uint seed = 1; seed <= 20; seed++)
        {
            var generator = GeneratorFor(galaxy, items, Zhestokost(galaxy), seed);
            var ship = generator.GenerateShipLoadout();
            Assert.NotNull(ship);
            Assert.NotEqual(hull.Name, HullName(items, ship));
            var station = generator.GenerateStationLoadout();
            Assert.DoesNotContain(EntitySerializer.Items(station), item => items.GetData(item).Name == hull.Name);
            Assert.Null(generator.ChooseProduct(hull));
            Assert.Null(generator.GenerateShipLoadout(candidate => candidate == hull));
        }

        var boss = Assert.Single(BossZonePack(galaxy, items, galaxy.Exit).Entities, entity => entity.Boss);
        Assert.Equal(hull.Name, HullName(items, boss));
    }

    // PlaceFactionsMain reads BossOf: with no reference set there is no boss zone, a reference to nothing places none, and the one
    // faction whose boss Loadout resolves is the one entry though another faction sorts first.
    [Fact]
    public void Main_galaxy_bosses_are_factions_with_a_boss_loadout()
    {
        Assert.Empty(RunStart.Generate(new MainGalaxy(), Inputs(() => 1)).BossZones);

        var factions = _cache.GetAll<Faction>().OrderBy(faction => faction.Name, StringComparer.Ordinal).ToArray();
        factions[0].BossLoadout = new CultRecordRef<Loadout>(Loadouts.KeyOf("No Such Loadout"));
        Assert.Empty(RunStart.Generate(new MainGalaxy(), Inputs(() => 1)).BossZones);

        var armed = MakeBossLoadout(BossTestHull(), "Main Boss");
        SetBoss(factions[1], armed);
        var galaxy = RunStart.Generate(new MainGalaxy(), Inputs(() => 1));
        var bossFaction = Assert.Single(galaxy.BossZones.Keys);
        Assert.Equal(factions[1].Name, bossFaction.Name);
        Assert.NotNull(Loadouts.BossOf(_cache, bossFaction, out _));
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
        var (galaxy, arena, staged, failures) = Launch(new DemoTerminus(), Inputs(() => 1));
        Assert.True(failures.Count == 0, string.Join("; ", failures));
        GiveAntagonistABoss(galaxy);
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

    // The same for a demo exit whose boss could not be built: the zone logs the gap by name and the gate stays sealed.
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
        // The shipped catalog sets no boss Loadout, so the main galaxy places no boss: give a faction one and a boss zone that is
        // not the exit, as the generator would for a faction with a boss Loadout.
        var faction = galaxy.Factions.First();
        SetBoss(faction, MakeBossLoadout(SoldShipHulls().First(), "Gate Boss"));
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
