/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System.Linq;
public class ItemUsage : Behavior
{
    private readonly string _itemKey;

    public ItemUsage(RuntimeBehaviorDefinition definition, EquippedItem item) : base(definition, item)
    {
        _itemKey = definition.ItemKey(1);
    }

    public ItemUsage(RuntimeBehaviorDefinition definition, ConsumableItemEffect item) : base(definition, item)
    {
        _itemKey = definition.ItemKey(1);
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
