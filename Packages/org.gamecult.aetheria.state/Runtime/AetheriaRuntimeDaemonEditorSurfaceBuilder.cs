using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

#nullable enable

namespace GameCult.Aetheria.State.Verse
{
    public static class AetheriaRuntimeDaemonEditorSurfaceBuilder
    {
        public const string SurfaceId = "aetheria.daemon.editor";
        public const string TuiSurfaceId = "aetheria.daemon.editor.tui";

        public static AetheriaRuntimeSurfaceDocument Build(
            AetheriaRuntimeDaemonProviderAdvertisementDocument provider,
            AetheriaRuntimeDaemonHealthDocument health,
            AetheriaRuntimeDaemonCommandBoundaryDocument commandBoundary)
        {
            provider ??= new AetheriaRuntimeDaemonProviderAdvertisementDocument();
            health ??= new AetheriaRuntimeDaemonHealthDocument();
            commandBoundary ??= AetheriaRuntimeDaemonCommandBoundaryDocument.Create(provider.DaemonId);

            return new AetheriaRuntimeSurfaceDocument(
                providerId: "aetheria.daemon",
                providerKind: "editor.daemon",
                title: "Aetheria Daemon Editor",
                version: health.FrameId,
                updatedAtUtc: string.IsNullOrWhiteSpace(health.PublishedAtUtc)
                    ? provider.PublishedAtUtc
                    : health.PublishedAtUtc,
                surface: new AetheriaRuntimeSurfaceTree(
                    SurfaceId,
                    Node(
                        "aetheria.daemon.editor.root",
                        "surface",
                        Array.Empty<(string Key, string Value)>(),
                        Node(
                            "aetheria.daemon.editor.provider",
                            "card",
                            new[] { ("title", "Verse Provider") },
                            Metric("aetheria.daemon.editor.provider.verse", "Verse", provider.VerseId),
                            Metric("aetheria.daemon.editor.provider.provider", "Provider", provider.ProviderId),
                            Metric("aetheria.daemon.editor.provider.daemon", "Daemon", provider.DaemonId),
                            Metric("aetheria.daemon.editor.provider.transport", "CultMesh", provider.CultMeshAddress),
                            Metric("aetheria.daemon.editor.provider.state", "State Witness", provider.StateWitnessPath)),
                        Node(
                            "aetheria.daemon.editor.witnesses",
                            "card",
                            new[] { ("title", "Witnesses") },
                            Metric("aetheria.daemon.editor.witnesses.frame", "Frame", provider.FrameWitnessPath),
                            Metric("aetheria.daemon.editor.witnesses.soa", "SoA", provider.SoaWitnessPath),
                            Metric("aetheria.daemon.editor.witnesses.health", "Health", provider.HealthWitnessPath),
                            Metric("aetheria.daemon.editor.witnesses.commands", "Command Boundary", provider.CommandBoundaryWitnessPath),
                            Metric("aetheria.daemon.editor.witnesses.game", "Game Surface", provider.EveGuiSurfaceWitnessPath),
                            Metric("aetheria.daemon.editor.witnesses.editor", "Editor Surface", provider.EditorGuiSurfaceWitnessPath)),
                        Node(
                            "aetheria.daemon.editor.health",
                            "card",
                            new[] { ("title", "Health") },
                            Metric("aetheria.daemon.editor.health.status", "Status", health.Status),
                            Metric("aetheria.daemon.editor.health.frame", "Frame", health.FrameId.ToString(CultureInfo.InvariantCulture)),
                            Metric("aetheria.daemon.editor.health.observed_commands", "Observed Commands", health.ObservedCommandCount.ToString(CultureInfo.InvariantCulture)),
                            Metric("aetheria.daemon.editor.health.applied", "Applied", health.AppliedCommandCount.ToString(CultureInfo.InvariantCulture)),
                            Metric("aetheria.daemon.editor.health.rejected", "Rejected", health.RejectedCommandCount.ToString(CultureInfo.InvariantCulture)),
                            Metric("aetheria.daemon.editor.health.source", "Source", health.PublicationSource),
                            Metric("aetheria.daemon.editor.health.transport", "Transport", health.Transport)),
                        Node(
                            "aetheria.daemon.editor.schemas",
                            "card",
                            new[] { ("title", "Published Schemas") },
                            Row(
                                "aetheria.daemon.editor.schemas.rows",
                                provider.PublishedSchemas
                                    .Select((schema, index) => Metric(
                                        $"aetheria.daemon.editor.schemas.{index}",
                                        "Schema",
                                        schema))
                                    .ToArray())),
                        Node(
                            "aetheria.daemon.editor.commands",
                            "card",
                            new[] { ("title", "Typed Commands") },
                            Metric(
                                "aetheria.daemon.editor.commands.boundary",
                                "Boundary",
                                commandBoundary.BoundaryId),
                            Metric(
                                "aetheria.daemon.editor.commands.count",
                                "Command Count",
                                commandBoundary.Commands.Count.ToString(CultureInfo.InvariantCulture)),
                            Row(
                                "aetheria.daemon.editor.commands.rows",
                                commandBoundary.Commands
                                    .Take(16)
                                    .Select((entry, index) => CommandRow(index, entry))
                                    .ToArray()))),
                    Array.Empty<AetheriaRuntimeSurfaceStyleToken>()),
                commands: commandBoundary.Commands
                    .Select(entry => new AetheriaRuntimeSurfaceCommandTemplate(
                        CommandName(entry.Kind),
                        entry.Kind.ToString(),
                        "cultmesh"))
                    .ToArray());
        }

        private static AetheriaRuntimeSurfaceComponent CommandRow(
            int index,
            AetheriaRuntimeDaemonCommandBoundaryEntry entry)
        {
            return Node(
                $"aetheria.daemon.editor.commands.{index}",
                "inspector.kv",
                new[]
                {
                    ("kind", entry.Kind.ToString()),
                    ("body", entry.CommandBody),
                    ("authority", entry.Authority),
                    ("receipt", entry.Receipt)
                });
        }

        private static string CommandName(AetheriaRuntimeDaemonCommandKinds kind)
        {
            return "aetheria.daemon.commands." + kind;
        }

        private static AetheriaRuntimeSurfaceComponent Metric(string id, string label, string value)
        {
            return Node(id, "metric", new[] { ("label", label), ("value", value ?? "") });
        }

        private static AetheriaRuntimeSurfaceComponent Row(
            string id,
            params AetheriaRuntimeSurfaceComponent[] children)
        {
            return Node(id, "row", Array.Empty<(string Key, string Value)>(), children);
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
                props.ToDictionary(prop => prop.Key, prop => prop.Value ?? "", StringComparer.Ordinal),
                children ?? Array.Empty<AetheriaRuntimeSurfaceComponent>());
        }
    }
}
