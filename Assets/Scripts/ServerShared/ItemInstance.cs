/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
//using JM.LinqFaster;
using MessagePack;
using MessagePack.Formatters;
using Unity.Mathematics;
using static Unity.Mathematics.math;

[Union(0, typeof(SimpleCommodity)),
 Union(1, typeof(CompoundCommodity)),
 Union(2, typeof(EquippableItem)),
 Union(3, typeof(ConsumableItem))]
public abstract class ItemInstance
{
    [Key(0)] public DatabaseLink<ItemData> Data;
    [Key(1)] public ItemRotation Rotation;
}

[Union(0, typeof(CompoundCommodity)),
 Union(1, typeof(EquippableItem)),
 Union(2, typeof(ConsumableItem))]
public abstract class CraftedItemInstance : ItemInstance
{
    [Key(2)]  public float Quality;

    //[Key(3)]  public List<ItemInstance> Ingredients = new List<ItemInstance>();

    //[Key(4)]  public Guid Blueprint;

    //[Key(3)]  public string Name;

    //[Key(4)]  public Guid SourceEntity;
}

[MessagePackObject]
public class CompoundCommodity : CraftedItemInstance { }

[MessagePackObject]
public class SimpleCommodity : ItemInstance
{
    [Key(2)]  public int Quantity;
}

[MessagePackObject]
public class EquippableItem : CraftedItemInstance
{
    [Key(7)] public float Durability;
    [Key(8)] public bool OverrideShutdown;
}

[MessagePackObject]
public class ConsumableItem : CraftedItemInstance
{
}