using GameCult.Aetheria.State.Verse;
using Ymir.Core;

namespace Aetheria.State.Daemon;

public sealed class AetheriaYmirWorldPhysics : IAetheriaRuntimeWorldPhysics, IDisposable
{
    public const double TractorRadius = 25;
    public const double TractorTraction = 25;
    public const double TractorDistance = 75;

    private const string EntityPrefix = "aetheria.daemon.entity.";
    private const string PickupPrefix = "aetheria.daemon.pickup.";
    private readonly Dictionary<WorldKey, SessionState> _sessions = new();

    public string ImplementationId => "ymir.box3d.retained-session.v1";

    public void RetainWorlds(string runId, IReadOnlyList<int> zoneIndices)
    {
        var retainedZones = (zoneIndices ?? Array.Empty<int>()).ToHashSet();
        foreach (var key in _sessions.Keys
            .Where(key => !string.Equals(key.RunId, runId, StringComparison.Ordinal) ||
                !retainedZones.Contains(key.ZoneIndex))
            .ToArray())
        {
            _sessions[key].Session.Dispose();
            _sessions.Remove(key);
        }
    }

    public AetheriaRuntimeWorldPickupStep ApplyPickupRejection(
        string runId,
        int zoneIndex,
        AetheriaRuntimeWorldBeginContact contact)
    {
        ArgumentNullException.ThrowIfNull(contact);
        if (string.IsNullOrWhiteSpace(contact.FactId))
            throw new ArgumentException("Pickup rejection requires a Ymir Begin fact id.", nameof(contact));
        if (!_sessions.TryGetValue(new WorldKey(runId, zoneIndex), out var state))
            throw new InvalidOperationException($"No retained Ymir session owns run '{runId}' zone {zoneIndex}.");

        var pickupId = PickupPrefix + contact.PickupIndex;
        var entityIndex = contact.EntityAIndex >= 0 ? contact.EntityAIndex : contact.EntityBIndex;
        var stableEntityId = contact.EntityAIndex >= 0 ? contact.EntityAId : contact.EntityBId;
        if (string.IsNullOrWhiteSpace(stableEntityId))
            throw new InvalidOperationException($"Ymir Begin fact '{contact.FactId}' has no stable entity identity.");
        var entityId = EntityPrefix + stableEntityId;
        var bodies = state.Session.Snapshot().Bodies.ToDictionary(body => body.Id, StringComparer.Ordinal);
        if (!bodies.TryGetValue(pickupId, out var pickup) || !bodies.TryGetValue(entityId, out var entity))
            throw new InvalidOperationException($"Ymir Begin fact '{contact.FactId}' references a body outside its retained session.");

        var normalSign = contact.EntityAIndex == entityIndex ? 1.0 : -1.0;
        var normal = Normalize(
            contact.NormalX * normalSign,
            contact.NormalZ * normalSign,
            pickup.Position.X >= entity.Position.X ? 1 : -1,
            0);
        RequireAccepted(state.Session.SetVelocity(new YmirSetBodyVelocityCommand(
            new YmirCommandHeader($"aetheria:fact:{contact.FactId}:pickup-rejection", state.Session.Info.Revision),
            pickupId,
            new Vec2(
                pickup.Velocity.X + (float)(normal.X * 25.0),
                pickup.Velocity.Y + (float)(normal.Y * 25.0)),
            pickup.AngularVelocity)));

        var rejected = state.Session.Snapshot().Bodies.Single(body => body.Id == pickupId);
        return new AetheriaRuntimeWorldPickupStep
        {
            PickupIndex = contact.PickupIndex,
            PositionX = rejected.Position.X,
            PositionZ = rejected.Position.Y,
            VelocityX = rejected.Velocity.X,
            VelocityZ = rejected.Velocity.Y
        };
    }

    public AetheriaRuntimeWorldStep Step(
        string runId,
        long frameId,
        int simulationStepIndex,
        AetheriaRuntimeZoneSnapshotCommit zone,
        IReadOnlyList<AetheriaRuntimeEntitySnapshotCommit> entities,
        double deltaSeconds)
    {
        ArgumentNullException.ThrowIfNull(zone);
        if (string.IsNullOrWhiteSpace(runId))
            throw new ArgumentException("A retained Ymir world requires a run id.", nameof(runId));
        if (deltaSeconds <= 0)
            throw new ArgumentOutOfRangeException(nameof(deltaSeconds));

        var key = new WorldKey(runId, zone.ZoneIndex);
        if (_sessions.TryGetValue(key, out var existing) &&
            existing.LastFrameId == frameId && existing.LastSimulationStepIndex == simulationStepIndex)
            return existing.LastResult!;
        if (existing != null &&
            (frameId < existing.LastFrameId ||
             (frameId == existing.LastFrameId && simulationStepIndex < existing.LastSimulationStepIndex)))
            throw new InvalidOperationException(
                $"Ymir session '{existing.Session.Info.SessionId}' cannot step backward from " +
                $"frame/step {existing.LastFrameId}/{existing.LastSimulationStepIndex} to {frameId}/{simulationStepIndex}.");

        var active = ActiveEntities(entities);
        var pickups = ActivePickups(zone);
        var desiredBodies = BuildBodies(active, pickups);
        var state = existing ?? CreateSession(key, zone, desiredBodies);
        if (existing == null)
            _sessions.Add(key, state);

        var commandOrdinal = 0;
        Reconcile(state.Session, desiredBodies, frameId, simulationStepIndex, ref commandOrdinal);
        ApplyTractorForces(state.Session, active, pickups, frameId, simulationStepIndex, ref commandOrdinal);

        var step = state.Session.Step(new YmirStepSessionCommand(
            Header(state.Session, frameId, simulationStepIndex, "step", "world", ref commandOrdinal),
            (float)deltaSeconds,
            GravityFields(zone)));
        var result = Lower(step, active);
        state.LastFrameId = frameId;
        state.LastSimulationStepIndex = simulationStepIndex;
        state.LastResult = result;
        return result;
    }

    public void Dispose()
    {
        foreach (var state in _sessions.Values)
            state.Session.Dispose();
        _sessions.Clear();
    }

    private static SessionState CreateSession(
        WorldKey key,
        AetheriaRuntimeZoneSnapshotCommit zone,
        IReadOnlyList<PhysicsBody> bodies)
    {
        var sessionId = $"aetheria.run.{key.RunId}.zone.{key.ZoneIndex}";
        return new SessionState(YmirSession.Create(new YmirSessionCreateRequest(
            sessionId,
            bodies,
            (float)zone.SimulationTimeSeconds)));
    }

    private static void Reconcile(
        YmirSession session,
        IReadOnlyList<PhysicsBody> desiredBodies,
        long frameId,
        int simulationStepIndex,
        ref int commandOrdinal)
    {
        var desired = desiredBodies.ToDictionary(body => body.Id, StringComparer.Ordinal);
        var current = session.Snapshot().Bodies.ToDictionary(body => body.Id, StringComparer.Ordinal);

        foreach (var body in current.Values.Where(body => !desired.ContainsKey(body.Id)).OrderBy(body => body.Id, StringComparer.Ordinal))
            RequireAccepted(session.Remove(new YmirRemoveBodyCommand(
                Header(session, frameId, simulationStepIndex, "remove", body.Id, ref commandOrdinal), body.Id)));

        foreach (var body in desired.Values.OrderBy(body => body.Id, StringComparer.Ordinal))
        {
            if (!current.TryGetValue(body.Id, out var present))
            {
                RequireAccepted(session.Spawn(new YmirSpawnBodyCommand(
                    Header(session, frameId, simulationStepIndex, "spawn", body.Id, ref commandOrdinal), body)));
                continue;
            }

            if (present.Radius != body.Radius || present.Mass != body.Mass ||
                present.IsStatic != body.IsStatic || present.Restitution != body.Restitution)
                RequireAccepted(session.Configure(new YmirConfigureBodyCommand(
                    Header(session, frameId, simulationStepIndex, "configure", body.Id, ref commandOrdinal),
                    body.Id, body.Radius, body.Mass, body.IsStatic, body.Restitution)));

            if (present.Direction != body.Direction)
                RequireAccepted(session.Teleport(new YmirTeleportBodyCommand(
                    Header(session, frameId, simulationStepIndex, "direction", body.Id, ref commandOrdinal),
                    body.Id, present.Position, body.Direction ?? new Vec2(0, 1))));

            if (!body.IsStatic && (present.Velocity != body.Velocity || present.AngularVelocity != body.AngularVelocity))
                RequireAccepted(session.SetVelocity(new YmirSetBodyVelocityCommand(
                    Header(session, frameId, simulationStepIndex, "velocity", body.Id, ref commandOrdinal),
                    body.Id, body.Velocity, body.AngularVelocity)));
        }
    }

    private static void ApplyTractorForces(
        YmirSession session,
        IReadOnlyList<AetheriaRuntimeEntitySnapshotCommit> active,
        IReadOnlyList<AetheriaRuntimeDroppedPickupCommit> pickups,
        long frameId,
        int simulationStepIndex,
        ref int commandOrdinal)
    {
        foreach (var actor in active.Where(entity => entity.TractorPower > 0))
        {
            var forward = Normalize(actor.DirectionX, actor.DirectionY, 0, 1);
            foreach (var pickup in pickups)
            {
                var dx = pickup.PositionX - actor.PositionX;
                var dz = pickup.PositionZ - actor.PositionZ;
                var along = dx * forward.X + dz * forward.Y;
                var lateral = Math.Abs(dx * forward.Y - dz * forward.X);
                var distance = Math.Sqrt(dx * dx + dz * dz);
                if (along < 0 || along > TractorDistance || lateral > TractorRadius || distance <= 0.001)
                    continue;

                var force = TractorTraction * actor.TractorPower;
                var pickupId = PickupPrefix + pickup.PickupIndex;
                RequireAccepted(session.ApplyForce(new YmirApplyForceCommand(
                    Header(session, frameId, simulationStepIndex, "tractor", $"{actor.EntityIndex}:{pickup.PickupIndex}", ref commandOrdinal),
                    pickupId,
                    new Vec2((float)(-dx / distance * force), (float)(-dz / distance * force)))));
            }
        }
    }

    private static AetheriaRuntimeWorldStep Lower(
        YmirSessionStepResult step,
        IReadOnlyList<AetheriaRuntimeEntitySnapshotCommit> active)
    {
        var bodies = step.World.Bodies;
        var entitiesByBodyId = active.ToDictionary(BodyId, StringComparer.Ordinal);
        return new AetheriaRuntimeWorldStep(
            bodies.Where(body => body.Id.StartsWith(EntityPrefix, StringComparison.Ordinal))
                .Select(body => new AetheriaRuntimeWorldBodyStep
                {
                    EntityIndex = entitiesByBodyId[body.Id].EntityIndex,
                    PositionX = body.Position.X,
                    PositionZ = body.Position.Y,
                    VelocityX = body.Velocity.X,
                    VelocityY = body.Velocity.Y,
                    DirectionX = body.Direction?.X ?? 0,
                    DirectionY = body.Direction?.Y ?? 1
                }).ToArray(),
            bodies.Where(body => body.Id.StartsWith(PickupPrefix, StringComparison.Ordinal))
                .Select(body => new AetheriaRuntimeWorldPickupStep
                {
                    PickupIndex = ParseIndex(body.Id, PickupPrefix),
                    PositionX = body.Position.X,
                    PositionZ = body.Position.Y,
                    VelocityX = body.Velocity.X,
                    VelocityZ = body.Velocity.Y
                }).ToArray(),
            step.ContactFacts
                .Where(fact => fact.Kind == YmirContactFactKind.Begin &&
                    (fact.BodyA.StartsWith(PickupPrefix, StringComparison.Ordinal) ||
                     fact.BodyB.StartsWith(PickupPrefix, StringComparison.Ordinal)))
                .Select(fact =>
                {
                    entitiesByBodyId.TryGetValue(fact.BodyA, out var entityA);
                    entitiesByBodyId.TryGetValue(fact.BodyB, out var entityB);
                    return new AetheriaRuntimeWorldBeginContact
                    {
                        FactId = fact.FactId,
                        EntityAId = entityA?.EntityId ?? "",
                        EntityBId = entityB?.EntityId ?? "",
                        EntityAIndex = entityA?.EntityIndex ?? -1,
                        EntityBIndex = entityB?.EntityIndex ?? -1,
                        PickupIndex = Math.Max(ParseIndex(fact.BodyA, PickupPrefix), ParseIndex(fact.BodyB, PickupPrefix)),
                        PointX = fact.Point?.X ?? 0,
                        PointZ = fact.Point?.Y ?? 0,
                        NormalX = fact.Normal?.X ?? 0,
                        NormalZ = fact.Normal?.Y ?? 0,
                        RelativeSpeed = fact.RelativeSpeed ?? 0
                    };
                })
                .Where(contact => contact.PickupIndex >= 0 &&
                    (contact.EntityAIndex >= 0 || contact.EntityBIndex >= 0))
                .ToArray());
    }

    private static IReadOnlyList<AetheriaRuntimeEntitySnapshotCommit> ActiveEntities(
        IReadOnlyList<AetheriaRuntimeEntitySnapshotCommit> entities)
    {
        var source = entities ?? Array.Empty<AetheriaRuntimeEntitySnapshotCommit>();
        var attached = source.SelectMany(entity => entity.ChildEntityIndices ?? Array.Empty<int>()).ToHashSet();
        return source.Where(entity => entity.IsActive && !attached.Contains(entity.EntityIndex)).ToArray();
    }

    private static IReadOnlyList<AetheriaRuntimeDroppedPickupCommit> ActivePickups(
        AetheriaRuntimeZoneSnapshotCommit zone) =>
        (zone.DroppedPickups ?? Array.Empty<AetheriaRuntimeDroppedPickupCommit>())
        .Where(pickup => pickup != null && pickup.AgeSeconds < pickup.LifetimeSeconds)
        .ToArray();

    private static IReadOnlyList<PhysicsBody> BuildBodies(
        IReadOnlyList<AetheriaRuntimeEntitySnapshotCommit> active,
        IReadOnlyList<AetheriaRuntimeDroppedPickupCommit> pickups) =>
        active.Select(entity =>
        {
            if (string.IsNullOrWhiteSpace(entity.EntityId))
                throw new InvalidOperationException(
                    $"Entity index {entity.EntityIndex} has no stable runtime identity for retained Ymir ownership.");
            var isStatic = string.Equals(entity.Kind, "station", StringComparison.OrdinalIgnoreCase);
            var direction = Normalize(entity.DirectionX, entity.DirectionY, 0, 1);
            return new PhysicsBody(
                BodyId(entity),
                new Vec2((float)entity.PositionX, (float)entity.PositionZ),
                isStatic ? new Vec2(0, 0) : new Vec2((float)entity.VelocityX, (float)entity.VelocityY),
                isStatic ? 48 : 20,
                1,
                IsStatic: isStatic,
                Restitution: 0.2f,
                Direction: new Vec2((float)direction.X, (float)direction.Y));
        }).Concat(pickups.Select(pickup => new PhysicsBody(
            PickupPrefix + pickup.PickupIndex,
            new Vec2((float)pickup.PositionX, (float)pickup.PositionZ),
            new Vec2((float)pickup.VelocityX, (float)pickup.VelocityZ),
            5,
            1,
            Restitution: 0.2f)))
        .ToArray();

    private static string BodyId(AetheriaRuntimeEntitySnapshotCommit entity) => EntityPrefix + entity.EntityId;

    private static IReadOnlyList<RadialField> GravityFields(AetheriaRuntimeZoneSnapshotCommit zone) =>
        AetheriaRuntimeDaemonRenderQueries.QueryBodyPoses(zone)
        .Select(pose => (Pose: pose, Body: (zone.Bodies ?? Array.Empty<AetheriaRuntimeBodySnapshotCommit>())
            .FirstOrDefault(body => body != null && body.BodyKey == pose.BodyKey)))
        .Where(pair => pair.Body != null && pair.Body.GravityInfluenceRadius > 0 && pair.Body.GravityWellDepth != 0)
        .Select(pair => new RadialField(
            "aetheria.daemon.gravity." + pair.Pose.BodyKey,
            new Vec2((float)pair.Pose.CenterX, (float)pair.Pose.CenterZ),
            (float)pair.Body!.GravityWellDepth,
            (float)pair.Body.GravityInfluenceRadius))
        .ToArray();

    private static YmirCommandHeader Header(
        YmirSession session,
        long frameId,
        int simulationStepIndex,
        string kind,
        string subject,
        ref int ordinal) =>
        new($"aetheria:frame:{frameId}:step:{simulationStepIndex}:{ordinal++}:{kind}:{subject}", session.Info.Revision);

    private static void RequireAccepted(YmirCommandReceipt receipt)
    {
        if (receipt.Outcome != YmirCommandOutcome.Accepted)
            throw new InvalidOperationException(
                $"Ymir rejected Aetheria command '{receipt.CommandId}' with {receipt.Error}.");
    }

    private static int ParseIndex(string id, string prefix) =>
        id.StartsWith(prefix, StringComparison.Ordinal) &&
        int.TryParse(id.AsSpan(prefix.Length), out var value)
            ? value
            : -1;

    private static (double X, double Y) Normalize(
        double x,
        double y,
        double fallbackX,
        double fallbackY)
    {
        var length = Math.Sqrt(x * x + y * y);
        return length <= 0.001 ? (fallbackX, fallbackY) : (x / length, y / length);
    }

    private sealed class SessionState(YmirSession session)
    {
        public YmirSession Session { get; } = session;
        public long LastFrameId { get; set; } = -1;
        public int LastSimulationStepIndex { get; set; } = -1;
        public AetheriaRuntimeWorldStep? LastResult { get; set; }
    }

    private readonly record struct WorldKey(string RunId, int ZoneIndex);
}
