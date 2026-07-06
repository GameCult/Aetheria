using System;
using System.Collections.Generic;
using System.Linq;

namespace GameCult.Aetheria.State.Verse
{
    public static class AetheriaRuntimeClientTargetSurfaceBuilder
    {
        public static AetheriaRuntimeSurfaceDocument Build(
            AetheriaRuntimeStateBootReport stateBoot,
            AetheriaRuntimeVerseHostSettingsDocument verseHost,
            string updatedAtUtc,
            long version = 1)
        {
            var targetKind = stateBoot.TargetKind ?? AetheriaRuntimeClientTargetKinds.StateFile;
            var targetTitle = stateBoot.Title ?? "";
            var targetVerseId = stateBoot.VerseId ?? "";
            var targetRuntimeId = string.IsNullOrWhiteSpace(stateBoot.RuntimeId) ? AetheriaRuntimeStateBoundary.DefaultClientRuntimeId : stateBoot.RuntimeId;
            var targetCultMeshAddress = stateBoot.CultMeshAddress ?? "";
            var targetStateFilePath = stateBoot.StateFilePath ?? "";
            var targetReplicaStateFilePath = stateBoot.ReplicaStateFilePath ?? "";
            var discoveryEndpointsText = string.Join(", ", stateBoot.DiscoveryEndpoints ?? Array.Empty<string>());
            var discoveredVerses = stateBoot.DiscoveredVerses ?? Array.Empty<AetheriaRuntimeDiscoveredVerse>();
            var targetSource = stateBoot.TargetSource ?? "";
            var bootFailureMessage = stateBoot.FailureMessage ?? "";
            var hostTitle = verseHost?.Title ?? targetTitle;
            var hostVerseId = verseHost?.VerseId ?? targetVerseId;
            var hostVisibility = verseHost?.Visibility ?? "unknown";
            var hostCultMeshAddress = verseHost?.CultMeshAddress ?? targetCultMeshAddress;
            updatedAtUtc ??= "";

            var targetKindLabel = string.Equals(targetKind, AetheriaRuntimeClientTargetKinds.CultMeshVerse, StringComparison.Ordinal)
                ? "CultMesh Verse"
                : "Local State File";
            var visibilityLabel = string.IsNullOrWhiteSpace(hostVisibility) ? "unknown" : hostVisibility;
            var visibilityActionLabel = string.Equals(visibilityLabel, "public", StringComparison.OrdinalIgnoreCase)
                ? "Make Private"
                : "Make Public";
            var lastDiscoveryLabel = string.IsNullOrWhiteSpace(stateBoot.LastDiscoveryAtUtc) ? "never" : stateBoot.LastDiscoveryAtUtc;
            var lastReplicaSyncLabel = string.IsNullOrWhiteSpace(stateBoot.LastReplicaSyncAtUtc) ? "never" : stateBoot.LastReplicaSyncAtUtc;
            var summaryNote = !stateBoot.SupportsLocalStateFileRead && !string.IsNullOrWhiteSpace(bootFailureMessage)
                ? bootFailureMessage
                : string.Equals(targetSource, "state-path-override", StringComparison.Ordinal)
                    ? "AETHERIA_STATE_PATH is overriding the persisted client target. Update the environment if you want boot to follow the saved target again."
                    : "Client target edits persist in aetheria-client.cc. Verse discovery and selection mutate the same typed owner. Verse visibility changes append provider-owned Eve requests for the daemon bridge. Remote Verse targets hydrate a cache-only local replica before observers read them.";

            var discoveryChildren = new List<AetheriaRuntimeSurfaceComponent>
            {
                TextInput(
                    "aetheria.clientTarget.discovery.endpoints",
                    "Discovery Endpoints",
                    discoveryEndpointsText,
                    AetheriaRuntimeClientTargetCommands.SetDiscoveryEndpoints),
                Metric("aetheria.clientTarget.discovery.lastScan", "Last Scan", lastDiscoveryLabel)
            };

            if (!string.IsNullOrWhiteSpace(stateBoot.LastDiscoveryError))
            {
                discoveryChildren.Add(Text(
                    "aetheria.clientTarget.discovery.error",
                    stateBoot.LastDiscoveryError));
            }

            if (discoveredVerses.Length == 0)
            {
                discoveryChildren.Add(Text(
                    "aetheria.clientTarget.discovery.empty",
                    string.IsNullOrWhiteSpace(discoveryEndpointsText)
                        ? "Add one or more cultnet:// discovery endpoints to scan for public or federated Aetheria Verses."
                        : "No Verse descriptors are cached yet for the configured discovery endpoints."));
            }
            else
            {
                discoveryChildren.Add(Metric(
                    "aetheria.clientTarget.discovery.count",
                    "Known Verses",
                    discoveredVerses.Length.ToString()));
                discoveryChildren.Add(Row(
                    "aetheria.clientTarget.discovery.list",
                    discoveredVerses
                        .Select((verse, index) => BuildDiscoveredVerseNode(targetKind, targetVerseId, verse, index))
                        .ToArray()));
            }

            discoveryChildren.Add(ButtonRow(
                "aetheria.clientTarget.discovery.actions",
                Button(
                    "aetheria.clientTarget.discovery.scan",
                    "Discover",
                    AetheriaRuntimeClientTargetCommands.DiscoverVerses),
                Button(
                    "aetheria.clientTarget.discovery.refresh",
                    "Refresh",
                    AetheriaRuntimeClientTargetCommands.Refresh)));

            return new AetheriaRuntimeSurfaceDocument(
                providerId: "aetheria",
                providerKind: "game.menu",
                title: "Aetheria Verse Settings",
                version: version,
                updatedAtUtc: updatedAtUtc,
                surface: new AetheriaRuntimeSurfaceTree(
                    AetheriaRuntimeClientTargetCommands.SurfaceId,
                    Node(
                        "aetheria.clientTarget.root",
                        "surface",
                        Array.Empty<(string Key, string Value)>(),
                        Node(
                            "aetheria.clientTarget.summary",
                            "card",
                            new[] { ("title", "Client Target") },
                            Metric("aetheria.clientTarget.summary.target", "Target", stateBoot.TargetLabel),
                            Metric("aetheria.clientTarget.summary.transport", "Transport", targetKindLabel),
                            Metric("aetheria.clientTarget.summary.source", "Target Source", targetSource),
                            Text("aetheria.clientTarget.summary.note", summaryNote)),
                        Node(
                            "aetheria.clientTarget.target",
                            "card",
                            new[] { ("title", "Target Fields") },
                            TextInput(
                                "aetheria.clientTarget.target.title",
                                "Title",
                                targetTitle,
                                AetheriaRuntimeClientTargetCommands.SetTitle),
                            TextInput(
                                "aetheria.clientTarget.target.verseId",
                                "Verse Id",
                                targetVerseId,
                                AetheriaRuntimeClientTargetCommands.SetVerseId),
                            TextInput(
                                "aetheria.clientTarget.target.runtimeId",
                                "Runtime Id",
                                targetRuntimeId,
                                AetheriaRuntimeClientTargetCommands.SetRuntimeId),
                            TextInput(
                                "aetheria.clientTarget.target.cultMeshAddress",
                                "CultMesh Address",
                                targetCultMeshAddress,
                                AetheriaRuntimeClientTargetCommands.SetCultMeshAddress),
                            TextInput(
                                "aetheria.clientTarget.target.stateFilePath",
                                "State File Path",
                                targetStateFilePath,
                                AetheriaRuntimeClientTargetCommands.SetStateFilePath),
                            Metric(
                                "aetheria.clientTarget.target.replicaStateFilePath",
                                "Replica State File",
                                targetReplicaStateFilePath),
                            Metric(
                                "aetheria.clientTarget.target.replicaSyncAt",
                                "Replica Sync",
                                lastReplicaSyncLabel),
                            Text(
                                "aetheria.clientTarget.target.replicaSyncError",
                                stateBoot.LastReplicaSyncError),
                            ButtonRow(
                                "aetheria.clientTarget.target.actions",
                                Button(
                                    "aetheria.clientTarget.target.cycleTransport",
                                    string.Equals(targetKind, AetheriaRuntimeClientTargetKinds.CultMeshVerse, StringComparison.Ordinal)
                                        ? "Use Local State File"
                                        : "Use CultMesh Verse",
                                    AetheriaRuntimeClientTargetCommands.CycleTargetKind),
                                Button(
                                    "aetheria.clientTarget.target.syncReplica",
                                    "Sync Replica",
                                    AetheriaRuntimeClientTargetCommands.SyncReplica),
                                Button(
                                    "aetheria.clientTarget.target.refresh",
                                    "Refresh",
                                    AetheriaRuntimeClientTargetCommands.Refresh))),
                        Node(
                            "aetheria.clientTarget.discovery",
                            "card",
                            new[] { ("title", "Verse Discovery") },
                            discoveryChildren.ToArray()),
                        Node(
                            "aetheria.clientTarget.host",
                            "card",
                            new[] { ("title", "Daemon Verse Host") },
                            Metric("aetheria.clientTarget.host.verse", "Verse", BuildVerseLabel(hostTitle, hostVerseId)),
                            Metric("aetheria.clientTarget.host.visibility", "Visibility", visibilityLabel),
                            Metric("aetheria.clientTarget.host.cultMesh", "CultMesh", hostCultMeshAddress),
                            ButtonRow(
                                "aetheria.clientTarget.host.actions",
                                Button(
                                    "aetheria.clientTarget.host.toggleVisibility",
                                    visibilityActionLabel,
                                    AetheriaRuntimeVerseHostCommands.CycleVisibility),
                                Button(
                                    "aetheria.clientTarget.host.refresh",
                                    "Refresh Host",
                                    AetheriaRuntimeVerseHostCommands.Refresh))),
                        Node(
                            "aetheria.clientTarget.host.noteCard",
                            "card",
                            new[] { ("title", "Ownership") },
                            Text(
                                "aetheria.clientTarget.host.note",
                                "Client target edits persist through the managed target document. Visibility changes append provider-owned Eve requests against the selected Verse target."))),
                    Array.Empty<AetheriaRuntimeSurfaceStyleToken>()),
                commands: new[]
                {
                    new AetheriaRuntimeSurfaceCommandTemplate(
                        AetheriaRuntimeClientTargetCommands.Refresh,
                        "Refresh",
                        AetheriaRuntimeSurfaceCommandTemplate.CultMeshTransport),
                    new AetheriaRuntimeSurfaceCommandTemplate(
                        AetheriaRuntimeClientTargetCommands.CycleTargetKind,
                        "Cycle Transport",
                        AetheriaRuntimeSurfaceCommandTemplate.CultMeshTransport),
                    new AetheriaRuntimeSurfaceCommandTemplate(
                        AetheriaRuntimeClientTargetCommands.SetTitle,
                        "Set Title",
                        AetheriaRuntimeSurfaceCommandTemplate.CultMeshTransport),
                    new AetheriaRuntimeSurfaceCommandTemplate(
                        AetheriaRuntimeClientTargetCommands.SetVerseId,
                        "Set Verse Id",
                        AetheriaRuntimeSurfaceCommandTemplate.CultMeshTransport),
                    new AetheriaRuntimeSurfaceCommandTemplate(
                        AetheriaRuntimeClientTargetCommands.SetRuntimeId,
                        "Set Runtime Id",
                        AetheriaRuntimeSurfaceCommandTemplate.CultMeshTransport),
                    new AetheriaRuntimeSurfaceCommandTemplate(
                        AetheriaRuntimeClientTargetCommands.SetCultMeshAddress,
                        "Set CultMesh Address",
                        AetheriaRuntimeSurfaceCommandTemplate.CultMeshTransport),
                    new AetheriaRuntimeSurfaceCommandTemplate(
                        AetheriaRuntimeClientTargetCommands.SetStateFilePath,
                        "Set State File Path",
                        AetheriaRuntimeSurfaceCommandTemplate.CultMeshTransport),
                    new AetheriaRuntimeSurfaceCommandTemplate(
                        AetheriaRuntimeClientTargetCommands.SetDiscoveryEndpoints,
                        "Set Discovery Endpoints",
                        AetheriaRuntimeSurfaceCommandTemplate.CultMeshTransport),
                    new AetheriaRuntimeSurfaceCommandTemplate(
                        AetheriaRuntimeClientTargetCommands.DiscoverVerses,
                        "Discover Verses",
                        AetheriaRuntimeSurfaceCommandTemplate.CultMeshTransport),
                    new AetheriaRuntimeSurfaceCommandTemplate(
                        AetheriaRuntimeClientTargetCommands.SelectDiscoveredVerse,
                        "Select Discovered Verse",
                        AetheriaRuntimeSurfaceCommandTemplate.CultMeshTransport),
                    new AetheriaRuntimeSurfaceCommandTemplate(
                        AetheriaRuntimeClientTargetCommands.SyncReplica,
                        "Sync Replica",
                        AetheriaRuntimeSurfaceCommandTemplate.CultMeshTransport),
                    new AetheriaRuntimeSurfaceCommandTemplate(
                        AetheriaRuntimeVerseHostCommands.CycleVisibility,
                        "Toggle Visibility",
                        "cultmesh"),
                    new AetheriaRuntimeSurfaceCommandTemplate(
                        AetheriaRuntimeVerseHostCommands.Refresh,
                        "Refresh Host",
                        "cultmesh")
                });
        }

        private static AetheriaRuntimeSurfaceComponent Metric(string id, string label, string value)
        {
            return Node(id, "metric", new[] { ("label", label), ("value", value ?? "") });
        }

        private static AetheriaRuntimeSurfaceComponent Text(string id, string value)
        {
            return Node(id, "text", new[] { ("value", value ?? "") });
        }

        private static AetheriaRuntimeSurfaceComponent Button(string id, string label, string command)
        {
            return Node(id, "control.button", new[] { ("label", label ?? ""), ("command", command ?? "") });
        }

        private static AetheriaRuntimeSurfaceComponent TextInput(string id, string label, string value, string command)
        {
            return Node(
                id,
                "control.text",
                new[] { ("label", label ?? ""), ("value", value ?? ""), ("command", command ?? "") });
        }

        private static AetheriaRuntimeSurfaceComponent ButtonRow(
            string id,
            params AetheriaRuntimeSurfaceComponent[] children)
        {
            return Node(id, "row", Array.Empty<(string Key, string Value)>(), children);
        }

        private static AetheriaRuntimeSurfaceComponent Row(
            string id,
            params AetheriaRuntimeSurfaceComponent[] children)
        {
            return Node(id, "inspector.kv", Array.Empty<(string Key, string Value)>(), children);
        }

        private static AetheriaRuntimeSurfaceComponent Node(
            string id,
            string kind,
            IEnumerable<(string Key, string Value)> props,
            params AetheriaRuntimeSurfaceComponent[] children)
        {
            return new AetheriaRuntimeSurfaceComponent(
                id,
                kind,
                props.ToDictionary(prop => prop.Key, prop => prop.Value, StringComparer.Ordinal),
                children ?? Array.Empty<AetheriaRuntimeSurfaceComponent>());
        }

        private static AetheriaRuntimeSurfaceComponent BuildDiscoveredVerseNode(
            string targetKind,
            string targetVerseId,
            AetheriaRuntimeDiscoveredVerse verse,
            int index)
        {
            var verseLabel = BuildVerseLabel(verse);
            var address = verse.DiscoveryEndpoints.FirstOrDefault() ?? "";
            var isSelected = string.Equals(targetKind, AetheriaRuntimeClientTargetKinds.CultMeshVerse, StringComparison.Ordinal) &&
                             string.Equals(targetVerseId, verse.VerseId, StringComparison.Ordinal);
            var buttonLabel = isSelected ? "Selected" : "Select";

            return Node(
                $"aetheria.clientTarget.discovery.verse.{index}",
                "inspector.kv",
                Array.Empty<(string Key, string Value)>(),
                Metric($"aetheria.clientTarget.discovery.verse.{index}.label", "Verse", verseLabel),
                Metric($"aetheria.clientTarget.discovery.verse.{index}.authority", "Authority", verse.AuthorityModel),
                Metric($"aetheria.clientTarget.discovery.verse.{index}.transport", "Transport", verse.TransportVersion),
                Metric($"aetheria.clientTarget.discovery.verse.{index}.endpoint", "Endpoint", address),
                Text(
                    $"aetheria.clientTarget.discovery.verse.{index}.description",
                    string.IsNullOrWhiteSpace(verse.Description)
                        ? "No public description."
                        : verse.Description),
                Node(
                    $"aetheria.clientTarget.discovery.verse.{index}.action",
                    "control.button",
                    new[]
                    {
                        ("label", buttonLabel),
                        ("command", AetheriaRuntimeClientTargetCommands.SelectDiscoveredVerse),
                        ("verseId", verse.VerseId ?? ""),
                        ("title", verse.DisplayName ?? ""),
                        ("cultMeshAddress", address),
                        ("discoveryEndpoints", string.Join(", ", verse.DiscoveryEndpoints ?? Array.Empty<string>()))
                    }));
        }

        private static string BuildVerseLabel(AetheriaRuntimeDiscoveredVerse verse)
        {
            return verse == null
                ? "Unknown Verse"
                : BuildVerseLabel(verse.DisplayName, verse.VerseId);
        }

        private static string BuildVerseLabel(string displayName, string verseId)
        {
            if (string.IsNullOrWhiteSpace(displayName))
                return string.IsNullOrWhiteSpace(verseId) ? "Unknown Verse" : verseId;

            return string.IsNullOrWhiteSpace(verseId) || string.Equals(displayName, verseId, StringComparison.Ordinal)
                ? displayName
                : $"{displayName} ({verseId})";
        }
    }
}
