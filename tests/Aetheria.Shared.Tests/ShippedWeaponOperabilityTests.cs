using System.Linq;
using Xunit;

// A weapon is thermally dead at 0 performance: ThermalOnline is false and wear runs at full rate. Hull cells
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

        var dead = weapons.Where(weapon => weapon.Performance(Entity.StartingCellTemperature) <= 0f)
            .Select(weapon => $"{weapon.Name} ({weapon.MinimumTemperature}..{weapon.MaximumTemperature} K)").ToArray();

        Assert.Empty(dead);
    }
}
