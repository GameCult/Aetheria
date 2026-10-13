/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System.Linq;
using Xunit;

// Pickup is a capability (aetheria-release cut loot-2), so a ship that can pick things up carries the gear that grants
// it. Generation is the one place a ship's gear is chosen, so this runs the real catalog through it.
public sealed partial class RunStartTests
{
    // The beams a pack carries fitted. A station may stock one in its hold to sell, which is the point of selling it.
    private int PickupGear(EntityPack pack) => pack.Equipment.Select(fitted => fitted.item)
        .Count(item => _items.GetData(item) is GearData gear && gear.Behaviors.Any(b => b is PickupData));

    [Fact]
    public void EveryGeneratedShipCarriesATractorBeam()
    {
        // Every ship hull a product sells.
        var generator = PreludeGenerator();
        foreach (var hull in LoadoutGenerator.SoldHulls(_cache).Where(hull => hull.HullType == HullType.Ship))
            Assert.True(PickupGear(generator.GenerateShipLoadout(candidate => candidate == hull)) == 1, $"a {hull.Name} carries exactly one tractor beam");

        // Ships generated for each faction of a main galaxy, and the starting ship the game stages.
        var main = Launch(new MainGalaxy(), Inputs(() => GalaxySeed));
        Assert.Empty(main.failures);
        var factions = main.galaxy.Factions.ToArray();
        for (var i = 0; i < 50; i++)
        {
            var faction = factions[i % factions.Length];
            var maker = new LoadoutGenerator(ref _items.Random, _items, main.galaxy, main.galaxy.Entrance, faction, .5f);
            Assert.True(PickupGear(maker.GenerateShipLoadout()) == 1, $"ship {i} of {faction.Name} carries exactly one tractor beam");
        }
        Assert.Equal(1, main.staged.Player.Equipment.Count(item => item.Behaviors.OfType<Pickup>().Any()));
        Assert.All(main.arena.Pack.Entities.Where(pack => pack is ShipPack), pack => Assert.Equal(1, PickupGear(pack)));

        // Stations and turrets share the interior outfitting and get none.
        for (var i = 0; i < 4; i++)
        {
            Assert.Equal(0, PickupGear(generator.GenerateStationLoadout()));
            Assert.Equal(0, PickupGear(generator.GenerateTurretLoadout()));
        }
        Assert.All(main.arena.Pack.Entities.Where(pack => !(pack is ShipPack)), pack => Assert.Equal(0, PickupGear(pack)));
    }
}
