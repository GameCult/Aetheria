using System;
using System.IO;
using System.Linq;
using Xunit;

// A player directory's GameData is exactly the catalog, Narrative/** and (when the repo has them) Mods/**.
public sealed class PlayerStagingTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "aetheria-staging-" + Guid.NewGuid().ToString("N"));
    private string Repo => Path.Combine(_root, "repo");
    private string Player => Path.Combine(_root, "player");
    private string Source(string relative) => Path.Combine(Repo, "GameData", relative);
    private string Staged(string relative) => Path.Combine(Player, "GameData", relative);

    public PlayerStagingTests()
    {
        Write(Source("Aetheria.cc"), "catalog-bytes");
        Write(Source("Narrative/Main.ink"), "main");
        Write(Source("Narrative/Quests/Deep/First.ink"), "first");
        Write(Source("SoundbanksInfo.json"), "{}");
        Write(Source("run.cc"), "a developer's run");
        Write(Source("player.cc"), "a developer's settings");
        Directory.CreateDirectory(Player);
    }

    public void Dispose() => Directory.Delete(_root, true);

    [Fact]
    public void StagesTheCatalogAndNarrativeFileForFile()
    {
        PlayerStaging.Stage(Repo, Player);

        Assert.Equal("catalog-bytes", File.ReadAllText(Staged("Aetheria.cc")));
        Assert.Equal("main", File.ReadAllText(Staged("Narrative/Main.ink")));
        Assert.Equal("first", File.ReadAllText(Staged("Narrative/Quests/Deep/First.ink")));
    }

    [Fact]
    public void StagesModsWhenTheRepoHasThemAndNoModsFolderWhenItDoesNot()
    {
        PlayerStaging.Stage(Repo, Player);
        Assert.False(Directory.Exists(Staged("Mods")));

        Write(Source("Mods/ship-a/ship.cc"), "ship");
        Write(Source("Mods/ship-a/mesh/hull.glb"), "hull");
        PlayerStaging.Stage(Repo, Player);

        Assert.Equal("ship", File.ReadAllText(Staged("Mods/ship-a/ship.cc")));
        Assert.Equal("hull", File.ReadAllText(Staged("Mods/ship-a/mesh/hull.glb")));
    }

    [Fact]
    public void StagesNothingElseFromGameData()
    {
        PlayerStaging.Stage(Repo, Player);

        var staged = Directory.GetFileSystemEntries(Path.Combine(Player, "GameData")).Select(Path.GetFileName).OrderBy(name => name).ToArray();
        Assert.Equal(new[] { "Aetheria.cc", "Narrative" }, staged);
    }

    [Fact]
    public void ReplacesWhatAnEarlierBuildLeftInGameData()
    {
        Write(Staged("Aetheria.cc"), "stale");
        Write(Staged("run.cc"), "a played run");
        Write(Staged("Mods/gone/ship.cc"), "removed mod");

        PlayerStaging.Stage(Repo, Player);

        Assert.Equal("catalog-bytes", File.ReadAllText(Staged("Aetheria.cc")));
        Assert.False(File.Exists(Staged("run.cc")));
        Assert.False(Directory.Exists(Staged("Mods")));
    }

    [Fact]
    public void RefusesAMissingCatalogNamingItAndLeavesNoGameData()
    {
        File.Delete(Source("Aetheria.cc"));

        var error = Assert.Throws<InvalidOperationException>(() => PlayerStaging.Stage(Repo, Player));

        Assert.Contains("GameData/Aetheria.cc", error.Message);
        Assert.False(Directory.Exists(Path.Combine(Player, "GameData")));
    }

    [Fact]
    public void RefusesMissingNarrative()
    {
        Directory.Delete(Source("Narrative"), true);

        var error = Assert.Throws<InvalidOperationException>(() => PlayerStaging.Stage(Repo, Player));

        Assert.Contains("GameData/Narrative", error.Message);
    }

    private static void Write(string path, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, text);
    }
}
