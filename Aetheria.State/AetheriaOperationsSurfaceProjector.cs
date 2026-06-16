using Aetheria.State.Documents;

namespace Aetheria.State;

public static class AetheriaOperationsSurfaceProjector
{
    public const string SurfaceKey = "eve:surface:aetheria.operations";
    public const string SurfaceId = "aetheria.operations";

    public static EveSurfaceState Build(
        AetheriaRuntimeCommitDrainStatus drainStatus,
        AetheriaEveCommandDrainStatus? eveCommandStatus = null,
        AetheriaRuntimeSession? runtimeSession = null,
        long version = 1)
    {
        var updatedAtUtc = LatestTimestamp(
            drainStatus.LastPollAtUtc,
            eveCommandStatus?.LastPollAtUtc,
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
                        "aetheria.operations.commitDrain",
                        "card",
                        [("title", "Runtime Commit Drain")],
                        Metric("commitDrain.status", "Status", drainStatus.Status),
                        Metric("commitDrain.pending", "Pending Before Apply", drainStatus.PendingBeforeApply.ToString()),
                        Metric("commitDrain.applied", "Commands Applied", drainStatus.CommandsApplied.ToString()),
                        Metric("commitDrain.settings", "Settings", drainStatus.AppliedPlayerSettings.ToString()),
                        Metric("commitDrain.loadouts", "Loadouts", drainStatus.AppliedLoadoutTemplates.ToString()),
                        Metric("commitDrain.runs", "Run Checkpoints", drainStatus.AppliedRunCheckpoints.ToString()),
                        Metric("commitDrain.failures", "Consecutive Failures", drainStatus.ConsecutiveFailures.ToString()),
                        Row(
                            "commitDrain.last",
                            ("runtime", drainStatus.RuntimeId),
                            ("lastPoll", drainStatus.LastPollAtUtc),
                            ("lastApplied", drainStatus.LastAppliedAtUtc),
                            ("error", drainStatus.LastError))),
                    Node(
                        "aetheria.operations.eveCommandDrain",
                        "card",
                        [("title", "Eve Command Drain")],
                        Metric("eveCommandDrain.status", "Status", eveCommandStatus?.Status ?? "missing"),
                        Metric("eveCommandDrain.pending", "Pending Before Apply", (eveCommandStatus?.PendingBeforeApply ?? 0).ToString()),
                        Metric("eveCommandDrain.accepted", "Commands Accepted", (eveCommandStatus?.CommandsAccepted ?? 0).ToString()),
                        Metric("eveCommandDrain.rejected", "Commands Rejected", (eveCommandStatus?.CommandsRejected ?? 0).ToString()),
                        Metric("eveCommandDrain.catalogRefreshes", "Catalog Refreshes", (eveCommandStatus?.AppliedCatalogRefreshes ?? 0).ToString()),
                        Metric("eveCommandDrain.operationsRefreshes", "Operations Refreshes", (eveCommandStatus?.AppliedOperationsRefreshes ?? 0).ToString()),
                        Metric("eveCommandDrain.playerSettings", "Player Settings Commands", (eveCommandStatus?.AppliedPlayerSettingsCommands ?? 0).ToString()),
                        Metric("eveCommandDrain.failures", "Consecutive Failures", (eveCommandStatus?.ConsecutiveFailures ?? 0).ToString()),
                        Row(
                            "eveCommandDrain.last",
                            ("runtime", eveCommandStatus?.RuntimeId ?? ""),
                            ("lastPoll", eveCommandStatus?.LastPollAtUtc ?? ""),
                            ("lastAccepted", eveCommandStatus?.LastAcceptedAtUtc ?? ""),
                            ("lastRejected", eveCommandStatus?.LastRejectedCommand ?? ""),
                            ("rejectedReason", eveCommandStatus?.LastRejectedReason ?? ""),
                            ("error", eveCommandStatus?.LastError ?? ""))),
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
                    Command = "aetheria.operations.refresh",
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
