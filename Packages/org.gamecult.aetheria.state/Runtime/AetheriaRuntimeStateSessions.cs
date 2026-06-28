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

    public sealed class AetheriaRuntimeDaemonSoaViewSession : IDisposable
    {
        private readonly CultMeshReactiveDocument<AetheriaRuntimeDaemonSoaViewDocument> _soaView;

        public AetheriaRuntimeDaemonSoaViewSession(
            CultMeshReactiveDocument<AetheriaRuntimeDaemonSoaViewDocument> soaView)
        {
            _soaView = soaView ?? throw new ArgumentNullException(nameof(soaView));
        }

        public AetheriaRuntimeDaemonSoaViewDocument? Current => _soaView.Current;

        public void Dispose()
        {
            _soaView.Dispose();
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

    public sealed class AetheriaRuntimeZoneRenderSession : IDisposable
    {
        private readonly CultMeshReactiveDocument<AetheriaRuntimeZoneRenderDocument> _zoneRender;

        public AetheriaRuntimeZoneRenderSession(
            CultMeshReactiveDocument<AetheriaRuntimeZoneRenderDocument> zoneRender)
        {
            _zoneRender = zoneRender ?? throw new ArgumentNullException(nameof(zoneRender));
        }

        public AetheriaRuntimeZoneRenderDocument? Current => _zoneRender.Current;

        public void Dispose()
        {
            _zoneRender.Dispose();
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

    public sealed class AetheriaRuntimeCurrentDockingSession : IDisposable
    {
        private readonly CultMeshReactiveDocument<AetheriaRuntimeCurrentDockingDocument> _currentDocking;

        public AetheriaRuntimeCurrentDockingSession(
            CultMeshReactiveDocument<AetheriaRuntimeCurrentDockingDocument> currentDocking)
        {
            _currentDocking = currentDocking ?? throw new ArgumentNullException(nameof(currentDocking));
        }

        public AetheriaRuntimeCurrentDockingDocument? Current => _currentDocking.Current;

        public void Dispose()
        {
            _currentDocking.Dispose();
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

    public sealed class AetheriaRuntimeMapViewportSession : IDisposable
    {
        private readonly CultMeshReactiveDocument<AetheriaRuntimeRtsViewportDocument> _mapViewport;

        public AetheriaRuntimeMapViewportSession(
            CultMeshReactiveDocument<AetheriaRuntimeRtsViewportDocument> mapViewport)
        {
            _mapViewport = mapViewport ?? throw new ArgumentNullException(nameof(mapViewport));
        }

        public AetheriaRuntimeRtsViewportDocument? Current => _mapViewport.Current;

        public void Dispose()
        {
            _mapViewport.Dispose();
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

    public sealed class AetheriaRuntimeGravityViewportSession : IDisposable
    {
        private readonly CultMeshReactiveDocument<AetheriaRuntimeGravityViewportDocument> _gravityViewport;

        public AetheriaRuntimeGravityViewportSession(
            CultMeshReactiveDocument<AetheriaRuntimeGravityViewportDocument> gravityViewport)
        {
            _gravityViewport = gravityViewport ?? throw new ArgumentNullException(nameof(gravityViewport));
        }

        public AetheriaRuntimeGravityViewportDocument? Current => _gravityViewport.Current;

        public void Dispose()
        {
            _gravityViewport.Dispose();
        }
    }

    public sealed class AetheriaRuntimeSelectedObjectSession : IDisposable
    {
        private readonly CultMeshReactiveDocument<AetheriaRuntimeSelectedObjectDocument> _selectedObject;

        public AetheriaRuntimeSelectedObjectSession(
            CultMeshReactiveDocument<AetheriaRuntimeSelectedObjectDocument> selectedObject)
        {
            _selectedObject = selectedObject ?? throw new ArgumentNullException(nameof(selectedObject));
        }

        public AetheriaRuntimeSelectedObjectDocument? Current => _selectedObject.Current;

        public void Dispose()
        {
            _selectedObject.Dispose();
        }
    }

    public sealed class AetheriaRuntimeLoadoutTemplatesSession : IDisposable
    {
        private readonly CultMeshReactiveDocument<AetheriaRuntimeLoadoutTemplatesDocument> _loadoutTemplates;

        public AetheriaRuntimeLoadoutTemplatesSession(
            CultMeshReactiveDocument<AetheriaRuntimeLoadoutTemplatesDocument> loadoutTemplates)
        {
            _loadoutTemplates = loadoutTemplates ?? throw new ArgumentNullException(nameof(loadoutTemplates));
        }

        public AetheriaRuntimeLoadoutTemplatesDocument? Current => _loadoutTemplates.Current;

        public void Dispose()
        {
            _loadoutTemplates.Dispose();
        }
    }

    public sealed class AetheriaRuntimeStarbridgeScenarioSession : IDisposable
    {
        private readonly CultMeshReactiveDocument<AetheriaRuntimeStarbridgeScenarioDocument> _scenario;

        public AetheriaRuntimeStarbridgeScenarioSession(
            CultMeshReactiveDocument<AetheriaRuntimeStarbridgeScenarioDocument> scenario)
        {
            _scenario = scenario ?? throw new ArgumentNullException(nameof(scenario));
        }

        public AetheriaRuntimeStarbridgeScenarioDocument? Current => _scenario.Current;

        public void Dispose()
        {
            _scenario.Dispose();
        }
    }

    public sealed class AetheriaRuntimeStarbridgeRunSession : IDisposable
    {
        private readonly CultMeshReactiveDocument<AetheriaRuntimeStarbridgeSessionDocument> _session;

        public AetheriaRuntimeStarbridgeRunSession(
            CultMeshReactiveDocument<AetheriaRuntimeStarbridgeSessionDocument> session)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
        }

        public AetheriaRuntimeStarbridgeSessionDocument? Current => _session.Current;

        public void Dispose()
        {
            _session.Dispose();
        }
    }

    public sealed class AetheriaRuntimeStarbridgeSummarySession : IDisposable
    {
        private readonly CultMeshReactiveDocument<AetheriaRuntimeStarbridgeSessionSummaryDocument> _summary;

        public AetheriaRuntimeStarbridgeSummarySession(
            CultMeshReactiveDocument<AetheriaRuntimeStarbridgeSessionSummaryDocument> summary)
        {
            _summary = summary ?? throw new ArgumentNullException(nameof(summary));
        }

        public AetheriaRuntimeStarbridgeSessionSummaryDocument? Current => _summary.Current;

        public void Dispose()
        {
            _summary.Dispose();
        }
    }

    public sealed class AetheriaRuntimeStarbridgePlayerSeatSession : IDisposable
    {
        private readonly CultMeshReactiveDocument<AetheriaRuntimeStarbridgePlayerSeatDocument> _playerSeat;

        public AetheriaRuntimeStarbridgePlayerSeatSession(
            CultMeshReactiveDocument<AetheriaRuntimeStarbridgePlayerSeatDocument> playerSeat)
        {
            _playerSeat = playerSeat ?? throw new ArgumentNullException(nameof(playerSeat));
        }

        public AetheriaRuntimeStarbridgePlayerSeatDocument? Current => _playerSeat.Current;

        public void Dispose()
        {
            _playerSeat.Dispose();
        }
    }
}
