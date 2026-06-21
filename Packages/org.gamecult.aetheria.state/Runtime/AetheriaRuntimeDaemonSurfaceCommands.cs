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
            return TrySubmitKnownCommand(client, observed, kind, out envelope);
        }

        private static bool TrySubmitKnownCommand(
            AetheriaRuntimeDaemonOperationClient client,
            AetheriaRuntimeObservedDaemonState? observed,
            AetheriaRuntimeDaemonCommandKinds kind,
            out AetheriaRuntimeDaemonCommandEnvelope? envelope)
        {
            envelope = null;
            if (client == null)
                return false;

            try
            {
                envelope = kind switch
                {
                    AetheriaRuntimeDaemonCommandKinds.ClearTarget => client.ClearTarget(observed),
                    AetheriaRuntimeDaemonCommandKinds.TargetNearest => client.TargetNearest(observed),
                    AetheriaRuntimeDaemonCommandKinds.TargetNext => client.TargetNext(observed),
                    AetheriaRuntimeDaemonCommandKinds.TargetPrevious => client.TargetPrevious(observed),
                    AetheriaRuntimeDaemonCommandKinds.SetMoveVector => client.SetMoveVector(observed, 0.0, 1.0),
                    AetheriaRuntimeDaemonCommandKinds.SetLookDirection => client.SetLookDirection(observed, 0.0, 1.0, 0.0),
                    AetheriaRuntimeDaemonCommandKinds.SetTractorPower => client.SetTractorPower(observed, 1.0),
                    AetheriaRuntimeDaemonCommandKinds.FireWeaponGroup => client.FireWeaponGroup(observed, 0),
                    AetheriaRuntimeDaemonCommandKinds.SetWeaponGroupActive => client.SetWeaponGroupActive(observed, 0, true),
                    AetheriaRuntimeDaemonCommandKinds.SensorPing => client.SensorPing(observed),
                    AetheriaRuntimeDaemonCommandKinds.SetHeatsinksEnabled => client.SetHeatsinksEnabled(observed, true),
                    AetheriaRuntimeDaemonCommandKinds.SetOverrideShutdown => client.SetOverrideShutdown(observed, true),
                    AetheriaRuntimeDaemonCommandKinds.ToggleShieldEnabled => client.ToggleShieldEnabled(observed),
                    AetheriaRuntimeDaemonCommandKinds.DockNearest => client.DockNearest(observed, 0.0),
                    AetheriaRuntimeDaemonCommandKinds.Undock => client.Undock(observed),
                    _ => null
                };
                return envelope != null;
            }
            catch (InvalidOperationException)
            {
                envelope = null;
                return false;
            }
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
