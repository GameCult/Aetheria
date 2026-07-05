using System;
using GameCult.Aetheria.State.Verse;
using GameCult.Mesh;

public sealed class AetheriaUnityGameViewportDocuments : IDisposable
{
    private readonly CultMeshReactiveDocument<AetheriaRuntimeZoneContactsDocument> _contacts;
    private readonly CultMeshReactiveDocument<AetheriaRuntimeObjectsViewportDocument> _objectsViewport;
    private readonly CultMeshReactiveDocument<AetheriaRuntimeRenderSplatsViewportDocument> _renderSplatsViewport;

    private AetheriaUnityGameViewportDocuments(
        AetheriaClientState state,
        AetheriaRuntimeViewportBounds viewport,
        bool bindContacts,
        bool bindObjects,
        bool bindRenderSplats)
    {
        if (state == null)
            throw new ArgumentNullException(nameof(state));
        if (viewport == null)
            throw new ArgumentNullException(nameof(viewport));

        Viewport = viewport;
        _contacts = bindContacts ? state.ZoneContacts.Reactive() : null;
        _objectsViewport = bindObjects ? state.ObjectsViewport(viewport).Reactive() : null;
        _renderSplatsViewport = bindRenderSplats ? state.RenderSplatsViewport(viewport).Reactive() : null;
    }

    public AetheriaRuntimeViewportBounds Viewport { get; }

    public AetheriaRuntimeZoneContactsDocument CurrentContacts => _contacts?.Current;
    public AetheriaRuntimeObjectsViewportDocument CurrentObjectsViewport => _objectsViewport?.Current;
    public AetheriaRuntimeRenderSplatsViewportDocument CurrentRenderSplatsViewport => _renderSplatsViewport?.Current;

    public static AetheriaUnityGameViewportDocuments OpenMap(
        AetheriaClientState state,
        AetheriaRuntimeViewportBounds viewport)
    {
        return new AetheriaUnityGameViewportDocuments(
            state,
            viewport,
            bindContacts: false,
            bindObjects: true,
            bindRenderSplats: true);
    }

    public static AetheriaUnityGameViewportDocuments OpenZonePresentation(
        AetheriaClientState state,
        AetheriaRuntimeViewportBounds viewport)
    {
        return new AetheriaUnityGameViewportDocuments(
            state,
            viewport,
            bindContacts: true,
            bindObjects: true,
            bindRenderSplats: false);
    }

    public static AetheriaUnityGameViewportDocuments OpenRenderSplats(
        AetheriaClientState state,
        AetheriaRuntimeViewportBounds viewport)
    {
        return new AetheriaUnityGameViewportDocuments(
            state,
            viewport,
            bindContacts: false,
            bindObjects: false,
            bindRenderSplats: true);
    }

    public bool Matches(AetheriaRuntimeViewportBounds viewport)
    {
        return SameViewport(Viewport, viewport);
    }

    public void Dispose()
    {
        _contacts?.Dispose();
        _objectsViewport?.Dispose();
        _renderSplatsViewport?.Dispose();
    }

    public static bool SameViewport(
        AetheriaRuntimeViewportBounds left,
        AetheriaRuntimeViewportBounds right)
    {
        if (left == null || right == null)
            return false;

        return Approximately(left.MinX, right.MinX) &&
            Approximately(left.MinY, right.MinY) &&
            Approximately(left.MaxX, right.MaxX) &&
            Approximately(left.MaxY, right.MaxY);
    }

    private static bool Approximately(double left, double right)
    {
        var scale = Math.Max(1.0, Math.Max(Math.Abs(left), Math.Abs(right)));
        return Math.Abs(left - right) <= scale * 1e-6;
    }
}
