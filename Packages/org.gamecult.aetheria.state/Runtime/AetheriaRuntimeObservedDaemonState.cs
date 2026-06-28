using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GameCult.Mesh;

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
            CultMeshReactiveDocument<AetheriaRuntimeDaemonFrameDocument> frame,
            CultMeshReactiveDocument<AetheriaRuntimeDaemonSoaViewDocument>? soaView,
            CultMeshReactiveDocument<AetheriaRuntimeZoneRenderDocument> zoneRender,
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

        public static async Task<AetheriaRuntimeObservedDaemonState?> ReadAsync(
            AetheriaClientState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));

            return await state.LatestObservedDaemonAsync().ConfigureAwait(false);
        }
    }

    public sealed class AetheriaRuntimeReactiveObservedDaemonState : IDisposable
    {
        private readonly CultMeshReactiveDocument<AetheriaRuntimeDaemonFrameDocument> _frame;
        private readonly CultMeshReactiveDocument<AetheriaRuntimeDaemonSoaViewDocument>? _soaView;
        private readonly CultMeshReactiveDocument<AetheriaRuntimeZoneRenderDocument> _zoneRender;

        private AetheriaRuntimeReactiveObservedDaemonState(
            CultMeshReactiveDocument<AetheriaRuntimeDaemonFrameDocument> frame,
            CultMeshReactiveDocument<AetheriaRuntimeDaemonSoaViewDocument>? soaView,
            CultMeshReactiveDocument<AetheriaRuntimeZoneRenderDocument> zoneRender)
        {
            _frame = frame ?? throw new ArgumentNullException(nameof(frame));
            _soaView = soaView;
            _zoneRender = zoneRender ?? throw new ArgumentNullException(nameof(zoneRender));
        }

        public static async Task<AetheriaRuntimeReactiveObservedDaemonState> CreateAsync(
            AetheriaClientState state,
            CultMeshReactiveDocumentOptions? options = null)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));

            var frame = await state.ReactiveDaemonFrameAsync(options).ConfigureAwait(false);
            var soaView = await TryCreateSoaViewAsync(state, options).ConfigureAwait(false);
            var zoneRender = await state.ReactiveZoneRenderAsync(options).ConfigureAwait(false);
            return new AetheriaRuntimeReactiveObservedDaemonState(frame, soaView, zoneRender);
        }

        public AetheriaRuntimeObservedDaemonState? Current
        {
            get
            {
                var frame = _frame.Current;
                if (frame == null)
                    return null;

                var soaView = _soaView?.Current;
                if (soaView == null ||
                    !string.Equals(soaView.Schema, AetheriaRuntimeDaemonSchemas.SoaView, StringComparison.Ordinal))
                {
                    soaView = null;
                }

                var zoneRender = _zoneRender.Current;
                if (zoneRender == null)
                    return null;

                return new AetheriaRuntimeObservedDaemonState(frame, soaView, zoneRender);
            }
        }

        public bool TryCurrent(out AetheriaRuntimeObservedDaemonState? observed)
        {
            observed = null;
            try
            {
                observed = Current;
                return observed != null;
            }
            catch
            {
                return false;
            }
        }

        public void Dispose()
        {
            _frame.Dispose();
            _soaView?.Dispose();
            _zoneRender.Dispose();
        }

        private static async Task<CultMeshReactiveDocument<AetheriaRuntimeDaemonSoaViewDocument>?> TryCreateSoaViewAsync(
            AetheriaClientState state,
            CultMeshReactiveDocumentOptions? options)
        {
            try
            {
                return await state.ReactiveDaemonSoaViewAsync(options).ConfigureAwait(false);
            }
            catch (KeyNotFoundException)
            {
                return null;
            }
        }
    }
}
