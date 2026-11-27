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

    // The lot a crafted unit was minted from: its design, provenance, workmanship and role fills all live there. A
    // simple commodity has none (ruling simple-commodities-fungible).
    [JsonProperty("lot"), Key(11)] public int Lot;
}

// A commodity that stacks: the one owner of which units are interchangeable (StacksWith) and of what a split carries
// (Split). The cargo bay applies it at every merge and ItemManager.MaxStack bounds it; nothing else decides either.
public interface ICommodityStack
{
    int Quantity { get; set; }

    // True when `other` is a unit this stack may merge with.
    bool StacksWith(ItemInstance other);

    // A new stack of `quantity` units interchangeable with this one; this stack is left as it was.
    ItemInstance Split(int quantity);
}

// Units of a compound commodity merge only within one lot (ruling compound-commodities-stack-by-lot): the lot is the
// units' provenance, workmanship and role fills.
[MessagePackObject, JsonObject(MemberSerialization.OptIn)]
public class CompoundCommodity : CraftedItemInstance, ICommodityStack
{
    // Keys 2, 9, 10 and 11 are CraftedItemInstance's and 12 EquippableItem's. A unit saved before Quantity existed has
    // no slot 13 and reads one.
    [JsonProperty("quantity"), Key(13)] public int Quantity { get; set; } = 1;

    public bool StacksWith(ItemInstance other) =>
        other is CompoundCommodity compound && compound.Data.Key.Equals(Data.Key) && compound.Lot == Lot;

    public ItemInstance Split(int quantity) =>
        new CompoundCommodity { Data = Data, Rotation = Rotation, Lot = Lot, Quantity = quantity };
}

[MessagePackObject, JsonObject(MemberSerialization.OptIn)]
public class SimpleCommodity : ItemInstance, ICommodityStack
{
    // Key 11 belonged to a lot before ruling simple-commodities-fungible and is never reused; a pre-ruling stack's
    // slot 11 is skipped on read.
    [JsonProperty("quantity"), Key(2)]  public int Quantity { get; set; }

    public bool StacksWith(ItemInstance other) => other is SimpleCommodity && other.Data.Key.Equals(Data.Key);

    public ItemInstance Split(int quantity) => new SimpleCommodity { Data = Data, Rotation = Rotation, Quantity = quantity };
}

[MessagePackObject, JsonObject(MemberSerialization.OptIn)]
public class EquippableItem : CraftedItemInstance
{
    [JsonProperty("durability"), Key(7)] public float Durability;
    [JsonProperty("override"), Key(8)] public bool OverrideShutdown;

    // The one rule for a destroyed item: its wear is spent.
    [IgnoreMember] public bool IsDestroyed => Durability < .01f;

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
