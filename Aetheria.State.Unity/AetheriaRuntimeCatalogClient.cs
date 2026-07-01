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

    public EveSurfaceDocument? ReadCatalogSurface()
    {
        return _node.CatalogSurface().Latest();
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
