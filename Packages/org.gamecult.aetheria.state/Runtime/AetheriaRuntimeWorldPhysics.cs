using System;
using System.Collections.Generic;

#nullable enable

namespace GameCult.Aetheria.State.Verse
{
    public interface IAetheriaRuntimeWorldPhysics
    {
        string AuthorityId { get; }
        AetheriaRuntimeWorldStep Step(AetheriaRuntimeZoneSnapshotCommit zone, IReadOnlyList<AetheriaRuntimeEntitySnapshotCommit> entities, double deltaSeconds);
    }

    public sealed class AetheriaRuntimeWorldStep
    {
        public AetheriaRuntimeWorldStep(IReadOnlyList<AetheriaRuntimeWorldBodyStep> bodies, IReadOnlyList<AetheriaRuntimeWorldPickupStep> pickups, IReadOnlyList<AetheriaRuntimeWorldContact> contacts)
        { Bodies = bodies ?? Array.Empty<AetheriaRuntimeWorldBodyStep>(); Pickups = pickups ?? Array.Empty<AetheriaRuntimeWorldPickupStep>(); Contacts = contacts ?? Array.Empty<AetheriaRuntimeWorldContact>(); }
        public IReadOnlyList<AetheriaRuntimeWorldBodyStep> Bodies { get; }
        public IReadOnlyList<AetheriaRuntimeWorldPickupStep> Pickups { get; }
        public IReadOnlyList<AetheriaRuntimeWorldContact> Contacts { get; }
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

    public sealed class AetheriaRuntimeWorldContact
    {
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
