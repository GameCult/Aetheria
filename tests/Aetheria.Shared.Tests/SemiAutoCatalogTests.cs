using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GameCult.Caching;
using GameCult.Caching.MessagePack;
using Xunit;

// Fire mode is a design property (ruling fire-mode-is-the-design): a plain InstantWeapon is semi-automatic, and its cycle
// stays at a pace a finger can keep. A gun that cycles faster is an AutoWeapon.
public sealed class SemiAutoCatalogTests
{
    private static PerformanceStat Const(float v) => new PerformanceStat { Min = v, Max = v };

    private static GearData Gun(string name, InstantWeaponData data) => new GearData { Name = name, Hardpoint = HardpointType.Sensors, Shape = new Shape(), Durability = 1, Behaviors = { data } };

    [Fact]
    public void TheShippedCatalogAndItsPackagesHoldNoFasterPlainGun()
    {
        var root = RunStartTests.FindRepoRoot();
        using var work = new TempDirectory();
        var (catalog, excluded) = ShipModCatalog.ResolveCatalog(Path.Combine(root, "GameData", "Aetheria.cc"), Path.Combine(work.Path, "derived.cc"),
            Path.Combine(root, "GameData", "Mods"), TestCatalog.Registry());
        Assert.Empty(excluded);
        using var cache = new CultCache(TestCatalog.Registry());
        cache.AddBackingStore(new SingleFileMessagePackBackingStore(catalog, true), AetheriaStores.CatalogTypes);
        var gear = cache.GetAll<GearData>().ToArray();
        Assert.Empty(InstantWeaponData.SemiAutoFaults(cache.GetAll<ItemData>()));
        // The check reads a catalog that holds guns, and the two automatic ones are AutoWeapons.
        foreach (var name in new[] { "Slug Rifle", "Long Slug Rifle" })
            Assert.IsType<AutoWeaponData>(gear.Single(g => g.Name == name).Behaviors.Single(b => b is InstantWeaponData));
        Assert.Contains(gear, g => g.Behaviors.Any(b => b is InstantWeaponData));
    }

    [Fact]
    public void ThePlainGunCheckRefusesAFastSemiAutoAndOnlyThat()
    {
        var floor = InstantWeaponData.ShortestSemiAutoCooldown;
        var fast = Gun("Fast", new InstantWeaponData { Cooldown = Const(floor / 2) });
        var edge = Gun("Edge", new InstantWeaponData { Cooldown = Const(floor) });
        var auto = Gun("Auto", new AutoWeaponData { Cooldown = Const(.01f) });
        var wide = Gun("Wide", new InstantWeaponData { Cooldown = new PerformanceStat { Min = floor / 2, Max = floor * 2 } });
        var faults = InstantWeaponData.SemiAutoFaults(new[] { fast, edge, auto, wide }).ToArray();
        // The floor is 0.1 s: ten presses a second is allowed, twenty is not.
        Assert.Equal(.1f, floor);
        Assert.Single(InstantWeaponData.SemiAutoFaults(new[] { Gun("Twenty", new InstantWeaponData { Cooldown = Const(.05f) }) }));
        Assert.Empty(InstantWeaponData.SemiAutoFaults(new[] { Gun("Ten", new InstantWeaponData { Cooldown = Const(.1f) }) }));
        Assert.Equal(2, faults.Length);
        Assert.Contains(faults, f => f.StartsWith("Fast:"));
        Assert.Contains(faults, f => f.StartsWith("Wide:"));
    }

    // Fire mode is one property of the weapon's data: every kind but AutoWeaponData is semi-automatic, and the floor holds each of
    // them, as gear or as a consumable. An automatic gun is exempt wherever it is carried.
    public static IEnumerable<object[]> Kinds() => new[]
    {
        new object[] { "plain", (Func<InstantWeaponData>) (() => new InstantWeaponData()) },
        new object[] { "guided", (Func<InstantWeaponData>) (() => new GuidedWeaponData()) },
        new object[] { "mine", (Func<InstantWeaponData>) (() => new MineLayerData()) },
        new object[] { "launcher", (Func<InstantWeaponData>) (() => new LauncherData()) },
        new object[] { "charged", (Func<InstantWeaponData>) (() => new ChargedWeaponData { CanFireEarly = true }) },
    };

    [Theory, MemberData(nameof(Kinds))]
    public void TheFloorRefusesEverySemiAutoKindBelowItAsGearOrConsumable(string kind, Func<InstantWeaponData> make)
    {
        InstantWeaponData At(float cooldown) { var data = make(); data.Cooldown = Const(cooldown); return data; }
        Assert.False(make().Automatic);
        var floor = InstantWeaponData.ShortestSemiAutoCooldown;
        var slow = At(floor);
        var fast = At(floor / 2);
        Assert.Empty(InstantWeaponData.SemiAutoFaults(new ItemData[] { Gun(kind, slow), new ConsumableItemData { Name = kind + " dose", Behaviors = { slow } } }));
        var faults = InstantWeaponData.SemiAutoFaults(new ItemData[] { Gun(kind, fast), new ConsumableItemData { Name = kind + " dose", Behaviors = { fast } } }).ToArray();
        Assert.Equal(2, faults.Length);
        Assert.Contains(faults, f => f.StartsWith(kind + ":"));
        Assert.Contains(faults, f => f.StartsWith(kind + " dose:"));
    }

    [Fact]
    public void AnAutomaticGunIsExemptAsGearOrConsumable()
    {
        var auto = new AutoWeaponData { Cooldown = Const(.01f) };
        Assert.True(auto.Automatic);
        Assert.Empty(InstantWeaponData.SemiAutoFaults(new ItemData[] { Gun("Auto", auto), new ConsumableItemData { Name = "Auto dose", Behaviors = { auto } } }));
    }
}
