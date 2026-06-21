using System;
using System.Collections.Generic;
using System.Linq;

#nullable enable

namespace GameCult.Aetheria.State.Verse
{
    public static class AetheriaRuntimeDaemonSurfaceCommandCatalog
    {
        public const string CommandPrefix = "aetheria.daemon.commands.";

        private static readonly AetheriaRuntimeDaemonCommandKinds[] ArgumentlessCommandKinds =
        {
            AetheriaRuntimeDaemonCommandKinds.ClearTarget,
            AetheriaRuntimeDaemonCommandKinds.TargetNearest,
            AetheriaRuntimeDaemonCommandKinds.TargetNext,
            AetheriaRuntimeDaemonCommandKinds.TargetPrevious,
            AetheriaRuntimeDaemonCommandKinds.SetMoveVector,
            AetheriaRuntimeDaemonCommandKinds.SetLookDirection,
            AetheriaRuntimeDaemonCommandKinds.SetTractorPower,
            AetheriaRuntimeDaemonCommandKinds.FireWeaponGroup,
            AetheriaRuntimeDaemonCommandKinds.SetWeaponGroupActive,
            AetheriaRuntimeDaemonCommandKinds.SensorPing,
            AetheriaRuntimeDaemonCommandKinds.SetHeatsinksEnabled,
            AetheriaRuntimeDaemonCommandKinds.SetOverrideShutdown,
            AetheriaRuntimeDaemonCommandKinds.ToggleShieldEnabled,
            AetheriaRuntimeDaemonCommandKinds.DockNearest,
            AetheriaRuntimeDaemonCommandKinds.Undock
        };

        public static IReadOnlyList<AetheriaRuntimeDaemonCommandKinds> ArgumentlessCommands => ArgumentlessCommandKinds;

        public static IReadOnlyList<AetheriaRuntimeSurfaceCommandTemplate> ArgumentlessSurfaceCommands =>
            ArgumentlessCommandKinds
                .Select(kind => new AetheriaRuntimeSurfaceCommandTemplate(
                    CommandName(kind),
                    Label(kind),
                    AetheriaRuntimeSurfaceCommandTemplate.CultMeshTransport))
                .ToArray();

        public static string CommandName(AetheriaRuntimeDaemonCommandKinds kind)
        {
            return CommandPrefix + kind;
        }

        public static string Label(AetheriaRuntimeDaemonCommandKinds kind)
        {
            return kind.ToString();
        }

        public static bool IsArgumentlessCommand(AetheriaRuntimeDaemonCommandKinds kind)
        {
            return ArgumentlessCommandKinds.Contains(kind);
        }

        public static bool TrySubmitArgumentless(
            AetheriaRuntimeDaemonOperationClient client,
            AetheriaRuntimeObservedDaemonState? observed,
            AetheriaRuntimeDaemonCommandKinds kind,
            out AetheriaRuntimeDaemonCommandEnvelope? envelope)
        {
            envelope = null;
            if (client == null || !IsArgumentlessCommand(kind))
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
    }
}
