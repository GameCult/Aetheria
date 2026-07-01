using System;
using System.Collections.Generic;
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
        public const string PlayerSettingsShellSurfaceId = "aetheria.main_menu.player_settings";
        public const string VerseSettingsShellSurfaceId = "aetheria.main_menu.verse_settings";

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

        public static AetheriaRuntimeSurfaceDocument BuildPlayerSettingsShell(
            AetheriaRuntimePlayerSettingsDocument playerSettings,
            string updatedAtUtc,
            long version = 1)
        {
            return WithBackAction(
                AetheriaRuntimePlayerSettingsSurfaceBuilder.Build(playerSettings, updatedAtUtc, version),
                AetheriaRuntimeMainMenuCommands.PlayerSettingsShellSurfaceId,
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
            string updatedAtUtc,
            long version)
        {
            var verseLabel = VerseLabel(verseTitle, verseId);
            var targetLine = TargetLine(targetLabel, targetKind, targetSource);
            var daemonLine = hasAuthoritativeDaemonFrame
                ? $"Run {daemonRunId} / frame {daemonFrameId}"
                : "No daemon frame";
            var transportLine = string.IsNullOrWhiteSpace(verseCultMeshAddress)
                ? verseVisibility
                : $"{verseVisibility} / {verseCultMeshAddress}";

            var builder = MainMenuSurface(
                    AetheriaRuntimeMainMenuCommands.RootSurfaceId,
                    "Aetheria Terminus",
                    updatedAtUtc,
                    version)
                .TitleSubtitle("AETHERIA", "TERMINUS")
                .ButtonColumn(
                    $"{AetheriaRuntimeMainMenuCommands.RootSurfaceId}.actions",
                    actions =>
                    {
                        if (!inGame)
                        {
                            actions.Button(
                                "Continue",
                                Operation(AetheriaRuntimeMainMenuCommands.ContinueRun, "Continue"));
                        }

                        actions.Button("New Game", Operation(AetheriaRuntimeMainMenuCommands.NewGame, "New Game"));
                        actions.Button("Settings", Operation(AetheriaRuntimeMainMenuCommands.ShowSettings, "Settings"));
                        actions.Button("Quit", Operation(AetheriaRuntimeMainMenuCommands.Quit, "Quit"));
                    })
                .Form(
                    $"{AetheriaRuntimeMainMenuCommands.RootSurfaceId}.verse",
                    form => form
                        .Metric("Verse", verseLabel)
                        .Metric("Target", targetLine)
                        .Metric("Daemon", daemonLine)
                        .Metric("Transport", transportLine));

            return AetheriaRuntimeSurfaceDocuments.FromPortableSurface(builder.Build());
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
                    "The runtime Eve input screen owns low-level InputSystem rebinding and action-bar input edits. This title shell reports typed player-settings state and hands off to that owner.",
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
                    "This title shell reports the typed player-settings state. Launch a run to open the runtime Eve input screen that owns low-level InputSystem rebinding.",
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

        public static AetheriaRuntimeSurfaceDocument BuildVerseSettingsShell(
            AetheriaRuntimeSurfaceDocument document,
            long version = 1)
        {
            return WithBackAction(
                document,
                AetheriaRuntimeMainMenuCommands.VerseSettingsShellSurfaceId,
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

        private static GameCult.Mesh.EveSurfaceBuilder MainMenuSurface(
            string surfaceId,
            string title,
            string updatedAtUtc,
            long version)
        {
            var builder = GameCult.Mesh.EveSurface.Create(surfaceId)
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

        private static AetheriaRuntimeSurfaceComponent Text(string id, string value, string kind)
        {
            return Node(id, kind, new[] { ("value", value ?? "") });
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
            return new AetheriaRuntimeSurfaceComponent(
                id,
                kind,
                props.ToDictionary(prop => prop.Key, prop => prop.Value ?? "", StringComparer.Ordinal),
                children ?? Array.Empty<AetheriaRuntimeSurfaceComponent>());
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
                case AetheriaRuntimeMainMenuCommands.PlayerSettingsShellSurfaceId:
                    return TryReadPlayerSettingsShell(operationId, out command);
                case AetheriaRuntimeMainMenuCommands.VerseSettingsShellSurfaceId:
                    return TryReadVerseSettingsShell(operationId, out command);
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

        private static bool TryReadPlayerSettingsShell(string commandText, out AetheriaRuntimeMainMenuCommand command)
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

        private static bool TryReadVerseSettingsShell(string commandText, out AetheriaRuntimeMainMenuCommand command)
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
