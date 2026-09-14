/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using GameCult.Caching;
//using JM.LinqFaster;
using MessagePack;
using MessagePack.Formatters;
using Newtonsoft.Json;
using CultMath;
using static CultMath.math;

[Union(0, typeof(SimpleCommodity)),
 Union(1, typeof(CompoundCommodity)),
 Union(2, typeof(EquippableItem)),
 Union(3, typeof(ConsumableItem)),
 JsonObject(MemberSerialization.OptIn)]
public abstract class ItemInstance
{
    [JsonProperty("data"), Key(0)] public CultRecordRef<ItemData> Data;
    [JsonProperty("rotation"), Key(1)] public ItemRotation Rotation;
}

[Union(0, typeof(CompoundCommodity)),
 Union(1, typeof(EquippableItem)),
 Union(2, typeof(ConsumableItem)),
 JsonObject(MemberSerialization.OptIn)]
public abstract class CraftedItemInstance : ItemInstance
{
    // The workmanship of this particular unit, as distinct from the parts that went into it.
    [JsonProperty("quality"), Key(2)]  public float Quality;

    // How good the part filling each of the design's roles turned out. Empty on items made before roles
    // existed; stats then read Quality as they always did.
    [JsonProperty("ingredients"), Key(9)]  public List<RoleFill> Ingredients = new List<RoleFill>();

    // The manufacturer's branded product this was built as, naming it and carrying its flavor text.
    [JsonProperty("product"), Key(10)]  public CultRecordRef<FactionProductData> Product;

    // The quality a stat reads for one of the design's roles. An unnamed role, a design without roles, and an
    // item built before roles existed all fall back to this item's own workmanship.
    public float QualityForRole(string role)
    {
        if (string.IsNullOrEmpty(role) || Ingredients == null) return Quality;
        foreach (var fill in Ingredients)
            if (fill.Role == role) return fill.Quality;
        return Quality;
    }
}

// One filled slot: the design's role name and how good the part that went into it turned out to be.
[MessagePackObject, JsonObject(MemberSerialization.OptIn)]
public class RoleFill
{
    [JsonProperty("role"), Key(0)]  public string Role;
    [JsonProperty("quality"), Key(1)]  public float Quality;
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
