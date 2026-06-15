/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
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
    // public Dictionary<Guid, List<IController>> CorporationControllers = new Dictionary<Guid, List<IController>>();
    // public Dictionary<Guid, ZoneDefinition> GalaxyZones;
    
    private Action<string> _logger;

    private double _time;
    private float _deltaTime;
    private Dictionary<Guid, Zone> _zones = new Dictionary<Guid, Zone>();

    // private Guid _forceLoadZone;
    
    private readonly IRuntimeItemCatalogReader _runtimeItems;

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
        return item == null
            ? null
            : !string.IsNullOrWhiteSpace(item.ItemKey)
                ? _runtimeItems.GetRuntimeItem(item.ItemKey)
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
        var directBehavior = CreateDirectRuntimeBehavior(payload, item);
        return directBehavior ?? BuildBehaviorConfig(payload)?.CreateInstance(item);
    }

    private static Behavior CreateRuntimeBehavior(AetheriaRuntimeBehaviorPayload payload, ConsumableItemEffect effect)
    {
        var directBehavior = CreateDirectRuntimeBehavior(payload, effect);
        return directBehavior ?? BuildBehaviorConfig(payload)?.CreateInstance(effect);
    }

    private static Behavior CreateDirectRuntimeBehavior(AetheriaRuntimeBehaviorPayload payload, EquippedItem item)
    {
        var definition = new RuntimeBehaviorDefinition(payload);
        switch (payload.Kind)
        {
            case "Cockpit": return new Cockpit(definition, item);
            case "Cooldown": return new Cooldown(definition, item);
            case "EnergyDraw": return new EnergyDraw(definition, item);
            case "Heat": return new Heat(definition, item);
            case "HeatStorage": return new HeatStorage(definition, item);
            case "ItemUsage": return new ItemUsage(definition, item);
            case "Reflector": return new Reflector(definition, item);
            case "Switch": return new Switch(definition, item);
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
            case "Cooldown": return new Cooldown(definition, effect);
            case "EnergyDraw": return new EnergyDraw(definition, effect);
            case "Heat": return new Heat(definition, effect);
            case "HeatStorage": return new HeatStorage(definition, effect);
            case "ItemUsage": return new ItemUsage(definition, effect);
            case "Reflector": return new Reflector(definition, effect);
            case "Switch": return new Switch(definition, effect);
            case "Trigger": return new Trigger(definition, effect);
            case "TurretController": return new TurretController(definition, effect);
            case "VelocityConversion": return new VelocityConversion(definition, effect);
            case "VelocityLimit": return new VelocityLimit(definition, effect);
            case "Visibility": return new Visibility(definition, effect);
            case "Wear": return new Wear(definition, effect);
            default: return null;
        }
    }

    private static RuntimeBehaviorConfig BuildBehaviorConfig(AetheriaRuntimeBehaviorPayload payload)
    {
        switch (payload.Kind)
        {
            case "AetherDrive": return Configure(new AetherDriveConfig(), payload, ApplyAetherDriveConfig);
            case "AutoWeapon": return Configure(new AutoWeaponConfig(), payload, ApplyInstantWeaponConfig);
            case "Capacitor": return Configure(new CapacitorConfig(), payload, ApplyCapacitorConfig);
            case "ChargedWeapon": return Configure(new ChargedWeaponConfig(), payload, ApplyChargedWeaponConfig);
            case "ConstantWeapon": return Configure(new ConstantWeaponConfig(), payload, ApplyConstantWeaponConfig);
            case "GuidedWeapon": return Configure(new GuidedWeaponConfig(), payload, ApplyGuidedWeaponConfig);
            case "InstantWeapon": return Configure(new InstantWeaponConfig(), payload, ApplyInstantWeaponConfig);
            case "Launcher": return Configure(new LauncherConfig(), payload, ApplyLauncherConfig);
            case "MiningTool": return Configure(new MiningToolConfig(), payload, ApplyMiningToolConfig);
            case "Radiator": return Configure(new RadiatorConfig(), payload, ApplyRadiatorConfig);
            case "Reactor": return Configure(new ReactorConfig(), payload, ApplyReactorConfig);
            case "ResourceScanner": return Configure(new ResourceScannerConfig(), payload, ApplyResourceScannerConfig);
            case "Sensor": return Configure(new SensorConfig(), payload, ApplySensorConfig);
            case "Shield": return Configure(new ShieldConfig(), payload, ApplyShieldConfig);
            case "StatModifier": return Configure(new StatModifierConfig(), payload, ApplyStatModifierConfig);
            case "Thermotoggle": return Configure(new ThermotoggleConfig(), payload, ApplyThermotoggleConfig);
            case "Thruster": return Configure(new ThrusterConfig(), payload, ApplyThrusterConfig);
            default: return null;
        }
    }

    private static T Configure<T>(T config, AetheriaRuntimeBehaviorPayload payload, Action<T, BehaviorPayloadReader> apply)
        where T : RuntimeBehaviorConfig
    {
        var reader = new BehaviorPayloadReader(payload);
        config.Kind = payload.Kind;
        config.Group = payload.Group;
        apply?.Invoke(config, reader);
        return config;
    }

    private static void ApplyAetherDriveConfig(AetherDriveConfig config, BehaviorPayloadReader reader)
    {
        config.RotorDiameter = reader.Float3(1, config.RotorDiameter);
        config.RotorMass = reader.Float3(2, config.RotorMass);
        config.MaximumRpm = reader.PerformanceStat(3, config.MaximumRpm);
        config.CouplingLambda = reader.Float3(4, config.CouplingLambda);
        config.LambdaMultiplier = reader.PerformanceStat(5, config.LambdaMultiplier);
        config.CouplingEfficiency = reader.PerformanceStat(6, config.CouplingEfficiency);
        config.Torque = reader.PerformanceStat(7, config.Torque);
        config.TorqueProfile = reader.BezierCurve(8, config.TorqueProfile);
        config.EnergyDraw = reader.PerformanceStat(9, config.EnergyDraw);
        config.PassiveCoupling = reader.PerformanceStat(10, config.PassiveCoupling);
        config.RpmAudioParameter = reader.UInt(11, config.RpmAudioParameter);
        config.TorqueRatioAudioParameter = reader.UInt(12, config.TorqueRatioAudioParameter);
        config.Particles = reader.String(13, config.Particles);
    }

    private static void ApplyCapacitorConfig(CapacitorConfig config, BehaviorPayloadReader reader)
    {
        config.Capacity = reader.PerformanceStat(1, config.Capacity);
        config.Efficiency = reader.PerformanceStat(2, config.Efficiency);
    }

    private static void ApplyMiningToolConfig(MiningToolConfig config, BehaviorPayloadReader reader)
    {
        config.DamagePerSecond = reader.PerformanceStat(1, config.DamagePerSecond);
        config.Efficiency = reader.PerformanceStat(2, config.Efficiency);
        config.Penetration = reader.PerformanceStat(3, config.Penetration);
        config.Range = reader.PerformanceStat(4, config.Range);
    }

    private static void ApplyRadiatorConfig(RadiatorConfig config, BehaviorPayloadReader reader)
    {
        config.Emissivity = reader.PerformanceStat(1, config.Emissivity);
        config.PumpedHeat = reader.PerformanceStat(2, config.PumpedHeat);
        config.TemperatureFloor = reader.Float(3, config.TemperatureFloor);
        config.WasteHeat = reader.PerformanceStat(4, config.WasteHeat);
        config.EnergyUsage = reader.PerformanceStat(5, config.EnergyUsage);
        config.ThermalMass = reader.PerformanceStat(6, config.ThermalMass);
    }

    private static void ApplyReactorConfig(ReactorConfig config, BehaviorPayloadReader reader)
    {
        config.Charge = reader.PerformanceStat(1, config.Charge);
        config.Efficiency = reader.PerformanceStat(2, config.Efficiency);
        config.OverloadEfficiency = reader.PerformanceStat(3, config.OverloadEfficiency);
        config.ThrottlingFactor = reader.PerformanceStat(4, config.ThrottlingFactor);
    }

    private static void ApplyResourceScannerConfig(ResourceScannerConfig config, BehaviorPayloadReader reader)
    {
        config.Range = reader.PerformanceStat(1, config.Range);
        config.MinimumDensity = reader.PerformanceStat(2, config.MinimumDensity);
        config.ScanDuration = reader.PerformanceStat(3, config.ScanDuration);
    }

    private static void ApplySensorConfig(SensorConfig config, BehaviorPayloadReader reader)
    {
        config.Sensitivity = reader.PerformanceStat(3, config.Sensitivity);
        config.SensitivityCurve = reader.BezierCurve(4, config.SensitivityCurve);
        config.PingBoost = reader.PerformanceStat(5, config.PingBoost);
        config.PingEnergy = reader.PerformanceStat(6, config.PingEnergy);
        config.PingVisibility = reader.PerformanceStat(7, config.PingVisibility);
        config.PingRange = reader.PerformanceStat(8, config.PingRange);
        config.PingCooldown = reader.PerformanceStat(9, config.PingCooldown);
        config.PingDuration = reader.Float(10, config.PingDuration);
        config.PingRadiusExponent = reader.Float(11, config.PingRadiusExponent);
    }

    private static void ApplyShieldConfig(ShieldConfig config, BehaviorPayloadReader reader)
    {
        config.Efficiency = reader.PerformanceStat(1, config.Efficiency);
        config.EnergyUsage = reader.PerformanceStat(2, config.EnergyUsage);
    }

    private static void ApplyStatModifierConfig(StatModifierConfig config, BehaviorPayloadReader reader)
    {
        config.Stat = reader.StatReference(1, config.Stat);
        config.Modifier = reader.PerformanceStat(2, config.Modifier);
        config.Type = reader.Enum(3, config.Type);
        config.RequireBehavior = reader.String(4, config.RequireBehavior);
    }

    private static void ApplyThermotoggleConfig(ThermotoggleConfig config, BehaviorPayloadReader reader)
    {
        config.TargetTemperature = reader.Float(1, config.TargetTemperature);
        config.HighPass = reader.Bool(2, config.HighPass);
        config.Adjustable = reader.Bool(3, config.Adjustable);
    }

    private static void ApplyThrusterConfig(ThrusterConfig config, BehaviorPayloadReader reader)
    {
        config.Thrust = reader.PerformanceStat(1, config.Thrust);
        config.Visibility = reader.PerformanceStat(2, config.Visibility);
        config.Heat = reader.PerformanceStat(3, config.Heat);
        config.EnergyUsage = reader.PerformanceStat(4, config.EnergyUsage);
        config.ParticlesPrefab = reader.String(5, config.ParticlesPrefab);
    }

    private static void ApplyWeaponConfig(WeaponConfig config, BehaviorPayloadReader reader)
    {
        config.DamageType = reader.Enum(1, config.DamageType);
        config.Damage = reader.PerformanceStat(2, config.Damage);
        config.Penetration = reader.PerformanceStat(3, config.Penetration);
        config.DamageSpread = reader.PerformanceStat(4, config.DamageSpread);
        config.MinRange = reader.PerformanceStat(5, config.MinRange);
        config.Range = reader.PerformanceStat(6, config.Range);
        config.DamageCurve = reader.BezierCurve(7, config.DamageCurve);
        config.EffectPrefab = reader.String(8, config.EffectPrefab);
        config.Energy = reader.PerformanceStat(9, config.Energy);
        config.Heat = reader.PerformanceStat(10, config.Heat);
        config.Visibility = reader.PerformanceStat(11, config.Visibility);
        config.AmmoItemKey = reader.ItemKey(12, config.AmmoItemKey);
        config.MagazineSize = reader.Int(13, config.MagazineSize);
        config.ReloadTime = reader.Float(14, config.ReloadTime);
        config.Spread = reader.PerformanceStat(15, config.Spread);
        config.Velocity = reader.PerformanceStat(16, config.Velocity);
    }

    private static void ApplyInstantWeaponConfig(InstantWeaponConfig config, BehaviorPayloadReader reader)
    {
        ApplyWeaponConfig(config, reader);
        config.Count = reader.PerformanceStat(17, config.Count);
        config.BurstTime = reader.PerformanceStat(18, config.BurstTime);
        config.Cooldown = reader.PerformanceStat(19, config.Cooldown);
        config.SingleAmmoBurst = reader.Bool(20, config.SingleAmmoBurst);
    }

    private static void ApplyConstantWeaponConfig(ConstantWeaponConfig config, BehaviorPayloadReader reader)
    {
        ApplyWeaponConfig(config, reader);
        config.AmmoInterval = reader.Float(17, config.AmmoInterval);
    }

    private static void ApplyChargedWeaponConfig(ChargedWeaponConfig config, BehaviorPayloadReader reader)
    {
        ApplyInstantWeaponConfig(config, reader);
        config.ChargeTime = reader.PerformanceStat(21, config.ChargeTime);
        config.ChargeEnergy = reader.PerformanceStat(22, config.ChargeEnergy);
        config.ChargeHeat = reader.PerformanceStat(23, config.ChargeHeat);
        config.CanFireEarly = reader.Bool(24, config.CanFireEarly);
        config.FailureCharge = reader.Float(25, config.FailureCharge);
        config.FailureDamage = reader.Float(26, config.FailureDamage);
        config.ChargeFiringDamageMultiplier = reader.Float(27, config.ChargeFiringDamageMultiplier);
        config.ChargeFiringSpreadMultiplier = reader.Float(28, config.ChargeFiringSpreadMultiplier);
        config.ChargeFiringBurstCountMultiplier = reader.Float(29, config.ChargeFiringBurstCountMultiplier);
        config.ChargeFiringVisibilityMultiplier = reader.Float(30, config.ChargeFiringVisibilityMultiplier);
        config.ChargeFiringVelocityMultiplier = reader.Float(31, config.ChargeFiringVelocityMultiplier);
        config.ChargeFiringHeatMultiplier = reader.Float(32, config.ChargeFiringHeatMultiplier);
    }

    private static void ApplyLockWeaponConfig(LockWeaponConfig config, BehaviorPayloadReader reader)
    {
        ApplyInstantWeaponConfig(config, reader);
        config.LockSpeed = reader.PerformanceStat(21, config.LockSpeed);
        config.SensorImpact = reader.PerformanceStat(22, config.SensorImpact);
        config.LockAngle = reader.PerformanceStat(23, config.LockAngle);
        config.DirectionImpact = reader.PerformanceStat(24, config.DirectionImpact);
        config.Decay = reader.PerformanceStat(25, config.Decay);
    }

    private static void ApplyLauncherConfig(LauncherConfig config, BehaviorPayloadReader reader)
    {
        ApplyLockWeaponConfig(config, reader);
        config.GuidanceCurve = reader.Float4Array(26, config.GuidanceCurve);
        config.ThrustCurve = reader.Float4Array(27, config.ThrustCurve);
        config.LiftCurve = reader.Float4Array(28, config.LiftCurve);
        config.Thrust = reader.PerformanceStat(29, config.Thrust);
        config.DodgeFrequency = reader.Float(30, config.DodgeFrequency);
        config.MissileVelocity = reader.PerformanceStat(31, config.MissileVelocity);
    }

    private static void ApplyGuidedWeaponConfig(GuidedWeaponConfig config, BehaviorPayloadReader reader)
    {
        ApplyInstantWeaponConfig(config, reader);
        config.GuidanceCurve = reader.Float4Array(21, config.GuidanceCurve);
        config.ThrustCurve = reader.Float4Array(22, config.ThrustCurve);
        config.LiftCurve = reader.Float4Array(23, config.LiftCurve);
        config.Thrust = reader.PerformanceStat(24, config.Thrust);
        config.DodgeFrequency = reader.Float(25, config.DodgeFrequency);
        config.MissileVelocity = reader.PerformanceStat(26, config.MissileVelocity);
    }

    private sealed class BehaviorPayloadReader
    {
        private readonly IReadOnlyDictionary<int, AetheriaRuntimeBehaviorValue> _fields;

        public BehaviorPayloadReader(AetheriaRuntimeBehaviorPayload payload)
        {
            var fields = new Dictionary<int, AetheriaRuntimeBehaviorValue>();
            foreach (var field in payload.Fields)
            {
                if (!fields.ContainsKey(field.Key))
                {
                    fields.Add(field.Key, field.Value);
                }
            }

            _fields = fields;
        }

        public string String(int key, string fallback = "")
        {
            return _fields.TryGetValue(key, out var value) ? value.StringValue ?? "" : fallback;
        }

        public bool Bool(int key, bool fallback = false)
        {
            return _fields.TryGetValue(key, out var value) ? value.BoolValue : fallback;
        }

        public float Float(int key, float fallback = 0)
        {
            return _fields.TryGetValue(key, out var value) ? (float)value.NumberValue : fallback;
        }

        public int Int(int key, int fallback = 0)
        {
            return _fields.TryGetValue(key, out var value) ? checked((int)value.NumberValue) : fallback;
        }

        public uint UInt(int key, uint fallback = 0)
        {
            return _fields.TryGetValue(key, out var value) ? checked((uint)value.NumberValue) : fallback;
        }

        public string ItemKey(int key, string fallback = "")
        {
            return _fields.TryGetValue(key, out var value) &&
                   !string.IsNullOrWhiteSpace(value.ItemKeyValue)
                ? value.ItemKeyValue
                : fallback;
        }

        public T Enum<T>(int key, T fallback) where T : struct
        {
            if (!_fields.TryGetValue(key, out var value))
            {
                return fallback;
            }

            if (!string.IsNullOrWhiteSpace(value.StringValue) &&
                System.Enum.TryParse(value.StringValue, true, out T parsed))
            {
                return parsed;
            }

            return (T)System.Enum.ToObject(typeof(T), checked((int)value.NumberValue));
        }

        public float3 Float3(int key, float3 fallback = default)
        {
            return _fields.TryGetValue(key, out var value) ? ToFloat3(value) : fallback;
        }

        public float4[] Float4Array(int key, float4[] fallback)
        {
            return _fields.TryGetValue(key, out var value)
                ? value.Children.Select(ToFloat4).ToArray()
                : fallback;
        }

        public PerformanceStat PerformanceStat(int key, PerformanceStat fallback)
        {
            return _fields.TryGetValue(key, out var value) ? ToPerformanceStat(value) : fallback;
        }

        public BezierCurve BezierCurve(int key, BezierCurve fallback)
        {
            if (!_fields.TryGetValue(key, out var value))
            {
                return fallback;
            }

            return new BezierCurve
            {
                Keys = value.Children.Count > 0
                    ? value.Children[0].Children.Select(ToFloat4).ToArray()
                    : Array.Empty<float4>()
            };
        }

        public StatReference StatReference(int key, StatReference fallback)
        {
            if (!_fields.TryGetValue(key, out var value))
            {
                return fallback;
            }

            return new StatReference
            {
                Target = ChildString(value, 1),
                Stat = ChildString(value, 2)
            };
        }

        private static PerformanceStat ToPerformanceStat(AetheriaRuntimeBehaviorValue value)
        {
            return new PerformanceStat
            {
                Min = ChildFloat(value, 0),
                Max = ChildFloat(value, 1),
                HeatExponentMultiplier = ChildFloat(value, 2),
                DurabilityExponentMultiplier = ChildFloat(value, 3),
                QualityExponent = ChildFloat(value, 4)
            };
        }

        private static float3 ToFloat3(AetheriaRuntimeBehaviorValue value)
        {
            return new float3(ChildFloat(value, 0), ChildFloat(value, 1), ChildFloat(value, 2));
        }

        private static float4 ToFloat4(AetheriaRuntimeBehaviorValue value)
        {
            return new float4(ChildFloat(value, 0), ChildFloat(value, 1), ChildFloat(value, 2), ChildFloat(value, 3));
        }

        private static float ChildFloat(AetheriaRuntimeBehaviorValue value, int index)
        {
            return value.Children.Count > index ? (float)value.Children[index].NumberValue : 0;
        }

        private static string ChildString(AetheriaRuntimeBehaviorValue value, int index)
        {
            return value.Children.Count > index ? value.Children[index].StringValue ?? "" : "";
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
