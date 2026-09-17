/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GameCult.Caching;
using CultMath;
using static CultMath.math;
using float2 = CultMath.float2;
using Random = CultMath.Random;
using JM.LinqFaster;
using UniRx;
using float4 = CultMath.float4;

public class ItemManager
{
    public Random Random = new Random((uint) (DateTime.Now.Ticks%uint.MaxValue));

    private Action<string> _logger;

    public CultCache ItemData { get; }
    public ProvenanceLedger Lots { get; }
    public GameplaySettings GameplaySettings { get; }

    public ItemManager(CultCache itemData, ProvenanceLedger lots, GameplaySettings settings, Action<string> logger)
    {
        ItemData = itemData;
        Lots = lots;
        GameplaySettings = settings;
        _logger = logger;
    }

    public void Log(string s)
    {
        _logger(s);
    }

    // The one resolution path from an item instance to its design
    public ItemData GetData(ItemInstance item) => ItemData.Get(item.Data);

    public SimpleCommodityData GetData(SimpleCommodity item) => GetData((ItemInstance) item) as SimpleCommodityData;

    public CraftedItemData GetData(CraftedItemInstance item) => GetData((ItemInstance) item) as CraftedItemData;

    public EquippableItemData GetData(EquippableItem item) => GetData((ItemInstance) item) as EquippableItemData;

    public float GetMass(ItemInstance item)
    {
        return item switch
        {
            CraftedItemInstance _ => GetData(item).Mass,
            SimpleCommodity commodity => GetData(item).Mass * commodity.Quantity,
            _ => 0
        };
    }

    public float GetThermalMass(ItemInstance item)
    {
        var data = GetData(item);
        return item switch
        {
            CraftedItemInstance _ => data.Mass * data.SpecificHeat,
            SimpleCommodity commodity => data.Mass * data.SpecificHeat * commodity.Quantity,
            _ => 0
        };
    }

    // The one resolution path from a crafted instance to the lot it was minted from.
    public Lot GetLot(CraftedItemInstance item) => Lots[item.Lot];

    // Returns stat when not equipped
    public float Evaluate(PerformanceStat stat, EquippableItem item)
    {
        var data = GetData(item);
        var lot = GetLot(item);
        var quality = pow(lot.QualityForRole(stat.FromRole), stat.QualityExponent);
        var durabilityExponent = lerp(
            GameplaySettings.DurabilityQualityMin,
            GameplaySettings.DurabilityQualityMax,
            pow(lot.Quality, GameplaySettings.DurabilityQualityExponent));
        var durability = pow(item.Durability / data.Durability, durabilityExponent * stat.DurabilityExponentMultiplier);
        var result = lerp(stat.Min, stat.Max, quality * durability);
        if (float.IsNaN(result))
            throw new InvalidOperationException($"Performance Stat on {data.Name} evaluating as NaN: input data is invalid! Durability: {item.Durability} / {data.Durability}");
        return result;

    }

    public int GetPrice(CraftedItemInstance item)
    {
        var data = GetData(item);
        return (int) (GameplaySettings.QualityPriceModifier.Evaluate(GetLot(item).Quality) * data.Price);
    }

    // The tier roll every mint applies, whether or not the lot fills roles.
    public float RollQuality()
    {
        var quality = Random.NextFloat();
        var tier = GameplaySettings.Tiers[0];
        foreach (var t in GameplaySettings.Tiers)
        {
            if (t.Rarity > quality)
                tier = t;
        }

        return tier.Quality;
    }

    // Builds one of a manufacturer's branded products: the unit's own workmanship rolled as before, and each of
    // the design's roles filled with a part whose quality is drawn from that manufacturer's distribution for it.
    public int CreateLot(FactionProductData product)
    {
        var design = ItemData.Get(product.Design);
        var lot = new Lot
        {
            Design = ItemData.RefOf<ItemData>(design),
            Origin = new Attributed { Faction = product.Manufacturer },
            Quality = RollQuality(),
            Roles = new List<RoleFill>()
        };
        if (design.Roles != null)
            foreach (var role in design.Roles)
            {
                var build = product.Roles?.FirstOrDefault(b => b.Role == role.Name) ?? new ProductRole();
                lot.Roles.Add(new RoleFill
                {
                    Role = role.Name,
                    Quality = clamp(Random.NextGaussian(build.Mean, build.StandardDeviation), .01f, 1)
                });
            }

        return Lots.Add(lot);
    }

    // No branded product: a lot attributed to maker (which may be unset) with no role fills, e.g. console `give`.
    public int CreateLot(CraftedItemData design, CultRecordRef<Faction> maker, float quality)
    {
        var lot = new Lot
        {
            Design = ItemData.RefOf<ItemData>(design),
            Origin = new Attributed { Faction = maker },
            Quality = quality
        };
        return Lots.Add(lot);
    }

    // The one writer of a crafted instance's Data: copied from the lot's design, never independently set.
    public CraftedItemInstance CreateInstance(int lot)
    {
        var l = Lots[lot];
        var design = ItemData.Get(l.Design) as CraftedItemData;
        if (design is EquippableItemData equippableItemData)
        {
            return new EquippableItem
            {
                Data = l.Design, Lot = lot, Durability = equippableItemData.Durability
            };
        }

        return new CompoundCommodity
        {
            Data = l.Design, Lot = lot
        };
    }

    public CraftedItemInstance CreateInstance(FactionProductData product)
    {
        var design = ItemData.Get(product.Design);
        if (design == null)
        {
            _logger($"Product {product.Name} names a design that does not exist!");
            return null;
        }

        return CreateInstance(CreateLot(product));
    }

    // Who made an item and what it is branded as, derived from its lot's provenance. Null maker (an unset faction,
    // or an Extracted lot with none) means no brand at all; a set maker with no matching product still shows the
    // maker, with no product name or flavour text.
    public (Faction Maker, FactionProductData Product) Brand(CraftedItemInstance item)
    {
        var lot = GetLot(item);
        var maker = lot.Origin switch
        {
            Attributed attributed => attributed.Faction,
            Produced produced => produced.Faction,
            _ => default
        };
        if (!maker.IsSet()) return (null, null);

        var product = ItemData.GetAll<FactionProductData>()
            .Where(p => p.Manufacturer.Key.Equals(maker.Key) && p.Design.Key.Equals(item.Data.Key))
            .OrderBy(p => ItemData.RefOf(p).Key.Value, StringComparer.Ordinal)
            .FirstOrDefault();
        return (ItemData.Get(maker), product);
    }

    public (RarityTier tier, int upgrades) GetTier(CraftedItemInstance item)
    {
        var quality = GetLot(item).Quality;
        var tier = GameplaySettings.Tiers[0];
        foreach (var t in GameplaySettings.Tiers)
            if (quality + .001f > t.Quality)
                tier = t;
        int upgrades = (int) ((quality - tier.Quality) / .0499f);
        return (tier, upgrades);
    }
}
