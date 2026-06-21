using Aetheria.State.Documents;
using GameCult.Aetheria.State.Verse;

namespace Aetheria.State;

public static class AetheriaOperationsSurfaceProjector
{
    public const string SurfaceKey = "eve:surface:aetheria.operations";
    public const string SurfaceId = AetheriaRuntimeOperationsCommands.SurfaceId;

    public static EveSurfaceState Build(
        AetheriaEveCommandAcceptanceStatus? eveCommandStatus = null,
        AetheriaVerseHostSettings? verseHostSettings = null,
        AetheriaRuntimeSession? runtimeSession = null,
        long version = 1)
    {
        var normalizedVerseHost = AetheriaVerseHostSettingsNormalizer.Normalize(verseHostSettings);
        var updatedAtUtc = LatestTimestamp(
            eveCommandStatus?.LastPollAtUtc,
            normalizedVerseHost.LastUpdatedAtUtc,
            runtimeSession?.LastSeenAtUtc);
        return new EveSurfaceState
        {
            ProviderId = "aetheria",
            ProviderKind = "game.runtime",
            Title = "Aetheria Operations",
            Version = version,
            UpdatedAtUtc = updatedAtUtc,
            Surface = new EveSurface
            {
                Id = SurfaceId,
                Root = Node(
                    "aetheria.operations.root",
                    "surface",
                    [],
                    Node(
                        "aetheria.operations.eveCommandAcceptance",
                        "card",
                        [("title", "Eve Request Acceptance")],
                        Metric("eveCommandAcceptance.status", "Status", eveCommandStatus?.Status ?? "missing"),
                        Metric("eveCommandAcceptance.observed", "Observed Before Accept", (eveCommandStatus?.ObservedBeforeAccept ?? 0).ToString()),
                        Metric("eveCommandAcceptance.accepted", "Commands Accepted", (eveCommandStatus?.CommandsAccepted ?? 0).ToString()),
                        Metric("eveCommandAcceptance.rejected", "Commands Rejected", (eveCommandStatus?.CommandsRejected ?? 0).ToString()),
                        Metric("eveCommandAcceptance.catalogRefreshes", "Catalog Refreshes", (eveCommandStatus?.AppliedCatalogRefreshes ?? 0).ToString()),
                        Metric("eveCommandAcceptance.operationsRefreshes", "Operations Refreshes", (eveCommandStatus?.AppliedOperationsRefreshes ?? 0).ToString()),
                        Metric("eveCommandAcceptance.playerSettings", "Player Settings Commands", (eveCommandStatus?.AppliedPlayerSettingsCommands ?? 0).ToString()),
                        Metric("eveCommandAcceptance.inputSettings", "Input Settings Commands", (eveCommandStatus?.AppliedInputSettingsCommands ?? 0).ToString()),
                        Metric("eveCommandAcceptance.loadoutTemplates", "Loadout Template Commands", (eveCommandStatus?.AppliedLoadoutTemplateCommands ?? 0).ToString()),
                        Metric("eveCommandAcceptance.verseHost", "Verse Host Commands", (eveCommandStatus?.AppliedVerseHostCommands ?? 0).ToString()),
                        Metric("eveCommandAcceptance.failures", "Consecutive Failures", (eveCommandStatus?.ConsecutiveFailures ?? 0).ToString()),
                        Row(
                            "eveCommandAcceptance.last",
                            ("runtime", eveCommandStatus?.RuntimeId ?? ""),
                            ("lastPoll", eveCommandStatus?.LastPollAtUtc ?? ""),
                            ("lastAccepted", eveCommandStatus?.LastAcceptedAtUtc ?? ""),
                            ("lastRejected", eveCommandStatus?.LastRejectedCommand ?? ""),
                            ("rejectedReason", eveCommandStatus?.LastRejectedReason ?? ""),
                            ("error", eveCommandStatus?.LastError ?? ""))),
                    Node(
                        "aetheria.operations.verseHost",
                        "card",
                        [("title", "Verse Host")],
                        Metric("verseHost.visibility", "Visibility", normalizedVerseHost.Visibility),
                        Metric("verseHost.service", "Service", normalizedVerseHost.ServiceId),
                        Metric("verseHost.verse", "Verse", normalizedVerseHost.VerseId),
                        Row(
                            "verseHost.identity",
                            ("rootVerse", normalizedVerseHost.RootVerse),
                            ("canonicalService", normalizedVerseHost.CanonicalService),
                            ("locatedService", normalizedVerseHost.LocatedService),
                            ("cultMeshAddress", normalizedVerseHost.CultMeshAddress))),
                    Node(
                        "aetheria.operations.runtimeSession",
                        "card",
                        [("title", "Runtime Session")],
                        Metric("runtimeSession.status", "Status", runtimeSession?.Status ?? "missing"),
                        Metric("runtimeSession.role", "Role", runtimeSession?.Role ?? ""),
                        Row(
                            "runtimeSession.last",
                            ("runtime", runtimeSession?.RuntimeId ?? ""),
                            ("started", runtimeSession?.StartedAtUtc ?? ""),
                            ("lastSeen", runtimeSession?.LastSeenAtUtc ?? ""))))
            },
            Commands =
            [
                new EveCommandTemplate
                {
                    Command = AetheriaRuntimeOperationsCommands.Refresh,
                    Label = "Refresh",
                    Transport = "cultmesh"
                }
            ]
        };
    }

    private static string LatestTimestamp(params string?[] timestamps)
    {
        var latest = "";
        foreach (var timestamp in timestamps)
        {
            if (!string.IsNullOrWhiteSpace(timestamp) &&
                (string.IsNullOrWhiteSpace(latest) || string.CompareOrdinal(timestamp, latest) > 0))
            {
                latest = timestamp;
            }
        }

        return latest;
    }

    private static EveSurfaceComponent Metric(string id, string label, string value)
    {
        return Node(id, "metric", [("label", label), ("value", value)]);
    }

    private static EveSurfaceComponent Row(string id, params (string Key, string Value)[] props)
    {
        return Node(id, "row", props);
    }

    private static EveSurfaceComponent Node(
        string id,
        string kind,
        (string Key, string Value)[] props,
        params EveSurfaceComponent[] children)
    {
        return new EveSurfaceComponent
        {
            Id = id,
            Kind = kind,
            Props = props.ToDictionary(prop => prop.Key, prop => prop.Value),
            Children = children
        };
    }
}
