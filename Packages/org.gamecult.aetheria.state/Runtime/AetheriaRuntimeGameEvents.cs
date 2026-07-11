using System;
using System.Linq;

#nullable enable

namespace GameCult.Aetheria.State.Verse
{
    public static class AetheriaRuntimeGameEvents
    {
        public const int RetainedEventCount = 256;
        public static void Append(AetheriaRuntimeRunCheckpointCommit run, AetheriaRuntimeGameEventCommit gameEvent)
        {
            if (run == null || gameEvent == null || string.IsNullOrWhiteSpace(gameEvent.EventId)) return;
            var events = (run.GameEvents ?? Array.Empty<AetheriaRuntimeGameEventCommit>())
                .Where(value => value != null && !string.Equals(value.EventId, gameEvent.EventId, StringComparison.Ordinal))
                .Append(gameEvent)
                .OrderBy(value => value.FrameId).ThenBy(value => value.EventId, StringComparer.Ordinal)
                .ToArray();
            run.GameEvents = events.Length <= RetainedEventCount ? events : events.Skip(events.Length - RetainedEventCount).ToArray();
        }
    }
}
