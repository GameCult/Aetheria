using System;

#nullable enable

namespace GameCult.Aetheria.State.Verse
{
    public sealed class AetheriaRuntimeDaemonObservationResult
    {
        public AetheriaRuntimeDaemonObservationResult(
            bool observed,
            bool frameChanged,
            bool soaViewChanged,
            long frameId,
            long soaGeneration,
            bool isAuthoritative)
        {
            Observed = observed;
            FrameChanged = frameChanged;
            SoaViewChanged = soaViewChanged;
            FrameId = frameId;
            SoaGeneration = soaGeneration;
            IsAuthoritative = isAuthoritative;
        }

        public bool Observed { get; }
        public bool FrameChanged { get; }
        public bool SoaViewChanged { get; }
        public bool Changed => FrameChanged || SoaViewChanged;
        public long FrameId { get; }
        public long SoaGeneration { get; }
        public bool IsAuthoritative { get; }
    }

    public sealed class AetheriaRuntimeDaemonObservationCursor
    {
        public long LastFrameId { get; private set; } = -1;
        public long LastSoaGeneration { get; private set; } = -1;
        public string LastDaemonId { get; private set; } = "";
        public string LastSessionId { get; private set; } = "";

        public AetheriaRuntimeDaemonObservationResult Observe(AetheriaRuntimeDaemonRenderView? observed)
        {
            if (observed == null)
            {
                return new AetheriaRuntimeDaemonObservationResult(
                    false,
                    false,
                    false,
                    LastFrameId,
                    LastSoaGeneration,
                    false);
            }

            var frame = observed.Frame;
            var soaGeneration = observed.SoaView?.Generation ?? -1;
            var frameChanged = frame.FrameId != LastFrameId ||
                               !string.Equals(frame.DaemonId, LastDaemonId, StringComparison.Ordinal) ||
                               !string.Equals(frame.SessionId, LastSessionId, StringComparison.Ordinal);
            var soaViewChanged = soaGeneration != LastSoaGeneration;

            LastFrameId = frame.FrameId;
            LastSoaGeneration = soaGeneration;
            LastDaemonId = frame.DaemonId ?? "";
            LastSessionId = frame.SessionId ?? "";

            return new AetheriaRuntimeDaemonObservationResult(
                true,
                frameChanged,
                soaViewChanged,
                frame.FrameId,
                soaGeneration,
                frame.IsAuthoritative);
        }

        public void Reset()
        {
            LastFrameId = -1;
            LastSoaGeneration = -1;
            LastDaemonId = "";
            LastSessionId = "";
        }
    }
}
