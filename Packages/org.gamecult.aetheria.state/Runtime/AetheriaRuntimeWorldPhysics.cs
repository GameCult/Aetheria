using System;
using System.Collections.Generic;

#nullable enable

namespace GameCult.Aetheria.State.Verse
{
    public interface IAetheriaRuntimeWorldPhysics
    {
        string ImplementationId { get; }
        void RetainWorlds(string runId, IReadOnlyList<int> zoneIndices);
        AetheriaRuntimeWorldPickupStep ApplyPickupRejection(
            string runId,
            int zoneIndex,
            AetheriaRuntimeWorldBeginContact contact);
        AetheriaRuntimeWorldStep Step(
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
            IReadOnlyList<AetheriaRuntimeWorldPickupStep> pickups,
            IReadOnlyList<AetheriaRuntimeWorldBeginContact> beginContacts)
        {
            Bodies = bodies ?? Array.Empty<AetheriaRuntimeWorldBodyStep>();
            Pickups = pickups ?? Array.Empty<AetheriaRuntimeWorldPickupStep>();
            BeginContacts = beginContacts ?? Array.Empty<AetheriaRuntimeWorldBeginContact>();
        }
        public IReadOnlyList<AetheriaRuntimeWorldBodyStep> Bodies { get; }
        public IReadOnlyList<AetheriaRuntimeWorldPickupStep> Pickups { get; }
        public IReadOnlyList<AetheriaRuntimeWorldBeginContact> BeginContacts { get; }
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

    public sealed class AetheriaRuntimeWorldBeginContact
    {
        public string FactId { get; set; } = "";
        public string EntityAId { get; set; } = "";
        public string EntityBId { get; set; } = "";
        public int EntityAIndex { get; set; } = -1;
        public int EntityBIndex { get; set; } = -1;
        public int PickupIndex { get; set; } = -1;
        public double PointX { get; set; }
        public double PointZ { get; set; }
        public double NormalX { get; set; }
        public double NormalZ { get; set; }
        public double RelativeSpeed { get; set; }
    }
}
