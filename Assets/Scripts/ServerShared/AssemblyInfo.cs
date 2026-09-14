using GameCult.Caching.MessagePack;

// CultCache serializes this assembly's documents with MathResolver ahead of MessagePack's standard resolvers.
[assembly: CultCacheFormatterResolver(typeof(MathResolver))]
