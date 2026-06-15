using System;
using System.Collections.Generic;
using System.Linq;
using GameCult.Aetheria.State.Unity;

public interface IRuntimeItemCatalogReader
{
    AetheriaRuntimeCatalogItem GetRuntimeItem(string itemKey);
}

public sealed class AetheriaRuntimeItemCatalog : IRuntimeItemCatalogReader
{
    private const string ItemDefinitionPrefix = "aetheria.item_definition:";
    private const string LegacyItemDefinitionPrefix = "aetheria.item_definition:legacy:";

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

        if (_typedItemsByKey.TryGetValue(itemKey, out var item))
            return item;

        return Guid.TryParse(RemoveItemPrefix(itemKey), out var legacyId)
            ? GetRuntimeItem(AetheriaRuntimeItemReference.FromLegacyId(legacyId))
            : null;
    }

    private static string RemoveItemPrefix(string itemKey)
    {
        if (itemKey.StartsWith(LegacyItemDefinitionPrefix, StringComparison.OrdinalIgnoreCase))
            return itemKey.Substring(LegacyItemDefinitionPrefix.Length);

        return itemKey.StartsWith(ItemDefinitionPrefix, StringComparison.OrdinalIgnoreCase)
            ? itemKey.Substring(ItemDefinitionPrefix.Length)
            : itemKey;
    }
}
