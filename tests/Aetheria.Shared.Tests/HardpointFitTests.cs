using System;
using System.Linq;
using Xunit;

// The hardpoint census (campaign aetheria-release, cut hardpoint-fit-1): every hardpoint on every hull the game boots takes a
// design some product sells. Program.UnfillableHardpoints is the one census, over HardpointData.Takes, the one fit rule.
public sealed class HardpointFitTests
{
    // Every hardpoint on every hull the game boots (shipped catalog plus GameData/Mods) takes a design some product sells.
    [Fact]
    public void EveryHardpointTheGameBootsTakesASoldDesign()
    {
        using var cache = RestoredHullsTests.OpenCatalog();
        Assert.Empty(Program.UnfillableHardpoints(cache));
    }

    // Positive control: the census is not a tautology. The Zenith's (11,6) mount put back to rotation None (its authoring slip, in
    // memory on a read-only catalog) is the one mount no sold design takes, and the census names it.
    [Fact]
    public void TheCensusNamesAMountNoSoldDesignTakes()
    {
        using var cache = RestoredHullsTests.OpenCatalog();
        var mount = Assert.Single(Assert.Single(cache.GetAll<HullData>(), h => h.Name == "Zenith").Hardpoints,
            h => h.Position.x == 11 && h.Position.y == 6);
        Assert.Equal(ItemRotation.Clockwise, mount.Rotation);
        mount.Rotation = ItemRotation.None;
        Assert.Equal(new[] { "Zenith (Station) Radiator 1x4 at (11,6)" }, Program.UnfillableHardpoints(cache));
    }

    // The census is over the catalog the game boots, packages included: a package hull's mount is as fillable as a shipped one.
    [Fact]
    public void TheCensusReadsThePackagesTheGameBoots()
    {
        var root = RunStartTests.FindRepoRoot();
        using (var db = AetherDb.OpenBooted(root: root))
            Assert.Contains(db.Cache.GetAll<HullData>(), hull => hull.Name == "Nakovalnya");
    }
}
