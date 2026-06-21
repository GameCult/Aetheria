using System;
using System.Collections.Generic;
using System.Linq;
using GameCult.Eve.Surface;

namespace GameCult.Aetheria.State.Unity
{
    public sealed class AetheriaRuntimeTradeCargoTargetOption
    {
        public AetheriaRuntimeTradeCargoTargetOption(string id, string label, string command)
        {
            Id = id ?? "";
            Label = label ?? "";
            Command = command ?? "";
        }

        public string Id { get; }
        public string Label { get; }
        public string Command { get; }
    }

    public sealed class AetheriaRuntimeTradeCargoSelectorSurfaceState
    {
        public AetheriaRuntimeTradeCargoSelectorSurfaceState(
            string currentTarget,
            IReadOnlyList<AetheriaRuntimeTradeCargoTargetOption> targets,
            string updatedAtUtc)
        {
            CurrentTarget = currentTarget ?? "";
            Targets = targets ?? Array.Empty<AetheriaRuntimeTradeCargoTargetOption>();
            UpdatedAtUtc = updatedAtUtc ?? "";
        }

        public string CurrentTarget { get; }
        public IReadOnlyList<AetheriaRuntimeTradeCargoTargetOption> Targets { get; }
        public string UpdatedAtUtc { get; }
    }

    public static class AetheriaRuntimeTradeCargoSelectorSurfaceBuilder
    {
        public const string SurfaceId = "aetheria.trade.target_cargo_selector";
        public const string Close = "aetheria.trade.target_cargo_selector.close";
        public const string DockingBay = "aetheria.trade.target_cargo_selector.docking_bay";

        public static string ShipBayCommand(int shipIndex, int bayIndex)
        {
            return $"{SurfaceId}.ship_{shipIndex}_bay_{bayIndex}";
        }

        public static AetheriaRuntimeSurfaceDocument Build(
            AetheriaRuntimeTradeCargoSelectorSurfaceState state,
            long version = 1)
        {
            state ??= new AetheriaRuntimeTradeCargoSelectorSurfaceState(
                "",
                Array.Empty<AetheriaRuntimeTradeCargoTargetOption>(),
                "");

            var targets = state.Targets
                .OrderBy(target => target.Label, StringComparer.Ordinal)
                .ToArray();

            return new AetheriaRuntimeSurfaceDocument(
                providerId: "aetheria",
                providerKind: "trade.menu",
                title: "Trade Target Cargo Selector",
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
                            "Target Cargo",
                            Metric($"{SurfaceId}.current", "Current", state.CurrentTarget),
                            Text(
                                $"{SurfaceId}.note",
                                "Unity projects available cargo targets; the shared runtime surface owns the cargo selector contract."),
                            ButtonColumn(
                                $"{SurfaceId}.options",
                                targets
                                    .Select(target => Button(target.Id, target.Label, target.Command))
                                    .ToArray()),
                            ButtonRow(
                                $"{SurfaceId}.actions",
                                Button($"{SurfaceId}.close", "Close", Close)))),
                    Array.Empty<AetheriaRuntimeSurfaceStyleToken>()),
                commands: targets
                    .Select(target => new AetheriaRuntimeSurfaceCommandTemplate(target.Command, target.Label, "unity-uitoolkit"))
                    .Append(new AetheriaRuntimeSurfaceCommandTemplate(Close, "Close", "unity-uitoolkit"))
                    .ToArray());
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
            return Node(id, "row", Array.Empty<(string Key, string Value)>(), children);
        }

        private static AetheriaRuntimeSurfaceComponent ButtonColumn(
            string id,
            params AetheriaRuntimeSurfaceComponent[] children)
        {
            return Node(id, "column", Array.Empty<(string Key, string Value)>(), children);
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

    public enum AetheriaRuntimeTradeCargoSelectorCommandKind
    {
        Unknown = 0,
        Close = 1,
        Select = 2
    }

    public readonly struct AetheriaRuntimeTradeCargoSelectorCommand
    {
        public AetheriaRuntimeTradeCargoSelectorCommand(
            AetheriaRuntimeTradeCargoSelectorCommandKind kind,
            string command)
        {
            Kind = kind;
            Command = command ?? "";
        }

        public AetheriaRuntimeTradeCargoSelectorCommandKind Kind { get; }
        public string Command { get; }
    }

    public static class AetheriaRuntimeTradeCargoSelectorSurfaceCommands
    {
        public static bool TryRead(
            EveSurfaceCommandRequest request,
            out AetheriaRuntimeTradeCargoSelectorCommand command)
        {
            command = default;
            if (request == null ||
                !string.Equals(request.SurfaceId, AetheriaRuntimeTradeCargoSelectorSurfaceBuilder.SurfaceId, StringComparison.Ordinal))
                return false;

            var commandText = request.Command ?? "";
            if (string.Equals(commandText, AetheriaRuntimeTradeCargoSelectorSurfaceBuilder.Close, StringComparison.Ordinal))
            {
                command = new AetheriaRuntimeTradeCargoSelectorCommand(
                    AetheriaRuntimeTradeCargoSelectorCommandKind.Close,
                    commandText);
                return true;
            }

            if (string.IsNullOrWhiteSpace(commandText))
                return false;

            command = new AetheriaRuntimeTradeCargoSelectorCommand(
                AetheriaRuntimeTradeCargoSelectorCommandKind.Select,
                commandText);
            return true;
        }
    }
}
