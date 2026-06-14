using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using GameCult.Aetheria.State.Unity;
using Unity.Mathematics;

public interface IRuntimeItemProjectionReader
{
    AetheriaRuntimeCatalogItem GetRuntimeItem(Guid guid);
    ItemData Get(Guid guid);
    T Get<T>(Guid guid) where T : ItemData;
}

public sealed class AetheriaRuntimeItemCatalog : IRuntimeItemProjectionReader
{
    private readonly Dictionary<Guid, AetheriaRuntimeCatalogItem> _typedItems;
    private readonly Dictionary<Guid, ItemData> _items;
    private static readonly IReadOnlyDictionary<int, Type> BehaviorTypesByUnionKey = new Dictionary<int, Type>
    {
        { 0, typeof(GuidedWeaponData) },
        { 1, typeof(LauncherData) },
        { 2, typeof(ReactorData) },
        { 3, typeof(RadiatorData) },
        { 4, typeof(StatModifierData) },
        { 5, typeof(SensorData) },
        { 6, typeof(ReflectorData) },
        { 7, typeof(ShieldData) },
        { 8, typeof(ThrusterData) },
        { 9, typeof(WearData) },
        { 10, typeof(VelocityConversionData) },
        { 11, typeof(VelocityLimitData) },
        { 12, typeof(AetherDriveData) },
        { 15, typeof(CooldownData) },
        { 16, typeof(HeatData) },
        { 18, typeof(ItemUsageData) },
        { 20, typeof(SwitchData) },
        { 21, typeof(TriggerData) },
        { 22, typeof(VisibilityData) },
        { 23, typeof(ThermotoggleData) },
        { 24, typeof(EnergyDrawData) },
        { 26, typeof(MiningToolData) },
        { 28, typeof(ResourceScannerData) },
        { 31, typeof(CapacitorData) },
        { 32, typeof(CockpitData) },
        { 33, typeof(HeatStorageData) },
        { 34, typeof(TurretControllerData) },
        { 35, typeof(InstantWeaponData) },
        { 36, typeof(ConstantWeaponData) },
        { 37, typeof(ChargedWeaponData) },
        { 38, typeof(AutoWeaponData) }
    };

    public AetheriaRuntimeItemCatalog(AetheriaRuntimeCatalogSnapshot catalog)
    {
        if (catalog == null) throw new ArgumentNullException(nameof(catalog));

        _typedItems = catalog.Items
            .Where(item => Guid.TryParse(item.LegacyId, out _))
            .ToDictionary(item => Guid.Parse(item.LegacyId), item => item);

        _items = catalog.Items
            .Select(ProjectItem)
            .Where(item => item != null)
            .ToDictionary(item => item.ID, item => item);
    }

    public AetheriaRuntimeCatalogItem GetRuntimeItem(Guid guid)
    {
        _typedItems.TryGetValue(guid, out var item);
        return item;
    }

    public ItemData Get(Guid guid)
    {
        ItemData item;
        _items.TryGetValue(guid, out item);
        return item;
    }

    public T Get<T>(Guid guid) where T : ItemData
    {
        return Get(guid) as T;
    }

    private static ItemData ProjectItem(AetheriaRuntimeCatalogItem item)
    {
        if (!Guid.TryParse(item.LegacyId, out var id))
        {
            return null;
        }

        var projected = CreateItemData(item);
        if (projected == null)
        {
            return null;
        }

        projected.ID = id;
        projected.Name = item.Name;
        projected.Description = item.Description;
        projected.Manufacturer = ParseGuid(item.ManufacturerLegacyId);
        projected.Mass = (float)item.Mass;
        projected.SpecificHeat = (float)item.SpecificHeat;
        projected.Shape = ProjectShape(item.ShapeWidth, item.ShapeHeight, item.ShapeCells);
        projected.Price = item.Price;

        if (projected is SimpleCommodityData simple)
        {
            simple.MaxStack = item.MaxStack;
        }

        if (projected is EquippableItemData equippable)
        {
            equippable.Durability = (float)item.Durability;
            equippable.Behaviors = item.BehaviorPayloads
                .Select(ProjectBehavior)
                .Where(behavior => behavior != null)
                .ToList();
        }

        if (projected is GearData gear)
        {
            gear.Hardpoint = ParseEnum(item.HardpointType, HardpointType.Tool);
        }

        if (projected is CargoBayData cargoBay)
        {
            cargoBay.InteriorShape = ProjectShape(item.InteriorShapeWidth, item.InteriorShapeHeight, item.InteriorShapeCells);
        }

        if (projected is WeaponItemData weapon)
        {
            weapon.WeaponRange = ParseEnum(item.WeaponRange, WeaponRange.Short);
            weapon.WeaponCaliber = ParseEnum(item.WeaponCaliber, WeaponCaliber.Small);
            weapon.WeaponType = ParseEnum(item.WeaponType, WeaponType.Laser);
            weapon.WeaponFireTypes = ParseFlags(item.WeaponFireTypes, WeaponFireType.None);
            weapon.WeaponModifiers = ParseFlags(item.WeaponModifiers, WeaponModifiers.None);
        }

        if (projected is HullData hull)
        {
            hull.HullType = ParseEnum(item.HullType, HullType.Ship);
            hull.Hardpoints = item.Hardpoints.Select(ProjectHardpoint).ToList();
        }

        return projected;
    }

    private static ItemData CreateItemData(AetheriaRuntimeCatalogItem item)
    {
        switch (item.Category)
        {
            case "SimpleCommodityData":
                return new SimpleCommodityData();
            case "CompoundCommodityData":
                return new CompoundCommodityData();
            case "ConsumableItemData":
                return new ConsumableItemData();
            case "GearData":
                return new GearData();
            case "CargoBayData":
                return new CargoBayData();
            case "DockingBayData":
                return new DockingBayData();
            case "WeaponItemData":
                return new WeaponItemData();
            case "HullData":
                return new HullData();
            default:
                return !string.IsNullOrWhiteSpace(item.HardpointType) ? new GearData() : null;
        }
    }

    private static Shape ProjectShape(int width, int height, IReadOnlyList<AetheriaRuntimeShapeCell> cells)
    {
        var shape = new Shape(Math.Max(width, 1), Math.Max(height, 1));
        foreach (var coordinate in shape.AllCoordinates)
        {
            shape[coordinate] = false;
        }

        foreach (var cell in cells)
        {
            shape[new int2(cell.X, cell.Y)] = true;
        }

        return shape;
    }

    private static HardpointData ProjectHardpoint(AetheriaRuntimeHardpoint hardpoint)
    {
        return new HardpointData
        {
            Type = ParseEnum(hardpoint.Type, HardpointType.Hull),
            Position = new int2(hardpoint.PositionX, hardpoint.PositionY),
            Shape = ProjectShape(hardpoint.ShapeWidth, hardpoint.ShapeHeight, hardpoint.ShapeCells),
            Transform = hardpoint.Transform,
            Rotation = ParseEnum(hardpoint.Rotation, ItemRotation.None),
            Armor = (float)hardpoint.Armor
        };
    }

    private static BehaviorData ProjectBehavior(AetheriaRuntimeBehaviorPayload payload)
    {
        if (!BehaviorTypesByUnionKey.TryGetValue(payload.UnionKey, out var behaviorType))
        {
            return null;
        }

        var behavior = Activator.CreateInstance(behaviorType) as BehaviorData;
        if (behavior == null)
        {
            return null;
        }

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

    private static T ParseEnum<T>(string value, T fallback) where T : struct
    {
        return Enum.TryParse(value, true, out T parsed) ? parsed : fallback;
    }

    private static T ParseFlags<T>(string value, T fallback) where T : struct
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        return value
            .Split('|')
            .Select(part => part.Trim())
            .Where(part => part.Length > 0)
            .Aggregate(fallback, (current, part) =>
                Enum.TryParse(part, true, out T parsed)
                    ? (T)Enum.ToObject(typeof(T), Convert.ToInt32(current) | Convert.ToInt32(parsed))
                    : current);
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
