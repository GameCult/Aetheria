using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GameCult.Eve.Surface;

namespace GameCult.Aetheria.State.Verse
{
    public sealed class AetheriaRuntimeInputSettingsSurfaceState
    {
        public AetheriaRuntimeInputSettingsSurfaceState(
            IReadOnlyList<AetheriaRuntimeInputBindingSurfaceState> bindings,
            IReadOnlyList<AetheriaRuntimeActionBarInputSurfaceState> actionBarInputs,
            bool capturePending,
            string capturePrompt,
            string updatedAtUtc)
        {
            Bindings = bindings ?? Array.Empty<AetheriaRuntimeInputBindingSurfaceState>();
            ActionBarInputs = actionBarInputs ?? Array.Empty<AetheriaRuntimeActionBarInputSurfaceState>();
            CapturePending = capturePending;
            CapturePrompt = capturePrompt ?? "";
            UpdatedAtUtc = updatedAtUtc ?? "";
        }

        public IReadOnlyList<AetheriaRuntimeInputBindingSurfaceState> Bindings { get; }

        public IReadOnlyList<AetheriaRuntimeActionBarInputSurfaceState> ActionBarInputs { get; }

        public bool CapturePending { get; }

        public string CapturePrompt { get; }

        public string UpdatedAtUtc { get; }
    }

    public sealed class AetheriaRuntimeInputBindingSurfaceState
    {
        public AetheriaRuntimeInputBindingSurfaceState(
            string actionName,
            int bindingIndex,
            string bindingLabel,
            string currentInputLabel)
        {
            ActionName = actionName ?? "";
            BindingIndex = bindingIndex;
            BindingLabel = bindingLabel ?? "";
            CurrentInputLabel = currentInputLabel ?? "";
        }

        public string ActionName { get; }

        public int BindingIndex { get; }

        public string BindingLabel { get; }

        public string CurrentInputLabel { get; }
    }

    public sealed class AetheriaRuntimeActionBarInputSurfaceState
    {
        public AetheriaRuntimeActionBarInputSurfaceState(string inputPath, string label, bool enabled)
        {
            InputPath = inputPath ?? "";
            Label = label ?? "";
            Enabled = enabled;
        }

        public string InputPath { get; }

        public string Label { get; }

        public bool Enabled { get; }
    }

    public static class AetheriaRuntimeInputSettingsSurfaceBuilder
    {
        public static AetheriaRuntimeSurfaceDocument Build(
            AetheriaRuntimeInputSettingsSurfaceState settings,
            long version = 1)
        {
            settings ??= new AetheriaRuntimeInputSettingsSurfaceState(
                Array.Empty<AetheriaRuntimeInputBindingSurfaceState>(),
                Array.Empty<AetheriaRuntimeActionBarInputSurfaceState>(),
                capturePending: false,
                capturePrompt: "",
                updatedAtUtc: "");

            return new AetheriaRuntimeSurfaceDocument(
                providerId: "aetheria",
                providerKind: "game.runtime",
                title: "Aetheria Input Settings",
                version: version,
                updatedAtUtc: settings.UpdatedAtUtc,
                surface: new AetheriaRuntimeSurfaceTree(
                    AetheriaRuntimeInputSettingsCommands.SurfaceId,
                    Node(
                        "aetheria.inputSettings.root",
                        "surface",
                        Array.Empty<(string Key, string Value)>(),
                        Node(
                            "aetheria.inputSettings.summary",
                            "card",
                            new[] { ("title", "Input Settings") },
                            Metric(
                                "aetheria.inputSettings.summary.bindings",
                                "Rebindable Inputs",
                                settings.Bindings.Count.ToString()),
                            Metric(
                                "aetheria.inputSettings.summary.actionBar",
                                "Action-Bar Inputs",
                                settings.ActionBarInputs.Count(entry => entry.Enabled).ToString()),
                            Text(
                                "aetheria.inputSettings.summary.note",
                                "Low-level InputSystem edits flow through this Eve surface as typed input-setting requests.")),
                        Node(
                            "aetheria.inputSettings.capture",
                            "card",
                            new[] { ("title", "Capture") },
                            Text(
                                "aetheria.inputSettings.capture.note",
                                settings.CapturePending
                                    ? settings.CapturePrompt
                                    : "Choose a binding row, then press a keyboard or mouse input to capture it."),
                            ButtonRow(
                                "aetheria.inputSettings.capture.actions",
                                settings.CapturePending
                                    ? Button(
                                        "aetheria.inputSettings.capture.cancel",
                                        "Cancel Capture",
                                        AetheriaRuntimeInputSettingsCommands.CancelCapture)
                                    : Button(
                                        "aetheria.inputSettings.capture.refresh",
                                        "Refresh",
                                        AetheriaRuntimeInputSettingsCommands.Refresh))),
                        Node(
                            "aetheria.inputSettings.bindings",
                            "card",
                            new[] { ("title", "Bindings") },
                            Node(
                                "aetheria.inputSettings.bindings.grid",
                                "grid",
                                Array.Empty<(string Key, string Value)>(),
                                settings.Bindings.Select(BuildBindingCard).ToArray())),
                        Node(
                            "aetheria.inputSettings.actionBarInputs",
                            "card",
                            new[] { ("title", "Action-Bar Inputs") },
                            Node(
                                "aetheria.inputSettings.actionBarInputs.grid",
                                "grid",
                                Array.Empty<(string Key, string Value)>(),
                                settings.ActionBarInputs.Select(BuildActionBarCard).ToArray()))),
                    Array.Empty<AetheriaRuntimeSurfaceStyleToken>()),
                commands: new[]
                {
                    new AetheriaRuntimeSurfaceCommandTemplate(
                        AetheriaRuntimeInputSettingsCommands.Refresh,
                        "Refresh",
                        AetheriaRuntimeSurfaceCommandTemplate.CultMeshTransport),
                    new AetheriaRuntimeSurfaceCommandTemplate(
                        AetheriaRuntimeInputSettingsCommands.BeginCapture,
                        "Capture Binding",
                        AetheriaRuntimeSurfaceCommandTemplate.CultMeshTransport),
                    new AetheriaRuntimeSurfaceCommandTemplate(
                        AetheriaRuntimeInputSettingsCommands.CancelCapture,
                        "Cancel Capture",
                        AetheriaRuntimeSurfaceCommandTemplate.CultMeshTransport),
                    new AetheriaRuntimeSurfaceCommandTemplate(
                        AetheriaRuntimeInputSettingsCommands.ToggleActionBar,
                        "Toggle Action-Bar Input",
                        AetheriaRuntimeSurfaceCommandTemplate.CultMeshTransport)
                });
        }

        private static AetheriaRuntimeSurfaceComponent BuildBindingCard(
            AetheriaRuntimeInputBindingSurfaceState binding)
        {
            return Node(
                $"aetheria.inputSettings.binding.{binding.ActionName}.{binding.BindingIndex}",
                "card",
                new[] { ("title", binding.BindingLabel) },
                Metric(
                    $"aetheria.inputSettings.binding.{binding.ActionName}.{binding.BindingIndex}.current",
                    "Current Input",
                    binding.CurrentInputLabel),
                Button(
                    $"aetheria.inputSettings.binding.{binding.ActionName}.{binding.BindingIndex}.capture",
                    "Capture Binding",
                    AetheriaRuntimeInputSettingsCommands.BeginCapture,
                    ("actionName", binding.ActionName),
                    ("bindingIndex", binding.BindingIndex.ToString()),
                    ("bindingLabel", binding.BindingLabel)));
        }

        private static AetheriaRuntimeSurfaceComponent BuildActionBarCard(
            AetheriaRuntimeActionBarInputSurfaceState input)
        {
            return Node(
                $"aetheria.inputSettings.actionBar.{input.Label}",
                "card",
                new[] { ("title", input.Label) },
                Metric(
                    $"aetheria.inputSettings.actionBar.{input.Label}.state",
                    "Enabled",
                    input.Enabled ? "Yes" : "No"),
                Button(
                    $"aetheria.inputSettings.actionBar.{input.Label}.toggle",
                    input.Enabled ? "Disable" : "Enable",
                    AetheriaRuntimeInputSettingsCommands.ToggleActionBar,
                    ("inputPath", input.InputPath),
                    ("enabled", input.Enabled ? "false" : "true"),
                    ("label", input.Label)));
        }

        private static AetheriaRuntimeSurfaceComponent Metric(string id, string label, string value)
        {
            return Node(id, "metric", new[] { ("label", label), ("value", value) });
        }

        private static AetheriaRuntimeSurfaceComponent Text(string id, string value)
        {
            return Node(id, "text", new[] { ("value", value) });
        }

        private static AetheriaRuntimeSurfaceComponent Button(
            string id,
            string label,
            string command,
            params (string Key, string Value)[] extraProps)
        {
            var props = new List<(string Key, string Value)>
            {
                ("label", label),
                ("command", command)
            };
            if (extraProps != null)
            {
                props.AddRange(extraProps);
            }

            return Node(id, "control.button", props);
        }

        private static AetheriaRuntimeSurfaceComponent ButtonRow(
            string id,
            params AetheriaRuntimeSurfaceComponent[] children)
        {
            return Node(id, "row", Array.Empty<(string Key, string Value)>(), children);
        }

        private static AetheriaRuntimeSurfaceComponent Node(
            string id,
            string kind,
            IEnumerable<(string Key, string Value)> props,
            params AetheriaRuntimeSurfaceComponent[] children)
        {
            return new AetheriaRuntimeSurfaceComponent(
                id,
                kind,
                props.ToDictionary(prop => prop.Key, prop => prop.Value, StringComparer.Ordinal),
                children ?? Array.Empty<AetheriaRuntimeSurfaceComponent>());
        }
    }

    public enum AetheriaRuntimeInputSettingsCommandKind
    {
        Unknown = 0,
        Refresh = 1,
        BeginCapture = 2,
        CancelCapture = 3,
        ToggleActionBar = 4
    }

    public readonly struct AetheriaRuntimeInputSettingsSurfaceCommand
    {
        public AetheriaRuntimeInputSettingsSurfaceCommand(
            AetheriaRuntimeInputSettingsCommandKind kind,
            string actionName = "",
            int bindingIndex = -1,
            string bindingLabel = "",
            string inputPath = "",
            bool enabled = false)
        {
            Kind = kind;
            ActionName = actionName ?? "";
            BindingIndex = bindingIndex;
            BindingLabel = bindingLabel ?? "";
            InputPath = inputPath ?? "";
            Enabled = enabled;
        }

        public AetheriaRuntimeInputSettingsCommandKind Kind { get; }
        public string ActionName { get; }
        public int BindingIndex { get; }
        public string BindingLabel { get; }
        public string InputPath { get; }
        public bool Enabled { get; }
    }

    public static class AetheriaRuntimeInputSettingsSurfaceCommands
    {
        public static bool TryRead(
            EveSurfaceCommandRequest request,
            out AetheriaRuntimeInputSettingsSurfaceCommand command)
        {
            command = default;
            if (request == null ||
                !string.Equals(request.SurfaceId, AetheriaRuntimeInputSettingsCommands.SurfaceId, StringComparison.Ordinal))
                return false;

            switch (request.Command ?? "")
            {
                case AetheriaRuntimeInputSettingsCommands.Refresh:
                    command = new AetheriaRuntimeInputSettingsSurfaceCommand(
                        AetheriaRuntimeInputSettingsCommandKind.Refresh);
                    return true;
                case AetheriaRuntimeInputSettingsCommands.CancelCapture:
                    command = new AetheriaRuntimeInputSettingsSurfaceCommand(
                        AetheriaRuntimeInputSettingsCommandKind.CancelCapture);
                    return true;
                case AetheriaRuntimeInputSettingsCommands.BeginCapture:
                    var actionName = ReadString(request, "actionName");
                    command = new AetheriaRuntimeInputSettingsSurfaceCommand(
                        AetheriaRuntimeInputSettingsCommandKind.BeginCapture,
                        actionName: actionName,
                        bindingIndex: ReadInt(request, "bindingIndex", -1),
                        bindingLabel: ReadString(request, "bindingLabel", actionName));
                    return true;
                case AetheriaRuntimeInputSettingsCommands.ToggleActionBar:
                    command = new AetheriaRuntimeInputSettingsSurfaceCommand(
                        AetheriaRuntimeInputSettingsCommandKind.ToggleActionBar,
                        inputPath: ReadString(request, "inputPath"),
                        enabled: ReadBool(request, "enabled", false));
                    return true;
                default:
                    return false;
            }
        }

        private static string ReadString(EveSurfaceCommandRequest request, string key, string defaultValue = "")
        {
            return request.Payload != null &&
                   request.Payload.TryGetValue(key, out var raw)
                ? raw ?? defaultValue
                : defaultValue;
        }

        private static int ReadInt(EveSurfaceCommandRequest request, string key, int defaultValue)
        {
            return request.Payload != null &&
                   request.Payload.TryGetValue(key, out var raw) &&
                   int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
                ? value
                : defaultValue;
        }

        private static bool ReadBool(EveSurfaceCommandRequest request, string key, bool defaultValue)
        {
            return request.Payload != null &&
                   request.Payload.TryGetValue(key, out var raw) &&
                   bool.TryParse(raw, out var value)
                ? value
                : defaultValue;
        }
    }
}
