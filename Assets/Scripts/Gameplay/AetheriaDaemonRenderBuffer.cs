using GameCult.Aetheria.State.Unity;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

public sealed class AetheriaDaemonRenderBuffer : MonoBehaviour
{
    public const string MatrixBufferPropertyName = "_AetheriaObjectToWorld";
    public const string InstanceCountPropertyName = "_AetheriaInstanceCount";

    private const int MatrixStrideBytes = 64;
    private const int DrawMeshInstancedIndirectArgsCount = 5;

    [SerializeField]
    private AetheriaDaemonObserver observer;

    [SerializeField]
    private Mesh indirectArgsMesh;

    [SerializeField]
    private int innerLoopBatchCount = 64;

    [SerializeField]
    private bool updateInLateUpdate;

    private readonly uint[] _indirectArgs = new uint[DrawMeshInstancedIndirectArgsCount];
    private NativeArray<float4x4> _nativeMatrices;
    private GraphicsBuffer _matrixBuffer;
    private GraphicsBuffer _indirectArgsBuffer;
    private long _allocatedGeneration = long.MinValue;
    private int _allocatedCount;

    public GraphicsBuffer MatrixBuffer => _matrixBuffer;
    public GraphicsBuffer IndirectArgsBuffer => _indirectArgsBuffer;
    public int InstanceCount { get; private set; }
    public long LastUploadedFrameId { get; private set; }
    public long LastUploadedGeneration { get; private set; }
    public bool HasRenderableView => observer != null && observer.HasRenderNativeView;
    public bool HasGpuBuffers =>
        _matrixBuffer != null &&
        _matrixBuffer.IsValid() &&
        InstanceCount > 0;

    private static readonly int MatrixBufferPropertyId = Shader.PropertyToID(MatrixBufferPropertyName);
    private static readonly int InstanceCountPropertyId = Shader.PropertyToID(InstanceCountPropertyName);

    private void Reset()
    {
        observer = GetComponent<AetheriaDaemonObserver>();
    }

    private void LateUpdate()
    {
        if (updateInLateUpdate)
        {
            UploadLatest();
        }
    }

    public bool UploadLatest()
    {
        return UploadLatest(indirectArgsMesh);
    }

    public bool UploadLatest(Mesh argsMesh)
    {
        return UploadLatest(argsMesh, -1);
    }

    public bool UploadLatest(Mesh argsMesh, int renderGroupId)
    {
        return UploadLatest(argsMesh, renderGroupId, 0);
    }

    public bool UploadLatest(Mesh argsMesh, int renderGroupId, int subMeshIndex)
    {
        if (!HasRenderableView)
        {
            InstanceCount = 0;
            UpdateIndirectArgs(null, 0);
            return false;
        }

        var view = observer.LastRenderNativeView;
        EnsureCapacity(view);
        if (!_nativeMatrices.IsCreated || _nativeMatrices.Length == 0)
        {
            InstanceCount = 0;
            UpdateIndirectArgs(argsMesh, 0);
            return false;
        }

        var index = observer.LastSoaIndex;
        var defaultScale = ResolveDefaultScale(index, renderGroupId);
        var renderLod = ResolveRenderLod(index, renderGroupId);
        var count = renderGroupId >= 0 && view.HasRenderGroupId
            ? BuildRenderGroupMatrices(view, renderGroupId, renderLod, defaultScale)
            : BuildAllMatrices(view, renderLod, defaultScale);

        _matrixBuffer.SetData(_nativeMatrices, 0, 0, count);
        InstanceCount = count;
        LastUploadedFrameId = view.FrameId;
        LastUploadedGeneration = view.Generation;
        UpdateIndirectArgs(argsMesh, count, subMeshIndex);
        return true;
    }

    private int BuildAllMatrices(AetheriaDaemonRenderNativeView view, int renderLod, float defaultScale)
    {
        if (view.HasRenderVisibility || renderLod >= 0)
        {
            return BuildCompactedMatrices(view, -1, renderLod, defaultScale);
        }

        var count = math.min(view.Count, _nativeMatrices.Length);
        var handle = view.ScheduleBuildRenderMatrices(
            _nativeMatrices,
            defaultScale,
            innerLoopBatchCount);
        handle.Complete();
        return count;
    }

    private int BuildRenderGroupMatrices(
        AetheriaDaemonRenderNativeView view,
        int renderGroupId,
        int renderLod,
        float defaultScale)
    {
        return BuildCompactedMatrices(view, renderGroupId, renderLod, defaultScale);
    }

    private int BuildCompactedMatrices(
        AetheriaDaemonRenderNativeView view,
        int renderGroupId,
        int renderLod,
        float defaultScale)
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
            UniformScale = defaultScale
        };

        job.Schedule().Complete();
        return math.clamp(writtenCount[0], 0, _nativeMatrices.Length);
    }

    private static float ResolveDefaultScale(AetheriaRuntimeDaemonSoaViewIndex index, int renderGroupId)
    {
        if (index != null &&
            renderGroupId >= 0 &&
            index.TryGetRenderGroup(renderGroupId, out var renderGroup))
        {
            return math.max(renderGroup.DefaultScale, 0.0001f);
        }

        var renderGroups = index?.RenderGroups;
        if (renderGroups != null && renderGroups.Count == 1)
        {
            return math.max(renderGroups[0].DefaultScale, 0.0001f);
        }

        return 1.0f;
    }

    private static int ResolveRenderLod(AetheriaRuntimeDaemonSoaViewIndex index, int renderGroupId)
    {
        if (index != null &&
            renderGroupId >= 0 &&
            index.TryGetRenderGroup(renderGroupId, out var renderGroup))
        {
            return renderGroup.Lod;
        }

        var renderGroups = index?.RenderGroups;
        if (renderGroups != null && renderGroups.Count == 1)
        {
            return renderGroups[0].Lod;
        }

        return -1;
    }

    public void ApplyTo(MaterialPropertyBlock properties)
    {
        if (properties == null)
        {
            return;
        }

        properties.SetInt(InstanceCountPropertyId, InstanceCount);
        if (HasGpuBuffers)
        {
            properties.SetBuffer(MatrixBufferPropertyId, _matrixBuffer);
        }
    }

    private void OnDisable()
    {
        ReleaseBuffers();
        InstanceCount = 0;
        LastUploadedFrameId = 0;
        LastUploadedGeneration = 0;
    }

    private void EnsureCapacity(AetheriaDaemonRenderNativeView view)
    {
        var count = math.max(0, view.Count);
        if (_nativeMatrices.IsCreated &&
            _allocatedGeneration == view.Generation &&
            _allocatedCount == count)
        {
            return;
        }

        ReleaseBuffers();
        _allocatedGeneration = view.Generation;
        _allocatedCount = count;
        if (count <= 0)
        {
            return;
        }

        _nativeMatrices = new NativeArray<float4x4>(
            count,
            Allocator.Persistent,
            NativeArrayOptions.UninitializedMemory);
        _matrixBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, count, MatrixStrideBytes);
        _indirectArgsBuffer = new GraphicsBuffer(
            GraphicsBuffer.Target.IndirectArguments,
            DrawMeshInstancedIndirectArgsCount,
            sizeof(uint));
    }

    private void UpdateIndirectArgs(Mesh mesh, int instanceCount)
    {
        UpdateIndirectArgs(mesh, instanceCount, 0);
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

    private void ReleaseBuffers()
    {
        if (_nativeMatrices.IsCreated)
        {
            _nativeMatrices.Dispose();
        }

        _matrixBuffer?.Release();
        _matrixBuffer = null;
        _indirectArgsBuffer?.Release();
        _indirectArgsBuffer = null;
        _allocatedGeneration = long.MinValue;
        _allocatedCount = 0;
    }
}
