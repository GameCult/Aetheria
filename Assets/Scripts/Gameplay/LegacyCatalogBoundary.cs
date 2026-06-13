using System.IO;

public static class LegacyCatalogBoundary
{
    private static LegacyCatalogCache _catalogCache;

    public static string GetStateFilePath(DirectoryInfo gameDataDirectory)
    {
        return Path.Combine(gameDataDirectory.FullName, "aetheria-world.cc");
    }

    public static string GetLegacyCatalogPath(DirectoryInfo gameDataDirectory)
    {
        return Path.Combine(gameDataDirectory.FullName, "AetherDB.msgpack");
    }

    public static LegacyCatalogCache GetCatalogCache(DirectoryInfo gameDataDirectory)
    {
        if (_catalogCache != null) return _catalogCache;

        _catalogCache = new LegacyCatalogCache();
        _catalogCache.AddBackingStore(new SingleFileMessagePackBackingStore(GetLegacyCatalogPath(gameDataDirectory)));
        _catalogCache.AddBackingStore(new MultiFileMessagePackBackingStore(gameDataDirectory.FullName), typeof(NameFile));
        _catalogCache.PullAllBackingStores();
        DatabaseLinkBase.BindLegacyCatalog(_catalogCache);
        return _catalogCache;
    }
}
