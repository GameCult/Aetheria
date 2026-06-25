using Aetheria.State.Documents;
using GameCult.Caching;
using GameCult.Aetheria.State.Verse;
using GameCult.Networking;

namespace Aetheria.State;

public static class AetheriaDocumentRegistry
{
    public static CultDocumentRegistry CreateCultCacheRegistry()
    {
        var registry = new CultDocumentRegistry();
        foreach (var documentType in DocumentTypes)
        {
            registry.GetRequired(documentType);
        }

        return registry;
    }

    public static CultNetDocumentRegistry CreateCultNetRegistry(CultDocumentRegistry? cacheRegistry = null)
    {
        var registry = cacheRegistry ?? CreateCultCacheRegistry();
        return new CultNetDocumentRegistry(
            registry,
            new[]
            {
                CultNetDocumentBinding.ForDocument<AetheriaWorldState>(registry),
                CultNetDocumentBinding.ForDocument<AetheriaItemDefinition>(registry),
                CultNetDocumentBinding.ForDocument<AetheriaCorporation>(registry),
                CultNetDocumentBinding.ForDocument<AetheriaNameFile>(registry),
                CultNetDocumentBinding.ForDocument<AetheriaPlayerProfile>(registry),
                CultNetDocumentBinding.ForDocument<AetheriaRuntimeSession>(registry),
                CultNetDocumentBinding.ForDocument<AetheriaMigrationLedger>(registry),
                CultNetDocumentBinding.ForDocument<AetheriaLegacyCatalogQuarantine>(registry),
                CultNetDocumentBinding.ForDocument<AetheriaTradeValuePolicy>(registry),
                CultNetDocumentBinding.ForDocument<AetheriaPlayerSettings>(registry),
                CultNetDocumentBinding.ForDocument<AetheriaLoadoutTemplate>(registry),
                CultNetDocumentBinding.ForDocument<AetheriaRunState>(registry),
                CultNetDocumentBinding.ForDocument<AetheriaZoneState>(registry),
                CultNetDocumentBinding.ForDocument<AetheriaEntitySnapshot>(registry),
                CultNetDocumentBinding.ForDocument<AetheriaVerseHostSettings>(registry),
                CultNetDocumentBinding.ForDocument<AetheriaEveCommandAcceptanceStatus>(registry),
                CultNetDocumentBinding.ForDocument<AetheriaRuntimeDaemonProviderAdvertisementDocument>(registry),
                CultNetDocumentBinding.ForDocument<AetheriaRuntimeDaemonHealthDocument>(registry),
                CultNetDocumentBinding.ForDocument<AetheriaRuntimeDaemonCommandBoundaryDocument>(registry),
                CultNetDocumentBinding.ForDocument<AetheriaRuntimeDaemonFrameDocument>(registry),
                CultNetDocumentBinding.ForDocument<AetheriaRuntimeRtsViewportDocument>(registry),
                CultNetDocumentBinding.ForDocument<AetheriaRuntimeObjectsViewportDocument>(registry),
                CultNetDocumentBinding.ForDocument<AetheriaRuntimeGravityViewportDocument>(registry),
                CultNetDocumentBinding.ForDocument<AetheriaRuntimeCurrentZoneDocument>(registry),
                CultNetDocumentBinding.ForDocument<AetheriaRuntimeCurrentEntityDocument>(registry),
                CultNetDocumentBinding.ForDocument<AetheriaRuntimeCurrentDockingDocument>(registry),
                CultNetDocumentBinding.ForDocument<AetheriaRuntimeZoneContactsDocument>(registry),
                CultNetDocumentBinding.ForDocument<AetheriaRuntimeStationRefitDocument>(registry),
                CultNetDocumentBinding.ForDocument<AetheriaRuntimeSectorMapDocument>(registry),
                CultNetDocumentBinding.ForDocument<AetheriaRuntimeZoneDetailsDocument>(registry),
                CultNetDocumentBinding.ForDocument<AetheriaRuntimeZoneRenderDocument>(registry),
                CultNetDocumentBinding.ForDocument<AetheriaRuntimeSelectedObjectDocument>(registry),
                CultNetDocumentBinding.ForDocument<AetheriaRuntimeInventoryDocument>(registry),
                CultNetDocumentBinding.ForDocument<AetheriaRuntimeStarbridgeScenarioDocument>(registry),
                CultNetDocumentBinding.ForDocument<AetheriaRuntimeStarbridgeSessionDocument>(registry),
                CultNetDocumentBinding.ForDocument<AetheriaRuntimeStarbridgeSessionSummaryDocument>(registry),
                CultNetDocumentBinding.ForDocument<AetheriaRuntimeDaemonSoaViewDocument>(registry),
                CultNetDocumentBinding.ForDocument<AetheriaRuntimeVerseAuthorityPolicyDocument>(registry),
                CultNetDocumentBinding.ForDocument<AetheriaRuntimeAuthorityLeaseDocument>(registry),
                CultNetDocumentBinding.ForDocument<AetheriaRuntimeDaemonCommandDocument>(registry),
                CultNetDocumentBinding.ForDocument<AetheriaRuntimeCommittedCommandFactDocument>(registry),
                CultNetDocumentBinding.ForDocument<AetheriaRuntimeEveCommandDocument>(registry),
                CultNetDocumentBinding.ForDocument<EveSurfaceState>(registry),
                CultNetDocumentBinding.ForDocument<EveProviderAdvertisementState>(registry)
            });
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
        typeof(AetheriaPlayerSettings),
        typeof(AetheriaLoadoutTemplate),
        typeof(AetheriaRunState),
        typeof(AetheriaZoneState),
        typeof(AetheriaEntitySnapshot),
        typeof(AetheriaVerseHostSettings),
        typeof(AetheriaEveCommandAcceptanceStatus),
        typeof(AetheriaRuntimeDaemonProviderAdvertisementDocument),
        typeof(AetheriaRuntimeDaemonHealthDocument),
        typeof(AetheriaRuntimeDaemonCommandBoundaryDocument),
        typeof(AetheriaRuntimeDaemonFrameDocument),
        typeof(AetheriaRuntimeRtsViewportDocument),
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
        typeof(AetheriaRuntimeDaemonSoaViewDocument),
        typeof(AetheriaRuntimeVerseAuthorityPolicyDocument),
        typeof(AetheriaRuntimeAuthorityLeaseDocument),
        typeof(AetheriaRuntimeDaemonCommandDocument),
        typeof(AetheriaRuntimeCommittedCommandFactDocument),
        typeof(AetheriaRuntimeEveCommandDocument),
        typeof(EveSurfaceState),
        typeof(EveProviderAdvertisementState)
    ];
}
