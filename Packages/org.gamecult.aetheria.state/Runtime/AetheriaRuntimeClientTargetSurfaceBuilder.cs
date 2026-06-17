using System;
using System.Collections.Generic;
using System.Linq;

namespace GameCult.Aetheria.State.Unity
{
    public sealed class AetheriaRuntimeClientTargetSurfaceState
    {
        public AetheriaRuntimeClientTargetSurfaceState(
            string targetKind,
            string targetTitle,
            string targetVerseId,
            string targetCultMeshAddress,
            string targetStateFilePath,
            string discoveryEndpointsText,
            IReadOnlyList<AetheriaRuntimeDiscoveredVerse> discoveredVerses,
            string lastDiscoveryAtUtc,
            string lastDiscoveryError,
            string targetSource,
            bool supportsLocalStateFileRead,
            string bootFailureMessage,
            string hostTitle,
            string hostVerseId,
            string hostVisibility,
            string hostCultMeshAddress,
            string updatedAtUtc)
        {
            TargetKind = targetKind ?? "";
            TargetTitle = targetTitle ?? "";
            TargetVerseId = targetVerseId ?? "";
            TargetCultMeshAddress = targetCultMeshAddress ?? "";
            TargetStateFilePath = targetStateFilePath ?? "";
            DiscoveryEndpointsText = discoveryEndpointsText ?? "";
            DiscoveredVerses = discoveredVerses ?? Array.Empty<AetheriaRuntimeDiscoveredVerse>();
            LastDiscoveryAtUtc = lastDiscoveryAtUtc ?? "";
            LastDiscoveryError = lastDiscoveryError ?? "";
            TargetSource = targetSource ?? "";
            SupportsLocalStateFileRead = supportsLocalStateFileRead;
            BootFailureMessage = bootFailureMessage ?? "";
            HostTitle = hostTitle ?? "";
            HostVerseId = hostVerseId ?? "";
            HostVisibility = hostVisibility ?? "";
            HostCultMeshAddress = hostCultMeshAddress ?? "";
            UpdatedAtUtc = updatedAtUtc ?? "";
        }

        public string TargetKind { get; }
        public string TargetTitle { get; }
        public string TargetVerseId { get; }
        public string TargetCultMeshAddress { get; }
        public string TargetStateFilePath { get; }
        public string DiscoveryEndpointsText { get; }
        public IReadOnlyList<AetheriaRuntimeDiscoveredVerse> DiscoveredVerses { get; }
        public string LastDiscoveryAtUtc { get; }
        public string LastDiscoveryError { get; }
        public string TargetSource { get; }
        public bool SupportsLocalStateFileRead { get; }
        public string BootFailureMessage { get; }
        public string HostTitle { get; }
        public string HostVerseId { get; }
        public string HostVisibility { get; }
        public string HostCultMeshAddress { get; }
        public string UpdatedAtUtc { get; }

        public string TargetLabel =>
            string.IsNullOrWhiteSpace(TargetTitle)
                ? (string.IsNullOrWhiteSpace(TargetVerseId) ? "Unknown Verse" : TargetVerseId)
                : (string.IsNullOrWhiteSpace(TargetVerseId) || string.Equals(TargetTitle, TargetVerseId, StringComparison.Ordinal)
                    ? TargetTitle
                    : $"{TargetTitle} ({TargetVerseId})");

        public string HostLabel =>
            string.IsNullOrWhiteSpace(HostTitle)
                ? (string.IsNullOrWhiteSpace(HostVerseId) ? "Unknown Verse" : HostVerseId)
                : (string.IsNullOrWhiteSpace(HostVerseId) || string.Equals(HostTitle, HostVerseId, StringComparison.Ordinal)
                    ? HostTitle
                    : $"{HostTitle} ({HostVerseId})");
    }

    public static class AetheriaRuntimeClientTargetSurfaceBuilder
    {
        public static AetheriaRuntimeSurfaceDocument Build(
            AetheriaRuntimeClientTargetSurfaceState state,
            long version = 1)
        {
            state ??= new AetheriaRuntimeClientTargetSurfaceState(
                AetheriaRuntimeClientTargetKinds.StateFile,
                "",
                "",
                "",
                "",
                "",
                Array.Empty<AetheriaRuntimeDiscoveredVerse>(),
                "",
                "",
                "",
                supportsLocalStateFileRead: true,
                bootFailureMessage: "",
                hostTitle: "",
                hostVerseId: "",
                hostVisibility: "",
                hostCultMeshAddress: "",
                updatedAtUtc: "");

            var targetKindLabel = string.Equals(state.TargetKind, AetheriaRuntimeClientTargetKinds.CultMeshVerse, StringComparison.Ordinal)
                ? "CultMesh Verse"
                : "Local State File";
            var visibilityLabel = string.IsNullOrWhiteSpace(state.HostVisibility) ? "unknown" : state.HostVisibility;
            var visibilityActionLabel = string.Equals(visibilityLabel, "public", StringComparison.OrdinalIgnoreCase)
                ? "Make Private"
                : "Make Public";
            var lastDiscoveryLabel = string.IsNullOrWhiteSpace(state.LastDiscoveryAtUtc) ? "never" : state.LastDiscoveryAtUtc;
            var summaryNote = !state.SupportsLocalStateFileRead && !string.IsNullOrWhiteSpace(state.BootFailureMessage)
                ? state.BootFailureMessage
                : string.Equals(state.TargetSource, "state-path-override", StringComparison.Ordinal)
                    ? "AETHERIA_STATE_PATH is overriding the persisted client target. Update the environment if you want boot to follow the saved target again."
                    : "Client target edits persist in aetheria-client.cc. Verse discovery and selection mutate the same typed owner. Verse visibility changes queue provider-owned Eve commands for the daemon bridge.";

            var discoveryChildren = new List<AetheriaRuntimeSurfaceComponent>
            {
                TextInput(
                    "aetheria.clientTarget.discovery.endpoints",
                    "Discovery Endpoints",
                    state.DiscoveryEndpointsText,
                    AetheriaRuntimeClientTargetCommands.SetDiscoveryEndpoints),
                Metric("aetheria.clientTarget.discovery.lastScan", "Last Scan", lastDiscoveryLabel)
            };

            if (!string.IsNullOrWhiteSpace(state.LastDiscoveryError))
            {
                discoveryChildren.Add(Text(
                    "aetheria.clientTarget.discovery.error",
                    state.LastDiscoveryError));
            }

            if (state.DiscoveredVerses.Count == 0)
            {
                discoveryChildren.Add(Text(
                    "aetheria.clientTarget.discovery.empty",
                    string.IsNullOrWhiteSpace(state.DiscoveryEndpointsText)
                        ? "Add one or more cultnet:// discovery endpoints to scan for public or federated Aetheria Verses."
                        : "No Verse descriptors are cached yet for the configured discovery endpoints."));
            }
            else
            {
                discoveryChildren.Add(Metric(
                    "aetheria.clientTarget.discovery.count",
                    "Known Verses",
                    state.DiscoveredVerses.Count.ToString()));
                discoveryChildren.Add(Row(
                    "aetheria.clientTarget.discovery.list",
                    state.DiscoveredVerses
                        .Select((verse, index) => BuildDiscoveredVerseNode(state, verse, index))
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
                    "Refresh Shell",
                    AetheriaRuntimeClientTargetCommands.Refresh)));

            return new AetheriaRuntimeSurfaceDocument(
                providerId: "aetheria",
                providerKind: "game.menu",
                title: "Aetheria Verse Settings",
                version: version,
                updatedAtUtc: state.UpdatedAtUtc,
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
                            Metric("aetheria.clientTarget.summary.target", "Target", state.TargetLabel),
                            Metric("aetheria.clientTarget.summary.transport", "Transport", targetKindLabel),
                            Metric("aetheria.clientTarget.summary.source", "Target Source", state.TargetSource),
                            Text("aetheria.clientTarget.summary.note", summaryNote)),
                        Node(
                            "aetheria.clientTarget.target",
                            "card",
                            new[] { ("title", "Target Fields") },
                            TextInput(
                                "aetheria.clientTarget.target.title",
                                "Title",
                                state.TargetTitle,
                                AetheriaRuntimeClientTargetCommands.SetTitle),
                            TextInput(
                                "aetheria.clientTarget.target.verseId",
                                "Verse Id",
                                state.TargetVerseId,
                                AetheriaRuntimeClientTargetCommands.SetVerseId),
                            TextInput(
                                "aetheria.clientTarget.target.cultMeshAddress",
                                "CultMesh Address",
                                state.TargetCultMeshAddress,
                                AetheriaRuntimeClientTargetCommands.SetCultMeshAddress),
                            TextInput(
                                "aetheria.clientTarget.target.stateFilePath",
                                "State File Path",
                                state.TargetStateFilePath,
                                AetheriaRuntimeClientTargetCommands.SetStateFilePath),
                            ButtonRow(
                                "aetheria.clientTarget.target.actions",
                                Button(
                                    "aetheria.clientTarget.target.cycleTransport",
                                    string.Equals(state.TargetKind, AetheriaRuntimeClientTargetKinds.CultMeshVerse, StringComparison.Ordinal)
                                        ? "Use Local State File"
                                        : "Use CultMesh Verse",
                                    AetheriaRuntimeClientTargetCommands.CycleTargetKind),
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
                            Metric("aetheria.clientTarget.host.verse", "Verse", state.HostLabel),
                            Metric("aetheria.clientTarget.host.visibility", "Visibility", visibilityLabel),
                            Metric("aetheria.clientTarget.host.cultMesh", "CultMesh", state.HostCultMeshAddress),
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
                                "Client target edits are local. Visibility changes queue provider-owned Eve commands against the selected local Verse state file."))),
                    Array.Empty<AetheriaRuntimeSurfaceStyleToken>()),
                commands: new[]
                {
                    new AetheriaRuntimeSurfaceCommandTemplate(
                        AetheriaRuntimeClientTargetCommands.Refresh,
                        "Refresh",
                        "unity-uitoolkit"),
                    new AetheriaRuntimeSurfaceCommandTemplate(
                        AetheriaRuntimeClientTargetCommands.CycleTargetKind,
                        "Cycle Transport",
                        "unity-uitoolkit"),
                    new AetheriaRuntimeSurfaceCommandTemplate(
                        AetheriaRuntimeClientTargetCommands.SetTitle,
                        "Set Title",
                        "unity-uitoolkit"),
                    new AetheriaRuntimeSurfaceCommandTemplate(
                        AetheriaRuntimeClientTargetCommands.SetVerseId,
                        "Set Verse Id",
                        "unity-uitoolkit"),
                    new AetheriaRuntimeSurfaceCommandTemplate(
                        AetheriaRuntimeClientTargetCommands.SetCultMeshAddress,
                        "Set CultMesh Address",
                        "unity-uitoolkit"),
                    new AetheriaRuntimeSurfaceCommandTemplate(
                        AetheriaRuntimeClientTargetCommands.SetStateFilePath,
                        "Set State File Path",
                        "unity-uitoolkit"),
                    new AetheriaRuntimeSurfaceCommandTemplate(
                        AetheriaRuntimeClientTargetCommands.SetDiscoveryEndpoints,
                        "Set Discovery Endpoints",
                        "unity-uitoolkit"),
                    new AetheriaRuntimeSurfaceCommandTemplate(
                        AetheriaRuntimeClientTargetCommands.DiscoverVerses,
                        "Discover Verses",
                        "unity-uitoolkit"),
                    new AetheriaRuntimeSurfaceCommandTemplate(
                        AetheriaRuntimeClientTargetCommands.SelectDiscoveredVerse,
                        "Select Discovered Verse",
                        "unity-uitoolkit"),
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
            AetheriaRuntimeClientTargetSurfaceState state,
            AetheriaRuntimeDiscoveredVerse verse,
            int index)
        {
            var verseLabel = BuildVerseLabel(verse);
            var address = verse.DiscoveryEndpoints.FirstOrDefault() ?? "";
            var isSelected = string.Equals(state.TargetKind, AetheriaRuntimeClientTargetKinds.CultMeshVerse, StringComparison.Ordinal) &&
                             string.Equals(state.TargetVerseId, verse.VerseId, StringComparison.Ordinal);
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
            if (string.IsNullOrWhiteSpace(verse.DisplayName))
                return string.IsNullOrWhiteSpace(verse.VerseId) ? "Unknown Verse" : verse.VerseId;

            return string.IsNullOrWhiteSpace(verse.VerseId) || string.Equals(verse.DisplayName, verse.VerseId, StringComparison.Ordinal)
                ? verse.DisplayName
                : $"{verse.DisplayName} ({verse.VerseId})";
        }
    }
}
