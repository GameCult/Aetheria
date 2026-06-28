using Aetheria.State.Documents;
using GameCult.Aetheria.State.Verse;
using GameCult.Mesh;

namespace Aetheria.State.Unity;

public sealed class AetheriaRuntimeCatalogClient : IAsyncDisposable, IDisposable
{
    private readonly AetheriaStateNode _node;

    private AetheriaRuntimeCatalogClient(AetheriaStateNode node)
    {
        _node = node;
    }

    public static async Task<AetheriaRuntimeCatalogClient> OpenAsync(string statePath)
    {
        var node = await AetheriaStateNode
            .OpenAsync(statePath, "aetheria-unity-runtime-catalog", enableDurableShardLogs: false)
            .ConfigureAwait(false);
        return new AetheriaRuntimeCatalogClient(node);
    }

    public AetheriaRuntimeCatalogSnapshot ReadCatalog()
    {
        return _node.RuntimeCatalog().Latest();
    }

    public async Task<EveSurfaceState?> ReadCatalogSurfaceAsync()
    {
        var surface = AetheriaRuntimeCatalogStore
            .ReadEveSurfaces(_node.StatePath)
            .FirstOrDefault(candidate => candidate.Surface.Id == AetheriaCatalogSurfaceProjector.SurfaceId);
        return surface == null ? null : ToState(surface);
    }

    private static EveSurfaceState ToState(global::GameCult.Eve.Surface.EveSurfaceDocument document)
    {
        return new EveSurfaceState
        {
            Type = document.Type,
            Schema = document.Schema,
            ProviderId = document.ProviderId,
            ProviderKind = document.ProviderKind,
            Title = document.Title,
            Version = document.Version,
            UpdatedAtUtc = document.UpdatedAtUtc,
            Surface = new EveSurface
            {
                Id = document.Surface.Id,
                Root = ToState(document.Surface.Root),
                Styles = document.Surface.Styles
                    .Select(style => new EveStyleToken { Name = style.Name, Value = style.Value })
                    .ToArray()
            },
            Commands = document.Commands
                .Select(command =>
                {
                    var record = CultMesh.OperationBindingRecord(command.Operation);
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

    private static EveSurfaceComponent ToState(global::GameCult.Eve.Surface.EveSurfaceComponent component)
    {
        return new EveSurfaceComponent
        {
            Id = component.Id,
            Kind = component.Kind,
            Props = new Dictionary<string, string>(component.Props, StringComparer.Ordinal),
            Children = component.Children.Select(ToState).ToArray(),
            StateBindings = component.StateBindings
                .Select(binding =>
                {
                    var record = CultMesh.StateBindingRecord(binding);
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

    public ValueTask DisposeAsync()
    {
        return _node.DisposeAsync();
    }

    public void Dispose()
    {
        _node.Dispose();
    }
}
