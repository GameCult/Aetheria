using System;
using System.Collections.Generic;

#nullable enable

namespace GameCult.Aetheria.State.Verse
{
    public interface IAetheriaRuntimeWorldPhysics
    {
        string ImplementationId { get; }
        void RetainWorlds(string runId, IReadOnlyList<int> zoneIndices);
        AetheriaRuntimeWorldStep Step(
            string runId,
            long frameId,
            int simulationStepIndex,
            AetheriaRuntimeZoneSnapshotCommit zone,
            IReadOnlyList<AetheriaRuntimeEntitySnapshotCommit> entities,
            double deltaSeconds);
        AetheriaRuntimePhysicalPayloadStep StepPhysicalPayloads(
            string runId,
            long frameId,
            int simulationStepIndex,
            AetheriaRuntimeZoneSnapshotCommit zone,
            IReadOnlyList<AetheriaRuntimeEntitySnapshotCommit> entities,
            double deltaSeconds);
    }

    public sealed class AetheriaRuntimeWorldStep
    {
        public AetheriaRuntimeWorldStep(
            IReadOnlyList<AetheriaRuntimeWorldBodyStep> bodies,
            IReadOnlyList<AetheriaRuntimeWorldPickupStep> pickups)
        {
            Bodies = bodies ?? Array.Empty<AetheriaRuntimeWorldBodyStep>();
            Pickups = pickups ?? Array.Empty<AetheriaRuntimeWorldPickupStep>();
        }
        public IReadOnlyList<AetheriaRuntimeWorldBodyStep> Bodies { get; }
        public IReadOnlyList<AetheriaRuntimeWorldPickupStep> Pickups { get; }
    }

    public sealed class AetheriaRuntimeWorldPickupStep
    {
        public int PickupIndex { get; set; } = -1;
        public double PositionX { get; set; }
        public double PositionZ { get; set; }
        public double VelocityX { get; set; }
        public double VelocityZ { get; set; }
    }

    public sealed class AetheriaRuntimeWorldBodyStep
    {
        public int EntityIndex { get; set; } = -1;
        public double PositionX { get; set; }
        public double PositionZ { get; set; }
        public double VelocityX { get; set; }
        public double VelocityY { get; set; }
        public double DirectionX { get; set; }
        public double DirectionY { get; set; }
    }

    public sealed class AetheriaRuntimePhysicalPayloadStep
    {
        public AetheriaRuntimePhysicalPayloadStep(
            IReadOnlyList<AetheriaRuntimePhysicalPayloadCommit> payloads,
            IReadOnlyList<AetheriaRuntimePhysicalPayloadHit> hits)
        {
            PhysicalPayloads = payloads ?? Array.Empty<AetheriaRuntimePhysicalPayloadCommit>();
            Hits = hits ?? Array.Empty<AetheriaRuntimePhysicalPayloadHit>();
        }

        public IReadOnlyList<AetheriaRuntimePhysicalPayloadCommit> PhysicalPayloads { get; }
        public IReadOnlyList<AetheriaRuntimePhysicalPayloadHit> Hits { get; }
    }

    public sealed class AetheriaRuntimePhysicalPayloadHit
    {
        public AetheriaRuntimePhysicalPayloadCommit Payload { get; set; } =
            new AetheriaRuntimePhysicalPayloadCommit();
        public string QueryId { get; set; } = "";
        public string PhysicalPayloadBodyId { get; set; } = "";
        public int TargetEntityIndex { get; set; }
        public string TargetBodyId { get; set; } = "";
        public double PointX { get; set; }
        public double PointZ { get; set; }
        public double NormalX { get; set; }
        public double NormalZ { get; set; }
    }
}
