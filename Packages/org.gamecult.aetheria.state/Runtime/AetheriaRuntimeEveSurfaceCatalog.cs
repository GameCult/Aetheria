using System;
using System.Collections.Generic;
using System.Linq;
using MessagePack;

#nullable enable

namespace GameCult.Aetheria.State.Verse
{
    [MessagePackObject]
    public sealed class AetheriaRuntimeEveSurfaceAdvertisement
    {
        [Key(0)]
        public string SurfaceId { get; set; } = "";

        [Key(1)]
        public string Title { get; set; } = "";

        [Key(2)]
        public string ProviderId { get; set; } = "aetheria";

        [Key(3)]
        public string ProviderKind { get; set; } = "game.runtime";

        [Key(4)]
        public string RecordRef { get; set; } = "";

        [Key(5)]
        public string Transport { get; set; } = "cultmesh-managed";

        [Key(6)]
        public string Status { get; set; } = "available";

        [Key(7)]
        public string Audience { get; set; } = "operator";

        [Key(8)]
        public string Mode { get; set; } = "interactive";

        [Key(9)]
        public string Summary { get; set; } = "";

        [Key(10)]
        public IReadOnlyList<string> Commands { get; set; } = Array.Empty<string>();

        [Key(11)]
        public string SurfaceKind { get; set; } = "";

        [Key(12)]
        public AetheriaRuntimeEveWorldInteractionAdvertisement? WorldInteraction { get; set; }

        [Key(13)]
        public IReadOnlyList<AetheriaRuntimeEvePluginRequirement> RequiresPlugins { get; set; } =
            Array.Empty<AetheriaRuntimeEvePluginRequirement>();
    }

    [MessagePackObject]
    public sealed class AetheriaRuntimeEvePluginRequirement
    {
        [Key(0)]
        public string PluginId { get; set; } = "";

        [Key(1)]
        public string VersionRange { get; set; } = "";

        [Key(2)]
        public string Availability { get; set; } = "required";

        [Key(3)]
        public IReadOnlyList<string> RequiredCapabilities { get; set; } = Array.Empty<string>();

        [Key(4)]
        public IReadOnlyList<string> OptionalCapabilities { get; set; } = Array.Empty<string>();
    }

    [MessagePackObject]
    public sealed class AetheriaRuntimeEveWorldInteractionAdvertisement
    {
        [Key(0)]
        public string ProjectionKind { get; set; } = "";

        [Key(1)]
        public IReadOnlyList<string> StateSchemas { get; set; } = Array.Empty<string>();

        [Key(2)]
        public IReadOnlyList<string> LoweringTargets { get; set; } = Array.Empty<string>();

        [Key(3)]
        public string CommandBoundary { get; set; } = "";

        [Key(4)]
        public string ReceiptSchema { get; set; } = "";

        [Key(5)]
        public string Ownership { get; set; } = "";
    }

    public static class AetheriaRuntimeEveSurfaceCatalog
    {
        private const string SurfaceSchema = "gamecult.eve.surface.v1";

        private static readonly AetheriaRuntimeEveSurfaceAdvertisement[] Surfaces =
        {
            Surface(AetheriaRuntimeCatalogCommands.SurfaceId, "Aetheria Catalog", "eve:surface:aetheria.catalog.operator", "operator", "Aetheria catalog operator surface.", AetheriaRuntimeCatalogCommands.Refresh),
            Surface(AetheriaRuntimeOperationsCommands.SurfaceId, "Aetheria Operations", "eve:surface:aetheria.operations", "operator", "Daemon operation and Eve command acceptance surface.", AetheriaRuntimeOperationsCommands.Refresh),
            Surface(AetheriaRuntimePlayerSettingsCommands.SurfaceId, "Player Settings", "eve:surface:aetheria.player_settings", "player", "Typed player settings surface.",
                AetheriaRuntimePlayerSettingsCommands.Refresh,
                AetheriaRuntimePlayerSettingsCommands.CycleTemperatureUnit,
                AetheriaRuntimePlayerSettingsCommands.DecrementSignificantDigits,
                AetheriaRuntimePlayerSettingsCommands.IncrementSignificantDigits,
                AetheriaRuntimePlayerSettingsCommands.CycleNebulaQuality,
                AetheriaRuntimePlayerSettingsCommands.ToggleShowAsteroidsInMinimap),
            Surface(AetheriaRuntimeDaemonGameSurfaceBuilder.PilotSurfaceId, "Aetheria Pilot", AetheriaRuntimeVerseRecordKeys.DaemonGameSurface.ToString(), "pilot", "Embodied pilot projection shared by Terminus and Starbridge.", providerId: "aetheria.daemon", providerKind: "game.daemon", transport: "cultmesh-record", surfaceKind: "interactive-world", worldInteraction: WorldInteraction("provider-authored-world-surface"), requiresPlugins: FieldPluginRequirements()),
            Surface(AetheriaRuntimeDaemonGameSurfaceBuilder.CommanderSurfaceId, "Starbridge Commander", AetheriaRuntimeVerseRecordKeys.StarbridgeCommanderSurface.ToString(), "commander", "Strategic Starbridge commander projection.", providerId: "aetheria.daemon", providerKind: "game.daemon", transport: "cultmesh-record", surfaceKind: "interactive-world", worldInteraction: WorldInteraction("provider-authored-world-surface"), requiresPlugins: FieldPluginRequirements()),
            Surface(AetheriaRuntimeDaemonGameSurfaceBuilder.TuiSurfaceId, "Aetheria Daemon TUI", AetheriaRuntimeVerseRecordKeys.DaemonGameTuiSurface.ToString(), "agent", "Compact daemon game surface for dense inspection.", providerId: "aetheria.daemon", providerKind: "game.daemon", transport: "cultmesh-record", surfaceKind: "interactive-world", worldInteraction: WorldInteraction("provider-authored-world-surface")),
            Surface(AetheriaRuntimeDaemonEditorSurfaceBuilder.SurfaceId, "Aetheria Daemon Editor", AetheriaRuntimeVerseRecordKeys.DaemonEditorSurface.ToString(), "operator", "Daemon editor and surface inventory.", providerId: "aetheria.daemon", providerKind: "editor.daemon", transport: "cultmesh-record", surfaceKind: "interactive-world-editor", worldInteraction: WorldInteraction("provider-authored-world-editor-surface")),
            Surface(AetheriaRuntimeDaemonEditorSurfaceBuilder.TuiSurfaceId, "Aetheria Daemon Editor TUI", AetheriaRuntimeVerseRecordKeys.DaemonEditorTuiSurface.ToString(), "agent", "Compact daemon editor and surface inventory.", providerId: "aetheria.daemon", providerKind: "editor.daemon", transport: "cultmesh-record", surfaceKind: "interactive-world-editor", worldInteraction: WorldInteraction("provider-authored-world-editor-surface")),
            Surface(AetheriaRuntimeMainMenuCommands.RootSurfaceId, "Main Menu", AetheriaRuntimeVerseRecordKeys.MainMenuSurface.ToString(), "player", "Runtime main menu root.", transport: "cultmesh-record", requiresPlugins: FieldPluginRequirements()),
            Surface(AetheriaRuntimeMainMenuCommands.SettingsSurfaceId, "Main Menu Settings", AetheriaRuntimeVerseRecordKeys.MainMenuSettingsSurface.ToString(), "player", "Runtime main menu settings panel.", transport: "cultmesh-record"),
            Surface(AetheriaRuntimeMainMenuCommands.InputSettingsSurfaceId, "Main Menu Input Settings", AetheriaRuntimeVerseRecordKeys.MainMenuInputSettingsSurface.ToString(), "player", "Main menu input settings panel.", transport: "cultmesh-record"),
            Surface(AetheriaRuntimeMainMenuCommands.PlayerSettingsSurfaceId, "Main Menu Player Settings", AetheriaRuntimeVerseRecordKeys.MainMenuPlayerSettingsSurface.ToString(), "player", "Main menu player settings panel.", transport: "cultmesh-record"),
            Surface(AetheriaRuntimeMainMenuCommands.VerseSettingsSurfaceId, "Main Menu Verse Settings", AetheriaRuntimeVerseRecordKeys.MainMenuVerseSettingsSurface.ToString(), "player", "Main menu Verse settings panel.", transport: "cultmesh-record"),
            Surface(AetheriaRuntimeInputSettingsCommands.SurfaceId, "Input Settings", "", "player", "Runtime input binding surface.", status: "client-contextual"),
            Surface(AetheriaRuntimeVerseHostCommands.SurfaceId, "Verse Host Settings", "", "operator", "Verse host visibility and discovery surface.", status: "client-contextual"),
            Surface(AetheriaRuntimeLoadoutTemplateCommands.SurfaceId, "Loadout Templates", "", "player", "Loadout template save surface.", status: "client-contextual"),
            Surface(AetheriaRuntimeClientTargetCommands.SurfaceId, "Client Target", "", "operator", "Local client target and Verse discovery surface.", status: "client-contextual"),
            Surface(AetheriaRuntimeLocalStorySurfaceBuilder.SurfaceId, "Local Story", "", "player", "Local runtime story/menu surface.", status: "client-contextual"),
            Surface(AetheriaRuntimeSectorMapSurfaceBuilder.SurfaceId, "Sector Map", AetheriaRuntimeVerseRecordKeys.MapMenuSurface.ToString(), "player", "Daemon-owned discovered sector graph with provider-owned presentation tokens.", transport: "cultmesh-record"),
            Surface(AetheriaRuntimeInventoryDropdownSurfaceBuilder.SurfaceId, "Inventory Dropdown", AetheriaRuntimeVerseRecordKeys.InventoryDropdownSurface.ToString(), "player", "Inventory navigation dropdown surface.", transport: "cultmesh-record"),
            Surface(AetheriaRuntimeShipSettingsSurfaceBuilder.SurfaceId, "Current Ship Settings", "", "player", "Current ship settings surface.", status: "contextual"),
            Surface(AetheriaRuntimeCargoItemDetailsSurfaceBuilder.SurfaceId, "Cargo Item Details", "", "player", "Selected cargo item details surface.", status: "contextual"),
            Surface(AetheriaRuntimeEquippedItemDetailsSurfaceBuilder.SurfaceId, "Equipped Item Details", "", "player", "Selected equipped item details surface.", status: "contextual"),
            Surface("aetheria.trade.menu", "Trade Menu", AetheriaRuntimeVerseRecordKeys.TradeMenuSurface.ToString(), "player", "Daemon-derived station stock trade menu.", transport: "cultmesh-record"),
            Surface(AetheriaRuntimeTradeCargoSelectorSurfaceBuilder.SurfaceId, "Trade Cargo Selector", "", "player", "Trade cargo target selector surface.", status: "contextual"),
            Surface(AetheriaRuntimeTradeInteractionSurfaceBuilder.FilterSurfaceId, "Trade Filter Selector", "", "player", "Trade filter selector surface.", status: "contextual"),
            Surface(AetheriaRuntimeTradeInteractionSurfaceBuilder.RowActionSurfaceId, "Trade Row Actions", "", "player", "Trade row action selector surface.", status: "contextual"),
            Surface(AetheriaRuntimeTradeItemDetailsSurfaceBuilder.SurfaceId, "Trade Item Details", "", "player", "Selected trade item details surface.", status: "contextual"),
            Surface(AetheriaRuntimeStatRecipeCommands.SurfaceId, "Stat Recipes", "", "designer", "Designer stat recipe surface.", status: "designer-preview"),
            Surface(AetheriaRuntimeTradeValuePolicySurfaceBuilder.SurfaceId, "Trade Value Policy", "", "designer", "Designer trade value policy surface.", status: "designer-preview")
        };

        public static IReadOnlyList<AetheriaRuntimeEveSurfaceAdvertisement> All => Surfaces;

        public static IReadOnlyList<string> SurfaceSchemas { get; } = new[] { SurfaceSchema };

        public static AetheriaRuntimeEveSurfaceAdvertisement? Find(string surfaceId)
        {
            if (string.IsNullOrWhiteSpace(surfaceId))
                return null;

            return Surfaces.FirstOrDefault(surface =>
                surface != null &&
                string.Equals(surface.SurfaceId, surfaceId, StringComparison.Ordinal));
        }

        private static AetheriaRuntimeEveSurfaceAdvertisement Surface(
            string surfaceId,
            string title,
            string recordRef,
            string audience,
            string summary,
            params string[] commands)
        {
            return Surface(
                surfaceId,
                title,
                recordRef,
                audience,
                summary,
                "aetheria",
                "game.runtime",
                "cultmesh-managed",
                "available",
                commands: commands);
        }

        private static AetheriaRuntimeEveSurfaceAdvertisement Surface(
            string surfaceId,
            string title,
            string recordRef,
            string audience,
            string summary,
            string providerId = "aetheria",
            string providerKind = "game.runtime",
            string transport = "cultmesh-managed",
            string status = "available",
            string surfaceKind = "",
            AetheriaRuntimeEveWorldInteractionAdvertisement? worldInteraction = null,
            IReadOnlyList<AetheriaRuntimeEvePluginRequirement>? requiresPlugins = null,
            params string[] commands)
        {
            return new AetheriaRuntimeEveSurfaceAdvertisement
            {
                SurfaceId = surfaceId,
                Title = title,
                ProviderId = providerId,
                ProviderKind = providerKind,
                RecordRef = recordRef ?? "",
                Transport = transport,
                Status = status,
                Audience = audience,
                Mode = "interactive",
                Summary = summary,
                Commands = commands?.Where(command => !string.IsNullOrWhiteSpace(command)).ToArray() ?? Array.Empty<string>(),
                SurfaceKind = surfaceKind ?? "",
                WorldInteraction = worldInteraction,
                RequiresPlugins = requiresPlugins ?? Array.Empty<AetheriaRuntimeEvePluginRequirement>()
            };
        }

        private static IReadOnlyList<AetheriaRuntimeEvePluginRequirement> FieldPluginRequirements()
        {
            return new[]
            {
                new AetheriaRuntimeEvePluginRequirement
                {
                    PluginId = "fields.surface",
                    VersionRange = "^0.1.0",
                    Availability = "required",
                    RequiredCapabilities = new[] { "field.surface2d", "gravity.surface" }
                }
            };
        }

        private static AetheriaRuntimeEveWorldInteractionAdvertisement WorldInteraction(string projectionKind)
        {
            return new AetheriaRuntimeEveWorldInteractionAdvertisement
            {
                ProjectionKind = projectionKind ?? "",
                StateSchemas = new[]
                {
                    AetheriaRuntimeDaemonSchemas.Frame,
                    AetheriaRuntimeDaemonSchemas.AssetManifest
                },
                LoweringTargets = new[]
                {
                    "web-reference",
                    "unity-uitoolkit",
                    "unity-scene",
                    "electron-shell",
                    "tui"
                },
                CommandBoundary = "aetheria.daemon.commands",
                ReceiptSchema = AetheriaRuntimeDaemonSchemas.CommittedCommandFact,
                Ownership = "provider-owns-world-state-assets-command-acceptance-and-receipts"
            };
        }
    }
}
