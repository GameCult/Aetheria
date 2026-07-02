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
            AetheriaRuntimeDaemonCommandBoundaryDocument commandBoundary,
            IReadOnlyList<AetheriaRuntimeSurfaceDocument>? designerSurfaces = null)
        {
            provider ??= new AetheriaRuntimeDaemonProviderAdvertisementDocument();
            health ??= new AetheriaRuntimeDaemonHealthDocument();
            commandBoundary ??= AetheriaRuntimeDaemonCommandBoundaryDocument.Create(provider.DaemonId);
            designerSurfaces ??= Array.Empty<AetheriaRuntimeSurfaceDocument>();

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
                            Metric("aetheria.daemon.editor.provider.state", "State", provider.StateRecordRef)),
                        Node(
                            "aetheria.daemon.editor.records",
                            "card",
                            new[] { ("title", "Managed Records") },
                            Metric("aetheria.daemon.editor.records.frame", "Frame", provider.FrameRecordRef),
                            Metric("aetheria.daemon.editor.records.soa", "SoA", provider.SoaViewRecordRef),
                            Metric("aetheria.daemon.editor.records.health", "Health", provider.HealthRecordRef),
                            Metric("aetheria.daemon.editor.records.commands", "Command Boundary", provider.CommandBoundaryRecordRef),
                            Metric("aetheria.daemon.editor.records.game", "Game Surface", provider.EveGuiSurfaceRecordRef),
                            Metric("aetheria.daemon.editor.records.editor", "Editor Surface", provider.EditorGuiSurfaceRecordRef)),
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
                                    .ToArray())),
                        Node(
                            "aetheria.daemon.editor.designer_surfaces",
                            "card",
                            new[] { ("title", "Designer Surfaces") },
                            Row(
                                "aetheria.daemon.editor.designer_surfaces.rows",
                                designerSurfaces
                                    .Where(surface => surface != null)
                                    .Select((surface, index) => DesignerSurfaceRow(index, surface))
                                    .ToArray())),
                        Node(
                            "aetheria.daemon.editor.eve_surfaces",
                            "card",
                            new[] { ("title", "Advertised Eve Surfaces") },
                            Row(
                                "aetheria.daemon.editor.eve_surfaces.rows",
                                provider.EveSurfaces
                                    .Where(surface => surface != null)
                                    .Select((surface, index) => AdvertisedSurfaceRow(index, surface))
                                    .ToArray()))),
                    Array.Empty<AetheriaRuntimeSurfaceStyleToken>()),
                commands: commandBoundary.Commands
                    .Where(entry => AetheriaRuntimeDaemonSurfaceCommandCatalog.IsArgumentlessCommand(entry.Kind))
                    .Select(entry => new AetheriaRuntimeSurfaceCommandTemplate(
                        AetheriaRuntimeDaemonSurfaceCommandCatalog.CommandName(entry.Kind),
                        AetheriaRuntimeDaemonSurfaceCommandCatalog.Label(entry.Kind),
                        "cultmesh"))
                    .ToArray());
        }

        private static AetheriaRuntimeSurfaceComponent DesignerSurfaceRow(
            int index,
            AetheriaRuntimeSurfaceDocument surface)
        {
            return Node(
                $"aetheria.daemon.editor.designer_surfaces.{index}",
                "inspector.kv",
                new[]
                {
                    ("title", surface.Title),
                    ("surfaceId", surface.Surface.Id),
                    ("provider", surface.ProviderId),
                    ("kind", surface.ProviderKind),
                    ("commands", surface.Commands.Count.ToString(CultureInfo.InvariantCulture))
                });
        }

        private static AetheriaRuntimeSurfaceComponent AdvertisedSurfaceRow(
            int index,
            AetheriaRuntimeEveSurfaceAdvertisement surface)
        {
            return Node(
                $"aetheria.daemon.editor.eve_surfaces.{index}",
                "inspector.kv",
                new[]
                {
                    ("title", surface.Title),
                    ("surfaceId", surface.SurfaceId),
                    ("record", surface.RecordRef),
                    ("status", surface.Status),
                    ("audience", surface.Audience),
                    ("provider", surface.ProviderId)
                });
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
            return AetheriaRuntimeDaemonSurfaceCommandCatalog.CommandName(kind);
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
