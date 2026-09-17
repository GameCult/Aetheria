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
    public GameplaySettings GameplaySettings { get; }

    public ItemManager(CultCache itemData, GameplaySettings settings, Action<string> logger)
    {
        ItemData = itemData;
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

    // Returns stat when not equipped
    public float Evaluate(PerformanceStat stat, EquippableItem item)
    {
        var data = GetData(item);
        var quality = pow(item.QualityForRole(stat.FromRole), stat.QualityExponent);
        var durabilityExponent = lerp(
            GameplaySettings.DurabilityQualityMin,
            GameplaySettings.DurabilityQualityMax,
            pow(item.Quality, GameplaySettings.DurabilityQualityExponent));
        var durability = pow(item.Durability / data.Durability, durabilityExponent * stat.DurabilityExponentMultiplier);
        var result = lerp(stat.Min, stat.Max, quality * durability);
        if (float.IsNaN(result))
            throw new InvalidOperationException($"Performance Stat on {data.Name} evaluating as NaN: input data is invalid! Durability: {item.Durability} / {data.Durability}");
        return result;

    }

    public int GetPrice(CraftedItemInstance item)
    {
        var data = GetData(item);
        return (int) (GameplaySettings.QualityPriceModifier.Evaluate(item.Quality) * data.Price);
    }

    public CraftedItemInstance CreateInstance(CraftedItemData item, float quality)
    {
        if (item is EquippableItemData equippableItemData)
        {
            return new EquippableItem
            {
                Data = ItemData.RefOf<ItemData>(item), Quality = quality, Durability = equippableItemData.Durability
            };
        }

        var newCommodity = new CompoundCommodity
        {
            Data = ItemData.RefOf<ItemData>(item),
            Quality = quality
        };
        return newCommodity;
    }

    public CraftedItemInstance CreateInstance(CraftedItemData item)
    {
        if (item == null)
        {
            throw new NullReferenceException("Attempted to create crafted item instance using missing or incorrect item data!");
        }

        var quality = Random.NextFloat();
        var tier = GameplaySettings.Tiers[0];
        foreach (var t in GameplaySettings.Tiers)
        {
            if (t.Rarity > quality)
                tier = t;
        }

        return CreateInstance(item, tier.Quality);
    }

    // Builds one of a manufacturer's branded products: the unit's own workmanship rolled as before, and each of
    // the design's roles filled with a part whose quality is drawn from that manufacturer's distribution for it.
    public CraftedItemInstance CreateInstance(FactionProductData product)
    {
        var design = ItemData.Get(product.Design);
        if (design == null)
        {
            _logger($"Product {product.Name} names a design that does not exist!");
            return null;
        }

        var instance = CreateInstance(design);
        instance.Product = ItemData.RefOf(product);
        if (design.Roles == null) return instance;
        foreach (var role in design.Roles)
        {
            var build = product.Roles?.FirstOrDefault(b => b.Role == role.Name) ?? new ProductRole();
            instance.Ingredients.Add(new RoleFill
            {
                Role = role.Name,
                Quality = clamp(Random.NextGaussian(build.Mean, build.StandardDeviation), .01f, 1)
            });
        }

        return instance;
    }

    public (RarityTier tier, int upgrades) GetTier(CraftedItemInstance item)
    {
        var tier = GameplaySettings.Tiers[0];
        foreach (var t in GameplaySettings.Tiers)
            if (item.Quality + .001f > t.Quality)
                tier = t;
        int upgrades = (int) ((item.Quality - tier.Quality) / .0499f);
        return (tier, upgrades);
    }
}
