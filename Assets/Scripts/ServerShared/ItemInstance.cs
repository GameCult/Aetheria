/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

public abstract class ItemInstance
{
    public AetheriaRuntimeItemReference Reference;
    public ItemRotation Rotation;

    public string ItemKey => Reference?.ItemKey ?? "";
}

public abstract class CraftedItemInstance : ItemInstance
{
    public float Quality;
}

public class CompoundCommodity : CraftedItemInstance { }

public class SimpleCommodity : ItemInstance
{
    public int Quantity;
}

public class EquippableItem : CraftedItemInstance
{
    public float Durability;
    public bool OverrideShutdown;
}

public class ConsumableItem : CraftedItemInstance
{
}
