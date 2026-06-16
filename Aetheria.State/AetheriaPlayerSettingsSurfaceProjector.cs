using System.Linq;
using Aetheria.State.Documents;

namespace Aetheria.State;

public static class AetheriaPlayerSettingsSurfaceProjector
{
    public const string SurfaceKey = "eve:surface:aetheria.player_settings";
    public const string SurfaceId = "aetheria.player_settings";

    public static EveSurfaceState Build(
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

        return new EveSurfaceState
        {
            ProviderId = "aetheria",
            ProviderKind = "game.runtime",
            Title = "Aetheria Player Settings",
            Version = version,
            UpdatedAtUtc = publishedAtUtc,
            Surface = new EveSurface
            {
                Id = SurfaceId,
                Root = Node(
                    "aetheria.playerSettings.root",
                    "surface",
                    [],
                    Node(
                        "aetheria.playerSettings.summary",
                        "card",
                        [("title", "Player Settings")],
                        Row(
                            "playerSettings.summary.values",
                            ("playerName", settings.PlayerName),
                            ("tutorialPassed", settings.TutorialPassed ? "Yes" : "No"),
                            ("activeRun", settings.ActiveRunKey)),
                        Text(
                            "playerSettings.summary.note",
                            "Input remapping still lives on the runtime-owned screen until Eve grows typed rebinding controls.")),
                    Node(
                        "aetheria.playerSettings.gameplay",
                        "card",
                        [("title", "Gameplay")],
                        Metric("playerSettings.gameplay.temperatureUnit", "Temperature Unit", gameplay.TemperatureUnit),
                        ButtonRow(
                            "playerSettings.gameplay.temperatureUnit.buttons",
                            Button(
                                "playerSettings.gameplay.temperatureUnit.cycle",
                                "Cycle Temperature Unit",
                                "aetheria.player_settings.gameplay.temperature_unit.cycle")),
                        Metric(
                            "playerSettings.gameplay.significantDigits",
                            "Significant Digits",
                            gameplay.SignificantDigits.ToString()),
                        ButtonRow(
                            "playerSettings.gameplay.significantDigits.buttons",
                            Button(
                                "playerSettings.gameplay.significantDigits.decrement",
                                "Digits -",
                                "aetheria.player_settings.gameplay.significant_digits.decrement"),
                            Button(
                                "playerSettings.gameplay.significantDigits.increment",
                                "Digits +",
                                "aetheria.player_settings.gameplay.significant_digits.increment"))),
                    Node(
                        "aetheria.playerSettings.graphics",
                        "card",
                        [("title", "Graphics")],
                        Metric("playerSettings.graphics.nebulaQuality", "Nebula Quality", graphics.NebulaQuality),
                        ButtonRow(
                            "playerSettings.graphics.nebulaQuality.buttons",
                            Button(
                                "playerSettings.graphics.nebulaQuality.cycle",
                                "Cycle Nebula Quality",
                                "aetheria.player_settings.graphics.nebula_quality.cycle")),
                        Metric(
                            "playerSettings.graphics.showAsteroids",
                            "Show Asteroids In Minimap",
                            graphics.ShowAsteroidsInMinimap ? "Enabled" : "Disabled"),
                        ButtonRow(
                            "playerSettings.graphics.showAsteroids.buttons",
                            Button(
                                "playerSettings.graphics.showAsteroids.toggle",
                                graphics.ShowAsteroidsInMinimap ? "Disable Minimap Asteroids" : "Enable Minimap Asteroids",
                                "aetheria.player_settings.graphics.show_asteroids.toggle"))))
            },
            Commands =
            [
                new EveCommandTemplate
                {
                    Command = "aetheria.player_settings.refresh",
                    Label = "Refresh",
                    Transport = "cultmesh"
                },
                new EveCommandTemplate
                {
                    Command = "aetheria.player_settings.gameplay.temperature_unit.cycle",
                    Label = "Cycle Temperature Unit",
                    Transport = "cultmesh"
                },
                new EveCommandTemplate
                {
                    Command = "aetheria.player_settings.gameplay.significant_digits.decrement",
                    Label = "Digits -",
                    Transport = "cultmesh"
                },
                new EveCommandTemplate
                {
                    Command = "aetheria.player_settings.gameplay.significant_digits.increment",
                    Label = "Digits +",
                    Transport = "cultmesh"
                },
                new EveCommandTemplate
                {
                    Command = "aetheria.player_settings.graphics.nebula_quality.cycle",
                    Label = "Cycle Nebula Quality",
                    Transport = "cultmesh"
                },
                new EveCommandTemplate
                {
                    Command = "aetheria.player_settings.graphics.show_asteroids.toggle",
                    Label = "Toggle Minimap Asteroids",
                    Transport = "cultmesh"
                }
            ]
        };
    }

    private static EveSurfaceComponent Metric(string id, string label, string value)
    {
        return Node(id, "metric", [("label", label), ("value", value)]);
    }

    private static EveSurfaceComponent Text(string id, string value)
    {
        return Node(id, "text", [("value", value)]);
    }

    private static EveSurfaceComponent Button(string id, string label, string command)
    {
        return Node(id, "control.button", [("label", label), ("command", command)]);
    }

    private static EveSurfaceComponent ButtonRow(string id, params EveSurfaceComponent[] children)
    {
        return Node(id, "row", [], children);
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
        return new EveSurfaceComponent
        {
            Id = id,
            Kind = kind,
            Props = props.ToDictionary(prop => prop.Key, prop => prop.Value),
            Children = children
        };
    }
}
