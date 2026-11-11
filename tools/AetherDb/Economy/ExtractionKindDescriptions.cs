using System;
using GameCult.Caching;

// Economy content, batch 1c: the mining and volatiles station-kind descriptions name every good their kind now extracts.
// MunitionsFeed gave mining a Diamond recipe and volatiles an Ammonia recipe; the descriptions the station-round bench
// wrote predate them. The same text is StationKindsCatalog.Table's, so a catalog authored fresh says the same thing.
// Sets the description whatever it was, so a rerun converges.
public sealed class ExtractionKindDescriptions : EconomyBatch
{
    public override void Author(EconomyAuthor author)
    {
        foreach (var (id, description) in new[]
        {
            ("mining", StationKindsCatalog.DescriptionOf("mining")),
            ("volatiles", StationKindsCatalog.DescriptionOf("volatiles")),
        })
        {
            var key = StationKindData.KeyOf(id);
            var kind = author.Catalog.Get<StationKindData>(key) ?? throw new InvalidOperationException($"The catalog holds no station kind '{id}'.");
            kind.Description = description;
            author.Put(kind, key);
        }
    }
}
