using System;
using System.Collections.Generic;

#nullable enable

namespace GameCult.Aetheria.State.Unity
{
    public sealed class AetheriaRuntimeDaemonSoaColumnBinding
    {
        public AetheriaRuntimeDaemonSoaColumnBinding(
            AetheriaRuntimeDaemonSoaColumnDocument column,
            AetheriaRuntimeDaemonSoaBufferDocument buffer,
            long absoluteByteOffset,
            long byteLength,
            bool directMemoryCompatible)
        {
            Column = column ?? throw new ArgumentNullException(nameof(column));
            Buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
            AbsoluteByteOffset = absoluteByteOffset;
            ByteLength = byteLength;
            DirectMemoryCompatible = directMemoryCompatible;
        }

        public AetheriaRuntimeDaemonSoaColumnDocument Column { get; }
        public AetheriaRuntimeDaemonSoaBufferDocument Buffer { get; }
        public long AbsoluteByteOffset { get; }
        public long ByteLength { get; }
        public bool DirectMemoryCompatible { get; }
    }

    public sealed class AetheriaRuntimeDaemonSoaViewIndex
    {
        private readonly Dictionary<string, AetheriaRuntimeDaemonSoaBufferDocument> _buffersById;
        private readonly Dictionary<string, AetheriaRuntimeDaemonSoaColumnBinding> _columnsById;
        private readonly Dictionary<string, List<AetheriaRuntimeDaemonSoaColumnBinding>> _columnsByKind;
        private readonly Dictionary<string, List<AetheriaRuntimeDaemonSoaDirtyRangeDocument>> _dirtyRangesByColumnId;
        private readonly Dictionary<int, AetheriaRuntimeDaemonRenderGroupDocument> _renderGroupsById;

        private AetheriaRuntimeDaemonSoaViewIndex(
            AetheriaRuntimeDaemonSoaViewDocument view,
            Dictionary<string, AetheriaRuntimeDaemonSoaBufferDocument> buffersById,
            Dictionary<string, AetheriaRuntimeDaemonSoaColumnBinding> columnsById,
            Dictionary<string, List<AetheriaRuntimeDaemonSoaColumnBinding>> columnsByKind,
            Dictionary<string, List<AetheriaRuntimeDaemonSoaDirtyRangeDocument>> dirtyRangesByColumnId,
            Dictionary<int, AetheriaRuntimeDaemonRenderGroupDocument> renderGroupsById,
            string[] validationErrors)
        {
            View = view ?? throw new ArgumentNullException(nameof(view));
            _buffersById = buffersById;
            _columnsById = columnsById;
            _columnsByKind = columnsByKind;
            _dirtyRangesByColumnId = dirtyRangesByColumnId;
            _renderGroupsById = renderGroupsById;
            ValidationErrors = validationErrors;
        }

        public AetheriaRuntimeDaemonSoaViewDocument View { get; }
        public IReadOnlyList<string> ValidationErrors { get; }
        public bool IsValid => ValidationErrors.Count == 0;

        public static AetheriaRuntimeDaemonSoaViewIndex Empty { get; } =
            Build(new AetheriaRuntimeDaemonSoaViewDocument { IsAuthoritative = false });

        public static AetheriaRuntimeDaemonSoaViewIndex Build(
            AetheriaRuntimeDaemonSoaViewDocument? view,
            bool requireObserverReadOnly = true)
        {
            var effectiveView = view ?? new AetheriaRuntimeDaemonSoaViewDocument { IsAuthoritative = false };
            var errors = new List<string>();
            var buffersById = new Dictionary<string, AetheriaRuntimeDaemonSoaBufferDocument>(StringComparer.Ordinal);
            var columnsById = new Dictionary<string, AetheriaRuntimeDaemonSoaColumnBinding>(StringComparer.Ordinal);
            var columnsByKind = new Dictionary<string, List<AetheriaRuntimeDaemonSoaColumnBinding>>(StringComparer.Ordinal);
            var dirtyRangesByColumnId = new Dictionary<string, List<AetheriaRuntimeDaemonSoaDirtyRangeDocument>>(StringComparer.Ordinal);
            var renderGroupsById = new Dictionary<int, AetheriaRuntimeDaemonRenderGroupDocument>();

            if (!string.Equals(effectiveView.Schema, AetheriaRuntimeDaemonSchemas.SoaView, StringComparison.Ordinal))
            {
                errors.Add($"Unexpected SoA schema '{effectiveView.Schema}'.");
            }

            foreach (var buffer in effectiveView.Buffers ?? Array.Empty<AetheriaRuntimeDaemonSoaBufferDocument>())
            {
                if (string.IsNullOrWhiteSpace(buffer.BufferId))
                {
                    errors.Add("SoA buffer is missing a buffer id.");
                    continue;
                }

                if (buffersById.ContainsKey(buffer.BufferId))
                {
                    errors.Add($"Duplicate SoA buffer id '{buffer.BufferId}'.");
                    continue;
                }

                if (buffer.ByteOffset < 0)
                {
                    errors.Add($"SoA buffer '{buffer.BufferId}' has a negative byte offset.");
                }

                if (buffer.ByteLength < 0)
                {
                    errors.Add($"SoA buffer '{buffer.BufferId}' has a negative byte length.");
                }

                if (requireObserverReadOnly && buffer.ObserverWritable)
                {
                    errors.Add($"SoA buffer '{buffer.BufferId}' is observer-writable; Unity must observe daemon state read-only.");
                }

                buffersById.Add(buffer.BufferId, buffer);
            }

            foreach (var column in effectiveView.Columns ?? Array.Empty<AetheriaRuntimeDaemonSoaColumnDocument>())
            {
                if (string.IsNullOrWhiteSpace(column.ColumnId))
                {
                    errors.Add("SoA column is missing a column id.");
                    continue;
                }

                if (columnsById.ContainsKey(column.ColumnId))
                {
                    errors.Add($"Duplicate SoA column id '{column.ColumnId}'.");
                    continue;
                }

                if (!buffersById.TryGetValue(column.BufferId ?? "", out var buffer))
                {
                    errors.Add($"SoA column '{column.ColumnId}' references missing buffer '{column.BufferId}'.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(column.Kind))
                {
                    errors.Add($"SoA column '{column.ColumnId}' is missing a semantic kind.");
                }

                if (column.ByteOffset < 0)
                {
                    errors.Add($"SoA column '{column.ColumnId}' has a negative byte offset.");
                }

                if (column.ElementStride <= 0)
                {
                    errors.Add($"SoA column '{column.ColumnId}' has a non-positive element stride.");
                }

                if (column.ElementCount < 0)
                {
                    errors.Add($"SoA column '{column.ColumnId}' has a negative element count.");
                }

                var scalarByteLength = GetScalarByteLength(column.ScalarType);
                if (scalarByteLength <= 0)
                {
                    errors.Add($"SoA column '{column.ColumnId}' has unsupported scalar type '{column.ScalarType}'.");
                    scalarByteLength = Math.Max(1, column.ElementStride);
                }

                var columnByteLength = GetColumnByteLength(column.ElementCount, column.ElementStride, scalarByteLength);
                var absoluteByteOffset = buffer.ByteOffset + column.ByteOffset;
                if (column.ByteOffset >= 0 && columnByteLength >= 0 && buffer.ByteLength >= 0)
                {
                    var columnEnd = column.ByteOffset + columnByteLength;
                    if (columnEnd > buffer.ByteLength)
                    {
                        errors.Add($"SoA column '{column.ColumnId}' exceeds buffer '{buffer.BufferId}'.");
                    }
                }

                var binding = new AetheriaRuntimeDaemonSoaColumnBinding(
                    column,
                    buffer,
                    absoluteByteOffset,
                    columnByteLength,
                    IsDirectMemoryCompatible(buffer));

                ValidateSemanticColumn(column, errors);

                columnsById.Add(column.ColumnId, binding);
                if (!columnsByKind.TryGetValue(column.Kind ?? "", out var kindBindings))
                {
                    kindBindings = new List<AetheriaRuntimeDaemonSoaColumnBinding>();
                    columnsByKind[column.Kind ?? ""] = kindBindings;
                }

                kindBindings.Add(binding);
            }

            foreach (var dirtyRange in effectiveView.DirtyRanges ?? Array.Empty<AetheriaRuntimeDaemonSoaDirtyRangeDocument>())
            {
                if (!columnsById.TryGetValue(dirtyRange.ColumnId ?? "", out var binding))
                {
                    errors.Add($"SoA dirty range references missing column '{dirtyRange.ColumnId}'.");
                    continue;
                }

                if (dirtyRange.StartIndex < 0)
                {
                    errors.Add($"SoA dirty range for column '{dirtyRange.ColumnId}' has a negative start index.");
                }

                if (dirtyRange.Count < 0)
                {
                    errors.Add($"SoA dirty range for column '{dirtyRange.ColumnId}' has a negative count.");
                }

                if (dirtyRange.StartIndex >= 0 && dirtyRange.Count >= 0 &&
                    dirtyRange.StartIndex + dirtyRange.Count > binding.Column.ElementCount)
                {
                    errors.Add($"SoA dirty range for column '{dirtyRange.ColumnId}' exceeds the column element count.");
                }

                if (!dirtyRangesByColumnId.TryGetValue(dirtyRange.ColumnId ?? "", out var ranges))
                {
                    ranges = new List<AetheriaRuntimeDaemonSoaDirtyRangeDocument>();
                    dirtyRangesByColumnId[dirtyRange.ColumnId ?? ""] = ranges;
                }

                ranges.Add(dirtyRange);
            }

            foreach (var renderGroup in effectiveView.RenderGroups ?? Array.Empty<AetheriaRuntimeDaemonRenderGroupDocument>())
            {
                if (renderGroup.GroupId < 0)
                {
                    errors.Add($"Render group '{renderGroup.GroupId}' has a negative group id.");
                    continue;
                }

                if (renderGroupsById.ContainsKey(renderGroup.GroupId))
                {
                    errors.Add($"Duplicate render group id '{renderGroup.GroupId}'.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(renderGroup.MeshKey))
                {
                    errors.Add($"Render group '{renderGroup.GroupId}' is missing a mesh key.");
                }

                if (string.IsNullOrWhiteSpace(renderGroup.MaterialKey))
                {
                    errors.Add($"Render group '{renderGroup.GroupId}' is missing a material key.");
                }

                if (renderGroup.SubMeshIndex < 0)
                {
                    errors.Add($"Render group '{renderGroup.GroupId}' has a negative submesh index.");
                }

                if (renderGroup.InstanceCount < -1)
                {
                    errors.Add($"Render group '{renderGroup.GroupId}' has an invalid instance count.");
                }

                if (renderGroup.BoundsSizeX <= 0 ||
                    renderGroup.BoundsSizeY <= 0 ||
                    renderGroup.BoundsSizeZ <= 0)
                {
                    errors.Add($"Render group '{renderGroup.GroupId}' must publish positive render bounds.");
                }

                if (!IsValidShadowMode(renderGroup.ShadowMode))
                {
                    errors.Add($"Render group '{renderGroup.GroupId}' has invalid shadow mode '{renderGroup.ShadowMode}'.");
                }

                if (renderGroup.DefaultScale <= 0)
                {
                    errors.Add($"Render group '{renderGroup.GroupId}' has a non-positive default scale.");
                }

                if (renderGroup.Lod < -1)
                {
                    errors.Add($"Render group '{renderGroup.GroupId}' has an invalid render lod.");
                }

                renderGroupsById.Add(renderGroup.GroupId, renderGroup);
            }

            if (renderGroupsById.Count > 1 &&
                !columnsByKind.ContainsKey(AetheriaRuntimeDaemonSoaColumnKinds.RenderGroupId))
            {
                errors.Add("Multiple render groups require a render group id column.");
            }

            return new AetheriaRuntimeDaemonSoaViewIndex(
                effectiveView,
                buffersById,
                columnsById,
                columnsByKind,
                dirtyRangesByColumnId,
                renderGroupsById,
                errors.ToArray());
        }

        public bool TryGetBuffer(string bufferId, out AetheriaRuntimeDaemonSoaBufferDocument buffer)
        {
            return _buffersById.TryGetValue(bufferId ?? "", out buffer);
        }

        public bool TryGetColumn(string columnId, out AetheriaRuntimeDaemonSoaColumnBinding binding)
        {
            return _columnsById.TryGetValue(columnId ?? "", out binding);
        }

        public bool TryGetFirstColumnOfKind(string kind, out AetheriaRuntimeDaemonSoaColumnBinding binding)
        {
            if (_columnsByKind.TryGetValue(kind ?? "", out var bindings) && bindings.Count > 0)
            {
                binding = bindings[0];
                return true;
            }

            binding = null!;
            return false;
        }

        public IReadOnlyList<AetheriaRuntimeDaemonSoaColumnBinding> GetColumnsOfKind(string kind)
        {
            return _columnsByKind.TryGetValue(kind ?? "", out var bindings)
                ? bindings
                : Array.Empty<AetheriaRuntimeDaemonSoaColumnBinding>();
        }

        public IReadOnlyList<AetheriaRuntimeDaemonSoaDirtyRangeDocument> GetDirtyRanges(string columnId)
        {
            return _dirtyRangesByColumnId.TryGetValue(columnId ?? "", out var ranges)
                ? ranges
                : Array.Empty<AetheriaRuntimeDaemonSoaDirtyRangeDocument>();
        }

        public IReadOnlyList<AetheriaRuntimeDaemonRenderGroupDocument> RenderGroups =>
            View.RenderGroups ?? Array.Empty<AetheriaRuntimeDaemonRenderGroupDocument>();

        public bool TryGetRenderGroup(int groupId, out AetheriaRuntimeDaemonRenderGroupDocument renderGroup)
        {
            return _renderGroupsById.TryGetValue(groupId, out renderGroup);
        }

        private static bool IsDirectMemoryCompatible(AetheriaRuntimeDaemonSoaBufferDocument buffer)
        {
            return !buffer.ObserverWritable &&
                (string.Equals(buffer.Backend, AetheriaRuntimeDaemonSoaBackends.MemoryMappedFile, StringComparison.Ordinal) ||
                 string.Equals(buffer.Backend, AetheriaRuntimeDaemonSoaBackends.SharedNativeMemory, StringComparison.Ordinal));
        }

        private static void ValidateSemanticColumn(
            AetheriaRuntimeDaemonSoaColumnDocument column,
            List<string> errors)
        {
            switch (column.Kind)
            {
                case AetheriaRuntimeDaemonSoaColumnKinds.PositionX:
                case AetheriaRuntimeDaemonSoaColumnKinds.PositionY:
                case AetheriaRuntimeDaemonSoaColumnKinds.PositionZ:
                case AetheriaRuntimeDaemonSoaColumnKinds.RotationRadians:
                case AetheriaRuntimeDaemonSoaColumnKinds.VelocityX:
                case AetheriaRuntimeDaemonSoaColumnKinds.VelocityY:
                case AetheriaRuntimeDaemonSoaColumnKinds.VelocityZ:
                case AetheriaRuntimeDaemonSoaColumnKinds.PhysicsBodyRadius:
                case AetheriaRuntimeDaemonSoaColumnKinds.PhysicsBodyMass:
                case AetheriaRuntimeDaemonSoaColumnKinds.PhysicsBodyInverseMass:
                    if (!string.Equals(column.ScalarType, "float32", StringComparison.Ordinal))
                    {
                        errors.Add($"SoA column '{column.ColumnId}' {column.Kind} must use float32.");
                    }

                    break;
                case AetheriaRuntimeDaemonSoaColumnKinds.RenderScale:
                    if (!string.Equals(column.ScalarType, "float32", StringComparison.Ordinal))
                    {
                        errors.Add($"SoA column '{column.ColumnId}' render scale must use float32.");
                    }

                    break;
                case AetheriaRuntimeDaemonSoaColumnKinds.RenderVisibility:
                    if (!string.Equals(column.ScalarType, "bool", StringComparison.Ordinal) &&
                        !string.Equals(column.ScalarType, "byte", StringComparison.Ordinal) &&
                        !string.Equals(column.ScalarType, "uint8", StringComparison.Ordinal))
                    {
                        errors.Add($"SoA column '{column.ColumnId}' render visibility must use bool, byte, or uint8.");
                    }

                    break;
                case AetheriaRuntimeDaemonSoaColumnKinds.RenderLod:
                    if (!string.Equals(column.ScalarType, "int32", StringComparison.Ordinal))
                    {
                        errors.Add($"SoA column '{column.ColumnId}' render lod must use int32.");
                    }

                    break;
            }
        }

        private static bool IsValidShadowMode(string? shadowMode)
        {
            switch (shadowMode)
            {
                case AetheriaRuntimeDaemonRenderShadowModes.Off:
                case AetheriaRuntimeDaemonRenderShadowModes.On:
                case AetheriaRuntimeDaemonRenderShadowModes.TwoSided:
                case AetheriaRuntimeDaemonRenderShadowModes.ShadowsOnly:
                    return true;
                default:
                    return false;
            }
        }

        private static int GetScalarByteLength(string? scalarType)
        {
            switch (scalarType)
            {
                case "float32":
                case "int32":
                case "uint32":
                    return 4;
                case "float64":
                case "int64":
                case "uint64":
                    return 8;
                case "int16":
                case "uint16":
                    return 2;
                case "byte":
                case "uint8":
                case "int8":
                case "bool":
                    return 1;
                default:
                    return 0;
            }
        }

        private static long GetColumnByteLength(int elementCount, int elementStride, int scalarByteLength)
        {
            if (elementCount <= 0)
            {
                return 0;
            }

            if (elementStride <= 0 || scalarByteLength <= 0)
            {
                return -1;
            }

            return ((long)elementCount - 1) * elementStride + scalarByteLength;
        }
    }
}
