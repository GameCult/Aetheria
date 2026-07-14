using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GameCult.Mesh;
using EveUiCommandRequest = GameCult.Eve.Surface.EveSurfaceCommandRequest;

namespace GameCult.Aetheria.State.Verse
{
    public static class AetheriaRuntimeMainMenuCommands
    {
        public const string RootSurfaceId = "aetheria.main_menu.root";
        public const string SettingsSurfaceId = "aetheria.main_menu.settings";
        public const string InputSettingsSurfaceId = "aetheria.main_menu.input_settings";
        public const string PlayerSettingsSurfaceId = "aetheria.main_menu.player_settings";
        public const string VerseSettingsSurfaceId = "aetheria.main_menu.verse_settings";

        public const string ContinueRun = "aetheria.main_menu.root.continue";
        public const string NewGame = "aetheria.main_menu.root.new_game";
        public const string ShowSettings = "aetheria.main_menu.root.show_settings";
        public const string Quit = "aetheria.main_menu.root.quit";
        public const string OpenRuntimeInputScreen = "aetheria.main_menu.input_settings.open_runtime_screen";
        public const string ShowPlayerSettings = "aetheria.main_menu.settings.show_player_settings";
        public const string ShowVerseSettings = "aetheria.main_menu.settings.show_verse_settings";
        public const string ShowInputSettings = "aetheria.main_menu.settings.show_input_settings";
        public const string BackToMain = "aetheria.main_menu.settings.back_to_main";
        public const string BackToSettings = "aetheria.main_menu.settings.back_to_settings";
    }

    public static class AetheriaRuntimeMainMenuSurfaceBuilder
    {
        public const string ProviderId = "aetheria";
        public const string ProviderKind = "game.menu";

        public static AetheriaRuntimeSurfaceDocument BuildRoot(
            AetheriaRuntimeStateBootReport stateBoot,
            AetheriaRuntimePlayerSettingsDocument playerSettings,
            bool canOpenRuntimeInputScreen,
            bool inGame,
            string updatedAtUtc,
            long version = 1)
        {
            return BuildRoot(
                inGame,
                updatedAtUtc,
                version);
        }

        public static AetheriaRuntimeSurfaceDocument BuildRoot(
            AetheriaRuntimeStateBootReport stateBoot,
            AetheriaRuntimeDaemonFrameDocument daemonFrame,
            AetheriaRuntimeVerseHostSettingsDocument verseHost,
            AetheriaRuntimePlayerSettingsDocument playerSettings,
            bool canOpenRuntimeInputScreen,
            bool inGame,
            string updatedAtUtc,
            long version = 1)
        {
            return BuildRoot(
                inGame,
                updatedAtUtc,
                version);
        }

        public static AetheriaRuntimeSurfaceDocument BuildRoot(
            AetheriaRuntimeStateBootReport stateBoot,
            AetheriaRuntimeSectorMapDocument sectorMap,
            AetheriaRuntimeVerseHostSettingsDocument verseHost,
            AetheriaRuntimePlayerSettingsDocument playerSettings,
            bool canOpenRuntimeInputScreen,
            bool inGame,
            string updatedAtUtc,
            long version = 1)
        {
            return BuildRoot(
                inGame,
                updatedAtUtc,
                version);
        }

        public static AetheriaRuntimeSurfaceDocument BuildInputSettings(
            AetheriaRuntimeStateBootReport stateBoot,
            AetheriaRuntimePlayerSettingsDocument playerSettings,
            bool canOpenRuntimeInputScreen,
            bool inGame,
            string updatedAtUtc,
            long version = 1)
        {
            return BuildInputSettings(
                playerSettings?.BindingOverrides?.Length ?? 0,
                playerSettings?.ActionBarInputs?.Length ?? 0,
                canOpenRuntimeInputScreen,
                inGame,
                updatedAtUtc,
                version);
        }

        public static AetheriaRuntimeSurfaceDocument BuildPlayerSettings(
            AetheriaRuntimePlayerSettingsDocument playerSettings,
            string updatedAtUtc,
            long version = 1)
        {
            return WithBackAction(
                AetheriaRuntimePlayerSettingsSurfaceBuilder.Build(playerSettings, updatedAtUtc, version),
                AetheriaRuntimeMainMenuCommands.PlayerSettingsSurfaceId,
                AetheriaRuntimeMainMenuCommands.BackToSettings,
                "Back");
        }

        private static AetheriaRuntimeSurfaceDocument BuildRoot(
            bool inGame,
            string updatedAtUtc,
            long version)
        {
            var actions = new List<AetheriaRuntimeSurfaceComponent>();
            if (!inGame)
                actions.Add(MenuButton($"{AetheriaRuntimeMainMenuCommands.RootSurfaceId}.continue", "Continue", AetheriaRuntimeMainMenuCommands.ContinueRun));
            actions.Add(MenuButton($"{AetheriaRuntimeMainMenuCommands.RootSurfaceId}.newGame", "New Game", AetheriaRuntimeMainMenuCommands.NewGame));
            actions.Add(MenuButton($"{AetheriaRuntimeMainMenuCommands.RootSurfaceId}.settings", "Settings", AetheriaRuntimeMainMenuCommands.ShowSettings));
            actions.Add(MenuButton($"{AetheriaRuntimeMainMenuCommands.RootSurfaceId}.quit", "Quit", AetheriaRuntimeMainMenuCommands.Quit));

            var root = Node(
                $"{AetheriaRuntimeMainMenuCommands.RootSurfaceId}.root",
                "surface",
                Array.Empty<(string Key, string Value)>(),
                Layout(
                    ("position", "relative"),
                    ("overflow", "hidden"),
                    ("width", "100%"),
                    ("height", "100vh"),
                    ("minHeight", "100vh")),
                Style(("background", "#020606")),
                GravitySurface($"{AetheriaRuntimeMainMenuCommands.RootSurfaceId}.gravity"),
                Node(
                    $"{AetheriaRuntimeMainMenuCommands.RootSurfaceId}.menu",
                    "column",
                    Array.Empty<(string Key, string Value)>(),
                    Layout(
                        ("position", "relative"),
                        ("padding", "7.25rem 0 0 6.75rem"),
                        ("gap", "1.1rem"),
                        ("width", "44rem"),
                        ("maxWidth", "calc(100vw - 3rem)"),
                        ("minHeight", "100vh"),
                        ("alignItems", "flex-start")),
                    Style(("color", "#e9fbff")),
                    Text($"{AetheriaRuntimeMainMenuCommands.RootSurfaceId}.title", "AETHERIA", "text.title",
                        Layout(("margin", "0 0 -1.6rem 0")),
                        Style(
                            ("font", "100 5.9rem/0.98 Montserrat, sans-serif"),
                            ("color", "rgba(232, 250, 255, 0.94)"),
                            ("whiteSpace", "nowrap"))),
                    Text($"{AetheriaRuntimeMainMenuCommands.RootSurfaceId}.subtitle", "STARBRIDGE", "text.subtitle",
                        Layout(("margin", "0 0 0.35rem 16.8rem")),
                        Style(
                            ("font", "100 2.6rem/1 Montserrat, sans-serif"),
                            ("color", "rgba(232, 250, 255, 0.9)"),
                            ("whiteSpace", "nowrap"))),
                    Node(
                        $"{AetheriaRuntimeMainMenuCommands.RootSurfaceId}.actions",
                        "column",
                        Array.Empty<(string Key, string Value)>(),
                        Layout(("gap", "0.18rem"), ("alignItems", "flex-start")),
                        Style(("color", "#e8fbff")),
                        actions.ToArray())));

            return new AetheriaRuntimeSurfaceDocument(
                ProviderId,
                ProviderKind,
                "Aetheria Starbridge",
                version,
                updatedAtUtc,
                new AetheriaRuntimeSurfaceTree(
                    AetheriaRuntimeMainMenuCommands.RootSurfaceId,
                    root,
                    MainMenuStyleTokens()),
                new[]
                {
                    Command(AetheriaRuntimeMainMenuCommands.ContinueRun, "Continue"),
                    Command(AetheriaRuntimeMainMenuCommands.NewGame, "New Game"),
                    Command(AetheriaRuntimeMainMenuCommands.ShowSettings, "Settings"),
                    Command(AetheriaRuntimeMainMenuCommands.Quit, "Quit")
                });
        }

        public static AetheriaRuntimeSurfaceDocument BuildSettings(
            string updatedAtUtc,
            long version = 1)
        {
            var builder = MainMenuSurface(
                    AetheriaRuntimeMainMenuCommands.SettingsSurfaceId,
                    "Aetheria Settings",
                    updatedAtUtc,
                    version)
                .Title("SETTINGS")
                .ButtonColumn(
                    "aetheria.mainMenu.settings.actions",
                    actions =>
                    {
                        actions.Button("Player Settings", Operation(AetheriaRuntimeMainMenuCommands.ShowPlayerSettings, "Player Settings"));
                        actions.Button("Verse", Operation(AetheriaRuntimeMainMenuCommands.ShowVerseSettings, "Verse"));
                        actions.Button("Input", Operation(AetheriaRuntimeMainMenuCommands.ShowInputSettings, "Input"));
                        actions.Button("Back", Operation(AetheriaRuntimeMainMenuCommands.BackToMain, "Back"));
                    });

            return AetheriaRuntimeSurfaceDocuments.FromPortableSurface(builder.Build());
        }

        private static AetheriaRuntimeSurfaceDocument BuildInputSettings(
            int bindingOverrideCount,
            int actionBarInputCount,
            bool canOpenRuntimeInputScreen,
            bool inGame,
            string updatedAtUtc,
            long version)
        {
            var builder = MainMenuSurface(
                    AetheriaRuntimeMainMenuCommands.InputSettingsSurfaceId,
                    "Aetheria Input Settings",
                    updatedAtUtc,
                    version)
                .Title("INPUT")
                .Form(
                    "aetheria.mainMenu.input.metrics",
                    form => form
                        .Metric("Binding Overrides", bindingOverrideCount.ToString())
                        .Metric("Action-Bar Inputs", actionBarInputCount.ToString()));

            if (canOpenRuntimeInputScreen)
            {
                builder.Text(
                    "The runtime Eve input screen owns low-level InputSystem rebinding and action-bar input edits. This panel reports typed player-settings state and hands off to that owner.",
                    "aetheria.mainMenu.input.note");
            }
            else if (inGame)
            {
                builder.Text(
                    "The runtime Eve input screen should own rebinding here, but this scene has no active input surface to hand off to.",
                    "aetheria.mainMenu.input.note");
            }
            else
            {
                builder.Text(
                    "This panel reports typed player-settings state. Launch a run to open the runtime Eve input screen that owns low-level InputSystem rebinding.",
                    "aetheria.mainMenu.input.note");
            }

            builder.ButtonColumn(
                "aetheria.mainMenu.input.actions",
                actions =>
                {
                    if (canOpenRuntimeInputScreen)
                    {
                        actions.Button(
                            "Open Remap Screen",
                            Operation(AetheriaRuntimeMainMenuCommands.OpenRuntimeInputScreen, "Open Remap Screen"));
                    }

                    actions.Button("Back", Operation(AetheriaRuntimeMainMenuCommands.BackToSettings, "Back"));
                });

            return AetheriaRuntimeSurfaceDocuments.FromPortableSurface(builder.Build());
        }

        public static AetheriaRuntimeSurfaceDocument BuildVerseSettings(
            AetheriaRuntimeSurfaceDocument document,
            long version = 1)
        {
            return WithBackAction(
                document,
                AetheriaRuntimeMainMenuCommands.VerseSettingsSurfaceId,
                AetheriaRuntimeMainMenuCommands.BackToSettings,
                "Back");
        }

        public static AetheriaRuntimeSurfaceDocument WithBackAction(
            AetheriaRuntimeSurfaceDocument document,
            string surfaceId,
            string backCommand,
            string backLabel)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));

            return new AetheriaRuntimeSurfaceDocument(
                document.ProviderId,
                document.ProviderKind,
                document.Title,
                document.Version,
                document.UpdatedAtUtc,
                new AetheriaRuntimeSurfaceTree(
                    surfaceId,
                    Node(
                        $"{surfaceId}.root",
                        "surface",
                        Array.Empty<(string Key, string Value)>(),
                        document.Surface.Root,
                        ButtonRow(
                            $"{surfaceId}.actions",
                            Button($"{surfaceId}.back", backLabel, backCommand))),
                    document.Surface.Styles),
                document.Commands
                    .Concat(new[] { Command(backCommand, backLabel) })
                    .ToArray());
        }

        private static GameCult.Eve.Surface.EveSurfaceBuilder MainMenuSurface(
            string surfaceId,
            string title,
            string updatedAtUtc,
            long version)
        {
            var builder = GameCult.Eve.Surface.EveSurface.Create(surfaceId)
                .Provider(ProviderId, ProviderKind)
                .Version(version)
                .UpdatedAtUtc(updatedAtUtc);

            foreach (var token in MainMenuStyleTokens())
                builder.Style(token.Name, token.Value);

            return builder;
        }

        private static IReadOnlyList<AetheriaRuntimeSurfaceStyleToken> MainMenuStyleTokens()
        {
            return new[]
            {
                new AetheriaRuntimeSurfaceStyleToken("font.title.family", "Montserrat"),
                new AetheriaRuntimeSurfaceStyleToken("font.title.style", "Thin"),
                new AetheriaRuntimeSurfaceStyleToken("font.title.weight", "100"),
                new AetheriaRuntimeSurfaceStyleToken("font.body.family", "Ubuntu"),
                new AetheriaRuntimeSurfaceStyleToken("font.body.style", "Regular"),
                new AetheriaRuntimeSurfaceStyleToken("font.body.weight", "400"),
                new AetheriaRuntimeSurfaceStyleToken(
                    "font.web.google",
                    "https://fonts.googleapis.com/css2?family=Montserrat:wght@100&family=Ubuntu:wght@400&display=swap")
            };
        }

        private static AetheriaRuntimeSurfaceCommandTemplate Command(string command, string label)
        {
            return new AetheriaRuntimeSurfaceCommandTemplate(command, label, AetheriaRuntimeSurfaceCommandTemplate.CultMeshTransport);
        }

        private static CultMeshOperationBindingDescriptor Operation(string command, string label)
        {
            return CultMesh.OperationBindingRecord(
                command,
                label,
                "",
                nameof(CultMeshLocalityKind.Automatic),
                AetheriaRuntimeSurfaceCommandTemplate.CultMeshTransport).ToBinding();
        }

        private static AetheriaRuntimeSurfaceComponent Text(string id, string value)
        {
            return Text(id, value, "text");
        }

        private static AetheriaRuntimeSurfaceComponent Text(
            string id,
            string value,
            string kind,
            IReadOnlyDictionary<string, string>? layout = null,
            IReadOnlyDictionary<string, string>? style = null)
        {
            return Node(id, kind, new[] { ("value", value ?? "") }, layout, style);
        }

        private static AetheriaRuntimeSurfaceComponent Button(string id, string label, string command)
        {
            return Node(id, "control.button", new[] { ("label", label), ("command", command) });
        }

        private static AetheriaRuntimeSurfaceComponent ButtonRow(
            string id,
            params AetheriaRuntimeSurfaceComponent[] children)
        {
            return Node(id, "row", Array.Empty<(string Key, string Value)>(), children);
        }

        private static AetheriaRuntimeSurfaceComponent ButtonColumn(
            string id,
            params AetheriaRuntimeSurfaceComponent[] children)
        {
            return Node(id, "column", Array.Empty<(string Key, string Value)>(), children);
        }

        private static AetheriaRuntimeSurfaceComponent Node(
            string id,
            string kind,
            IEnumerable<(string Key, string Value)> props,
            params AetheriaRuntimeSurfaceComponent[] children)
        {
            return Node(id, kind, props, null, null, children);
        }

        private static AetheriaRuntimeSurfaceComponent Node(
            string id,
            string kind,
            IEnumerable<(string Key, string Value)> props,
            IReadOnlyDictionary<string, string>? layout,
            IReadOnlyDictionary<string, string>? style,
            params AetheriaRuntimeSurfaceComponent[] children)
        {
            var normalizedProps = props.ToDictionary(prop => prop.Key, prop => prop.Value ?? "", StringComparer.Ordinal);
            return new AetheriaRuntimeSurfaceComponent(
                id,
                kind,
                normalizedProps,
                children ?? Array.Empty<AetheriaRuntimeSurfaceComponent>(),
                AetheriaRuntimeSurfaceStateBindings.FromProps(normalizedProps),
                Array.Empty<AetheriaRuntimeEmbeddedDocumentSlot>(),
                layout,
                style);
        }

        private static AetheriaRuntimeSurfaceComponent GravitySurface(string id)
        {
            var viewport = MainMenuViewport();
            var renderSplatsDocumentId = ViewportDocumentId("aetheria.viewport.render_splats", viewport);
            var gravityDocumentId = ViewportDocumentId("aetheria.viewport.gravity", viewport);
            var objectsDocumentId = ViewportDocumentId("aetheria.viewport.objects", viewport);
            var props = new[]
            {
                ("label", "Aetheria level field surface"),
                ("minX", F(viewport.MinX)),
                ("minY", F(viewport.MinY)),
                ("maxX", F(viewport.MaxX)),
                ("maxY", F(viewport.MaxY)),
                ("fieldModel", "gamecult.fields.surface2d.v1"),
                ("scalarField", "gravity.height"),
                ("scalarFieldSlot", "gravity"),
                ("scalarFieldSchemaId", AetheriaRuntimeDaemonSchemas.GravityViewport),
                ("scalarFieldDefaultVisualizer", "isolines.branchless"),
                ("scalarFieldVisualizers", "isolines.branchless,height-shade,probe"),
                ("scalarFieldLineInterval", "11.4398025"),
                ("scalarFieldBaseColor", "0.002,0.006,0.012"),
                ("scalarFieldGlowColor", "0.018,0.050,0.075"),
                ("scalarFieldLowLineColor", "0.000,0.340,0.520"),
                ("scalarFieldHighLineColor", "1.450,0.300,0.050"),
                ("scalarFieldLowAngleColor", "0.060,0.160,0.240"),
                ("scalarFieldHighAngleColor", "1.100,0.240,0.040"),
                ("vectorField", "nebula.tint"),
                ("vectorFieldSlot", "renderSplats"),
                ("vectorFieldLayer", AetheriaRuntimeRenderSplatLayerKeys.FogTint),
                ("vectorFieldSchemaId", AetheriaRuntimeDaemonSchemas.RenderSplatsViewport),
                ("vectorFieldDefaultVisualizer", "color.powerpulse"),
                ("vectorFieldVisualizers", "color.powerpulse,probe"),
                ("vectorFieldTintScale", "0.45"),
                ("objectFieldSlot", "objects"),
                ("objectFieldSchemaId", AetheriaRuntimeDaemonSchemas.ObjectsViewport),
                ("bodyIconMinPx", "24"),
                ("sunIconMinPx", "34"),
                ("bodyIconScale", "0.48"),
                ("sunIconScale", "0.72"),
                ("bodyLabelColor", "rgba(226, 244, 255, 0.82)"),
                ("objectLabelFont", "700 12px Ubuntu, system-ui, sans-serif"),
                ("objectLabelStroke", "rgba(0, 0, 0, 0.72)"),
                ("objectLabelStrokeWidth", "3"),
                ("objectControlledColor", "rgba(122, 240, 255, {alpha})"),
                ("objectRaiderColor", "rgba(255, 143, 74, {alpha})"),
                ("objectNeutralColor", "rgba(232, 232, 224, {alpha})"),
                ("objectDefaultColor", "rgba(214, 244, 255, {alpha})"),
                ("shipIconSizePx", "22"),
                ("remoteShipIconSizePx", "18"),
                ("stationIconSizePx", "34"),
                ("renderSplatsDocumentId", renderSplatsDocumentId),
                ("renderSplatsSchemaId", AetheriaRuntimeDaemonSchemas.RenderSplatsViewport),
                ("gravityDocumentId", gravityDocumentId),
                ("gravitySchemaId", AetheriaRuntimeDaemonSchemas.GravityViewport),
                ("objectsDocumentId", objectsDocumentId),
                ("objectsSchemaId", AetheriaRuntimeDaemonSchemas.ObjectsViewport),
                ("samplesX", "196"),
                ("shader", "aetheria.field-surface2d.v1"),
                ("stateRefreshMs", "50")
            };
            var normalizedProps = props.ToDictionary(prop => prop.Item1, prop => prop.Item2 ?? "", StringComparer.Ordinal);
            return new AetheriaRuntimeSurfaceComponent(
                id,
                "field.surface2d",
                normalizedProps,
                Array.Empty<AetheriaRuntimeSurfaceComponent>(),
                AetheriaRuntimeSurfaceStateBindings.FromProps(normalizedProps),
                new[]
                {
                    new AetheriaRuntimeEmbeddedDocumentSlot(
                        "renderSplats",
                        renderSplatsDocumentId,
                        AetheriaRuntimeDaemonSchemas.RenderSplatsViewport,
                        "data"),
                    new AetheriaRuntimeEmbeddedDocumentSlot(
                        "gravity",
                        gravityDocumentId,
                        AetheriaRuntimeDaemonSchemas.GravityViewport,
                        "data"),
                    new AetheriaRuntimeEmbeddedDocumentSlot(
                        "objects",
                        objectsDocumentId,
                        AetheriaRuntimeDaemonSchemas.ObjectsViewport,
                        "data")
                },
                Layout(
                    ("position", "absolute"),
                    ("top", "0"),
                    ("right", "0"),
                    ("bottom", "0"),
                    ("left", "0"),
                    ("width", "100%"),
                    ("height", "100%")),
                null);
        }

        private static AetheriaRuntimeViewportBounds MainMenuViewport()
        {
            return new AetheriaRuntimeViewportBounds
            {
                MinX = -1500,
                MinY = -1000,
                MaxX = 1500,
                MaxY = 1000
            };
        }

        private static string ViewportDocumentId(string prefix, AetheriaRuntimeViewportBounds viewport)
        {
            var normalized = AetheriaRuntimeGameDocuments.Normalize(viewport);
            return string.Join(
                ".",
                prefix,
                ViewportToken(normalized.MinX),
                ViewportToken(normalized.MinY),
                ViewportToken(normalized.MaxX),
                ViewportToken(normalized.MaxY));
        }

        private static string ViewportToken(double value)
        {
            return value
                .ToString("0.###", CultureInfo.InvariantCulture)
                .Replace('-', 'n')
                .Replace('.', 'p');
        }

        private static AetheriaRuntimeSurfaceComponent MenuButton(string id, string label, string command)
        {
            return Node(
                id,
                "control.button",
                new[] { ("label", label), ("command", command) },
                Layout(("minWidth", "0"), ("padding", "0.04rem 0")),
                Style(
                    ("background", "rgba(0, 0, 0, 0)"),
                    ("borderWidth", "0"),
                    ("borderStyle", "solid"),
                    ("boxShadow", "none"),
                    ("font", "400 1.55rem/1.2 Ubuntu, sans-serif"),
                    ("color", "#e8fbff"),
                    ("textAlign", "left")),
                Array.Empty<AetheriaRuntimeSurfaceComponent>());
        }

        private static IReadOnlyDictionary<string, string> Layout(params (string Key, string Value)[] values)
        {
            return values.ToDictionary(value => value.Key, value => value.Value ?? "", StringComparer.Ordinal);
        }

        private static IReadOnlyDictionary<string, string> Style(params (string Key, string Value)[] values)
        {
            return Layout(values);
        }

        private static string F(double value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

    }

    public enum AetheriaRuntimeMainMenuCommandKind
    {
        Unknown = 0,
        ContinueRun = 1,
        NewGame = 2,
        ShowSettings = 3,
        Quit = 4,
        ShowPlayerSettings = 5,
        ShowVerseSettings = 6,
        ShowInputSettings = 7,
        BackToMain = 8,
        BackToSettings = 9,
        OpenRuntimeInputScreen = 10,
        PlayerSettingsCommand = 11,
        ClientTargetCommand = 12,
        VerseHostCommand = 13
    }

    public readonly struct AetheriaRuntimeMainMenuCommand
    {
        public AetheriaRuntimeMainMenuCommand(
            AetheriaRuntimeMainMenuCommandKind kind,
            string command = "")
        {
            Kind = kind;
            Command = command ?? "";
        }

        public AetheriaRuntimeMainMenuCommandKind Kind { get; }
        public string Command { get; }
    }

    public static class AetheriaRuntimeMainMenuSurfaceCommands
    {
        public static bool TryRead(
            EveUiCommandRequest request,
            out AetheriaRuntimeMainMenuCommand command)
        {
            command = default;
            if (request == null)
                return false;

            var operationId = request.Operation?.OperationId ?? "";
            switch (request.SurfaceId ?? "")
            {
                case AetheriaRuntimeMainMenuCommands.RootSurfaceId:
                    return TryReadRoot(operationId, out command);
                case AetheriaRuntimeMainMenuCommands.SettingsSurfaceId:
                    return TryReadSettings(operationId, out command);
                case AetheriaRuntimeMainMenuCommands.InputSettingsSurfaceId:
                    return TryReadInputSettings(operationId, out command);
                case AetheriaRuntimeMainMenuCommands.PlayerSettingsSurfaceId:
                    return TryReadPlayerSettings(operationId, out command);
                case AetheriaRuntimeMainMenuCommands.VerseSettingsSurfaceId:
                    return TryReadVerseSettings(operationId, out command);
                default:
                    return false;
            }
        }

        private static bool TryReadRoot(string commandText, out AetheriaRuntimeMainMenuCommand command)
        {
            switch (commandText)
            {
                case AetheriaRuntimeMainMenuCommands.ContinueRun:
                    command = new AetheriaRuntimeMainMenuCommand(AetheriaRuntimeMainMenuCommandKind.ContinueRun, commandText);
                    return true;
                case AetheriaRuntimeMainMenuCommands.NewGame:
                    command = new AetheriaRuntimeMainMenuCommand(AetheriaRuntimeMainMenuCommandKind.NewGame, commandText);
                    return true;
                case AetheriaRuntimeMainMenuCommands.ShowSettings:
                    command = new AetheriaRuntimeMainMenuCommand(AetheriaRuntimeMainMenuCommandKind.ShowSettings, commandText);
                    return true;
                case AetheriaRuntimeMainMenuCommands.Quit:
                    command = new AetheriaRuntimeMainMenuCommand(AetheriaRuntimeMainMenuCommandKind.Quit, commandText);
                    return true;
                default:
                    command = default;
                    return false;
            }
        }

        private static bool TryReadSettings(string commandText, out AetheriaRuntimeMainMenuCommand command)
        {
            switch (commandText)
            {
                case AetheriaRuntimeMainMenuCommands.ShowPlayerSettings:
                    command = new AetheriaRuntimeMainMenuCommand(AetheriaRuntimeMainMenuCommandKind.ShowPlayerSettings, commandText);
                    return true;
                case AetheriaRuntimeMainMenuCommands.ShowVerseSettings:
                    command = new AetheriaRuntimeMainMenuCommand(AetheriaRuntimeMainMenuCommandKind.ShowVerseSettings, commandText);
                    return true;
                case AetheriaRuntimeMainMenuCommands.ShowInputSettings:
                    command = new AetheriaRuntimeMainMenuCommand(AetheriaRuntimeMainMenuCommandKind.ShowInputSettings, commandText);
                    return true;
                case AetheriaRuntimeMainMenuCommands.BackToMain:
                    command = new AetheriaRuntimeMainMenuCommand(AetheriaRuntimeMainMenuCommandKind.BackToMain, commandText);
                    return true;
                default:
                    command = default;
                    return false;
            }
        }

        private static bool TryReadInputSettings(string commandText, out AetheriaRuntimeMainMenuCommand command)
        {
            switch (commandText)
            {
                case AetheriaRuntimeMainMenuCommands.OpenRuntimeInputScreen:
                    command = new AetheriaRuntimeMainMenuCommand(AetheriaRuntimeMainMenuCommandKind.OpenRuntimeInputScreen, commandText);
                    return true;
                case AetheriaRuntimeMainMenuCommands.BackToSettings:
                    command = new AetheriaRuntimeMainMenuCommand(AetheriaRuntimeMainMenuCommandKind.BackToSettings, commandText);
                    return true;
                default:
                    command = default;
                    return false;
            }
        }

        private static bool TryReadPlayerSettings(string commandText, out AetheriaRuntimeMainMenuCommand command)
        {
            if (string.Equals(commandText, AetheriaRuntimeMainMenuCommands.BackToSettings, StringComparison.Ordinal))
            {
                command = new AetheriaRuntimeMainMenuCommand(AetheriaRuntimeMainMenuCommandKind.BackToSettings, commandText);
                return true;
            }

            if (AetheriaRuntimePlayerSettingsCommands.IsKnown(commandText))
            {
                command = new AetheriaRuntimeMainMenuCommand(AetheriaRuntimeMainMenuCommandKind.PlayerSettingsCommand, commandText);
                return true;
            }

            command = default;
            return false;
        }

        private static bool TryReadVerseSettings(string commandText, out AetheriaRuntimeMainMenuCommand command)
        {
            if (string.Equals(commandText, AetheriaRuntimeMainMenuCommands.BackToSettings, StringComparison.Ordinal))
            {
                command = new AetheriaRuntimeMainMenuCommand(AetheriaRuntimeMainMenuCommandKind.BackToSettings, commandText);
                return true;
            }

            if (AetheriaRuntimeClientTargetCommands.IsKnown(commandText))
            {
                command = new AetheriaRuntimeMainMenuCommand(AetheriaRuntimeMainMenuCommandKind.ClientTargetCommand, commandText);
                return true;
            }

            if (AetheriaRuntimeVerseHostCommands.IsKnown(commandText))
            {
                command = new AetheriaRuntimeMainMenuCommand(AetheriaRuntimeMainMenuCommandKind.VerseHostCommand, commandText);
                return true;
            }

            command = default;
            return false;
        }
    }
}
