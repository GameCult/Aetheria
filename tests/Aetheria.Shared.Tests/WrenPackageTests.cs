using System.Linq;
using CultMath;
using GameCult.Caching;
using Xunit;
using static CultMath.math;
using Random = CultMath.Random;

// GameData/Mods/aetheria.wren: a first-party package written by `ship-authoring starter --like Djinni`, then priced and given
// an unoffered Aeronautics Unlimited product by `ship-authoring price` and `product`; its model is the starter's boxed
// stand-in. It is the pipeline's proof from a hull package on disk to a ship that flies in the headless simulation: the
// package composes into the catalog, its mounts take catalog gear, and the fitted ship moves under thrust. `fly` cannot
// reach it (an unoffered product is never drawn), so these tests are the flight check until a pinned Loadout exists.
public sealed class WrenPackageTests
{
    private const string Id = "aetheria.wren";

    private static HullData Wren(CultCache cache) => cache.Get(new CultRecordRef<HullData>(ShipModCatalog.HullKey(Id)));

    [Fact]
    public void TheStarterPackageIsInTheComposedCatalogWithTheStatsOfItsLike()
    {
        using var cache = RestoredHullsTests.OpenCatalog();
        var hull = Wren(cache);
        var like = cache.GetByName<HullData>("Djinni");
        Assert.NotNull(hull);
        Assert.Equal(("Wren", HullType.Ship, like.Mass, like.Durability), (hull.Name, hull.HullType, hull.Mass, hull.Durability));
        Assert.Equal(ShipModCatalog.AuthoringKey(Id), hull.Visual.Key);
        Assert.Equal(2, hull.Hardpoints.Count(h => h.Type == HardpointType.Thruster));
        Assert.Equal(2, hull.Hardpoints.Count(h => h.Type == HardpointType.Energy));
    }

    // The starter's product is its maker's but off the market (rulings package-carries-product, off-market-is-unoffered-product):
    // the census wants every design sold or placed, and no faction fields a stand-in box.
    [Fact]
    public void TheStarterIsMadeButNeverOffered()
    {
        using var cache = RestoredHullsTests.OpenCatalog();
        var hull = Wren(cache);
        var product = Assert.Single(cache.GetAll<FactionProductData>(), p => p.Design.Key.Equals(ShipModCatalog.HullKey(Id)));
        Assert.Equal("Aeronautics Unlimited", cache.Get(product.Manufacturer).Name);
        Assert.True(product.NotOffered);
        Assert.True(hull.Price > 0);
        Assert.DoesNotContain(hull, LoadoutGenerator.SoldHulls(cache));
    }

    // Every mount takes a catalog design: the starter's footprints are sized to gear that exists, so it can be fitted out.
    [Fact]
    public void EveryMountTakesACatalogDesign()
    {
        using var cache = RestoredHullsTests.OpenCatalog();
        var designs = cache.GetAll<EquippableItemData>().ToArray();
        var bare = Wren(cache).Hardpoints.Where(hardpoint => !designs.Any(hardpoint.Takes)).Select(hardpoint => $"{hardpoint.Transform} ({hardpoint.Type})").ToArray();
        // No shield design fits a 1x1 yet (hardpoint-fit lists Djinni's 4-cell shield with none either); every other mount does.
        Assert.Equal(new[] { "internal.shield (Shield)" }, bare);
    }

    [Fact]
    public void TheStarterFliesUnderThrust()
    {
        using var cache = RestoredHullsTests.OpenCatalog();
        var ship = RestoredHullsTests.BuildThrustedShip(cache, "Wren");
        var start = ship.Velocity;
        ship.MovementDirection = float2(0, 1);
        for (var i = 0; i < 10; i++) ship.Zone.Update(1f / 60f);
        Assert.NotEqual(start, ship.Velocity);
        Assert.True(ship.GetBehavior<VelocityLimit>().Limit > 0f);
    }
}
