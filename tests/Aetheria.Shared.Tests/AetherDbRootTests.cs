using System;
using System.IO;
using Xunit;

[CollectionDefinition("process working directory", DisableParallelization = true)]
public sealed class ProcessWorkingDirectoryCollection { }

// FindRoot reads the process working directory, so these run alone.
[Collection("process working directory")]
public sealed class AetherDbRootTests : IDisposable
{
    private readonly TempDirectory _game = new TempDirectory();
    private readonly string _previous = Environment.CurrentDirectory;

    public void Dispose()
    {
        Environment.CurrentDirectory = _previous;
        _game.Dispose();
    }

    [Fact]
    public void TheRootIsTheNearestFolderWhoseGameDataHoldsAetheriaCc()
    {
        Directory.CreateDirectory(Path.Combine(_game.Path, "GameData", "Mods", "probe.a"));
        File.WriteAllBytes(Path.Combine(_game.Path, "GameData", "Aetheria.cc"), Array.Empty<byte>());
        Environment.CurrentDirectory = _game.Path;
        var game = Environment.CurrentDirectory;
        Environment.CurrentDirectory = Path.Combine(game, "GameData", "Mods", "probe.a");
        Assert.Equal(game, AetherDb.FindRoot());
    }

    [Fact]
    public void AFolderHoldingOnlyTheRepositoryMarkerIsNotARoot()
    {
        Directory.CreateDirectory(Path.Combine(_game.Path, "Aetheria.Shared"));
        File.WriteAllText(Path.Combine(_game.Path, "Aetheria.Shared", "Aetheria.Shared.csproj"), "");
        Environment.CurrentDirectory = _game.Path;
        var error = Assert.Throws<DirectoryNotFoundException>(AetherDb.FindRoot);
        Assert.Contains("GameData holds Aetheria.cc", error.Message);
    }
}
