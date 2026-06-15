/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System.Linq;
[Inspectable, Order(-5)]
public class ItemUsageConfig : RuntimeBehaviorConfig
{
    public string ItemKey;

    public override Behavior CreateInstance(EquippedItem item)
    {
        return new ItemUsage(this, item);
    }

    public override Behavior CreateInstance(ConsumableItemEffect item)
    {
        return new ItemUsage(this, item);
    }
}

public class ItemUsage : Behavior
{
    private readonly string _itemKey;

    public ItemUsage(ItemUsageConfig data, EquippedItem item) : base(data, item)
    {
        _itemKey = data.ItemKey;
    }

    public ItemUsage(ItemUsageConfig data, ConsumableItemEffect item) : base(data, item)
    {
        _itemKey = data.ItemKey;
    }

    public override bool Execute(float dt)
    {
        var cargo = Entity.FindItemInCargo(_itemKey);
        if (cargo == null) return false;

        var item = cargo.GetFirstItem(_itemKey);
        if (item is SimpleCommodity simpleCommodity)
            cargo.Remove(simpleCommodity, 1);
        if (item is CraftedItemInstance craftedItemInstance)
            cargo.Remove(craftedItemInstance);

        return true;
    }
}
