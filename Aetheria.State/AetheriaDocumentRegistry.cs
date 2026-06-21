using Aetheria.State.Documents;
using GameCult.Caching;
using GameCult.Aetheria.State.Unity;
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
                CultNetDocumentBinding.ForDocument<AetheriaRuntimeDaemonSoaViewDocument>(registry),
                CultNetDocumentBinding.ForDocument<AetheriaRuntimeDaemonCommandDocument>(registry),
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
        typeof(AetheriaRuntimeDaemonSoaViewDocument),
        typeof(AetheriaRuntimeDaemonCommandDocument),
        typeof(AetheriaRuntimeEveCommandDocument),
        typeof(EveSurfaceState),
        typeof(EveProviderAdvertisementState)
    ];
}
