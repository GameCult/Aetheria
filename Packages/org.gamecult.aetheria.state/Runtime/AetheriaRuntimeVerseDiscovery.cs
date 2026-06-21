using System;
using System.Linq;
using GameCult.Mesh;

#nullable enable

namespace GameCult.Aetheria.State.Verse
{
    public static class AetheriaRuntimeVerseDiscovery
    {
        public static AetheriaRuntimeClientTargetDocument RefreshClientTarget(
            string clientTargetPath,
            string defaultStateFilePath)
        {
            var discoveredAtUtc = DateTime.UtcNow.ToString("O");
            var target = AetheriaRuntimeClientTargetStore.ReadOrInitialize(clientTargetPath, defaultStateFilePath);
            var endpoints = target.DiscoveryEndpoints?
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal)
                .ToArray()
                ?? Array.Empty<string>();

            if (endpoints.Length == 0)
            {
                return AetheriaRuntimeClientTargetStore.Update(
                    clientTargetPath,
                    defaultStateFilePath,
                    document =>
                    {
                        document.DiscoveryEndpoints = endpoints;
                        document.DiscoveredVerses = Array.Empty<AetheriaRuntimeDiscoveredVerse>();
                        document.LastDiscoveryAtUtc = discoveredAtUtc;
                        document.LastDiscoveryError = "No CultMesh discovery endpoints are configured.";
                        document.UpdatedAtUtc = discoveredAtUtc;
                    });
            }

            try
            {
                using var catalog = CultMesh.CreateVerseCatalog();
                var discoveryClient = CultMesh.CreateVerseDiscoveryClient();
                discoveryClient.DiscoverAsync(catalog, endpoints)
                    .GetAwaiter()
                    .GetResult();

                var discoveredVerses = catalog.Verses
                    .Select(verse => new AetheriaRuntimeDiscoveredVerse
                    {
                        VerseId = verse.VerseId ?? "",
                        DisplayName = verse.DisplayName ?? "",
                        AuthorityModel = verse.AuthorityModel.ToString(),
                        TransportVersion = verse.Compatibility.TransportVersion ?? "",
                        RulesHash = verse.Compatibility.RulesHash ?? "",
                        Description = verse.Description ?? "",
                        DiscoveryEndpoints = verse.DiscoveryEndpoints.ToArray(),
                        AuthorityRuntimeIds = verse.AuthorityRuntimeIds.ToArray(),
                        ParentVerseId = verse.ParentVerseId ?? ""
                    })
                    .OrderBy(verse => verse.DisplayName, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(verse => verse.VerseId, StringComparer.Ordinal)
                    .ToArray();

                return AetheriaRuntimeClientTargetStore.Update(
                    clientTargetPath,
                    defaultStateFilePath,
                    document =>
                    {
                        document.DiscoveryEndpoints = endpoints;
                        document.DiscoveredVerses = discoveredVerses;
                        document.LastDiscoveryAtUtc = discoveredAtUtc;
                        document.LastDiscoveryError = "";
                        document.UpdatedAtUtc = discoveredAtUtc;
                    });
            }
            catch (Exception ex)
            {
                return AetheriaRuntimeClientTargetStore.Update(
                    clientTargetPath,
                    defaultStateFilePath,
                    document =>
                    {
                        document.DiscoveryEndpoints = endpoints;
                        document.DiscoveredVerses = Array.Empty<AetheriaRuntimeDiscoveredVerse>();
                        document.LastDiscoveryAtUtc = discoveredAtUtc;
                        document.LastDiscoveryError = ex.Message ?? ex.GetType().Name;
                        document.UpdatedAtUtc = discoveredAtUtc;
                    });
            }
        }
    }
}
