using System;
using System.Collections.Generic;
using System.Linq;
using GameCult.Eve.Surface;

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
            var commands = new List<AetheriaRuntimeSurfaceCommandTemplate>();
            var actionButtons = new List<AetheriaRuntimeSurfaceComponent>();

            if (!inGame)
            {
                commands.Add(Command(AetheriaRuntimeMainMenuCommands.ContinueRun, "Continue"));
                actionButtons.Add(Button(
                    $"{AetheriaRuntimeMainMenuCommands.RootSurfaceId}.continue",
                    "Continue",
                    AetheriaRuntimeMainMenuCommands.ContinueRun));
            }

            commands.Add(Command(AetheriaRuntimeMainMenuCommands.NewGame, "New Game"));
            commands.Add(Command(AetheriaRuntimeMainMenuCommands.ShowSettings, "Settings"));
            commands.Add(Command(AetheriaRuntimeMainMenuCommands.Quit, "Quit"));

            actionButtons.Add(Button($"{AetheriaRuntimeMainMenuCommands.RootSurfaceId}.newGame", "New Game", AetheriaRuntimeMainMenuCommands.NewGame));
            actionButtons.Add(Button($"{AetheriaRuntimeMainMenuCommands.RootSurfaceId}.settings", "Settings", AetheriaRuntimeMainMenuCommands.ShowSettings));
            actionButtons.Add(Button($"{AetheriaRuntimeMainMenuCommands.RootSurfaceId}.quit", "Quit", AetheriaRuntimeMainMenuCommands.Quit));

            return BuildSurfaceDocument(
                AetheriaRuntimeMainMenuCommands.RootSurfaceId,
                "Aetheria Terminus",
                updatedAtUtc,
                version,
                commands,
                Text($"{AetheriaRuntimeMainMenuCommands.RootSurfaceId}.title", "AETHERIA", "text.title"),
                Text($"{AetheriaRuntimeMainMenuCommands.RootSurfaceId}.subtitle", "TERMINUS", "text.subtitle"),
                ButtonColumn($"{AetheriaRuntimeMainMenuCommands.RootSurfaceId}.actions", actionButtons.ToArray()));
        }

        public static AetheriaRuntimeSurfaceDocument BuildSettings(
            string updatedAtUtc,
            long version = 1)
        {
            return BuildSurfaceDocument(
                AetheriaRuntimeMainMenuCommands.SettingsSurfaceId,
                "Aetheria Settings",
                updatedAtUtc,
                version,
                new[]
                {
                    Command(AetheriaRuntimeMainMenuCommands.ShowPlayerSettings, "Player Settings"),
                    Command(AetheriaRuntimeMainMenuCommands.ShowVerseSettings, "Verse"),
                    Command(AetheriaRuntimeMainMenuCommands.ShowInputSettings, "Input"),
                    Command(AetheriaRuntimeMainMenuCommands.BackToMain, "Back")
                },
                Text("aetheria.mainMenu.settings.title", "SETTINGS", "text.title"),
                ButtonColumn(
                    "aetheria.mainMenu.settings.actions",
                    Button("aetheria.mainMenu.settings.playerSettings", "Player Settings", AetheriaRuntimeMainMenuCommands.ShowPlayerSettings),
                    Button("aetheria.mainMenu.settings.verse", "Verse", AetheriaRuntimeMainMenuCommands.ShowVerseSettings),
                    Button("aetheria.mainMenu.settings.input", "Input", AetheriaRuntimeMainMenuCommands.ShowInputSettings),
                    Button("aetheria.mainMenu.settings.back", "Back", AetheriaRuntimeMainMenuCommands.BackToMain)));
        }

        private static AetheriaRuntimeSurfaceDocument BuildInputSettings(
            int bindingOverrideCount,
            int actionBarInputCount,
            bool canOpenRuntimeInputScreen,
            bool inGame,
            string updatedAtUtc,
            long version)
        {
            var commands = new List<AetheriaRuntimeSurfaceCommandTemplate>
            {
                Command(AetheriaRuntimeMainMenuCommands.BackToSettings, "Back")
            };

            var children = new List<AetheriaRuntimeSurfaceComponent>
            {
                Text("aetheria.mainMenu.input.title", "INPUT", "text.title"),
                Metric(
                    "aetheria.mainMenu.input.bindingOverrides",
                    "Binding Overrides",
                    bindingOverrideCount.ToString()),
                Metric(
                    "aetheria.mainMenu.input.actionBarInputs",
                    "Action-Bar Inputs",
                    actionBarInputCount.ToString())
            };

            if (canOpenRuntimeInputScreen)
            {
                commands.Insert(0, Command(AetheriaRuntimeMainMenuCommands.OpenRuntimeInputScreen, "Open Remap Screen"));
                children.Add(Text(
                    "aetheria.mainMenu.input.note",
                    "The runtime Eve input screen owns low-level InputSystem rebinding and action-bar input edits. This title shell reports typed player-settings state and hands off to that owner."));
            }
            else if (inGame)
            {
                children.Add(Text(
                    "aetheria.mainMenu.input.note",
                    "The runtime Eve input screen should own rebinding here, but this scene has no active input surface to hand off to."));
            }
            else
            {
                children.Add(Text(
                    "aetheria.mainMenu.input.note",
                    "This title shell reports the typed player-settings state. Launch a run to open the runtime Eve input screen that owns low-level InputSystem rebinding."));
            }

            var buttons = new List<AetheriaRuntimeSurfaceComponent>();
            if (canOpenRuntimeInputScreen)
            {
                buttons.Add(Button(
                    "aetheria.mainMenu.input.openRuntimeScreen",
                    "Open Remap Screen",
                    AetheriaRuntimeMainMenuCommands.OpenRuntimeInputScreen));
            }

            buttons.Add(Button("aetheria.mainMenu.input.back", "Back", AetheriaRuntimeMainMenuCommands.BackToSettings));
            children.Add(ButtonColumn("aetheria.mainMenu.input.actions", buttons.ToArray()));

            return BuildSurfaceDocument(
                AetheriaRuntimeMainMenuCommands.InputSettingsSurfaceId,
                "Aetheria Input Settings",
                updatedAtUtc,
                version,
                commands,
                children.ToArray());
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

        private static string VerseLabel(string verseTitle, string verseId)
        {
            var title = string.IsNullOrWhiteSpace(verseTitle) ? "Unknown Verse" : verseTitle;
            var id = string.IsNullOrWhiteSpace(verseId) ? "unknown" : verseId;
            return string.Equals(title, id, StringComparison.Ordinal) ? title : $"{title} ({id})";
        }

        private static AetheriaRuntimeSurfaceDocument BuildSurfaceDocument(
            string surfaceId,
            string title,
            string updatedAtUtc,
            long version,
            IReadOnlyList<AetheriaRuntimeSurfaceCommandTemplate> commands,
            params AetheriaRuntimeSurfaceComponent[] children)
        {
            return new AetheriaRuntimeSurfaceDocument(
                providerId: "aetheria",
                providerKind: "game.menu",
                title: title,
                version: version,
                updatedAtUtc: updatedAtUtc,
                surface: new AetheriaRuntimeSurfaceTree(
                    surfaceId,
                    Node($"{surfaceId}.root", "surface", Array.Empty<(string Key, string Value)>(), children),
                    Array.Empty<AetheriaRuntimeSurfaceStyleToken>()),
                commands: commands);
        }

        private static AetheriaRuntimeSurfaceCommandTemplate Command(string command, string label)
        {
            return new AetheriaRuntimeSurfaceCommandTemplate(command, label, AetheriaRuntimeSurfaceCommandTemplate.CultMeshTransport);
        }

        private static AetheriaRuntimeSurfaceComponent Card(
            string id,
            string title,
            params AetheriaRuntimeSurfaceComponent[] children)
        {
            return Node(id, "card", new[] { ("title", title) }, children);
        }

        private static AetheriaRuntimeSurfaceComponent Metric(string id, string label, string value)
        {
            return Node(id, "metric", new[] { ("label", label), ("value", value ?? "") });
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
            EveSurfaceCommandRequest request,
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
