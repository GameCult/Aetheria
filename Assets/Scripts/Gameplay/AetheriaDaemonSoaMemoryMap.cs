using System;
using System.Collections.Generic;
using System.IO.MemoryMappedFiles;
using GameCult.Aetheria.State.Unity;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

public unsafe sealed class AetheriaDaemonSoaMemoryMap : IDisposable
{
    private readonly Dictionary<string, MappedBuffer> _buffersById =
        new Dictionary<string, MappedBuffer>(StringComparer.Ordinal);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
    private readonly List<AtomicSafetyHandle> _safetyHandles = new List<AtomicSafetyHandle>();
#endif

    private bool _disposed;

    private AetheriaDaemonSoaMemoryMap()
    {
    }

    public static bool TryOpen(
        AetheriaRuntimeDaemonSoaViewIndex index,
        out AetheriaDaemonSoaMemoryMap map,
        out string error)
    {
        map = null;
        error = "";

        if (index == null)
        {
            error = "No daemon SoA view index was provided.";
            return false;
        }

        if (!index.IsValid)
        {
            error = index.ValidationErrors.Count > 0
                ? index.ValidationErrors[0]
                : "Daemon SoA view index is invalid.";
            return false;
        }

        var opened = new AetheriaDaemonSoaMemoryMap();
        try
        {
            foreach (var buffer in index.View.Buffers)
            {
                if (!IsMappableReadOnlyBuffer(buffer))
                {
                    continue;
                }

                opened.OpenBuffer(buffer);
            }

            map = opened;
            return true;
        }
        catch (Exception exception)
        {
            opened.Dispose();
            error = exception.Message;
            return false;
        }
    }

    public bool TryCreateNativeArray<T>(
        AetheriaRuntimeDaemonSoaColumnBinding binding,
        out NativeArray<T> array)
        where T : struct
    {
        ThrowIfDisposed();
        array = default;

        if (binding == null ||
            !binding.DirectMemoryCompatible ||
            !_buffersById.TryGetValue(binding.Buffer.BufferId, out var buffer))
        {
            return false;
        }

        var byteOffset = checked((ulong)binding.AbsoluteByteOffset);
        var byteLength = checked((ulong)Math.Max(0, binding.ByteLength));
        if (byteOffset + byteLength > buffer.ByteLength)
        {
            return false;
        }

        var pointer = buffer.Pointer + byteOffset;
        array = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<T>(
            pointer,
            binding.Column.ElementCount,
            Allocator.None);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        var safetyHandle = AtomicSafetyHandle.Create();
        _safetyHandles.Add(safetyHandle);
        NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref array, safetyHandle);
#endif

        return true;
    }

    public bool TryCreateFirstNativeArrayOfKind<T>(
        AetheriaRuntimeDaemonSoaViewIndex index,
        string kind,
        out NativeArray<T> array)
        where T : struct
    {
        ThrowIfDisposed();
        array = default;

        if (index == null || !index.TryGetFirstColumnOfKind(kind, out var binding))
        {
            return false;
        }

        return TryCreateNativeArray(binding, out array);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        for (var i = 0; i < _safetyHandles.Count; i++)
        {
            AtomicSafetyHandle.Release(_safetyHandles[i]);
        }

        _safetyHandles.Clear();
#endif

        foreach (var buffer in _buffersById.Values)
        {
            buffer.Dispose();
        }

        _buffersById.Clear();
        _disposed = true;
    }

    private void OpenBuffer(AetheriaRuntimeDaemonSoaBufferDocument buffer)
    {
        if (string.IsNullOrWhiteSpace(buffer.BufferId) ||
            _buffersById.ContainsKey(buffer.BufferId))
        {
            return;
        }

        var byteLength = checked((ulong)(buffer.ByteOffset + buffer.ByteLength));
        var memoryMappedFile = MemoryMappedFile.OpenExisting(
            buffer.Location,
            MemoryMappedFileRights.Read);
        var accessor = memoryMappedFile.CreateViewAccessor(0, (long)byteLength, MemoryMappedFileAccess.Read);

        byte* pointer = null;
        accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref pointer);

        _buffersById.Add(
            buffer.BufferId,
            new MappedBuffer(memoryMappedFile, accessor, pointer, byteLength));
    }

    private static bool IsMappableReadOnlyBuffer(AetheriaRuntimeDaemonSoaBufferDocument buffer)
    {
        return buffer != null &&
            !buffer.ObserverWritable &&
            !string.IsNullOrWhiteSpace(buffer.Location) &&
            string.Equals(buffer.Backend, AetheriaRuntimeDaemonSoaBackends.MemoryMappedFile, StringComparison.Ordinal);
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(AetheriaDaemonSoaMemoryMap));
        }
    }

    private sealed class MappedBuffer : IDisposable
    {
        private readonly MemoryMappedFile _memoryMappedFile;
        private readonly MemoryMappedViewAccessor _accessor;
        private bool _disposed;

        public MappedBuffer(
            MemoryMappedFile memoryMappedFile,
            MemoryMappedViewAccessor accessor,
            byte* pointer,
            ulong byteLength)
        {
            _memoryMappedFile = memoryMappedFile;
            _accessor = accessor;
            Pointer = pointer;
            ByteLength = byteLength;
        }

        public byte* Pointer { get; }
        public ulong ByteLength { get; }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _accessor.SafeMemoryMappedViewHandle.ReleasePointer();
            _accessor.Dispose();
            _memoryMappedFile.Dispose();
            _disposed = true;
        }
    }
}
