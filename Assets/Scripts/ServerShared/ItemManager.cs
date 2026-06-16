/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.Linq;
using GameCult.Aetheria.State.Unity;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using Random = Unity.Mathematics.Random;
using JM.LinqFaster;
using UniRx;

public class ItemManager
{
    public Random Random = new Random((uint) (DateTime.Now.Ticks%uint.MaxValue));

    private Action<string> _logger;
    private readonly AetheriaRuntimeCatalogSnapshot _runtimeCatalog;

    public GameplaySettings GameplaySettings { get; }

    public ItemManager(AetheriaRuntimeCatalogSnapshot runtimeCatalog, GameplaySettings settings, Action<string> logger)
    {
        _runtimeCatalog = runtimeCatalog ?? throw new ArgumentNullException(nameof(runtimeCatalog));
        GameplaySettings = settings;
        _logger = logger;
    }

    public AetheriaRuntimeCatalogItem GetRuntimeItem(ItemInstance item)
    {
        return item == null
            ? null
            : !string.IsNullOrWhiteSpace(item.ItemKey)
                ? _runtimeCatalog.FindItem(item.ItemKey)
                : null;
    }

    public Behavior[] CreateRuntimeBehaviors(EquippedItem item)
    {
        return GetRuntimeItem(item?.EquippableItem)?.BehaviorPayloads
            .Select(payload => CreateRuntimeBehavior(payload, item))
            .Where(behavior => behavior != null)
            .ToArray() ?? Array.Empty<Behavior>();
    }

    public Behavior[] CreateRuntimeBehaviors(ConsumableItemEffect effect)
    {
        return GetRuntimeItem(effect?.Item)?.BehaviorPayloads
            .Select(payload => CreateRuntimeBehavior(payload, effect))
            .Where(behavior => behavior != null)
            .ToArray() ?? Array.Empty<Behavior>();
    }

    private static Behavior CreateRuntimeBehavior(AetheriaRuntimeBehaviorPayload payload, EquippedItem item)
    {
        return CreateDirectRuntimeBehavior(payload, item);
    }

    private static Behavior CreateRuntimeBehavior(AetheriaRuntimeBehaviorPayload payload, ConsumableItemEffect effect)
    {
        return CreateDirectRuntimeBehavior(payload, effect);
    }

    private static Behavior CreateDirectRuntimeBehavior(AetheriaRuntimeBehaviorPayload payload, EquippedItem item)
    {
        var definition = new RuntimeBehaviorDefinition(payload);
        switch (payload.Kind)
        {
            case "Cockpit": return new Cockpit(definition, item);
            case "AetherDrive": return new AetherDrive(definition, item);
            case "AutoWeapon": return new AutoWeapon(definition, item);
            case "Capacitor": return new Capacitor(definition, item);
            case "ChargedWeapon": return new ChargedWeapon(definition, item);
            case "Cooldown": return new Cooldown(definition, item);
            case "ConstantWeapon": return new ConstantWeapon(definition, item);
            case "EnergyDraw": return new EnergyDraw(definition, item);
            case "GuidedWeapon": return new InstantWeapon(definition, item);
            case "Heat": return new Heat(definition, item);
            case "HeatStorage": return new HeatStorage(definition, item);
            case "InstantWeapon": return new InstantWeapon(definition, item);
            case "ItemUsage": return new ItemUsage(definition, item);
            case "Launcher": return new LockWeapon(definition, item);
            case "LockWeapon": return new LockWeapon(definition, item);
            case "MiningTool": return new MiningTool(definition, item);
            case "Radiator": return new Radiator(definition, item);
            case "Reactor": return new Reactor(definition, item);
            case "Reflector": return new Reflector(definition, item);
            case "ResourceScanner": return new ResourceScanner(definition, item);
            case "Sensor": return new Sensor(definition, item);
            case "Shield": return new Shield(definition, item);
            case "StatModifier": return new StatModifier(definition, item);
            case "Switch": return new Switch(definition, item);
            case "Thermotoggle": return new Thermotoggle(definition, item);
            case "Thruster": return new Thruster(definition, item);
            case "Trigger": return new Trigger(definition, item);
            case "TurretController": return new TurretController(definition, item);
            case "VelocityConversion": return new VelocityConversion(definition, item);
            case "VelocityLimit": return new VelocityLimit(definition, item);
            case "Visibility": return new Visibility(definition, item);
            case "Wear": return new Wear(definition, item);
            default: return null;
        }
    }

    private static Behavior CreateDirectRuntimeBehavior(AetheriaRuntimeBehaviorPayload payload, ConsumableItemEffect effect)
    {
        var definition = new RuntimeBehaviorDefinition(payload);
        switch (payload.Kind)
        {
            case "Cockpit": return new Cockpit(definition, effect);
            case "AetherDrive": return new AetherDrive(definition, effect);
            case "AutoWeapon": return new InstantWeapon(definition, effect);
            case "Capacitor": return new Capacitor(definition, effect);
            case "ChargedWeapon": return new ChargedWeapon(definition, effect);
            case "Cooldown": return new Cooldown(definition, effect);
            case "ConstantWeapon": return new ConstantWeapon(definition, effect);
            case "EnergyDraw": return new EnergyDraw(definition, effect);
            case "GuidedWeapon": return new InstantWeapon(definition, effect);
            case "Heat": return new Heat(definition, effect);
            case "HeatStorage": return new HeatStorage(definition, effect);
            case "InstantWeapon": return new InstantWeapon(definition, effect);
            case "ItemUsage": return new ItemUsage(definition, effect);
            case "Launcher": return new LockWeapon(definition, effect);
            case "LockWeapon": return new LockWeapon(definition, effect);
            case "MiningTool": return new MiningTool(definition, effect);
            case "Radiator": return new Radiator(definition, effect);
            case "Reactor": return new Reactor(definition, effect);
            case "Reflector": return new Reflector(definition, effect);
            case "ResourceScanner": return new ResourceScanner(definition, effect);
            case "Sensor": return new Sensor(definition, effect);
            case "Shield": return new Shield(definition, effect);
            case "StatModifier": return new StatModifier(definition, effect);
            case "Switch": return new Switch(definition, effect);
            case "Thermotoggle": return new Thermotoggle(definition, effect);
            case "Thruster": return new Thruster(definition, effect);
            case "Trigger": return new Trigger(definition, effect);
            case "TurretController": return new TurretController(definition, effect);
            case "VelocityConversion": return new VelocityConversion(definition, effect);
            case "VelocityLimit": return new VelocityLimit(definition, effect);
            case "Visibility": return new Visibility(definition, effect);
            case "Wear": return new Wear(definition, effect);
            default: return null;
        }
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

    public AetheriaRuntimeItemReference CreateReference(AetheriaRuntimeCatalogItem item)
    {
        return new AetheriaRuntimeItemReference(ToItemKey(item));
    }

    private static string ToItemKey(AetheriaRuntimeCatalogItem item)
    {
        return item?.ItemKey ?? "";
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
            throw new InvalidOperationException($"Performance Stat on {typedItem?.Name ?? item.ItemKey} evaluating as NaN: input data is invalid! Durability: {item.Durability} / {maxDurability}");
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
        if (!string.IsNullOrWhiteSpace(item?.ItemKey))
        {
            return new SimpleCommodity
            {
                Reference = CreateReference(item),
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
            _logger($"Attempted to instantiate missing item key {item?.ItemKey}");
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

        if (string.IsNullOrWhiteSpace(item?.ItemKey))
        {
            throw new NullReferenceException("Attempted to create crafted item instance using missing typed item data!");
        }

        if (string.Equals(item.Category, AetheriaRuntimeItemCategories.Consumable, StringComparison.Ordinal))
        {
            return new ConsumableItem
            {
                Reference = CreateReference(item),
                Quality = quality
            };
        }

        return new CompoundCommodity
        {
            Reference = CreateReference(item),
            Quality = quality
        };
    }

    public CraftedItemInstance CreateCraftedInstance(AetheriaRuntimeCatalogItem item)
    {
        return CreateCraftedInstance(item, SelectCraftedQuality());
    }

    public EquippableItem CreateEquippableInstance(AetheriaRuntimeCatalogItem item, float quality)
    {
        if (string.IsNullOrWhiteSpace(item?.ItemKey))
        {
            throw new NullReferenceException("Attempted to create equippable item instance using missing typed item data!");
        }

        return new EquippableItem
        {
            Reference = CreateReference(item),
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

    private static bool IsEquippable(AetheriaRuntimeCatalogItem item)
    {
        return !string.IsNullOrWhiteSpace(item?.HardpointType) ||
               string.Equals(item?.Category, AetheriaRuntimeItemCategories.Hull, StringComparison.Ordinal);
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
