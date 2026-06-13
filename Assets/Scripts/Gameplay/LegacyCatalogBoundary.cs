using System.IO;

public static class LegacyItemCatalogBoundary
{
    private static LegacyItemCatalogCache _catalogCache;

    public static string GetLegacyItemCatalogPath(DirectoryInfo gameDataDirectory)
    {
        return Path.Combine(gameDataDirectory.FullName, "AetherDB.msgpack");
    }

    public static ILegacyItemCatalogReader GetCatalog(DirectoryInfo gameDataDirectory)
    {
        if (_catalogCache != null) return _catalogCache;

        _catalogCache = new LegacyItemCatalogCache();
        _catalogCache.AddBackingStore(new SingleFileMessagePackBackingStore(GetLegacyItemCatalogPath(gameDataDirectory)));
        _catalogCache.PullAllBackingStores();
        DatabaseLinkBase.BindLegacyItemCatalog(_catalogCache);
        return _catalogCache;
    }
}
