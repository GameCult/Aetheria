using Aetheria.State.Documents;
using GameCult.Aetheria.State.Unity;

namespace Aetheria.State;

public static class AetheriaRuntimeEveSurfaceStateProjector
{
    public static EveSurfaceState ToState(AetheriaRuntimeSurfaceDocument document)
    {
        return new EveSurfaceState
        {
            ProviderId = document.ProviderId,
            ProviderKind = document.ProviderKind,
            Title = document.Title,
            Version = document.Version,
            UpdatedAtUtc = document.UpdatedAtUtc,
            Surface = ToSurface(document.Surface),
            Commands = document.Commands
                .Select(command => new EveCommandTemplate
                {
                    Command = command.Command,
                    Label = command.Label,
                    Transport = command.Transport
                })
                .ToArray()
        };
    }

    private static EveSurface ToSurface(AetheriaRuntimeSurfaceTree surface)
    {
        return new EveSurface
        {
            Id = surface.Id,
            Root = ToComponent(surface.Root),
            Styles = surface.Styles
                .Select(style => new EveStyleToken
                {
                    Name = style.Name,
                    Value = style.Value
                })
                .ToArray()
        };
    }

    private static EveSurfaceComponent ToComponent(AetheriaRuntimeSurfaceComponent component)
    {
        return new EveSurfaceComponent
        {
            Id = component.Id,
            Kind = component.Kind,
            Props = new Dictionary<string, string>(component.Props, StringComparer.Ordinal),
            Children = component.Children.Select(ToComponent).ToArray()
        };
    }
}
