using System;

#nullable enable

namespace GameCult.Aetheria.State.Verse
{
    internal sealed class AetheriaRuntimeManagedClientInputs : IDisposable
    {
        private readonly AetheriaRuntimeDaemonFrameSession _daemonFrame;
        private readonly AetheriaRuntimeCatalogSession _catalog;
        private readonly AetheriaRuntimeLoadoutTemplatesSession _loadoutTemplates;
        private readonly AetheriaRuntimeStarbridgeScenarioSession _starbridgeScenario;
        private readonly AetheriaRuntimeStarbridgeRunSession _starbridgeSession;

        public AetheriaRuntimeManagedClientInputs(AetheriaClientState state)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));

            _daemonFrame = state.ObserveDaemonFrame();
            _catalog = state.ObserveCatalog();
            _loadoutTemplates = state.ObserveLoadoutTemplates();
            _starbridgeScenario = state.Starbridge.ObserveScenario();
            _starbridgeSession = state.Starbridge.ObserveSession();
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
