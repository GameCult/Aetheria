using System;

#nullable enable

namespace GameCult.Aetheria.State.Verse
{
    public sealed class AetheriaRuntimePlayerHudSession : IDisposable
    {
        private readonly AetheriaRuntimeCatalogSession _catalog;
        private readonly AetheriaRuntimePlayerSettingsSession _playerSettings;
        private readonly AetheriaRuntimeCurrentEntitySession _currentEntity;

        public AetheriaRuntimePlayerHudSession(
            AetheriaRuntimeCatalogSession catalog,
            AetheriaRuntimePlayerSettingsSession playerSettings,
            AetheriaRuntimeCurrentEntitySession currentEntity)
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
