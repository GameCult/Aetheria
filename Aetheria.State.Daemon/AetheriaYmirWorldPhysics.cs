using GameCult.Aetheria.State.Verse;
using Ymir.Core;

namespace Aetheria.State.Daemon;

public sealed class AetheriaYmirWorldPhysics : IAetheriaRuntimeWorldPhysics
{
    public const double TractorRadius = 25;
    public const double TractorTraction = 25;
    public const double TractorDistance = 75;
    private const string Prefix = "aetheria.daemon.entity.";
    private const string PickupPrefix = "aetheria.daemon.pickup.";
    private readonly YmirSimulator _simulator;
    public AetheriaYmirWorldPhysics(YmirSimulator? simulator = null) => _simulator = simulator ?? new YmirSimulator();
    public string AuthorityId => "ymir.core";

    public AetheriaRuntimeWorldStep Step(AetheriaRuntimeZoneSnapshotCommit zone, IReadOnlyList<AetheriaRuntimeEntitySnapshotCommit> entities, double deltaSeconds)
    {
        ArgumentNullException.ThrowIfNull(zone);
        if (deltaSeconds <= 0) throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
        var attached = (entities ?? Array.Empty<AetheriaRuntimeEntitySnapshotCommit>()).SelectMany(entity => entity.ChildEntityIndices ?? Array.Empty<int>()).ToHashSet();
        var active = (entities ?? Array.Empty<AetheriaRuntimeEntitySnapshotCommit>()).Where(entity => entity.IsActive && !attached.Contains(entity.EntityIndex)).ToArray();
        var entityVelocity = active.ToDictionary(entity => entity.EntityIndex, entity => new Vec2((float)entity.VelocityX, (float)entity.VelocityY));
        var pickups = (zone.DroppedPickups ?? Array.Empty<AetheriaRuntimeDroppedPickupCommit>()).Where(pickup => pickup != null && pickup.AgeSeconds < pickup.LifetimeSeconds).ToArray();
        var pickupVelocity = pickups.ToDictionary(pickup => pickup.PickupIndex, pickup => new Vec2((float)pickup.VelocityX, (float)pickup.VelocityZ));
        foreach (var actor in active.Where(entity => entity.TractorPower > 0))
        {
            var forwardLength = Math.Sqrt(actor.DirectionX * actor.DirectionX + actor.DirectionY * actor.DirectionY);
            var fx = forwardLength < 0.001 ? 0 : actor.DirectionX / forwardLength;
            var fz = forwardLength < 0.001 ? 1 : actor.DirectionY / forwardLength;
            foreach (var pickup in pickups)
            {
                var dx = pickup.PositionX - actor.PositionX; var dz = pickup.PositionZ - actor.PositionZ;
                var along = dx * fx + dz * fz;
                var lateral = Math.Abs(dx * fz - dz * fx);
                if (along < 0 || along > TractorDistance || lateral > TractorRadius) continue;
                var distance = Math.Sqrt(dx * dx + dz * dz);
                if (distance <= 0.001) continue;
                var impulse = TractorTraction * actor.TractorPower * deltaSeconds;
                var current = pickupVelocity[pickup.PickupIndex];
                pickupVelocity[pickup.PickupIndex] = new Vec2(current.X - (float)(dx / distance * impulse), current.Y - (float)(dz / distance * impulse));
            }
        }
        var bodies = active
            .Select(entity => new PhysicsBody(Prefix + entity.EntityIndex, new Vec2((float)entity.PositionX, (float)entity.PositionZ), entityVelocity[entity.EntityIndex],
                string.Equals(entity.Kind, "station", StringComparison.OrdinalIgnoreCase) ? 48 : 20, 1,
                IsStatic: string.Equals(entity.Kind, "station", StringComparison.OrdinalIgnoreCase), Restitution: 0.2f,
                Direction: new Vec2((float)entity.DirectionX, (float)entity.DirectionY)))
            .Concat(pickups.Select(pickup => new PhysicsBody(PickupPrefix + pickup.PickupIndex, new Vec2((float)pickup.PositionX, (float)pickup.PositionZ), pickupVelocity[pickup.PickupIndex], 5, 1, Restitution: 0.2f)))
            .ToArray();
        var fields = AetheriaRuntimeDaemonRenderQueries.QueryBodyPoses(zone)
            .Select(pose => (Pose: pose, Body: (zone.Bodies ?? Array.Empty<AetheriaRuntimeBodySnapshotCommit>()).FirstOrDefault(body => body != null && body.BodyKey == pose.BodyKey)))
            .Where(pair => pair.Body != null && pair.Body.GravityInfluenceRadius > 0 && pair.Body.GravityWellDepth != 0)
            .Select(pair => new RadialField("aetheria.daemon.gravity." + pair.Pose.BodyKey, new Vec2((float)pair.Pose.CenterX, (float)pair.Pose.CenterZ), (float)pair.Body!.GravityWellDepth, (float)pair.Body.GravityInfluenceRadius)).ToArray();
        var result = _simulator.Step(new SimulationStepRequest((float)deltaSeconds, new YmirWorld((float)zone.SimulationTimeSeconds, bodies, fields)));
        int Index(string id) => id.StartsWith(Prefix, StringComparison.Ordinal) && int.TryParse(id.AsSpan(Prefix.Length), out var value) ? value : -1;
        int PickupIndex(string id) => id.StartsWith(PickupPrefix, StringComparison.Ordinal) && int.TryParse(id.AsSpan(PickupPrefix.Length), out var value) ? value : -1;
        return new AetheriaRuntimeWorldStep(
            result.World.Bodies.Where(body => body.Id.StartsWith(Prefix, StringComparison.Ordinal)).Select(body => new AetheriaRuntimeWorldBodyStep { EntityIndex = Index(body.Id), PositionX = body.Position.X, PositionZ = body.Position.Y, VelocityX = body.Velocity.X, VelocityY = body.Velocity.Y, DirectionX = body.Direction?.X ?? 0, DirectionY = body.Direction?.Y ?? 1 }).ToArray(),
            result.World.Bodies.Where(body => body.Id.StartsWith(PickupPrefix, StringComparison.Ordinal)).Select(body => new AetheriaRuntimeWorldPickupStep { PickupIndex = PickupIndex(body.Id), PositionX = body.Position.X, PositionZ = body.Position.Y, VelocityX = body.Velocity.X, VelocityZ = body.Velocity.Y }).ToArray(),
            result.Contacts.Select(contact => new AetheriaRuntimeWorldContact { EntityAIndex = Index(contact.BodyA), EntityBIndex = Index(contact.BodyB), PickupIndex = Math.Max(PickupIndex(contact.BodyA), PickupIndex(contact.BodyB)), PointX = contact.Point.X, PointZ = contact.Point.Y, NormalX = contact.Normal.X, NormalZ = contact.Normal.Y, RelativeSpeed = contact.RelativeSpeed }).ToArray());
    }
}
