using System;
using System.Collections.Generic;
using System.Linq;

#nullable enable

namespace GameCult.Aetheria.State.Verse
{
    public static class AetheriaRuntimeDaemonSimulation
    {
        private const string Hull = "hull";
        private const string Shield = "shield";
        private const string Heat = "heat";

        public static void Step(
            AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeDaemonIntentState intents,
            double deltaSeconds,
            AetheriaRuntimeDaemonSimulationSettings settings,
            IAetheriaRuntimeProjectilePhysics projectilePhysics,
            IAetheriaRuntimeWorldPhysics worldPhysics,
            AetheriaRuntimeCatalogSnapshot? catalog = null,
            long frameId = 0,
            double simulationTimeSeconds = 0)
        {
            if (run == null || deltaSeconds <= 0)
                return;
            if (projectilePhysics == null)
                throw new ArgumentNullException(nameof(projectilePhysics));
            if (worldPhysics == null)
                throw new ArgumentNullException(nameof(worldPhysics));

            foreach (var zone in run.Zones ?? Array.Empty<AetheriaRuntimeZoneSnapshotCommit>())
            {
                if (zone == null)
                    continue;

                var entities = (zone.Entities ?? Array.Empty<AetheriaRuntimeEntitySnapshotCommit>())
                    .Where(entity => entity != null)
                    .ToArray();
                if (entities.Length == 0)
                    continue;

                StepPickupLifetimes(zone, deltaSeconds);

                EnsureStats(entities, settings);
                foreach (var entity in entities)
                    AetheriaRuntimeThermalSimulation.EnsureState(entity);
                foreach (var movement in intents?.Movements ?? Enumerable.Empty<AetheriaRuntimeDaemonMovementIntent>())
                    ApplyMovementIntent(run, entities, movement, settings);
                StepTractorPower(entities, deltaSeconds);
                StepRaiderAi(entities);
                StepTargetPursuit(entities, settings);
                StepWorldPhysics(zone, entities, deltaSeconds, worldPhysics);
                StepCombat(run, zone, entities, intents, deltaSeconds, settings, projectilePhysics);
                AetheriaRuntimeMiningSimulation.Step(run, zone, entities, intents, catalog, frameId, simulationTimeSeconds, deltaSeconds);
                AetheriaRuntimeSurveySimulation.Step(run, zone, entities, intents, catalog, frameId, simulationTimeSeconds, deltaSeconds);
                RefreshContacts(entities, settings);
            }
        }

        private static void StepPickupLifetimes(AetheriaRuntimeZoneSnapshotCommit zone, double deltaSeconds)
        {
            foreach (var pickup in zone.DroppedPickups ?? Array.Empty<AetheriaRuntimeDroppedPickupCommit>())
                if (pickup != null) pickup.AgeSeconds += deltaSeconds;
            zone.DroppedPickups = (zone.DroppedPickups ?? Array.Empty<AetheriaRuntimeDroppedPickupCommit>())
                .Where(pickup => pickup != null && pickup.AgeSeconds < pickup.LifetimeSeconds)
                .ToArray();
        }

        private static void StepTractorPower(IReadOnlyList<AetheriaRuntimeEntitySnapshotCommit> entities, double deltaSeconds)
        {
            foreach (var entity in entities)
            {
                var delta = entity.TractorTargetPower - entity.TractorPower;
                entity.TractorPower += Math.Sign(delta) * Math.Min(Math.Abs(delta), deltaSeconds * 2.0);
                entity.TractorPower = Clamp01(entity.TractorPower);
            }
        }

        private static void ApplyMovementIntent(
            AetheriaRuntimeRunCheckpointCommit run,
            IReadOnlyList<AetheriaRuntimeEntitySnapshotCommit> entities,
            AetheriaRuntimeDaemonMovementIntent? movement,
            AetheriaRuntimeDaemonSimulationSettings settings)
        {
            if (movement == null || !TryParseEntityIndex(movement.ActorEntityKey, out var entityIndex))
                return;

            var entity = entities.FirstOrDefault(candidate => candidate.EntityIndex == entityIndex);
            if (entity == null || !IsAlive(entity))
                return;

            var magnitude = Clamp01(movement.Magnitude);
            var normalized = Normalize(movement.DirectionX, movement.DirectionY);
            var speed = ResolveSpeed(entity, settings);
            entity.VelocityX = normalized.X * speed * magnitude;
            entity.VelocityY = normalized.Y * speed * magnitude;
            if (magnitude <= 0.001)
                entity.TargetEntityIndex = -1;
            else
                Face(entity, normalized.X, normalized.Y);
        }

        private static void StepRaiderAi(IReadOnlyList<AetheriaRuntimeEntitySnapshotCommit> entities)
        {
            foreach (var raider in entities.Where(entity => string.Equals(entity.FactionKey, "raider", StringComparison.OrdinalIgnoreCase)))
            {
                if (!IsAlive(raider))
                    continue;

                var target = entities
                    .Where(IsPlayerOwned)
                    .Where(IsAlive)
                    .OrderBy(candidate => DistanceSq(raider, candidate))
                    .FirstOrDefault();
                if (target == null)
                    continue;

                raider.TargetEntityIndex = target.EntityIndex;
            }
        }

        private static void StepTargetPursuit(
            IReadOnlyList<AetheriaRuntimeEntitySnapshotCommit> entities,
            AetheriaRuntimeDaemonSimulationSettings settings)
        {
            var byIndex = entities.ToDictionary(entity => entity.EntityIndex);
            foreach (var entity in entities)
            {
                if (!IsAlive(entity) ||
                    entity.TargetEntityIndex < 0 ||
                    !byIndex.TryGetValue(entity.TargetEntityIndex, out var target) ||
                    !IsAlive(target))
                {
                    continue;
                }

                var dx = target.PositionX - entity.PositionX;
                var dy = target.PositionZ - entity.PositionZ;
                var distance = Math.Sqrt(dx * dx + dy * dy);
                if (distance <= settings.AttackRange * settings.AttackHoldRatio)
                {
                    entity.VelocityX *= 0.72;
                    entity.VelocityY *= 0.72;
                    Face(entity, dx, dy);
                    continue;
                }

                if (string.Equals(entity.FactionKey, "raider", StringComparison.OrdinalIgnoreCase))
                {
                    var direction = Normalize(dx, dy);
                    var speed = ResolveSpeed(entity, settings);
                    entity.VelocityX = direction.X * speed;
                    entity.VelocityY = direction.Y * speed;
                    Face(entity, direction.X, direction.Y);
                }
            }
        }

        private static void StepWorldPhysics(
            AetheriaRuntimeZoneSnapshotCommit zone,
            IReadOnlyList<AetheriaRuntimeEntitySnapshotCommit> entities,
            double deltaSeconds,
            IAetheriaRuntimeWorldPhysics worldPhysics)
        {
            var result = worldPhysics.Step(zone, entities, deltaSeconds);
            var byIndex = entities.ToDictionary(entity => entity.EntityIndex);
            foreach (var body in result.Bodies)
            {
                if (!byIndex.TryGetValue(body.EntityIndex, out var entity)) continue;
                entity.PositionX = body.PositionX; entity.PositionZ = body.PositionZ;
                entity.VelocityX = body.VelocityX; entity.VelocityY = body.VelocityY;
                entity.DirectionX = body.DirectionX; entity.DirectionY = body.DirectionY;
            }
            foreach (var parent in entities)
            foreach (var childIndex in parent.ChildEntityIndices ?? Array.Empty<int>())
            {
                var child = entities.FirstOrDefault(value => value.EntityIndex == childIndex);
                if (child == null) continue;
                child.PositionX = parent.PositionX; child.PositionZ = parent.PositionZ;
                child.VelocityX = parent.VelocityX; child.VelocityY = parent.VelocityY;
            }
        }

        private static void StepCombat(
            AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeZoneSnapshotCommit zone,
            IReadOnlyList<AetheriaRuntimeEntitySnapshotCommit> entities,
            AetheriaRuntimeDaemonIntentState intents,
            double deltaSeconds,
            AetheriaRuntimeDaemonSimulationSettings settings,
            IAetheriaRuntimeProjectilePhysics projectilePhysics)
        {
            var byIndex = entities.ToDictionary(entity => entity.EntityIndex);
            foreach (var attacker in entities)
            {
                var weaponState = EnsureDaemonWeaponState(attacker, settings);
                weaponState.Firing = false;
                weaponState.CooldownProgress = Math.Max(0, weaponState.CooldownProgress - deltaSeconds);
                weaponState.CoolingDown = weaponState.CooldownProgress > 0;

                if (!IsAlive(attacker) ||
                    attacker.TargetEntityIndex < 0 ||
                    !byIndex.TryGetValue(attacker.TargetEntityIndex, out var target) ||
                    !IsAlive(target) ||
                    !Hostile(attacker, target))
                {
                    continue;
                }

                if (DistanceSq(attacker, target) > settings.AttackRange * settings.AttackRange)
                    continue;

                Face(attacker, target.PositionX - attacker.PositionX, target.PositionZ - attacker.PositionZ);
                weaponState.LockTargetEntityIndex = target.EntityIndex;
                weaponState.LockProgress = 1.0;

                if (weaponState.CooldownProgress > 0)
                    continue;

                if (IsPlayerOwned(attacker) && !WantsFire(run, zone, attacker, intents))
                    continue;

                SpawnProjectile(zone, attacker, target, settings);
                weaponState.Firing = true;
                weaponState.CoolingDown = true;
                weaponState.CooldownProgress = settings.WeaponCooldownSeconds;
                AetheriaRuntimeThermalSimulation.AddHeat(
                    attacker,
                    ResolveProjectileDamage(attacker, settings) * settings.ProjectileHeatScale);
            }

            PrepareProjectiles(zone, byIndex, deltaSeconds);
            var projectileStep = zone.Projectiles.Count == 0
                ? new AetheriaRuntimeProjectileStep(
                    Array.Empty<AetheriaRuntimeProjectileCommit>(),
                    Array.Empty<AetheriaRuntimeProjectileHit>())
                : projectilePhysics.Step(zone, entities, deltaSeconds);
            zone.Projectiles = projectileStep.Projectiles;
            foreach (var hit in projectileStep.Hits)
            {
                if (byIndex.TryGetValue(hit.TargetEntityIndex, out var target))
                    Damage(target, hit.Projectile.Damage);
            }

            foreach (var entity in entities)
            {
                AetheriaRuntimeThermalSimulation.Step(entity, deltaSeconds);
                entity.IsActive = IsAlive(entity);
                if (!entity.IsActive)
                {
                    entity.VelocityX = 0;
                    entity.VelocityY = 0;
                    entity.TargetEntityIndex = -1;
                }
            }
        }

        private static void PrepareProjectiles(
            AetheriaRuntimeZoneSnapshotCommit zone,
            IReadOnlyDictionary<int, AetheriaRuntimeEntitySnapshotCommit> entities,
            double deltaSeconds)
        {
            var active = new List<AetheriaRuntimeProjectileCommit>();
            foreach (var projectile in zone.Projectiles ?? Array.Empty<AetheriaRuntimeProjectileCommit>())
            {
                if (projectile == null || !projectile.Active)
                    continue;

                projectile.AgeSeconds += deltaSeconds;
                if (projectile.AgeSeconds >= projectile.LifetimeSeconds)
                    continue;

                if (projectile.Guided &&
                    projectile.TargetEntityIndex >= 0 &&
                    entities.TryGetValue(projectile.TargetEntityIndex, out var target) &&
                    IsAlive(target))
                {
                    GuideProjectile(projectile, target);
                }

                active.Add(projectile);
            }
            zone.Projectiles = active;
        }

        private static void GuideProjectile(
            AetheriaRuntimeProjectileCommit projectile,
            AetheriaRuntimeEntitySnapshotCommit target)
        {
            var speed = Math.Sqrt(
                projectile.VelocityX * projectile.VelocityX +
                projectile.VelocityY * projectile.VelocityY);
            if (speed <= 0.0001)
                return;

            var x = target.PositionX - projectile.PositionX;
            var y = target.PositionZ - projectile.PositionZ;
            var magnitude = Math.Sqrt(x * x + y * y);
            if (magnitude <= 0.0001)
                return;

            projectile.DirectionX = x / magnitude;
            projectile.DirectionY = y / magnitude;
            projectile.VelocityX = projectile.DirectionX * speed;
            projectile.VelocityY = projectile.DirectionY * speed;
        }

        private static bool WantsFire(
            AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeZoneSnapshotCommit zone,
            AetheriaRuntimeEntitySnapshotCommit attacker,
            AetheriaRuntimeDaemonIntentState intents)
        {
            return (intents == null
                    ? Enumerable.Empty<AetheriaRuntimeDaemonWeaponGroupIntent>()
                    : intents.WeaponGroups)
                .Any(intent => intent != null &&
                    ActorMatches(intent.ActorEntityKey, zone.ZoneIndex, attacker.EntityIndex) &&
                    intent.WeaponGroup == 0 &&
                    intent.Fire &&
                    intent.Active);
        }

        private static bool ActorMatches(string actorEntityKey, int zoneIndex, int entityIndex) =>
            AetheriaRuntimeRunCheckpointCommit.TryParseEntityKey(actorEntityKey, out var actorZoneIndex, out var actorEntityIndex) &&
            actorZoneIndex == zoneIndex && actorEntityIndex == entityIndex;

        private static AetheriaRuntimeWeaponStateCommit EnsureDaemonWeaponState(
            AetheriaRuntimeEntitySnapshotCommit entity,
            AetheriaRuntimeDaemonSimulationSettings settings)
        {
            var states = (entity.WeaponStates ?? Array.Empty<AetheriaRuntimeWeaponStateCommit>()).ToList();
            var state = states.FirstOrDefault(candidate =>
                candidate != null &&
                string.Equals(candidate.OwnerKind, "daemon-simulation", StringComparison.Ordinal) &&
                candidate.OwnerIndex == entity.EntityIndex);
            if (state != null)
                return state;

            state = new AetheriaRuntimeWeaponStateCommit
            {
                OwnerKind = "daemon-simulation",
                OwnerIndex = entity.EntityIndex,
                BehaviorIndex = 0,
                BehaviorKind = "ProjectileWeapon",
                Ammo = -1,
                BurstRemaining = 0,
                BurstInterval = settings.WeaponCooldownSeconds,
                LockTargetEntityIndex = -1
            };
            states.Add(state);
            entity.WeaponStates = states.ToArray();
            return state;
        }

        private static void SpawnProjectile(
            AetheriaRuntimeZoneSnapshotCommit zone,
            AetheriaRuntimeEntitySnapshotCommit attacker,
            AetheriaRuntimeEntitySnapshotCommit target,
            AetheriaRuntimeDaemonSimulationSettings settings)
        {
            var direction = Normalize(target.PositionX - attacker.PositionX, target.PositionZ - attacker.PositionZ);
            if (Math.Abs(direction.X) + Math.Abs(direction.Y) <= 0.0001)
                direction = Normalize(attacker.DirectionX, attacker.DirectionY);
            if (Math.Abs(direction.X) + Math.Abs(direction.Y) <= 0.0001)
                direction = (0, 1);

            var projectiles = (zone.Projectiles ?? Array.Empty<AetheriaRuntimeProjectileCommit>())
                .Where(projectile => projectile != null && projectile.Active)
                .ToList();
            projectiles.Add(new AetheriaRuntimeProjectileCommit
            {
                ProjectileId = CreateProjectileId(zone, attacker, target, projectiles.Count),
                SourceEntityIndex = attacker.EntityIndex,
                TargetEntityIndex = target.EntityIndex,
                FactionKey = attacker.FactionKey ?? "",
                PositionX = attacker.PositionX + direction.X * settings.ProjectileSpawnOffset,
                PositionY = attacker.PositionY,
                PositionZ = attacker.PositionZ + direction.Y * settings.ProjectileSpawnOffset,
                DirectionX = direction.X,
                DirectionY = direction.Y,
                VelocityX = direction.X * settings.ProjectileSpeed,
                VelocityY = direction.Y * settings.ProjectileSpeed,
                Damage = ResolveProjectileDamage(attacker, settings),
                Radius = settings.ProjectileRadius,
                LifetimeSeconds = settings.ProjectileLifetimeSeconds,
                Guided = true,
                Active = true,
                WeaponKind = IsPlayerOwned(attacker) ? "vanguard-bolt" : "raider-bolt"
            });
            zone.Projectiles = projectiles.ToArray();
        }

        private static string CreateProjectileId(
            AetheriaRuntimeZoneSnapshotCommit zone,
            AetheriaRuntimeEntitySnapshotCommit attacker,
            AetheriaRuntimeEntitySnapshotCommit target,
            int ordinal)
        {
            var tick = Math.Max(0, (long)Math.Round(zone.SimulationTimeSeconds * 1000.0));
            return $"projectile:{zone.ZoneIndex}:{tick}:{attacker.EntityIndex}:{target.EntityIndex}:{ordinal}";
        }

        private static double ResolveProjectileDamage(
            AetheriaRuntimeEntitySnapshotCommit attacker,
            AetheriaRuntimeDaemonSimulationSettings settings)
        {
            return IsPlayerOwned(attacker) ? settings.PawnProjectileDamage : settings.RaiderProjectileDamage;
        }

        private static void RefreshContacts(
            IReadOnlyList<AetheriaRuntimeEntitySnapshotCommit> entities,
            AetheriaRuntimeDaemonSimulationSettings settings)
        {
            foreach (var observer in entities)
            {
                var contacts = new List<AetheriaRuntimeEntityContactCommit>();
                foreach (var target in entities)
                {
                    if (target.EntityIndex == observer.EntityIndex)
                        continue;

                    var distance = Math.Sqrt(DistanceSq(observer, target));
                    var visible = distance <= ResolveSensorRange(observer, settings);
                    if (!visible && !Hostile(observer, target))
                        continue;

                    contacts.Add(new AetheriaRuntimeEntityContactCommit
                    {
                        TargetEntityIndex = target.EntityIndex,
                        InfoGathered = visible ? 1.0 : 0.25,
                        Hostile = Hostile(observer, target),
                        Visible = visible
                    });
                }

                observer.Contacts = contacts;
                observer.Visibility = observer.IsActive ? ResolveSensorRange(observer, settings) : 0;
                observer.VisibilitySourceCount = observer.IsActive ? 1 : 0;
            }
        }

        private static void EnsureStats(
            IReadOnlyList<AetheriaRuntimeEntitySnapshotCommit> entities,
            AetheriaRuntimeDaemonSimulationSettings settings)
        {
            foreach (var entity in entities)
            {
                if (entity.StatGrids == null || entity.StatGrids.Count == 0)
                {
                    entity.StatGrids = new[]
                    {
                        Stat(Hull, DefaultHull(entity, settings)),
                        Stat(Shield, DefaultShield(entity, settings)),
                        Stat(Heat, 0)
                    };
                }

                if (!entity.IsActive && GetStat(entity, Hull) > 0)
                    entity.IsActive = true;
            }
        }

        private static void Damage(AetheriaRuntimeEntitySnapshotCommit target, double damage)
        {
            var shield = GetStat(target, Shield);
            var shieldDamage = Math.Min(shield, damage);
            if (shieldDamage > 0)
            {
                SetStat(target, Shield, shield - shieldDamage);
                damage -= shieldDamage;
            }

            if (damage > 0)
                SetStat(target, Hull, Math.Max(0, GetStat(target, Hull) - damage));
        }

        private static bool IsAlive(AetheriaRuntimeEntitySnapshotCommit entity)
        {
            return entity.IsActive && GetStat(entity, Hull) > 0;
        }

        private static bool IsPlayerOwned(AetheriaRuntimeEntitySnapshotCommit entity)
        {
            return string.Equals(entity.FactionKey, "player", StringComparison.OrdinalIgnoreCase);
        }

        private static bool Hostile(
            AetheriaRuntimeEntitySnapshotCommit left,
            AetheriaRuntimeEntitySnapshotCommit right)
        {
            return IsPlayerOwned(left) != IsPlayerOwned(right) &&
                (string.Equals(left.FactionKey, "raider", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(right.FactionKey, "raider", StringComparison.OrdinalIgnoreCase));
        }

        private static double ResolveSpeed(
            AetheriaRuntimeEntitySnapshotCommit entity,
            AetheriaRuntimeDaemonSimulationSettings settings)
        {
            if (string.Equals(entity.Kind, "station", StringComparison.OrdinalIgnoreCase))
                return 0;

            return string.Equals(entity.FactionKey, "raider", StringComparison.OrdinalIgnoreCase)
                ? settings.RaiderSpeed
                : settings.PawnSpeed;
        }

        private static double ResolveSensorRange(
            AetheriaRuntimeEntitySnapshotCommit entity,
            AetheriaRuntimeDaemonSimulationSettings settings)
        {
            return string.Equals(entity.Kind, "station", StringComparison.OrdinalIgnoreCase)
                ? settings.StationSensorRange
                : settings.EntitySensorRange;
        }

        private static double DefaultHull(
            AetheriaRuntimeEntitySnapshotCommit entity,
            AetheriaRuntimeDaemonSimulationSettings settings)
        {
            return string.Equals(entity.Kind, "station", StringComparison.OrdinalIgnoreCase)
                ? (IsPlayerOwned(entity) ? settings.PlayerStationHull : settings.HostileStationHull)
                : string.Equals(entity.FactionKey, "raider", StringComparison.OrdinalIgnoreCase)
                    ? settings.RaiderEntityHull
                    : settings.PlayerEntityHull;
        }

        private static double DefaultShield(
            AetheriaRuntimeEntitySnapshotCommit entity,
            AetheriaRuntimeDaemonSimulationSettings settings)
        {
            return string.Equals(entity.Kind, "station", StringComparison.OrdinalIgnoreCase)
                ? settings.StationShield
                : settings.EntityShield;
        }

        private static double GetStat(AetheriaRuntimeEntitySnapshotCommit entity, string name)
        {
            var grid = (entity.StatGrids ?? Array.Empty<AetheriaRuntimeEntityStatGridCommit>())
                .FirstOrDefault(candidate => string.Equals(candidate.Name, name, StringComparison.OrdinalIgnoreCase));
            return grid?.Values?.FirstOrDefault() ?? 0;
        }

        private static void SetStat(AetheriaRuntimeEntitySnapshotCommit entity, string name, double value)
        {
            var grids = (entity.StatGrids ?? Array.Empty<AetheriaRuntimeEntityStatGridCommit>()).ToList();
            var index = grids.FindIndex(candidate => string.Equals(candidate.Name, name, StringComparison.OrdinalIgnoreCase));
            var grid = Stat(name, value);
            if (index >= 0)
                grids[index] = grid;
            else
                grids.Add(grid);
            entity.StatGrids = grids;
        }

        private static AetheriaRuntimeEntityStatGridCommit Stat(string name, double value)
        {
            return new AetheriaRuntimeEntityStatGridCommit
            {
                Name = name,
                Width = 1,
                Height = 1,
                Values = new[] { value }
            };
        }

        private static double DistanceSq(
            AetheriaRuntimeEntitySnapshotCommit left,
            AetheriaRuntimeEntitySnapshotCommit right)
        {
            var dx = right.PositionX - left.PositionX;
            var dy = right.PositionZ - left.PositionZ;
            return dx * dx + dy * dy;
        }

        private static (double X, double Y) Normalize(double x, double y)
        {
            var magnitude = Math.Sqrt(x * x + y * y);
            return magnitude <= 0.0001 ? (0, 0) : (x / magnitude, y / magnitude);
        }

        private static void Face(AetheriaRuntimeEntitySnapshotCommit entity, double x, double y)
        {
            var direction = Normalize(x, y);
            if (Math.Abs(direction.X) + Math.Abs(direction.Y) <= 0.0001)
                return;

            entity.DirectionX = direction.X;
            entity.DirectionY = direction.Y;
        }

        private static double Clamp01(double value)
        {
            return value < 0 ? 0 : value > 1 ? 1 : value;
        }

        private static bool TryParseEntityIndex(string? entityKey, out int entityIndex)
        {
            entityIndex = -1;
            if (string.IsNullOrWhiteSpace(entityKey))
                return false;

            var marker = entityKey.LastIndexOf(".entity.", StringComparison.OrdinalIgnoreCase);
            if (marker < 0)
                return false;

            var start = marker + ".entity.".Length;
            var end = start;
            while (end < entityKey.Length && char.IsDigit(entityKey[end]))
                end++;

            return end > start && int.TryParse(entityKey.Substring(start, end - start), out entityIndex);
        }
    }
}
