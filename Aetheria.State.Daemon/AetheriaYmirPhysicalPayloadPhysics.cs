using GameCult.Aetheria.State.Verse;
using Ymir.Core;

namespace Aetheria.State.Daemon;

public sealed class AetheriaYmirPhysicalPayloadPhysics : IAetheriaRuntimePhysicalPayloadPhysics
{
    private const string PhysicalPayloadBodyPrefix = "aetheria.physical-payload.";
    private const string EntityBodyPrefix = "aetheria.daemon.entity.";
    private readonly YmirSimulator _simulator;

    public AetheriaYmirPhysicalPayloadPhysics(YmirSimulator? simulator = null)
    {
        _simulator = simulator ?? new YmirSimulator();
    }

    public string AuthorityId => "ymir.core";

    public AetheriaRuntimePhysicalPayloadStep Step(
        AetheriaRuntimeZoneSnapshotCommit zone,
        IReadOnlyList<AetheriaRuntimeEntitySnapshotCommit> entities,
        double deltaSeconds)
    {
        ArgumentNullException.ThrowIfNull(zone);
        if (deltaSeconds <= 0)
            throw new ArgumentOutOfRangeException(nameof(deltaSeconds));

        var entityByBodyId = (entities ?? Array.Empty<AetheriaRuntimeEntitySnapshotCommit>())
            .Where(entity => entity is { IsActive: true })
            .ToDictionary(entity => EntityBodyId(entity.EntityIndex), StringComparer.Ordinal);
        var survivors = new List<AetheriaRuntimePhysicalPayloadCommit>();
        var hits = new List<AetheriaRuntimePhysicalPayloadHit>();

        foreach (var projectile in zone.PhysicalPayloads ?? Array.Empty<AetheriaRuntimePhysicalPayloadCommit>())
        {
            if (projectile is not { Active: true })
                continue;

            var projectileBodyId = PhysicalPayloadBodyId(projectile.PayloadId);
            var bodies = new List<PhysicsBody>
            {
                new(
                    projectileBodyId,
                    new Vec2((float)projectile.PositionX, (float)projectile.PositionZ),
                    new Vec2((float)projectile.VelocityX, (float)projectile.VelocityY),
                    (float)Math.Max(0.001, projectile.Radius),
                    1.0f,
                    IsStatic: projectile.Stationary,
                    Restitution: 0.0f)
            };
            foreach (var pair in entityByBodyId)
            {
                var entity = pair.Value;
                if (entity.EntityIndex == projectile.SourceEntityIndex)
                    continue;
                bodies.Add(new PhysicsBody(
                    pair.Key,
                    new Vec2((float)entity.PositionX, (float)entity.PositionZ),
                    Vec2.Zero,
                    ResolveEntityRadius(entity),
                    1.0f,
                    IsStatic: true,
                    Restitution: 0.0f));
            }

            var result = _simulator.Step(new SimulationStepRequest(
                (float)deltaSeconds,
                new YmirWorld((float)zone.SimulationTimeSeconds, bodies, Array.Empty<RadialField>())));
            var projectileBody = result.World.Bodies.Single(body =>
                string.Equals(body.Id, projectileBodyId, StringComparison.Ordinal));
            projectile.PositionX = projectileBody.Position.X;
            projectile.PositionZ = projectileBody.Position.Y;
            projectile.VelocityX = projectileBody.Velocity.X;
            projectile.VelocityY = projectileBody.Velocity.Y;

            var contact = result.Contacts.FirstOrDefault(candidate =>
                string.Equals(candidate.BodyA, projectileBodyId, StringComparison.Ordinal) ||
                string.Equals(candidate.BodyB, projectileBodyId, StringComparison.Ordinal));
            if (contact == null)
            {
                survivors.Add(projectile);
                continue;
            }

            var targetBodyId = string.Equals(contact.BodyA, projectileBodyId, StringComparison.Ordinal)
                ? contact.BodyB
                : contact.BodyA;
            if (!entityByBodyId.TryGetValue(targetBodyId, out var target))
            {
                survivors.Add(projectile);
                continue;
            }

            var normal = string.Equals(contact.BodyA, projectileBodyId, StringComparison.Ordinal)
                ? contact.Normal
                : new Vec2(-contact.Normal.X, -contact.Normal.Y);
            hits.Add(new AetheriaRuntimePhysicalPayloadHit
            {
                Payload = projectile,
                PhysicalPayloadBodyId = projectileBodyId,
                TargetEntityIndex = target.EntityIndex,
                TargetBodyId = targetBodyId,
                PointX = contact.Point.X,
                PointZ = contact.Point.Y,
                NormalX = normal.X,
                NormalZ = normal.Y
            });
            if (string.Equals(projectile.PayloadKind, "mine", StringComparison.Ordinal))
            {
                projectile.Stationary = true;
                projectile.VelocityX = 0;
                projectile.VelocityY = 0;
                survivors.Add(projectile);
            }
        }

        return new AetheriaRuntimePhysicalPayloadStep(survivors, hits);
    }

    private static string PhysicalPayloadBodyId(string? projectileId) =>
        PhysicalPayloadBodyPrefix + (projectileId ?? "");

    private static string EntityBodyId(int entityIndex) =>
        EntityBodyPrefix + entityIndex;

    private static float ResolveEntityRadius(AetheriaRuntimeEntitySnapshotCommit entity) =>
        string.Equals(entity.Kind, "station", StringComparison.OrdinalIgnoreCase) ? 48.0f : 20.0f;
}
