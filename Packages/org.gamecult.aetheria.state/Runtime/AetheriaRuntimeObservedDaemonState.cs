using System;
using System.Collections.Generic;
using System.Threading.Tasks;

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

            var frame = await state.LatestDaemonFrameAsync().ConfigureAwait(false);
            if (frame == null)
                return null;

            var soaView = await TryReadLatestSoaViewAsync(state).ConfigureAwait(false);
            if (soaView == null ||
                !string.Equals(soaView.Schema, AetheriaRuntimeDaemonSchemas.SoaView, StringComparison.Ordinal))
            {
                soaView = null;
            }

            return new AetheriaRuntimeObservedDaemonState(
                frame,
                soaView);
        }

        private static async Task<AetheriaRuntimeDaemonSoaViewDocument?> TryReadLatestSoaViewAsync(
            AetheriaClientState state)
        {
            try
            {
                return await state.LatestDaemonSoaViewAsync().ConfigureAwait(false);
            }
            catch (KeyNotFoundException)
            {
                return null;
            }
        }
    }
}
