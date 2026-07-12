using System;
using System.Collections.Generic;

#nullable enable

namespace GameCult.Aetheria.State.Verse
{
    public interface IAetheriaRuntimePhysicalPayloadPhysics
    {
        string ImplementationId { get; }

        AetheriaRuntimePhysicalPayloadStep Step(
            AetheriaRuntimeZoneSnapshotCommit zone,
            IReadOnlyList<AetheriaRuntimeEntitySnapshotCommit> entities,
            double deltaSeconds);

    }

    public sealed class AetheriaRuntimePhysicalPayloadPhysicsUnavailable : IAetheriaRuntimePhysicalPayloadPhysics
    {
        public static AetheriaRuntimePhysicalPayloadPhysicsUnavailable Instance { get; } =
            new AetheriaRuntimePhysicalPayloadPhysicsUnavailable();

        private AetheriaRuntimePhysicalPayloadPhysicsUnavailable()
        {
        }

        public string ImplementationId => "unavailable";

        public AetheriaRuntimePhysicalPayloadStep Step(
            AetheriaRuntimeZoneSnapshotCommit zone,
            IReadOnlyList<AetheriaRuntimeEntitySnapshotCommit> entities,
            double deltaSeconds)
        {
            throw new InvalidOperationException(
                "Aetheria physical payload simulation requires an injected authoritative physics owner.");
        }
    }

    public sealed class AetheriaRuntimePhysicalPayloadStep
    {
        public AetheriaRuntimePhysicalPayloadStep(
            IReadOnlyList<AetheriaRuntimePhysicalPayloadCommit> projectiles,
            IReadOnlyList<AetheriaRuntimePhysicalPayloadHit> hits)
        {
            PhysicalPayloads = projectiles ?? Array.Empty<AetheriaRuntimePhysicalPayloadCommit>();
            Hits = hits ?? Array.Empty<AetheriaRuntimePhysicalPayloadHit>();
        }

        public IReadOnlyList<AetheriaRuntimePhysicalPayloadCommit> PhysicalPayloads { get; }

        public IReadOnlyList<AetheriaRuntimePhysicalPayloadHit> Hits { get; }
    }

    public sealed class AetheriaRuntimePhysicalPayloadHit
    {
        public AetheriaRuntimePhysicalPayloadCommit Payload { get; set; } =
            new AetheriaRuntimePhysicalPayloadCommit();

        public string PhysicalPayloadBodyId { get; set; } = "";

        public int TargetEntityIndex { get; set; }

        public string TargetBodyId { get; set; } = "";

        public double PointX { get; set; }

        public double PointZ { get; set; }

        public double NormalX { get; set; }

        public double NormalZ { get; set; }
    }

}
