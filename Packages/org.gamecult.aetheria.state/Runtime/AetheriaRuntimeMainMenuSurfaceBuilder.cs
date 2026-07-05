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
                stateBoot.TargetLabel,
                stateBoot.TargetKind,
                stateBoot.TargetSource,
                stateBoot.Title,
                stateBoot.VerseId,
                "unknown",
                stateBoot.CultMeshAddress,
                inGame,
                hasAuthoritativeDaemonFrame: false,
                daemonRunId: "",
                daemonFrameId: -1,
                gravityField: AetheriaMainMenuGravityField.Default(),
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
                stateBoot.TargetLabel,
                stateBoot.TargetKind,
                stateBoot.TargetSource,
                verseHost?.Title ?? stateBoot.Title,
                verseHost?.VerseId ?? stateBoot.VerseId,
                verseHost?.Visibility ?? "unknown",
                verseHost?.CultMeshAddress ?? stateBoot.CultMeshAddress,
                inGame,
                hasAuthoritativeDaemonFrame: daemonFrame != null,
                daemonFrame?.Run?.RunId ?? "",
                daemonFrame?.FrameId ?? -1,
                gravityField: AetheriaMainMenuGravityField.FromDaemonFrame(daemonFrame),
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
                stateBoot.TargetLabel,
                stateBoot.TargetKind,
                stateBoot.TargetSource,
                verseHost?.Title ?? stateBoot.Title,
                verseHost?.VerseId ?? stateBoot.VerseId,
                verseHost?.Visibility ?? "unknown",
                verseHost?.CultMeshAddress ?? stateBoot.CultMeshAddress,
                inGame,
                hasAuthoritativeDaemonFrame: sectorMap != null,
                sectorMap?.RunId ?? "",
                sectorMap?.FrameId ?? -1,
                gravityField: AetheriaMainMenuGravityField.Default(),
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
            string targetLabel,
            string targetKind,
            string targetSource,
            string verseTitle,
            string verseId,
            string verseVisibility,
            string verseCultMeshAddress,
            bool inGame,
            bool hasAuthoritativeDaemonFrame,
            string daemonRunId,
            long daemonFrameId,
            AetheriaMainMenuGravityField gravityField,
            string updatedAtUtc,
            long version)
        {
            gravityField = gravityField.IsEmpty ? AetheriaMainMenuGravityField.Default() : gravityField;
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
                GravitySurface($"{AetheriaRuntimeMainMenuCommands.RootSurfaceId}.gravity", gravityField),
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
                    Text($"{AetheriaRuntimeMainMenuCommands.RootSurfaceId}.subtitle", "TERMINUS", "text.subtitle",
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
                "Aetheria Terminus",
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

        private static string VerseLabel(string verseTitle, string verseId)
        {
            if (string.IsNullOrWhiteSpace(verseTitle))
                return string.IsNullOrWhiteSpace(verseId) ? "Unknown Verse" : verseId;

            if (string.IsNullOrWhiteSpace(verseId) || string.Equals(verseTitle, verseId, StringComparison.Ordinal))
                return verseTitle;

            return $"{verseTitle} / {verseId}";
        }

        private static string TargetLine(string targetLabel, string targetKind, string targetSource)
        {
            var label = string.IsNullOrWhiteSpace(targetLabel) ? "Unknown target" : targetLabel;
            var kind = string.IsNullOrWhiteSpace(targetKind) ? "unknown" : targetKind;
            if (string.IsNullOrWhiteSpace(targetSource))
                return $"{label} ({kind})";

            return $"{label} ({kind}) / {targetSource}";
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

        private static AetheriaRuntimeSurfaceComponent GravitySurface(
            string id,
            AetheriaMainMenuGravityField gravityField)
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
                ("fieldModel", "aetheria.fields2d.v1"),
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

        private static AetheriaRuntimeRtsViewportBounds MainMenuViewport()
        {
            return new AetheriaRuntimeRtsViewportBounds
            {
                MinX = -1500,
                MinY = -1000,
                MaxX = 1500,
                MaxY = 1000
            };
        }

        private static string ViewportDocumentId(string prefix, AetheriaRuntimeRtsViewportBounds viewport)
        {
            var normalized = AetheriaRuntimeRtsDocuments.Normalize(viewport);
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

        private static string GravityBodies(IReadOnlyList<AetheriaMainMenuGravityBody> bodies)
        {
            return string.Join(
                ";",
                (bodies ?? Array.Empty<AetheriaMainMenuGravityBody>())
                    .Select(body => string.Join(
                        "|",
                        body.Key,
                        body.Kind,
                        F(body.X),
                        F(body.Y),
                        F(body.Radius),
                        F(body.Depth),
                        F(body.Exponent),
                        F(body.WaveRadius),
                        F(body.WaveDepth),
                        F(body.WaveSpeed),
                        body.IconAssetUri,
                        body.TintSplatAssetUri)));
        }

        private static string GravityObjects(IReadOnlyList<AetheriaMainMenuGravityObject> objects)
        {
            return string.Join(
                ";",
                (objects ?? Array.Empty<AetheriaMainMenuGravityObject>())
                    .Select(obj => string.Join(
                        "|",
                        S(obj.Key),
                        S(obj.Kind),
                        S(obj.Label),
                        F(obj.X),
                        F(obj.Y),
                        F(obj.DirectionX),
                        F(obj.DirectionY),
                        S(obj.FactionKey),
                        obj.Controlled ? "1" : "0",
                        F(obj.Visibility),
                        S(obj.IconAssetUri))));
        }

        private static string S(string value)
        {
            return (value ?? "").Replace("|", "/").Replace(";", ",");
        }
    }

    internal readonly struct AetheriaMainMenuGravityField
    {
        public AetheriaMainMenuGravityField(
            double viewRadius,
            double terrainRadius,
            double terrainDepth,
            double terrainDepthExponent,
            double terrainWaveFrequency,
            double simulationTimeSeconds,
            IReadOnlyList<AetheriaMainMenuGravityBody> bodies,
            IReadOnlyList<AetheriaMainMenuGravityObject>? objects = null)
        {
            ViewRadius = viewRadius;
            TerrainRadius = terrainRadius;
            TerrainDepth = terrainDepth;
            TerrainDepthExponent = terrainDepthExponent <= 0 ? 1.0 : terrainDepthExponent;
            TerrainWaveFrequency = terrainWaveFrequency;
            SimulationTimeSeconds = simulationTimeSeconds;
            Bodies = bodies ?? Array.Empty<AetheriaMainMenuGravityBody>();
            Objects = objects ?? Array.Empty<AetheriaMainMenuGravityObject>();
        }

        public double ViewRadius { get; }
        public double TerrainRadius { get; }
        public double TerrainDepth { get; }
        public double TerrainDepthExponent { get; }
        public double TerrainWaveFrequency { get; }
        public double SimulationTimeSeconds { get; }
        public IReadOnlyList<AetheriaMainMenuGravityBody> Bodies { get; }
        public IReadOnlyList<AetheriaMainMenuGravityObject> Objects { get; }
        public bool IsEmpty => Bodies == null || Bodies.Count == 0;

        public static AetheriaMainMenuGravityField Default()
        {
            return new AetheriaMainMenuGravityField(
                1200,
                1200,
                -8,
                1.2,
                0.6,
                0,
                new[]
                {
                    new AetheriaMainMenuGravityBody("terminus-sun", "Sun", 0, 0, 900, -80, 3, 450, 10, 2),
                    new AetheriaMainMenuGravityBody("anchor-moon", "Planet", 360, -180, 260, -22, 2.2, 180, 4, 1.4)
                },
                new[]
                {
                    new AetheriaMainMenuGravityObject("anchor-station", "station", "Anchor Station", -220, -90, 1, 0, "player", true, 1),
                    new AetheriaMainMenuGravityObject("vanguard-one", "ship", "Vanguard One", -60, -40, 1, 0.2, "player", true, 1),
                    new AetheriaMainMenuGravityObject("ash-raider", "ship", "Ash Raider", 420, 180, -0.8, -0.1, "raider", false, 1)
                });
        }

        public static AetheriaMainMenuGravityField FromDaemonFrame(AetheriaRuntimeDaemonFrameDocument? frame)
        {
            var run = frame?.Run;
            var zones = run?.Zones ?? Array.Empty<AetheriaRuntimeZoneSnapshotCommit>();
            var currentZone = zones.FirstOrDefault(zone => zone != null && zone.ZoneIndex == run?.CurrentZoneIndex)
                ?? zones.FirstOrDefault(zone => zone != null);
            if (currentZone == null)
                return Default();

            var bodies = (currentZone.Bodies ?? Array.Empty<AetheriaRuntimeBodySnapshotCommit>())
                .Where(body => body != null)
                .Select(body => new AetheriaMainMenuGravityBody(
                    string.IsNullOrWhiteSpace(body.BodyKey) ? body.Name : body.BodyKey,
                    body.Kind,
                    double.IsNaN(body.GravityInfluenceCenterX) ? 0 : body.GravityInfluenceCenterX,
                    double.IsNaN(body.GravityInfluenceCenterZ) ? 0 : body.GravityInfluenceCenterZ,
                    body.GravityInfluenceRadius > 0 ? body.GravityInfluenceRadius : Math.Max(160, body.GravityRadiusMultiplier * 220),
                    body.GravityWellDepth,
                    body.GravityDepthExponent <= 0 ? 1.0 : body.GravityDepthExponent,
                    body.GravityWaveRadius,
                    body.GravityWaveDepth,
                    body.GravityWaveSpeed))
                .ToArray();
            var objects = (currentZone.Entities ?? Array.Empty<AetheriaRuntimeEntitySnapshotCommit>())
                .Where(entity => entity != null && entity.IsActive)
                .Select(entity =>
                {
                    var entityKey = AetheriaRuntimeRunCheckpointCommit.EntityRecordKey(run?.RunId ?? "local-rts", currentZone.ZoneIndex, entity.EntityIndex);
                    return new AetheriaMainMenuGravityObject(
                        entityKey,
                        entity.Kind,
                        string.IsNullOrWhiteSpace(entity.Name) ? $"entity {entity.EntityIndex.ToString(CultureInfo.InvariantCulture)}" : entity.Name,
                        entity.PositionX,
                        entity.PositionZ,
                        entity.DirectionX,
                        entity.DirectionY,
                        entity.FactionKey,
                        string.Equals(entity.FactionKey, "player", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(entityKey, run?.CurrentEntityKey, StringComparison.Ordinal),
                        entity.Visibility <= 0 ? 1 : entity.Visibility);
                })
                .ToArray();

            return new AetheriaMainMenuGravityField(
                Math.Max(900, AutoViewRadius(currentZone, bodies, objects)),
                currentZone.GravityTerrainRadius > 0 ? currentZone.GravityTerrainRadius : 1200,
                currentZone.GravityTerrainDepth == 0 ? -8 : currentZone.GravityTerrainDepth,
                currentZone.GravityTerrainDepthExponent <= 0 ? 1.0 : currentZone.GravityTerrainDepthExponent,
                currentZone.GravityTerrainWaveFrequency,
                currentZone.SimulationTimeSeconds,
                bodies.Length == 0 ? Default().Bodies : bodies,
                objects);
        }

        private static double AutoViewRadius(
            AetheriaRuntimeZoneSnapshotCommit currentZone,
            IReadOnlyList<AetheriaMainMenuGravityBody> bodies,
            IReadOnlyList<AetheriaMainMenuGravityObject> objects)
        {
            var radius = currentZone.GravityTerrainRadius > 0 ? currentZone.GravityTerrainRadius : 1200;
            foreach (var body in bodies ?? Array.Empty<AetheriaMainMenuGravityBody>())
                radius = Math.Max(radius, Math.Max(Math.Abs(body.X), Math.Abs(body.Y)) + body.Radius * 1.1);
            foreach (var obj in objects ?? Array.Empty<AetheriaMainMenuGravityObject>())
                radius = Math.Max(radius, Math.Max(Math.Abs(obj.X), Math.Abs(obj.Y)) + 220);
            return radius;
        }
    }

    internal readonly struct AetheriaMainMenuGravityBody
    {
        public AetheriaMainMenuGravityBody(
            string key,
            string kind,
            double x,
            double y,
            double radius,
            double depth,
            double exponent,
            double waveRadius,
            double waveDepth,
            double waveSpeed)
        {
            Key = key ?? "";
            Kind = string.IsNullOrWhiteSpace(kind) ? "Body" : kind;
            X = x;
            Y = y;
            Radius = radius <= 0 ? 1 : radius;
            Depth = depth;
            Exponent = exponent <= 0 ? 1.0 : exponent;
            WaveRadius = waveRadius;
            WaveDepth = waveDepth;
            WaveSpeed = waveSpeed;
            IconAssetUri = $"cultmesh://aetheria/assets/icons/ui/{IconKind(Kind)}";
            TintSplatAssetUri = "cultmesh://aetheria/assets/textures/tint_splat";
        }

        public string Key { get; }
        public string Kind { get; }
        public double X { get; }
        public double Y { get; }
        public double Radius { get; }
        public double Depth { get; }
        public double Exponent { get; }
        public double WaveRadius { get; }
        public double WaveDepth { get; }
        public double WaveSpeed { get; }
        public string IconAssetUri { get; }
        public string TintSplatAssetUri { get; }

        private static string IconKind(string kind)
        {
            var normalized = kind?.ToLowerInvariant() ?? "";
            if (normalized.Contains("sun") || normalized.Contains("star"))
                return "sun";
            if (normalized.Contains("gas"))
                return "gasgiant";
            if (normalized.Contains("moon"))
                return "moon";
            if (normalized.Contains("orbital") || normalized.Contains("station") || normalized.Contains("colony"))
                return "orbital";
            if (normalized.Contains("wormhole"))
                return "wormhole";
            return "planet";
        }
    }

    internal readonly struct AetheriaMainMenuGravityObject
    {
        public AetheriaMainMenuGravityObject(
            string key,
            string kind,
            string label,
            double x,
            double y,
            double directionX,
            double directionY,
            string factionKey,
            bool controlled,
            double visibility)
        {
            Key = key ?? "";
            Kind = string.IsNullOrWhiteSpace(kind) ? "object" : kind;
            Label = label ?? "";
            X = x;
            Y = y;
            DirectionX = directionX;
            DirectionY = directionY;
            FactionKey = factionKey ?? "";
            Controlled = controlled;
            Visibility = visibility <= 0 ? 1 : visibility;
            IconAssetUri = $"cultmesh://aetheria/assets/icons/ui/{IconKind(Kind)}";
        }

        public string Key { get; }
        public string Kind { get; }
        public string Label { get; }
        public double X { get; }
        public double Y { get; }
        public double DirectionX { get; }
        public double DirectionY { get; }
        public string FactionKey { get; }
        public bool Controlled { get; }
        public double Visibility { get; }
        public string IconAssetUri { get; }

        private static string IconKind(string kind)
        {
            var normalized = kind?.ToLowerInvariant() ?? "";
            if (normalized.Contains("station") || normalized.Contains("colony"))
                return "orbital";
            return "ship";
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
