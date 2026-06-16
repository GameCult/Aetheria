using Aetheria.State.Documents;

namespace Aetheria.State;

public static class AetheriaProviderAdvertisementProjector
{
    public const string AdvertisementKey = "eve:provider:aetheria";
    public const string ProviderId = "aetheria";

    public static EveProviderAdvertisementState Build(string statePath, string updatedAtUtc)
    {
        return new EveProviderAdvertisementState
        {
            ProviderId = ProviderId,
            ServiceId = "aetheria.runtime",
            VerseId = "aetheria.local",
            RootVerse = "asgard",
            CanonicalService = "asgard.aetheria",
            LocatedService = "asgard.local.aetheria",
            CultMeshAddress = "asgard.local.aetheria/eve",
            Title = "Aetheria",
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
                "aetheria.runtime_session.v1",
                "aetheria.runtime_commit_drain_status.v1",
                "aetheria.eve_command_drain_status.v1",
                "gamecult.eve.surface.v1",
                "gamecult.eve.command.v1"
            ],
            Witnesses =
            [
                new EveProviderWitness
                {
                    Kind = "cultcache",
                    Ref = statePath,
                    Summary = "Aetheria typed CultCache state file"
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
                }
            ],
            Commands =
            [
                new EveProviderCommandRef
                {
                    Command = "aetheria.catalog.refresh",
                    Summary = "Refresh catalog projection"
                },
                new EveProviderCommandRef
                {
                    Command = "aetheria.operations.refresh",
                    Summary = "Refresh operations projection"
                },
                new EveProviderCommandRef
                {
                    Command = GameCult.Aetheria.State.Unity.AetheriaRuntimePlayerSettingsCommands.Refresh,
                    Summary = "Refresh player settings projection"
                },
                new EveProviderCommandRef
                {
                    Command = GameCult.Aetheria.State.Unity.AetheriaRuntimePlayerSettingsCommands.CycleTemperatureUnit,
                    Summary = "Cycle the typed player temperature unit"
                },
                new EveProviderCommandRef
                {
                    Command = GameCult.Aetheria.State.Unity.AetheriaRuntimePlayerSettingsCommands.DecrementSignificantDigits,
                    Summary = "Decrease typed player significant digits"
                },
                new EveProviderCommandRef
                {
                    Command = GameCult.Aetheria.State.Unity.AetheriaRuntimePlayerSettingsCommands.IncrementSignificantDigits,
                    Summary = "Increase typed player significant digits"
                },
                new EveProviderCommandRef
                {
                    Command = GameCult.Aetheria.State.Unity.AetheriaRuntimePlayerSettingsCommands.CycleNebulaQuality,
                    Summary = "Cycle typed player nebula quality"
                },
                new EveProviderCommandRef
                {
                    Command = GameCult.Aetheria.State.Unity.AetheriaRuntimePlayerSettingsCommands.ToggleShowAsteroidsInMinimap,
                    Summary = "Toggle typed player minimap asteroid visibility"
                }
            ]
        };
    }
}
