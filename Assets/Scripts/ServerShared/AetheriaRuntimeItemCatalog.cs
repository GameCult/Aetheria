using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using GameCult.Aetheria.State.Unity;
using Unity.Mathematics;

public interface IRuntimeItemCatalogReader
{
    AetheriaRuntimeCatalogItem GetRuntimeItem(Guid guid);
    IReadOnlyList<BehaviorData> GetTemporaryBehaviorConfigs(Guid guid);
}

public sealed class AetheriaRuntimeItemCatalog : IRuntimeItemCatalogReader
{
    private readonly Dictionary<Guid, AetheriaRuntimeCatalogItem> _typedItems;
    private static readonly IReadOnlyDictionary<string, Type> BehaviorTypesByKind = new Dictionary<string, Type>(StringComparer.Ordinal)
    {
        { "GuidedWeapon", typeof(GuidedWeaponData) },
        { "Launcher", typeof(LauncherData) },
        { "Reactor", typeof(ReactorData) },
        { "Radiator", typeof(RadiatorData) },
        { "StatModifier", typeof(StatModifierData) },
        { "Sensor", typeof(SensorData) },
        { "Reflector", typeof(ReflectorData) },
        { "Shield", typeof(ShieldData) },
        { "Thruster", typeof(ThrusterData) },
        { "Wear", typeof(WearData) },
        { "VelocityConversion", typeof(VelocityConversionData) },
        { "VelocityLimit", typeof(VelocityLimitData) },
        { "AetherDrive", typeof(AetherDriveData) },
        { "Cooldown", typeof(CooldownData) },
        { "Heat", typeof(HeatData) },
        { "ItemUsage", typeof(ItemUsageData) },
        { "Switch", typeof(SwitchData) },
        { "Trigger", typeof(TriggerData) },
        { "Visibility", typeof(VisibilityData) },
        { "Thermotoggle", typeof(ThermotoggleData) },
        { "EnergyDraw", typeof(EnergyDrawData) },
        { "MiningTool", typeof(MiningToolData) },
        { "ResourceScanner", typeof(ResourceScannerData) },
        { "Capacitor", typeof(CapacitorData) },
        { "Cockpit", typeof(CockpitData) },
        { "HeatStorage", typeof(HeatStorageData) },
        { "TurretController", typeof(TurretControllerData) },
        { "InstantWeapon", typeof(InstantWeaponData) },
        { "ConstantWeapon", typeof(ConstantWeaponData) },
        { "ChargedWeapon", typeof(ChargedWeaponData) },
        { "AutoWeapon", typeof(AutoWeaponData) }
    };

    public AetheriaRuntimeItemCatalog(AetheriaRuntimeCatalogSnapshot catalog)
    {
        if (catalog == null) throw new ArgumentNullException(nameof(catalog));

        _typedItems = catalog.Items
            .Where(item => Guid.TryParse(item.LegacyId, out _))
            .ToDictionary(item => Guid.Parse(item.LegacyId), item => item);
    }

    public AetheriaRuntimeCatalogItem GetRuntimeItem(Guid guid)
    {
        _typedItems.TryGetValue(guid, out var item);
        return item;
    }

    public IReadOnlyList<BehaviorData> GetTemporaryBehaviorConfigs(Guid guid)
    {
        var item = GetRuntimeItem(guid);
        return item == null
            ? Array.Empty<BehaviorData>()
            : BuildBehaviorConfigs(item);
    }

    private static BehaviorData[] BuildBehaviorConfigs(AetheriaRuntimeCatalogItem item)
    {
        return item.BehaviorPayloads
            .Select(BuildBehaviorConfig)
            .Where(behavior => behavior != null)
            .ToArray();
    }

    private static PerformanceStat ProjectPerformanceStat(AetheriaRuntimePerformanceStat stat)
    {
        return new PerformanceStat
        {
            Min = (float)stat.Min,
            Max = (float)stat.Max,
            HeatExponentMultiplier = (float)stat.HeatExponentMultiplier,
            DurabilityExponentMultiplier = (float)stat.DurabilityExponentMultiplier,
            QualityExponent = (float)stat.QualityExponent
        };
    }

    private static BehaviorData BuildBehaviorConfig(AetheriaRuntimeBehaviorPayload payload)
    {
        if (!BehaviorTypesByKind.TryGetValue(payload.Kind, out var behaviorType))
        {
            return null;
        }

        var behavior = Activator.CreateInstance(behaviorType) as BehaviorData;
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
}
