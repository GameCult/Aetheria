using MessagePack;

#nullable enable

namespace GameCult.Aetheria.State.Verse
{
    [MessagePackObject(true)]
    public readonly struct AetheriaRuntimeDaemonSimulationSettings
    {
        public static AetheriaRuntimeDaemonSimulationSettings AetheriaDefault { get; } =
            new AetheriaRuntimeDaemonSimulationSettings(
                pawnSpeed: 84.0,
                raiderSpeed: 68.0,
                attackRange: 145.0,
                attackHoldRatio: 0.82,
                pawnProjectileDamage: 18.0,
                raiderProjectileDamage: 12.0,
                weaponCooldownSeconds: 0.55,
                projectileSpeed: 330.0,
                projectileRadius: 18.0,
                projectileLifetimeSeconds: 2.2,
                projectileSpawnOffset: 18.0,
                projectileHeatScale: 0.18,
                heatDissipationPerSecond: 8.0,
                stationSensorRange: 720.0,
                entitySensorRange: 520.0,
                playerStationHull: 420.0,
                hostileStationHull: 240.0,
                playerEntityHull: 120.0,
                raiderEntityHull: 80.0,
                stationShield: 120.0,
                entityShield: 45.0,
                weaponLockSpeed: 2.0,
                weaponLockSensorImpact: 1.0,
                weaponLockAngleDegrees: 45.0,
                weaponLockDirectionImpact: 1.0,
                weaponLockDecayPerSecond: 1.0,
                lootDropProbability: 0.25,
                lootDropVelocity: 25.0,
                pickupLifetimeSeconds: 30.0,
                severeThermalRiskThreshold: 0.25,
                heatstrokeTemperature: 330.0,
                heatstrokeMultiplier: 0.00001,
                heatstrokeExponent: 2.0,
                heatstrokeRecoveryPerSecond: 0.2,
                hypothermiaTemperature: 273.0,
                hypothermiaMultiplier: 0.00001,
                hypothermiaExponent: 2.0,
                hypothermiaRecoveryPerSecond: 0.2,
                agentRangeExponent: 0.25,
                agentForwardLerp: 0.5,
                agentMaxForwardDistance: 50,
                agentDpsSampleCount: 32,
                wormholeAnimationDuration: 4,
                wormholeExitCurveStart: 0.8,
                wormholeExitVelocity: 20,
                wormholeExitRadius: 50,
                wormholeDepth: 1000);

        public AetheriaRuntimeDaemonSimulationSettings(
            double pawnSpeed,
            double raiderSpeed,
            double attackRange,
            double attackHoldRatio,
            double pawnProjectileDamage,
            double raiderProjectileDamage,
            double weaponCooldownSeconds,
            double projectileSpeed,
            double projectileRadius,
            double projectileLifetimeSeconds,
            double projectileSpawnOffset,
            double projectileHeatScale,
            double heatDissipationPerSecond,
            double stationSensorRange,
            double entitySensorRange,
            double playerStationHull,
            double hostileStationHull,
            double playerEntityHull,
            double raiderEntityHull,
            double stationShield,
            double entityShield,
            double weaponLockSpeed = AetheriaDefaultRaw.WeaponLockSpeed,
            double weaponLockSensorImpact = AetheriaDefaultRaw.WeaponLockSensorImpact,
            double weaponLockAngleDegrees = AetheriaDefaultRaw.WeaponLockAngleDegrees,
            double weaponLockDirectionImpact = AetheriaDefaultRaw.WeaponLockDirectionImpact,
            double weaponLockDecayPerSecond = AetheriaDefaultRaw.WeaponLockDecayPerSecond,
            double lootDropProbability = AetheriaDefaultRaw.LootDropProbability,
            double lootDropVelocity = AetheriaDefaultRaw.LootDropVelocity,
            double pickupLifetimeSeconds = AetheriaDefaultRaw.PickupLifetimeSeconds,
            double severeThermalRiskThreshold = AetheriaDefaultRaw.SevereThermalRiskThreshold,
            double heatstrokeTemperature = AetheriaDefaultRaw.HeatstrokeTemperature,
            double heatstrokeMultiplier = AetheriaDefaultRaw.HeatstrokeMultiplier,
            double heatstrokeExponent = AetheriaDefaultRaw.HeatstrokeExponent,
            double heatstrokeRecoveryPerSecond = AetheriaDefaultRaw.HeatstrokeRecoveryPerSecond,
            double hypothermiaTemperature = AetheriaDefaultRaw.HypothermiaTemperature,
            double hypothermiaMultiplier = AetheriaDefaultRaw.HypothermiaMultiplier,
            double hypothermiaExponent = AetheriaDefaultRaw.HypothermiaExponent,
            double hypothermiaRecoveryPerSecond = AetheriaDefaultRaw.HypothermiaRecoveryPerSecond,
            double aetherTorqueMultiplier = AetheriaDefaultRaw.AetherTorqueMultiplier,
            double aetherHeatMultiplier = AetheriaDefaultRaw.AetherHeatMultiplier,
            double torqueFloor = AetheriaDefaultRaw.TorqueFloor,
            double torqueMultiplier = AetheriaDefaultRaw.TorqueMultiplier,
            double agentRangeExponent = AetheriaDefaultRaw.AgentRangeExponent,
            double agentForwardLerp = AetheriaDefaultRaw.AgentForwardLerp,
            double agentMaxForwardDistance = AetheriaDefaultRaw.AgentMaxForwardDistance,
            int agentDpsSampleCount = AetheriaDefaultRaw.AgentDpsSampleCount,
            double wormholeAnimationDuration = AetheriaDefaultRaw.WormholeAnimationDuration,
            double wormholeExitCurveStart = AetheriaDefaultRaw.WormholeExitCurveStart,
            double wormholeExitVelocity = AetheriaDefaultRaw.WormholeExitVelocity,
            double wormholeExitRadius = AetheriaDefaultRaw.WormholeExitRadius,
            double wormholeDepth = AetheriaDefaultRaw.WormholeDepth)
        {
            PawnSpeed = PositiveOr(pawnSpeed, AetheriaDefaultRaw.PawnSpeed);
            RaiderSpeed = PositiveOr(raiderSpeed, AetheriaDefaultRaw.RaiderSpeed);
            AttackRange = PositiveOr(attackRange, AetheriaDefaultRaw.AttackRange);
            AttackHoldRatio = Clamp01Or(attackHoldRatio, AetheriaDefaultRaw.AttackHoldRatio);
            PawnProjectileDamage = PositiveOr(pawnProjectileDamage, AetheriaDefaultRaw.PawnProjectileDamage);
            RaiderProjectileDamage = PositiveOr(raiderProjectileDamage, AetheriaDefaultRaw.RaiderProjectileDamage);
            WeaponCooldownSeconds = PositiveOr(weaponCooldownSeconds, AetheriaDefaultRaw.WeaponCooldownSeconds);
            ProjectileSpeed = PositiveOr(projectileSpeed, AetheriaDefaultRaw.ProjectileSpeed);
            ProjectileRadius = PositiveOr(projectileRadius, AetheriaDefaultRaw.ProjectileRadius);
            ProjectileLifetimeSeconds = PositiveOr(projectileLifetimeSeconds, AetheriaDefaultRaw.ProjectileLifetimeSeconds);
            ProjectileSpawnOffset = PositiveOr(projectileSpawnOffset, AetheriaDefaultRaw.ProjectileSpawnOffset);
            ProjectileHeatScale = NonNegativeOr(projectileHeatScale, AetheriaDefaultRaw.ProjectileHeatScale);
            HeatDissipationPerSecond = NonNegativeOr(heatDissipationPerSecond, AetheriaDefaultRaw.HeatDissipationPerSecond);
            StationSensorRange = PositiveOr(stationSensorRange, AetheriaDefaultRaw.StationSensorRange);
            EntitySensorRange = PositiveOr(entitySensorRange, AetheriaDefaultRaw.EntitySensorRange);
            PlayerStationHull = PositiveOr(playerStationHull, AetheriaDefaultRaw.PlayerStationHull);
            HostileStationHull = PositiveOr(hostileStationHull, AetheriaDefaultRaw.HostileStationHull);
            PlayerEntityHull = PositiveOr(playerEntityHull, AetheriaDefaultRaw.PlayerEntityHull);
            RaiderEntityHull = PositiveOr(raiderEntityHull, AetheriaDefaultRaw.RaiderEntityHull);
            StationShield = NonNegativeOr(stationShield, AetheriaDefaultRaw.StationShield);
            EntityShield = NonNegativeOr(entityShield, AetheriaDefaultRaw.EntityShield);
            WeaponLockSpeed = PositiveOr(weaponLockSpeed, AetheriaDefaultRaw.WeaponLockSpeed);
            WeaponLockSensorImpact = NonNegativeOr(weaponLockSensorImpact, AetheriaDefaultRaw.WeaponLockSensorImpact);
            WeaponLockAngleDegrees = PositiveOr(weaponLockAngleDegrees, AetheriaDefaultRaw.WeaponLockAngleDegrees);
            WeaponLockDirectionImpact = NonNegativeOr(weaponLockDirectionImpact, AetheriaDefaultRaw.WeaponLockDirectionImpact);
            WeaponLockDecayPerSecond = NonNegativeOr(weaponLockDecayPerSecond, AetheriaDefaultRaw.WeaponLockDecayPerSecond);
            LootDropProbability = Clamp01Or(lootDropProbability, AetheriaDefaultRaw.LootDropProbability);
            LootDropVelocity = NonNegativeOr(lootDropVelocity, AetheriaDefaultRaw.LootDropVelocity);
            PickupLifetimeSeconds = PositiveOr(pickupLifetimeSeconds, AetheriaDefaultRaw.PickupLifetimeSeconds);
            SevereThermalRiskThreshold = Clamp01Or(severeThermalRiskThreshold, AetheriaDefaultRaw.SevereThermalRiskThreshold);
            HeatstrokeTemperature = PositiveOr(heatstrokeTemperature, AetheriaDefaultRaw.HeatstrokeTemperature);
            HeatstrokeMultiplier = NonNegativeOr(heatstrokeMultiplier, AetheriaDefaultRaw.HeatstrokeMultiplier);
            HeatstrokeExponent = PositiveOr(heatstrokeExponent, AetheriaDefaultRaw.HeatstrokeExponent);
            HeatstrokeRecoveryPerSecond = NonNegativeOr(heatstrokeRecoveryPerSecond, AetheriaDefaultRaw.HeatstrokeRecoveryPerSecond);
            HypothermiaTemperature = PositiveOr(hypothermiaTemperature, AetheriaDefaultRaw.HypothermiaTemperature);
            HypothermiaMultiplier = NonNegativeOr(hypothermiaMultiplier, AetheriaDefaultRaw.HypothermiaMultiplier);
            HypothermiaExponent = PositiveOr(hypothermiaExponent, AetheriaDefaultRaw.HypothermiaExponent);
            HypothermiaRecoveryPerSecond = NonNegativeOr(hypothermiaRecoveryPerSecond, AetheriaDefaultRaw.HypothermiaRecoveryPerSecond);
            AetherTorqueMultiplier = NonNegativeOr(aetherTorqueMultiplier, AetheriaDefaultRaw.AetherTorqueMultiplier);
            AetherHeatMultiplier = NonNegativeOr(aetherHeatMultiplier, AetheriaDefaultRaw.AetherHeatMultiplier);
            TorqueFloor = NonNegativeOr(torqueFloor, AetheriaDefaultRaw.TorqueFloor);
            TorqueMultiplier = NonNegativeOr(torqueMultiplier, AetheriaDefaultRaw.TorqueMultiplier);
            AgentRangeExponent = NonNegativeOr(agentRangeExponent, AetheriaDefaultRaw.AgentRangeExponent);
            AgentForwardLerp = Clamp01Or(agentForwardLerp, AetheriaDefaultRaw.AgentForwardLerp);
            AgentMaxForwardDistance = PositiveOr(agentMaxForwardDistance, AetheriaDefaultRaw.AgentMaxForwardDistance);
            AgentDpsSampleCount = agentDpsSampleCount > 0 ? agentDpsSampleCount : AetheriaDefaultRaw.AgentDpsSampleCount;
            WormholeAnimationDuration = PositiveOr(wormholeAnimationDuration, AetheriaDefaultRaw.WormholeAnimationDuration);
            WormholeExitCurveStart = Clamp01Or(wormholeExitCurveStart, AetheriaDefaultRaw.WormholeExitCurveStart);
            WormholeExitVelocity = PositiveOr(wormholeExitVelocity, AetheriaDefaultRaw.WormholeExitVelocity);
            WormholeExitRadius = PositiveOr(wormholeExitRadius, AetheriaDefaultRaw.WormholeExitRadius);
            WormholeDepth = PositiveOr(wormholeDepth, AetheriaDefaultRaw.WormholeDepth);
        }

        public double PawnSpeed { get; }
        public double RaiderSpeed { get; }
        public double AttackRange { get; }
        public double AttackHoldRatio { get; }
        public double PawnProjectileDamage { get; }
        public double RaiderProjectileDamage { get; }
        public double WeaponCooldownSeconds { get; }
        public double ProjectileSpeed { get; }
        public double ProjectileRadius { get; }
        public double ProjectileLifetimeSeconds { get; }
        public double ProjectileSpawnOffset { get; }
        public double ProjectileHeatScale { get; }
        public double HeatDissipationPerSecond { get; }
        [System.Obsolete("Sensor reach derives from installed equipment. Retained only for snapshot compatibility.")]
        public double StationSensorRange { get; }
        public double FallbackSensorRange => EntitySensorRange;
        public double EntitySensorRange { get; }
        public double PlayerStationHull { get; }
        public double HostileStationHull { get; }
        public double PlayerEntityHull { get; }
        public double RaiderEntityHull { get; }
        public double StationShield { get; }
        public double EntityShield { get; }
        public double WeaponLockSpeed { get; }
        public double WeaponLockSensorImpact { get; }
        public double WeaponLockAngleDegrees { get; }
        public double WeaponLockDirectionImpact { get; }
        public double WeaponLockDecayPerSecond { get; }
        public double LootDropProbability { get; }
        public double LootDropVelocity { get; }
        public double PickupLifetimeSeconds { get; }
        public double SevereThermalRiskThreshold { get; }
        public double HeatstrokeTemperature { get; }
        public double HeatstrokeMultiplier { get; }
        public double HeatstrokeExponent { get; }
        public double HeatstrokeRecoveryPerSecond { get; }
        public double HypothermiaTemperature { get; }
        public double HypothermiaMultiplier { get; }
        public double HypothermiaExponent { get; }
        public double HypothermiaRecoveryPerSecond { get; }
        public double AetherTorqueMultiplier { get; }
        public double AetherHeatMultiplier { get; }
        public double TorqueFloor { get; }
        public double TorqueMultiplier { get; }
        public double AgentRangeExponent { get; }
        public double AgentForwardLerp { get; }
        public double AgentMaxForwardDistance { get; }
        public int AgentDpsSampleCount { get; }
        public double WormholeAnimationDuration { get; }
        public double WormholeExitCurveStart { get; }
        public double WormholeExitVelocity { get; }
        public double WormholeExitRadius { get; }
        public double WormholeDepth { get; }

        private static double PositiveOr(double value, double fallback)
        {
            return double.IsFinite(value) && value > 0 ? value : fallback;
        }

        private static double NonNegativeOr(double value, double fallback)
        {
            return double.IsFinite(value) && value >= 0 ? value : fallback;
        }

        private static double Clamp01Or(double value, double fallback)
        {
            return double.IsFinite(value) ? value < 0 ? 0 : value > 1 ? 1 : value : fallback;
        }

        private static class AetheriaDefaultRaw
        {
            public const double PawnSpeed = 84.0;
            public const double RaiderSpeed = 68.0;
            public const double AttackRange = 145.0;
            public const double AttackHoldRatio = 0.82;
            public const double PawnProjectileDamage = 18.0;
            public const double RaiderProjectileDamage = 12.0;
            public const double WeaponCooldownSeconds = 0.55;
            public const double ProjectileSpeed = 330.0;
            public const double ProjectileRadius = 18.0;
            public const double ProjectileLifetimeSeconds = 2.2;
            public const double ProjectileSpawnOffset = 18.0;
            public const double ProjectileHeatScale = 0.18;
            public const double HeatDissipationPerSecond = 8.0;
            public const double StationSensorRange = 720.0;
            public const double EntitySensorRange = 520.0;
            public const double PlayerStationHull = 420.0;
            public const double HostileStationHull = 240.0;
            public const double PlayerEntityHull = 120.0;
            public const double RaiderEntityHull = 80.0;
            public const double StationShield = 120.0;
            public const double EntityShield = 45.0;
            public const double WeaponLockSpeed = 2.0;
            public const double WeaponLockSensorImpact = 1.0;
            public const double WeaponLockAngleDegrees = 45.0;
            public const double WeaponLockDirectionImpact = 1.0;
            public const double WeaponLockDecayPerSecond = 1.0;
            public const double LootDropProbability = 0.25;
            public const double LootDropVelocity = 25.0;
            public const double PickupLifetimeSeconds = 30.0;
            public const double SevereThermalRiskThreshold = 0.25;
            public const double HeatstrokeTemperature = 330.0;
            public const double HeatstrokeMultiplier = 0.00001;
            public const double HeatstrokeExponent = 2.0;
            public const double HeatstrokeRecoveryPerSecond = 0.2;
            public const double HypothermiaTemperature = 273.0;
            public const double HypothermiaMultiplier = 0.00001;
            public const double HypothermiaExponent = 2.0;
            public const double HypothermiaRecoveryPerSecond = 0.2;
            public const double AetherTorqueMultiplier = 0.1;
            public const double AetherHeatMultiplier = 0.25;
            public const double TorqueFloor = 0.5;
            public const double TorqueMultiplier = 0.1;
            public const double AgentRangeExponent = 0.25;
            public const double AgentForwardLerp = 0.5;
            public const double AgentMaxForwardDistance = 50.0;
            public const int AgentDpsSampleCount = 32;
            public const double WormholeAnimationDuration = 4.0;
            public const double WormholeExitCurveStart = 0.8;
            public const double WormholeExitVelocity = 20.0;
            public const double WormholeExitRadius = 50.0;
            public const double WormholeDepth = 1000.0;
        }
    }
}
