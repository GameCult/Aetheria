using Aetheria.State.Documents;
using GameCult.Mesh;

namespace Aetheria.State;

public sealed class AetheriaVerseDiscoveryHost : IDisposable
{
    private readonly AetheriaStateNode _node;
    private CultMeshVerseCatalog? _catalog;
    private CultMeshVerseDiscoveryServer? _server;
    private string _fingerprint = "";

    public AetheriaVerseDiscoveryHost(AetheriaStateNode node)
    {
        _node = node ?? throw new ArgumentNullException(nameof(node));
    }

    public void Update(AetheriaVerseHostSettings? settings)
    {
        var normalized = AetheriaVerseHostSettingsNormalizer.Normalize(settings);
        if (!string.Equals(normalized.Visibility, "public", StringComparison.OrdinalIgnoreCase))
        {
            DisposeServer();
            return;
        }

        var nextFingerprint = CreateFingerprint(normalized);
        if (string.Equals(_fingerprint, nextFingerprint, StringComparison.Ordinal))
            return;

        DisposeServer();

        _catalog = CultMesh.CreateVerseCatalog();
        _catalog.Upsert(AetheriaVerseCatalogDocuments.Build(normalized));
        _server = CultMesh.ServeVerseCatalog(_node.MeshNode, _catalog);
        _fingerprint = nextFingerprint;
    }

    public void Dispose()
    {
        DisposeServer();
        GC.SuppressFinalize(this);
    }

    private void DisposeServer()
    {
        _server?.Dispose();
        _server = null;
        _catalog?.Dispose();
        _catalog = null;
        _fingerprint = "";
    }

    private static string CreateFingerprint(AetheriaVerseHostSettings settings)
    {
        return string.Join(
            "\u001f",
            settings.ServiceId ?? "",
            settings.VerseId ?? "",
            settings.RootVerse ?? "",
            settings.CanonicalService ?? "",
            settings.LocatedService ?? "",
            settings.CultMeshAddress ?? "",
            settings.Title ?? "",
            settings.Visibility ?? "");
    }
}
