/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GameCult.Aetheria.State.Unity;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using float2 = Unity.Mathematics.float2;
using Random = Unity.Mathematics.Random;
using JM.LinqFaster;
using UniRx;
using float4 = Unity.Mathematics.float4;

public class ItemManager
{
    public Random Random = new Random((uint) (DateTime.Now.Ticks%uint.MaxValue));
    // public SimpleCommodityData[] Resources;
    // public Dictionary<Guid, List<IController>> CorporationControllers = new Dictionary<Guid, List<IController>>();
    // public Dictionary<Guid, ZoneDefinition> GalaxyZones;
    
    private Action<string> _logger;

    private double _time;
    private float _deltaTime;
    private Dictionary<Guid, Zone> _zones = new Dictionary<Guid, Zone>();

    // private Guid _forceLoadZone;
    
    private readonly IRuntimeItemProjectionReader _itemProjections;
    public GameplaySettings GameplaySettings { get; }

    // public double Time
    // {
    //     get => _time;
    //     set
    //     {
    //         _deltaTime = (float) (value - _time);
    //         _time = value;
    //         //Log($"GameContext delta time: {_deltaTime}");
    //     }
    // }

    // private readonly Dictionary<CraftedItemData, int> Tier = new Dictionary<CraftedItemData, int>();

    public ItemManager(IRuntimeItemProjectionReader itemProjections, GameplaySettings settings, Action<string> logger)
    {
        _itemProjections = itemProjections;
        GameplaySettings = settings;
        _logger = logger;
    }

    public AetheriaRuntimeCatalogItem GetRuntimeItem(ItemInstance item)
    {
        var itemId = item?.Data?.ItemId ?? Guid.Empty;
        return GetRuntimeItem(itemId);
    }

    public AetheriaRuntimeCatalogItem GetRuntimeItem(Guid itemId)
    {
        return itemId == Guid.Empty ? null : _itemProjections.GetRuntimeItem(itemId);
    }

    public IReadOnlyList<BehaviorData> GetRuntimeBehaviorProjections(ItemInstance item)
    {
        var itemId = item?.Data?.ItemId ?? Guid.Empty;
        return itemId == Guid.Empty
            ? Array.Empty<BehaviorData>()
            : _itemProjections.GetBehaviorProjections(itemId);
    }

    public Shape GetRuntimeShape(ItemInstance item)
    {
        var typedItem = GetRuntimeItem(item);
        return ToShape(typedItem?.ShapeWidth ?? 1, typedItem?.ShapeHeight ?? 1, typedItem?.ShapeCells);
    }

    private static Shape ToShape(int width, int height, IReadOnlyList<AetheriaRuntimeShapeCell> cells)
    {
        var shape = new Shape(Math.Max(width, 1), Math.Max(height, 1));
        if (cells == null) return shape;

        foreach (var cell in cells)
            shape[new int2(cell.X, cell.Y)] = true;

        return shape;
    }

    public RuntimeItemReference CreateReference(Guid itemId)
    {
        return new RuntimeItemReference(itemId);
    }

    public void Log(string s)
    {
        _logger(s);
    }

    public float GetMass(ItemInstance item)
    {
        var typedItem = GetRuntimeItem(item);
        if (typedItem == null)
        {
            return 0;
        }

        return item switch
        {
            CraftedItemInstance _ => (float)typedItem.Mass,
            SimpleCommodity commodity => (float)typedItem.Mass * commodity.Quantity,
            _ => 0
        };
    }

    public float GetThermalMass(ItemInstance item)
    {
        var typedItem = GetRuntimeItem(item);
        if (typedItem == null)
        {
            return 0;
        }

        return item switch
        {
            CraftedItemInstance _ => (float)(typedItem.Mass * typedItem.SpecificHeat),
            SimpleCommodity commodity => (float)(typedItem.Mass * typedItem.SpecificHeat * commodity.Quantity),
            _ => 0
        };
    }

    // Returns stat when not equipped
    public float Evaluate(PerformanceStat stat, EquippableItem item)
    {
        var typedItem = GetRuntimeItem(item);
        var maxDurability = (float)(typedItem?.Durability ?? 0);
        if (maxDurability <= 0)
        {
            maxDurability = Math.Max(item.Durability, 1f);
        }

        var quality = pow(item.Quality, stat.QualityExponent);
        var durabilityExponent = lerp(
            GameplaySettings.DurabilityQualityMin,
            GameplaySettings.DurabilityQualityMax,
            pow(item.Quality, GameplaySettings.DurabilityQualityExponent));
        var durability = pow(item.Durability / maxDurability, durabilityExponent * stat.DurabilityExponentMultiplier);
        var result = lerp(stat.Min, stat.Max, quality * durability);
        if (float.IsNaN(result)) 
            throw new InvalidOperationException($"Performance Stat on {typedItem?.Name ?? item.Data?.ItemId.ToString() ?? "unknown item"} evaluating as NaN: input data is invalid! Durability: {item.Durability} / {maxDurability}");
        return result;

    }

    public int GetPrice(CraftedItemInstance item)
    {
        var typedItem = GetRuntimeItem(item);
        return typedItem == null
            ? 0
            : (int) (GameplaySettings.QualityPriceModifier.Evaluate(item.Quality) * typedItem.Price);
    }

    public int GetPrice(ItemInstance item)
    {
        var typedItem = GetRuntimeItem(item);
        if (typedItem == null)
        {
            return 0;
        }

        return item switch
        {
            CraftedItemInstance crafted => (int)(GameplaySettings.QualityPriceModifier.Evaluate(crafted.Quality) * typedItem.Price),
            SimpleCommodity commodity => typedItem.Price * commodity.Quantity,
            _ => typedItem.Price
        };
    }

    public SimpleCommodity CreateSimpleCommodityInstance(AetheriaRuntimeCatalogItem item, int count)
    {
        var itemId = GetLegacyGuid(item);
        if (itemId != Guid.Empty)
        {
            return new SimpleCommodity
            {
                Data = CreateReference(itemId),
                Quantity = count
            };
        }

        _logger("Attempted to create simple commodity instance using missing typed item data");
        return null;
    }

    public ItemInstance Instantiate(ItemInstance item)
    {
        var typedItem = GetRuntimeItem(item);
        if (typedItem == null)
        {
            _logger($"Attempted to instantiate missing item id {item?.Data?.ItemId}");
            return null;
        }

        ItemInstance instance = item switch
        {
            SimpleCommodity simple => CreateSimpleCommodityInstance(typedItem, simple.Quantity),
            CraftedItemInstance => CreateCraftedInstance(typedItem),
            _ => null
        };

        if (instance != null)
            instance.Rotation = item.Rotation;

        return instance;
    }

    public CraftedItemInstance CreateCraftedInstance(AetheriaRuntimeCatalogItem item, float quality)
    {
        if (IsEquippable(item))
            return CreateEquippableInstance(item, quality);

        var itemId = GetLegacyGuid(item);
        if (itemId == Guid.Empty)
        {
            throw new NullReferenceException("Attempted to create crafted item instance using missing typed item data!");
        }

        if (string.Equals(item.Category, nameof(ConsumableItemData), StringComparison.Ordinal))
        {
            return new ConsumableItem
            {
                Data = CreateReference(itemId),
                Quality = quality
            };
        }

        return new CompoundCommodity
        {
            Data = CreateReference(itemId),
            Quality = quality
        };
    }

    public CraftedItemInstance CreateCraftedInstance(AetheriaRuntimeCatalogItem item)
    {
        return CreateCraftedInstance(item, SelectCraftedQuality());
    }

    public EquippableItem CreateEquippableInstance(AetheriaRuntimeCatalogItem item, float quality)
    {
        var itemId = GetLegacyGuid(item);
        if (itemId == Guid.Empty)
        {
            throw new NullReferenceException("Attempted to create equippable item instance using missing typed item data!");
        }

        return new EquippableItem
        {
            Data = CreateReference(itemId),
            Quality = quality,
            Durability = item.Durability > 0 ? (float)item.Durability : 1f
        };
    }

    public EquippableItem CreateEquippableInstance(AetheriaRuntimeCatalogItem item)
    {
        return CreateEquippableInstance(item, SelectCraftedQuality());
    }

    private float SelectCraftedQuality()
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

    private static Guid GetLegacyGuid(AetheriaRuntimeCatalogItem item)
    {
        return item != null && Guid.TryParse(item.LegacyId, out var itemId)
            ? itemId
            : Guid.Empty;
    }

    private static bool IsEquippable(AetheriaRuntimeCatalogItem item)
    {
        return !string.IsNullOrWhiteSpace(item?.HardpointType) ||
               string.Equals(item?.Category, nameof(HullData), StringComparison.Ordinal);
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
