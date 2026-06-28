using System;

#nullable enable

namespace GameCult.Aetheria.State.Verse
{
    public sealed class AetheriaRuntimeObservedDaemonState
    {
        public AetheriaRuntimeObservedDaemonState(
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
            AetheriaRuntimeDaemonFrameSession frame,
            AetheriaRuntimeDaemonSoaViewSession? soaView,
            AetheriaRuntimeZoneRenderSession zoneRender,
            out AetheriaRuntimeObservedDaemonState? observed)
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

                observed = new AetheriaRuntimeObservedDaemonState(currentFrame, currentSoaView, currentZoneRender);
                return true;
            }
            catch
            {
                return false;
            }
        }

    }

    public sealed class AetheriaRuntimeObservedDaemonSession : IDisposable
    {
        private readonly AetheriaRuntimeDaemonFrameSession _frame;
        private readonly AetheriaRuntimeDaemonSoaViewSession? _soaView;
        private readonly AetheriaRuntimeZoneRenderSession _zoneRender;

        public AetheriaRuntimeObservedDaemonSession(
            AetheriaRuntimeDaemonFrameSession frame,
            AetheriaRuntimeDaemonSoaViewSession? soaView,
            AetheriaRuntimeZoneRenderSession zoneRender)
        {
            _frame = frame ?? throw new ArgumentNullException(nameof(frame));
            _soaView = soaView;
            _zoneRender = zoneRender ?? throw new ArgumentNullException(nameof(zoneRender));
        }

        public AetheriaRuntimeObservedDaemonState? Current =>
            AetheriaRuntimeObservedDaemonState.TryCreateCurrent(_frame, _soaView, _zoneRender, out var current)
                ? current
                : null;

        public void Dispose()
        {
            _frame.Dispose();
            _soaView?.Dispose();
            _zoneRender.Dispose();
        }
    }
}
