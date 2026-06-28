using System;
using GameCult.Mesh;

#nullable enable

namespace GameCult.Aetheria.State.Verse
{
    public sealed class AetheriaRuntimePlayerHudSession : IDisposable
    {
        private readonly CultMeshReactiveDocument<AetheriaRuntimeCatalogSnapshot> _catalog;
        private readonly CultMeshReactiveDocument<AetheriaRuntimePlayerSettingsDocument> _playerSettings;
        private readonly CultMeshReactiveDocument<AetheriaRuntimeCurrentEntityDocument> _currentEntity;

        public AetheriaRuntimePlayerHudSession(
            CultMeshReactiveDocument<AetheriaRuntimeCatalogSnapshot> catalog,
            CultMeshReactiveDocument<AetheriaRuntimePlayerSettingsDocument> playerSettings,
            CultMeshReactiveDocument<AetheriaRuntimeCurrentEntityDocument> currentEntity)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _playerSettings = playerSettings ?? throw new ArgumentNullException(nameof(playerSettings));
            _currentEntity = currentEntity ?? throw new ArgumentNullException(nameof(currentEntity));
        }

        public AetheriaRuntimeCatalogSnapshot? Catalog => _catalog.Current;
        public AetheriaRuntimePlayerSettingsDocument? PlayerSettings => _playerSettings.Current;
        public AetheriaRuntimeCurrentEntityDocument? CurrentEntity => _currentEntity.Current;
        public AetheriaRuntimeCurrentEntityHudStatus Hud =>
            CurrentEntity?.Hud ?? new AetheriaRuntimeCurrentEntityHudStatus();

        public void Dispose()
        {
            _catalog.Dispose();
            _playerSettings.Dispose();
            _currentEntity.Dispose();
        }
    }
}
