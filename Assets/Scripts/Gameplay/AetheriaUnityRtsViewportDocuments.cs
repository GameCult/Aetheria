using System;
using GameCult.Aetheria.State.Verse;
using GameCult.Mesh;

public sealed class AetheriaUnityRtsViewportDocuments : IDisposable
{
    private AetheriaUnityRtsViewportDocuments(
        AetheriaClientState state,
        AetheriaRuntimeRtsViewportBounds viewport,
        bool bindContacts,
        bool bindObjects,
        bool bindRenderSplats)
    {
        if (state == null)
            throw new ArgumentNullException(nameof(state));
        if (viewport == null)
            throw new ArgumentNullException(nameof(viewport));

        Viewport = viewport;
        Contacts = bindContacts ? state.ReactiveZoneContacts() : null;
        ObjectsViewport = bindObjects ? state.ReactiveObjectsViewport(viewport) : null;
        RenderSplatsViewport = bindRenderSplats ? state.ReactiveRenderSplatsViewport(viewport) : null;
    }

    public AetheriaRuntimeRtsViewportBounds Viewport { get; }
    public CultMeshReactiveDocument<AetheriaRuntimeZoneContactsDocument> Contacts { get; }
    public CultMeshReactiveDocument<AetheriaRuntimeObjectsViewportDocument> ObjectsViewport { get; }
    public CultMeshReactiveDocument<AetheriaRuntimeRenderSplatsViewportDocument> RenderSplatsViewport { get; }

    public AetheriaRuntimeZoneContactsDocument CurrentContacts => Contacts?.Current;
    public AetheriaRuntimeObjectsViewportDocument CurrentObjectsViewport => ObjectsViewport?.Current;
    public AetheriaRuntimeRenderSplatsViewportDocument CurrentRenderSplatsViewport => RenderSplatsViewport?.Current;

    public static AetheriaUnityRtsViewportDocuments OpenMap(
        AetheriaClientState state,
        AetheriaRuntimeRtsViewportBounds viewport)
    {
        return new AetheriaUnityRtsViewportDocuments(
            state,
            viewport,
            bindContacts: false,
            bindObjects: true,
            bindRenderSplats: true);
    }

    public static AetheriaUnityRtsViewportDocuments OpenZonePresentation(
        AetheriaClientState state,
        AetheriaRuntimeRtsViewportBounds viewport)
    {
        return new AetheriaUnityRtsViewportDocuments(
            state,
            viewport,
            bindContacts: true,
            bindObjects: true,
            bindRenderSplats: false);
    }

    public static AetheriaUnityRtsViewportDocuments OpenRenderSplats(
        AetheriaClientState state,
        AetheriaRuntimeRtsViewportBounds viewport)
    {
        return new AetheriaUnityRtsViewportDocuments(
            state,
            viewport,
            bindContacts: false,
            bindObjects: false,
            bindRenderSplats: true);
    }

    public bool Matches(AetheriaRuntimeRtsViewportBounds viewport)
    {
        return SameViewport(Viewport, viewport);
    }

    public void Dispose()
    {
        Contacts?.Dispose();
        ObjectsViewport?.Dispose();
        RenderSplatsViewport?.Dispose();
    }

    public static bool SameViewport(
        AetheriaRuntimeRtsViewportBounds left,
        AetheriaRuntimeRtsViewportBounds right)
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
