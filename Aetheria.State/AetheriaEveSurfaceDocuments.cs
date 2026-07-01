using System;
using System.Collections.Generic;
using System.Linq;
using Aetheria.State.Documents;
using GameCult.Aetheria.State.Verse;
using GameCult.Mesh;
using EveSurfaceComponent = GameCult.Mesh.EveSurfaceComponent;
using EveSurfaceDocument = GameCult.Mesh.EveSurfaceDocument;
using EveSurfaceTree = GameCult.Mesh.EveSurfaceTree;

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
                Array.Empty<EveSurfaceStyleToken>()),
            new[]
            {
                new EveSurfaceCommandTemplate(refreshCommand.ToBinding())
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
                Array.Empty<EveSurfaceStyleToken>()),
            new[]
            {
                new EveSurfaceCommandTemplate(refreshCommand.ToBinding())
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

    public static EveProviderAdvertisementState BuildProviderAdvertisement(
        AetheriaVerseHostSettings settings,
        string statePath,
        string updatedAtUtc)
    {
        var normalized = AetheriaVerseHostSettingsNormalizer.Normalize(settings);
        return new EveProviderAdvertisementState
        {
            ProviderId = ProviderId,
            ServiceId = normalized.ServiceId,
            VerseId = normalized.VerseId,
            RootVerse = normalized.RootVerse,
            CanonicalService = normalized.CanonicalService,
            LocatedService = normalized.LocatedService,
            CultMeshAddress = normalized.CultMeshAddress,
            Title = normalized.Title,
            Kind = "game.runtime",
            UpdatedAtUtc = updatedAtUtc,
            Freshness = new EveProviderFreshness
            {
                State = "fresh",
                LastSeenAtUtc = updatedAtUtc,
                MaxAgeMs = 15000
            },
            Schemas =
            [
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
                "gamecult.eve.surface.v1",
                "gamecult.eve.command.v1",
                AetheriaRuntimeDaemonSchemas.ProviderAdvertisement,
                AetheriaRuntimeDaemonSchemas.Frame,
                AetheriaRuntimeDaemonSchemas.SoaView,
                AetheriaRuntimeDaemonSchemas.Health,
                AetheriaRuntimeDaemonSchemas.CommandBoundary,
                AetheriaRuntimeDaemonSchemas.GameSurface,
                AetheriaRuntimeDaemonSchemas.EditorSurface,
                AetheriaRuntimeDaemonSchemas.Command
            ],
            Witnesses =
            [
                new EveProviderWitness
                {
                    Kind = "cultcache",
                    Ref = statePath,
                    Summary = "Aetheria typed CultCache state file"
                },
                new EveProviderWitness
                {
                    Kind = DaemonRecordTransport,
                    Ref = AetheriaRuntimeVerseRecordKeys.DaemonProviderAdvertisement.ToString(),
                    Summary = "Aetheria daemon-owned provider advertisement record"
                },
                new EveProviderWitness
                {
                    Kind = DaemonRecordTransport,
                    Ref = AetheriaRuntimeVerseRecordKeys.DaemonFrameLatest.ToString(),
                    Summary = "Aetheria daemon latest simulation frame record"
                },
                new EveProviderWitness
                {
                    Kind = DaemonRecordTransport,
                    Ref = AetheriaRuntimeVerseRecordKeys.DaemonSoaViewLatest.ToString(),
                    Summary = "Aetheria daemon latest SoA view record for thin clients"
                },
                new EveProviderWitness
                {
                    Kind = DaemonRecordTransport,
                    Ref = AetheriaRuntimeVerseRecordKeys.DaemonHealth.ToString(),
                    Summary = "Aetheria daemon health record"
                },
                new EveProviderWitness
                {
                    Kind = DaemonRecordTransport,
                    Ref = AetheriaRuntimeVerseRecordKeys.DaemonCommandBoundary.ToString(),
                    Summary = "Aetheria daemon typed command boundary record"
                },
                new EveProviderWitness
                {
                    Kind = DaemonRecordTransport,
                    Ref = AetheriaRuntimeVerseRecordKeys.DaemonGameSurface.ToString(),
                    Summary = "Aetheria daemon game Eve GUI surface record"
                },
                new EveProviderWitness
                {
                    Kind = DaemonRecordTransport,
                    Ref = AetheriaRuntimeVerseRecordKeys.DaemonGameTuiSurface.ToString(),
                    Summary = "Aetheria daemon game Eve TUI surface record"
                },
                new EveProviderWitness
                {
                    Kind = DaemonRecordTransport,
                    Ref = AetheriaRuntimeVerseRecordKeys.DaemonEditorSurface.ToString(),
                    Summary = "Aetheria daemon editor Eve GUI surface record"
                },
                new EveProviderWitness
                {
                    Kind = DaemonRecordTransport,
                    Ref = AetheriaRuntimeVerseRecordKeys.DaemonEditorTuiSurface.ToString(),
                    Summary = "Aetheria daemon editor Eve TUI surface record"
                }
            ],
            Surfaces =
            [
                new EveProviderSurfaceRef
                {
                    SurfaceId = CatalogSurfaceId,
                    Key = CatalogSurfaceKey
                },
                new EveProviderSurfaceRef
                {
                    SurfaceId = OperationsSurfaceId,
                    Key = OperationsSurfaceKey
                },
                new EveProviderSurfaceRef
                {
                    SurfaceId = PlayerSettingsSurfaceId,
                    Key = PlayerSettingsSurfaceKey
                },
                new EveProviderSurfaceRef
                {
                    SurfaceId = AetheriaRuntimeDaemonGameSurfaceBuilder.SurfaceId,
                    Key = DaemonGameSurfaceKey
                },
                new EveProviderSurfaceRef
                {
                    SurfaceId = AetheriaRuntimeDaemonGameSurfaceBuilder.TuiSurfaceId,
                    Key = DaemonGameTuiSurfaceKey
                },
                new EveProviderSurfaceRef
                {
                    SurfaceId = AetheriaRuntimeDaemonEditorSurfaceBuilder.SurfaceId,
                    Key = DaemonEditorSurfaceKey
                },
                new EveProviderSurfaceRef
                {
                    SurfaceId = AetheriaRuntimeDaemonEditorSurfaceBuilder.TuiSurfaceId,
                    Key = DaemonEditorTuiSurfaceKey
                }
            ],
            Commands =
            [
                new EveProviderCommandRef
                {
                    Command = DaemonCommandBoundaryId,
                    Transport = "cultmesh",
                    Summary = "Aetheria daemon typed command boundary"
                },
                new EveProviderCommandRef
                {
                    Command = AetheriaRuntimeCatalogCommands.Refresh,
                    Summary = "Refresh catalog state"
                },
                new EveProviderCommandRef
                {
                    Command = AetheriaRuntimeOperationsCommands.Refresh,
                    Summary = "Refresh operations state"
                },
                new EveProviderCommandRef
                {
                    Command = AetheriaRuntimePlayerSettingsCommands.Refresh,
                    Summary = "Refresh player settings state"
                },
                new EveProviderCommandRef
                {
                    Command = AetheriaRuntimePlayerSettingsCommands.CycleTemperatureUnit,
                    Summary = "Cycle the typed player temperature unit"
                },
                new EveProviderCommandRef
                {
                    Command = AetheriaRuntimePlayerSettingsCommands.DecrementSignificantDigits,
                    Summary = "Decrease typed player significant digits"
                },
                new EveProviderCommandRef
                {
                    Command = AetheriaRuntimePlayerSettingsCommands.IncrementSignificantDigits,
                    Summary = "Increase typed player significant digits"
                },
                new EveProviderCommandRef
                {
                    Command = AetheriaRuntimePlayerSettingsCommands.CycleNebulaQuality,
                    Summary = "Cycle typed player nebula quality"
                },
                new EveProviderCommandRef
                {
                    Command = AetheriaRuntimePlayerSettingsCommands.ToggleShowAsteroidsInMinimap,
                    Summary = "Toggle typed player minimap asteroid visibility"
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
