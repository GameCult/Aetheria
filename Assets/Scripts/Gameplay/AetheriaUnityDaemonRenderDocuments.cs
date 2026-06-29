using System;
using GameCult.Aetheria.State.Verse;
using GameCult.Mesh;

public sealed class AetheriaUnityDaemonRenderDocuments : IDisposable
{
    private readonly CultMeshReactiveDocument<AetheriaRuntimeDaemonFrameDocument> _frame;
    private readonly CultMeshReactiveDocument<AetheriaRuntimeDaemonSoaViewDocument> _soaView;
    private readonly CultMeshReactiveDocument<AetheriaRuntimeZoneRenderDocument> _zoneRender;

    private AetheriaUnityDaemonRenderDocuments(AetheriaClientState state)
    {
        if (state == null)
            throw new ArgumentNullException(nameof(state));

        _frame = state.ReactiveDaemonFrame();
        _soaView = state.ReactiveDaemonSoaView();
        _zoneRender = state.ReactiveZoneRender();
    }

    public AetheriaRuntimeDaemonRenderView Current =>
        TryRead(out var observed) ? observed : null;

    public static AetheriaUnityDaemonRenderDocuments Open(AetheriaClientState state)
    {
        return new AetheriaUnityDaemonRenderDocuments(state);
    }

    public bool TryRead(out AetheriaRuntimeDaemonRenderView observed)
    {
        return AetheriaRuntimeDaemonRenderView.TryCreateCurrent(
            _frame,
            _soaView,
            _zoneRender,
            out observed);
    }

    public void Dispose()
    {
        _frame?.Dispose();
        _soaView?.Dispose();
        _zoneRender?.Dispose();
    }
}
