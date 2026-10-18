using System.Collections.Generic;
using GameCult.Caching;

// A fixture's run at a fixed seed, restored as Continue restores it: one zone, committed through RunSave.Commit and
// read back by the Galaxy save constructor, so a test's galaxy carries the seed the way a real run's does.
internal static class RunSeedFixture
{
    public static Galaxy Continue(CultCache cache, string zoneName, uint seed)
    {
        var game = new SavedGame
        {
            Seed = seed,
            Factions = new CultRecordRef<Faction>[0],
            Relationships = new FactionRelationship[0],
            HomeZones = new Dictionary<int, int>(),
            BossZones = new Dictionary<int, int>(),
            DiscoveredZones = new[] { 0 },
            ActionBarBindings = new SavedActionBarBinding[0],
            Entrance = 0,
            Exit = -1
        };
        var zone = new SavedZone { Name = zoneName, AdjacentZones = new int[0], Factions = new int[0], Owner = -1 };
        RunSave.Commit(cache, game, new[] { zone }, new ProvenanceLedger());
        return new Galaxy(cache, cache.GetGlobal<SavedGame>(), _ => { });
    }
}
