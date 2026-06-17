using System;
using System.Collections.Generic;
using System.Linq;

namespace GameCult.Aetheria.State.Unity
{
    public sealed class AetheriaRuntimeSurfaceDocument
    {
        public AetheriaRuntimeSurfaceDocument(
            string providerId,
            string providerKind,
            string title,
            long version,
            string updatedAtUtc,
            AetheriaRuntimeSurfaceTree surface,
            IReadOnlyList<AetheriaRuntimeSurfaceCommandTemplate> commands)
        {
            ProviderId = providerId ?? "";
            ProviderKind = providerKind ?? "";
            Title = title ?? "";
            Version = version;
            UpdatedAtUtc = updatedAtUtc ?? "";
            Surface = surface ?? throw new ArgumentNullException(nameof(surface));
            Commands = commands ?? Array.Empty<AetheriaRuntimeSurfaceCommandTemplate>();
        }

        public string ProviderId { get; }

        public string ProviderKind { get; }

        public string Title { get; }

        public long Version { get; }

        public string UpdatedAtUtc { get; }

        public AetheriaRuntimeSurfaceTree Surface { get; }

        public IReadOnlyList<AetheriaRuntimeSurfaceCommandTemplate> Commands { get; }
    }

    public sealed class AetheriaRuntimeSurfaceTree
    {
        public AetheriaRuntimeSurfaceTree(
            string id,
            AetheriaRuntimeSurfaceComponent root,
            IReadOnlyList<AetheriaRuntimeSurfaceStyleToken> styles)
        {
            Id = id ?? "";
            Root = root ?? throw new ArgumentNullException(nameof(root));
            Styles = styles ?? Array.Empty<AetheriaRuntimeSurfaceStyleToken>();
        }

        public string Id { get; }

        public AetheriaRuntimeSurfaceComponent Root { get; }

        public IReadOnlyList<AetheriaRuntimeSurfaceStyleToken> Styles { get; }
    }

    public sealed class AetheriaRuntimeSurfaceComponent
    {
        public AetheriaRuntimeSurfaceComponent(
            string id,
            string kind,
            IReadOnlyDictionary<string, string> props,
            IReadOnlyList<AetheriaRuntimeSurfaceComponent> children)
        {
            Id = id ?? "";
            Kind = kind ?? "";
            Props = props ?? new Dictionary<string, string>(StringComparer.Ordinal);
            Children = children ?? Array.Empty<AetheriaRuntimeSurfaceComponent>();
        }

        public string Id { get; }

        public string Kind { get; }

        public IReadOnlyDictionary<string, string> Props { get; }

        public IReadOnlyList<AetheriaRuntimeSurfaceComponent> Children { get; }
    }

    public sealed class AetheriaRuntimeSurfaceStyleToken
    {
        public AetheriaRuntimeSurfaceStyleToken(string name, string value)
        {
            Name = name ?? "";
            Value = value ?? "";
        }

        public string Name { get; }

        public string Value { get; }
    }

    public sealed class AetheriaRuntimeSurfaceCommandTemplate
    {
        public AetheriaRuntimeSurfaceCommandTemplate(string command, string label, string transport)
        {
            Command = command ?? "";
            Label = label ?? "";
            Transport = transport ?? "";
        }

        public string Command { get; }

        public string Label { get; }

        public string Transport { get; }
    }

    public sealed class AetheriaRuntimePlayerSettingsSurfaceState
    {
        public AetheriaRuntimePlayerSettingsSurfaceState(
            string playerName,
            bool tutorialPassed,
            string activeRunKey,
            string temperatureUnit,
            int significantDigits,
            string nebulaQuality,
            bool showAsteroidsInMinimap,
            string updatedAtUtc)
        {
            PlayerName = playerName ?? "";
            TutorialPassed = tutorialPassed;
            ActiveRunKey = activeRunKey ?? "";
            TemperatureUnit = temperatureUnit ?? "";
            SignificantDigits = significantDigits;
            NebulaQuality = nebulaQuality ?? "";
            ShowAsteroidsInMinimap = showAsteroidsInMinimap;
            UpdatedAtUtc = updatedAtUtc ?? "";
        }

        public string PlayerName { get; }

        public bool TutorialPassed { get; }

        public string ActiveRunKey { get; }

        public string TemperatureUnit { get; }

        public int SignificantDigits { get; }

        public string NebulaQuality { get; }

        public bool ShowAsteroidsInMinimap { get; }

        public string UpdatedAtUtc { get; }
    }

    public static class AetheriaRuntimePlayerSettingsSurfaceBuilder
    {
        public static AetheriaRuntimeSurfaceDocument Build(
            AetheriaRuntimePlayerSettingsSurfaceState settings,
            long version = 1)
        {
            settings ??= new AetheriaRuntimePlayerSettingsSurfaceState(
                "",
                tutorialPassed: false,
                activeRunKey: "",
                temperatureUnit: "",
                significantDigits: 0,
                nebulaQuality: "",
                showAsteroidsInMinimap: false,
                updatedAtUtc: "");

            return new AetheriaRuntimeSurfaceDocument(
                providerId: "aetheria",
                providerKind: "game.runtime",
                title: "Aetheria Player Settings",
                version: version,
                updatedAtUtc: settings.UpdatedAtUtc,
                surface: new AetheriaRuntimeSurfaceTree(
                    AetheriaRuntimePlayerSettingsCommands.SurfaceId,
                    Node(
                        "aetheria.playerSettings.root",
                        "surface",
                        Array.Empty<(string Key, string Value)>(),
                        Node(
                            "aetheria.playerSettings.summary",
                            "card",
                            new[] { ("title", "Player Settings") },
                            TextInput(
                                "playerSettings.summary.playerName",
                                "Name",
                                settings.PlayerName,
                                AetheriaRuntimePlayerSettingsCommands.SetPlayerName),
                            Row(
                                "playerSettings.summary.values",
                                ("tutorialPassed", settings.TutorialPassed ? "Yes" : "No"),
                                ("activeRun", settings.ActiveRunKey)),
                            Text(
                                "playerSettings.summary.note",
                                "Input remapping lowers through the runtime Eve input screen and queues typed player-settings commits.")),
                        Node(
                            "aetheria.playerSettings.gameplay",
                            "card",
                            new[] { ("title", "Gameplay") },
                            Metric("playerSettings.gameplay.temperatureUnit", "Temperature Unit", settings.TemperatureUnit),
                            ButtonRow(
                                "playerSettings.gameplay.temperatureUnit.buttons",
                                Button(
                                    "playerSettings.gameplay.temperatureUnit.cycle",
                                    "Cycle Temperature Unit",
                                    AetheriaRuntimePlayerSettingsCommands.CycleTemperatureUnit)),
                            Metric(
                                "playerSettings.gameplay.significantDigits",
                                "Significant Digits",
                                settings.SignificantDigits.ToString()),
                            ButtonRow(
                                "playerSettings.gameplay.significantDigits.buttons",
                                Button(
                                    "playerSettings.gameplay.significantDigits.decrement",
                                    "Digits -",
                                    AetheriaRuntimePlayerSettingsCommands.DecrementSignificantDigits),
                                Button(
                                    "playerSettings.gameplay.significantDigits.increment",
                                    "Digits +",
                                    AetheriaRuntimePlayerSettingsCommands.IncrementSignificantDigits))),
                        Node(
                            "aetheria.playerSettings.graphics",
                            "card",
                            new[] { ("title", "Graphics") },
                            Metric("playerSettings.graphics.nebulaQuality", "Nebula Quality", settings.NebulaQuality),
                            ButtonRow(
                                "playerSettings.graphics.nebulaQuality.buttons",
                                Button(
                                    "playerSettings.graphics.nebulaQuality.cycle",
                                    "Cycle Nebula Quality",
                                    AetheriaRuntimePlayerSettingsCommands.CycleNebulaQuality)),
                            Metric(
                                "playerSettings.graphics.showAsteroids",
                                "Show Asteroids In Minimap",
                                settings.ShowAsteroidsInMinimap ? "Enabled" : "Disabled"),
                            ButtonRow(
                                "playerSettings.graphics.showAsteroids.buttons",
                                Button(
                                    "playerSettings.graphics.showAsteroids.toggle",
                                    settings.ShowAsteroidsInMinimap ? "Disable Minimap Asteroids" : "Enable Minimap Asteroids",
                                    AetheriaRuntimePlayerSettingsCommands.ToggleShowAsteroidsInMinimap)))),
                    Array.Empty<AetheriaRuntimeSurfaceStyleToken>()),
                commands: new[]
                {
                    new AetheriaRuntimeSurfaceCommandTemplate(
                        AetheriaRuntimePlayerSettingsCommands.Refresh,
                        "Refresh",
                        "cultmesh"),
                    new AetheriaRuntimeSurfaceCommandTemplate(
                        AetheriaRuntimePlayerSettingsCommands.SetPlayerName,
                        "Set Player Name",
                        "cultmesh"),
                    new AetheriaRuntimeSurfaceCommandTemplate(
                        AetheriaRuntimePlayerSettingsCommands.CycleTemperatureUnit,
                        "Cycle Temperature Unit",
                        "cultmesh"),
                    new AetheriaRuntimeSurfaceCommandTemplate(
                        AetheriaRuntimePlayerSettingsCommands.DecrementSignificantDigits,
                        "Digits -",
                        "cultmesh"),
                    new AetheriaRuntimeSurfaceCommandTemplate(
                        AetheriaRuntimePlayerSettingsCommands.IncrementSignificantDigits,
                        "Digits +",
                        "cultmesh"),
                    new AetheriaRuntimeSurfaceCommandTemplate(
                        AetheriaRuntimePlayerSettingsCommands.CycleNebulaQuality,
                        "Cycle Nebula Quality",
                        "cultmesh"),
                    new AetheriaRuntimeSurfaceCommandTemplate(
                        AetheriaRuntimePlayerSettingsCommands.ToggleShowAsteroidsInMinimap,
                        "Toggle Minimap Asteroids",
                        "cultmesh")
                });
        }

        private static AetheriaRuntimeSurfaceComponent Metric(string id, string label, string value)
        {
            return Node(id, "metric", new[] { ("label", label), ("value", value) });
        }

        private static AetheriaRuntimeSurfaceComponent Text(string id, string value)
        {
            return Node(id, "text", new[] { ("value", value) });
        }

        private static AetheriaRuntimeSurfaceComponent Button(string id, string label, string command)
        {
            return Node(id, "control.button", new[] { ("label", label), ("command", command) });
        }

        private static AetheriaRuntimeSurfaceComponent TextInput(string id, string label, string value, string command)
        {
            return Node(id, "control.text", new[] { ("label", label), ("value", value), ("command", command) });
        }

        private static AetheriaRuntimeSurfaceComponent ButtonRow(
            string id,
            params AetheriaRuntimeSurfaceComponent[] children)
        {
            return Node(id, "row", Array.Empty<(string Key, string Value)>(), children);
        }

        private static AetheriaRuntimeSurfaceComponent Row(
            string id,
            params (string Key, string Value)[] props)
        {
            return Node(id, "row", props);
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
                props.ToDictionary(prop => prop.Key, prop => prop.Value, StringComparer.Ordinal),
                children ?? Array.Empty<AetheriaRuntimeSurfaceComponent>());
        }
    }
}
