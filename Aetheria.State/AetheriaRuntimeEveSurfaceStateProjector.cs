using Aetheria.State.Documents;
using GameCult.Aetheria.State.Verse;

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
        var props = new Dictionary<string, string>(component.Props, StringComparer.Ordinal);
        AetheriaRuntimeSurfaceStateBindings.AddPointerProps(props, component.StateBindings);

        return new EveSurfaceComponent
        {
            Id = component.Id,
            Kind = component.Kind,
            Props = props,
            Children = component.Children.Select(ToComponent).ToArray(),
            StateBindings = component.StateBindings
                .Select(binding =>
                {
                    var record = GameCult.Mesh.CultMesh.StateBindingRecord(binding);
                    return new EveSurfaceStateBinding
                    {
                        TargetProp = record.TargetProp,
                        PointerId = record.PointerId,
                        SourceId = record.SourceId,
                        SchemaId = record.SchemaId,
                        RouteKind = record.RouteKind,
                        RouteDescription = record.RouteDescription
                    };
                })
                .ToArray()
        };
    }
}
