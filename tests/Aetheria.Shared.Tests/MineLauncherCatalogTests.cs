using System.Linq;
using Xunit;

// sim-mines-presenter: the shipped catalog carries the Mine Launcher a ship can be fitted with, and what it
// carries is what the sim's mine layer reads (MineLayerData for the mine's own timings, the item's WeaponType,
// Fuse and BlastRadius for the layer and the blast).
public sealed class MineLauncherCatalogTests
{
    [Fact]
    public void TheShippedCatalogHasAMineLauncherThatLaysMines()
    {
        var cache = RestoredHullsTests.OpenCatalog();
        var launcher = Assert.Single(cache.GetAll<GearData>().OfType<WeaponItemData>(), item => item.Name == "Mine Launcher");

        Assert.Equal(HardpointType.Launcher, launcher.HardpointType);
        Assert.NotNull(launcher.Shape);
        Assert.Contains(cache.GetAll<FactionProductData>(), product => cache.RefOf(launcher).Key.Equals(product.Design.Key));
        Assert.Equal(WeaponType.Mine, launcher.WeaponType);
        Assert.Equal(WeaponFuse.Proximity, launcher.Fuse);
        Assert.Equal(25f, launcher.BlastRadius);

        var layer = Assert.IsType<MineLayerData>(Assert.Single(launcher.Behaviors));
        Assert.Equal(2f, layer.ArmingDelay);
        Assert.Equal(2f, layer.FuseDelay);
        Assert.Equal(30f, layer.Lifetime);
        Assert.True(layer.Damage.Max > 0f);
        Assert.True(layer.Velocity.Max > 0f);
        Assert.True(layer.Cooldown.Max > 0f);
    }
}
