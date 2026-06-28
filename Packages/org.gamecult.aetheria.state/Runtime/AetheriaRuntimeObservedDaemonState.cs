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
            AetheriaRuntimeZoneRenderDocument? zoneRender = null)
        {
            Frame = frame ?? throw new ArgumentNullException(nameof(frame));
            SoaView = soaView;
            SoaIndex = AetheriaRuntimeDaemonSoaViewIndex.Build(soaView);
            ZoneRender = zoneRender ?? AetheriaRuntimeRtsProjection.ProjectZoneRender(Frame);
        }

        public AetheriaRuntimeDaemonFrameDocument Frame { get; }
        public AetheriaRuntimeDaemonSoaViewDocument? SoaView { get; }
        public AetheriaRuntimeDaemonSoaViewIndex SoaIndex { get; }
        public AetheriaRuntimeZoneRenderDocument ZoneRender { get; }
        public bool HasSoaView => SoaView != null && SoaIndex.IsValid;
        public bool IsAuthoritative => Frame.IsAuthoritative;
        public AetheriaRuntimeRunCheckpointCommit Run => Frame.Run;

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

        private AetheriaRuntimeReactiveObservedDaemonState(
            CultMeshReactiveDocument<AetheriaRuntimeDaemonFrameDocument> frame,
            CultMeshReactiveDocument<AetheriaRuntimeDaemonSoaViewDocument>? soaView)
        {
            _frame = frame ?? throw new ArgumentNullException(nameof(frame));
            _soaView = soaView;
        }

        public static async Task<AetheriaRuntimeReactiveObservedDaemonState> CreateAsync(
            AetheriaClientState state,
            CultMeshReactiveDocumentOptions? options = null)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));

            var frame = await state.ReactiveDaemonFrameAsync(options).ConfigureAwait(false);
            var soaView = await TryCreateSoaViewAsync(state, options).ConfigureAwait(false);
            return new AetheriaRuntimeReactiveObservedDaemonState(frame, soaView);
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

                return new AetheriaRuntimeObservedDaemonState(frame, soaView);
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
