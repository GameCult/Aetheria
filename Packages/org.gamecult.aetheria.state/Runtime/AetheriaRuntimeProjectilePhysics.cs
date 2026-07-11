using System;
using System.Collections.Generic;

#nullable enable

namespace GameCult.Aetheria.State.Verse
{
    public interface IAetheriaRuntimeProjectilePhysics
    {
        string AuthorityId { get; }

        AetheriaRuntimeProjectileStep Step(
            AetheriaRuntimeZoneSnapshotCommit zone,
            IReadOnlyList<AetheriaRuntimeEntitySnapshotCommit> entities,
            double deltaSeconds);
    }

    public sealed class AetheriaRuntimeProjectilePhysicsUnavailable : IAetheriaRuntimeProjectilePhysics
    {
        public static AetheriaRuntimeProjectilePhysicsUnavailable Instance { get; } =
            new AetheriaRuntimeProjectilePhysicsUnavailable();

        private AetheriaRuntimeProjectilePhysicsUnavailable()
        {
        }

        public string AuthorityId => "unavailable";

        public AetheriaRuntimeProjectileStep Step(
            AetheriaRuntimeZoneSnapshotCommit zone,
            IReadOnlyList<AetheriaRuntimeEntitySnapshotCommit> entities,
            double deltaSeconds)
        {
            throw new InvalidOperationException(
                "Aetheria projectile simulation requires an injected authoritative physics owner.");
        }
    }

    public sealed class AetheriaRuntimeProjectileStep
    {
        public AetheriaRuntimeProjectileStep(
            IReadOnlyList<AetheriaRuntimeProjectileCommit> projectiles,
            IReadOnlyList<AetheriaRuntimeProjectileHit> hits)
        {
            Projectiles = projectiles ?? Array.Empty<AetheriaRuntimeProjectileCommit>();
            Hits = hits ?? Array.Empty<AetheriaRuntimeProjectileHit>();
        }

        public IReadOnlyList<AetheriaRuntimeProjectileCommit> Projectiles { get; }

        public IReadOnlyList<AetheriaRuntimeProjectileHit> Hits { get; }
    }

    public sealed class AetheriaRuntimeProjectileHit
    {
        public AetheriaRuntimeProjectileCommit Projectile { get; set; } =
            new AetheriaRuntimeProjectileCommit();

        public string ProjectileBodyId { get; set; } = "";

        public int TargetEntityIndex { get; set; }

        public string TargetBodyId { get; set; } = "";

        public double PointX { get; set; }

        public double PointZ { get; set; }

        public double NormalX { get; set; }

        public double NormalZ { get; set; }
    }
}
