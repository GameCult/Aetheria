using System;
using System.Collections.Generic;
using GameCult.Caching;
using MessagePack;

#nullable enable

namespace GameCult.Aetheria.State.Verse
{
    public static class AetheriaRuntimeDaemonSoaBackends
    {
        public const string CultCache = "cultcache";
        public const string CultMesh = "cultmesh";
        public const string MemoryMappedFile = "memory_mapped_file";
        public const string SharedNativeMemory = "shared_native_memory";
    }

    public static class AetheriaRuntimeDaemonSoaSynchronizationModes
    {
        public const string ImmutableFrame = "immutable_frame";
        public const string DoubleBuffered = "double_buffered";
        public const string TripleBuffered = "triple_buffered";
    }

    public static class AetheriaRuntimeDaemonSoaColumnKinds
    {
        public const string EntityIndex = "entity.index";
        public const string EntityKey = "entity.key";
        public const string ZoneIndex = "entity.zone_index";
        public const string Position = "transform.position";
        public const string RotationRadians = "transform.rotation.radians";
        public const string Velocity = "motion.velocity";
        public const string PhysicsBodyRadius = "physics.body.radius";
        public const string PhysicsBodyMass = "physics.body.mass";
        public const string PhysicsBodyInverseMass = "physics.body.inverse_mass";
        public const string Hull = "stats.hull";
        public const string Heat = "stats.heat";
        public const string Quality = "stats.quality";
        public const string Durability = "stats.durability";
        public const string RenderGroupId = "render.group.id";
        public const string RenderScale = "render.scale";
        public const string RenderVisibility = "render.visibility";
        public const string RenderLod = "render.lod";
        public const string CargoQuantity = "inventory.cargo.quantity";
    }

    public static class AetheriaRuntimeDaemonRenderShadowModes
    {
        public const string Off = "off";
        public const string On = "on";
        public const string TwoSided = "two_sided";
        public const string ShadowsOnly = "shadows_only";
    }

    [CultDocument("gamecult.aetheria.daemon_soa_view", "gamecult.aetheria.daemon_soa_view.v1")]
    [MessagePackObject]
    public sealed class AetheriaRuntimeDaemonSoaViewDocument
    {
        [Key(0)]
        public string Schema { get; set; } = AetheriaRuntimeDaemonSchemas.SoaView;

        [Key(1)]
        public string DaemonId { get; set; } = "local";

        [Key(2)]
        public string SessionId { get; set; } = "local";

        [Key(3)]
        public long FrameId { get; set; }

        [Key(4)]
        public long Generation { get; set; }

        [Key(5)]
        public string PublishedAtUtc { get; set; } = "";

        [Key(6)]
        public bool IsAuthoritative { get; set; } = true;

        [Key(7)]
        public string Backend { get; set; } = AetheriaRuntimeDaemonSoaBackends.CultCache;

        [Key(8)]
        public string SynchronizationMode { get; set; } = AetheriaRuntimeDaemonSoaSynchronizationModes.ImmutableFrame;

        [Key(9)]
        public IReadOnlyList<AetheriaRuntimeDaemonSoaBufferDocument> Buffers { get; set; } =
            Array.Empty<AetheriaRuntimeDaemonSoaBufferDocument>();

        [Key(10)]
        public IReadOnlyList<AetheriaRuntimeDaemonSoaColumnDocument> Columns { get; set; } =
            Array.Empty<AetheriaRuntimeDaemonSoaColumnDocument>();

        [Key(11)]
        public IReadOnlyList<AetheriaRuntimeDaemonSoaDirtyRangeDocument> DirtyRanges { get; set; } =
            Array.Empty<AetheriaRuntimeDaemonSoaDirtyRangeDocument>();

        [Key(12)]
        public IReadOnlyList<AetheriaRuntimeDaemonRenderGroupDocument> RenderGroups { get; set; } =
            Array.Empty<AetheriaRuntimeDaemonRenderGroupDocument>();

        [Key(13)]
        public IReadOnlyList<AetheriaRuntimeDaemonSoaIdentityDocument> Identities { get; set; } =
            Array.Empty<AetheriaRuntimeDaemonSoaIdentityDocument>();

        public static AetheriaRuntimeDaemonSoaViewDocument Create(
            string daemonId,
            string sessionId,
            long frameId,
            long generation,
            IReadOnlyList<AetheriaRuntimeDaemonSoaBufferDocument>? buffers,
            IReadOnlyList<AetheriaRuntimeDaemonSoaColumnDocument>? columns,
            IReadOnlyList<AetheriaRuntimeDaemonSoaDirtyRangeDocument>? dirtyRanges = null,
            string backend = AetheriaRuntimeDaemonSoaBackends.CultCache,
            string synchronizationMode = AetheriaRuntimeDaemonSoaSynchronizationModes.ImmutableFrame,
            bool isAuthoritative = true,
            IReadOnlyList<AetheriaRuntimeDaemonRenderGroupDocument>? renderGroups = null,
            IReadOnlyList<AetheriaRuntimeDaemonSoaIdentityDocument>? identities = null)
        {
            return new AetheriaRuntimeDaemonSoaViewDocument
            {
                DaemonId = string.IsNullOrWhiteSpace(daemonId) ? "local" : daemonId,
                SessionId = string.IsNullOrWhiteSpace(sessionId) ? "local" : sessionId,
                FrameId = frameId,
                Generation = generation,
                PublishedAtUtc = DateTime.UtcNow.ToString("O"),
                IsAuthoritative = isAuthoritative,
                Backend = string.IsNullOrWhiteSpace(backend) ? AetheriaRuntimeDaemonSoaBackends.CultCache : backend,
                SynchronizationMode = string.IsNullOrWhiteSpace(synchronizationMode)
                    ? AetheriaRuntimeDaemonSoaSynchronizationModes.ImmutableFrame
                    : synchronizationMode,
                Buffers = buffers ?? Array.Empty<AetheriaRuntimeDaemonSoaBufferDocument>(),
                Columns = columns ?? Array.Empty<AetheriaRuntimeDaemonSoaColumnDocument>(),
                DirtyRanges = dirtyRanges ?? Array.Empty<AetheriaRuntimeDaemonSoaDirtyRangeDocument>(),
                RenderGroups = renderGroups ?? Array.Empty<AetheriaRuntimeDaemonRenderGroupDocument>(),
                Identities = identities ?? Array.Empty<AetheriaRuntimeDaemonSoaIdentityDocument>()
            };
        }
    }

    [MessagePackObject]
    public sealed class AetheriaRuntimeDaemonSoaIdentityDocument
    {
        [Key(0)] public int EntityIndex { get; set; } = -1;
        [Key(1)] public string EntityId { get; set; } = "";
        [Key(2)] public string Kind { get; set; } = "";
        [Key(3)] public string Label { get; set; } = "";
        [Key(4)] public string Faction { get; set; } = "";
        [Key(5)] public bool Selectable { get; set; } = true;
        [Key(6)] public bool Controllable { get; set; }
        [Key(7)] public string AssetRef { get; set; } = "";
    }

    [MessagePackObject]
    public sealed class AetheriaRuntimeDaemonRenderGroupDocument
    {
        [Key(0)]
        public int GroupId { get; set; }

        [Key(1)]
        public string MeshKey { get; set; } = "";

        [Key(2)]
        public string MaterialKey { get; set; } = "";

        [Key(3)]
        public int SubMeshIndex { get; set; }

        [Key(4)]
        public int Layer { get; set; }

        [Key(5)]
        public string ShaderKey { get; set; } = "";

        [Key(6)]
        public string DisplayName { get; set; } = "";

        [Key(7)]
        public int InstanceCount { get; set; } = -1;

        [Key(8)]
        public float BoundsCenterX { get; set; }

        [Key(9)]
        public float BoundsCenterY { get; set; }

        [Key(10)]
        public float BoundsCenterZ { get; set; }

        [Key(11)]
        public float BoundsSizeX { get; set; } = -1.0f;

        [Key(12)]
        public float BoundsSizeY { get; set; } = -1.0f;

        [Key(13)]
        public float BoundsSizeZ { get; set; } = -1.0f;

        [Key(14)]
        public string ShadowMode { get; set; } = AetheriaRuntimeDaemonRenderShadowModes.On;

        [Key(15)]
        public bool ReceiveShadows { get; set; } = true;

        [Key(16)]
        public float DefaultScale { get; set; } = 1.0f;

        [Key(17)]
        public int Lod { get; set; } = -1;

        [Key(18)]
        public AetheriaRuntimeAssetRef MeshAsset { get; set; } =
            AetheriaRuntimeAssetRef.Empty(AetheriaRuntimeAssetKinds.Mesh);

        [Key(19)]
        public AetheriaRuntimeAssetRef MaterialAsset { get; set; } =
            AetheriaRuntimeAssetRef.Empty(AetheriaRuntimeAssetKinds.Material);
    }

    [MessagePackObject]
    public sealed class AetheriaRuntimeDaemonSoaBufferDocument
    {
        [Key(0)]
        public string BufferId { get; set; } = "";

        [Key(1)]
        public string DisplayName { get; set; } = "";

        [Key(2)]
        public string Backend { get; set; } = AetheriaRuntimeDaemonSoaBackends.CultCache;

        [Key(3)]
        public string Location { get; set; } = "";

        [Key(4)]
        public long ByteOffset { get; set; }

        [Key(5)]
        public long ByteLength { get; set; }

        [Key(6)]
        public long Generation { get; set; }

        [Key(7)]
        public bool DaemonWritable { get; set; } = true;

        [Key(8)]
        public bool ObserverWritable { get; set; }
    }

    [MessagePackObject]
    public sealed class AetheriaRuntimeDaemonSoaColumnDocument
    {
        [Key(0)]
        public string ColumnId { get; set; } = "";

        [Key(1)]
        public string Kind { get; set; } = "";

        [Key(2)]
        public string BufferId { get; set; } = "";

        [Key(3)]
        public string ScalarType { get; set; } = "";

        [Key(4)]
        public long ByteOffset { get; set; }

        [Key(5)]
        public int ElementStride { get; set; }

        [Key(6)]
        public int ElementCount { get; set; }

        [Key(7)]
        public string Unit { get; set; } = "";

        [Key(8)]
        public string CoordinateSpace { get; set; } = "";
    }

    [MessagePackObject]
    public sealed class AetheriaRuntimeDaemonSoaDirtyRangeDocument
    {
        [Key(0)]
        public string ColumnId { get; set; } = "";

        [Key(1)]
        public int StartIndex { get; set; }

        [Key(2)]
        public int Count { get; set; }

        [Key(3)]
        public long Generation { get; set; }
    }
}
