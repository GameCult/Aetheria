using GameCult.Aetheria.State.Verse;
using Ymir.Core;

namespace Aetheria.State.Daemon;

public sealed class AetheriaYmirWorldPhysics : IAetheriaRuntimeWorldPhysics
{
    private const string Prefix = "aetheria.daemon.entity.";
    private readonly YmirSimulator _simulator;
    public AetheriaYmirWorldPhysics(YmirSimulator? simulator = null) => _simulator = simulator ?? new YmirSimulator();
    public string AuthorityId => "ymir.core";

    public AetheriaRuntimeWorldStep Step(AetheriaRuntimeZoneSnapshotCommit zone, IReadOnlyList<AetheriaRuntimeEntitySnapshotCommit> entities, double deltaSeconds)
    {
        ArgumentNullException.ThrowIfNull(zone);
        if (deltaSeconds <= 0) throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
        var attached = (entities ?? Array.Empty<AetheriaRuntimeEntitySnapshotCommit>()).SelectMany(entity => entity.ChildEntityIndices ?? Array.Empty<int>()).ToHashSet();
        var bodies = (entities ?? Array.Empty<AetheriaRuntimeEntitySnapshotCommit>()).Where(entity => entity.IsActive && !attached.Contains(entity.EntityIndex))
            .Select(entity => new PhysicsBody(Prefix + entity.EntityIndex, new Vec2((float)entity.PositionX, (float)entity.PositionZ), new Vec2((float)entity.VelocityX, (float)entity.VelocityY),
                string.Equals(entity.Kind, "station", StringComparison.OrdinalIgnoreCase) ? 48 : 20, 1,
                IsStatic: string.Equals(entity.Kind, "station", StringComparison.OrdinalIgnoreCase), Restitution: 0.2f,
                Direction: new Vec2((float)entity.DirectionX, (float)entity.DirectionY))).ToArray();
        var fields = AetheriaRuntimeDaemonRenderQueries.QueryBodyPoses(zone)
            .Select(pose => (Pose: pose, Body: (zone.Bodies ?? Array.Empty<AetheriaRuntimeBodySnapshotCommit>()).FirstOrDefault(body => body != null && body.BodyKey == pose.BodyKey)))
            .Where(pair => pair.Body != null && pair.Body.GravityInfluenceRadius > 0 && pair.Body.GravityWellDepth != 0)
            .Select(pair => new RadialField("aetheria.daemon.gravity." + pair.Pose.BodyKey, new Vec2((float)pair.Pose.CenterX, (float)pair.Pose.CenterZ), (float)pair.Body!.GravityWellDepth, (float)pair.Body.GravityInfluenceRadius)).ToArray();
        var result = _simulator.Step(new SimulationStepRequest((float)deltaSeconds, new YmirWorld((float)zone.SimulationTimeSeconds, bodies, fields)));
        int Index(string id) => int.TryParse(id.AsSpan(Prefix.Length), out var value) ? value : -1;
        return new AetheriaRuntimeWorldStep(
            result.World.Bodies.Select(body => new AetheriaRuntimeWorldBodyStep { EntityIndex = Index(body.Id), PositionX = body.Position.X, PositionZ = body.Position.Y, VelocityX = body.Velocity.X, VelocityY = body.Velocity.Y, DirectionX = body.Direction?.X ?? 0, DirectionY = body.Direction?.Y ?? 1 }).ToArray(),
            result.Contacts.Select(contact => new AetheriaRuntimeWorldContact { EntityAIndex = Index(contact.BodyA), EntityBIndex = Index(contact.BodyB), PointX = contact.Point.X, PointZ = contact.Point.Y, NormalX = contact.Normal.X, NormalZ = contact.Normal.Y, RelativeSpeed = contact.RelativeSpeed }).ToArray());
    }
}
