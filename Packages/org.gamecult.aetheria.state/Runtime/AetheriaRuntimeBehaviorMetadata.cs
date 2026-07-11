using System;
using System.Collections.Generic;
using System.Linq;

namespace GameCult.Aetheria.State.Verse
{
    public enum AetheriaRuntimeBehaviorFieldValueKind
    {
        Number,
        Integer,
        PerformanceStat,
        Temperature
    }

    public sealed class AetheriaRuntimeBehaviorFieldMetadata
    {
        public AetheriaRuntimeBehaviorFieldMetadata(string name, int key, AetheriaRuntimeBehaviorFieldValueKind valueKind)
        {
            Name = name ?? "";
            Key = key;
            ValueKind = valueKind;
        }

        public string Name { get; }
        public int Key { get; }
        public AetheriaRuntimeBehaviorFieldValueKind ValueKind { get; }
    }

    public sealed class AetheriaRuntimeBehaviorMetadata
    {
        public AetheriaRuntimeBehaviorMetadata(
            string kind,
            string parentKind,
            IReadOnlyList<AetheriaRuntimeBehaviorFieldMetadata> displayFields)
        {
            Kind = kind ?? "";
            ParentKind = parentKind ?? "";
            DisplayFields = displayFields ?? Array.Empty<AetheriaRuntimeBehaviorFieldMetadata>();
        }

        public string Kind { get; }
        public string ParentKind { get; }
        public IReadOnlyList<AetheriaRuntimeBehaviorFieldMetadata> DisplayFields { get; }
    }

    public static class AetheriaRuntimeBehaviorMetadataCatalog
    {
        private static readonly AetheriaRuntimeBehaviorFieldMetadata[] WeaponFields =
        {
            Stat("Damage", 2),
            Stat("Penetration", 3),
            Stat("DamageSpread", 4),
            Stat("MinRange", 5),
            Stat("Range", 6),
            Stat("Energy", 9),
            Stat("Heat", 10),
            Stat("Visibility", 11),
            Integer("MagazineSize", 13),
            Number("ReloadTime", 14),
            Stat("Spread", 15),
            Stat("Velocity", 16)
        };

        private static readonly AetheriaRuntimeBehaviorFieldMetadata[] InstantWeaponFields =
            With(WeaponFields, Stat("BurstCount", 17), Stat("BurstTime", 18), Stat("Cooldown", 19));

        private static readonly AetheriaRuntimeBehaviorFieldMetadata[] LockWeaponFields =
            With(InstantWeaponFields, Stat("LockSpeed", 21), Stat("SensorImpact", 22), Stat("LockAngle", 23),
                Stat("DirectionImpact", 24), Stat("Decay", 25));

        private static readonly IReadOnlyDictionary<string, AetheriaRuntimeBehaviorMetadata> ByKind =
            new[]
            {
                Behavior("AetherDrive", "", Stat("MaximumRpm", 3), Stat("CouplingEfficiency", 6), Stat("Torque", 7), Stat("EnergyDraw", 9), Stat("PassiveCoupling", 10)),
                Behavior("Capacitor", "", Stat("Capacity", 1), Stat("Efficiency", 2)),
                Behavior("ChargedWeapon", AetheriaRuntimeBehaviorKinds.InstantWeapon, With(InstantWeaponFields, Stat("ChargeTime", 21), Stat("ChargeEnergy", 22), Stat("ChargeHeat", 23))),
                Behavior("Cockpit", ""),
                Behavior("ConstantWeapon", "Weapon", WeaponFields),
                Behavior(AetheriaRuntimeBehaviorKinds.GuidedWeapon, AetheriaRuntimeBehaviorKinds.InstantWeapon, With(InstantWeaponFields, Stat("MissileVelocity", 26))),
                Behavior(AetheriaRuntimeBehaviorKinds.Launcher, "LockWeapon", With(LockWeaponFields, Stat("MissileVelocity", 31))),
                Behavior("LockWeapon", AetheriaRuntimeBehaviorKinds.InstantWeapon, LockWeaponFields),
                Behavior(AetheriaRuntimeBehaviorKinds.InstantWeapon, "Weapon", InstantWeaponFields),
                Behavior(AetheriaRuntimeBehaviorKinds.AutoWeapon, AetheriaRuntimeBehaviorKinds.InstantWeapon, InstantWeaponFields),
                Behavior("Cooldown", "", Stat("Cooldown", 1)),
                Behavior("EnergyDraw", "", Stat("EnergyDraw", 1)),
                Behavior("Heat", "", Stat("Heat", 1)),
                Behavior("HeatStorage", ""),
                Behavior("ItemUsage", ""),
                Behavior("MiningTool", "", Stat("DamagePerSecond", 1), Stat("Efficiency", 2), Stat("Penetration", 3), Stat("Range", 4)),
                Behavior("Radiator", "", Stat("Emissivity", 1), Stat("PumpedHeat", 2), Temperature("TemperatureFloor", 3), Stat("WasteHeat", 4), Stat("EnergyUsage", 5), Stat("ThermalMass", 6)),
                Behavior("Reactor", "", Stat("Charge", 1), Stat("Efficiency", 2), Stat("OverloadEfficiency", 3), Stat("ThrottlingFactor", 4)),
                Behavior("Reflector", "", Stat("CrossSection", 1)),
                Behavior("ResourceScanner", "", Stat("Range", 1), Stat("MinimumDensity", 2), Stat("ScanDuration", 3)),
                Behavior("Sensor", "", Stat("Sensitivity", 3), Stat("PingBoost", 5), Stat("PingEnergy", 6), Stat("PingVisibility", 7), Stat("PingRange", 8), Stat("PingCooldown", 9)),
                Behavior("Shield", "", Stat("Efficiency", 1), Stat("EnergyUsage", 2)),
                Behavior("Switch", ""),
                Behavior("Thermotoggle", "", Temperature("TargetTemperature", 1)),
                Behavior("Thruster", "", Stat("Thrust", 1), Stat("Visibility", 2), Stat("Heat", 3), Stat("EnergyUsage", 4)),
                Behavior("Trigger", ""),
                Behavior("TurretController", ""),
                Behavior("VelocityConversion", "", Stat("Lambda", 1)),
                Behavior("VelocityLimit", "", Stat("TopSpeed", 1)),
                Behavior("Visibility", "", Stat("Visibility", 1)),
                Behavior("Weapon", "", WeaponFields),
                Behavior("Wear", "")
            }
            .ToDictionary(metadata => metadata.Kind, StringComparer.Ordinal);

        public static AetheriaRuntimeBehaviorMetadata Get(string kind)
        {
            return !string.IsNullOrWhiteSpace(kind) && ByKind.TryGetValue(kind, out var metadata)
                ? metadata
                : null!;
        }

        public static IReadOnlyList<AetheriaRuntimeBehaviorMetadata> All => ByKind.Values.ToArray();

        public static bool IsKindOrDescendant(string candidateKind, string expectedKind)
        {
            if (string.IsNullOrWhiteSpace(candidateKind) || string.IsNullOrWhiteSpace(expectedKind))
            {
                return false;
            }

            var currentKind = candidateKind;
            while (!string.IsNullOrWhiteSpace(currentKind))
            {
                if (string.Equals(currentKind, expectedKind, StringComparison.Ordinal))
                {
                    return true;
                }

                currentKind = ByKind.TryGetValue(currentKind, out var metadata)
                    ? metadata.ParentKind
                    : "";
            }

            return false;
        }

        private static AetheriaRuntimeBehaviorMetadata Behavior(
            string kind,
            string parentKind,
            params AetheriaRuntimeBehaviorFieldMetadata[] displayFields)
        {
            return new AetheriaRuntimeBehaviorMetadata(kind, parentKind, displayFields);
        }

        private static AetheriaRuntimeBehaviorFieldMetadata Stat(string name, int key)
        {
            return new AetheriaRuntimeBehaviorFieldMetadata(name, key, AetheriaRuntimeBehaviorFieldValueKind.PerformanceStat);
        }

        private static AetheriaRuntimeBehaviorFieldMetadata Number(string name, int key)
        {
            return new AetheriaRuntimeBehaviorFieldMetadata(name, key, AetheriaRuntimeBehaviorFieldValueKind.Number);
        }

        private static AetheriaRuntimeBehaviorFieldMetadata Integer(string name, int key)
        {
            return new AetheriaRuntimeBehaviorFieldMetadata(name, key, AetheriaRuntimeBehaviorFieldValueKind.Integer);
        }

        private static AetheriaRuntimeBehaviorFieldMetadata Temperature(string name, int key)
        {
            return new AetheriaRuntimeBehaviorFieldMetadata(name, key, AetheriaRuntimeBehaviorFieldValueKind.Temperature);
        }

        private static AetheriaRuntimeBehaviorFieldMetadata[] With(
            AetheriaRuntimeBehaviorFieldMetadata[] baseFields,
            params AetheriaRuntimeBehaviorFieldMetadata[] extraFields)
        {
            return baseFields.Concat(extraFields).ToArray();
        }
    }
}
