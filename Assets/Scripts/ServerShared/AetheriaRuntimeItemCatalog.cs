using System.Collections.Generic;
using System.Linq;
using GameCult.Aetheria.State.Unity;

public interface IRuntimeItemCatalogReader
{
    AetheriaRuntimeCatalogItem GetRuntimeItem(string itemKey);
}

public sealed class AetheriaRuntimeItemCatalog : IRuntimeItemCatalogReader
{
    private readonly Dictionary<string, AetheriaRuntimeCatalogItem> _typedItemsByKey;

    public AetheriaRuntimeItemCatalog(AetheriaRuntimeCatalogSnapshot catalog)
    {
        if (catalog == null) throw new ArgumentNullException(nameof(catalog));

        _typedItemsByKey = catalog.Items
            .Where(item => !string.IsNullOrWhiteSpace(item.ItemKey))
            .ToDictionary(item => item.ItemKey, item => item, StringComparer.OrdinalIgnoreCase);
    }

    public AetheriaRuntimeCatalogItem GetRuntimeItem(string itemKey)
    {
        if (string.IsNullOrWhiteSpace(itemKey))
            return null;

        return _typedItemsByKey.TryGetValue(itemKey, out var item)
            ? item
            : null;
    }
}
