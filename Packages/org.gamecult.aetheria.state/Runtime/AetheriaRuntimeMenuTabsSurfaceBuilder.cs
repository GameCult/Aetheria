using System;
using System.Collections.Generic;
using System.Linq;
using GameCult.Eve.Surface;

namespace GameCult.Aetheria.State.Verse
{
    public sealed class AetheriaRuntimeMenuTabSurfaceEntry
    {
        public AetheriaRuntimeMenuTabSurfaceEntry(string key, string label, bool selected)
        {
            Key = key ?? "";
            Label = label ?? "";
            Selected = selected;
        }

        public string Key { get; }
        public string Label { get; }
        public bool Selected { get; }
    }

    public sealed class AetheriaRuntimeMenuTabsSurfaceState
    {
        public AetheriaRuntimeMenuTabsSurfaceState(
            string currentTabKey,
            IReadOnlyList<AetheriaRuntimeMenuTabSurfaceEntry> visibleTabs,
            string updatedAtUtc)
        {
            CurrentTabKey = currentTabKey ?? "";
            VisibleTabs = visibleTabs ?? Array.Empty<AetheriaRuntimeMenuTabSurfaceEntry>();
            UpdatedAtUtc = updatedAtUtc ?? "";
        }

        public string CurrentTabKey { get; }
        public IReadOnlyList<AetheriaRuntimeMenuTabSurfaceEntry> VisibleTabs { get; }
        public string UpdatedAtUtc { get; }
    }

    public sealed class AetheriaRuntimeMenuTabProjectionOption
    {
        public AetheriaRuntimeMenuTabProjectionOption(string key, string label, int order)
        {
            Key = key ?? "";
            Label = label ?? "";
            Order = order;
        }

        public string Key { get; }
        public string Label { get; }
        public int Order { get; }
    }

    public static class AetheriaRuntimeMenuTabsSurfaceBuilder
    {
        public const string SurfaceId = "aetheria.runtime_menu.tabs";

        public static string NormalizeTabKey(string tabKey)
        {
            return string.IsNullOrWhiteSpace(tabKey)
                ? "unknown"
                : tabKey.Trim().ToLowerInvariant();
        }

        public static string CommandFor(string tabKey)
        {
            return $"aetheria.runtime_menu.tab.{NormalizeTabKey(tabKey)}";
        }

        public static AetheriaRuntimeMenuTabsSurfaceState Project(
            string currentTabKey,
            IEnumerable<AetheriaRuntimeMenuTabProjectionOption> visibleTabs,
            string updatedAtUtc)
        {
            var normalizedCurrent = NormalizeTabKey(currentTabKey);
            return new AetheriaRuntimeMenuTabsSurfaceState(
                normalizedCurrent,
                (visibleTabs ?? Array.Empty<AetheriaRuntimeMenuTabProjectionOption>())
                    .Where(tab => tab != null)
                    .OrderBy(tab => tab.Order)
                    .ThenBy(tab => NormalizeTabKey(tab.Key), StringComparer.Ordinal)
                    .Select(tab =>
                    {
                        var key = NormalizeTabKey(tab.Key);
                        return new AetheriaRuntimeMenuTabSurfaceEntry(
                            key,
                            string.IsNullOrWhiteSpace(tab.Label) ? key : tab.Label,
                            string.Equals(key, normalizedCurrent, StringComparison.Ordinal));
                    })
                    .ToArray(),
                updatedAtUtc);
        }

        public static AetheriaRuntimeSurfaceDocument Build(
            AetheriaRuntimeMenuTabsSurfaceState state,
            long version = 1)
        {
            state ??= new AetheriaRuntimeMenuTabsSurfaceState(
                "",
                Array.Empty<AetheriaRuntimeMenuTabSurfaceEntry>(),
                "");

            var commands = state.VisibleTabs
                .Select(tab => new AetheriaRuntimeSurfaceCommandTemplate(
                    CommandFor(tab.Key),
                    LabelFor(tab),
                    AetheriaRuntimeSurfaceCommandTemplate.CultMeshTransport))
                .ToArray();
            var buttons = state.VisibleTabs
                .Select(tab => Button(
                    $"{SurfaceId}.{SafeId(tab.Key)}",
                    tab.Selected ? $"{LabelFor(tab)} *" : LabelFor(tab),
                    CommandFor(tab.Key)))
                .ToArray();

            return new AetheriaRuntimeSurfaceDocument(
                providerId: "aetheria",
                providerKind: "runtime.menu",
                title: "Runtime Menu Tabs",
                version: version,
                updatedAtUtc: state.UpdatedAtUtc,
                surface: new AetheriaRuntimeSurfaceTree(
                    SurfaceId,
                    Node(
                        $"{SurfaceId}.root",
                        "surface",
                        Array.Empty<(string Key, string Value)>(),
                        Text(
                            $"{SurfaceId}.current",
                            $"Current: {state.CurrentTabKey}"),
                        ButtonRow($"{SurfaceId}.tabs", buttons)),
                    Array.Empty<AetheriaRuntimeSurfaceStyleToken>()),
                commands: commands);
        }

        private static string LabelFor(AetheriaRuntimeMenuTabSurfaceEntry tab)
        {
            return string.IsNullOrWhiteSpace(tab.Label) ? tab.Key : tab.Label;
        }

        private static string SafeId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "empty";

            return new string(value
                .Select(character => char.IsLetterOrDigit(character) ? char.ToLowerInvariant(character) : '-')
                .ToArray()).Trim('-');
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
                props.ToDictionary(prop => prop.Key, prop => prop.Value ?? "", StringComparer.Ordinal),
                children ?? Array.Empty<AetheriaRuntimeSurfaceComponent>());
        }
    }

    public enum AetheriaRuntimeMenuTabCommandKind
    {
        Unknown = 0,
        SelectTab = 1
    }

    public readonly struct AetheriaRuntimeMenuTabCommand
    {
        public AetheriaRuntimeMenuTabCommand(
            AetheriaRuntimeMenuTabCommandKind kind,
            string tabKey)
        {
            Kind = kind;
            TabKey = tabKey ?? "";
        }

        public AetheriaRuntimeMenuTabCommandKind Kind { get; }
        public string TabKey { get; }
    }

    public static class AetheriaRuntimeMenuTabsSurfaceCommands
    {
        private const string CommandPrefix = "aetheria.runtime_menu.tab.";

        public static bool TryRead(
            EveSurfaceCommandRequest request,
            out AetheriaRuntimeMenuTabCommand command)
        {
            command = default;
            if (request == null ||
                !string.Equals(request.SurfaceId, AetheriaRuntimeMenuTabsSurfaceBuilder.SurfaceId, StringComparison.Ordinal))
                return false;

            var commandText = request.Command ?? "";
            if (!commandText.StartsWith(CommandPrefix, StringComparison.Ordinal))
                return false;

            var tabKey = commandText.Substring(CommandPrefix.Length);
            if (string.IsNullOrWhiteSpace(tabKey))
                return false;

            command = new AetheriaRuntimeMenuTabCommand(
                AetheriaRuntimeMenuTabCommandKind.SelectTab,
                tabKey);
            return true;
        }
    }
}
