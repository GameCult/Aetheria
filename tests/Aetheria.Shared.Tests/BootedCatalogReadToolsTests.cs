using System;
using System.IO;
using Xunit;

// Every AetherDb read tool answers for the catalog the game boots (campaign aetheria-living-world, follow-up aetherdb-booted-catalog):
// the shipped GameData/Aetheria.cc with the first-party packages in GameData/Mods composed, through the one AetherDb.ComposeBooted.
// The station hulls besides Zenith are packages', so a tool that opened the shipped file alone answered wrong with exit 0.
[Collection("process working directory")]
public sealed class BootedCatalogReadToolsTests : IDisposable
{
    private readonly string _previous = Environment.CurrentDirectory;
    private readonly TempDirectory _broken = new TempDirectory();
    private readonly string _repo = AetherDb.FindRoot();

    public void Dispose()
    {
        Environment.CurrentDirectory = _previous;
        _broken.Dispose();
    }

    // The Seized Refinery's hull, read from its package: a hull the shipped catalog does not hold.
    private string SeizedRefineryHull()
    {
        var (hull, _, _) = ShipAuthoringStore.Load(Path.Combine(_repo, "GameData", "Mods", "aetheria.seized-refinery", "ship.cc"));
        return hull.Name;
    }

    private string Run(params string[] args)
    {
        Environment.CurrentDirectory = _repo;
        // The exit code counts the tool's own findings (loadout's hulls with no loadout), not whether it ran; the answer is the text.
        return ConsoleProbe.WrittenBy(() => Program.Main(args));
    }

    // A game folder with the shipped catalog and one package the compose refuses.
    private string BrokenRoot()
    {
        Directory.CreateDirectory(Path.Combine(_broken.Path, "GameData", "Mods", "mod.broken"));
        File.Copy(Path.Combine(_repo, "GameData", "Aetheria.cc"), Path.Combine(_broken.Path, "GameData", "Aetheria.cc"));
        File.WriteAllBytes(Path.Combine(_broken.Path, "GameData", "Mods", "mod.broken", "ship.cc"), new byte[] { 1, 2, 3 });
        return _broken.Path;
    }

    // Zenith, plus the Pirates' and Zhestokost's package hulls: three station hulls, not the shipped one.
    [Fact]
    public void CensusCountsThePackageStationHulls() => Assert.Matches(@"Hull/Station\s+3\b", Run("census"));

    [Fact]
    public void StationFitChecksThePackageHulls()
    {
        var output = Run("station-fit");
        Assert.Contains("Arsenal Depot:", output);
        Assert.Contains(SeizedRefineryHull() + ":", output);
    }

    [Fact]
    public void LoadoutListsThePackageStationHulls()
    {
        var output = Run("loadout", "2");
        Assert.Contains("Arsenal Depot", output);
        Assert.Contains(SeizedRefineryHull(), output);
    }

    // economy report and economy balance print no hull, so the pin is the compose itself: a package the game would skip is a throw
    // that names it, and only a tool that composes the packages can throw it. The same holds for every other read tool.
    [Theory]
    [InlineData("census")]
    [InlineData("station-fit")]
    [InlineData("loadout", "2")]
    [InlineData("hardpoint-fit")]
    [InlineData("factions")]
    [InlineData("weapon-ammo")]
    [InlineData("economy", "report")]
    [InlineData("economy", "balance")]
    public void AReadToolThrowsNamingAnExcludedPackage(params string[] args)
    {
        Environment.CurrentDirectory = BrokenRoot();
        var error = Assert.Throws<InvalidOperationException>(() => ConsoleProbe.WrittenBy(() => Program.Main(args)));
        Assert.Contains("mod.broken", error.Message);
    }

    [Fact]
    public void ComposeBootedThrowsNamingAnExcludedPackage()
    {
        var scratch = Directory.CreateDirectory(Path.Combine(_broken.Path, "scratch")).FullName;
        var error = Assert.Throws<InvalidOperationException>(() => AetherDb.ComposeBooted(BrokenRoot(), scratch));
        Assert.Contains("mod.broken", error.Message);
    }
}
