/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
    // public Dictionary<Guid, List<IController>> CorporationControllers = new Dictionary<Guid, List<IController>>();
    // public Dictionary<Guid, ZoneDefinition> GalaxyZones;
    
    private Action<string> _logger;

    private double _time;
    private float _deltaTime;
    private Dictionary<Guid, Zone> _zones = new Dictionary<Guid, Zone>();

    // private Guid _forceLoadZone;
    
    private readonly IRuntimeItemCatalogReader _runtimeItems;
    private static readonly IReadOnlyDictionary<string, Type> BehaviorTypesByKind = new Dictionary<string, Type>(StringComparer.Ordinal)
    {
        { "GuidedWeapon", typeof(GuidedWeaponConfig) },
        { "Launcher", typeof(LauncherConfig) },
        { "Reactor", typeof(ReactorConfig) },
        { "Radiator", typeof(RadiatorConfig) },
        { "StatModifier", typeof(StatModifierConfig) },
        { "Sensor", typeof(SensorConfig) },
        { "Reflector", typeof(ReflectorConfig) },
        { "Shield", typeof(ShieldConfig) },
        { "Thruster", typeof(ThrusterConfig) },
        { "Wear", typeof(WearConfig) },
        { "VelocityConversion", typeof(VelocityConversionConfig) },
        { "VelocityLimit", typeof(VelocityLimitConfig) },
        { "AetherDrive", typeof(AetherDriveConfig) },
        { "Cooldown", typeof(CooldownConfig) },
        { "Heat", typeof(HeatConfig) },
        { "ItemUsage", typeof(ItemUsageConfig) },
        { "Switch", typeof(SwitchConfig) },
        { "Trigger", typeof(TriggerConfig) },
        { "Visibility", typeof(VisibilityConfig) },
        { "Thermotoggle", typeof(ThermotoggleConfig) },
        { "EnergyDraw", typeof(EnergyDrawConfig) },
        { "MiningTool", typeof(MiningToolConfig) },
        { "ResourceScanner", typeof(ResourceScannerConfig) },
        { "Capacitor", typeof(CapacitorConfig) },
        { "Cockpit", typeof(CockpitConfig) },
        { "HeatStorage", typeof(HeatStorageConfig) },
        { "TurretController", typeof(TurretControllerConfig) },
        { "InstantWeapon", typeof(InstantWeaponConfig) },
        { "ConstantWeapon", typeof(ConstantWeaponConfig) },
        { "ChargedWeapon", typeof(ChargedWeaponConfig) },
        { "AutoWeapon", typeof(AutoWeaponConfig) }
    };

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

    public ItemManager(IRuntimeItemCatalogReader runtimeItems, GameplaySettings settings, Action<string> logger)
    {
        _runtimeItems = runtimeItems;
        GameplaySettings = settings;
        _logger = logger;
    }

    public AetheriaRuntimeCatalogItem GetRuntimeItem(ItemInstance item)
    {
        return GetRuntimeItem(item?.ItemId ?? Guid.Empty);
    }

    public AetheriaRuntimeCatalogItem GetRuntimeItem(Guid itemId)
    {
        return itemId == Guid.Empty ? null : _runtimeItems.GetRuntimeItem(itemId);
    }

    public Behavior[] CreateRuntimeBehaviors(EquippedItem item)
    {
        return CreateRuntimeBehaviorConfigs(item?.EquippableItem)
            .Select(config => config.CreateInstance(item))
            .ToArray();
    }

    public Behavior[] CreateRuntimeBehaviors(ConsumableItemEffect effect)
    {
        return CreateRuntimeBehaviorConfigs(effect?.Item)
            .Select(config => config.CreateInstance(effect))
            .ToArray();
    }

    private IReadOnlyList<RuntimeBehaviorConfig> CreateRuntimeBehaviorConfigs(ItemInstance item)
    {
        var typedItem = GetRuntimeItem(item);
        return typedItem == null
            ? Array.Empty<RuntimeBehaviorConfig>()
            : BuildBehaviorConfigs(typedItem);
    }

    private static RuntimeBehaviorConfig[] BuildBehaviorConfigs(AetheriaRuntimeCatalogItem item)
    {
        return item.BehaviorPayloads
            .Select(BuildBehaviorConfig)
            .Where(behavior => behavior != null)
            .ToArray();
    }

    private static RuntimeBehaviorConfig BuildBehaviorConfig(AetheriaRuntimeBehaviorPayload payload)
    {
        if (!BehaviorTypesByKind.TryGetValue(payload.Kind, out var behaviorType))
        {
            return null;
        }

        var behavior = Activator.CreateInstance(behaviorType) as RuntimeBehaviorConfig;
        if (behavior == null)
        {
            return null;
        }

        behavior.Kind = payload.Kind;
        behavior.Group = payload.Group;

        foreach (var field in GetKeyedFields(behaviorType))
        {
            var payloadField = payload.Fields.FirstOrDefault(candidate => candidate.Key == field.key);
            if (payloadField == null)
            {
                continue;
            }

            field.field.SetValue(behavior, ConvertValue(payloadField.Value, field.field.FieldType));
        }

        return behavior;
    }

    private static IEnumerable<(int key, FieldInfo field)> GetKeyedFields(Type type)
    {
        return type
            .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Select(field => (attribute: field.GetCustomAttribute<LegacyPayloadKeyAttribute>(), field))
            .Where(entry => entry.attribute != null)
            .Select(entry => (entry.attribute.Key, entry.field));
    }

    private static object ConvertValue(AetheriaRuntimeBehaviorValue value, Type targetType)
    {
        if (targetType == typeof(string)) return value.StringValue ?? "";
        if (targetType == typeof(bool)) return value.BoolValue;
        if (targetType == typeof(float)) return (float)value.NumberValue;
        if (targetType == typeof(double)) return value.NumberValue;
        if (targetType == typeof(int)) return checked((int)value.NumberValue);
        if (targetType == typeof(uint)) return checked((uint)value.NumberValue);
        if (targetType == typeof(Guid)) return ParseGuid(value.LegacyIdValue);
        if (targetType == typeof(float2)) return ConvertFloat2(value);
        if (targetType == typeof(float3)) return ConvertFloat3(value);
        if (targetType == typeof(float4)) return ConvertFloat4(value);
        if (targetType == typeof(int2)) return ConvertInt2(value);
        if (targetType.IsEnum) return ParseEnumValue(value, targetType);

        if (targetType.IsArray)
        {
            return ConvertArray(value, targetType.GetElementType());
        }

        if (typeof(IList).IsAssignableFrom(targetType) && targetType.IsGenericType)
        {
            return ConvertList(value, targetType);
        }

        if (GetKeyedFields(targetType).Any())
        {
            return ConvertKeyedObject(value, targetType);
        }

        return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;
    }

    private static object ConvertArray(AetheriaRuntimeBehaviorValue value, Type elementType)
    {
        var array = Array.CreateInstance(elementType, value.Children.Count);
        for (var index = 0; index < value.Children.Count; index++)
        {
            array.SetValue(ConvertValue(value.Children[index], elementType), index);
        }

        return array;
    }

    private static object ConvertList(AetheriaRuntimeBehaviorValue value, Type targetType)
    {
        var elementType = targetType.GetGenericArguments()[0];
        var list = Activator.CreateInstance(targetType) as IList;
        foreach (var child in value.Children)
        {
            list.Add(ConvertValue(child, elementType));
        }

        return list;
    }

    private static object ConvertKeyedObject(AetheriaRuntimeBehaviorValue value, Type targetType)
    {
        var instance = Activator.CreateInstance(targetType);
        var children = value.Children;
        foreach (var field in GetKeyedFields(targetType))
        {
            if (field.key >= 0 && field.key < children.Count)
            {
                field.field.SetValue(instance, ConvertValue(children[field.key], field.field.FieldType));
            }
        }

        return instance;
    }

    private static object ParseEnumValue(AetheriaRuntimeBehaviorValue value, Type targetType)
    {
        if (!string.IsNullOrWhiteSpace(value.StringValue))
        {
            return Enum.Parse(targetType, value.StringValue, true);
        }

        return Enum.ToObject(targetType, checked((int)value.NumberValue));
    }

    private static Guid ParseGuid(string value)
    {
        return Guid.TryParse(value, out var result) ? result : Guid.Empty;
    }

    private static float2 ConvertFloat2(AetheriaRuntimeBehaviorValue value)
    {
        return new float2(
            value.Children.Count > 0 ? (float)value.Children[0].NumberValue : 0,
            value.Children.Count > 1 ? (float)value.Children[1].NumberValue : 0);
    }

    private static float3 ConvertFloat3(AetheriaRuntimeBehaviorValue value)
    {
        return new float3(
            value.Children.Count > 0 ? (float)value.Children[0].NumberValue : 0,
            value.Children.Count > 1 ? (float)value.Children[1].NumberValue : 0,
            value.Children.Count > 2 ? (float)value.Children[2].NumberValue : 0);
    }

    private static float4 ConvertFloat4(AetheriaRuntimeBehaviorValue value)
    {
        return new float4(
            value.Children.Count > 0 ? (float)value.Children[0].NumberValue : 0,
            value.Children.Count > 1 ? (float)value.Children[1].NumberValue : 0,
            value.Children.Count > 2 ? (float)value.Children[2].NumberValue : 0,
            value.Children.Count > 3 ? (float)value.Children[3].NumberValue : 0);
    }

    private static int2 ConvertInt2(AetheriaRuntimeBehaviorValue value)
    {
        return new int2(
            value.Children.Count > 0 ? checked((int)value.Children[0].NumberValue) : 0,
            value.Children.Count > 1 ? checked((int)value.Children[1].NumberValue) : 0);
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

    public RuntimeItemDefinitionReference CreateReference(Guid itemId)
    {
        return new RuntimeItemDefinitionReference(itemId);
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
            throw new InvalidOperationException($"Performance Stat on {typedItem?.Name ?? item.ItemId.ToString()} evaluating as NaN: input data is invalid! Durability: {item.Durability} / {maxDurability}");
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
                Reference = CreateReference(itemId),
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
            _logger($"Attempted to instantiate missing item id {item?.ItemId}");
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

        if (string.Equals(item.Category, AetheriaRuntimeItemCategories.Consumable, StringComparison.Ordinal))
        {
            return new ConsumableItem
            {
                Reference = CreateReference(itemId),
                Quality = quality
            };
        }

        return new CompoundCommodity
        {
            Reference = CreateReference(itemId),
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
            Reference = CreateReference(itemId),
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
