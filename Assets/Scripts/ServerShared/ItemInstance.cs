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
    // Keys 2 (Quality), 9 (Ingredients) and 10 (Product) belonged to fields moved to Lot; do not reuse them.

    // The lot this unit was minted from: its design, provenance, workmanship and role fills all live there.
    [JsonProperty("lot"), Key(11)] public int Lot;
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

    // Cut 5 (docs/stats-and-power-cut.md §1.3, §0b "A power tier"): a player choice, stored on the unit rather
    // than the design, so two units of the same design can be prioritized differently. PowerTiers.Unassigned
    // until the first equip seeds a behaviour-kind default (EquippedItem's constructor) or the player overrides
    // it from the schematic UI; from then on PowerBus only ever reads it, never derives it. An old run store
    // missing key 12 deserializes to this same field initializer, so no migration is needed (§ Cut 5).
    [JsonProperty("powerTier"), Key(12)] public int PowerTier = PowerTiers.Unassigned;
}

[MessagePackObject, JsonObject(MemberSerialization.OptIn)]
public class ConsumableItem : CraftedItemInstance
{
}
