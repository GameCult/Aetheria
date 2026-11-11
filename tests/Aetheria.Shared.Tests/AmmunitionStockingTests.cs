using System.Collections.Generic;
using System.Linq;
using static CultMath.math;
using Xunit;

// Ruling ballistics-real-ammo: generation stocks combatants with rounds, because a magazine weapon reloads from cargo and a ship with
// no crates fires one magazine and goes quiet. Measured on ships the production generator returns, over the shipped catalog.
public sealed partial class RunStartTests
{
    // Every generated ship whose weapons draw an ammunition type carries crates of each such type; a ship whose weapons draw none
    // carries none of an ammunition commodity it does not draw. Kills: generation that stocks nothing (every NPC gun dies after one magazine),
    // that stocks only one of several types, or that stocks ships with no use for them.
    [Fact]
    public void GeneratedShipsCarryTheAmmunitionTheirWeaponsDraw()
    {
        var generator = new LoadoutGenerator(ref _items.Random, _items, _galaxy, _galaxy.Entrance, _protagonist, .5f);
        var shipHulls = LoadoutGenerator.SoldHulls(_cache).Where(h => h.HullType == HullType.Ship).Select(h => h.Name).ToList();
        var ammunition = _cache.GetAll<WeaponItemData>().SelectMany(i => i.Behaviors.OfType<WeaponData>()).Where(w => w.AmmoType.IsSet())
            .Select(w => w.AmmoType.Key).Distinct().ToArray();
        Assert.NotEmpty(ammunition);
        var fed = 0;
        foreach (var name in shipHulls)
            for (var i = 0; i < 12; i++)
            {
                var pack = generator.GenerateShipLoadout(h => h.Name == name);
                if (pack == null) continue;
                var ship = EntitySerializer.Unpack(_items, null, pack);
                var drawn = ship.Weapons.Select(w => w.WeaponData.AmmoType).Where(t => t.IsSet()).Select(t => t.Key).Distinct().ToArray();
                foreach (var key in drawn)
                {
                    Assert.True(ship.CountItemsInCargo(key) > 0, $"{name}: a weapon draws {key.Value} and the hold has none");
                    Assert.Equal(LoadoutGenerator.AmmunitionCrates, ship.CountItemsInCargo(key));
                }
                foreach (var key in ammunition.Except(drawn))
                    Assert.True(ship.CountItemsInCargo(key) == 0, $"{name}: no weapon draws {key.Value} and the hold carries it");
                if (drawn.Length > 0) fed++;
            }
        Assert.True(fed > 0, "fixture: some generated ship carries an ammunition-fed weapon");
    }

    // A generated turret reloads from its own hold like a ship: every ammunition type its weapons draw has crates in it. Kills: a turret
    // path that skips StockAmmunition, which leaves an ammo-fed turret quiet after one magazine.
    [Fact]
    public void GeneratedTurretsCarryTheAmmunitionTheirWeaponsDraw()
    {
        var generator = new LoadoutGenerator(ref _items.Random, _items, _galaxy, _galaxy.Entrance, _protagonist, .5f);
        var fed = 0;
        for (var i = 0; i < 60; i++)
        {
            var turret = EntitySerializer.Unpack(_items, null, generator.GenerateTurretLoadout());
            foreach (var key in turret.Weapons.Select(w => w.WeaponData.AmmoType).Where(t => t.IsSet()).Select(t => t.Key).Distinct())
            {
                fed++;
                Assert.Equal(LoadoutGenerator.AmmunitionCrates, turret.CountItemsInCargo(key));
            }
        }
        Assert.True(fed > 0, "fixture: some generated turret carries an ammunition-fed weapon");
    }

    private static int[] FedTypesStocked(Entity ship, out int fed)
    {
        var keys = ship.Weapons.Select(w => w.WeaponData.AmmoType).Where(t => t.IsSet()).Select(t => t.Key).Distinct().ToArray();
        fed = keys.Length;
        return keys.Select(key => ship.CountItemsInCargo(key)).ToArray();
    }

    // A test hull, a one-cell ammunition-fed gun (a magazine of none, so the single-round rule is in play) and a bay,
    // in the shared catalog: the smallest authored ship that can carry crates.
    private (string Hull, (string, CultMath.int2, ItemRotation)[] Slots) FedFit(bool bay)
    {
        var shape = new Shape(7, 7);
        foreach (var cell in shape.AllCoordinates) shape[cell] = true;
        var hullData = new HullData { Name = "Fed Hull", HullType = HullType.Ship, Shape = shape, Durability = 10, Mass = 100 };
        _cache.Upsert(hullData);
        var rounds = _cache.Upsert(new SimpleCommodityData { Name = "Fed Rounds", Shape = new Shape(), Price = 1, MaxStack = 100 });
        _cache.Upsert(new WeaponItemData
        {
            Name = "Fed Gun", Hardpoint = HardpointType.Tool, Shape = new Shape(), Durability = 1,
            Behaviors = { new InstantWeaponData { AmmoType = rounds, MagazineSize = 0 } }
        });
        var interior = new Shape(4, 4);
        foreach (var cell in interior.AllCoordinates) interior[cell] = true;
        _cache.Upsert(new CargoBayData { Name = "Fed Bay", Shape = new Shape(), InteriorShape = interior, Price = 20, Durability = 4 });
        var slots = new List<(string, CultMath.int2, ItemRotation)> { ("Fed Gun", int2(3, 3), ItemRotation.None) };
        if (bay) slots.Add(("Fed Bay", int2(4, 3), ItemRotation.None));
        return ("Fed Hull", slots.ToArray());
    }

    // A ship a scenario fits slot by slot (stage.Fit, no generator) that carries a bay and an ammunition-fed weapon ends with the crates that
    // weapon draws, and its own Reserve reads them. Kills: Materialize returning the entity unstocked (the authored ship fires one magazine).
    [Fact]
    public void AScenarioFitWithAFedWeaponAndABayCarriesItsCrates()
    {
        var (hull, slots) = FedFit(bay: true);
        var (_, _, staged, failures) = Launch(new Scripted(false, stage => stage.Player(stage.Fit(hull, slots), float2(0, 0))), Inputs(() => GalaxySeed));
        Assert.True(failures.Count == 0, string.Join("; ", failures));
        var weapon = Assert.Single(staged.Player.Weapons);
        Assert.True(weapon.WeaponData.AmmoType.IsSet(), "fixture: the gun draws ammunition");
        Assert.Equal(LoadoutGenerator.AmmunitionCrates, weapon.Reserve);
    }

    // A scenario that authors a fit's cargo (stage.Cargo) gets exactly that cargo: its author named what the hold holds, so no crates are added.
    // Kills: stocking that ignores authored cargo and puts missiles into a scenario whose check counts what the hold carries.
    [Fact]
    public void AScenarioFitWhoseCargoIsAuthoredIsNotStocked()
    {
        var (hull, slots) = FedFit(bay: true);
        var (_, _, staged, failures) = Launch(new Scripted(false, stage =>
            stage.Cargo(stage.Player(stage.Fit(hull, slots), float2(0, 0)), "Fed Gun")), Inputs(() => GalaxySeed));
        Assert.True(failures.Count == 0, string.Join("; ", failures));
        Assert.Equal(0, Assert.Single(staged.Player.Weapons).Reserve);
    }

    // With no hold to put crates in there are none, and the fit still builds: stocking is bounded by what the ship can carry, never a failure.
    [Fact]
    public void AScenarioFitWithNoBayCarriesNoCratesAndStillBuilds()
    {
        var (hull, slots) = FedFit(bay: false);
        var (_, _, staged, failures) = Launch(new Scripted(false, stage => stage.Player(stage.Fit(hull, slots), float2(0, 0))), Inputs(() => GalaxySeed));
        Assert.True(failures.Count == 0, string.Join("; ", failures));
        Assert.Equal(0, Assert.Single(staged.Player.Weapons).Reserve);
    }

    // The new run's player ship (stage.Generated at the starting hull) and every ship the shipped main-galaxy scenario generates carry the crates
    // their weapons draw: nothing that reaches play with ammunition-fed weapons is left to one magazine.
    [Fact]
    public void ANewRunPlayerShipCarriesTheCratesItsWeaponsDraw()
    {
        var fed = 0;
        for (uint seed = GalaxySeed; seed < GalaxySeed + 8; seed++)
        {
            var (_, _, staged, failures) = Launch(new Scripted(false, stage => stage.Player(stage.Generated(stage.StartingHull), float2(0, 0)), seed),
                Inputs(() => seed));
            Assert.True(failures.Count == 0, string.Join("; ", failures));
            foreach (var count in FedTypesStocked(staged.Player, out var types))
            {
                Assert.Equal(LoadoutGenerator.AmmunitionCrates, count);
                fed += types;
            }
        }
        Assert.True(fed > 0, "fixture: some new-run player ship carries an ammunition-fed weapon");
    }
}
