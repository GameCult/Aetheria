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
    private AetheriaRuntimeDaemonOperationClient _operationClient;
    private AetheriaDaemonOperations _operations;
    private AetheriaDaemonSoaMemoryMap _soaMemoryMap;
    private AetheriaDaemonRenderNativeView _renderNativeView;

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
    public AetheriaDaemonOperations Operations => _operations ??= new AetheriaDaemonOperations(this);

    public event Action<AetheriaRuntimeObservedDaemonState, AetheriaRuntimeDaemonObservationResult> ObservedDaemonStateChanged;

    internal AetheriaRuntimeDaemonCommandEnvelope SendOperation(
        Func<AetheriaRuntimeDaemonOperationClient, AetheriaRuntimeObservedDaemonState, AetheriaRuntimeDaemonCommandEnvelope> submit)
    {
        if (submit == null)
        {
            throw new ArgumentNullException(nameof(submit));
        }

        _operationClient ??= new AetheriaRuntimeDaemonOperationClient(
            ActionGameManager.RuntimeStateFilePath,
            clientId,
            LastObservedState?.Frame.SessionId ?? "local");

        var envelope = submit(_operationClient, LastObservedState);

        if (logChanges)
        {
            Debug.Log($"Submitted Aetheria daemon operation {envelope.Kind}: {envelope.CommandId}");
        }

        return envelope;
    }

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
        if (!AetheriaRuntimeStateReader.TryReadObservedDaemonState(
                ActionGameManager.RuntimeStateFilePath,
                out var observed))
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
        DisposeSoaMemoryMap();
        LastObservedState = null;
        LastObservation = null;
    }

    private void OnDisable()
    {
        DisposeSoaMemoryMap();
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
}
