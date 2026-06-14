using System;
using System.Collections.Generic;
using System.Linq;
using GameCult.Aetheria.State.Unity;

public interface IRuntimeItemCatalogReader
{
    AetheriaRuntimeCatalogItem GetRuntimeItem(Guid guid);
}

public sealed class AetheriaRuntimeItemCatalog : IRuntimeItemCatalogReader
{
    private readonly Dictionary<Guid, AetheriaRuntimeCatalogItem> _typedItems;

    public AetheriaRuntimeItemCatalog(AetheriaRuntimeCatalogSnapshot catalog)
    {
        if (catalog == null) throw new ArgumentNullException(nameof(catalog));

        _typedItems = catalog.Items
            .Where(item => Guid.TryParse(item.LegacyId, out _))
            .ToDictionary(item => Guid.Parse(item.LegacyId), item => item);
    }

    public AetheriaRuntimeCatalogItem GetRuntimeItem(Guid guid)
    {
        _typedItems.TryGetValue(guid, out var item);
        return item;
    }
}
