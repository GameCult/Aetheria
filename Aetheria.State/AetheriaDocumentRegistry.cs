using Aetheria.State.Documents;
using GameCult.Caching;
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
                CultNetDocumentBinding.ForDocument<AetheriaRunState>(registry),
                CultNetDocumentBinding.ForDocument<AetheriaZoneState>(registry),
                CultNetDocumentBinding.ForDocument<AetheriaEntitySnapshot>(registry)
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
        typeof(AetheriaRunState),
        typeof(AetheriaZoneState),
        typeof(AetheriaEntitySnapshot)
    ];
}
