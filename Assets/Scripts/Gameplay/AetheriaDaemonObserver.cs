using System;
using GameCult.Aetheria.State.Verse;
using GameCult.Mesh;
using UnityEngine;

public sealed class AetheriaDaemonObserver : MonoBehaviour
{
    [SerializeField]
    private bool pollInUpdate = true;

    [SerializeField]
    private float pollIntervalSeconds = 0.05f;

    [SerializeField]
    private bool logChanges;

    [SerializeField]
    private string clientId = "unity-observer";

    private readonly AetheriaRuntimeDaemonObservationCursor _cursor = new AetheriaRuntimeDaemonObservationCursor();
    private float _nextPollTime;
    private AetheriaDaemonSoaMemoryMap _soaMemoryMap;
    private AetheriaDaemonRenderNativeView _renderNativeView;
    private CultMeshReactiveDocument<AetheriaRuntimeDaemonFrameDocument> _daemonFrame;
    private CultMeshReactiveDocument<AetheriaRuntimeDaemonSoaViewDocument> _daemonSoaView;
    private CultMeshReactiveDocument<AetheriaRuntimeZoneRenderDocument> _zoneRender;

    public AetheriaRuntimeDaemonRenderView LastRenderView { get; private set; }
    public AetheriaRuntimeDaemonObservationResult LastObservation { get; private set; }
    public long LastFrameId => _cursor.LastFrameId;
    public long LastSoaGeneration => _cursor.LastSoaGeneration;
    public bool HasAuthoritativeState => LastRenderView != null && LastRenderView.IsAuthoritative;
    public bool HasSoaView => LastRenderView != null && LastRenderView.HasSoaView;
    public AetheriaRuntimeDaemonSoaViewIndex LastSoaIndex =>
        LastRenderView?.SoaIndex ?? AetheriaRuntimeDaemonSoaViewIndex.Empty;
    public AetheriaDaemonSoaMemoryMap LastSoaMemoryMap => _soaMemoryMap;
    public AetheriaDaemonRenderNativeView LastRenderNativeView => _renderNativeView;
    public bool HasRenderNativeView => _renderNativeView.IsCreated;

    public event Action<AetheriaRuntimeDaemonRenderView, AetheriaRuntimeDaemonObservationResult> DaemonRenderViewChanged;

    private void Update()
    {
        if (!pollInUpdate || Time.unscaledTime < _nextPollTime)
        {
            return;
        }

        _nextPollTime = Time.unscaledTime + Mathf.Max(0.001f, pollIntervalSeconds);
        Poll();
    }

    public bool Poll()
    {
        var observed = ReadDaemonRenderView();
        if (observed == null)
        {
            LastObservation = _cursor.Observe(null);
            return false;
        }

        var result = _cursor.Observe(observed);
        LastRenderView = observed;
        LastObservation = result;

        if (result.SoaViewChanged)
        {
            RemapSoaView(observed);
        }

        if (result.Changed)
        {
            DaemonRenderViewChanged?.Invoke(observed, result);
            if (logChanges)
            {
                Debug.Log(
                    $"Observed Aetheria daemon frame {result.FrameId} " +
                    $"soa generation {result.SoaGeneration} authoritative={result.IsAuthoritative}");
            }
        }

        return true;
    }

    public void ResetObservation()
    {
        _cursor.Reset();
        DisposeRenderViewDocuments();
        DisposeSoaMemoryMap();
        LastRenderView = null;
        LastObservation = null;
    }

    private void OnDisable()
    {
        DisposeRenderViewDocuments();
        DisposeSoaMemoryMap();
    }

    private AetheriaRuntimeDaemonRenderView ReadDaemonRenderView()
    {
        try
        {
            EnsureRenderViewDocuments();
            return AetheriaRuntimeDaemonRenderView.TryCreateCurrent(
                _daemonFrame,
                _daemonSoaView,
                _zoneRender,
                out var observed)
                ? observed
                : null;
        }
        catch
        {
            return null;
        }
    }

    private void EnsureRenderViewDocuments()
    {
        if (_daemonFrame != null && _daemonSoaView != null && _zoneRender != null)
        {
            return;
        }

        var state = ResolveState();
        if (state == null)
        {
            return;
        }

        _daemonFrame ??= state.Reactive<AetheriaRuntimeDaemonFrameDocument>();
        _daemonSoaView ??= TryReactive<AetheriaRuntimeDaemonSoaViewDocument>(state);
        _zoneRender ??= state.Reactive<AetheriaRuntimeZoneRenderDocument>();
    }

    private static CultMeshReactiveDocument<TDocument> TryReactive<TDocument>(
        AetheriaClientState state)
        where TDocument : class
    {
        try
        {
            return state.Reactive<TDocument>();
        }
        catch (System.Collections.Generic.KeyNotFoundException)
        {
            return null;
        }
    }

    private AetheriaClientState ResolveState()
    {
        var stateBoot = AetheriaRuntimeStateBoot.Inspect(AetheriaUnityRuntimePaths.GameDataDirectory);
        if (!stateBoot.SupportsLocalStateFileRead || !stateBoot.StateFileExists)
        {
            throw new InvalidOperationException(
                $"Aetheria daemon observer requires a readable local Verse state file: {stateBoot.FailureMessage}");
        }

        return AetheriaUnityRuntimeClientProvider.RuntimeState(stateBoot, clientId);
    }

    private void RemapSoaView(AetheriaRuntimeDaemonRenderView observed)
    {
        DisposeSoaMemoryMap();
        if (observed == null || !observed.HasSoaView)
        {
            return;
        }

        if (AetheriaDaemonSoaMemoryMap.TryOpen(observed.SoaIndex, out var map, out var error))
        {
            _soaMemoryMap = map;
            if (!AetheriaDaemonRenderNativeView.TryCreate(observed.SoaIndex, map, out _renderNativeView))
            {
                _renderNativeView = default;
                if (logChanges)
                {
                    Debug.LogWarning("Mapped Aetheria daemon SoA memory, but no complete render native view was published.");
                }
            }

            return;
        }

        if (logChanges)
        {
            Debug.LogWarning($"Could not map Aetheria daemon SoA view read-only: {error}");
        }
    }

    private void DisposeSoaMemoryMap()
    {
        _renderNativeView = default;
        _soaMemoryMap?.Dispose();
        _soaMemoryMap = null;
    }

    private void DisposeRenderViewDocuments()
    {
        _daemonFrame?.Dispose();
        _daemonFrame = null;
        _daemonSoaView?.Dispose();
        _daemonSoaView = null;
        _zoneRender?.Dispose();
        _zoneRender = null;
    }
}
