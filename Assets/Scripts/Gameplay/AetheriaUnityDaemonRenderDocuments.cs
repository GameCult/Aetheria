using System;
using GameCult.Aetheria.State.Verse;
using GameCult.Mesh;

public sealed class AetheriaUnityDaemonRenderDocuments : IDisposable
{
    private AetheriaUnityDaemonRenderDocuments(AetheriaClientState state)
    {
        if (state == null)
            throw new ArgumentNullException(nameof(state));

        Frame = state.ReactiveDaemonFrame();
        SoaView = state.ReactiveDaemonSoaView();
        ZoneRender = state.ReactiveZoneRender();
    }

    public CultMeshReactiveDocument<AetheriaRuntimeDaemonFrameDocument> Frame { get; }
    public CultMeshReactiveDocument<AetheriaRuntimeDaemonSoaViewDocument> SoaView { get; }
    public CultMeshReactiveDocument<AetheriaRuntimeZoneRenderDocument> ZoneRender { get; }

    public AetheriaRuntimeDaemonRenderView Current =>
        TryRead(out var observed) ? observed : null;

    public static AetheriaUnityDaemonRenderDocuments Open(AetheriaClientState state)
    {
        return new AetheriaUnityDaemonRenderDocuments(state);
    }

    public bool TryRead(out AetheriaRuntimeDaemonRenderView observed)
    {
        return AetheriaRuntimeDaemonRenderView.TryCreateCurrent(
            Frame,
            SoaView,
            ZoneRender,
            out observed);
    }

    public void Dispose()
    {
        Frame?.Dispose();
        SoaView?.Dispose();
        ZoneRender?.Dispose();
    }
}
