using System.Collections.Generic;
using GameCult.Aetheria.State.Verse;
using UnityEngine;
using UnityEngine.Rendering;

public sealed class AetheriaDaemonIndirectRenderer : MonoBehaviour
{
    [SerializeField]
    private AetheriaDaemonObserver observer;

    [SerializeField]
    private AetheriaDaemonRenderAssetCatalog assetCatalog;

    [SerializeField]
    private bool logMissingAssets;

    [SerializeField]
    private Camera targetCamera;

    private MaterialPropertyBlock _properties;
    private readonly Dictionary<int, AetheriaDaemonRenderGroupBatch> _batchesByGroupId =
        new Dictionary<int, AetheriaDaemonRenderGroupBatch>();
    private readonly HashSet<int> _seenGroupIds = new HashSet<int>();
    private readonly List<int> _staleGroupIds = new List<int>();
    private readonly List<AetheriaRuntimeDaemonRenderGroupDocument> _visibleRenderGroups =
        new List<AetheriaRuntimeDaemonRenderGroupDocument>();
    private readonly Vector3[] _frustumCorners = new Vector3[8];
    private static readonly int MatrixBufferPropertyId =
        Shader.PropertyToID(AetheriaDaemonRenderBuffer.MatrixBufferPropertyName);
    private static readonly int InstanceCountPropertyId =
        Shader.PropertyToID(AetheriaDaemonRenderBuffer.InstanceCountPropertyName);

    public int LastDrawnCount { get; private set; }
    public bool HasDrawTarget =>
        observer != null &&
        assetCatalog != null;

    private void Reset()
    {
        observer = GetComponent<AetheriaDaemonObserver>();
    }

    private void LateUpdate()
    {
        DrawLatest();
    }

    public bool DrawLatest()
    {
        LastDrawnCount = 0;
        if (!HasDrawTarget)
        {
            return false;
        }

        if (!observer.HasRenderNativeView)
        {
            return false;
        }

        _properties ??= new MaterialPropertyBlock();
        var drewAny = false;
        var index = observer.LastSoaIndex;
        var view = observer.LastRenderNativeView;
        _seenGroupIds.Clear();
        var renderGroups = QueryRenderGroups(index);
        foreach (var renderGroup in renderGroups)
        {
            _seenGroupIds.Add(renderGroup.GroupId);
            if (!TryResolveMesh(renderGroup, out var mesh) ||
                !TryResolveMaterial(renderGroup, out var material))
            {
                if (logMissingAssets)
                {
                    Debug.LogWarning(
                        $"Could not resolve daemon render group {renderGroup.GroupId} " +
                        $"mesh='{renderGroup.MeshKey}' material='{renderGroup.MaterialKey}'.");
                }

                continue;
            }

            if (!_batchesByGroupId.TryGetValue(renderGroup.GroupId, out var batch))
            {
                batch = new AetheriaDaemonRenderGroupBatch();
                _batchesByGroupId.Add(renderGroup.GroupId, batch);
            }

            if (batch.LastUploadedGeneration != view.Generation)
            {
                batch.UploadLatest(view, renderGroup, mesh, renderGroup.DefaultScale);
            }

            if (!batch.HasGpuBuffers)
            {
                continue;
            }

            _properties.Clear();
            _properties.SetInt(InstanceCountPropertyId, batch.InstanceCount);
            _properties.SetBuffer(MatrixBufferPropertyId, batch.MatrixBuffer);

            Graphics.DrawMeshInstancedIndirect(
                mesh,
                renderGroup.SubMeshIndex,
                material,
                GetDrawBounds(renderGroup),
                batch.IndirectArgsBuffer,
                0,
                _properties,
                GetShadowCastingMode(renderGroup),
                renderGroup.ReceiveShadows,
                renderGroup.Layer,
                targetCamera);

            LastDrawnCount += batch.InstanceCount;
            drewAny = true;
        }

        ReleaseStaleBatches();
        return drewAny;
    }

    private IReadOnlyList<AetheriaRuntimeDaemonRenderGroupDocument> QueryRenderGroups(
        AetheriaRuntimeDaemonSoaViewIndex index)
    {
        if (!TryGetCameraQueryBounds(out var min, out var max))
        {
            return index.RenderGroups;
        }

        AetheriaRuntimeDaemonRenderQueries.QueryRenderGroups(
            index,
            min.x,
            min.y,
            min.z,
            max.x,
            max.y,
            max.z,
            _visibleRenderGroups);
        return _visibleRenderGroups;
    }

    private bool TryGetCameraQueryBounds(out Vector3 min, out Vector3 max)
    {
        var camera = targetCamera != null ? targetCamera : Camera.main;
        if (camera == null || !camera.enabled)
        {
            min = default;
            max = default;
            return false;
        }

        var near = Mathf.Max(0.0f, camera.nearClipPlane);
        var far = Mathf.Max(near, camera.farClipPlane);
        _frustumCorners[0] = camera.ViewportToWorldPoint(new Vector3(0, 0, near));
        _frustumCorners[1] = camera.ViewportToWorldPoint(new Vector3(1, 0, near));
        _frustumCorners[2] = camera.ViewportToWorldPoint(new Vector3(0, 1, near));
        _frustumCorners[3] = camera.ViewportToWorldPoint(new Vector3(1, 1, near));
        _frustumCorners[4] = camera.ViewportToWorldPoint(new Vector3(0, 0, far));
        _frustumCorners[5] = camera.ViewportToWorldPoint(new Vector3(1, 0, far));
        _frustumCorners[6] = camera.ViewportToWorldPoint(new Vector3(0, 1, far));
        _frustumCorners[7] = camera.ViewportToWorldPoint(new Vector3(1, 1, far));

        min = _frustumCorners[0];
        max = _frustumCorners[0];
        for (var i = 1; i < _frustumCorners.Length; i++)
        {
            min = Vector3.Min(min, _frustumCorners[i]);
            max = Vector3.Max(max, _frustumCorners[i]);
        }

        return true;
    }

    private Bounds GetDrawBounds(AetheriaRuntimeDaemonRenderGroupDocument renderGroup)
    {
        return new Bounds(
            new Vector3(
                renderGroup.BoundsCenterX,
                renderGroup.BoundsCenterY,
                renderGroup.BoundsCenterZ),
            new Vector3(
                renderGroup.BoundsSizeX,
                renderGroup.BoundsSizeY,
                renderGroup.BoundsSizeZ));
    }

    private static ShadowCastingMode GetShadowCastingMode(AetheriaRuntimeDaemonRenderGroupDocument renderGroup)
    {
        switch (renderGroup.ShadowMode)
        {
            case AetheriaRuntimeDaemonRenderShadowModes.Off:
                return ShadowCastingMode.Off;
            case AetheriaRuntimeDaemonRenderShadowModes.TwoSided:
                return ShadowCastingMode.TwoSided;
            case AetheriaRuntimeDaemonRenderShadowModes.ShadowsOnly:
                return ShadowCastingMode.ShadowsOnly;
            default:
                return ShadowCastingMode.On;
        }
    }

    private void OnDisable()
    {
        foreach (var batch in _batchesByGroupId.Values)
        {
            batch.Dispose();
        }

        _batchesByGroupId.Clear();
        _seenGroupIds.Clear();
    }

    private void ReleaseStaleBatches()
    {
        if (_batchesByGroupId.Count == _seenGroupIds.Count)
        {
            return;
        }

        _staleGroupIds.Clear();
        foreach (var groupId in _batchesByGroupId.Keys)
        {
            if (!_seenGroupIds.Contains(groupId))
            {
                _staleGroupIds.Add(groupId);
            }
        }

        for (var i = 0; i < _staleGroupIds.Count; i++)
        {
            var groupId = _staleGroupIds[i];
            _batchesByGroupId[groupId].Dispose();
            _batchesByGroupId.Remove(groupId);
        }

        _staleGroupIds.Clear();
    }

    private bool TryResolveMesh(AetheriaRuntimeDaemonRenderGroupDocument renderGroup, out Mesh mesh)
    {
        if (renderGroup?.MeshAsset != null &&
            !string.IsNullOrWhiteSpace(renderGroup.MeshAsset.AssetKey) &&
            assetCatalog.TryResolveMesh(renderGroup.MeshAsset, out mesh))
        {
            return true;
        }

        mesh = null;
        return false;
    }

    private bool TryResolveMaterial(AetheriaRuntimeDaemonRenderGroupDocument renderGroup, out Material material)
    {
        if (renderGroup?.MaterialAsset != null &&
            !string.IsNullOrWhiteSpace(renderGroup.MaterialAsset.AssetKey) &&
            assetCatalog.TryResolveMaterial(renderGroup.MaterialAsset, out material))
        {
            return true;
        }

        material = null;
        return false;
    }
}
