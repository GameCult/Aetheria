using System;
using GameCult.Eve.Surface;

#nullable enable

namespace GameCult.Aetheria.State.Verse
{
    public static class AetheriaRuntimeDaemonSurfaceCommands
    {
        public static bool TrySubmit(
            AetheriaClient client,
            EveSurfaceCommandRequest request,
            out AetheriaRuntimeDaemonCommandEnvelope? envelope)
        {
            envelope = null;
            if (request == null ||
                client == null ||
                !client.Control.TrySubmitSurfaceCommand(request, out envelope))
            {
                return false;
            }

            return true;
        }
    }
}
