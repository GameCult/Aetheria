using Aetheria.State.Documents;

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
        var node = await AetheriaStateNode.OpenAsync(statePath, "aetheria-unity-runtime-catalog").ConfigureAwait(false);
        return new AetheriaRuntimeCatalogClient(node);
    }

    public AetheriaRuntimeCatalogSnapshot ReadCatalog()
    {
        var catalog = _node.ReadCatalogSnapshot();
        return AetheriaRuntimeCatalogSnapshot.FromCatalog(catalog);
    }

    public async Task<EveSurfaceState?> ReadCatalogSurfaceAsync()
    {
        return await _node.GetCatalogSurfaceAsync().ConfigureAwait(false);
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
