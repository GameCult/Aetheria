using System.Linq;
using GameCult.Caching;
using Xunit;

// Ruling ballistics-real-ammo, follow-up launcher-ammunition: a launcher is not a free gun. Read from the shipped catalog through
// AetherDb's own open, so what is checked is what ships.
public sealed class LauncherAmmunitionCatalogTests
{
    private static bool IsLauncher(WeaponData data) => data is LauncherData || data is GuidedWeaponData || data is LockWeaponData;

    // Every launcher design reloads from the Missiles commodity, and Missiles can be made. Kills: a launcher authored (or a new one
    // added) with AmmoType unset, which reloads for free forever; Missiles without a recipe, which no station would ever supply.
    [Fact]
    public void EveryShippedLauncherReloadsFromMissilesAndMissilesCanBeMade()
    {
        using var db = AetherDb.Open();
        var missiles = db.Cache.GetAll<SimpleCommodityData>().Single(c => c.Name == "Missiles");
        var key = db.Cache.RefOf(missiles).Key;
        var launchers = db.Cache.GetAll<WeaponItemData>().SelectMany(i => i.Behaviors.OfType<WeaponData>().Where(IsLauncher).Select(d => (i.Name, d))).ToArray();
        Assert.True(launchers.Length >= 5, $"fixture: the catalog holds its launchers ({launchers.Length})");
        foreach (var (name, data) in launchers)
        {
            Assert.True(data.AmmoType.IsSet(), $"{name} draws no ammunition");
            Assert.Equal(key, data.AmmoType.Key);
        }
        Assert.Contains(db.Cache.GetAll<RecipeData>(), r => r.Output.Key.Equals(key) && r.Inputs.Count > 0);
    }
}
