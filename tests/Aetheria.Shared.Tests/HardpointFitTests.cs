using System;
using System.Linq;
using Xunit;

// The hardpoint census (campaign aetheria-release, cut hardpoint-fit-1): every hardpoint on every hull the game boots takes a
// design some product sells. Program.UnfillableHardpoints is the one census, over HardpointData.Takes, the one fit rule.
public sealed class HardpointFitTests
{
    // The census is over the catalog the game boots, packages included: a package hull's mount is as fillable as a shipped one.
    [Fact]
    public void TheCensusReadsThePackagesTheGameBoots()
    {
        var root = RunStartTests.FindRepoRoot();
        using (var db = AetherDb.Open(root: root, withMods: true))
            Assert.Contains(db.Cache.GetAll<HullData>(), hull => hull.Name == "Nakovalnya");
        Assert.Throws<ArgumentException>(() => AetherDb.Open(catalogWritable: true, root: root, withMods: true));
    }
}
