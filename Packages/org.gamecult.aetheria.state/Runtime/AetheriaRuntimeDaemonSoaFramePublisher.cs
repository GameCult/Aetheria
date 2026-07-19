using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GameCult.Caching;
using GameCult.Eve.Surface;
using GameCult.Mesh;

#nullable enable

namespace GameCult.Aetheria.State.Verse
{
    public sealed class AetheriaRuntimeDaemonSoaFrame : IDisposable
    {
        private CultMeshFrameBodyWriteLease? _write;

        public AetheriaRuntimeDaemonSoaFrame(
            AetheriaRuntimeDaemonSoaViewDocument view,
            CultMeshFrameBodyWriteLease write,
            int byteLength,
            bool publishSharedMemory,
            bool publishNetwork)
        {
            View = view ?? throw new ArgumentNullException(nameof(view));
            _write = write ?? throw new ArgumentNullException(nameof(write));
            ByteLength = byteLength;
            PublishSharedMemory = publishSharedMemory;
            PublishNetwork = publishNetwork;
        }

        public AetheriaRuntimeDaemonSoaViewDocument View { get; }
        public int ByteLength { get; }
        public bool PublishSharedMemory { get; }
        public bool PublishNetwork { get; }
        public ReadOnlySpan<byte> Span => (_write ?? throw new ObjectDisposedException(nameof(AetheriaRuntimeDaemonSoaFrame))).Span[..ByteLength];

        public CultMeshBodyDescriptor Commit(DateTimeOffset nowUtc)
        {
            var write = _write ?? throw new ObjectDisposedException(nameof(AetheriaRuntimeDaemonSoaFrame));
            var descriptor = write.Commit(ByteLength, nowUtc);
            _write = null;
            return descriptor;
        }

        public void Dispose()
        {
            _write?.Dispose();
            _write = null;
        }
    }

    public sealed class AetheriaRuntimeDaemonSoaPublication
    {
        public AetheriaRuntimeDaemonSoaPublication(
            CultMeshBodyPublicationDocument body,
            EveEntitySoaViewDocument view)
        {
            Body = body ?? throw new ArgumentNullException(nameof(body));
            View = view ?? throw new ArgumentNullException(nameof(view));
        }

        public CultMeshBodyPublicationDocument Body { get; }
        public EveEntitySoaViewDocument View { get; }
    }

    public sealed class AetheriaRuntimeDaemonSoaFramePublisher : IDisposable
    {
        public const string BodyId = "eve:entity-soa:aetheria.daemon:pilot";
        public const string ProducerId = "aetheria.daemon";
        public const string BodySchemaId = "gamecult.eve.entity_soa.body.v2";
        public const int LayoutVersion = 3;
        private const int Capacity = 4096;
        private const int EntityRenderGroupId = 1;
        private const int FloatStride = 4;
        private const int Float3Stride = 12;
        private const int IntStride = 4;
        private const int ByteStride = 1;
        private const string EntityProxyMeshAssetKey = "daemon.entity_proxy.mesh";
        private const string EntityProxyMaterialAssetKey = "daemon.entity_proxy.material";
        private const string EntityProxyMeshUri = "cultmesh://aetheria/assets/daemon/entity_proxy/mesh";
        private const string EntityProxyMaterialUri = "cultmesh://aetheria/assets/daemon/entity_proxy/material";

        private readonly CultMeshFrameBodyPublisher _localPublisher;
        private readonly CultMeshNetworkBodyPublisher _networkPublisher;
        private readonly CultMeshBodyDemandTracker? _demand;
        private readonly Dictionary<string, int> _syntheticEntityIndices =
            new Dictionary<string, int>(StringComparer.Ordinal);
        private int _nextSyntheticEntityIndex = -2;

        public AetheriaRuntimeDaemonSoaFramePublisher(
            CultCache cache,
            long producerEpoch,
            CultMeshBodyDemandTracker? demand = null)
        {
            if (cache == null) throw new ArgumentNullException(nameof(cache));
            _localPublisher = new CultMeshFrameBodyPublisher(
                BodyId, BodySchemaId, LayoutVersion, Capacity, producerEpoch,
                checked((int)EntityHotSlabLayout.Create(Capacity).TotalByteLength),
                TimeSpan.FromSeconds(30));
            _networkPublisher = new CultMeshNetworkBodyPublisher(
                cache,
                generation => string.Equals(generation.ProducerId, ProducerId, StringComparison.Ordinal));
            _demand = demand;
        }

        public AetheriaRuntimeDaemonSoaFrame? BuildCurrentZoneEntities(
            AetheriaRuntimeDaemonFrameDocument frame,
            AetheriaRuntimeCatalogSnapshot? catalog = null)
        {
            if (frame == null)
                throw new ArgumentNullException(nameof(frame));
            var demand = _demand?.Plan(BodyId);
            if (demand != null && !demand.HasConsumers)
                return null;
            var publishSharedMemory = demand?.RequiresSharedMemory ?? true;
            var publishNetwork = demand?.RequiresNetwork ?? true;

            var run = frame.Run ?? new AetheriaRuntimeRunCheckpointCommit();
            var zone = (run.Zones ?? Array.Empty<AetheriaRuntimeZoneSnapshotCommit>())
                .FirstOrDefault(candidate => candidate != null && candidate.ZoneIndex == run.CurrentZoneIndex);
            AetheriaRuntimeRunCheckpointCommit.TryParseEntityKey(
                run.CurrentEntityKey,
                out var controlledZoneIndex,
                out var controlledEntityIndex);
            var controlled = controlledZoneIndex == run.CurrentZoneIndex
                ? (zone?.Entities ?? Array.Empty<AetheriaRuntimeEntitySnapshotCommit>())
                    .FirstOrDefault(entity => entity != null && entity.EntityIndex == controlledEntityIndex)
                : null;
            var dockParent = (zone?.Entities ?? Array.Empty<AetheriaRuntimeEntitySnapshotCommit>())
                .FirstOrDefault(entity => entity != null && controlled != null &&
                    (entity.DockingBayAssignments ?? Array.Empty<int>()).Contains(controlled.EntityIndex));
            var visibleEntityIndices = AetheriaRuntimeDaemonRenderQueries
                .QueryEffectiveContacts(zone, controlled?.EntityIndex ?? -1)
                .Where(contact => contact.Contact.Visible)
                .Select(contact => contact.Contact.TargetEntityIndex)
                .Append(controlled?.EntityIndex ?? -1)
                .ToHashSet();
            if (dockParent != null)
                visibleEntityIndices.Add(dockParent.EntityIndex);
            var entities = (zone?.Entities ?? Array.Empty<AetheriaRuntimeEntitySnapshotCommit>())
                .Where(entity => entity != null && visibleEntityIndices.Contains(entity.EntityIndex))
                .OrderBy(entity => entity.EntityIndex)
                .ToArray();
            var pickups = (zone?.DroppedPickups ?? Array.Empty<AetheriaRuntimeDroppedPickupCommit>())
                .Where(pickup => pickup != null)
                .OrderBy(pickup => pickup.PickupIndex)
                .ToArray();
            var payloads = (zone?.PhysicalPayloads ?? Array.Empty<AetheriaRuntimePhysicalPayloadCommit>())
                .Where(payload => payload != null && payload.Active)
                .OrderBy(payload => payload.PayloadId, StringComparer.Ordinal)
                .ToArray();
            var celestialBodies = BuildCelestialBodies(frame, zone);
            var asteroidInstances = BuildAsteroidInstances(frame, zone);
            var unsupportedPayload = payloads.FirstOrDefault(payload =>
                !string.Equals(payload.PayloadKind, "mine", StringComparison.Ordinal));
            if (unsupportedPayload != null)
                throw new InvalidOperationException(
                    $"Physical payload '{unsupportedPayload.PayloadId}' uses unsupported provider asset kind '{unsupportedPayload.PayloadKind}'.");
            var pickupEntityIds = pickups
                .Select(pickup => $"pickup:{run.CurrentZoneIndex}:{pickup.PickupIndex}")
                .ToArray();
            var payloadEntityIds = payloads
                .Select(payload => $"{run.RunId}:zone:{run.CurrentZoneIndex}:physical-payload:{payload.PayloadId}")
                .ToArray();
            var syntheticEntityIds = pickupEntityIds
                .Concat(payloadEntityIds)
                .Concat(celestialBodies.Select(value => value.EntityId))
                .Concat(asteroidInstances.Select(value => value.EntityId))
                .ToArray();
            if (syntheticEntityIds.Distinct(StringComparer.Ordinal).Count() != syntheticEntityIds.Length)
                throw new InvalidOperationException("Current-zone synthetic SoA entity identities are not unique.");
            var syntheticEntityIndices = syntheticEntityIds.ToDictionary(
                entityId => entityId,
                GetOrAllocateSyntheticEntityIndex,
                StringComparer.Ordinal);
            var count = entities.Length + pickups.Length + payloads.Length +
                celestialBodies.Count + asteroidInstances.Count;
            if (count > Capacity)
                throw new InvalidOperationException($"Aetheria entity SoA capacity {Capacity} was exceeded by {count} rows.");
            var generation = Math.Max(frame.FrameId, 0);
            var layout = EntityHotSlabLayout.Create(count);
            if (!_localPublisher.TryAcquireWrite(out var write))
                throw new InvalidOperationException("CultMesh has no unleased frame slot for the Aetheria SoA generation.");
            try
            {
                var bytes = write.Span[..checked((int)layout.TotalByteLength)];
                bytes.Clear();
                WriteEntities(bytes, layout, entities);
                WritePickups(bytes, layout, pickups, pickupEntityIds, syntheticEntityIndices, entities.Length);
                WritePayloads(bytes, layout, payloads, payloadEntityIds, syntheticEntityIndices, entities.Length + pickups.Length);
                WriteCelestialBodies(
                    bytes,
                    layout,
                    celestialBodies,
                    syntheticEntityIndices,
                    entities.Length + pickups.Length + payloads.Length);
                WriteAsteroidInstances(
                    bytes,
                    layout,
                    asteroidInstances,
                    syntheticEntityIndices,
                    entities.Length + pickups.Length + payloads.Length + celestialBodies.Count);

            var view = AetheriaRuntimeDaemonSoaViewDocument.Create(
                string.IsNullOrWhiteSpace(frame.DaemonId) ? "aetheria-daemon" : frame.DaemonId,
                string.IsNullOrWhiteSpace(frame.SessionId) ? "local" : frame.SessionId,
                frame.FrameId,
                generation,
                new[]
                {
                    new AetheriaRuntimeDaemonSoaBufferDocument
                    {
                        BufferId = BodyId,
                        DisplayName = "Current zone daemon entity hot slab",
                        ByteOffset = 0,
                        ByteLength = layout.TotalByteLength,
                        Generation = generation,
                        DaemonWritable = true,
                        ObserverWritable = false
                    }
                },
                layout.CreateColumns(count),
                layout.CreateDirtyRanges(count, generation),
                backend: AetheriaRuntimeDaemonSoaBackends.CultMesh,
                synchronizationMode: AetheriaRuntimeDaemonSoaSynchronizationModes.ImmutableFrame,
                renderGroups: CreateRenderGroups(entities, pickups, payloads, celestialBodies, asteroidInstances),
                identities: entities.Select(entity => new AetheriaRuntimeDaemonSoaIdentityDocument
                    {
                        EntityIndex = entity.EntityIndex,
                        EntityId = entity.EntityId,
                        Kind = entity.Kind,
                        Label = entity.Name,
                        Faction = entity.FactionKey,
                        Selectable = entity.EntityIndex != controlled?.EntityIndex,
                        Controllable = entity.EntityIndex == controlled?.EntityIndex,
                        AssetRef = AetheriaRuntimeAssets.ResolveEntityPrefabAssetRef(entity, catalog)
                    })
                    .Concat(pickups.Select((pickup, pickupRow) => new AetheriaRuntimeDaemonSoaIdentityDocument
                    {
                        EntityIndex = syntheticEntityIndices[pickupEntityIds[pickupRow]],
                        EntityId = pickupEntityIds[pickupRow],
                        Kind = "pickup",
                        Label = string.IsNullOrWhiteSpace(pickup.Item?.ItemKey) ? "Pickup" : pickup.Item.ItemKey,
                        Faction = "",
                        Selectable = true,
                        Controllable = false,
                        AssetRef = "prefab.entity.pickup"
                    }))
                    .Concat(payloads.Select((payload, payloadRow) => new AetheriaRuntimeDaemonSoaIdentityDocument
                    {
                        EntityIndex = syntheticEntityIndices[payloadEntityIds[payloadRow]],
                        EntityId = payloadEntityIds[payloadRow],
                        Kind = "physical-payload",
                        Label = string.IsNullOrWhiteSpace(payload.PayloadKind) ? "Physical payload" : payload.PayloadKind,
                        Faction = payload.FactionKey ?? "",
                        Selectable = false,
                        Controllable = false,
                        AssetRef = "prefab.entity.mine"
                    }))
                    .Concat(celestialBodies.Select(value => new AetheriaRuntimeDaemonSoaIdentityDocument
                    {
                        EntityIndex = syntheticEntityIndices[value.EntityId],
                        EntityId = value.EntityId,
                        Kind = value.EntityKind,
                        Label = value.Label,
                        Faction = "",
                        Selectable = false,
                        Controllable = false,
                        AssetRef = value.AssetRef
                    }))
                    .Concat(asteroidInstances.Select(value => new AetheriaRuntimeDaemonSoaIdentityDocument
                    {
                        EntityIndex = syntheticEntityIndices[value.EntityId],
                        EntityId = value.EntityId,
                        Kind = "celestial.asteroid",
                        Label = "Asteroid",
                        Faction = "",
                        Selectable = false,
                        Controllable = false,
                        AssetRef = "prefab.body.asteroid"
                    }))
                    .ToArray());

                return new AetheriaRuntimeDaemonSoaFrame(
                    view,
                    write,
                    checked((int)layout.TotalByteLength),
                    publishSharedMemory,
                    publishNetwork);
            }
            catch
            {
                write.Dispose();
                throw;
            }
        }

        public async Task<AetheriaRuntimeDaemonSoaPublication> PublishAsync(AetheriaRuntimeDaemonSoaFrame frame)
        {
            if (frame == null) throw new ArgumentNullException(nameof(frame));
            var now = DateTimeOffset.UtcNow;
            var networkBytes = frame.PublishNetwork ? frame.Span.ToArray() : null;
            var local = frame.Commit(now);
            var generation = new CultMeshBodyGeneration
            {
                BodyId = BodyId,
                ProducerId = ProducerId,
                SchemaId = BodySchemaId,
                LayoutVersion = LayoutVersion,
                Capacity = Capacity,
                ProducerEpoch = local.ProducerEpoch,
                Sequence = local.Sequence,
                Synchronization = local.Synchronization,
                LeaseExpiresAtUnixMs = local.LeaseExpiresAtUnixMs
            };
            var representations = new List<CultMeshBodyDescriptor>();
            if (frame.PublishSharedMemory) representations.Add(local);
            if (frame.PublishNetwork)
                representations.Add(await _networkPublisher.PublishAsync(generation, networkBytes!).ConfigureAwait(false));
            var publication = new CultMeshBodyPublicationDocument
            {
                BodyId = BodyId,
                ProducerId = ProducerId,
                SchemaId = BodySchemaId,
                LayoutVersion = LayoutVersion,
                ByteSize = local.ByteSize,
                Capacity = Capacity,
                ProducerEpoch = local.ProducerEpoch,
                Sequence = local.Sequence,
                Synchronization = local.Synchronization,
                LivenessExpiresAtUnixMs = local.LeaseExpiresAtUnixMs,
                Representations = representations.ToArray()
            };
            new CultMeshBodyPublicationHandle(BodyId, publication.ProducerEpoch, publication.Sequence)
                .Validate(publication);
            var view = AetheriaRuntimeEveEntitySoaProjection.Project(frame.View, generation);
            if (view.Buffers.Length != 1 || !string.Equals(view.Buffers[0].BufferId, publication.BodyId, StringComparison.Ordinal) ||
                view.Columns.Any(column => !string.Equals(column.BufferId, publication.BodyId, StringComparison.Ordinal)))
                throw new InvalidOperationException("Aetheria Eve SoA layout does not reference its published logical body identity.");
            return new AetheriaRuntimeDaemonSoaPublication(publication, view);
        }

        public void Dispose() => _localPublisher.Dispose();

        private static IReadOnlyList<AetheriaRuntimeDaemonRenderGroupDocument> CreateRenderGroups(
            IReadOnlyList<AetheriaRuntimeEntitySnapshotCommit> entities,
            IReadOnlyList<AetheriaRuntimeDroppedPickupCommit> pickups,
            IReadOnlyList<AetheriaRuntimePhysicalPayloadCommit> payloads,
            IReadOnlyList<CelestialBodyPresentation> celestialBodies,
            IReadOnlyList<AsteroidPresentation> asteroidInstances)
        {
            if (entities.Count == 0 && pickups.Count == 0 && payloads.Count == 0 &&
                celestialBodies.Count == 0 && asteroidInstances.Count == 0)
                return Array.Empty<AetheriaRuntimeDaemonRenderGroupDocument>();

            var positions = entities.Select(entity => (entity.PositionX, entity.PositionY, entity.PositionZ))
                .Concat(pickups.Select(pickup => (pickup.PositionX, pickup.PositionY, pickup.PositionZ)))
                .Concat(payloads.Select(payload => (payload.PositionX, PositionY: payload.PositionY, payload.PositionZ)))
                .Concat(celestialBodies.Select(value => (value.PositionX, value.PositionY, value.PositionZ)))
                .Concat(asteroidInstances.Select(value => (value.PositionX, value.PositionY, value.PositionZ)))
                .ToArray();
            var minX = positions.Min(position => (float)position.PositionX);
            var minY = positions.Min(position => (float)position.PositionY);
            var minZ = positions.Min(position => (float)position.PositionZ);
            var maxX = positions.Max(position => (float)position.PositionX);
            var maxY = positions.Max(position => (float)position.PositionY);
            var maxZ = positions.Max(position => (float)position.PositionZ);
            const float boundsCell = 1024.0f;
            var boundsMinX = MathF.Floor((minX - 16.0f) / boundsCell) * boundsCell;
            var boundsMinY = MathF.Floor((minY - 16.0f) / boundsCell) * boundsCell;
            var boundsMinZ = MathF.Floor((minZ - 16.0f) / boundsCell) * boundsCell;
            var boundsMaxX = MathF.Ceiling((maxX + 16.0f) / boundsCell) * boundsCell;
            var boundsMaxY = MathF.Ceiling((maxY + 16.0f) / boundsCell) * boundsCell;
            var boundsMaxZ = MathF.Ceiling((maxZ + 16.0f) / boundsCell) * boundsCell;

            return new[]
            {
                new AetheriaRuntimeDaemonRenderGroupDocument
                {
                    GroupId = EntityRenderGroupId,
                    MeshKey = EntityProxyMeshUri,
                    MaterialKey = EntityProxyMaterialUri,
                    MeshAsset = AetheriaRuntimeAssetRef.FromKey(
                        EntityProxyMeshAssetKey,
                        AetheriaRuntimeAssetKinds.Mesh,
                        EntityProxyMeshUri,
                        AetheriaRuntimeAssetTransports.CultMesh),
                    MaterialAsset = AetheriaRuntimeAssetRef.FromKey(
                        EntityProxyMaterialAssetKey,
                        AetheriaRuntimeAssetKinds.Material,
                        EntityProxyMaterialUri,
                        AetheriaRuntimeAssetTransports.CultMesh),
                    SubMeshIndex = 0,
                    Layer = 0,
                    ShaderKey = "aetheria.daemon.entity-proxy",
                    DisplayName = "Daemon current-zone entities",
                    InstanceCount = entities.Count + pickups.Count + payloads.Count +
                        celestialBodies.Count + asteroidInstances.Count,
                    BoundsCenterX = (boundsMinX + boundsMaxX) * 0.5f,
                    BoundsCenterY = (boundsMinY + boundsMaxY) * 0.5f,
                    BoundsCenterZ = (boundsMinZ + boundsMaxZ) * 0.5f,
                    BoundsSizeX = Math.Max(boundsMaxX - boundsMinX, boundsCell),
                    BoundsSizeY = Math.Max(boundsMaxY - boundsMinY, boundsCell),
                    BoundsSizeZ = Math.Max(boundsMaxZ - boundsMinZ, boundsCell),
                    ShadowMode = AetheriaRuntimeDaemonRenderShadowModes.On,
                    ReceiveShadows = true,
                    DefaultScale = 1.0f,
                    Lod = -1
                }
            };
        }

        private static void WriteEntities(
            Span<byte> bytes,
            EntityHotSlabLayout layout,
            IReadOnlyList<AetheriaRuntimeEntitySnapshotCommit> entities)
        {
            for (var index = 0; index < entities.Count; index++)
            {
                var entity = entities[index];
                WriteInt32(bytes, layout.EntityIndex + index * IntStride, entity.EntityIndex);
                WriteInt32(bytes, layout.CargoQuantity + index * IntStride, AetheriaRuntimeCargoCapacityQueries.Quantity(entity));
                WriteFloat(bytes, layout.BeamPower, index, entity.TractorPower);
                WriteFloat3(bytes, layout.Position, index, entity.PositionX, entity.PositionY, entity.PositionZ);
                WriteFloat(bytes, layout.RotationRadians, index, Math.Atan2(entity.DirectionX, entity.DirectionY));
                WriteFloat3(bytes, layout.Velocity, index, entity.VelocityX, 0.0, entity.VelocityY);
                WriteFloat(bytes, layout.PhysicsBodyRadius, index, 1.0);
                WriteFloat(bytes, layout.PhysicsBodyMass, index, 1.0);
                WriteFloat(bytes, layout.PhysicsBodyInverseMass, index, 1.0);
                WriteFloat(bytes, layout.RenderScale, index, 1.0);
                bytes[checked((int)(layout.RenderVisibility + index * ByteStride))] = (byte)(entity.IsActive ? 1 : 0);
                WriteInt32(bytes, layout.RenderLod + index * IntStride, 0);
                WriteUInt32(bytes, layout.RenderGroupId + index * IntStride, EntityRenderGroupId);
            }
        }

        private static void WritePickups(
            Span<byte> bytes,
            EntityHotSlabLayout layout,
            IReadOnlyList<AetheriaRuntimeDroppedPickupCommit> pickups,
            IReadOnlyList<string> pickupEntityIds,
            IReadOnlyDictionary<string, int> syntheticEntityIndices,
            int rowOffset)
        {
            for (var pickupRow = 0; pickupRow < pickups.Count; pickupRow++)
            {
                var pickup = pickups[pickupRow];
                var row = rowOffset + pickupRow;
                WriteInt32(bytes, layout.EntityIndex + row * IntStride, syntheticEntityIndices[pickupEntityIds[pickupRow]]);
                WriteInt32(bytes, layout.CargoQuantity + row * IntStride, 0);
                WriteFloat3(bytes, layout.Position, row, pickup.PositionX, pickup.PositionY, pickup.PositionZ);
                WriteFloat(bytes, layout.RotationRadians, row, 0.0);
                WriteFloat3(bytes, layout.Velocity, row, pickup.VelocityX, pickup.VelocityY, pickup.VelocityZ);
                WriteFloat(bytes, layout.PhysicsBodyRadius, row, 0.5);
                WriteFloat(bytes, layout.PhysicsBodyMass, row, 0.25);
                WriteFloat(bytes, layout.PhysicsBodyInverseMass, row, 4.0);
                WriteFloat(bytes, layout.RenderScale, row, 1.0);
                bytes[checked((int)(layout.RenderVisibility + row * ByteStride))] = 1;
                WriteInt32(bytes, layout.RenderLod + row * IntStride, 0);
                WriteUInt32(bytes, layout.RenderGroupId + row * IntStride, EntityRenderGroupId);
            }
        }

        private static void WritePayloads(
            Span<byte> bytes,
            EntityHotSlabLayout layout,
            IReadOnlyList<AetheriaRuntimePhysicalPayloadCommit> payloads,
            IReadOnlyList<string> payloadEntityIds,
            IReadOnlyDictionary<string, int> syntheticEntityIndices,
            int rowOffset)
        {
            for (var payloadRow = 0; payloadRow < payloads.Count; payloadRow++)
            {
                var payload = payloads[payloadRow];
                var row = rowOffset + payloadRow;
                WriteInt32(bytes, layout.EntityIndex + row * IntStride, syntheticEntityIndices[payloadEntityIds[payloadRow]]);
                WriteInt32(bytes, layout.CargoQuantity + row * IntStride, 0);
                WriteFloat3(bytes, layout.Position, row, payload.PositionX, payload.PositionY, payload.PositionZ);
                WriteFloat(bytes, layout.RotationRadians, row, Math.Atan2(payload.DirectionX, payload.DirectionY));
                WriteFloat3(bytes, layout.Velocity, row, payload.VelocityX, 0.0, payload.VelocityY);
                WriteFloat(bytes, layout.PhysicsBodyRadius, row, Math.Max(0.01, payload.Radius));
                WriteFloat(bytes, layout.PhysicsBodyMass, row, payload.Stationary ? 0.0 : 1.0);
                WriteFloat(bytes, layout.PhysicsBodyInverseMass, row, payload.Stationary ? 0.0 : 1.0);
                WriteFloat(bytes, layout.RenderScale, row, 1.0);
                bytes[checked((int)(layout.RenderVisibility + row * ByteStride))] = 1;
                WriteInt32(bytes, layout.RenderLod + row * IntStride, 0);
                WriteUInt32(bytes, layout.RenderGroupId + row * IntStride, EntityRenderGroupId);
            }
        }

        private static IReadOnlyList<CelestialBodyPresentation> BuildCelestialBodies(
            AetheriaRuntimeDaemonFrameDocument frame,
            AetheriaRuntimeZoneSnapshotCommit? zone)
        {
            if (zone == null)
                return Array.Empty<CelestialBodyPresentation>();

            var poses = AetheriaRuntimeDaemonRenderQueries.QueryBodyPoses(zone)
                .ToDictionary(pose => pose.BodyKey, StringComparer.Ordinal);
            var values = new List<CelestialBodyPresentation>();
            foreach (var body in zone.Bodies ?? Array.Empty<AetheriaRuntimeBodySnapshotCommit>())
            {
                if (body == null ||
                    string.Equals(body.Kind, "asteroid_belt", StringComparison.OrdinalIgnoreCase) ||
                    string.IsNullOrWhiteSpace(body.BodyKey) ||
                    !poses.TryGetValue(body.BodyKey, out var pose))
                    continue;

                var scale = Math.Max(
                    0.1,
                    frame.RenderSettings.ResolveBodyRadius(body.Mass) * Math.Max(0, body.BodyRadiusMultiplier));
                var terrainHeight = AetheriaRuntimeDaemonRenderQueries.EvaluateGravityTerrainHeight(
                    zone,
                    pose.CenterX,
                    pose.CenterZ,
                    frame.SimulationTimeSeconds);
                var kind = NormalizeCelestialKind(body.Kind);
                values.Add(new CelestialBodyPresentation(
                    $"{frame.Run.RunId}:zone:{zone.ZoneIndex}:body:{body.BodyKey}",
                    kind,
                    string.IsNullOrWhiteSpace(body.Name) ? body.BodyKey : body.Name,
                    ResolveCelestialAssetRef(kind),
                    pose.CenterX,
                    terrainHeight + scale * 2.0,
                    pose.CenterZ,
                    frame.SimulationTimeSeconds * frame.RenderSettings.PlanetRotationSpeed * Math.PI / 180.0,
                    scale));
            }
            return values;
        }

        private static IReadOnlyList<AsteroidPresentation> BuildAsteroidInstances(
            AetheriaRuntimeDaemonFrameDocument frame,
            AetheriaRuntimeZoneSnapshotCommit? zone)
        {
            if (zone == null)
                return Array.Empty<AsteroidPresentation>();

            var values = new List<AsteroidPresentation>();
            foreach (var belt in zone.Bodies ?? Array.Empty<AetheriaRuntimeBodySnapshotCommit>())
            {
                if (belt == null ||
                    !string.Equals(belt.Kind, "asteroid_belt", StringComparison.OrdinalIgnoreCase) ||
                    string.IsNullOrWhiteSpace(belt.BodyKey))
                    continue;

                foreach (var pose in AetheriaRuntimeDaemonRenderQueries.QueryAsteroidInstancePoses(
                             zone,
                             belt.BodyKey,
                             frame.SimulationTimeSeconds))
                {
                    if (pose.Size <= 0)
                        continue;
                    var terrainHeight = AetheriaRuntimeDaemonRenderQueries.EvaluateGravityTerrainHeight(
                        zone,
                        pose.PositionX,
                        pose.PositionZ,
                        frame.SimulationTimeSeconds);
                    values.Add(new AsteroidPresentation(
                        $"{frame.Run.RunId}:zone:{zone.ZoneIndex}:asteroid:{belt.BodyKey}:{pose.AsteroidIndex}",
                        pose.PositionX,
                        terrainHeight + frame.RenderSettings.AsteroidVerticalOffset,
                        pose.PositionZ,
                        pose.Rotation,
                        pose.Size));
                }
            }
            return values;
        }

        private static string NormalizeCelestialKind(string? kind)
        {
            var normalized = (kind ?? "").Trim().ToLowerInvariant().Replace('_', '-');
            return normalized switch
            {
                "sun" => "sun",
                "gas-giant" => "gas-giant",
                _ => "planet"
            };
        }

        private static string ResolveCelestialAssetRef(string kind)
        {
            return kind switch
            {
                "sun" => "prefab.body.sun",
                "gas-giant" => "prefab.body.gas-giant",
                _ => "prefab.body.planet"
            };
        }

        private static void WriteCelestialBodies(
            Span<byte> bytes,
            EntityHotSlabLayout layout,
            IReadOnlyList<CelestialBodyPresentation> values,
            IReadOnlyDictionary<string, int> syntheticEntityIndices,
            int rowOffset)
        {
            for (var valueIndex = 0; valueIndex < values.Count; valueIndex++)
            {
                var value = values[valueIndex];
                var row = rowOffset + valueIndex;
                WritePresentationRow(
                    bytes,
                    layout,
                    row,
                    syntheticEntityIndices[value.EntityId],
                    value.PositionX,
                    value.PositionY,
                    value.PositionZ,
                    value.RotationRadians,
                    value.Scale);
            }
        }

        private static void WriteAsteroidInstances(
            Span<byte> bytes,
            EntityHotSlabLayout layout,
            IReadOnlyList<AsteroidPresentation> values,
            IReadOnlyDictionary<string, int> syntheticEntityIndices,
            int rowOffset)
        {
            for (var valueIndex = 0; valueIndex < values.Count; valueIndex++)
            {
                var value = values[valueIndex];
                var row = rowOffset + valueIndex;
                WritePresentationRow(
                    bytes,
                    layout,
                    row,
                    syntheticEntityIndices[value.EntityId],
                    value.PositionX,
                    value.PositionY,
                    value.PositionZ,
                    value.RotationRadians,
                    value.Scale);
            }
        }

        private static void WritePresentationRow(
            Span<byte> bytes,
            EntityHotSlabLayout layout,
            int row,
            int entityIndex,
            double positionX,
            double positionY,
            double positionZ,
            double rotationRadians,
            double scale)
        {
            WriteInt32(bytes, layout.EntityIndex + row * IntStride, entityIndex);
            WriteInt32(bytes, layout.CargoQuantity + row * IntStride, 0);
            WriteFloat3(bytes, layout.Position, row, positionX, positionY, positionZ);
            WriteFloat(bytes, layout.RotationRadians, row, rotationRadians);
            WriteFloat3(bytes, layout.Velocity, row, 0, 0, 0);
            WriteFloat(bytes, layout.PhysicsBodyRadius, row, Math.Max(0.01, scale));
            WriteFloat(bytes, layout.PhysicsBodyMass, row, 0);
            WriteFloat(bytes, layout.PhysicsBodyInverseMass, row, 0);
            WriteFloat(bytes, layout.RenderScale, row, Math.Max(0.01, scale));
            bytes[checked((int)(layout.RenderVisibility + row * ByteStride))] = 1;
            WriteInt32(bytes, layout.RenderLod + row * IntStride, 0);
            WriteUInt32(bytes, layout.RenderGroupId + row * IntStride, EntityRenderGroupId);
        }

        private int GetOrAllocateSyntheticEntityIndex(string entityId)
        {
            if (_syntheticEntityIndices.TryGetValue(entityId, out var existing))
                return existing;
            if (_nextSyntheticEntityIndex == int.MinValue)
                throw new InvalidOperationException("Aetheria SoA synthetic entity index space is exhausted.");

            var allocated = _nextSyntheticEntityIndex--;
            _syntheticEntityIndices.Add(entityId, allocated);
            return allocated;
        }

        private sealed class CelestialBodyPresentation
        {
            public CelestialBodyPresentation(
                string entityId,
                string kind,
                string label,
                string assetRef,
                double positionX,
                double positionY,
                double positionZ,
                double rotationRadians,
                double scale)
            {
                EntityId = entityId;
                EntityKind = "celestial." + kind;
                Label = label;
                AssetRef = assetRef;
                PositionX = positionX;
                PositionY = positionY;
                PositionZ = positionZ;
                RotationRadians = rotationRadians;
                Scale = scale;
            }

            public string EntityId { get; }
            public string EntityKind { get; }
            public string Label { get; }
            public string AssetRef { get; }
            public double PositionX { get; }
            public double PositionY { get; }
            public double PositionZ { get; }
            public double RotationRadians { get; }
            public double Scale { get; }
        }

        private sealed class AsteroidPresentation
        {
            public AsteroidPresentation(
                string entityId,
                double positionX,
                double positionY,
                double positionZ,
                double rotationRadians,
                double scale)
            {
                EntityId = entityId;
                PositionX = positionX;
                PositionY = positionY;
                PositionZ = positionZ;
                RotationRadians = rotationRadians;
                Scale = scale;
            }

            public string EntityId { get; }
            public double PositionX { get; }
            public double PositionY { get; }
            public double PositionZ { get; }
            public double RotationRadians { get; }
            public double Scale { get; }
        }

        private static void WriteFloat(Span<byte> bytes, long byteOffset, int index, double value)
        {
            var scalar = IsFinite(value) ? (float)value : 0.0f;
            BinaryPrimitives.WriteInt32LittleEndian(
                bytes.Slice(checked((int)(byteOffset + index * FloatStride)), FloatStride),
                BitConverter.SingleToInt32Bits(scalar));
        }

        private static void WriteFloat3(
            Span<byte> bytes,
            long byteOffset,
            int index,
            double x,
            double y,
            double z)
        {
            var elementOffset = byteOffset + index * Float3Stride;
            WriteFloat(bytes, elementOffset, 0, x);
            WriteFloat(bytes, elementOffset + FloatStride, 0, y);
            WriteFloat(bytes, elementOffset + FloatStride * 2, 0, z);
        }

        private static void WriteInt32(Span<byte> bytes, long offset, int value) =>
            BinaryPrimitives.WriteInt32LittleEndian(bytes.Slice(checked((int)offset), IntStride), value);

        private static void WriteUInt32(Span<byte> bytes, long offset, int value) =>
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.Slice(checked((int)offset), IntStride), (uint)value);

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private readonly struct EntityHotSlabLayout
        {
            private EntityHotSlabLayout(
                long entityIndex,
                long cargoQuantity,
                long beamPower,
                long position,
                long rotationRadians,
                long velocity,
                long physicsBodyRadius,
                long physicsBodyMass,
                long physicsBodyInverseMass,
                long renderScale,
                long renderVisibility,
                long renderLod,
                long renderGroupId,
                long totalByteLength)
            {
                EntityIndex = entityIndex;
                CargoQuantity = cargoQuantity;
                BeamPower = beamPower;
                Position = position;
                RotationRadians = rotationRadians;
                Velocity = velocity;
                PhysicsBodyRadius = physicsBodyRadius;
                PhysicsBodyMass = physicsBodyMass;
                PhysicsBodyInverseMass = physicsBodyInverseMass;
                RenderScale = renderScale;
                RenderVisibility = renderVisibility;
                RenderLod = renderLod;
                RenderGroupId = renderGroupId;
                TotalByteLength = totalByteLength;
            }

            public long EntityIndex { get; }
            public long CargoQuantity { get; }
            public long BeamPower { get; }
            public long Position { get; }
            public long RotationRadians { get; }
            public long Velocity { get; }
            public long PhysicsBodyRadius { get; }
            public long PhysicsBodyMass { get; }
            public long PhysicsBodyInverseMass { get; }
            public long RenderScale { get; }
            public long RenderVisibility { get; }
            public long RenderLod { get; }
            public long RenderGroupId { get; }
            public long TotalByteLength { get; }

            public static EntityHotSlabLayout Create(int count)
            {
                count = Math.Max(0, count);
                var offset = 0L;
                var entityIndex = Take(ref offset, count, IntStride);
                var cargoQuantity = Take(ref offset, count, IntStride);
                var beamPower = Take(ref offset, count, FloatStride);
                var position = Take(ref offset, count, Float3Stride);
                var rotationRadians = Take(ref offset, count, FloatStride);
                var velocity = Take(ref offset, count, Float3Stride);
                var physicsBodyRadius = Take(ref offset, count, FloatStride);
                var physicsBodyMass = Take(ref offset, count, FloatStride);
                var physicsBodyInverseMass = Take(ref offset, count, FloatStride);
                var renderScale = Take(ref offset, count, FloatStride);
                var renderVisibility = Take(ref offset, count, ByteStride);
                offset = Align4(offset);
                var renderLod = Take(ref offset, count, IntStride);
                var renderGroupId = Take(ref offset, count, IntStride);
                return new EntityHotSlabLayout(
                    entityIndex,
                    cargoQuantity,
                    beamPower,
                    position,
                    rotationRadians,
                    velocity,
                    physicsBodyRadius,
                    physicsBodyMass,
                    physicsBodyInverseMass,
                    renderScale,
                    renderVisibility,
                    renderLod,
                    renderGroupId,
                    Math.Max(1, offset));
            }

            public AetheriaRuntimeDaemonSoaColumnDocument[] CreateColumns(int count)
            {
                return new[]
                {
                    Column("entity-index", AetheriaRuntimeDaemonSoaColumnKinds.EntityIndex, "int32", EntityIndex, IntStride, count, "index", "world"),
                    Column("cargo-quantity", AetheriaRuntimeDaemonSoaColumnKinds.CargoQuantity, "int32", CargoQuantity, IntStride, count, "items", "entity"),
                    Column("beam-power", AetheriaRuntimeDaemonSoaColumnKinds.BeamPower, "float32", BeamPower, FloatStride, count, "normalized", "entity"),
                    Column("position", AetheriaRuntimeDaemonSoaColumnKinds.Position, "float3", Position, Float3Stride, count, "world_units", "world"),
                    Column("rotation-radians", AetheriaRuntimeDaemonSoaColumnKinds.RotationRadians, "float32", RotationRadians, FloatStride, count, "radians", "world"),
                    Column("velocity", AetheriaRuntimeDaemonSoaColumnKinds.Velocity, "float3", Velocity, Float3Stride, count, "world_units_per_second", "world"),
                    Column("physics-body-radius", AetheriaRuntimeDaemonSoaColumnKinds.PhysicsBodyRadius, "float32", PhysicsBodyRadius, FloatStride, count, "world_units", "world"),
                    Column("physics-body-mass", AetheriaRuntimeDaemonSoaColumnKinds.PhysicsBodyMass, "float32", PhysicsBodyMass, FloatStride, count, "mass_units", "world"),
                    Column("physics-body-inverse-mass", AetheriaRuntimeDaemonSoaColumnKinds.PhysicsBodyInverseMass, "float32", PhysicsBodyInverseMass, FloatStride, count, "inverse_mass_units", "world"),
                    Column("render-scale", AetheriaRuntimeDaemonSoaColumnKinds.RenderScale, "float32", RenderScale, FloatStride, count, "scale", "world"),
                    Column("render-visibility", AetheriaRuntimeDaemonSoaColumnKinds.RenderVisibility, "uint8", RenderVisibility, ByteStride, count, "bool", "world"),
                    Column("render-lod", AetheriaRuntimeDaemonSoaColumnKinds.RenderLod, "int32", RenderLod, IntStride, count, "lod", "world"),
                    Column("render-group-id", AetheriaRuntimeDaemonSoaColumnKinds.RenderGroupId, "uint32", RenderGroupId, IntStride, count, "id", "world")
                };
            }

            public AetheriaRuntimeDaemonSoaDirtyRangeDocument[] CreateDirtyRanges(int count, long generation)
            {
                return CreateColumns(count)
                    .Select(column => new AetheriaRuntimeDaemonSoaDirtyRangeDocument
                    {
                        ColumnId = column.ColumnId,
                        StartIndex = 0,
                        Count = count,
                        Generation = generation
                    })
                    .ToArray();
            }

            private static AetheriaRuntimeDaemonSoaColumnDocument Column(
                string columnId,
                string kind,
                string scalarType,
                long byteOffset,
                int stride,
                int count,
                string unit,
                string coordinateSpace)
            {
                return new AetheriaRuntimeDaemonSoaColumnDocument
                {
                    ColumnId = columnId,
                    Kind = kind,
                    BufferId = BodyId,
                    ScalarType = scalarType,
                    ByteOffset = byteOffset,
                    ElementStride = stride,
                    ElementCount = count,
                    Unit = unit,
                    CoordinateSpace = coordinateSpace
                };
            }

            private static long Take(ref long offset, int count, int stride)
            {
                var current = offset;
                offset += (long)count * stride;
                return current;
            }

            private static long Align4(long value)
            {
                var remainder = value % 4;
                return remainder == 0 ? value : value + 4 - remainder;
            }
        }
    }
}
