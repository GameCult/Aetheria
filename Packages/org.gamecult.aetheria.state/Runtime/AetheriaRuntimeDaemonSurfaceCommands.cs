using System;
using System.Collections.Generic;
using GameCult.Eve.Surface;
using GameCult.Mesh;

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
                !string.Equals(request.ProviderId, "aetheria.daemon", StringComparison.Ordinal) ||
                !TryResolveKind(request, out var kind))
            {
                return false;
            }

            using var frame = client.State.ReactiveDaemonFrame();
            using var soaView = TryReactiveDaemonSoaView(client);
            using var zoneRender = client.State.ReactiveZoneRender();
            var observed = AetheriaRuntimeObservedDaemonState.TryCreateCurrent(frame, soaView, zoneRender, out var current)
                ? current
                : null;
            var operationClient = new AetheriaRuntimeDaemonOperationClient(
                client.StatePath,
                string.IsNullOrWhiteSpace(request.ClientId) ? AetheriaRuntimeDaemonOperationClient.DefaultClientId : request.ClientId,
                observed?.Frame.SessionId ?? "local",
                client.SubmitDaemonCommandDocument);
            return AetheriaRuntimeDaemonSurfaceCommandCatalog.TrySubmitArgumentless(
                operationClient,
                observed,
                kind,
                out envelope);
        }

        private static CultMeshReactiveDocument<AetheriaRuntimeDaemonSoaViewDocument>? TryReactiveDaemonSoaView(
            AetheriaClient client)
        {
            try
            {
                return client.State.ReactiveDaemonSoaView();
            }
            catch (KeyNotFoundException)
            {
                return null;
            }
        }

        private static bool TryResolveKind(EveSurfaceCommandRequest request, out AetheriaRuntimeDaemonCommandKinds kind)
        {
            kind = AetheriaRuntimeDaemonCommandKinds.None;
            var command = request.Operation?.OperationId ?? "";
            if (command.StartsWith(AetheriaRuntimeDaemonSurfaceCommandCatalog.CommandPrefix, StringComparison.Ordinal))
                command = command.Substring(AetheriaRuntimeDaemonSurfaceCommandCatalog.CommandPrefix.Length);

            return Enum.TryParse(command, ignoreCase: false, out kind) &&
                   kind != AetheriaRuntimeDaemonCommandKinds.None;
        }
    }
}
