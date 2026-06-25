using System;
using System.Collections.Generic;
using System.Linq;
using Aetheria.State.Documents;
using GameCult.Aetheria.State.Verse;

namespace Aetheria.State;

public static class AetheriaPlayerSettingsSurfaceProjector
{
    public const string SurfaceKey = "eve:surface:aetheria.player_settings";
    public const string SurfaceId = AetheriaRuntimePlayerSettingsCommands.SurfaceId;

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

        var surface = AetheriaRuntimePlayerSettingsSurfaceBuilder.Build(
            new AetheriaRuntimePlayerSettingsSurfaceState(
                settings.PlayerName,
                settings.TutorialPassed,
                settings.ActiveRunKey,
                gameplay.TemperatureUnit,
                gameplay.SignificantDigits,
                graphics.NebulaQuality,
                graphics.ShowAsteroidsInMinimap,
                publishedAtUtc),
            version);

        return new EveSurfaceState
        {
            ProviderId = surface.ProviderId,
            ProviderKind = surface.ProviderKind,
            Title = surface.Title,
            Version = surface.Version,
            UpdatedAtUtc = surface.UpdatedAtUtc,
            Surface = new EveSurface
            {
                Id = surface.Surface.Id,
                Root = ConvertComponent(surface.Surface.Root),
                Styles = surface.Surface.Styles
                    .Select(style => new EveStyleToken
                    {
                        Name = style.Name,
                        Value = style.Value
                    })
                    .ToArray()
            },
            Commands = surface.Commands
                .Select(command =>
                {
                    var record = GameCult.Mesh.CultMesh.OperationBindingRecord(command.Operation);
                    return new EveCommandTemplate
                    {
                        Command = record.OperationId,
                        Label = record.Label,
                        Transport = record.RouteDescription,
                        SchemaId = record.SchemaId,
                        RouteKind = record.RouteKind,
                        RouteDescription = record.RouteDescription
                    };
                })
                .ToArray()
        };
    }

    private static EveSurfaceComponent ConvertComponent(AetheriaRuntimeSurfaceComponent component)
    {
        return new EveSurfaceComponent
        {
            Id = component.Id,
            Kind = component.Kind,
            Props = new Dictionary<string, string>(component.Props, StringComparer.Ordinal),
            Children = component.Children.Select(ConvertComponent).ToArray()
        };
    }
}
