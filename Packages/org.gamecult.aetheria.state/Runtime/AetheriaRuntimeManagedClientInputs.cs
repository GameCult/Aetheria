using System;
using GameCult.Mesh;

#nullable enable

namespace GameCult.Aetheria.State.Verse
{
    internal sealed class AetheriaRuntimeManagedClientInputs : IDisposable
    {
        private readonly CultMeshReactiveDocument<AetheriaRuntimeDaemonFrameDocument> _daemonFrame;
        private readonly CultMeshReactiveDocument<AetheriaRuntimeCatalogSnapshot> _catalog;
        private readonly CultMeshReactiveDocument<AetheriaRuntimeLoadoutTemplatesDocument> _loadoutTemplates;
        private readonly CultMeshReactiveDocument<AetheriaRuntimeStarbridgeScenarioDocument> _starbridgeScenario;
        private readonly CultMeshReactiveDocument<AetheriaRuntimeStarbridgeSessionDocument> _starbridgeSession;

        public AetheriaRuntimeManagedClientInputs(AetheriaClientState state)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));

            _daemonFrame = state.Reactive<AetheriaRuntimeDaemonFrameDocument>();
            _catalog = state.Reactive<AetheriaRuntimeCatalogSnapshot>();
            _loadoutTemplates = state.Reactive<AetheriaRuntimeLoadoutTemplatesDocument>();
            _starbridgeScenario = state.Reactive<AetheriaRuntimeStarbridgeScenarioDocument>();
            _starbridgeSession = state.Reactive<AetheriaRuntimeStarbridgeSessionDocument>();
        }

        public AetheriaRuntimeCatalogSnapshot Catalog => _catalog.Current
            ?? throw new InvalidOperationException("Aetheria Verse client has no runtime catalog document yet.");

        public AetheriaRuntimeLoadoutTemplatesDocument LoadoutTemplates => _loadoutTemplates.Current
            ?? throw new InvalidOperationException("Aetheria Verse client has no loadout templates document yet.");

        public AetheriaRuntimeStarbridgeScenarioDocument? StarbridgeScenario => _starbridgeScenario.Current;

        public AetheriaRuntimeStarbridgeSessionDocument? StarbridgeSession => _starbridgeSession.Current;

        public AetheriaRuntimeDaemonFrameDocument RequireFrame()
        {
            return _daemonFrame.Current
                ?? throw new InvalidOperationException("Aetheria Verse client has no daemon frame yet.");
        }

        public void Dispose()
        {
            _daemonFrame.Dispose();
            _catalog.Dispose();
            _loadoutTemplates.Dispose();
            _starbridgeScenario.Dispose();
            _starbridgeSession.Dispose();
        }
    }
}
