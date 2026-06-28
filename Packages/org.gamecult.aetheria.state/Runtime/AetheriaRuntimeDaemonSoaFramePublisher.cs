using System;
using System.Collections.Generic;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

#nullable enable

namespace GameCult.Aetheria.State.Verse
{
    public static class AetheriaRuntimeDaemonSoaFramePublisher
    {
        private const string BufferId = "current-zone-entities-hot";
        private const int EntityRenderGroupId = 1;
        private const int FloatStride = 4;
        private const int Float3Stride = 12;
        private const int IntStride = 4;
        private const int ByteStride = 1;
        private const int RetainedBufferCount = 4;

        private static readonly object Sync = new object();
        private static readonly Dictionary<string, RetainedMappedBuffer> RetainedBuffers =
            new Dictionary<string, RetainedMappedBuffer>(StringComparer.Ordinal);
        private static long RetainedBufferUseSequence;

        public static AetheriaRuntimeDaemonSoaViewDocument BuildCurrentZoneEntities(
            string stateFilePath,
            AetheriaRuntimeDaemonFrameDocument frame)
        {
            if (string.IsNullOrWhiteSpace(stateFilePath))
                throw new ArgumentException("State file path must be non-empty.", nameof(stateFilePath));
            if (frame == null)
                throw new ArgumentNullException(nameof(frame));

            var run = frame.Run ?? new AetheriaRuntimeRunCheckpointCommit();
            var zone = (run.Zones ?? Array.Empty<AetheriaRuntimeZoneSnapshotCommit>())
                .FirstOrDefault(candidate => candidate != null && candidate.ZoneIndex == run.CurrentZoneIndex);
            var entities = (zone?.Entities ?? Array.Empty<AetheriaRuntimeEntitySnapshotCommit>())
                .Where(entity => entity != null)
                .OrderBy(entity => entity.EntityIndex)
                .ToArray();
            var count = entities.Length;
            var generation = Math.Max(frame.FrameId, 0);
            var layout = EntityHotSlabLayout.Create(count);
            var location = CreateLocation(stateFilePath, frame.SessionId, generation);

            var buffer = RetainBuffer(location, layout.TotalByteLength);
            WriteEntities(buffer.Accessor, layout, entities);

            var view = AetheriaRuntimeDaemonSoaViewDocument.Create(
                string.IsNullOrWhiteSpace(frame.DaemonId) ? "aetheria-daemon" : frame.DaemonId,
                string.IsNullOrWhiteSpace(frame.SessionId) ? "local" : frame.SessionId,
                frame.FrameId,
                generation,
                new[]
                {
                    new AetheriaRuntimeDaemonSoaBufferDocument
                    {
                        BufferId = BufferId,
                        DisplayName = "Current zone daemon entity hot slab",
                        Backend = AetheriaRuntimeDaemonSoaBackends.MemoryMappedFile,
                        Location = location,
                        ByteOffset = 0,
                        ByteLength = layout.TotalByteLength,
                        Generation = generation,
                        DaemonWritable = true,
                        ObserverWritable = false
                    }
                },
                layout.CreateColumns(count),
                layout.CreateDirtyRanges(count, generation),
                backend: AetheriaRuntimeDaemonSoaBackends.MemoryMappedFile,
                synchronizationMode: AetheriaRuntimeDaemonSoaSynchronizationModes.ImmutableFrame,
                renderGroups: CreateRenderGroups(entities));

            return view;
        }

        private static IReadOnlyList<AetheriaRuntimeDaemonRenderGroupDocument> CreateRenderGroups(
            IReadOnlyList<AetheriaRuntimeEntitySnapshotCommit> entities)
        {
            if (entities.Count == 0)
                return Array.Empty<AetheriaRuntimeDaemonRenderGroupDocument>();

            var minX = entities.Min(entity => (float)entity.PositionX);
            var minY = entities.Min(entity => (float)entity.PositionY);
            var minZ = entities.Min(entity => (float)entity.PositionZ);
            var maxX = entities.Max(entity => (float)entity.PositionX);
            var maxY = entities.Max(entity => (float)entity.PositionY);
            var maxZ = entities.Max(entity => (float)entity.PositionZ);
            const float padding = 16.0f;

            return new[]
            {
                new AetheriaRuntimeDaemonRenderGroupDocument
                {
                    GroupId = EntityRenderGroupId,
                    MeshKey = "resources://Aetheria/Daemon/EntityProxy",
                    MaterialKey = "resources://Aetheria/Daemon/EntityProxy",
                    MeshAsset = AetheriaRuntimeAssetRef.FromKey(
                        "daemon.entity_proxy.mesh",
                        AetheriaRuntimeAssetKinds.Mesh,
                        "resources://Aetheria/Daemon/EntityProxy",
                        AetheriaRuntimeAssetTransports.Resources),
                    MaterialAsset = AetheriaRuntimeAssetRef.FromKey(
                        "daemon.entity_proxy.material",
                        AetheriaRuntimeAssetKinds.Material,
                        "resources://Aetheria/Daemon/EntityProxy",
                        AetheriaRuntimeAssetTransports.Resources),
                    SubMeshIndex = 0,
                    Layer = 0,
                    ShaderKey = "aetheria.daemon.entity-proxy",
                    DisplayName = "Daemon current-zone entities",
                    InstanceCount = entities.Count,
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
            MemoryMappedViewAccessor accessor,
            EntityHotSlabLayout layout,
            IReadOnlyList<AetheriaRuntimeEntitySnapshotCommit> entities)
        {
            for (var index = 0; index < entities.Count; index++)
            {
                var entity = entities[index];
                accessor.Write(layout.EntityIndex + index * IntStride, entity.EntityIndex);
                WriteFloat3(accessor, layout.Position, index, entity.PositionX, entity.PositionY, entity.PositionZ);
                WriteFloat(accessor, layout.RotationRadians, index, Math.Atan2(entity.DirectionX, entity.DirectionY));
                WriteFloat3(accessor, layout.Velocity, index, entity.VelocityX, 0.0, entity.VelocityY);
                WriteFloat(accessor, layout.PhysicsBodyRadius, index, 1.0);
                WriteFloat(accessor, layout.PhysicsBodyMass, index, 1.0);
                WriteFloat(accessor, layout.PhysicsBodyInverseMass, index, 1.0);
                WriteFloat(accessor, layout.RenderScale, index, 1.0);
                accessor.Write(layout.RenderVisibility + index * ByteStride, (byte)(entity.IsActive ? 1 : 0));
                accessor.Write(layout.RenderLod + index * IntStride, 0);
                accessor.Write(layout.RenderGroupId + index * IntStride, (uint)EntityRenderGroupId);
            }
        }

        private static void WriteFloat(MemoryMappedViewAccessor accessor, long byteOffset, int index, double value)
        {
            accessor.Write(byteOffset + index * FloatStride, IsFinite(value) ? (float)value : 0.0f);
        }

        private static void WriteFloat3(
            MemoryMappedViewAccessor accessor,
            long byteOffset,
            int index,
            double x,
            double y,
            double z)
        {
            var elementOffset = byteOffset + index * Float3Stride;
            accessor.Write(elementOffset, IsFinite(x) ? (float)x : 0.0f);
            accessor.Write(elementOffset + FloatStride, IsFinite(y) ? (float)y : 0.0f);
            accessor.Write(elementOffset + FloatStride * 2, IsFinite(z) ? (float)z : 0.0f);
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private static RetainedMappedBuffer RetainBuffer(string location, long byteLength)
        {
            lock (Sync)
            {
                if (RetainedBuffers.TryGetValue(location, out var existing))
                {
                    existing.MarkUsed(++RetainedBufferUseSequence);
                    return existing;
                }

                var memory = MemoryMappedFile.CreateOrOpen(location, byteLength, MemoryMappedFileAccess.ReadWrite);
                var accessor = memory.CreateViewAccessor(0, byteLength, MemoryMappedFileAccess.ReadWrite);
                var buffer = new RetainedMappedBuffer(memory, accessor, ++RetainedBufferUseSequence);
                RetainedBuffers[location] = buffer;
                TrimRetainedBuffers(location);
                return buffer;
            }
        }

        private static void TrimRetainedBuffers(string retainedLocation)
        {
            if (RetainedBuffers.Count <= RetainedBufferCount)
                return;

            foreach (var key in RetainedBuffers
                .Where(pair => !string.Equals(pair.Key, retainedLocation, StringComparison.Ordinal))
                .OrderBy(pair => pair.Value.LastUsedSequence)
                .Select(pair => pair.Key)
                .Take(RetainedBuffers.Count - RetainedBufferCount)
                .ToArray())
            {
                RetainedBuffers[key].Dispose();
                RetainedBuffers.Remove(key);
            }
        }

        private static string CreateLocation(string stateFilePath, string sessionId, long generation)
        {
            using var sha = SHA256.Create();
            var identity = $"{stateFilePath}|{sessionId}|{generation}";
            var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(identity));
            return "Aetheria.Daemon.Soa." + BitConverter.ToString(hash, 0, 12).Replace("-", "");
        }

        private sealed class RetainedMappedBuffer : IDisposable
        {
            private readonly MemoryMappedFile _memory;
            public RetainedMappedBuffer(MemoryMappedFile memory, MemoryMappedViewAccessor accessor, long lastUsedSequence)
            {
                _memory = memory;
                Accessor = accessor;
                LastUsedSequence = lastUsedSequence;
            }

            public MemoryMappedViewAccessor Accessor { get; }

            public long LastUsedSequence { get; private set; }

            public void MarkUsed(long sequence)
            {
                LastUsedSequence = sequence;
            }

            public void Dispose()
            {
                Accessor.Dispose();
                _memory.Dispose();
            }
        }

        private readonly struct EntityHotSlabLayout
        {
            private EntityHotSlabLayout(
                long entityIndex,
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
                    BufferId = BufferId,
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
