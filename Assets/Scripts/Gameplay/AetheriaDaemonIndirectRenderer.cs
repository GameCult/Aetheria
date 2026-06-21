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
        foreach (var renderGroup in index.RenderGroups)
        {
            _seenGroupIds.Add(renderGroup.GroupId);
            if (!assetCatalog.TryResolveMesh(renderGroup.MeshKey, out var mesh) ||
                !assetCatalog.TryResolveMaterial(renderGroup.MaterialKey, out var material))
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
}
