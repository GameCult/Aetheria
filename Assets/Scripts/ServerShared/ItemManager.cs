/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
    // public Dictionary<string, GalaxyMapLayerData> MapLayers = new Dictionary<string, GalaxyMapLayerData>();
    // public SimpleCommodityData[] Resources;
    // public Dictionary<Guid, List<IController>> CorporationControllers = new Dictionary<Guid, List<IController>>();
    // public Dictionary<Guid, ZoneDefinition> GalaxyZones;
    
    private Action<string> _logger;

    private double _time;
    private float _deltaTime;
    private Dictionary<Guid, Zone> _zones = new Dictionary<Guid, Zone>();

    // private Guid _forceLoadZone;
    
    // public GlobalData GlobalData => _globalData ?? (_globalData = ItemData.GetAll<GlobalData>().FirstOrDefault());
    public CultCache ItemData { get; }
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

    // public void Update()
    // {
    //     foreach(var zone in _zones.Values)
    //         zone.Update((float) Time, _deltaTime);
    //     
    //     foreach (var corporation in Cache.GetAll<Corporation>())
    //     {
    //         foreach (var tasks in corporation.Tasks
    //             .Select(id => Cache.Get<AgentTask>(id)) // Fetch the tasks from the database cache
    //             .Where(task => !task.Reserved) // Filter out tasks that have already been reserved
    //             .GroupBy(task => task.Type)) // Group tasks by type
    //         {
    //             // Create a list of available controllers for this task type
    //             var availableControllers = CorporationControllers[corporation.ID]
    //                 .Where(controller => controller.Available && controller.TaskType == tasks.Key).ToList();
    //             
    //             // Iterate over the highest priority tasks for which controllers are available
    //             foreach (var task in tasks.OrderByDescending(task => task.Priority).Take(availableControllers.Count))
    //             {
    //                 // Find the nearest controller for this task
    //                 IController nearestController = availableControllers[0];
    //                 List<ZoneDefinition> nearestControllerPath = FindPath(GalaxyZones[availableControllers.First().Zone.Data.ID], GalaxyZones[task.Zone], true);
    //                 foreach (var controller in availableControllers.Skip(1))
    //                 {
    //                     var path = FindPath(GalaxyZones[controller.Zone.Data.ID], GalaxyZones[task.Zone], true);
    //                     if (path.Count < nearestControllerPath.Count)
    //                     {
    //                         nearestControllerPath = path;
    //                         nearestController = controller;
    //                     }
    //                 }
    //                 task.Reserved = true;
    //                 nearestController.AssignTask(task.ID);
    //             }
    //         }
    //     }
    //     
    // }

    // public int ItemTier(CraftedItemData itemData)
    // {
    //     if (Tier.ContainsKey(itemData)) return Tier[itemData];
    //
    //     Tier[itemData] = itemData.Ingredients.Keys.Max(ci => _cache.Get<ItemData>(ci) is CraftedItemData craftableIngredient ? ItemTier(craftableIngredient) : 0);
		  //
    //     return Tier[itemData];
    // }

    public SimpleCommodityData GetData(SimpleCommodity item)
    {
        return item.Data.Value as SimpleCommodityData;
    }

    public CraftedItemData GetData(CraftedItemInstance item)
    {
        return item.Data.Value as CraftedItemData;
    }

    public EquippableItemData GetData(EquippableItem item)
    {
        return item.Data.Value as EquippableItemData;
    }

    public float GetMass(ItemInstance item)
    {
        return item switch
        {
            CraftedItemInstance _ => item.Data.Value.Mass,
            SimpleCommodity commodity => item.Data.Value.Mass * commodity.Quantity,
            _ => 0
        };
    }

    public float GetThermalMass(ItemInstance item)
    {
        var data = item.Data.Value;
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
        var quality = pow(item.Quality, stat.QualityExponent);
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

    public SimpleCommodity CreateInstance(SimpleCommodityData item, int count)
    {
        if (item != null)
        {
            var newItem = new SimpleCommodity
            {
                Data = new DatabaseLink<ItemData>{LinkID = item.ID},
                Quantity = count
            };
            //ItemData.Add(newItem);
            return newItem;
        }
        
        _logger("Attempted to create Simple Commodity instance using missing or incorrect item id");
        return null;
    }

    public ItemInstance Instantiate(ItemInstance item)
    {
        var data = item.Data.Value;
        if(data is CraftedItemData c)
        {
            var i = CreateInstance(c);
            i.Rotation = item.Rotation;
            return i;
        }
        if (item is SimpleCommodity s)
        {
            var i = CreateInstance(data as SimpleCommodityData, s.Quantity);
            i.Rotation = item.Rotation;
            return i;
        }
        return null;
    }

    public CraftedItemInstance CreateInstance(CraftedItemData item, float quality)
    {
        if (item is EquippableItemData equippableItemData)
        {
            return new EquippableItem
            {
                Data = new DatabaseLink<ItemData> {LinkID = item.ID}, Quality = quality, Durability = equippableItemData.Durability
            };
        }

        var newCommodity = new CompoundCommodity
        {
            Data = new DatabaseLink<ItemData>{LinkID = item.ID},
            Quality = quality
        };
        return newCommodity;
    }
    
    public CraftedItemInstance CreateInstance(CraftedItemData item)
    {
        if (item == null)
        {
            throw new NullReferenceException("Attempted to create crafted item instance using missing or incorrect item data!");
            return null;
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