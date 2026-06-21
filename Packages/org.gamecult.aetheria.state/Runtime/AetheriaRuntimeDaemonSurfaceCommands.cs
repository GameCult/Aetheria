using System;
using GameCult.Eve.Surface;

#nullable enable

namespace GameCult.Aetheria.State.Verse
{
    public static class AetheriaRuntimeDaemonSurfaceCommands
    {
        private const string CommandPrefix = "aetheria.daemon.commands.";

        public static bool TrySubmit(
            string stateFilePath,
            EveSurfaceCommandRequest request,
            out AetheriaRuntimeDaemonCommandEnvelope? envelope)
        {
            envelope = null;
            if (request == null ||
                !string.Equals(request.ProviderId, "aetheria.daemon", StringComparison.Ordinal) ||
                !TryResolveKind(request, out var kind))
            {
                return false;
            }

            AetheriaRuntimeStateReader.TryReadObservedDaemonState(stateFilePath, out var observed);
            var client = new AetheriaRuntimeDaemonOperationClient(
                stateFilePath,
                string.IsNullOrWhiteSpace(request.ClientId) ? "unity-uitoolkit" : request.ClientId,
                observed?.Frame.SessionId ?? "local");
            var command = client.Create(kind, observed);
            if (client.TrySend(command, out envelope, out _))
            {
                return true;
            }

            envelope = AetheriaRuntimeDaemonOperationClient.ToEnvelope(command);
            return true;
        }

        private static bool TryResolveKind(EveSurfaceCommandRequest request, out AetheriaRuntimeDaemonCommandKinds kind)
        {
            kind = AetheriaRuntimeDaemonCommandKinds.None;
            var command = request.Command ?? "";
            if (command.StartsWith(CommandPrefix, StringComparison.Ordinal))
                command = command.Substring(CommandPrefix.Length);

            return Enum.TryParse(command, ignoreCase: false, out kind) &&
                   kind != AetheriaRuntimeDaemonCommandKinds.None;
        }
    }
}
