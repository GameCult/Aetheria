/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using GameCult.Aetheria.State.Verse;

public sealed class AetheriaUnityLoadoutItemFactory
{
    private readonly ItemManager _itemManager;
    private readonly AetheriaRuntimeCatalogSnapshot _catalog;

    public AetheriaUnityLoadoutItemFactory(
        ItemManager itemManager,
        AetheriaRuntimeCatalogSnapshot catalog)
    {
        _itemManager = itemManager ?? throw new ArgumentNullException(nameof(itemManager));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
    }

    public ItemInstance CreateLoadoutItem(AetheriaRuntimeLoadoutItemCommit item)
    {
        if (item == null)
            return null;

        return CreateLoadoutItem(
            item.ItemKey,
            item.Quality,
            item.Durability,
            Math.Max(1, item.Quantity));
    }

    public ItemInstance CreateLoadoutItem(AetheriaRuntimeLoadoutItemSnapshot item)
    {
        if (item == null)
            return null;

        return CreateLoadoutItem(
            item.ItemKey,
            item.Quality,
            item.Durability,
            Math.Max(1, item.Quantity));
    }

    private ItemInstance CreateLoadoutItem(
        string itemKey,
        double quality,
        double durability,
        int quantity)
    {
        var typedItem = _catalog.FindItem(itemKey ?? "");
        if (typedItem == null)
            return null;

        if (typedItem.Stackable)
            return _itemManager.CreateSimpleCommodityInstance(typedItem, Math.Max(1, quantity));

        var instance = _itemManager.CreateCraftedInstance(typedItem, (float)quality);
        if (instance is EquippableItem equippable && durability > 0)
            equippable.Durability = (float)durability;
        return instance;
    }
}
