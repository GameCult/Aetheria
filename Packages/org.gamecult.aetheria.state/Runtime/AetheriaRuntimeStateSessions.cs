using System;
using GameCult.Mesh;

#nullable enable

namespace GameCult.Aetheria.State.Verse
{
    public abstract class AetheriaRuntimeReactiveSession<TDocument> : IDisposable
        where TDocument : class
    {
        private readonly CultMeshReactiveDocument<TDocument> _document;

        protected AetheriaRuntimeReactiveSession(CultMeshReactiveDocument<TDocument> document)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
        }

        public TDocument? Current => _document.Current;

        public void Dispose()
        {
            _document.Dispose();
        }
    }

    public sealed class AetheriaRuntimeCatalogSession
        : AetheriaRuntimeReactiveSession<AetheriaRuntimeCatalogSnapshot>
    {
        public AetheriaRuntimeCatalogSession(CultMeshReactiveDocument<AetheriaRuntimeCatalogSnapshot> catalog)
            : base(catalog)
        {
        }
    }

    public sealed class AetheriaRuntimePlayerSettingsSession
        : AetheriaRuntimeReactiveSession<AetheriaRuntimePlayerSettingsDocument>
    {
        public AetheriaRuntimePlayerSettingsSession(CultMeshReactiveDocument<AetheriaRuntimePlayerSettingsDocument> playerSettings)
            : base(playerSettings)
        {
        }
    }

    public sealed class AetheriaRuntimeVerseHostSettingsSession
        : AetheriaRuntimeReactiveSession<AetheriaRuntimeVerseHostSettingsDocument>
    {
        public AetheriaRuntimeVerseHostSettingsSession(CultMeshReactiveDocument<AetheriaRuntimeVerseHostSettingsDocument> verseHostSettings)
            : base(verseHostSettings)
        {
        }
    }

    public sealed class AetheriaRuntimeSectorMapSession
        : AetheriaRuntimeReactiveSession<AetheriaRuntimeSectorMapDocument>
    {
        public AetheriaRuntimeSectorMapSession(CultMeshReactiveDocument<AetheriaRuntimeSectorMapDocument> sectorMap)
            : base(sectorMap)
        {
        }
    }

    public sealed class AetheriaRuntimeDaemonFrameSession
        : AetheriaRuntimeReactiveSession<AetheriaRuntimeDaemonFrameDocument>
    {
        public AetheriaRuntimeDaemonFrameSession(CultMeshReactiveDocument<AetheriaRuntimeDaemonFrameDocument> daemonFrame)
            : base(daemonFrame)
        {
        }
    }

    public sealed class AetheriaRuntimeDaemonSoaViewSession
        : AetheriaRuntimeReactiveSession<AetheriaRuntimeDaemonSoaViewDocument>
    {
        public AetheriaRuntimeDaemonSoaViewSession(CultMeshReactiveDocument<AetheriaRuntimeDaemonSoaViewDocument> soaView)
            : base(soaView)
        {
        }
    }

    public sealed class AetheriaRuntimeZoneContactsSession
        : AetheriaRuntimeReactiveSession<AetheriaRuntimeZoneContactsDocument>
    {
        public AetheriaRuntimeZoneContactsSession(CultMeshReactiveDocument<AetheriaRuntimeZoneContactsDocument> zoneContacts)
            : base(zoneContacts)
        {
        }
    }

    public sealed class AetheriaRuntimeStationRefitSession
        : AetheriaRuntimeReactiveSession<AetheriaRuntimeStationRefitDocument>
    {
        public AetheriaRuntimeStationRefitSession(CultMeshReactiveDocument<AetheriaRuntimeStationRefitDocument> stationRefit)
            : base(stationRefit)
        {
        }
    }

    public sealed class AetheriaRuntimeZoneRenderSession
        : AetheriaRuntimeReactiveSession<AetheriaRuntimeZoneRenderDocument>
    {
        public AetheriaRuntimeZoneRenderSession(CultMeshReactiveDocument<AetheriaRuntimeZoneRenderDocument> zoneRender)
            : base(zoneRender)
        {
        }
    }

    public sealed class AetheriaRuntimeCurrentZoneSession
        : AetheriaRuntimeReactiveSession<AetheriaRuntimeCurrentZoneDocument>
    {
        public AetheriaRuntimeCurrentZoneSession(CultMeshReactiveDocument<AetheriaRuntimeCurrentZoneDocument> currentZone)
            : base(currentZone)
        {
        }
    }

    public sealed class AetheriaRuntimeCurrentEntitySession
        : AetheriaRuntimeReactiveSession<AetheriaRuntimeCurrentEntityDocument>
    {
        public AetheriaRuntimeCurrentEntitySession(CultMeshReactiveDocument<AetheriaRuntimeCurrentEntityDocument> currentEntity)
            : base(currentEntity)
        {
        }
    }

    public sealed class AetheriaRuntimeCurrentDockingSession
        : AetheriaRuntimeReactiveSession<AetheriaRuntimeCurrentDockingDocument>
    {
        public AetheriaRuntimeCurrentDockingSession(CultMeshReactiveDocument<AetheriaRuntimeCurrentDockingDocument> currentDocking)
            : base(currentDocking)
        {
        }
    }

    public sealed class AetheriaRuntimeZoneDetailsSession
        : AetheriaRuntimeReactiveSession<AetheriaRuntimeZoneDetailsDocument>
    {
        public AetheriaRuntimeZoneDetailsSession(CultMeshReactiveDocument<AetheriaRuntimeZoneDetailsDocument> zoneDetails)
            : base(zoneDetails)
        {
        }
    }

    public sealed class AetheriaRuntimeInventorySession
        : AetheriaRuntimeReactiveSession<AetheriaRuntimeInventoryDocument>
    {
        public AetheriaRuntimeInventorySession(CultMeshReactiveDocument<AetheriaRuntimeInventoryDocument> inventory)
            : base(inventory)
        {
        }
    }

    public sealed class AetheriaRuntimeObjectsViewportSession
        : AetheriaRuntimeReactiveSession<AetheriaRuntimeObjectsViewportDocument>
    {
        public AetheriaRuntimeObjectsViewportSession(CultMeshReactiveDocument<AetheriaRuntimeObjectsViewportDocument> objectsViewport)
            : base(objectsViewport)
        {
        }
    }

    public sealed class AetheriaRuntimeMapViewportSession
        : AetheriaRuntimeReactiveSession<AetheriaRuntimeRtsViewportDocument>
    {
        public AetheriaRuntimeMapViewportSession(CultMeshReactiveDocument<AetheriaRuntimeRtsViewportDocument> mapViewport)
            : base(mapViewport)
        {
        }
    }

    public sealed class AetheriaRuntimeRenderSplatsViewportSession
        : AetheriaRuntimeReactiveSession<AetheriaRuntimeRenderSplatsViewportDocument>
    {
        public AetheriaRuntimeRenderSplatsViewportSession(CultMeshReactiveDocument<AetheriaRuntimeRenderSplatsViewportDocument> renderSplatsViewport)
            : base(renderSplatsViewport)
        {
        }
    }

    public sealed class AetheriaRuntimeGravityViewportSession
        : AetheriaRuntimeReactiveSession<AetheriaRuntimeGravityViewportDocument>
    {
        public AetheriaRuntimeGravityViewportSession(CultMeshReactiveDocument<AetheriaRuntimeGravityViewportDocument> gravityViewport)
            : base(gravityViewport)
        {
        }
    }

    public sealed class AetheriaRuntimeSelectedObjectSession
        : AetheriaRuntimeReactiveSession<AetheriaRuntimeSelectedObjectDocument>
    {
        public AetheriaRuntimeSelectedObjectSession(CultMeshReactiveDocument<AetheriaRuntimeSelectedObjectDocument> selectedObject)
            : base(selectedObject)
        {
        }
    }

    public sealed class AetheriaRuntimeLoadoutTemplatesSession
        : AetheriaRuntimeReactiveSession<AetheriaRuntimeLoadoutTemplatesDocument>
    {
        public AetheriaRuntimeLoadoutTemplatesSession(CultMeshReactiveDocument<AetheriaRuntimeLoadoutTemplatesDocument> loadoutTemplates)
            : base(loadoutTemplates)
        {
        }
    }

    public sealed class AetheriaRuntimeStarbridgeScenarioSession
        : AetheriaRuntimeReactiveSession<AetheriaRuntimeStarbridgeScenarioDocument>
    {
        public AetheriaRuntimeStarbridgeScenarioSession(CultMeshReactiveDocument<AetheriaRuntimeStarbridgeScenarioDocument> scenario)
            : base(scenario)
        {
        }
    }

    public sealed class AetheriaRuntimeStarbridgeRunSession
        : AetheriaRuntimeReactiveSession<AetheriaRuntimeStarbridgeSessionDocument>
    {
        public AetheriaRuntimeStarbridgeRunSession(CultMeshReactiveDocument<AetheriaRuntimeStarbridgeSessionDocument> session)
            : base(session)
        {
        }
    }

    public sealed class AetheriaRuntimeStarbridgeSummarySession
        : AetheriaRuntimeReactiveSession<AetheriaRuntimeStarbridgeSessionSummaryDocument>
    {
        public AetheriaRuntimeStarbridgeSummarySession(CultMeshReactiveDocument<AetheriaRuntimeStarbridgeSessionSummaryDocument> summary)
            : base(summary)
        {
        }
    }

    public sealed class AetheriaRuntimeStarbridgePlayerSeatSession
        : AetheriaRuntimeReactiveSession<AetheriaRuntimeStarbridgePlayerSeatDocument>
    {
        public AetheriaRuntimeStarbridgePlayerSeatSession(CultMeshReactiveDocument<AetheriaRuntimeStarbridgePlayerSeatDocument> playerSeat)
            : base(playerSeat)
        {
        }
    }
}
