using Aetheria.State.Documents;
using GameCult.Caching;
using GameCult.Aetheria.State.Verse;
using GameCult.Eve.Surface;
using GameCult.Eve.PluginFields;
using GameCult.Mesh;
using GameCult.Networking;

namespace Aetheria.State;

public static class AetheriaDocumentRegistry
{
    public static CultDocumentRegistry CreateCultCacheRegistry()
    {
        return CultMesh.CreateCultCacheDocumentRegistry(DocumentTypes);
    }

    public static CultNetDocumentRegistry CreateCultNetRegistry(CultDocumentRegistry? cacheRegistry = null)
    {
        var registry = cacheRegistry ?? CreateCultCacheRegistry();
        return CultMesh.CreateCultNetDocumentRegistry(DocumentTypes, registry);
    }

    public static IReadOnlyList<Type> DocumentTypes { get; } =
    [
        typeof(AetheriaWorldState),
        typeof(AetheriaItemDefinition),
        typeof(AetheriaCorporation),
        typeof(AetheriaNameFile),
        typeof(AetheriaPlayerProfile),
        typeof(AetheriaRuntimeSession),
        typeof(AetheriaMigrationLedger),
        typeof(AetheriaLegacyCatalogQuarantine),
        typeof(AetheriaTradeValuePolicy),
        typeof(AetheriaRuntimeCatalogSnapshot),
        typeof(AetheriaRuntimeNameCorpusSnapshot),
        typeof(AetheriaPlayerSettings),
        typeof(AetheriaLoadoutTemplate),
        typeof(AetheriaRunState),
        typeof(AetheriaZoneState),
        typeof(AetheriaEntitySnapshot),
        typeof(AetheriaVerseHostSettings),
        typeof(AetheriaMainMenuState),
        typeof(AetheriaGameSessionState),
        typeof(AetheriaEveCommandAcceptanceStatus),
        typeof(AetheriaRuntimeDaemonProviderAdvertisementDocument),
        typeof(AetheriaRuntimeDaemonHealthDocument),
        typeof(AetheriaRuntimeDaemonCommandBoundaryDocument),
        typeof(AetheriaRuntimeAssetManifestDocument),
        typeof(AetheriaRuntimeDaemonFrameDocument),
        typeof(AetheriaRuntimeGameViewportDocument),
        typeof(AetheriaRuntimeObjectsViewportDocument),
        typeof(AetheriaRuntimeGravityViewportDocument),
        typeof(AetheriaRuntimeCurrentZoneDocument),
        typeof(AetheriaRuntimeCurrentEntityDocument),
        typeof(AetheriaRuntimeCurrentDockingDocument),
        typeof(AetheriaRuntimeZoneContactsDocument),
        typeof(AetheriaRuntimeStationRefitDocument),
        typeof(AetheriaRuntimeSectorMapDocument),
        typeof(AetheriaRuntimeZoneDetailsDocument),
        typeof(AetheriaRuntimeZoneRenderDocument),
        typeof(AetheriaRuntimeSelectedObjectDocument),
        typeof(AetheriaRuntimeInventoryDocument),
        typeof(AetheriaRuntimeStarbridgeScenarioDocument),
        typeof(AetheriaRuntimeStarbridgeSessionDocument),
        typeof(AetheriaRuntimeStarbridgeSessionSummaryDocument),
        typeof(EveFieldsSplatsDocument),
        typeof(EveInputCapabilityDocument),
        typeof(AetheriaRuntimeDaemonSoaViewDocument),
        typeof(AetheriaRuntimeVerseAuthorityPolicyDocument),
        typeof(AetheriaRuntimeAuthorityLeaseDocument),
        typeof(AetheriaRuntimeDaemonCommandDocument),
        typeof(AetheriaRuntimeCommittedCommandFactDocument),
        typeof(AetheriaRuntimeEveCommandDocument),
        typeof(AetheriaRuntimeSurfaceDocument),
        typeof(EveProviderAdvertisementDocument),
        typeof(EveSurfaceDocument),
        typeof(EveSurfaceCommandRequest),
        typeof(EveCommandReceiptDocument),
        typeof(EveAssetCatalogDocument)
    ];
}
