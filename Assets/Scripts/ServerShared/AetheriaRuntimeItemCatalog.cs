using System;
using System.Collections.Generic;
using System.Linq;
using GameCult.Aetheria.State.Unity;

public interface IRuntimeItemCatalogReader
{
    AetheriaRuntimeCatalogItem GetRuntimeItem(string itemKey);
    AetheriaRuntimeCatalogItem GetRuntimeItem(Guid guid);
}

public sealed class AetheriaRuntimeItemCatalog : IRuntimeItemCatalogReader
{
    private const string ItemDefinitionPrefix = "aetheria.item_definition:";
    private const string LegacyItemDefinitionPrefix = "aetheria.item_definition:legacy:";

    private readonly Dictionary<string, AetheriaRuntimeCatalogItem> _typedItemsByKey;
    private readonly Dictionary<Guid, AetheriaRuntimeCatalogItem> _typedItemsByLegacyId;

    public AetheriaRuntimeItemCatalog(AetheriaRuntimeCatalogSnapshot catalog)
    {
        if (catalog == null) throw new ArgumentNullException(nameof(catalog));

        _typedItemsByKey = catalog.Items
            .Where(item => !string.IsNullOrWhiteSpace(item.LegacyId))
            .ToDictionary(item => ToItemKey(item.LegacyId), item => item, StringComparer.OrdinalIgnoreCase);

        _typedItemsByLegacyId = catalog.Items
            .Where(item => Guid.TryParse(item.LegacyId, out _))
            .ToDictionary(item => Guid.Parse(item.LegacyId), item => item);
    }

    public AetheriaRuntimeCatalogItem GetRuntimeItem(string itemKey)
    {
        if (string.IsNullOrWhiteSpace(itemKey))
            return null;

        if (_typedItemsByKey.TryGetValue(itemKey, out var item))
            return item;

        return Guid.TryParse(RemoveItemPrefix(itemKey), out var legacyId)
            ? GetRuntimeItem(legacyId)
            : null;
    }

    public AetheriaRuntimeCatalogItem GetRuntimeItem(Guid guid)
    {
        _typedItemsByLegacyId.TryGetValue(guid, out var item);
        return item;
    }

    private static string ToItemKey(string legacyId)
    {
        return string.IsNullOrWhiteSpace(legacyId) ? "" : $"{LegacyItemDefinitionPrefix}{legacyId}";
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
