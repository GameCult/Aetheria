using System;
using GameCult.Aetheria.State.Verse;
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
    private AetheriaRuntimeObservedDaemonSession _observedDaemon;

    public AetheriaRuntimeObservedDaemonState LastObservedState { get; private set; }
    public AetheriaRuntimeDaemonObservationResult LastObservation { get; private set; }
    public long LastFrameId => _cursor.LastFrameId;
    public long LastSoaGeneration => _cursor.LastSoaGeneration;
    public bool HasAuthoritativeState => LastObservedState != null && LastObservedState.IsAuthoritative;
    public bool HasSoaView => LastObservedState != null && LastObservedState.HasSoaView;
    public AetheriaRuntimeDaemonSoaViewIndex LastSoaIndex =>
        LastObservedState?.SoaIndex ?? AetheriaRuntimeDaemonSoaViewIndex.Empty;
    public AetheriaDaemonSoaMemoryMap LastSoaMemoryMap => _soaMemoryMap;
    public AetheriaDaemonRenderNativeView LastRenderNativeView => _renderNativeView;
    public bool HasRenderNativeView => _renderNativeView.IsCreated;
    public AetheriaClient Client => ResolveClient();
    public AetheriaControl Control => Client.Control;

    public event Action<AetheriaRuntimeObservedDaemonState, AetheriaRuntimeDaemonObservationResult> ObservedDaemonStateChanged;

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
        var observed = ReadObservedDaemonState();
        if (observed == null)
        {
            LastObservation = _cursor.Observe(null);
            return false;
        }

        var result = _cursor.Observe(observed);
        LastObservedState = observed;
        LastObservation = result;

        if (result.SoaViewChanged)
        {
            RemapSoaView(observed);
        }

        if (result.Changed)
        {
            ObservedDaemonStateChanged?.Invoke(observed, result);
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
        DisposeObservedDaemonSession();
        DisposeSoaMemoryMap();
        LastObservedState = null;
        LastObservation = null;
    }

    private void OnDisable()
    {
        DisposeObservedDaemonSession();
        DisposeSoaMemoryMap();
    }

    private AetheriaRuntimeObservedDaemonState ReadObservedDaemonState()
    {
        try
        {
            _observedDaemon ??= ResolveClient()?.State?.ObserveDaemon();
            return _observedDaemon?.Current;
        }
        catch
        {
            return null;
        }
    }

    private AetheriaClient ResolveClient()
    {
        var stateBoot = AetheriaRuntimeStateBoot.Inspect(AetheriaUnityRuntimePaths.GameDataDirectory);
        if (!stateBoot.SupportsLocalStateFileRead || !stateBoot.StateFileExists)
        {
            throw new InvalidOperationException(
                $"Aetheria daemon observer requires a readable local Verse state file: {stateBoot.FailureMessage}");
        }

        return AetheriaUnityRuntimeClientProvider.ResolveClient(stateBoot, clientId);
    }

    private void RemapSoaView(AetheriaRuntimeObservedDaemonState observed)
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

    private void DisposeObservedDaemonSession()
    {
        _observedDaemon?.Dispose();
        _observedDaemon = null;
    }
}
