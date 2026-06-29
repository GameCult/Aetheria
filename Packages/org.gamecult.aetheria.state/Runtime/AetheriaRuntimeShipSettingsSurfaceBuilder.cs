using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GameCult.Eve.Surface;

namespace GameCult.Aetheria.State.Verse
{
    public sealed class AetheriaRuntimeShipSettingsSurfaceState
    {
        public AetheriaRuntimeShipSettingsSurfaceState(
            string shipName,
            string shutdownPerformance,
            string updatedAtUtc)
        {
            ShipName = shipName ?? "";
            ShutdownPerformance = shutdownPerformance ?? "";
            UpdatedAtUtc = updatedAtUtc ?? "";
        }

        public string ShipName { get; }
        public string ShutdownPerformance { get; }
        public string UpdatedAtUtc { get; }
    }

    public static class AetheriaRuntimeShipSettingsSurfaceBuilder
    {
        public const string SurfaceId = "aetheria.inventory.current_ship_settings";
        public const string DecrementShutdownThreshold = "aetheria.inventory.current_ship_settings.shutdown.decrement";
        public const string IncrementShutdownThreshold = "aetheria.inventory.current_ship_settings.shutdown.increment";
        public const string ResetShutdownThreshold = "aetheria.inventory.current_ship_settings.shutdown.reset";
        public const string Close = "aetheria.inventory.current_ship_settings.close";

        public static AetheriaRuntimeSurfaceDocument Build(
            string shipName,
            float shutdownPerformance,
            Func<float, string> formatShutdownPerformance,
            DateTime updatedAtUtc = default(DateTime),
            long version = 1)
        {
            return Build(
                ComposeState(
                    shipName,
                    shutdownPerformance,
                    formatShutdownPerformance,
                    updatedAtUtc),
                version);
        }

        private static AetheriaRuntimeShipSettingsSurfaceState ComposeState(
            string shipName,
            float shutdownPerformance,
            Func<float, string> formatShutdownPerformance,
            DateTime updatedAtUtc = default(DateTime))
        {
            if (updatedAtUtc == default(DateTime))
                updatedAtUtc = DateTime.UtcNow;

            return new AetheriaRuntimeShipSettingsSurfaceState(
                shipName,
                formatShutdownPerformance == null
                    ? shutdownPerformance.ToString("0.###", CultureInfo.InvariantCulture)
                    : formatShutdownPerformance(shutdownPerformance),
                updatedAtUtc.ToString("O", CultureInfo.InvariantCulture));
        }

        public static AetheriaRuntimeSurfaceDocument Build(
            AetheriaRuntimeShipSettingsSurfaceState state,
            long version = 1)
        {
            state ??= new AetheriaRuntimeShipSettingsSurfaceState("", "", "");

            return new AetheriaRuntimeSurfaceDocument(
                providerId: "aetheria",
                providerKind: "inventory.menu",
                title: "Current Ship Settings",
                version: version,
                updatedAtUtc: state.UpdatedAtUtc,
                surface: new AetheriaRuntimeSurfaceTree(
                    SurfaceId,
                    Node(
                        $"{SurfaceId}.root",
                        "surface",
                        Array.Empty<(string Key, string Value)>(),
                        Card(
                            $"{SurfaceId}.card",
                            state.ShipName,
                            Metric(
                                $"{SurfaceId}.shutdown.metric",
                                "Shutdown Threshold",
                                state.ShutdownPerformance),
                            Text(
                                $"{SurfaceId}.note",
                                "The observing client supplies selected ship state; shutdown changes are sent as daemon operations."),
                            ButtonRow(
                                $"{SurfaceId}.shutdown.buttons",
                                Button($"{SurfaceId}.shutdown.decrement", "Threshold -", DecrementShutdownThreshold),
                                Button($"{SurfaceId}.shutdown.increment", "Threshold +", IncrementShutdownThreshold),
                                Button($"{SurfaceId}.shutdown.reset", "Default", ResetShutdownThreshold),
                                Button($"{SurfaceId}.close", "Close", Close)))),
                    Array.Empty<AetheriaRuntimeSurfaceStyleToken>()),
                commands: new[]
                {
                    new AetheriaRuntimeSurfaceCommandTemplate(DecrementShutdownThreshold, "Threshold -", AetheriaRuntimeSurfaceCommandTemplate.CultMeshTransport),
                    new AetheriaRuntimeSurfaceCommandTemplate(IncrementShutdownThreshold, "Threshold +", AetheriaRuntimeSurfaceCommandTemplate.CultMeshTransport),
                    new AetheriaRuntimeSurfaceCommandTemplate(ResetShutdownThreshold, "Default", AetheriaRuntimeSurfaceCommandTemplate.CultMeshTransport),
                    new AetheriaRuntimeSurfaceCommandTemplate(Close, "Close", AetheriaRuntimeSurfaceCommandTemplate.CultMeshTransport)
                });
        }

        private static AetheriaRuntimeSurfaceComponent Card(
            string id,
            string title,
            params AetheriaRuntimeSurfaceComponent[] children)
        {
            return Node(id, "card", new[] { ("title", title ?? "") }, children);
        }

        private static AetheriaRuntimeSurfaceComponent Metric(string id, string label, string value)
        {
            return Node(id, "metric", new[] { ("label", label ?? ""), ("value", value ?? "") });
        }

        private static AetheriaRuntimeSurfaceComponent Text(string id, string value)
        {
            return Node(id, "text", new[] { ("value", value ?? "") });
        }

        private static AetheriaRuntimeSurfaceComponent Button(string id, string label, string command)
        {
            return Node(id, "control.button", new[] { ("label", label ?? ""), ("command", command ?? "") });
        }

        private static AetheriaRuntimeSurfaceComponent ButtonRow(
            string id,
            params AetheriaRuntimeSurfaceComponent[] children)
        {
            return Node(id, "control.row", Array.Empty<(string Key, string Value)>(), children);
        }

        private static AetheriaRuntimeSurfaceComponent Node(
            string id,
            string kind,
            IEnumerable<(string Key, string Value)> props,
            params AetheriaRuntimeSurfaceComponent[] children)
        {
            return new AetheriaRuntimeSurfaceComponent(
                id ?? "",
                kind ?? "",
                (props ?? Array.Empty<(string Key, string Value)>())
                    .ToDictionary(prop => prop.Key, prop => prop.Value ?? "", StringComparer.Ordinal),
                children ?? Array.Empty<AetheriaRuntimeSurfaceComponent>());
        }
    }

    public enum AetheriaRuntimeShipSettingsCommandKind
    {
        Unknown = 0,
        DecrementShutdownThreshold = 1,
        IncrementShutdownThreshold = 2,
        ResetShutdownThreshold = 3,
        Close = 4
    }

    public readonly struct AetheriaRuntimeShipSettingsCommand
    {
        public AetheriaRuntimeShipSettingsCommand(AetheriaRuntimeShipSettingsCommandKind kind)
        {
            Kind = kind;
        }

        public AetheriaRuntimeShipSettingsCommandKind Kind { get; }
    }

    public static class AetheriaRuntimeShipSettingsSurfaceCommands
    {
        public const float DefaultShutdownThresholdStep = 0.05f;

        public static bool TryRead(
            EveSurfaceCommandRequest request,
            out AetheriaRuntimeShipSettingsCommand command)
        {
            command = default;
            if (request == null ||
                !string.Equals(request.SurfaceId, AetheriaRuntimeShipSettingsSurfaceBuilder.SurfaceId, StringComparison.Ordinal))
                return false;

            switch (request.Operation?.OperationId ?? "")
            {
                case AetheriaRuntimeShipSettingsSurfaceBuilder.DecrementShutdownThreshold:
                    command = new AetheriaRuntimeShipSettingsCommand(
                        AetheriaRuntimeShipSettingsCommandKind.DecrementShutdownThreshold);
                    return true;
                case AetheriaRuntimeShipSettingsSurfaceBuilder.IncrementShutdownThreshold:
                    command = new AetheriaRuntimeShipSettingsCommand(
                        AetheriaRuntimeShipSettingsCommandKind.IncrementShutdownThreshold);
                    return true;
                case AetheriaRuntimeShipSettingsSurfaceBuilder.ResetShutdownThreshold:
                    command = new AetheriaRuntimeShipSettingsCommand(
                        AetheriaRuntimeShipSettingsCommandKind.ResetShutdownThreshold);
                    return true;
                case AetheriaRuntimeShipSettingsSurfaceBuilder.Close:
                    command = new AetheriaRuntimeShipSettingsCommand(
                        AetheriaRuntimeShipSettingsCommandKind.Close);
                    return true;
                default:
                    return false;
            }
        }

        public static float ResolveShutdownPerformance(
            AetheriaRuntimeShipSettingsCommandKind kind,
            float currentShutdownPerformance,
            float defaultShutdownPerformance,
            float shutdownThresholdStep = DefaultShutdownThresholdStep)
        {
            switch (kind)
            {
                case AetheriaRuntimeShipSettingsCommandKind.DecrementShutdownThreshold:
                    return ClampShutdownPerformance(currentShutdownPerformance - shutdownThresholdStep);
                case AetheriaRuntimeShipSettingsCommandKind.IncrementShutdownThreshold:
                    return ClampShutdownPerformance(currentShutdownPerformance + shutdownThresholdStep);
                case AetheriaRuntimeShipSettingsCommandKind.ResetShutdownThreshold:
                    return ClampShutdownPerformance(defaultShutdownPerformance);
                default:
                    return ClampShutdownPerformance(currentShutdownPerformance);
            }
        }

        public static float ClampShutdownPerformance(float value)
        {
            if (float.IsNaN(value))
                return 0f;
            if (value <= 0f)
                return 0f;
            return value >= 1f ? 1f : value;
        }
    }
}
