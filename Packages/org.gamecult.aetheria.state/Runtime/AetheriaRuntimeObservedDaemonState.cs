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
            string framePath,
            string soaViewPath)
        {
            Frame = frame ?? throw new ArgumentNullException(nameof(frame));
            SoaView = soaView;
            SoaIndex = AetheriaRuntimeDaemonSoaViewIndex.Build(soaView);
            FramePath = framePath ?? "";
            SoaViewPath = soaViewPath ?? "";
        }

        public AetheriaRuntimeDaemonFrameDocument Frame { get; }
        public AetheriaRuntimeDaemonSoaViewDocument? SoaView { get; }
        public AetheriaRuntimeDaemonSoaViewIndex SoaIndex { get; }
        public string FramePath { get; }
        public string SoaViewPath { get; }
        public bool HasSoaView => SoaView != null && SoaIndex.IsValid;
        public bool IsAuthoritative => Frame.IsAuthoritative;
        public AetheriaRuntimeRunCheckpointCommit Run => Frame.Run;

        public static async Task<AetheriaRuntimeObservedDaemonState?> ReadAsync(
            AetheriaClientState state,
            string statePath)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));

            var frame = await state.Daemon.LatestFrame.LatestAsync().ConfigureAwait(false);
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
                soaView,
                AetheriaRuntimeDaemonFrameStore.GetFramePath(statePath),
                AetheriaRuntimeDaemonSoaViewStore.GetViewPath(statePath));
        }

        private static async Task<AetheriaRuntimeDaemonSoaViewDocument?> TryReadLatestSoaViewAsync(
            AetheriaClientState state)
        {
            try
            {
                return await state.Daemon.LatestSoaView.LatestAsync().ConfigureAwait(false);
            }
            catch (KeyNotFoundException)
            {
                return null;
            }
        }
    }
}
