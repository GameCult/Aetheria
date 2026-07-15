using System;
using System.Linq;
using GameCult.Eve.Surface;
using GameCult.Mesh;

#nullable enable

namespace GameCult.Aetheria.State.Verse
{
    public static class AetheriaRuntimeEveEntitySoaProjection
    {
        public static EveEntitySoaViewDocument Project(
            AetheriaRuntimeDaemonSoaViewDocument source,
            CultMeshBodyGeneration generation)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (generation == null) throw new ArgumentNullException(nameof(generation));
            return new EveEntitySoaViewDocument
            {
                ProviderId = "aetheria",
                ViewId = "pilot",
                FrameId = source.FrameId,
                PublishedAtUtc = source.PublishedAtUtc,
                BodySchemaId = generation.SchemaId,
                LayoutVersion = generation.LayoutVersion,
                ProducerEpoch = generation.ProducerEpoch,
                Sequence = generation.Sequence,
                Capacity = generation.Capacity,
                Buffers = (source.Buffers ?? Array.Empty<AetheriaRuntimeDaemonSoaBufferDocument>())
                    .Select(buffer => new EveEntitySoaBuffer
                    {
                        BufferId = buffer.BufferId,
                        ByteOffset = buffer.ByteOffset,
                        ByteLength = buffer.ByteLength
                    }).ToArray(),
                Columns = (source.Columns ?? Array.Empty<AetheriaRuntimeDaemonSoaColumnDocument>())
                    .Select(column => new EveEntitySoaColumn
                    {
                        ColumnId = column.ColumnId,
                        Semantic = column.Kind,
                        BufferId = column.BufferId,
                        ScalarType = column.ScalarType,
                        ByteOffset = column.ByteOffset,
                        ElementStride = column.ElementStride,
                        ElementCount = column.ElementCount,
                        Unit = column.Unit,
                        CoordinateSpace = column.CoordinateSpace
                    }).ToArray(),
                DirtyRanges = (source.DirtyRanges ?? Array.Empty<AetheriaRuntimeDaemonSoaDirtyRangeDocument>())
                    .Select(range => new EveEntitySoaDirtyRange
                    {
                        ColumnId = range.ColumnId,
                        StartIndex = range.StartIndex,
                        Count = range.Count,
                        Sequence = generation.Sequence
                    }).ToArray(),
                RenderGroups = (source.RenderGroups ?? Array.Empty<AetheriaRuntimeDaemonRenderGroupDocument>())
                    .Select(group => new EveEntityRenderGroup
                    {
                        GroupId = group.GroupId,
                        MeshAssetRef = group.MeshAsset.Uri,
                        MaterialAssetRef = group.MaterialAsset.Uri,
                        SubMeshIndex = group.SubMeshIndex,
                        Layer = group.Layer,
                        InstanceCount = Math.Max(group.InstanceCount, 0),
                        DefaultScale = group.DefaultScale,
                        BoundsCenterX = group.BoundsCenterX,
                        BoundsCenterY = group.BoundsCenterY,
                        BoundsCenterZ = group.BoundsCenterZ,
                        BoundsSizeX = Math.Max(group.BoundsSizeX, 0),
                        BoundsSizeY = Math.Max(group.BoundsSizeY, 0),
                        BoundsSizeZ = Math.Max(group.BoundsSizeZ, 0),
                        ShadowMode = group.ShadowMode,
                        ReceiveShadows = group.ReceiveShadows,
                        Lod = group.Lod
                    }).ToArray(),
                Identities = (source.Identities ?? Array.Empty<AetheriaRuntimeDaemonSoaIdentityDocument>())
                    .Select(identity => new EveEntityIdentity
                    {
                        Index = identity.EntityIndex,
                        EntityId = identity.EntityId,
                        EntityKind = identity.Kind,
                        Label = identity.Label,
                        Faction = identity.Faction,
                        Selectable = identity.Selectable,
                        Controllable = identity.Controllable,
                        AssetRef = identity.AssetRef
                    }).ToArray()
            };
        }

    }
}
