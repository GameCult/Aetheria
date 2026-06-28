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

    public sealed class AetheriaRuntimeDaemonFrameSession : IDisposable
    {
        private readonly CultMeshReactiveDocument<AetheriaRuntimeDaemonFrameDocument> _daemonFrame;

        public AetheriaRuntimeDaemonFrameSession(
            CultMeshReactiveDocument<AetheriaRuntimeDaemonFrameDocument> daemonFrame)
        {
            _daemonFrame = daemonFrame ?? throw new ArgumentNullException(nameof(daemonFrame));
        }

        public AetheriaRuntimeDaemonFrameDocument? Current => _daemonFrame.Current;

        public void Dispose()
        {
            _daemonFrame.Dispose();
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

    public sealed class AetheriaRuntimeStationRefitSession : IDisposable
    {
        private readonly CultMeshReactiveDocument<AetheriaRuntimeStationRefitDocument> _stationRefit;

        public AetheriaRuntimeStationRefitSession(
            CultMeshReactiveDocument<AetheriaRuntimeStationRefitDocument> stationRefit)
        {
            _stationRefit = stationRefit ?? throw new ArgumentNullException(nameof(stationRefit));
        }

        public AetheriaRuntimeStationRefitDocument? Current => _stationRefit.Current;

        public void Dispose()
        {
            _stationRefit.Dispose();
        }
    }

    public sealed class AetheriaRuntimeCurrentZoneSession : IDisposable
    {
        private readonly CultMeshReactiveDocument<AetheriaRuntimeCurrentZoneDocument> _currentZone;

        public AetheriaRuntimeCurrentZoneSession(
            CultMeshReactiveDocument<AetheriaRuntimeCurrentZoneDocument> currentZone)
        {
            _currentZone = currentZone ?? throw new ArgumentNullException(nameof(currentZone));
        }

        public AetheriaRuntimeCurrentZoneDocument? Current => _currentZone.Current;

        public void Dispose()
        {
            _currentZone.Dispose();
        }
    }

    public sealed class AetheriaRuntimeCurrentEntitySession : IDisposable
    {
        private readonly CultMeshReactiveDocument<AetheriaRuntimeCurrentEntityDocument> _currentEntity;

        public AetheriaRuntimeCurrentEntitySession(
            CultMeshReactiveDocument<AetheriaRuntimeCurrentEntityDocument> currentEntity)
        {
            _currentEntity = currentEntity ?? throw new ArgumentNullException(nameof(currentEntity));
        }

        public AetheriaRuntimeCurrentEntityDocument? Current => _currentEntity.Current;

        public void Dispose()
        {
            _currentEntity.Dispose();
        }
    }

    public sealed class AetheriaRuntimeZoneDetailsSession : IDisposable
    {
        private readonly CultMeshReactiveDocument<AetheriaRuntimeZoneDetailsDocument> _zoneDetails;

        public AetheriaRuntimeZoneDetailsSession(
            CultMeshReactiveDocument<AetheriaRuntimeZoneDetailsDocument> zoneDetails)
        {
            _zoneDetails = zoneDetails ?? throw new ArgumentNullException(nameof(zoneDetails));
        }

        public AetheriaRuntimeZoneDetailsDocument? Current => _zoneDetails.Current;

        public void Dispose()
        {
            _zoneDetails.Dispose();
        }
    }

    public sealed class AetheriaRuntimeInventorySession : IDisposable
    {
        private readonly CultMeshReactiveDocument<AetheriaRuntimeInventoryDocument> _inventory;

        public AetheriaRuntimeInventorySession(
            CultMeshReactiveDocument<AetheriaRuntimeInventoryDocument> inventory)
        {
            _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
        }

        public AetheriaRuntimeInventoryDocument? Current => _inventory.Current;

        public void Dispose()
        {
            _inventory.Dispose();
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
