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
        private CultMeshReactiveDocument<EveSurfaceDocument>? _surfaceState;
        private CultMeshReactiveDocument<AetheriaRuntimeCatalogSnapshot>? _catalogState;
        private AetheriaRuntimeStateBootReport? _stateBoot;
        private AetheriaClientState? _runtimeState;
        private event Action<EveUnityPlayableWorldAssetManifestDocument>? AssetManifestDocumentAvailable;

        public AetheriaEveUnitySceneProviderBridge(
            string stateFilePathOverride = "",
            string surfaceId = AetheriaRuntimeDaemonGameSurfaceBuilder.SurfaceId,
            string runtimeId = "unity-scene")
        {
            _stateFilePathOverride = stateFilePathOverride ?? "";
            _surfaceId = string.IsNullOrWhiteSpace(surfaceId)
                ? AetheriaRuntimeDaemonGameSurfaceBuilder.SurfaceId
                : surfaceId;
            _runtimeId = string.IsNullOrWhiteSpace(runtimeId) ? "unity-scene" : runtimeId;
            CurrentDocument = CreateSurfaceDocument(AetheriaRuntimeSurfaceDocuments.EmptySurface(_surfaceId), 0);
            CurrentDocumentManifest = new EveUnityPlayableWorldAssetManifestDocument(
                AetheriaRuntimeVerseRecordKeys.DaemonAssetManifest.ToString(),
                Array.Empty<EveUnityPlayableWorldAssetManifestDocumentEntry>(),
                "aetheria.daemon");
        }

        public string SinkKind => "aetheria-daemon-command-boundary";

        public string ManifestRef => CurrentDocumentManifest.ManifestRef;

        public EveUnitySceneProviderSurfaceDocument CurrentDocument { get; private set; }

        EveUnityPlayableWorldAssetManifestDocument IEveUnityPlayableWorldAssetManifestDocumentSource.CurrentDocument =>
            CurrentDocumentManifest;

        public EveUnityPlayableWorldAssetManifestDocument CurrentDocumentManifest { get; private set; }

        public event Action<EveUnitySceneProviderSurfaceDocument>? DocumentAvailable;

        public event Action<EveUnitySceneCommandReceipt>? ReceiptAvailable;

        event Action<EveUnityPlayableWorldAssetManifestDocument> IEveUnityPlayableWorldAssetManifestDocumentSource.DocumentAvailable
        {
            add => AssetManifestDocumentAvailable += value;
            remove => AssetManifestDocumentAvailable -= value;
        }

        public void Refresh()
        {
            var stateBoot = ResolveStateBoot();
            if (!stateBoot.SupportsLocalStateFileRead || !stateBoot.StateFileExists)
                return;

            var runtimeState = ResolveRuntimeState(stateBoot);
            var surface = ReadSurface(runtimeState, stateBoot);
            if (surface != null)
            {
                CurrentDocument = CreateSurfaceDocument(surface, surface.Version);
                DocumentAvailable?.Invoke(CurrentDocument);
            }

            CurrentDocumentManifest = ReadAssetManifest(runtimeState);
            AssetManifestDocumentAvailable?.Invoke(CurrentDocumentManifest);
        }

        public void Submit(EveSurfaceCommandRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            var clientId = string.IsNullOrWhiteSpace(request.ClientId) ? _runtimeId : request.ClientId;
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

        public void Dispose()
        {
            _surfaceState?.Dispose();
            _surfaceState = null;
            _catalogState?.Dispose();
            _catalogState = null;
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
            AetheriaRuntimeStateBootReport stateBoot)
        {
            try
            {
                var handle = runtimeState.EveSurfaceDocument(_surfaceId);
                if (handle == null)
                    return null;

                _surfaceState ??= handle.Reactive();
                var resolver = AetheriaEveRuntimeUnityHooks.TryCreateStateRefResolver(stateBoot, _runtimeId);
                return AetheriaRuntimeSurfaceDocuments.ToEveSurfaceDocument(_surfaceState.Current, resolver);
            }
            catch (KeyNotFoundException)
            {
                return null;
            }
        }

        private EveUnityPlayableWorldAssetManifestDocument ReadAssetManifest(AetheriaClientState runtimeState)
        {
            _catalogState ??= runtimeState.Catalog.Reactive();
            var manifest = AetheriaRuntimeAssets.ProjectManifest(
                _catalogState.Current,
                runId: "",
                baseUri: "cultmesh://aetheria.local/assets");
            return ToUnityPlayableWorldAssetManifest(manifest);
        }

        private EveUnitySceneProviderSurfaceDocument CreateSurfaceDocument(
            EveSurfaceDocument surface,
            long version)
        {
            return new EveUnitySceneProviderSurfaceDocument(
                surface,
                new EveUnitySceneProviderSurfaceAdvertisement(
                    _surfaceId,
                    "interactive-world",
                    new EveUnitySceneWorldInteraction(
                        "provider-authored-world-surface",
                        "aetheria.daemon.commands",
                        "aetheria.eve_command_acceptance_status.v1",
                        "provider-owns-world-state-assets-command-acceptance-and-receipts")),
                SourcePointer(),
                version);
        }

        private string SourcePointer()
        {
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
}
