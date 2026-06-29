using System;
using GameCult.Mesh;

#nullable enable

namespace GameCult.Aetheria.State.Verse
{
    public sealed class AetheriaRuntimeDaemonRenderView
    {
        public AetheriaRuntimeDaemonRenderView(
            AetheriaRuntimeDaemonFrameDocument frame,
            AetheriaRuntimeDaemonSoaViewDocument? soaView,
            AetheriaRuntimeZoneRenderDocument zoneRender)
        {
            Frame = frame ?? throw new ArgumentNullException(nameof(frame));
            SoaView = soaView;
            SoaIndex = AetheriaRuntimeDaemonSoaViewIndex.Build(soaView);
            ZoneRender = zoneRender ?? throw new ArgumentNullException(nameof(zoneRender));
        }

        public AetheriaRuntimeDaemonFrameDocument Frame { get; }
        public AetheriaRuntimeDaemonSoaViewDocument? SoaView { get; }
        public AetheriaRuntimeDaemonSoaViewIndex SoaIndex { get; }
        public AetheriaRuntimeZoneRenderDocument ZoneRender { get; }
        public bool HasSoaView => SoaView != null && SoaIndex.IsValid;
        public bool IsAuthoritative => Frame.IsAuthoritative;
        public AetheriaRuntimeRunCheckpointCommit Run => Frame.Run;

        public static bool TryCreateCurrent(
            CultMeshReactiveDocument<AetheriaRuntimeDaemonFrameDocument> frame,
            CultMeshReactiveDocument<AetheriaRuntimeDaemonSoaViewDocument>? soaView,
            CultMeshReactiveDocument<AetheriaRuntimeZoneRenderDocument> zoneRender,
            out AetheriaRuntimeDaemonRenderView? observed)
        {
            observed = null;
            try
            {
                var currentFrame = frame?.Current;
                var currentZoneRender = zoneRender?.Current;
                if (currentFrame == null || currentZoneRender == null)
                    return false;

                var currentSoaView = soaView?.Current;
                if (currentSoaView == null ||
                    !string.Equals(currentSoaView.Schema, AetheriaRuntimeDaemonSchemas.SoaView, StringComparison.Ordinal))
                {
                    currentSoaView = null;
                }

                observed = new AetheriaRuntimeDaemonRenderView(currentFrame, currentSoaView, currentZoneRender);
                return true;
            }
            catch
            {
                return false;
            }
        }

    }
}
