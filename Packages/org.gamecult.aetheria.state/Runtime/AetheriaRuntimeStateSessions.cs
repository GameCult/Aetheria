using System;
using GameCult.Mesh;

#nullable enable

namespace GameCult.Aetheria.State.Verse
{
    public sealed class AetheriaRuntimeCatalogSession : IDisposable
    {
        private readonly CultMeshReactiveDocument<AetheriaRuntimeCatalogSnapshot> _catalog;

        public AetheriaRuntimeCatalogSession(
            CultMeshReactiveDocument<AetheriaRuntimeCatalogSnapshot> catalog)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        }

        public AetheriaRuntimeCatalogSnapshot? Current => _catalog.Current;

        public void Dispose()
        {
            _catalog.Dispose();
        }
    }

    public sealed class AetheriaRuntimePlayerSettingsSession : IDisposable
    {
        private readonly CultMeshReactiveDocument<AetheriaRuntimePlayerSettingsDocument> _playerSettings;

        public AetheriaRuntimePlayerSettingsSession(
            CultMeshReactiveDocument<AetheriaRuntimePlayerSettingsDocument> playerSettings)
        {
            _playerSettings = playerSettings ?? throw new ArgumentNullException(nameof(playerSettings));
        }

        public AetheriaRuntimePlayerSettingsDocument? Current => _playerSettings.Current;

        public void Dispose()
        {
            _playerSettings.Dispose();
        }
    }

    public sealed class AetheriaRuntimeZoneContactsSession : IDisposable
    {
        private readonly CultMeshReactiveDocument<AetheriaRuntimeZoneContactsDocument> _zoneContacts;

        public AetheriaRuntimeZoneContactsSession(
            CultMeshReactiveDocument<AetheriaRuntimeZoneContactsDocument> zoneContacts)
        {
            _zoneContacts = zoneContacts ?? throw new ArgumentNullException(nameof(zoneContacts));
        }

        public AetheriaRuntimeZoneContactsDocument? Current => _zoneContacts.Current;

        public void Dispose()
        {
            _zoneContacts.Dispose();
        }
    }
}
