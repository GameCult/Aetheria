using System;
using System.Collections.Generic;
using System.Linq;

#nullable enable

namespace GameCult.Aetheria.State.Verse
{
    public sealed class AetheriaRuntimeTurretWeaponRequest
    {
        public AetheriaRuntimeTurretWeaponRequest(int equipmentIndex, int behaviorIndex)
        {
            EquipmentIndex = equipmentIndex;
            BehaviorIndex = behaviorIndex;
        }

        public int EquipmentIndex { get; }
        public int BehaviorIndex { get; }
    }

    public static class AetheriaRuntimeTurretControllerSimulation
    {
        public static IReadOnlyList<AetheriaRuntimeTurretWeaponRequest> StepEntity(
            AetheriaRuntimeEntitySnapshotCommit entity,
            IReadOnlyList<AetheriaRuntimeEntitySnapshotCommit> entities,
            AetheriaRuntimeCatalogSnapshot? catalog)
        {
            var controllers = AetheriaRuntimeEquippedBehaviorQueries.FindExecuting(
                entity, catalog, AetheriaRuntimeBehaviorKinds.TurretController);
            if (controllers.Count == 0)
                return Array.Empty<AetheriaRuntimeTurretWeaponRequest>();

            var weapons = AetheriaRuntimeEquippedBehaviorQueries.FindOperational(entity, catalog, "Weapon");
            var shotSpeed = 0.0;
            var predictShots = false;
            foreach (var weapon in weapons)
            {
                var velocity = Math.Max(0, weapon.EvaluateStat(16, ThermalPerformance(entity, weapon.EquipmentIndex)));
                if (velocity <= 0.1)
                    continue;
                predictShots = true;
                shotSpeed = velocity;
            }
            foreach (var controller in controllers)
            {
                controller.State.TurretControllerWeaponCount = weapons.Count;
                controller.State.TurretControllerShotSpeed = shotSpeed;
                controller.State.TurretControllerPredictShots = predictShots;
            }

            var target = entities.FirstOrDefault(candidate =>
                candidate != null && candidate.EntityIndex == entity.TargetEntityIndex && candidate.IsActive);
            if (target == null)
            {
                entity.TargetEntityIndex = SelectVisibleEnemyShip(entity, entities)?.EntityIndex ?? -1;
                return Array.Empty<AetheriaRuntimeTurretWeaponRequest>();
            }

            var dx = target.PositionX - entity.PositionX;
            var dz = target.PositionZ - entity.PositionZ;
            var distance = Math.Sqrt(dx * dx + dz * dz);
            var aim = predictShots
                ? InterceptDirection(dx, dz, target.VelocityX, target.VelocityY, shotSpeed)
                : Normalize(dx, dz, 0, 1);
            entity.LookDirectionX = aim.X;
            entity.LookDirectionY = aim.Y;

            var forward = Normalize(entity.DirectionX, entity.DirectionY, 0, 1);
            return weapons
                .Where(weapon => weapon.EvaluateStat(6, ThermalPerformance(entity, weapon.EquipmentIndex)) > distance)
                .Where(weapon =>
                {
                    var direction = AetheriaRuntimeEquipmentRotation.RotateQuarter(
                        forward.X,
                        forward.Y,
                        AetheriaRuntimeEquipmentRotation.QuarterTurns(weapon.Slot.Rotation));
                    return direction.X * aim.X + direction.Y * aim.Y > 0.99;
                })
                .Select(weapon => new AetheriaRuntimeTurretWeaponRequest(
                    weapon.EquipmentIndex, weapon.BehaviorIndex))
                .ToArray();
        }

        private static AetheriaRuntimeEntitySnapshotCommit? SelectVisibleEnemyShip(
            AetheriaRuntimeEntitySnapshotCommit observer,
            IReadOnlyList<AetheriaRuntimeEntitySnapshotCommit> entities)
        {
            var visibleHostiles = (observer.Contacts ?? Array.Empty<AetheriaRuntimeEntityContactCommit>())
                .Where(contact => contact != null && contact.Visible && contact.Hostile)
                .Select(contact => contact.TargetEntityIndex)
                .ToHashSet();
            return entities.FirstOrDefault(candidate =>
                candidate != null &&
                candidate.IsActive &&
                string.Equals(candidate.Kind, "ship", StringComparison.OrdinalIgnoreCase) &&
                visibleHostiles.Contains(candidate.EntityIndex));
        }

        private static (double X, double Y) InterceptDirection(
            double relativeX,
            double relativeY,
            double targetVelocityX,
            double targetVelocityY,
            double shotSpeed)
        {
            var relative = new CultMath.float3((float)relativeX, 0, (float)relativeY);
            var velocity = new CultMath.float3((float)targetVelocityX, 0, (float)targetVelocityY);
            var time = CultMath.math.first_order_intercept_time((float)shotSpeed, relative, velocity);
            return Normalize(
                relativeX + targetVelocityX * time,
                relativeY + targetVelocityY * time,
                relativeX,
                relativeY);
        }

        private static double ThermalPerformance(AetheriaRuntimeEntitySnapshotCommit entity, int equipmentIndex) =>
            (entity.EquipmentStates ?? Array.Empty<AetheriaRuntimeEquipmentStateCommit>())
            .FirstOrDefault(state => state != null && state.EquipmentIndex == equipmentIndex)?
            .ThermalPerformance ?? 1;

        private static (double X, double Y) Normalize(
            double x, double y, double fallbackX, double fallbackY)
        {
            var length = Math.Sqrt(x * x + y * y);
            if (length > 0.000001)
                return (x / length, y / length);
            var fallbackLength = Math.Sqrt(fallbackX * fallbackX + fallbackY * fallbackY);
            return fallbackLength > 0.000001
                ? (fallbackX / fallbackLength, fallbackY / fallbackLength)
                : (0, 1);
        }
    }
}
