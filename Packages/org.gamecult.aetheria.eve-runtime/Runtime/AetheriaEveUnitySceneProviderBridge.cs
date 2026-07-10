using System;
using System.Collections.Generic;
using System.Linq;
using GameCult.Aetheria.State.Verse;
using GameCult.Eve.Surface;
using GameCult.Eve.UnityScene;
using GameCult.Mesh;
using UnityEngine;
using EveSurfaceDocument = GameCult.Eve.Surface.EveSurfaceDocument;

#nullable enable

namespace GameCult.Aetheria.EveRuntime
{
    public sealed class AetheriaEveUnitySceneProviderBridge :
        IEveUnitySceneLiveProviderTransport,
        IEveUnitySceneProviderSurfaceDocumentSource,
        IEveUnityPlayableWorldAssetManifestDocumentSource,
        IEveUnitySceneCommandSink,
        IEveUnitySceneCommandReceiptSource,
        IEveUnityProviderRefreshSource,
        IDisposable
    {
        private readonly string _stateFilePathOverride;
        private readonly string _surfaceId;
        private readonly string _runtimeId;
        private readonly string _cultMeshEndpoint;
        private AetheriaRuntimeStateBootReport? _stateBoot;
        private AetheriaClientState? _runtimeState;
        private AetheriaClient? _remoteClient;
        private readonly AetheriaEveUnityRemoteReceiptTracker _remoteReceipts =
            new AetheriaEveUnityRemoteReceiptTracker();
        private event Action<EveUnityPlayableWorldAssetManifestDocument>? AssetManifestDocumentAvailable;

        public AetheriaEveUnitySceneProviderBridge(
            string stateFilePathOverride = "",
            string surfaceId = AetheriaRuntimeDaemonGameSurfaceBuilder.SurfaceId,
            string runtimeId = "unity-scene",
            string cultMeshEndpoint = "")
        {
            _stateFilePathOverride = stateFilePathOverride ?? "";
            _surfaceId = string.IsNullOrWhiteSpace(surfaceId)
                ? AetheriaRuntimeDaemonGameSurfaceBuilder.SurfaceId
                : surfaceId;
            _runtimeId = string.IsNullOrWhiteSpace(runtimeId) ? "unity-scene" : runtimeId;
            _cultMeshEndpoint = cultMeshEndpoint?.Trim() ?? "";
            CurrentDocument = CreateSurfaceDocument(AetheriaRuntimeSurfaceDocuments.EmptySurface(_surfaceId), 0);
            CurrentDocumentManifest = new EveUnityPlayableWorldAssetManifestDocument(
                AetheriaRuntimeVerseRecordKeys.DaemonAssetManifest.ToString(),
                Array.Empty<EveUnityPlayableWorldAssetManifestDocumentEntry>(),
                "aetheria.daemon");
        }

        public string TransportKind => IsRemote
            ? "aetheria-remote-cultmesh-replica"
            : "aetheria-local-cultmesh-replica";

        public string SurfacePointer => CurrentDocument.SourcePointer;

        public string AssetManifestPointer => CurrentDocumentManifest.ManifestRef;

        public string SinkKind => "aetheria-daemon-command-boundary";

        public string ManifestRef => CurrentDocumentManifest.ManifestRef;

        public EveUnitySceneProviderSurfaceDocument CurrentDocument { get; private set; }

        EveUnitySceneProviderSurfaceDocument IEveUnitySceneLiveProviderTransport.CurrentSurfaceDocument =>
            CurrentDocument;

        EveUnityPlayableWorldAssetManifestDocument IEveUnityPlayableWorldAssetManifestDocumentSource.CurrentDocument =>
            CurrentDocumentManifest;

        EveUnityPlayableWorldAssetManifestDocument IEveUnitySceneLiveProviderTransport.CurrentAssetManifestDocument =>
            CurrentDocumentManifest;

        public EveUnityPlayableWorldAssetManifestDocument CurrentDocumentManifest { get; private set; }

        public event Action<EveUnitySceneProviderSurfaceDocument>? DocumentAvailable;

        public event Action<EveUnitySceneCommandReceipt>? ReceiptAvailable;

        event Action<EveUnitySceneProviderSurfaceDocument> IEveUnitySceneLiveProviderTransport.SurfaceDocumentAvailable
        {
            add => DocumentAvailable += value;
            remove => DocumentAvailable -= value;
        }

        event Action<EveUnityPlayableWorldAssetManifestDocument> IEveUnityPlayableWorldAssetManifestDocumentSource.DocumentAvailable
        {
            add => AssetManifestDocumentAvailable += value;
            remove => AssetManifestDocumentAvailable -= value;
        }

        event Action<EveUnityPlayableWorldAssetManifestDocument> IEveUnitySceneLiveProviderTransport.AssetManifestDocumentAvailable
        {
            add => AssetManifestDocumentAvailable += value;
            remove => AssetManifestDocumentAvailable -= value;
        }

        event Action<EveUnitySceneCommandReceipt> IEveUnitySceneLiveProviderTransport.CommandReceiptAvailable
        {
            add => ReceiptAvailable += value;
            remove => ReceiptAvailable -= value;
        }

        public void Connect()
        {
            Refresh();
        }

        public void Disconnect()
        {
        }

        public void Refresh()
        {
            if (IsRemote)
            {
                var remote = ResolveRemoteClient();
                remote.RefreshRemoteAsync().GetAwaiter().GetResult();
                RefreshFromRuntimeState(remote.State, null);
                return;
            }

            var stateBoot = ResolveStateBoot();
            if (!stateBoot.SupportsLocalStateFileRead || !stateBoot.StateFileExists)
                return;

            RefreshFromRuntimeState(ResolveRuntimeState(stateBoot), stateBoot);
        }

        private void RefreshFromRuntimeState(
            AetheriaClientState runtimeState,
            AetheriaRuntimeStateBootReport? stateBoot)
        {
            var surface = ReadSurface(runtimeState, stateBoot);
            if (surface != null)
            {
                CurrentDocument = CreateSurfaceDocument(
                    surface,
                    surface.Version,
                    ReadAdvertisedSurface(runtimeState));
                DocumentAvailable?.Invoke(CurrentDocument);
            }

            CurrentDocumentManifest = ReadAssetManifest(runtimeState);
            AssetManifestDocumentAvailable?.Invoke(CurrentDocumentManifest);
            ReconcileRemoteReceipts();
        }

        public void Submit(EveSurfaceCommandRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            var clientId = string.IsNullOrWhiteSpace(request.ClientId) ? _runtimeId : request.ClientId;
            if (IsRemote)
            {
                var remote = ResolveRemoteClient();
                if (!remote.Control.TrySubmitSurfaceCommand(request, out var submitted))
                    throw new InvalidOperationException($"Aetheria daemon did not advertise command {request.Operation?.OperationId ?? "unknown"}.");

                Debug.Log(
                    $"Forwarded Aetheria operation to remote CultMesh authority: {submitted!.Kind} {submitted.CommandId}");
                _remoteReceipts.Track(submitted.CommandId, request);
                return;
            }

            var stateBoot = ResolveStateBoot();
            var control = AetheriaEveRuntimeUnityHooks.RequireControl(stateBoot, clientId);
            if (control.TrySubmitSurfaceCommand(request, out var daemonEnvelope))
            {
                Debug.Log(
                    $"Submitted Aetheria daemon operation from Eve Unity scene: {daemonEnvelope!.Kind} {daemonEnvelope.CommandId}");
                ReceiptAvailable?.Invoke(ToReceipt(request, daemonEnvelope));
                return;
            }

            var envelope = AetheriaEveRuntimeUnityHooks
                .RequireUi(stateBoot, _runtimeId)
                .SurfaceCommandAsync(request, clientId)
                .GetAwaiter()
                .GetResult();
            Debug.Log($"Submitted Eve Unity scene operation for CultMesh bridge: {envelope.OperationId}");
            ReceiptAvailable?.Invoke(ToReceipt(request, envelope));
        }

        public void SubmitCommand(EveSurfaceCommandRequest request)
        {
            Submit(request);
        }

        public void Dispose()
        {
            _remoteClient?.Dispose();
            _remoteClient = null;
            _remoteReceipts.Clear();
        }

        private bool IsRemote => !string.IsNullOrWhiteSpace(_cultMeshEndpoint);

        private AetheriaClient ResolveRemoteClient()
        {
            if (_remoteClient != null)
                return _remoteClient;

            var replicaPath = string.IsNullOrWhiteSpace(_stateFilePathOverride)
                ? System.IO.Path.Combine(Application.persistentDataPath, "aetheria-remote-replica.cc")
                : _stateFilePathOverride;
            _remoteClient = AetheriaClient
                .OpenRemoteAsync(
                    replicaPath,
                    _cultMeshEndpoint,
                    _runtimeId,
                    sessionId: "eve-unity",
                    synchronizeOnOpen: false)
                .GetAwaiter()
                .GetResult();
            return _remoteClient;
        }

        private void ReconcileRemoteReceipts()
        {
            if (!IsRemote || !_remoteReceipts.HasPending || _remoteClient == null)
                return;

            foreach (var receipt in _remoteReceipts.Reconcile(
                         _remoteClient.CommittedCommandFacts(),
                         CurrentDocument.Version))
                ReceiptAvailable?.Invoke(receipt);
        }

        private AetheriaRuntimeStateBootReport ResolveStateBoot()
        {
            var stateBoot = AetheriaEveRuntimeUnityHooks.RequireStateBoot(_stateFilePathOverride);
            _stateBoot = stateBoot;
            return stateBoot;
        }

        private AetheriaClientState ResolveRuntimeState(AetheriaRuntimeStateBootReport stateBoot)
        {
            var runtimeState = AetheriaEveRuntimeUnityHooks.RequireRuntimeState(stateBoot, _runtimeId);
            _runtimeState = runtimeState;
            return runtimeState;
        }

        private EveSurfaceDocument? ReadSurface(
            AetheriaClientState runtimeState,
            AetheriaRuntimeStateBootReport? stateBoot)
        {
            try
            {
                var handle = runtimeState.EveSurfaceDocument(_surfaceId);
                if (handle == null)
                    return null;

                var resolver = stateBoot == null
                    ? null
                    : AetheriaEveRuntimeUnityHooks.TryCreateStateRefResolver(stateBoot.Value, _runtimeId);
                return AetheriaRuntimeSurfaceDocuments.ToEveSurfaceDocument(handle.Latest(), resolver);
            }
            catch (KeyNotFoundException)
            {
                return null;
            }
        }

        private EveUnityPlayableWorldAssetManifestDocument ReadAssetManifest(AetheriaClientState runtimeState)
        {
            var manifest = runtimeState.AssetManifest.Latest();
            return ToUnityPlayableWorldAssetManifest(manifest);
        }

        private AetheriaRuntimeEveSurfaceAdvertisement? ReadAdvertisedSurface(AetheriaClientState runtimeState)
        {
            var provider = runtimeState.ProviderAdvertisement.Latest();
            return (provider.EveSurfaces ?? Array.Empty<AetheriaRuntimeEveSurfaceAdvertisement>())
                .FirstOrDefault(surface => surface != null && string.Equals(surface.SurfaceId, _surfaceId, StringComparison.Ordinal))
                ?? AetheriaRuntimeEveSurfaceCatalog.Find(_surfaceId);
        }

        private EveUnitySceneProviderSurfaceDocument CreateSurfaceDocument(
            EveSurfaceDocument surface,
            long version,
            AetheriaRuntimeEveSurfaceAdvertisement? advertisedSurface = null)
        {
            advertisedSurface ??= AetheriaRuntimeEveSurfaceCatalog.Find(_surfaceId);
            var worldInteraction = advertisedSurface?.WorldInteraction;
            return new EveUnitySceneProviderSurfaceDocument(
                surface,
                new EveUnitySceneProviderSurfaceAdvertisement(
                    _surfaceId,
                    advertisedSurface?.SurfaceKind ?? "",
                    new EveUnitySceneWorldInteraction(
                        worldInteraction?.ProjectionKind ?? "",
                        worldInteraction?.CommandBoundary ?? "",
                        worldInteraction?.ReceiptSchema ?? "",
                        worldInteraction?.Ownership ?? "")),
                SourcePointer(advertisedSurface),
                version);
        }

        private string SourcePointer(AetheriaRuntimeEveSurfaceAdvertisement? advertisedSurface)
        {
            if (!string.IsNullOrWhiteSpace(advertisedSurface?.RecordRef))
                return advertisedSurface.RecordRef;

            var path = _stateBoot?.StateFilePath ?? _stateFilePathOverride;
            return string.IsNullOrWhiteSpace(path)
                ? AetheriaRuntimeVerseRecordKeys.DaemonGameSurface.ToString()
                : $"{path}#{_surfaceId}";
        }

        private static EveUnitySceneCommandReceipt ToReceipt(
            EveSurfaceCommandRequest request,
            AetheriaRuntimeDaemonCommandEnvelope envelope)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (envelope == null) throw new ArgumentNullException(nameof(envelope));

            return new EveUnitySceneCommandReceipt(
                string.IsNullOrWhiteSpace(envelope.CommandId) ? envelope.OperationId : envelope.CommandId,
                envelope.OperationId,
                envelope.CommandId,
                envelope.Accepted ? "accepted" : "denied",
                "Aetheria",
                "aetheria-daemon-command-boundary",
                request.ReceiptSchema,
                request.ProviderId,
                request.SurfaceId,
                envelope.Diagnostic ?? "",
                ParseIssuedAt(envelope.IssuedAtUtc, request.IssuedAt));
        }

        private static EveUnitySceneCommandReceipt ToReceipt(
            EveSurfaceCommandRequest request,
            CultMeshOperationReceipt receipt)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (receipt == null) throw new ArgumentNullException(nameof(receipt));

            var commandId = request.Operation?.IdempotencyKey ?? "";
            return new EveUnitySceneCommandReceipt(
                string.IsNullOrWhiteSpace(commandId) ? receipt.OperationId : commandId,
                receipt.OperationId,
                commandId,
                receipt.Accepted ? "accepted" : "denied",
                "Aetheria",
                "aetheria-eve-command-boundary",
                request.ReceiptSchema,
                request.ProviderId,
                request.SurfaceId,
                receipt.Diagnostic ?? "",
                request.IssuedAt);
        }

        private static DateTimeOffset ParseIssuedAt(string value, DateTimeOffset fallback)
        {
            return DateTimeOffset.TryParse(value, out var parsed) ? parsed : fallback;
        }

        private static EveUnityPlayableWorldAssetManifestDocument ToUnityPlayableWorldAssetManifest(
            AetheriaRuntimeAssetManifestDocument manifest)
        {
            var entries = (manifest.Assets ?? Array.Empty<AetheriaRuntimeAssetManifestEntry>())
                .Where(entry => entry?.Ref != null && !string.IsNullOrWhiteSpace(entry.Ref.AssetKey))
                .Select(entry => new EveUnityPlayableWorldAssetManifestDocumentEntry(
                    entry.Ref.AssetKey,
                    EntityKind(entry),
                    ResourcePath(entry.Ref),
                    entry.Ref.AssetKey,
                    "provider-asset-ref"))
                .ToArray();

            return new EveUnityPlayableWorldAssetManifestDocument(
                AetheriaRuntimeVerseRecordKeys.DaemonAssetManifest.ToString(),
                entries,
                "aetheria.daemon");
        }

        private static string EntityKind(AetheriaRuntimeAssetManifestEntry entry)
        {
            return (entry.Tags ?? Array.Empty<string>())
                .FirstOrDefault(tag => !string.IsNullOrWhiteSpace(tag) && !string.Equals(tag, "map", StringComparison.OrdinalIgnoreCase) && !string.Equals(tag, "icon", StringComparison.OrdinalIgnoreCase))
                ?? "";
        }

        private static string ResourcePath(AetheriaRuntimeAssetRef asset)
        {
            if (asset.Metadata != null && asset.Metadata.TryGetValue("resourcesPath", out var resourcesPath))
                return resourcesPath ?? "";

            return string.IsNullOrWhiteSpace(asset.AssetKey)
                ? ""
                : asset.AssetKey.Replace('.', '/');
        }
    }

    public sealed class AetheriaEveUnityRemoteReceiptTracker
    {
        private readonly Dictionary<string, EveSurfaceCommandRequest> _pending =
            new Dictionary<string, EveSurfaceCommandRequest>(StringComparer.Ordinal);

        public bool HasPending => _pending.Count > 0;

        public void Track(string commandId, EveSurfaceCommandRequest request)
        {
            if (string.IsNullOrWhiteSpace(commandId))
                throw new ArgumentException("Remote command id must be non-empty.", nameof(commandId));
            _pending[commandId] = request ?? throw new ArgumentNullException(nameof(request));
        }

        public IReadOnlyList<EveUnitySceneCommandReceipt> Reconcile(
            IReadOnlyList<AetheriaRuntimeCommittedCommandFactDocument> facts,
            long observedSurfaceVersion)
        {
            if (facts == null) throw new ArgumentNullException(nameof(facts));
            var receipts = new List<EveUnitySceneCommandReceipt>();
            var byCommandId = facts
                .Where(fact => fact != null && !string.IsNullOrWhiteSpace(fact.CommandId))
                .GroupBy(fact => fact.CommandId, StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => group.OrderByDescending(fact => fact.SourceFrameId).First(),
                    StringComparer.Ordinal);
            foreach (var commandId in _pending.Keys.ToArray())
            {
                if (!byCommandId.TryGetValue(commandId, out var fact) ||
                    observedSurfaceVersion < fact.SourceFrameId)
                    continue;

                var request = _pending[commandId];
                _pending.Remove(commandId);
                var accepted = string.Equals(
                    fact.Outcome,
                    AetheriaRuntimeCommandFactOutcomes.Applied,
                    StringComparison.Ordinal);
                receipts.Add(new EveUnitySceneCommandReceipt(
                    fact.FactId,
                    request.Operation?.OperationId ?? "",
                    commandId,
                    accepted ? "accepted" : "denied",
                    "Aetheria",
                    fact.SourceDaemonId,
                    AetheriaRuntimeDaemonSchemas.CommittedCommandFact,
                    request.ProviderId,
                    request.SurfaceId,
                    accepted ? "Command applied by authoritative daemon." : "Command rejected by authoritative daemon.",
                    DateTimeOffset.TryParse(fact.CommittedAtUtc, out var committedAt) ? committedAt : request.IssuedAt,
                    fact.SourceFrameId));
            }
            return receipts;
        }

        public void Clear()
        {
            _pending.Clear();
        }
    }
}
