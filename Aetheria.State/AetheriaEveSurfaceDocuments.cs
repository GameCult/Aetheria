using System;
using System.Collections.Generic;
using System.Linq;
using Aetheria.State.Documents;
using GameCult.Aetheria.State.Verse;
using GameCult.Mesh;
using EveCommandTemplate = GameCult.Eve.Surface.EveCommandTemplate;
using EveAdvertisedCommand = GameCult.Eve.Surface.EveAdvertisedCommand;
using EveAdvertisedSurface = GameCult.Eve.Surface.EveAdvertisedSurface;
using EveProviderAdvertisementDocument = GameCult.Eve.Surface.EveProviderAdvertisementDocument;
using EveProviderFreshness = GameCult.Eve.Surface.EveProviderFreshness;
using EveProviderWitness = GameCult.Eve.Surface.EveProviderWitness;
using EveStyleToken = GameCult.Eve.Surface.EveStyleToken;
using EveSurfaceComponent = GameCult.Eve.Surface.EveSurfaceComponent;
using EveSurfaceDocument = GameCult.Eve.Surface.EveSurfaceDocument;
using EveSurfaceTree = GameCult.Eve.Surface.EveSurfaceTree;
using EveWorldInteractionAdvertisement = GameCult.Eve.Surface.EveWorldInteractionAdvertisement;

namespace Aetheria.State;

public static class AetheriaEveSurfaceDocuments
{
    public const string ProviderId = "aetheria";
    public const string ProviderAdvertisementKey = "eve:provider:aetheria";
    public const string CatalogSurfaceKey = "eve:surface:aetheria.catalog.operator";
    public const string OperationsSurfaceKey = "eve:surface:aetheria.operations";
    public const string PlayerSettingsSurfaceKey = "eve:surface:aetheria.player_settings";
    public const string DaemonGameSurfaceKey = "eve:surface:aetheria.daemon.game";
    public const string DaemonGameTuiSurfaceKey = "eve:surface:aetheria.daemon.game.tui";
    public const string DaemonEditorSurfaceKey = "eve:surface:aetheria.daemon.editor";
    public const string DaemonEditorTuiSurfaceKey = "eve:surface:aetheria.daemon.editor.tui";

    public const string CatalogSurfaceId = AetheriaRuntimeCatalogCommands.SurfaceId;
    public const string OperationsSurfaceId = AetheriaRuntimeOperationsCommands.SurfaceId;
    public const string PlayerSettingsSurfaceId = AetheriaRuntimePlayerSettingsCommands.SurfaceId;

    private const string DaemonCommandBoundaryId = "aetheria.daemon.commands";
    private const string DaemonRecordTransport = "cultmesh-record";

    public static EveSurfaceDocument BuildCatalogSurface(
        AetheriaRuntimeCatalogSnapshot catalog,
        string updatedAtUtc,
        long version = 1)
    {
        var tradeItems = catalog.TradeItems.Take(12).Select(item =>
            Row(
                $"item.{SafeId(item.ItemKey)}",
                ("name", item.Name),
                ("manufacturer", catalog.GetManufacturer(item)?.Name ?? "GameCult"),
                ("price", item.Price.ToString("N0")),
                ("size", item.ShapeWidth > 0 && item.ShapeHeight > 0 ? $"{item.ShapeWidth}x{item.ShapeHeight}" : "")))
            .ToArray();

        var corporations = catalog.Corporations.Take(12).Select(corporation =>
            Row(
                $"corporation.{SafeId(corporation.CorporationKey)}",
                ("name", corporation.Name),
                ("short", corporation.ShortName),
                ("names", catalog.GetNameFile(corporation)?.Name ?? ""),
                ("influence", corporation.InfluenceDistance.ToString())))
            .ToArray();
        var refreshCommand = CultMesh.OperationBindingRecord(
            CultMesh.OperationBinding(
                AetheriaRuntimeCatalogCommands.Refresh,
                label: "Refresh",
                routeHint: new CultMeshRouteHint(
                    CultMeshLocalityKind.Automatic,
                    "cultmesh")));

        return new EveSurfaceDocument(
            ProviderId,
            "game.runtime",
            "Aetheria Catalog",
            version,
            updatedAtUtc,
            new EveSurfaceTree(
                CatalogSurfaceId,
                Node(
                    "aetheria.catalog.root",
                    "surface",
                    [],
                    Node(
                        "aetheria.catalog.summary",
                        "grid",
                        [("columns", "6")],
                        Metric("summary.items", "Items", catalog.Items.Count.ToString()),
                        Metric("summary.trade", "Trade Items", catalog.TradeItems.Count().ToString()),
                        Metric("summary.equipment", "Equipment", catalog.EquipmentItems.Count().ToString()),
                        Metric("summary.behaviors", "Behavior Kinds", catalog.Items.SelectMany(item => item.BehaviorKinds).Distinct().Count().ToString()),
                        Metric("summary.corporations", "Corporations", catalog.Corporations.Count.ToString()),
                        Metric("summary.nameFiles", "Name Files", catalog.NameFiles.Count.ToString())),
                    Node(
                        "aetheria.catalog.trade",
                        "card",
                        [("title", "Trade Catalog")],
                        Node("aetheria.catalog.trade.rows", "inspector.kv", [], tradeItems)),
                    Node(
                        "aetheria.catalog.corporations",
                        "card",
                        [("title", "Corporations")],
                        Node("aetheria.catalog.corporation.rows", "inspector.kv", [], corporations))),
                Array.Empty<EveStyleToken>()),
            new[]
            {
                new EveCommandTemplate(refreshCommand.ToBinding())
            });
    }

    public static EveSurfaceDocument BuildOperationsSurface(
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
        var refreshCommand = CultMesh.OperationBindingRecord(
            CultMesh.OperationBinding(
                AetheriaRuntimeOperationsCommands.Refresh,
                label: "Refresh",
                routeHint: new CultMeshRouteHint(
                    CultMeshLocalityKind.Automatic,
                    "cultmesh")));
        return new EveSurfaceDocument(
            ProviderId,
            "game.runtime",
            "Aetheria Operations",
            version,
            updatedAtUtc,
            new EveSurfaceTree(
                OperationsSurfaceId,
                Node(
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
                            ("lastSeen", runtimeSession?.LastSeenAtUtc ?? "")))),
                Array.Empty<EveStyleToken>()),
            new[]
            {
                new EveCommandTemplate(refreshCommand.ToBinding())
            });
    }

    public static EveSurfaceDocument BuildPlayerSettingsSurface(
        AetheriaPlayerSettings? settings,
        string updatedAtUtc,
        long version = 1)
    {
        settings ??= new AetheriaPlayerSettings();
        var gameplay = settings.Gameplay ?? new AetheriaPlayerGameplaySettings();
        var graphics = settings.Graphics ?? new AetheriaPlayerGraphicsSettings();
        var publishedAtUtc = !string.IsNullOrWhiteSpace(settings.LastUpdatedAtUtc)
            ? settings.LastUpdatedAtUtc
            : updatedAtUtc;

        var surface = AetheriaRuntimePlayerSettingsSurfaceBuilder.Build(
            settings.PlayerName,
            settings.TutorialPassed,
            settings.ActiveRunKey,
            gameplay.TemperatureUnit,
            gameplay.SignificantDigits,
            graphics.NebulaQuality,
            graphics.ShowAsteroidsInMinimap,
            publishedAtUtc,
            version);

        return AetheriaRuntimeSurfaceDocuments.ToPortableSurface(surface);
    }

    public static EveProviderAdvertisementDocument BuildProviderAdvertisement(
        AetheriaVerseHostSettings settings,
        string statePath,
        string updatedAtUtc)
    {
        var normalized = AetheriaVerseHostSettingsNormalizer.Normalize(settings);
        var schemas = new[]
        {
            "aetheria.world_state.v1",
            "aetheria.item_definition.v1",
            "aetheria.corporation.v2",
            "aetheria.name_file.v2",
            "aetheria.trade_value_policy.v1",
            "aetheria.player_settings.v1",
            "aetheria.loadout_template.v1",
            "aetheria.run_state.v1",
            "aetheria.zone_state.v1",
            "aetheria.entity_snapshot.v1",
            "aetheria.verse_host_settings.v1",
            "aetheria.runtime_session.v1",
            "aetheria.eve_command_acceptance_status.v1",
            EveSurfaceDocument.SchemaId,
            AetheriaRuntimeEveCommandDocument.SchemaId,
            AetheriaRuntimeDaemonSchemas.ProviderAdvertisement,
            AetheriaRuntimeDaemonSchemas.Frame,
            AetheriaRuntimeDaemonSchemas.SoaView,
            AetheriaRuntimeDaemonSchemas.Health,
            AetheriaRuntimeDaemonSchemas.CommandBoundary,
            AetheriaRuntimeDaemonSchemas.GameSurface,
            AetheriaRuntimeDaemonSchemas.EditorSurface,
            AetheriaRuntimeDaemonSchemas.Command
        };
        var witnesses = new[]
        {
            Witness("cultcache", statePath, "Aetheria typed CultCache state file"),
            Witness(DaemonRecordTransport, AetheriaRuntimeVerseRecordKeys.DaemonProviderAdvertisement.ToString(),
                "Aetheria daemon-owned provider advertisement record"),
            Witness(DaemonRecordTransport, AetheriaRuntimeVerseRecordKeys.DaemonFrameLatest.ToString(),
                "Aetheria daemon latest simulation frame record"),
            Witness(DaemonRecordTransport, AetheriaRuntimeVerseRecordKeys.DaemonSoaViewLatest.ToString(),
                "Aetheria daemon latest SoA view record for thin clients"),
            Witness(DaemonRecordTransport, AetheriaRuntimeVerseRecordKeys.DaemonHealth.ToString(),
                "Aetheria daemon health record"),
            Witness(DaemonRecordTransport, AetheriaRuntimeVerseRecordKeys.DaemonCommandBoundary.ToString(),
                "Aetheria daemon typed command boundary record"),
            Witness(DaemonRecordTransport, AetheriaRuntimeVerseRecordKeys.DaemonGameSurface.ToString(),
                "Aetheria daemon game Eve GUI surface record"),
            Witness(DaemonRecordTransport, AetheriaRuntimeVerseRecordKeys.DaemonGameTuiSurface.ToString(),
                "Aetheria daemon game Eve TUI surface record"),
            Witness(DaemonRecordTransport, AetheriaRuntimeVerseRecordKeys.DaemonEditorSurface.ToString(),
                "Aetheria daemon editor Eve GUI surface record"),
            Witness(DaemonRecordTransport, AetheriaRuntimeVerseRecordKeys.DaemonEditorTuiSurface.ToString(),
                "Aetheria daemon editor Eve TUI surface record")
        };
        var commands = new[]
        {
            Command(DaemonCommandBoundaryId, "", "Aetheria daemon typed command boundary"),
            Command(AetheriaRuntimeCatalogCommands.Refresh, AetheriaRuntimeCatalogCommands.SurfaceId, "Refresh catalog state"),
            Command(AetheriaRuntimeOperationsCommands.Refresh, AetheriaRuntimeOperationsCommands.SurfaceId, "Refresh operations state"),
            Command(AetheriaRuntimePlayerSettingsCommands.Refresh, AetheriaRuntimePlayerSettingsCommands.SurfaceId, "Refresh player settings state"),
            Command(AetheriaRuntimePlayerSettingsCommands.CycleTemperatureUnit, AetheriaRuntimePlayerSettingsCommands.SurfaceId, "Cycle the typed player temperature unit"),
            Command(AetheriaRuntimePlayerSettingsCommands.DecrementSignificantDigits, AetheriaRuntimePlayerSettingsCommands.SurfaceId, "Decrease typed player significant digits"),
            Command(AetheriaRuntimePlayerSettingsCommands.IncrementSignificantDigits, AetheriaRuntimePlayerSettingsCommands.SurfaceId, "Increase typed player significant digits"),
            Command(AetheriaRuntimePlayerSettingsCommands.CycleNebulaQuality, AetheriaRuntimePlayerSettingsCommands.SurfaceId, "Cycle typed player nebula quality"),
            Command(AetheriaRuntimePlayerSettingsCommands.ToggleShowAsteroidsInMinimap, AetheriaRuntimePlayerSettingsCommands.SurfaceId, "Toggle typed player minimap asteroid visibility")
        };

        return new EveProviderAdvertisementDocument(
            ProviderId,
            normalized.ServiceId,
            normalized.VerseId,
            normalized.Title,
            "game.runtime",
            normalized.CultMeshAddress,
            updatedAtUtc,
            new EveProviderFreshness("fresh", updatedAtUtc, 15000),
            schemas,
            witnesses,
            AetheriaRuntimeEveSurfaceCatalog.All.Select(ToProviderSurface).ToArray(),
            commands);
    }

    private static EveProviderWitness Witness(string kind, string reference, string summary) =>
        new(kind, reference, summary);

    private static EveAdvertisedCommand Command(string command, string surfaceId, string summary) =>
        new(command, surfaceId, "cultmesh", summary);

    private static EveAdvertisedSurface ToProviderSurface(AetheriaRuntimeEveSurfaceAdvertisement surface)
    {
        var interaction = surface.WorldInteraction == null
            ? null
            : new EveWorldInteractionAdvertisement(
                surface.WorldInteraction.ProjectionKind,
                surface.WorldInteraction.StateSchemas,
                surface.WorldInteraction.CommandBoundary,
                AetheriaRuntimeVerseRecordKeys.DaemonCommandBoundary.ToString(),
                surface.WorldInteraction.ReceiptSchema,
                "",
                "",
                surface.WorldInteraction.LoweringTargets,
                surface.WorldInteraction.Ownership);
        return new EveAdvertisedSurface(
            surface.SurfaceId,
            EveSurfaceDocument.SchemaId,
            surface.RecordRef,
            surface.Transport,
            surface.Status,
            surface.SurfaceKind,
            interaction);
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
        return new EveSurfaceComponent(
            id,
            kind,
            props.ToDictionary(prop => prop.Key, prop => prop.Value),
            children);
    }

    private static string SafeId(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "empty"
            : new string(value.Select(character => char.IsLetterOrDigit(character) ? character : '.').ToArray());
    }
}
