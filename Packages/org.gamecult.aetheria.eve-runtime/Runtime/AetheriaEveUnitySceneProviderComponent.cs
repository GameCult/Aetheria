using System;
using GameCult.Aetheria.State.Verse;
using GameCult.Eve.Surface;
using GameCult.Eve.UnityScene;
using UnityEngine;

#nullable enable

namespace GameCult.Aetheria.EveRuntime
{
    public sealed class AetheriaEveUnitySceneProviderComponent :
        MonoBehaviour,
        IEveUnitySceneProviderSurfaceDocumentSource,
        IEveUnityPlayableWorldAssetManifestDocumentSource,
        IEveUnitySceneCommandSink,
        IEveUnitySceneCommandReceiptSource,
        IEveUnityProviderRefreshSource,
        IDisposable
    {
        [SerializeField] private string stateFilePathOverride = "";
        [SerializeField] private string surfaceId = AetheriaRuntimeDaemonGameSurfaceBuilder.SurfaceId;
        [SerializeField] private string runtimeId = "unity-scene";
        [SerializeField] private bool refreshOnEnable = true;

        private AetheriaEveUnitySceneProviderBridge? _bridge;

        public string SinkKind => Bridge.SinkKind;

        public string ManifestRef => ((IEveUnityPlayableWorldAssetManifestDocumentSource)Bridge).ManifestRef;

        public EveUnitySceneProviderSurfaceDocument CurrentDocument => Bridge.CurrentDocument;

        EveUnityPlayableWorldAssetManifestDocument IEveUnityPlayableWorldAssetManifestDocumentSource.CurrentDocument =>
            ((IEveUnityPlayableWorldAssetManifestDocumentSource)Bridge).CurrentDocument;

        public event Action<EveUnitySceneProviderSurfaceDocument>? DocumentAvailable
        {
            add => Bridge.DocumentAvailable += value;
            remove => Bridge.DocumentAvailable -= value;
        }

        public event Action<EveUnitySceneCommandReceipt>? ReceiptAvailable
        {
            add => Bridge.ReceiptAvailable += value;
            remove => Bridge.ReceiptAvailable -= value;
        }

        event Action<EveUnityPlayableWorldAssetManifestDocument> IEveUnityPlayableWorldAssetManifestDocumentSource.DocumentAvailable
        {
            add => ((IEveUnityPlayableWorldAssetManifestDocumentSource)Bridge).DocumentAvailable += value;
            remove => ((IEveUnityPlayableWorldAssetManifestDocumentSource)Bridge).DocumentAvailable -= value;
        }

        public void Refresh()
        {
            Bridge.Refresh();
        }

        public void Submit(EveSurfaceCommandRequest request)
        {
            Bridge.Submit(request);
        }

        public void Dispose()
        {
            _bridge?.Dispose();
            _bridge = null;
        }

        private AetheriaEveUnitySceneProviderBridge Bridge
        {
            get
            {
                if (_bridge == null)
                {
                    _bridge = new AetheriaEveUnitySceneProviderBridge(
                        stateFilePathOverride,
                        surfaceId,
                        runtimeId);
                }

                return _bridge;
            }
        }

        private void OnEnable()
        {
            if (refreshOnEnable)
                Refresh();
        }

        private void OnDestroy()
        {
            Dispose();
        }
    }
}
