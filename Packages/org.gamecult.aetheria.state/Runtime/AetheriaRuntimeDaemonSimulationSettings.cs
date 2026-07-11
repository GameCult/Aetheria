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
                weaponLockDecayPerSecond: 1.0);

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
            double weaponLockDecayPerSecond = AetheriaDefaultRaw.WeaponLockDecayPerSecond)
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
        public double StationSensorRange { get; }
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
        }
    }
}
