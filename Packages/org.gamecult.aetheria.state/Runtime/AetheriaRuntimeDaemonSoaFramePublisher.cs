using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GameCult.Caching;
using GameCult.Eve.Surface;
using GameCult.Mesh;

#nullable enable

namespace GameCult.Aetheria.State.Verse
{
    public sealed class AetheriaRuntimeDaemonSoaFrame
    {
        public AetheriaRuntimeDaemonSoaFrame(AetheriaRuntimeDaemonSoaViewDocument view, byte[] bytes)
        {
            View = view ?? throw new ArgumentNullException(nameof(view));
            Bytes = bytes ?? throw new ArgumentNullException(nameof(bytes));
        }

        public AetheriaRuntimeDaemonSoaViewDocument View { get; }
        public byte[] Bytes { get; }
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
        public const int LayoutVersion = 2;
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
        private readonly Dictionary<string, int> _syntheticEntityIndices =
            new Dictionary<string, int>(StringComparer.Ordinal);
        private int _nextSyntheticEntityIndex = -2;

        public AetheriaRuntimeDaemonSoaFramePublisher(CultCache cache, long producerEpoch)
        {
            if (cache == null) throw new ArgumentNullException(nameof(cache));
            _localPublisher = new CultMeshFrameBodyPublisher(
                BodyId, BodySchemaId, LayoutVersion, Capacity, producerEpoch,
                checked((int)EntityHotSlabLayout.Create(Capacity).TotalByteLength),
                TimeSpan.FromSeconds(30));
            _networkPublisher = new CultMeshNetworkBodyPublisher(
                cache,
                generation => string.Equals(generation.ProducerId, ProducerId, StringComparison.Ordinal));
        }

        public AetheriaRuntimeDaemonSoaFrame BuildCurrentZoneEntities(
            AetheriaRuntimeDaemonFrameDocument frame)
        {
            if (frame == null)
                throw new ArgumentNullException(nameof(frame));

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
            var syntheticEntityIds = pickupEntityIds.Concat(payloadEntityIds).ToArray();
            if (syntheticEntityIds.Distinct(StringComparer.Ordinal).Count() != syntheticEntityIds.Length)
                throw new InvalidOperationException("Current-zone synthetic SoA entity identities are not unique.");
            var syntheticEntityIndices = syntheticEntityIds.ToDictionary(
                entityId => entityId,
                GetOrAllocateSyntheticEntityIndex,
                StringComparer.Ordinal);
            var count = entities.Length + pickups.Length + payloads.Length;
            if (count > Capacity)
                throw new InvalidOperationException($"Aetheria entity SoA capacity {Capacity} was exceeded by {count} rows.");
            var generation = Math.Max(frame.FrameId, 0);
            var layout = EntityHotSlabLayout.Create(count);
            var bytes = new byte[layout.TotalByteLength];
            WriteEntities(bytes, layout, entities);
            WritePickups(bytes, layout, pickups, pickupEntityIds, syntheticEntityIndices, entities.Length);
            WritePayloads(bytes, layout, payloads, payloadEntityIds, syntheticEntityIndices, entities.Length + pickups.Length);

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
                renderGroups: CreateRenderGroups(entities, pickups, payloads),
                identities: entities.Select(entity => new AetheriaRuntimeDaemonSoaIdentityDocument
                    {
                        EntityIndex = entity.EntityIndex,
                        EntityId = entity.EntityId,
                        Kind = entity.Kind,
                        Label = entity.Name,
                        Faction = entity.FactionKey,
                        Selectable = entity.EntityIndex != controlled?.EntityIndex,
                        Controllable = entity.EntityIndex == controlled?.EntityIndex,
                        AssetRef = AetheriaRuntimeAssets.ResolveEntityPrefabAssetRef(entity)
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
                    .ToArray());

            return new AetheriaRuntimeDaemonSoaFrame(view, bytes);
        }

        public async Task<AetheriaRuntimeDaemonSoaPublication> PublishAsync(AetheriaRuntimeDaemonSoaFrame frame)
        {
            if (frame == null) throw new ArgumentNullException(nameof(frame));
            var now = DateTimeOffset.UtcNow;
            if (!_localPublisher.TryPublish(frame.Bytes, now, out var local))
                throw new InvalidOperationException("CultMesh has no unleased frame slot for the Aetheria SoA generation.");
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
            var network = await _networkPublisher.PublishAsync(generation, frame.Bytes).ConfigureAwait(false);
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
                PreferredLocal = local,
                NetworkFallback = network
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
            IReadOnlyList<AetheriaRuntimePhysicalPayloadCommit> payloads)
        {
            if (entities.Count == 0 && pickups.Count == 0 && payloads.Count == 0)
                return Array.Empty<AetheriaRuntimeDaemonRenderGroupDocument>();

            var positions = entities.Select(entity => (entity.PositionX, entity.PositionY, entity.PositionZ))
                .Concat(pickups.Select(pickup => (pickup.PositionX, pickup.PositionY, pickup.PositionZ)))
                .Concat(payloads.Select(payload => (payload.PositionX, PositionY: payload.PositionY, payload.PositionZ)))
                .ToArray();
            var minX = positions.Min(position => (float)position.PositionX);
            var minY = positions.Min(position => (float)position.PositionY);
            var minZ = positions.Min(position => (float)position.PositionZ);
            var maxX = positions.Max(position => (float)position.PositionX);
            var maxY = positions.Max(position => (float)position.PositionY);
            var maxZ = positions.Max(position => (float)position.PositionZ);
            const float padding = 16.0f;

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
                    InstanceCount = entities.Count + pickups.Count + payloads.Count,
                    BoundsCenterX = (minX + maxX) * 0.5f,
                    BoundsCenterY = (minY + maxY) * 0.5f,
                    BoundsCenterZ = (minZ + maxZ) * 0.5f,
                    BoundsSizeX = Math.Max(maxX - minX + padding, padding),
                    BoundsSizeY = Math.Max(maxY - minY + padding, padding),
                    BoundsSizeZ = Math.Max(maxZ - minZ + padding, padding),
                    ShadowMode = AetheriaRuntimeDaemonRenderShadowModes.On,
                    ReceiveShadows = true,
                    DefaultScale = 1.0f,
                    Lod = -1
                }
            };
        }

        private static void WriteEntities(
            byte[] bytes,
            EntityHotSlabLayout layout,
            IReadOnlyList<AetheriaRuntimeEntitySnapshotCommit> entities)
        {
            for (var index = 0; index < entities.Count; index++)
            {
                var entity = entities[index];
                WriteInt32(bytes, layout.EntityIndex + index * IntStride, entity.EntityIndex);
                WriteInt32(bytes, layout.CargoQuantity + index * IntStride, CountCargoUnits(entity));
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
            byte[] bytes,
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
            byte[] bytes,
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

        private static void WriteFloat(byte[] bytes, long byteOffset, int index, double value)
        {
            WriteBytes(bytes, byteOffset + index * FloatStride, BitConverter.GetBytes(IsFinite(value) ? (float)value : 0.0f));
        }

        private static void WriteFloat3(
            byte[] bytes,
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

        private static void WriteInt32(byte[] bytes, long offset, int value) =>
            WriteBytes(bytes, offset, BitConverter.GetBytes(value));

        private static void WriteUInt32(byte[] bytes, long offset, int value) =>
            WriteBytes(bytes, offset, BitConverter.GetBytes((uint)value));

        private static void WriteBytes(byte[] destination, long offset, byte[] source) =>
            Buffer.BlockCopy(source, 0, destination, checked((int)offset), source.Length);

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private static int CountCargoUnits(AetheriaRuntimeEntitySnapshotCommit entity)
        {
            return (entity.CargoContents ?? Array.Empty<AetheriaRuntimeCargoBayLoadoutCommit>())
                .Where(bay => bay != null)
                .SelectMany(bay => bay.Items ?? Array.Empty<AetheriaRuntimeLoadoutItemSlotCommit>())
                .Where(slot => slot?.Item != null)
                .Sum(slot => Math.Max(0, slot.Item.Quantity));
        }

        private readonly struct EntityHotSlabLayout
        {
            private EntityHotSlabLayout(
                long entityIndex,
                long cargoQuantity,
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
