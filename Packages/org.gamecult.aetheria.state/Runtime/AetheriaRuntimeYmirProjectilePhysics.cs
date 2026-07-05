using System;
using System.Collections.Generic;
using System.Linq;

#nullable enable

namespace GameCult.Aetheria.State.Verse
{
    public static class AetheriaRuntimeYmirProjectilePhysics
    {
        private const string ProjectileBodyPrefix = "aetheria.projectile.";
        private const string DaemonEntityBodyPrefix = "aetheria.daemon.entity.";
        private const double DefaultEntityPhysicsRadius = 20.0;

        public static AetheriaRuntimeYmirProjectileStep Step(
            AetheriaRuntimeZoneSnapshotCommit zone,
            IReadOnlyList<AetheriaRuntimeEntitySnapshotCommit> entities,
            double deltaSeconds)
        {
            if (zone == null || deltaSeconds <= 0)
            {
                return new AetheriaRuntimeYmirProjectileStep(
                    zone?.Projectiles ?? Array.Empty<AetheriaRuntimeProjectileCommit>(),
                    Array.Empty<AetheriaRuntimeYmirProjectileHit>());
            }

            var entityByIndex = (entities ?? Array.Empty<AetheriaRuntimeEntitySnapshotCommit>())
                .Where(entity => entity != null)
                .ToDictionary(entity => entity.EntityIndex);
            var activeProjectiles = new List<AetheriaRuntimeProjectileCommit>();
            var hits = new List<AetheriaRuntimeYmirProjectileHit>();

            foreach (var projectile in zone.Projectiles ?? Array.Empty<AetheriaRuntimeProjectileCommit>())
            {
                if (projectile == null || !projectile.Active)
                    continue;

                projectile.AgeSeconds += deltaSeconds;
                if (projectile.AgeSeconds >= projectile.LifetimeSeconds)
                    continue;

                AetheriaRuntimeEntitySnapshotCommit? target = null;
                if (projectile.TargetEntityIndex >= 0 &&
                    entityByIndex.TryGetValue(projectile.TargetEntityIndex, out var resolvedTarget) &&
                    IsActiveBody(resolvedTarget))
                {
                    target = resolvedTarget;
                }

                if (projectile.Guided && target != null)
                    GuideProjectile(projectile, target);

                var previousX = projectile.PositionX;
                var previousZ = projectile.PositionZ;
                projectile.PositionX += projectile.VelocityX * deltaSeconds;
                projectile.PositionZ += projectile.VelocityY * deltaSeconds;

                if (target != null &&
                    TryResolveProjectileContact(projectile, previousX, previousZ, target, out var contact))
                {
                    hits.Add(contact);
                    continue;
                }

                activeProjectiles.Add(projectile);
            }

            return new AetheriaRuntimeYmirProjectileStep(
                activeProjectiles.ToArray(),
                hits.ToArray());
        }

        private static void GuideProjectile(
            AetheriaRuntimeProjectileCommit projectile,
            AetheriaRuntimeEntitySnapshotCommit target)
        {
            var speed = Math.Sqrt(projectile.VelocityX * projectile.VelocityX + projectile.VelocityY * projectile.VelocityY);
            if (speed <= 0.0001)
                return;

            var direction = Normalize(target.PositionX - projectile.PositionX, target.PositionZ - projectile.PositionZ);
            if (Math.Abs(direction.X) + Math.Abs(direction.Y) <= 0.0001)
                return;

            projectile.DirectionX = direction.X;
            projectile.DirectionY = direction.Y;
            projectile.VelocityX = direction.X * speed;
            projectile.VelocityY = direction.Y * speed;
        }

        private static bool TryResolveProjectileContact(
            AetheriaRuntimeProjectileCommit projectile,
            double previousX,
            double previousZ,
            AetheriaRuntimeEntitySnapshotCommit target,
            out AetheriaRuntimeYmirProjectileHit contact)
        {
            var targetRadius = ResolveEntityPhysicsRadius(target);
            var combinedRadius = Math.Max(0.001, projectile.Radius) + targetRadius;
            var currentDistanceSq = Sqr(target.PositionX - projectile.PositionX) + Sqr(target.PositionZ - projectile.PositionZ);
            if (currentDistanceSq <= combinedRadius * combinedRadius)
            {
                contact = Contact(projectile, target, projectile.PositionX, projectile.PositionZ);
                return true;
            }

            var segmentX = projectile.PositionX - previousX;
            var segmentZ = projectile.PositionZ - previousZ;
            var segmentLengthSq = segmentX * segmentX + segmentZ * segmentZ;
            if (segmentLengthSq <= 0.000001)
            {
                contact = default!;
                return false;
            }

            var targetX = target.PositionX - previousX;
            var targetZ = target.PositionZ - previousZ;
            var t = Math.Max(0, Math.Min(1, (targetX * segmentX + targetZ * segmentZ) / segmentLengthSq));
            var closestX = previousX + segmentX * t;
            var closestZ = previousZ + segmentZ * t;
            var closestDistanceSq = Sqr(target.PositionX - closestX) + Sqr(target.PositionZ - closestZ);
            if (closestDistanceSq > combinedRadius * combinedRadius)
            {
                contact = default!;
                return false;
            }

            contact = Contact(projectile, target, closestX, closestZ);
            return true;
        }

        private static AetheriaRuntimeYmirProjectileHit Contact(
            AetheriaRuntimeProjectileCommit projectile,
            AetheriaRuntimeEntitySnapshotCommit target,
            double pointX,
            double pointZ)
        {
            var normal = Normalize(pointX - target.PositionX, pointZ - target.PositionZ);
            return new AetheriaRuntimeYmirProjectileHit
            {
                Projectile = projectile,
                ProjectileBodyId = ProjectileBodyPrefix + (projectile.ProjectileId ?? ""),
                TargetEntityIndex = target.EntityIndex,
                TargetBodyId = DaemonEntityBodyPrefix + target.EntityIndex,
                PointX = pointX,
                PointZ = pointZ,
                NormalX = normal.X,
                NormalZ = normal.Y
            };
        }

        private static double ResolveEntityPhysicsRadius(AetheriaRuntimeEntitySnapshotCommit entity)
        {
            if (string.Equals(entity.Kind, "station", StringComparison.OrdinalIgnoreCase))
                return 48.0;

            return DefaultEntityPhysicsRadius;
        }

        private static bool IsActiveBody(AetheriaRuntimeEntitySnapshotCommit entity)
        {
            return entity.IsActive;
        }

        private static double Sqr(double value) => value * value;

        private static (double X, double Y) Normalize(double x, double y)
        {
            var magnitude = Math.Sqrt(x * x + y * y);
            return magnitude <= 0.0001 ? (0, 0) : (x / magnitude, y / magnitude);
        }
    }

    public sealed class AetheriaRuntimeYmirProjectileStep
    {
        public AetheriaRuntimeYmirProjectileStep(
            IReadOnlyList<AetheriaRuntimeProjectileCommit> projectiles,
            IReadOnlyList<AetheriaRuntimeYmirProjectileHit> hits)
        {
            Projectiles = projectiles ?? Array.Empty<AetheriaRuntimeProjectileCommit>();
            Hits = hits ?? Array.Empty<AetheriaRuntimeYmirProjectileHit>();
        }

        public IReadOnlyList<AetheriaRuntimeProjectileCommit> Projectiles { get; }

        public IReadOnlyList<AetheriaRuntimeYmirProjectileHit> Hits { get; }
    }

    public sealed class AetheriaRuntimeYmirProjectileHit
    {
        public AetheriaRuntimeProjectileCommit Projectile { get; set; } = new AetheriaRuntimeProjectileCommit();

        public string ProjectileBodyId { get; set; } = "";

        public int TargetEntityIndex { get; set; }

        public string TargetBodyId { get; set; } = "";

        public double PointX { get; set; }

        public double PointZ { get; set; }

        public double NormalX { get; set; }

        public double NormalZ { get; set; }
    }
}
