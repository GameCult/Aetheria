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

    public sealed class AetheriaRuntimeVerseHostSettingsSession : IDisposable
    {
        private readonly CultMeshReactiveDocument<AetheriaRuntimeVerseHostSettingsDocument> _verseHostSettings;

        public AetheriaRuntimeVerseHostSettingsSession(
            CultMeshReactiveDocument<AetheriaRuntimeVerseHostSettingsDocument> verseHostSettings)
        {
            _verseHostSettings = verseHostSettings ?? throw new ArgumentNullException(nameof(verseHostSettings));
        }

        public AetheriaRuntimeVerseHostSettingsDocument? Current => _verseHostSettings.Current;

        public void Dispose()
        {
            _verseHostSettings.Dispose();
        }
    }

    public sealed class AetheriaRuntimeSectorMapSession : IDisposable
    {
        private readonly CultMeshReactiveDocument<AetheriaRuntimeSectorMapDocument> _sectorMap;

        public AetheriaRuntimeSectorMapSession(
            CultMeshReactiveDocument<AetheriaRuntimeSectorMapDocument> sectorMap)
        {
            _sectorMap = sectorMap ?? throw new ArgumentNullException(nameof(sectorMap));
        }

        public AetheriaRuntimeSectorMapDocument? Current => _sectorMap.Current;

        public void Dispose()
        {
            _sectorMap.Dispose();
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

    public sealed class AetheriaRuntimeObjectsViewportSession : IDisposable
    {
        private readonly CultMeshReactiveDocument<AetheriaRuntimeObjectsViewportDocument> _objectsViewport;

        public AetheriaRuntimeObjectsViewportSession(
            CultMeshReactiveDocument<AetheriaRuntimeObjectsViewportDocument> objectsViewport)
        {
            _objectsViewport = objectsViewport ?? throw new ArgumentNullException(nameof(objectsViewport));
        }

        public AetheriaRuntimeObjectsViewportDocument? Current => _objectsViewport.Current;

        public void Dispose()
        {
            _objectsViewport.Dispose();
        }
    }

    public sealed class AetheriaRuntimeRenderSplatsViewportSession : IDisposable
    {
        private readonly CultMeshReactiveDocument<AetheriaRuntimeRenderSplatsViewportDocument> _renderSplatsViewport;

        public AetheriaRuntimeRenderSplatsViewportSession(
            CultMeshReactiveDocument<AetheriaRuntimeRenderSplatsViewportDocument> renderSplatsViewport)
        {
            _renderSplatsViewport = renderSplatsViewport ?? throw new ArgumentNullException(nameof(renderSplatsViewport));
        }

        public AetheriaRuntimeRenderSplatsViewportDocument? Current => _renderSplatsViewport.Current;

        public void Dispose()
        {
            _renderSplatsViewport.Dispose();
        }
    }
}
