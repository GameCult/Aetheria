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

    public sealed class AetheriaRuntimeMainMenuSurfaceState
    {
        public AetheriaRuntimeMainMenuSurfaceState(
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
            int bindingOverrideCount,
            int actionBarInputCount,
            bool canOpenRuntimeInputScreen,
            string updatedAtUtc)
        {
            TargetLabel = targetLabel ?? "";
            TargetKind = targetKind ?? "";
            TargetSource = targetSource ?? "";
            VerseTitle = verseTitle ?? "";
            VerseId = verseId ?? "";
            VerseVisibility = verseVisibility ?? "";
            VerseCultMeshAddress = verseCultMeshAddress ?? "";
            InGame = inGame;
            HasAuthoritativeDaemonFrame = hasAuthoritativeDaemonFrame;
            DaemonRunId = daemonRunId ?? "";
            DaemonFrameId = daemonFrameId;
            BindingOverrideCount = bindingOverrideCount;
            ActionBarInputCount = actionBarInputCount;
            CanOpenRuntimeInputScreen = canOpenRuntimeInputScreen;
            UpdatedAtUtc = updatedAtUtc ?? "";
        }

        public string TargetLabel { get; }
        public string TargetKind { get; }
        public string TargetSource { get; }
        public string VerseTitle { get; }
        public string VerseId { get; }
        public string VerseVisibility { get; }
        public string VerseCultMeshAddress { get; }
        public bool InGame { get; }
        public bool HasAuthoritativeDaemonFrame { get; }
        public string DaemonRunId { get; }
        public long DaemonFrameId { get; }
        public int BindingOverrideCount { get; }
        public int ActionBarInputCount { get; }
        public bool CanOpenRuntimeInputScreen { get; }
        public string UpdatedAtUtc { get; }

        public string VerseLabel
        {
            get
            {
                var title = string.IsNullOrWhiteSpace(VerseTitle) ? "Unknown Verse" : VerseTitle;
                var verseId = string.IsNullOrWhiteSpace(VerseId) ? "unknown" : VerseId;
                return string.Equals(title, verseId, StringComparison.Ordinal) ? title : $"{title} ({verseId})";
            }
        }
    }

    public static class AetheriaRuntimeMainMenuSurfaceBuilder
    {
        public static AetheriaRuntimeMainMenuSurfaceState ProjectRoot(
            AetheriaRuntimeStateBootReport stateBoot,
            AetheriaRuntimeDaemonFrameDocument daemonFrame,
            AetheriaRuntimeVerseHostSettingsSnapshot verseHost,
            AetheriaRuntimePlayerSettingsSnapshot playerSettings,
            bool canOpenRuntimeInputScreen,
            bool inGame,
            string updatedAtUtc)
        {
            return new AetheriaRuntimeMainMenuSurfaceState(
                stateBoot.TargetLabel,
                stateBoot.TargetKind,
                stateBoot.TargetSource,
                verseHost?.Title ?? stateBoot.Title,
                verseHost?.VerseId ?? stateBoot.VerseId,
                verseHost?.Visibility ?? "unknown",
                verseHost?.CultMeshAddress ?? stateBoot.CultMeshAddress,
                inGame,
                daemonFrame != null,
                daemonFrame?.Run?.RunId ?? "",
                daemonFrame?.FrameId ?? -1,
                playerSettings?.BindingOverrides?.Count ?? 0,
                playerSettings?.ActionBarInputs?.Count ?? 0,
                canOpenRuntimeInputScreen,
                updatedAtUtc);
        }

        public static AetheriaRuntimeMainMenuSurfaceState ProjectRoot(
            AetheriaRuntimeStateBootReport stateBoot,
            AetheriaRuntimeSectorMapDocument sectorMap,
            AetheriaRuntimeVerseHostSettingsSnapshot verseHost,
            AetheriaRuntimePlayerSettingsSnapshot playerSettings,
            bool canOpenRuntimeInputScreen,
            bool inGame,
            string updatedAtUtc)
        {
            return new AetheriaRuntimeMainMenuSurfaceState(
                stateBoot.TargetLabel,
                stateBoot.TargetKind,
                stateBoot.TargetSource,
                verseHost?.Title ?? stateBoot.Title,
                verseHost?.VerseId ?? stateBoot.VerseId,
                verseHost?.Visibility ?? "unknown",
                verseHost?.CultMeshAddress ?? stateBoot.CultMeshAddress,
                inGame,
                sectorMap != null,
                sectorMap?.RunId ?? "",
                sectorMap?.FrameId ?? -1,
                playerSettings?.BindingOverrides?.Count ?? 0,
                playerSettings?.ActionBarInputs?.Count ?? 0,
                canOpenRuntimeInputScreen,
                updatedAtUtc);
        }

        public static AetheriaRuntimePlayerSettingsSurfaceState ProjectPlayerSettings(
            AetheriaRuntimePlayerSettingsSnapshot playerSettings,
            string updatedAtUtc)
        {
            return new AetheriaRuntimePlayerSettingsSurfaceState(
                playerSettings?.PlayerName ?? "",
                playerSettings?.TutorialPassed ?? false,
                "",
                playerSettings?.TemperatureUnit ?? "",
                Math.Max(0, playerSettings?.SignificantDigits ?? 0),
                playerSettings?.NebulaQuality ?? "",
                playerSettings?.ShowAsteroidsInMinimap ?? false,
                updatedAtUtc);
        }

        public static AetheriaRuntimeClientTargetSurfaceState ProjectVerseSettings(
            AetheriaRuntimeStateBootReport stateBoot,
            AetheriaRuntimeVerseHostSettingsSnapshot verseHost,
            string updatedAtUtc)
        {
            return new AetheriaRuntimeClientTargetSurfaceState(
                stateBoot.TargetKind,
                stateBoot.Title,
                stateBoot.VerseId,
                stateBoot.RuntimeId,
                stateBoot.CultMeshAddress,
                stateBoot.StateFilePath,
                stateBoot.ReplicaStateFilePath,
                string.Join(", ", stateBoot.DiscoveryEndpoints ?? Array.Empty<string>()),
                stateBoot.DiscoveredVerses ?? Array.Empty<AetheriaRuntimeDiscoveredVerse>(),
                stateBoot.LastDiscoveryAtUtc,
                stateBoot.LastDiscoveryError,
                stateBoot.LastReplicaSyncAtUtc,
                stateBoot.LastReplicaSyncError,
                stateBoot.TargetSource,
                stateBoot.SupportsLocalStateFileRead,
                stateBoot.FailureMessage,
                verseHost?.Title ?? stateBoot.Title,
                verseHost?.VerseId ?? stateBoot.VerseId,
                verseHost?.Visibility ?? "unknown",
                verseHost?.CultMeshAddress ?? stateBoot.CultMeshAddress,
                updatedAtUtc);
        }

        public static AetheriaRuntimeSurfaceDocument BuildRoot(
            AetheriaRuntimeMainMenuSurfaceState state,
            long version = 1)
        {
            state ??= EmptyState();

            var commands = new List<AetheriaRuntimeSurfaceCommandTemplate>();
            var actionButtons = new List<AetheriaRuntimeSurfaceComponent>();
            var cardChildren = new List<AetheriaRuntimeSurfaceComponent>();
            var targetKind = string.IsNullOrWhiteSpace(state.TargetKind) ? "unknown" : state.TargetKind;
            var targetSource = string.IsNullOrWhiteSpace(state.TargetSource) ? "unknown" : state.TargetSource;
            var verseVisibility = string.IsNullOrWhiteSpace(state.VerseVisibility) ? "unknown" : state.VerseVisibility;
            var verseMeshAddress = string.IsNullOrWhiteSpace(state.VerseCultMeshAddress) ? "unpublished" : state.VerseCultMeshAddress;

            if (!state.InGame)
            {
                if (!state.HasAuthoritativeDaemonFrame)
                {
                    cardChildren.Add(Text(
                        $"{AetheriaRuntimeMainMenuCommands.RootSurfaceId}.continue.note",
                        $"No authoritative daemon frame is available in {state.VerseLabel} yet. Start or connect a daemon before opening the observer scene."));
                }
                else
                {
                    commands.Add(Command(AetheriaRuntimeMainMenuCommands.ContinueRun, "Continue"));
                    actionButtons.Add(Button(
                        $"{AetheriaRuntimeMainMenuCommands.RootSurfaceId}.continue",
                        "Continue",
                        AetheriaRuntimeMainMenuCommands.ContinueRun));
                    cardChildren.Add(Metric(
                        $"{AetheriaRuntimeMainMenuCommands.RootSurfaceId}.continue.run",
                        "Daemon Run",
                        state.DaemonRunId));
                    cardChildren.Add(Metric(
                        $"{AetheriaRuntimeMainMenuCommands.RootSurfaceId}.continue.frame",
                        "Daemon Frame",
                        state.DaemonFrameId.ToString()));
                }
            }

            cardChildren.Add(Metric(
                $"{AetheriaRuntimeMainMenuCommands.RootSurfaceId}.target.title",
                "Client Target",
                state.TargetLabel));
            cardChildren.Add(Metric(
                $"{AetheriaRuntimeMainMenuCommands.RootSurfaceId}.target.transport",
                "Transport",
                targetKind));
            cardChildren.Add(Metric(
                $"{AetheriaRuntimeMainMenuCommands.RootSurfaceId}.target.source",
                "Target Source",
                targetSource));
            cardChildren.Add(Metric(
                $"{AetheriaRuntimeMainMenuCommands.RootSurfaceId}.verse.title",
                "Verse",
                state.VerseLabel));
            cardChildren.Add(Metric(
                $"{AetheriaRuntimeMainMenuCommands.RootSurfaceId}.verse.visibility",
                "Visibility",
                verseVisibility));
            cardChildren.Add(Metric(
                $"{AetheriaRuntimeMainMenuCommands.RootSurfaceId}.verse.mesh",
                "CultMesh",
                verseMeshAddress));

            commands.Add(Command(AetheriaRuntimeMainMenuCommands.NewGame, "New Game"));
            commands.Add(Command(AetheriaRuntimeMainMenuCommands.ShowSettings, "Settings"));
            commands.Add(Command(AetheriaRuntimeMainMenuCommands.Quit, "Quit"));

            actionButtons.Add(Button($"{AetheriaRuntimeMainMenuCommands.RootSurfaceId}.newGame", "New Game", AetheriaRuntimeMainMenuCommands.NewGame));
            actionButtons.Add(Button($"{AetheriaRuntimeMainMenuCommands.RootSurfaceId}.settings", "Settings", AetheriaRuntimeMainMenuCommands.ShowSettings));
            actionButtons.Add(Button($"{AetheriaRuntimeMainMenuCommands.RootSurfaceId}.quit", "Quit", AetheriaRuntimeMainMenuCommands.Quit));

            cardChildren.Add(Text(
                $"{AetheriaRuntimeMainMenuCommands.RootSurfaceId}.note",
                $"The client lowers this shell through Eve. The client target chooses which Verse it follows; game truth belongs to the daemon serving {state.VerseLabel}."));
            cardChildren.Add(ButtonColumn($"{AetheriaRuntimeMainMenuCommands.RootSurfaceId}.actions", actionButtons.ToArray()));

            return BuildSurfaceDocument(
                AetheriaRuntimeMainMenuCommands.RootSurfaceId,
                "Aetheria Terminus",
                state.UpdatedAtUtc,
                version,
                commands,
                Card(
                    $"{AetheriaRuntimeMainMenuCommands.RootSurfaceId}.card",
                    "Aetheria Terminus",
                    cardChildren.ToArray()));
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
                Card(
                    "aetheria.mainMenu.settings.card",
                    "Settings",
                    Text(
                        "aetheria.mainMenu.settings.note",
                        "Player settings, client target selection, and input rebinding all lower through typed Eve surfaces. Audio still has no typed owner."),
                    ButtonRow(
                        "aetheria.mainMenu.settings.actions",
                        Button("aetheria.mainMenu.settings.playerSettings", "Player Settings", AetheriaRuntimeMainMenuCommands.ShowPlayerSettings),
                        Button("aetheria.mainMenu.settings.verse", "Verse", AetheriaRuntimeMainMenuCommands.ShowVerseSettings),
                        Button("aetheria.mainMenu.settings.input", "Input", AetheriaRuntimeMainMenuCommands.ShowInputSettings),
                        Button("aetheria.mainMenu.settings.back", "Back", AetheriaRuntimeMainMenuCommands.BackToMain))));
        }

        public static AetheriaRuntimeSurfaceDocument BuildInputSettings(
            AetheriaRuntimeMainMenuSurfaceState state,
            long version = 1)
        {
            state ??= EmptyState();

            var commands = new List<AetheriaRuntimeSurfaceCommandTemplate>
            {
                Command(AetheriaRuntimeMainMenuCommands.BackToSettings, "Back")
            };

            var cardChildren = new List<AetheriaRuntimeSurfaceComponent>
            {
                Metric(
                    "aetheria.mainMenu.input.bindingOverrides",
                    "Binding Overrides",
                    state.BindingOverrideCount.ToString()),
                Metric(
                    "aetheria.mainMenu.input.actionBarInputs",
                    "Action-Bar Inputs",
                    state.ActionBarInputCount.ToString())
            };

            if (state.CanOpenRuntimeInputScreen)
            {
                commands.Insert(0, Command(AetheriaRuntimeMainMenuCommands.OpenRuntimeInputScreen, "Open Remap Screen"));
                cardChildren.Add(Text(
                    "aetheria.mainMenu.input.note",
                    "The runtime Eve input screen owns low-level InputSystem rebinding and action-bar input edits. This title shell reports typed player-settings state and hands off to that owner."));
            }
            else if (state.InGame)
            {
                cardChildren.Add(Text(
                    "aetheria.mainMenu.input.note",
                    "The runtime Eve input screen should own rebinding here, but this scene has no active input surface to hand off to."));
            }
            else
            {
                cardChildren.Add(Text(
                    "aetheria.mainMenu.input.note",
                    "This title shell reports the typed player-settings state. Launch a run to open the runtime Eve input screen that owns low-level InputSystem rebinding."));
            }

            var buttons = new List<AetheriaRuntimeSurfaceComponent>();
            if (state.CanOpenRuntimeInputScreen)
            {
                buttons.Add(Button(
                    "aetheria.mainMenu.input.openRuntimeScreen",
                    "Open Remap Screen",
                    AetheriaRuntimeMainMenuCommands.OpenRuntimeInputScreen));
            }

            buttons.Add(Button("aetheria.mainMenu.input.back", "Back", AetheriaRuntimeMainMenuCommands.BackToSettings));
            cardChildren.Add(ButtonRow("aetheria.mainMenu.input.actions", buttons.ToArray()));

            return BuildSurfaceDocument(
                AetheriaRuntimeMainMenuCommands.InputSettingsSurfaceId,
                "Aetheria Input Settings",
                state.UpdatedAtUtc,
                version,
                commands,
                Card(
                    "aetheria.mainMenu.input.card",
                    "Input Settings",
                    cardChildren.ToArray()));
        }

        public static AetheriaRuntimeSurfaceDocument BuildPlayerSettingsShell(
            AetheriaRuntimePlayerSettingsSurfaceState state,
            long version = 1)
        {
            return WithBackAction(
                AetheriaRuntimePlayerSettingsSurfaceBuilder.Build(state, version),
                AetheriaRuntimeMainMenuCommands.PlayerSettingsShellSurfaceId,
                AetheriaRuntimeMainMenuCommands.BackToSettings,
                "Back");
        }

        public static AetheriaRuntimeSurfaceDocument BuildVerseSettingsShell(
            AetheriaRuntimeClientTargetSurfaceState state,
            long version = 1)
        {
            return WithBackAction(
                AetheriaRuntimeClientTargetSurfaceBuilder.Build(state, version),
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

        private static AetheriaRuntimeMainMenuSurfaceState EmptyState()
        {
            return new AetheriaRuntimeMainMenuSurfaceState(
                "",
                "",
                "",
                "",
                "",
                "",
                "",
                inGame: false,
                hasAuthoritativeDaemonFrame: false,
                daemonRunId: "",
                daemonFrameId: -1,
                bindingOverrideCount: 0,
                actionBarInputCount: 0,
                canOpenRuntimeInputScreen: false,
                updatedAtUtc: "");
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
            return Node(id, "text", new[] { ("value", value ?? "") });
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
