using Aetheria.State.Documents;
using GameCult.Mesh;

namespace Aetheria.State;

public static class AetheriaVerseCatalogProjector
{
    private const string TransportVersion = "cultmesh.v0";
    private const string RulesEpoch = "aetheria-runtime-state.v1";

    public static CultMeshVerseDescriptor Build(AetheriaVerseHostSettings? settings)
    {
        var normalized = AetheriaVerseHostSettingsNormalizer.Normalize(settings);
        var discoveryEndpoint = BuildDiscoveryEndpoint(normalized);
        var rulesHash = CultMeshVerseDescriptor.ComputeRulesHash(
            "aetheria",
            RulesEpoch,
            normalized.RootVerse,
            normalized.CanonicalService);

        return new CultMeshVerseDescriptor(
            normalized.VerseId,
            normalized.Title,
            CultMeshVerseAuthorityModel.OperatorCluster,
            new CultMeshVerseCompatibility(TransportVersion, rulesHash),
            discoveryEndpoints: new[] { discoveryEndpoint },
            authorityRuntimeIds: new[] { normalized.ServiceId },
            parentVerseId: normalized.RootVerse,
            description: BuildDescription(normalized));
    }

    public static string BuildDiscoveryEndpoint(AetheriaVerseHostSettings? settings)
    {
        var normalized = AetheriaVerseHostSettingsNormalizer.Normalize(settings);
        var host = normalized.LocatedService;
        if (string.IsNullOrWhiteSpace(host))
            host = normalized.CanonicalService;
        if (string.IsNullOrWhiteSpace(host))
            host = "localhost";

        host = host.Trim();
        var slashIndex = host.IndexOf('/', StringComparison.Ordinal);
        if (slashIndex >= 0)
            host = host.Substring(0, slashIndex);

        return $"cultnet://{host}:3075";
    }

    private static string BuildDescription(AetheriaVerseHostSettings settings)
    {
        var visibility = string.IsNullOrWhiteSpace(settings.Visibility) ? "private" : settings.Visibility;
        return $"{settings.Title} Verse host ({visibility})";
    }
}
