using System.Linq;
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
}
