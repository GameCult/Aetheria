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

                StepPickupLifetimes(run, zone, frameId, deltaSeconds);

                EnsureStats(entities, settings);
                foreach (var entity in entities)
                    AetheriaRuntimeThermalSimulation.EnsureState(entity);
                foreach (var movement in intents?.Movements ?? Enumerable.Empty<AetheriaRuntimeDaemonMovementIntent>())
                    ApplyMovementIntent(run, entities, movement, settings);
                StepTractorPower(entities, deltaSeconds);
                StepRaiderAi(entities);
                StepTargetPursuit(entities, settings);
                var worldStep = StepWorldPhysics(zone, entities, deltaSeconds, worldPhysics);
                ResolvePickupContacts(run, zone, entities, worldStep, catalog, frameId);
                StepCombat(run, zone, entities, intents, deltaSeconds, settings, projectilePhysics, catalog, frameId);
                AetheriaRuntimeMiningSimulation.Step(run, zone, entities, intents, catalog, frameId, simulationTimeSeconds, deltaSeconds);
                AetheriaRuntimeSurveySimulation.Step(run, zone, entities, intents, catalog, frameId, simulationTimeSeconds, deltaSeconds);
                RefreshContacts(entities, settings);
            }
        }

        private static void StepPickupLifetimes(AetheriaRuntimeRunCheckpointCommit run, AetheriaRuntimeZoneSnapshotCommit zone, long frameId, double deltaSeconds)
        {
            foreach (var pickup in zone.DroppedPickups ?? Array.Empty<AetheriaRuntimeDroppedPickupCommit>())
                if (pickup != null) pickup.AgeSeconds += deltaSeconds;
            foreach (var pickup in (zone.DroppedPickups ?? Array.Empty<AetheriaRuntimeDroppedPickupCommit>())
                .Where(pickup => pickup != null && pickup.AgeSeconds >= pickup.LifetimeSeconds))
                AetheriaRuntimeGameEvents.Append(run, new AetheriaRuntimeGameEventCommit { EventId = $"frame:{frameId}:zone:{zone.ZoneIndex}:pickup:{pickup.PickupIndex}:expired", Kind = "pickup.expired", FrameId = frameId, ZoneIndex = zone.ZoneIndex, PickupIndex = pickup.PickupIndex, ItemKey = pickup.Item?.ItemKey ?? "" });
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

        private static AetheriaRuntimeWorldStep StepWorldPhysics(
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
            var pickups = (zone.DroppedPickups ?? Array.Empty<AetheriaRuntimeDroppedPickupCommit>())
                .Where(pickup => pickup != null).ToDictionary(pickup => pickup.PickupIndex);
            foreach (var body in result.Pickups)
            {
                if (!pickups.TryGetValue(body.PickupIndex, out var pickup)) continue;
                pickup.PositionX = body.PositionX; pickup.PositionZ = body.PositionZ;
                pickup.VelocityX = body.VelocityX; pickup.VelocityZ = body.VelocityZ;
            }
            foreach (var parent in entities)
            foreach (var childIndex in parent.ChildEntityIndices ?? Array.Empty<int>())
            {
                var child = entities.FirstOrDefault(value => value.EntityIndex == childIndex);
                if (child == null) continue;
                child.PositionX = parent.PositionX; child.PositionZ = parent.PositionZ;
                child.VelocityX = parent.VelocityX; child.VelocityY = parent.VelocityY;
            }
            return result;
        }

        private static void ResolvePickupContacts(
            AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeZoneSnapshotCommit zone,
            IReadOnlyList<AetheriaRuntimeEntitySnapshotCommit> entities,
            AetheriaRuntimeWorldStep worldStep,
            AetheriaRuntimeCatalogSnapshot? catalog,
            long frameId)
        {
            foreach (var contact in worldStep.Contacts.Where(contact => contact.PickupIndex >= 0))
            {
                var entityIndex = Math.Max(contact.EntityAIndex, contact.EntityBIndex);
                var entity = entities.FirstOrDefault(value => value.EntityIndex == entityIndex && value.IsActive);
                var pickup = (zone.DroppedPickups ?? Array.Empty<AetheriaRuntimeDroppedPickupCommit>())
                    .FirstOrDefault(value => value != null && value.PickupIndex == contact.PickupIndex);
                if (entity == null || pickup == null) continue;
                if (AetheriaRuntimePickupTransactions.TryCollect(zone, entity, pickup.PickupIndex, catalog, requireRange: false))
                {
                    AetheriaRuntimeGameEvents.Append(run, new AetheriaRuntimeGameEventCommit { EventId = $"frame:{frameId}:zone:{zone.ZoneIndex}:entity:{entity.EntityIndex}:pickup:{pickup.PickupIndex}:collected", Kind = "pickup.collected", FrameId = frameId, ZoneIndex = zone.ZoneIndex, TargetEntityIndex = entity.EntityIndex, PickupIndex = pickup.PickupIndex, ItemKey = pickup.Item?.ItemKey ?? "", ScalarValue = Math.Max(1, pickup.Item?.Quantity ?? 1) });
                    continue;
                }
                AetheriaRuntimeGameEvents.Append(run, new AetheriaRuntimeGameEventCommit { EventId = $"frame:{frameId}:zone:{zone.ZoneIndex}:entity:{entity.EntityIndex}:pickup:{pickup.PickupIndex}:rejected", Kind = "pickup.rejected", FrameId = frameId, ZoneIndex = zone.ZoneIndex, TargetEntityIndex = entity.EntityIndex, PickupIndex = pickup.PickupIndex, ItemKey = pickup.Item?.ItemKey ?? "", ScalarValue = Math.Max(1, pickup.Item?.Quantity ?? 1) });
                var dx = pickup.PositionX - entity.PositionX; var dz = pickup.PositionZ - entity.PositionZ;
                var length = Math.Sqrt(dx * dx + dz * dz);
                if (length < 0.001) { dx = 1; dz = 0; length = 1; }
                pickup.VelocityX += dx / length * 25;
                pickup.VelocityZ += dz / length * 25;
            }
        }

        private static void StepCombat(
            AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeZoneSnapshotCommit zone,
            IReadOnlyList<AetheriaRuntimeEntitySnapshotCommit> entities,
            AetheriaRuntimeDaemonIntentState intents,
            double deltaSeconds,
            AetheriaRuntimeDaemonSimulationSettings settings,
            IAetheriaRuntimeProjectilePhysics projectilePhysics,
            AetheriaRuntimeCatalogSnapshot? catalog,
            long frameId)
        {
            var byIndex = entities.ToDictionary(entity => entity.EntityIndex);
            foreach (var attacker in entities)
            {
                var weaponGroup = ResolveFireGroup(run, zone, attacker, intents);
                var weapons = ResolveWeapons(attacker, weaponGroup, catalog, settings);
                foreach (var weapon in weapons)
                {
                    weapon.State.Firing = false;
                    if (weapon.State.Reloading)
                    {
                        weapon.State.ReloadProgress = Math.Max(0, weapon.State.ReloadProgress - deltaSeconds);
                        if (weapon.State.ReloadProgress <= 0)
                        {
                            weapon.State.Reloading = false;
                            weapon.State.Ammo = weapon.MagazineSize;
                        }
                    }
                    weapon.State.CooldownProgress = Math.Max(0, weapon.State.CooldownProgress - deltaSeconds);
                    weapon.State.CoolingDown = weapon.State.CooldownProgress > 0;
                }

                if (!IsAlive(attacker) ||
                    attacker.TargetEntityIndex < 0 ||
                    !byIndex.TryGetValue(attacker.TargetEntityIndex, out var target) ||
                    !IsAlive(target) ||
                    !Hostile(attacker, target))
                {
                    foreach (var weapon in weapons)
                    {
                        weapon.State.LockTargetEntityIndex = -1;
                        weapon.State.LockProgress = 0;
                    }
                    continue;
                }

                if (IsPlayerOwned(attacker) && weaponGroup < 0)
                    continue;

                foreach (var weapon in weapons)
                {
                    UpdateWeaponLock(attacker, target, weapon, deltaSeconds, settings);
                    if (DistanceSq(attacker, target) > weapon.Range * weapon.Range ||
                        weapon.State.LockProgress <= 0.99 ||
                        weapon.State.CooldownProgress > 0 ||
                        weapon.State.Reloading)
                        continue;

                    if (CommitWeaponRound(attacker, weapon) != WeaponRoundResult.Fired)
                        continue;
                    var projectile = SpawnProjectile(zone, attacker, target, weapon, settings);
                    AetheriaRuntimeGameEvents.Append(run, new AetheriaRuntimeGameEventCommit { EventId = $"projectile:{projectile.ProjectileId}:launched", Kind = "projectile.launched", FrameId = frameId, ZoneIndex = zone.ZoneIndex, SourceEntityIndex = attacker.EntityIndex, TargetEntityIndex = target.EntityIndex, SubjectKey = projectile.ProjectileId, ItemKey = projectile.WeaponKind, ScalarValue = projectile.Damage, PositionX = projectile.PositionX, PositionZ = projectile.PositionZ });
                    weapon.State.Firing = true;
                    weapon.State.CoolingDown = true;
                    weapon.State.CooldownProgress = weapon.Cooldown;
                    AetheriaRuntimeThermalSimulation.AddHeat(attacker, weapon.Heat);
                }
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
                {
                    var aliveBefore = IsAlive(target);
                    Damage(target, hit.Projectile.Damage);
                    AetheriaRuntimeGameEvents.Append(run, new AetheriaRuntimeGameEventCommit { EventId = $"projectile:{hit.Projectile.ProjectileId}:impact", Kind = "projectile.impact", FrameId = frameId, ZoneIndex = zone.ZoneIndex, SourceEntityIndex = hit.Projectile.SourceEntityIndex, TargetEntityIndex = target.EntityIndex, SubjectKey = hit.Projectile.ProjectileId, ItemKey = hit.Projectile.WeaponKind, ScalarValue = hit.Projectile.Damage, PositionX = hit.PointX, PositionZ = hit.PointZ });
                    AetheriaRuntimeGameEvents.Append(run, new AetheriaRuntimeGameEventCommit { EventId = $"projectile:{hit.Projectile.ProjectileId}:damage", Kind = "entity.damaged", FrameId = frameId, ZoneIndex = zone.ZoneIndex, SourceEntityIndex = hit.Projectile.SourceEntityIndex, TargetEntityIndex = target.EntityIndex, SubjectKey = hit.Projectile.ProjectileId, ScalarValue = hit.Projectile.Damage, PositionX = hit.PointX, PositionZ = hit.PointZ });
                    if (aliveBefore && !IsAlive(target))
                        AetheriaRuntimeGameEvents.Append(run, new AetheriaRuntimeGameEventCommit { EventId = $"projectile:{hit.Projectile.ProjectileId}:destroyed:{target.EntityIndex}", Kind = "entity.destroyed", FrameId = frameId, ZoneIndex = zone.ZoneIndex, SourceEntityIndex = hit.Projectile.SourceEntityIndex, TargetEntityIndex = target.EntityIndex, SubjectKey = hit.Projectile.ProjectileId, PositionX = hit.PointX, PositionZ = hit.PointZ });
                }
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

        private static void UpdateWeaponLock(
            AetheriaRuntimeEntitySnapshotCommit attacker,
            AetheriaRuntimeEntitySnapshotCommit target,
            ResolvedWeapon weapon,
            double deltaSeconds,
            AetheriaRuntimeDaemonSimulationSettings settings)
        {
            var weaponState = weapon.State;
            if (weaponState.LockTargetEntityIndex != target.EntityIndex)
            {
                weaponState.LockTargetEntityIndex = target.EntityIndex;
                weaponState.LockProgress = 0;
            }

            var targetDirection = Normalize(target.PositionX - attacker.PositionX, target.PositionZ - attacker.PositionZ);
            var lookDirection = Normalize(attacker.DirectionX, attacker.DirectionY);
            var dot = Math.Max(-1.0, Math.Min(1.0,
                targetDirection.X * lookDirection.X + targetDirection.Y * lookDirection.Y));
            var angleDegrees = Math.Acos(dot) * 180.0 / Math.PI;
            if (angleDegrees >= weapon.LockAngleDegrees)
            {
                weaponState.LockProgress = Clamp01(
                    weaponState.LockProgress - deltaSeconds * weapon.LockDecayPerSecond);
                return;
            }

            var contact = (attacker.Contacts ?? Array.Empty<AetheriaRuntimeEntityContactCommit>())
                .FirstOrDefault(candidate => candidate != null && candidate.TargetEntityIndex == target.EntityIndex);
            var information = Clamp01(contact?.InfoGathered ?? 0);
            var directionalQuality = Math.Max(0, 1.0 - angleDegrees / 90.0);
            var acquisition =
                Math.Pow(directionalQuality, weapon.LockDirectionImpact) *
                deltaSeconds *
                weapon.LockSpeed *
                Math.Pow(information, weapon.LockSensorImpact);
            weaponState.LockProgress = Clamp01(weaponState.LockProgress + acquisition);
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

        private static int ResolveFireGroup(
            AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeZoneSnapshotCommit zone,
            AetheriaRuntimeEntitySnapshotCommit attacker,
            AetheriaRuntimeDaemonIntentState intents)
        {
            var intent = (intents == null
                    ? Enumerable.Empty<AetheriaRuntimeDaemonWeaponGroupIntent>()
                    : intents.WeaponGroups)
                .LastOrDefault(intent => intent != null &&
                    ActorMatches(intent.ActorEntityKey, zone.ZoneIndex, attacker.EntityIndex) &&
                    intent.Fire &&
                    intent.Active);
            return intent?.WeaponGroup ?? (IsPlayerOwned(attacker) ? -1 : 0);
        }

        private static bool ActorMatches(string actorEntityKey, int zoneIndex, int entityIndex) =>
            AetheriaRuntimeRunCheckpointCommit.TryParseEntityKey(actorEntityKey, out var actorZoneIndex, out var actorEntityIndex) &&
            actorZoneIndex == zoneIndex && actorEntityIndex == entityIndex;

        private static IReadOnlyList<ResolvedWeapon> ResolveWeapons(
            AetheriaRuntimeEntitySnapshotCommit entity,
            int weaponGroup,
            AetheriaRuntimeCatalogSnapshot? catalog,
            AetheriaRuntimeDaemonSimulationSettings settings)
        {
            var equipmentIndices = weaponGroup >= 0 && weaponGroup < (entity.WeaponGroups ?? Array.Empty<IReadOnlyList<int>>()).Count
                ? entity.WeaponGroups[weaponGroup] ?? Array.Empty<int>()
                : Array.Empty<int>();
            var authored = AetheriaRuntimeEquippedBehaviorQueries.Find(entity, catalog, AetheriaRuntimeBehaviorKinds.InstantWeapon)
                .Where(behavior => equipmentIndices.Contains(behavior.EquipmentIndex))
                .Select(behavior => ResolveAuthoredWeapon(entity, behavior, settings))
                .ToArray();
            if (catalog != null)
                return authored;

            return new[] { ResolveFallbackWeapon(entity, settings) };
        }

        private static ResolvedWeapon ResolveAuthoredWeapon(
            AetheriaRuntimeEntitySnapshotCommit entity,
            AetheriaRuntimeEquippedBehavior behavior,
            AetheriaRuntimeDaemonSimulationSettings settings)
        {
            var state = EnsureWeaponState(entity, AetheriaRuntimeBehaviorStateProjector.EquipmentOwnerKind,
                behavior.EquipmentIndex, behavior.BehaviorIndex, behavior.Payload.Kind,
                Math.Max(0, (int)Math.Round(ReadNumber(behavior.Payload, 13))), settings);
            return new ResolvedWeapon(
                state,
                behavior.Item.ItemKey,
                PositiveOr(behavior.EvaluateStat(2), ResolveProjectileDamage(entity, settings)),
                PositiveOr(behavior.EvaluateStat(6), settings.AttackRange),
                PositiveOr(behavior.EvaluateStat(19), settings.WeaponCooldownSeconds),
                PositiveOr(behavior.EvaluateStat(16), settings.ProjectileSpeed),
                Math.Max(0, behavior.EvaluateStat(10)),
                Math.Max(0, behavior.EvaluateStat(9)),
                ReadItemKey(behavior.Payload, 12),
                Math.Max(0, (int)Math.Round(ReadNumber(behavior.Payload, 13))),
                PositiveOr(ReadNumber(behavior.Payload, 14), settings.WeaponCooldownSeconds),
                PositiveOr(behavior.EvaluateStat(21), settings.WeaponLockSpeed),
                Math.Max(0, behavior.EvaluateStat(22)),
                PositiveOr(behavior.EvaluateStat(23), settings.WeaponLockAngleDegrees),
                PositiveOr(behavior.EvaluateStat(24), settings.WeaponLockDirectionImpact),
                Math.Max(0, behavior.EvaluateStat(25)));
        }

        private static ResolvedWeapon ResolveFallbackWeapon(
            AetheriaRuntimeEntitySnapshotCommit entity,
            AetheriaRuntimeDaemonSimulationSettings settings)
        {
            var state = EnsureWeaponState(entity, "daemon-simulation", entity.EntityIndex, 0, "ProjectileWeapon", -1, settings);
            return new ResolvedWeapon(state,
                IsPlayerOwned(entity) ? "vanguard-bolt" : "raider-bolt",
                ResolveProjectileDamage(entity, settings), settings.AttackRange, settings.WeaponCooldownSeconds,
                settings.ProjectileSpeed, ResolveProjectileDamage(entity, settings) * settings.ProjectileHeatScale,
                0, "", -1, settings.WeaponCooldownSeconds,
                settings.WeaponLockSpeed, settings.WeaponLockSensorImpact, settings.WeaponLockAngleDegrees,
                settings.WeaponLockDirectionImpact, settings.WeaponLockDecayPerSecond);
        }

        private static AetheriaRuntimeWeaponStateCommit EnsureWeaponState(
            AetheriaRuntimeEntitySnapshotCommit entity,
            string ownerKind,
            int ownerIndex,
            int behaviorIndex,
            string behaviorKind,
            int initialAmmo,
            AetheriaRuntimeDaemonSimulationSettings settings)
        {
            var states = (entity.WeaponStates ?? Array.Empty<AetheriaRuntimeWeaponStateCommit>()).ToList();
            var state = states.FirstOrDefault(candidate =>
                candidate != null &&
                string.Equals(candidate.OwnerKind, ownerKind, StringComparison.Ordinal) &&
                candidate.OwnerIndex == ownerIndex &&
                candidate.BehaviorIndex == behaviorIndex);
            if (state != null)
                return state;

            state = new AetheriaRuntimeWeaponStateCommit
            {
                OwnerKind = ownerKind,
                OwnerIndex = ownerIndex,
                BehaviorIndex = behaviorIndex,
                BehaviorKind = behaviorKind,
                Ammo = initialAmmo,
                BurstRemaining = 0,
                BurstInterval = settings.WeaponCooldownSeconds,
                LockTargetEntityIndex = -1
            };
            states.Add(state);
            entity.WeaponStates = states.ToArray();
            return state;
        }

        private static AetheriaRuntimeProjectileCommit SpawnProjectile(
            AetheriaRuntimeZoneSnapshotCommit zone,
            AetheriaRuntimeEntitySnapshotCommit attacker,
            AetheriaRuntimeEntitySnapshotCommit target,
            ResolvedWeapon weapon,
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
            var projectile = new AetheriaRuntimeProjectileCommit
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
                VelocityX = direction.X * weapon.ProjectileSpeed,
                VelocityY = direction.Y * weapon.ProjectileSpeed,
                Damage = weapon.Damage,
                Radius = settings.ProjectileRadius,
                LifetimeSeconds = settings.ProjectileLifetimeSeconds,
                Guided = true,
                Active = true,
                WeaponKind = weapon.ItemKey
            };
            projectiles.Add(projectile);
            zone.Projectiles = projectiles.ToArray();
            return projectile;
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

        private static double PositiveOr(double value, double fallback) =>
            double.IsFinite(value) && value > 0 ? value : fallback;

        private static string ReadItemKey(AetheriaRuntimeBehaviorPayload payload, int key)
        {
            var value = (payload.Fields ?? Array.Empty<AetheriaRuntimeBehaviorField>())
                .FirstOrDefault(field => field != null && field.Key == key)?.Value;
            return value?.ItemKeyValue ?? value?.StringValue ?? "";
        }

        private static double ReadNumber(AetheriaRuntimeBehaviorPayload payload, int key)
        {
            return (payload.Fields ?? Array.Empty<AetheriaRuntimeBehaviorField>())
                .FirstOrDefault(field => field != null && field.Key == key)?.Value?.NumberValue ?? 0;
        }

        private static WeaponRoundResult CommitWeaponRound(
            AetheriaRuntimeEntitySnapshotCommit entity,
            ResolvedWeapon weapon)
        {
            if (weapon.MagazineSize > 1 && weapon.State.Ammo <= 0)
            {
                if (!string.IsNullOrWhiteSpace(weapon.AmmoItemKey))
                {
                    if (!AetheriaRuntimeCargoTransactions.TryFind(entity, weapon.AmmoItemKey,
                            out var cargoIndex, out var x, out var y) ||
                        !AetheriaRuntimeCargoTransactions.TryRemoveQuantity(
                            entity, cargoIndex, weapon.AmmoItemKey, x, y, 1, out _))
                        return WeaponRoundResult.NoAmmo;
                }
                weapon.State.Reloading = true;
                weapon.State.ReloadProgress = weapon.ReloadTime;
                return WeaponRoundResult.ReloadStarted;
            }

            if (!CanSupplyEnergy(entity, weapon.Energy))
                return WeaponRoundResult.InsufficientEnergy;
            CommitEnergy(entity, weapon.Energy);
            if (weapon.MagazineSize > 1)
                weapon.State.Ammo--;
            return WeaponRoundResult.Fired;
        }

        private static bool CanSupplyEnergy(AetheriaRuntimeEntitySnapshotCommit entity, double demand)
        {
            if (demand < 0.01) return true;
            var states = entity.BehaviorStates ?? Array.Empty<AetheriaRuntimeBehaviorStateCommit>();
            var charge = states.Where(value => value != null && value.BehaviorKind == "Capacitor")
                .Sum(value => Math.Max(0, value.CapacitorCharge));
            return charge >= demand || states.Any(value => value != null && value.BehaviorKind == "Reactor");
        }

        private static void CommitEnergy(AetheriaRuntimeEntitySnapshotCommit entity, double demand)
        {
            if (demand < 0.01) return;
            var capacitors = (entity.BehaviorStates ?? Array.Empty<AetheriaRuntimeBehaviorStateCommit>())
                .Where(value => value != null && value.BehaviorKind == "Capacitor" && value.CapacitorCharge > 0)
                .ToList();
            var remaining = demand;
            while (remaining > 0.000001 && capacitors.Count > 0)
            {
                var share = remaining / capacitors.Count;
                foreach (var capacitor in capacitors.ToArray())
                {
                    var drained = Math.Min(share, capacitor.CapacitorCharge);
                    capacitor.CapacitorCharge -= drained;
                    remaining -= drained;
                    if (capacitor.CapacitorCharge <= 0.000001) capacitors.Remove(capacitor);
                }
            }
            var reactors = (entity.BehaviorStates ?? Array.Empty<AetheriaRuntimeBehaviorStateCommit>())
                .Where(value => value != null && value.BehaviorKind == "Reactor")
                .ToArray();
            if (remaining > 0.000001 && reactors.Length > 0)
                foreach (var reactor in reactors) reactor.ReactorDraw += remaining / reactors.Length;
        }

        private enum WeaponRoundResult { Fired, ReloadStarted, InsufficientEnergy, NoAmmo }

        private sealed class ResolvedWeapon
        {
            public ResolvedWeapon(AetheriaRuntimeWeaponStateCommit state, string itemKey, double damage, double range,
                double cooldown, double projectileSpeed, double heat, double energy, string ammoItemKey,
                int magazineSize, double reloadTime, double lockSpeed, double lockSensorImpact,
                double lockAngleDegrees, double lockDirectionImpact, double lockDecayPerSecond)
            {
                State = state; ItemKey = itemKey; Damage = damage; Range = range; Cooldown = cooldown;
                ProjectileSpeed = projectileSpeed; Heat = heat; Energy = energy; AmmoItemKey = ammoItemKey;
                MagazineSize = magazineSize; ReloadTime = reloadTime; LockSpeed = lockSpeed;
                LockSensorImpact = lockSensorImpact; LockAngleDegrees = lockAngleDegrees;
                LockDirectionImpact = lockDirectionImpact; LockDecayPerSecond = lockDecayPerSecond;
            }

            public AetheriaRuntimeWeaponStateCommit State { get; }
            public string ItemKey { get; }
            public double Damage { get; }
            public double Range { get; }
            public double Cooldown { get; }
            public double ProjectileSpeed { get; }
            public double Heat { get; }
            public double Energy { get; }
            public string AmmoItemKey { get; }
            public int MagazineSize { get; }
            public double ReloadTime { get; }
            public double LockSpeed { get; }
            public double LockSensorImpact { get; }
            public double LockAngleDegrees { get; }
            public double LockDirectionImpact { get; }
            public double LockDecayPerSecond { get; }
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
