using Aetheria.State.Documents;
using GameCult.Aetheria.State.Verse;

namespace Aetheria.State;

public static class AetheriaProviderAdvertisementProjector
{
    public const string AdvertisementKey = "eve:provider:aetheria";
    public const string DaemonGameSurfaceKey = "eve:surface:aetheria.daemon.game";
    public const string DaemonGameTuiSurfaceKey = "eve:surface:aetheria.daemon.game.tui";
    public const string DaemonEditorSurfaceKey = "eve:surface:aetheria.daemon.editor";
    public const string DaemonEditorTuiSurfaceKey = "eve:surface:aetheria.daemon.editor.tui";
    public const string ProviderId = "aetheria";
    private const string DaemonCommandBoundaryId = "aetheria.daemon.commands";
    private const string DaemonWitnessTransport = "cultcache-witness";

    public static EveProviderAdvertisementState Build(
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
                    Kind = DaemonWitnessTransport,
                    Ref = AetheriaRuntimeStateBoundary.GetDaemonProviderPath(statePath),
                    Summary = "Aetheria daemon-owned provider advertisement"
                },
                new EveProviderWitness
                {
                    Kind = DaemonWitnessTransport,
                    Ref = AetheriaRuntimeStateBoundary.GetDaemonFramePath(statePath),
                    Summary = "Aetheria daemon simulation frame"
                },
                new EveProviderWitness
                {
                    Kind = DaemonWitnessTransport,
                    Ref = AetheriaRuntimeStateBoundary.GetDaemonSoaViewPath(statePath),
                    Summary = "Aetheria daemon SoA view for thin clients"
                },
                new EveProviderWitness
                {
                    Kind = DaemonWitnessTransport,
                    Ref = AetheriaRuntimeStateBoundary.GetDaemonHealthPath(statePath),
                    Summary = "Aetheria daemon health publication"
                },
                new EveProviderWitness
                {
                    Kind = DaemonWitnessTransport,
                    Ref = AetheriaRuntimeStateBoundary.GetDaemonCommandBoundaryPath(statePath),
                    Summary = "Aetheria daemon typed command boundary"
                },
                new EveProviderWitness
                {
                    Kind = DaemonWitnessTransport,
                    Ref = AetheriaRuntimeStateBoundary.GetDaemonGameSurfacePath(statePath),
                    Summary = "Aetheria daemon game Eve GUI surface"
                },
                new EveProviderWitness
                {
                    Kind = DaemonWitnessTransport,
                    Ref = AetheriaRuntimeStateBoundary.GetDaemonGameTuiSurfacePath(statePath),
                    Summary = "Aetheria daemon game Eve TUI surface"
                },
                new EveProviderWitness
                {
                    Kind = DaemonWitnessTransport,
                    Ref = AetheriaRuntimeStateBoundary.GetDaemonEditorSurfacePath(statePath),
                    Summary = "Aetheria daemon editor Eve GUI surface"
                },
                new EveProviderWitness
                {
                    Kind = DaemonWitnessTransport,
                    Ref = AetheriaRuntimeStateBoundary.GetDaemonEditorTuiSurfacePath(statePath),
                    Summary = "Aetheria daemon editor Eve TUI surface"
                }
            ],
            Surfaces =
            [
                new EveProviderSurfaceRef
                {
                    SurfaceId = AetheriaCatalogSurfaceProjector.SurfaceId,
                    Key = AetheriaCatalogSurfaceProjector.SurfaceKey
                },
                new EveProviderSurfaceRef
                {
                    SurfaceId = AetheriaOperationsSurfaceProjector.SurfaceId,
                    Key = AetheriaOperationsSurfaceProjector.SurfaceKey
                },
                new EveProviderSurfaceRef
                {
                    SurfaceId = AetheriaPlayerSettingsSurfaceProjector.SurfaceId,
                    Key = AetheriaPlayerSettingsSurfaceProjector.SurfaceKey
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
                    Transport = DaemonWitnessTransport,
                    Summary = "Aetheria daemon typed command boundary"
                },
                new EveProviderCommandRef
                {
                    Command = AetheriaRuntimeCatalogCommands.Refresh,
                    Summary = "Refresh catalog projection"
                },
                new EveProviderCommandRef
                {
                    Command = AetheriaRuntimeOperationsCommands.Refresh,
                    Summary = "Refresh operations projection"
                },
                new EveProviderCommandRef
                {
                    Command = AetheriaRuntimePlayerSettingsCommands.Refresh,
                    Summary = "Refresh player settings projection"
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
}
