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
            AetheriaRuntimeDaemonSimulationSettings settings)
        {
            if (run == null || deltaSeconds <= 0)
                return;

            foreach (var zone in run.Zones ?? Array.Empty<AetheriaRuntimeZoneSnapshotCommit>())
            {
                if (zone == null)
                    continue;

                var entities = (zone.Entities ?? Array.Empty<AetheriaRuntimeEntitySnapshotCommit>())
                    .Where(entity => entity != null)
                    .ToArray();
                if (entities.Length == 0)
                    continue;

                EnsureStats(entities, settings);
                ApplyMovementIntent(run, entities, intents?.Movement, settings);
                StepRaiderAi(entities);
                StepTargetPursuit(entities, settings);
                StepMovement(entities, deltaSeconds);
                StepCombat(run, zone, entities, intents, deltaSeconds, settings);
                StepTractorSalvage(run, zone, entities, settings);
                RefreshContacts(entities, settings);
            }
        }

        private static void StepTractorSalvage(
            AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeZoneSnapshotCommit zone,
            IReadOnlyList<AetheriaRuntimeEntitySnapshotCommit> entities,
            AetheriaRuntimeDaemonSimulationSettings settings)
        {
            if (!TryParseEntityIndex(run.CurrentEntityKey, out var actorIndex))
                return;
            var actor = entities.FirstOrDefault(entity => entity.EntityIndex == actorIndex && IsAlive(entity));
            if (actor == null || actor.TractorPower <= 0 || actor.TargetEntityIndex < 0)
                return;
            var wreck = entities.FirstOrDefault(entity => entity.EntityIndex == actor.TargetEntityIndex && !IsAlive(entity));
            if (wreck == null || DistanceSq(actor, wreck) > settings.AttackRange * settings.AttackRange)
                return;

            var loot = (wreck.CargoContents ?? Array.Empty<AetheriaRuntimeCargoBayLoadoutCommit>())
                .SelectMany(bay => bay?.Items ?? Array.Empty<AetheriaRuntimeLoadoutItemSlotCommit>())
                .ToArray();
            if (loot.Length == 0)
                return;
            var bays = (actor.CargoContents ?? Array.Empty<AetheriaRuntimeCargoBayLoadoutCommit>()).ToList();
            if (bays.Count == 0)
                bays.Add(new AetheriaRuntimeCargoBayLoadoutCommit());
            bays[0].Items = (bays[0].Items ?? Array.Empty<AetheriaRuntimeLoadoutItemSlotCommit>()).Concat(loot).ToArray();
            actor.CargoContents = bays.ToArray();
            wreck.CargoContents = Array.Empty<AetheriaRuntimeCargoBayLoadoutCommit>();
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
            if (entity == null || !IsPlayerOwned(entity) || !IsAlive(entity))
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

        private static void StepMovement(
            IReadOnlyList<AetheriaRuntimeEntitySnapshotCommit> entities,
            double deltaSeconds)
        {
            foreach (var entity in entities)
            {
                if (!IsAlive(entity))
                    continue;

                entity.PositionX += entity.VelocityX * deltaSeconds;
                entity.PositionZ += entity.VelocityY * deltaSeconds;
                entity.VelocityX *= 0.992;
                entity.VelocityY *= 0.992;
                if (Math.Abs(entity.VelocityX) + Math.Abs(entity.VelocityY) > 0.01)
                    Face(entity, entity.VelocityX, entity.VelocityY);
            }
        }

        private static void StepCombat(
            AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeZoneSnapshotCommit zone,
            IReadOnlyList<AetheriaRuntimeEntitySnapshotCommit> entities,
            AetheriaRuntimeDaemonIntentState intents,
            double deltaSeconds,
            AetheriaRuntimeDaemonSimulationSettings settings)
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
                SetStat(attacker, Heat, Math.Min(100, GetStat(attacker, Heat) + ResolveProjectileDamage(attacker, settings) * settings.ProjectileHeatScale));
            }

            var projectileStep = AetheriaRuntimeYmirProjectilePhysics.Step(zone, entities, deltaSeconds);
            zone.Projectiles = projectileStep.Projectiles;
            foreach (var hit in projectileStep.Hits)
            {
                if (byIndex.TryGetValue(hit.TargetEntityIndex, out var target))
                    Damage(target, hit.Projectile.Damage);
            }

            foreach (var entity in entities)
            {
                SetStat(entity, Heat, Math.Max(0, GetStat(entity, Heat) - settings.HeatDissipationPerSecond * deltaSeconds));
                entity.IsActive = IsAlive(entity);
                if (!entity.IsActive)
                {
                    entity.VelocityX = 0;
                    entity.VelocityY = 0;
                    entity.TargetEntityIndex = -1;
                }
            }
        }

        private static bool WantsFire(
            AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeZoneSnapshotCommit zone,
            AetheriaRuntimeEntitySnapshotCommit attacker,
            AetheriaRuntimeDaemonIntentState intents)
        {
            var actorKey = run.EntityRecordKey(zone.ZoneIndex, attacker.EntityIndex);
            return (intents == null
                    ? Enumerable.Empty<AetheriaRuntimeDaemonWeaponGroupIntent>()
                    : intents.WeaponGroups)
                .Any(intent => intent != null &&
                    string.Equals(intent.ActorEntityKey, actorKey, StringComparison.Ordinal) &&
                    intent.WeaponGroup == 0 &&
                    intent.Fire &&
                    intent.Active);
        }

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
