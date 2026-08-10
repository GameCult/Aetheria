/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Linq;
using MessagePack;
using Newtonsoft.Json;

[Inspectable, MessagePackObject, JsonObject(MemberSerialization.OptIn), Order(-5), RuntimeInspectable]
public class ItemUsageData : BehaviorData
{
    [InspectableDatabaseLink(typeof(SimpleCommodityData)), JsonProperty("item"), Key(1), RuntimeInspectable]  
    public Guid Item;
    
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
    private ItemUsageData _data;

    public ItemUsage(ItemUsageData data, EquippedItem item) : base(data, item)
    {
        _data = data;
    }

    public ItemUsage(ItemUsageData data, ConsumableItemEffect item) : base(data, item)
    {
        _data = data;
    }

    public override bool Execute(float dt)
    {
        var cargo = Entity.FindItemInCargo(_data.Item);
        if (cargo == null) return false;

        var item = cargo.ItemsOfType[_data.Item][0];
        if (item is SimpleCommodity simpleCommodity)
            cargo.Remove(simpleCommodity, 1);
        if (item is CraftedItemInstance craftedItemInstance)
            cargo.Remove(craftedItemInstance);
        
        return true;
    }
}