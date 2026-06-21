using System;
using GameCult.Aetheria.State.Verse;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

public sealed class AetheriaDaemonRenderGroupBatch : IDisposable
{
    private const int MatrixStrideBytes = 64;
    private const int DrawMeshInstancedIndirectArgsCount = 5;

    private readonly uint[] _indirectArgs = new uint[DrawMeshInstancedIndirectArgsCount];
    private NativeArray<float4x4> _nativeMatrices;
    private GraphicsBuffer _matrixBuffer;
    private GraphicsBuffer _indirectArgsBuffer;
    private int _capacity;

    public int GroupId { get; private set; }
    public int InstanceCount { get; private set; }
    public long LastUploadedFrameId { get; private set; }
    public long LastUploadedGeneration { get; private set; }
    public GraphicsBuffer MatrixBuffer => _matrixBuffer;
    public GraphicsBuffer IndirectArgsBuffer => _indirectArgsBuffer;
    public bool HasGpuBuffers =>
        _matrixBuffer != null &&
        _matrixBuffer.IsValid() &&
        _indirectArgsBuffer != null &&
        _indirectArgsBuffer.IsValid() &&
        InstanceCount > 0;

    public bool UploadLatest(
        AetheriaDaemonRenderNativeView view,
        AetheriaRuntimeDaemonRenderGroupDocument renderGroup,
        Mesh mesh,
        float uniformScale)
    {
        if (!view.IsCreated || mesh == null)
        {
            InstanceCount = 0;
            UpdateIndirectArgs(mesh, 0, renderGroup?.SubMeshIndex ?? 0);
            return false;
        }

        var targetCapacity = renderGroup.InstanceCount >= 0
            ? math.min(renderGroup.InstanceCount, view.Count)
            : view.Count;
        EnsureCapacity(targetCapacity);
        if (!_nativeMatrices.IsCreated || _nativeMatrices.Length == 0)
        {
            InstanceCount = 0;
            UpdateIndirectArgs(mesh, 0, renderGroup.SubMeshIndex);
            return false;
        }

        var count = view.HasRenderGroupId
            ? BuildRenderGroupMatrices(view, renderGroup.GroupId, renderGroup.Lod, uniformScale)
            : BuildAllMatrices(view, renderGroup.Lod, uniformScale);

        _matrixBuffer.SetData(_nativeMatrices, 0, 0, count);
        GroupId = renderGroup.GroupId;
        InstanceCount = count;
        LastUploadedFrameId = view.FrameId;
        LastUploadedGeneration = view.Generation;
        UpdateIndirectArgs(mesh, count, renderGroup.SubMeshIndex);
        return count > 0;
    }

    public void Dispose()
    {
        if (_nativeMatrices.IsCreated)
        {
            _nativeMatrices.Dispose();
        }

        _matrixBuffer?.Release();
        _matrixBuffer = null;
        _indirectArgsBuffer?.Release();
        _indirectArgsBuffer = null;
        _capacity = 0;
        InstanceCount = 0;
    }

    private int BuildAllMatrices(AetheriaDaemonRenderNativeView view, int renderLod, float uniformScale)
    {
        return BuildCompactedMatrices(view, -1, renderLod, uniformScale);
    }

    private int BuildRenderGroupMatrices(
        AetheriaDaemonRenderNativeView view,
        int renderGroupId,
        int renderLod,
        float uniformScale)
    {
        return BuildCompactedMatrices(view, renderGroupId, renderLod, uniformScale);
    }

    private int BuildCompactedMatrices(
        AetheriaDaemonRenderNativeView view,
        int renderGroupId,
        int renderLod,
        float uniformScale)
    {
        using var writtenCount = new NativeArray<int>(1, Allocator.TempJob);
        var job = new AetheriaDaemonRenderGroupMatrixJob
        {
            PositionX = view.PositionX,
            PositionY = view.PositionY,
            PositionZ = view.PositionZ,
            RotationRadians = view.RotationRadians,
            PhysicsBodyRadius = view.PhysicsBodyRadius,
            RenderScale = view.RenderScale,
            RenderVisibility = view.RenderVisibility,
            RenderLod = view.RenderLod,
            RenderGroupId = view.RenderGroupId,
            Matrices = _nativeMatrices,
            WrittenCount = writtenCount,
            TargetRenderGroupId = renderGroupId >= 0 ? (uint)renderGroupId : 0,
            TargetRenderLod = renderLod,
            HasRotation = view.HasRotation,
            HasPhysicsRadius = view.HasPhysicsRadius,
            HasRenderScale = view.HasRenderScale,
            HasRenderVisibility = view.HasRenderVisibility,
            HasRenderLod = view.HasRenderLod,
            HasRenderGroupFilter = renderGroupId >= 0,
            HasRenderLodFilter = renderLod >= 0,
            UniformScale = uniformScale
        };

        job.Schedule().Complete();
        return math.clamp(writtenCount[0], 0, _nativeMatrices.Length);
    }

    private void EnsureCapacity(int capacity)
    {
        capacity = math.max(0, capacity);
        if (_nativeMatrices.IsCreated && _capacity == capacity)
        {
            return;
        }

        Dispose();
        _capacity = capacity;
        if (capacity <= 0)
        {
            return;
        }

        _nativeMatrices = new NativeArray<float4x4>(
            capacity,
            Allocator.Persistent,
            NativeArrayOptions.UninitializedMemory);
        _matrixBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, capacity, MatrixStrideBytes);
        _indirectArgsBuffer = new GraphicsBuffer(
            GraphicsBuffer.Target.IndirectArguments,
            DrawMeshInstancedIndirectArgsCount,
            sizeof(uint));
    }

    private void UpdateIndirectArgs(Mesh mesh, int instanceCount, int subMeshIndex)
    {
        if (_indirectArgsBuffer == null || !_indirectArgsBuffer.IsValid())
        {
            return;
        }

        var safeSubMeshIndex = mesh != null
            ? math.clamp(subMeshIndex, 0, math.max(0, mesh.subMeshCount - 1))
            : 0;
        _indirectArgs[0] = mesh != null ? mesh.GetIndexCount(safeSubMeshIndex) : 0;
        _indirectArgs[1] = (uint)math.max(0, instanceCount);
        _indirectArgs[2] = mesh != null ? mesh.GetIndexStart(safeSubMeshIndex) : 0;
        _indirectArgs[3] = mesh != null ? mesh.GetBaseVertex(safeSubMeshIndex) : 0;
        _indirectArgs[4] = 0;
        _indirectArgsBuffer.SetData(_indirectArgs);
    }
}
