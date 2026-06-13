using Aetheria.State.Documents;

namespace Aetheria.State;

public static class AetheriaOperationsSurfaceProjector
{
    public const string SurfaceKey = "eve:surface:aetheria.operations";
    public const string SurfaceId = "aetheria.operations";

    public static EveSurfaceState Build(AetheriaRuntimeCommitDrainStatus drainStatus, long version = 1)
    {
        return new EveSurfaceState
        {
            ProviderId = "aetheria",
            ProviderKind = "game.runtime",
            Title = "Aetheria Operations",
            Version = version,
            UpdatedAtUtc = drainStatus.LastPollAtUtc,
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
                            ("error", drainStatus.LastError))))
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
