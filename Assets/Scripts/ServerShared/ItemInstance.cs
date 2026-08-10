/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using JsonKnownTypes;
//using JM.LinqFaster;
using MessagePack;
using MessagePack.Formatters;
using Newtonsoft.Json;
using Unity.Mathematics;
using static Unity.Mathematics.math;

[Union(0, typeof(SimpleCommodity)), 
 Union(1, typeof(CompoundCommodity)), 
 Union(2, typeof(EquippableItem)),
 Union(3, typeof(ConsumableItem)),
 JsonObject(MemberSerialization.OptIn), 
 JsonConverter(typeof(JsonKnownTypesConverter<ItemInstance>))]
public abstract class ItemInstance
{
    [JsonProperty("data"), Key(0)] public DatabaseLink<ItemData> Data;
    [JsonProperty("rotation"), Key(1)] public ItemRotation Rotation;
}

[Union(0, typeof(CompoundCommodity)), 
 Union(1, typeof(EquippableItem)), 
 Union(2, typeof(ConsumableItem)), 
 JsonObject(MemberSerialization.OptIn),
 JsonConverter(typeof(JsonKnownTypesConverter<CraftedItemInstance>))]
public abstract class CraftedItemInstance : ItemInstance
{
    [JsonProperty("quality"), Key(2)]  public float Quality;

    //[JsonProperty("ingredients"), Key(3)]  public List<ItemInstance> Ingredients = new List<ItemInstance>();
    
    //[JsonProperty("blueprint"), Key(4)]  public Guid Blueprint;
    
    //[JsonProperty("name"), Key(3)]  public string Name;
    
    //[JsonProperty("sourceEntity"), Key(4)]  public Guid SourceEntity;
}

[MessagePackObject, JsonObject(MemberSerialization.OptIn)]
public class CompoundCommodity : CraftedItemInstance { }

[MessagePackObject, JsonObject(MemberSerialization.OptIn)]
public class SimpleCommodity : ItemInstance
{
    [JsonProperty("quantity"), Key(2)]  public int Quantity;
}

[MessagePackObject, JsonObject(MemberSerialization.OptIn)]
public class EquippableItem : CraftedItemInstance
{
    [JsonProperty("durability"), Key(7)] public float Durability;
    [JsonProperty("override"), Key(8)] public bool OverrideShutdown;
}

[MessagePackObject, JsonObject(MemberSerialization.OptIn)]
public class ConsumableItem : CraftedItemInstance
{
}