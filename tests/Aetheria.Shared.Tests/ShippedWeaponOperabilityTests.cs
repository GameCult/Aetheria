using System.Linq;
using Xunit;

// A weapon is thermally online only while its performance exceeds the shutdown threshold (Entity.UpdatePerformance
// compares against the catalog settings' DefaultEntitySettings.ShutdownPerformance, which production clones), and wear runs at full rate below it. Hull cells
// start at Entity.StartingCellTemperature, so every shipped weapon must perform there, or a ship fitted with it
// can never fire it. The catalog comes from the shipped GameData/Aetheria.cc and the temperature from the
// constant Entity builds its cells with, so a unit slip in authoring (celsius for kelvin) fails here.
public sealed class ShippedWeaponOperabilityTests
{
    [Fact]
    public void EveryShippedWeaponPerformsAtAHullsStartingTemperature()
    {
        using var cache = RestoredHullsTests.OpenCatalog();
        var weapons = cache.GetAll<GearData>().OfType<WeaponItemData>().ToArray();
        Assert.NotEmpty(weapons);

        var threshold = RestoredHullsTests.Settings().DefaultEntitySettings.ShutdownPerformance;
        Assert.True(threshold > 0f);

        var dead = weapons.Where(weapon => weapon.Performance(Entity.StartingCellTemperature) <= threshold)
            .Select(weapon => $"{weapon.Name} ({weapon.MinimumTemperature}..{weapon.MaximumTemperature} K)").ToArray();

        Assert.Empty(dead);
    }
}
