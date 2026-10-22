using System;
using System.IO;
using System.Linq;
using GameCult.Caching;
using GameCult.Caching.MessagePack;
using Xunit;

// tests/vectors/cultlib-1.0.60/run.cc is a run.cc written by Aetheria on CultLib 1.0.60 (64 zones, factions, one entity).
// A player's save outlives the runtime that wrote it, so the current runtime must open it as compatible drift.
public sealed class RunSaveCompatibilityTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "aetheria-runcompat-" + Guid.NewGuid().ToString("N"));

    public RunSaveCompatibilityTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, true);

    [Fact]
    public void ARunWrittenByCultLib1060OpensWithEveryZoneAndFactionResolving()
    {
        var run = Path.Combine(_root, "run.cc");
        File.Copy(Path.Combine(RunStartTests.FindRepoRoot(), "tests", "vectors", "cultlib-1.0.60", "run.cc"), run);
        using var cache = new CultCache(TestCatalog.Registry());
        cache.AddBackingStore(new SingleFileMessagePackBackingStore(TestCatalog.Repo, true), AetheriaStores.CatalogTypes);
        cache.AddBackingStore(new SingleFileMessagePackBackingStore(run), AetheriaStores.RunTypes);
        var saved = cache.GetGlobal<SavedGame>();
        Assert.NotNull(saved);
        Assert.NotEmpty(saved.Zones);
        Assert.NotEmpty(saved.Factions);
        Assert.All(saved.Zones, zone => Assert.NotNull(cache.Get(zone)));
        Assert.All(saved.Factions, faction => Assert.NotNull(cache.Get(faction)));
    }
}
