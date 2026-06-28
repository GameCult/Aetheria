using System;
using System.Collections.Generic;
using System.Linq;
using GameCult.Eve.Surface;

namespace GameCult.Aetheria.State.Verse
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

    public enum AetheriaRuntimeTradeCargoTargetKind
    {
        Unknown = 0,
        DockingBay = 1,
        ShipBay = 2
    }

    public sealed class AetheriaRuntimeTradeCargoModelOption
    {
        public AetheriaRuntimeTradeCargoModelOption(
            AetheriaRuntimeTradeCargoTargetKind kind,
            string label,
            string entityKey = "",
            int shipIndex = -1,
            int bayIndex = -1,
            bool isCurrent = false)
        {
            Kind = kind;
            Label = label ?? "";
            EntityKey = entityKey ?? "";
            ShipIndex = shipIndex;
            BayIndex = bayIndex;
            IsCurrent = isCurrent;
        }

        public AetheriaRuntimeTradeCargoTargetKind Kind { get; }
        public string Label { get; }
        public string EntityKey { get; }
        public int ShipIndex { get; }
        public int BayIndex { get; }
        public bool IsCurrent { get; }
    }

    public readonly struct AetheriaRuntimeTradeCargoSelection
    {
        public AetheriaRuntimeTradeCargoSelection(
            AetheriaRuntimeTradeCargoTargetKind kind,
            string command,
            string label,
            string entityKey = "",
            int shipIndex = -1,
            int bayIndex = -1)
        {
            Kind = kind;
            Command = command ?? "";
            Label = label ?? "";
            EntityKey = entityKey ?? "";
            ShipIndex = shipIndex;
            BayIndex = bayIndex;
        }

        public AetheriaRuntimeTradeCargoTargetKind Kind { get; }
        public string Command { get; }
        public string Label { get; }
        public string EntityKey { get; }
        public int ShipIndex { get; }
        public int BayIndex { get; }
    }

    public sealed class AetheriaRuntimeTradeCargoSelectorSurfaceModel
    {
        public AetheriaRuntimeTradeCargoSelectorSurfaceModel(
            AetheriaRuntimeTradeCargoSelectorSurfaceState state,
            IReadOnlyDictionary<string, AetheriaRuntimeTradeCargoSelection> selections)
        {
            State = state ?? new AetheriaRuntimeTradeCargoSelectorSurfaceState(
                "",
                Array.Empty<AetheriaRuntimeTradeCargoTargetOption>(),
                "");
            Selections = selections ?? new Dictionary<string, AetheriaRuntimeTradeCargoSelection>(StringComparer.Ordinal);
        }

        public AetheriaRuntimeTradeCargoSelectorSurfaceState State { get; }
        public IReadOnlyDictionary<string, AetheriaRuntimeTradeCargoSelection> Selections { get; }

        public bool TryResolve(string command, out AetheriaRuntimeTradeCargoSelection selection)
        {
            return Selections.TryGetValue(command ?? "", out selection);
        }
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

        public static AetheriaRuntimeTradeCargoSelectorSurfaceModel Compose(
            string currentTarget,
            IEnumerable<AetheriaRuntimeTradeCargoModelOption> targets,
            string updatedAtUtc)
        {
            var options = new List<AetheriaRuntimeTradeCargoTargetOption>();
            var selections = new Dictionary<string, AetheriaRuntimeTradeCargoSelection>(StringComparer.Ordinal);

            foreach (var target in targets ?? Array.Empty<AetheriaRuntimeTradeCargoModelOption>())
            {
                if (target == null ||
                    target.IsCurrent ||
                    target.Kind == AetheriaRuntimeTradeCargoTargetKind.Unknown)
                {
                    continue;
                }

                var command = CommandFor(target);
                if (string.IsNullOrWhiteSpace(command))
                    continue;

                var label = string.IsNullOrWhiteSpace(target.Label)
                    ? command
                    : target.Label;
                options.Add(new AetheriaRuntimeTradeCargoTargetOption(
                    $"{SurfaceId}.{StableToken(command)}",
                    label,
                    command));
                selections[command] = new AetheriaRuntimeTradeCargoSelection(
                    target.Kind,
                    command,
                    label,
                    target.EntityKey,
                    target.ShipIndex,
                    target.BayIndex);
            }

            return new AetheriaRuntimeTradeCargoSelectorSurfaceModel(
                new AetheriaRuntimeTradeCargoSelectorSurfaceState(
                    currentTarget,
                    options,
                    updatedAtUtc),
                selections);
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
                                "The observing client lists available cargo targets; the shared runtime surface owns the cargo selector contract."),
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
                    .Select(target => new AetheriaRuntimeSurfaceCommandTemplate(target.Command, target.Label, AetheriaRuntimeSurfaceCommandTemplate.CultMeshTransport))
                    .Append(new AetheriaRuntimeSurfaceCommandTemplate(Close, "Close", AetheriaRuntimeSurfaceCommandTemplate.CultMeshTransport))
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

        private static string CommandFor(AetheriaRuntimeTradeCargoModelOption target)
        {
            switch (target.Kind)
            {
                case AetheriaRuntimeTradeCargoTargetKind.DockingBay:
                    return DockingBay;
                case AetheriaRuntimeTradeCargoTargetKind.ShipBay:
                    return target.ShipIndex >= 0 && target.BayIndex >= 0
                        ? ShipBayCommand(target.ShipIndex, target.BayIndex)
                        : "";
                default:
                    return "";
            }
        }

        private static string StableToken(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "empty";

            return new string(value
                .Select(character => char.IsLetterOrDigit(character) ? char.ToLowerInvariant(character) : '-')
                .ToArray()).Trim('-');
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

            var commandText = request.Operation?.OperationId ?? "";
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
