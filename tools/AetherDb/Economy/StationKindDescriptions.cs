using System;
using GameCult.Caching;

// Economy content: station-kind descriptions that name only goods the recipe graph makes or takes. The station-round bench wrote
// descriptions naming Uranium, Hydrogen, Bacteria, Fungi and Animal Products, none of which any recipe yields or consumes; this
// restates the five kinds that named them (mining, volatiles, agriculture, habitat-support, power) as StationKindsCatalog.Table
// has them, so a catalog authored fresh says the same thing. Sets the description whatever it was, so a rerun converges.
public sealed class StationKindDescriptions : EconomyBatch
{
    public override void Author(EconomyAuthor author)
    {
        foreach (var (id, description) in new[]
        {
            ("mining", StationKindsCatalog.DescriptionOf("mining")),
            ("volatiles", StationKindsCatalog.DescriptionOf("volatiles")),
            ("agriculture", StationKindsCatalog.DescriptionOf("agriculture")),
            ("habitat-support", StationKindsCatalog.DescriptionOf("habitat-support")),
            ("power", StationKindsCatalog.DescriptionOf("power")),
        })
        {
            var key = StationKindData.KeyOf(id);
            var kind = author.Catalog.Get<StationKindData>(key) ?? throw new InvalidOperationException($"The catalog holds no station kind '{id}'.");
            kind.Description = description;
            author.Put(kind, key);
        }
    }
}
