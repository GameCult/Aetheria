using System;
using System.Collections.Generic;
using System.Linq;
using GameCult.Eve.Surface;

namespace GameCult.Aetheria.State.Verse
{
    public sealed class AetheriaRuntimeTradeSurfaceOption
    {
        public AetheriaRuntimeTradeSurfaceOption(string id, string label, string command)
        {
            Id = id ?? "";
            Label = label ?? "";
            Command = command ?? "";
        }

        public string Id { get; }
        public string Label { get; }
        public string Command { get; }
    }

    public sealed class AetheriaRuntimeTradeSurfaceGroup
    {
        public AetheriaRuntimeTradeSurfaceGroup(
            string id,
            string title,
            IReadOnlyList<AetheriaRuntimeTradeSurfaceOption> options)
        {
            Id = id ?? "";
            Title = title ?? "";
            Options = options ?? Array.Empty<AetheriaRuntimeTradeSurfaceOption>();
        }

        public string Id { get; }
        public string Title { get; }
        public IReadOnlyList<AetheriaRuntimeTradeSurfaceOption> Options { get; }
    }

    public sealed class AetheriaRuntimeTradeFilterSurfaceState
    {
        public AetheriaRuntimeTradeFilterSurfaceState(
            string filterSummary,
            IReadOnlyList<AetheriaRuntimeTradeSurfaceGroup> groups,
            string updatedAtUtc)
        {
            FilterSummary = filterSummary ?? "";
            Groups = groups ?? Array.Empty<AetheriaRuntimeTradeSurfaceGroup>();
            UpdatedAtUtc = updatedAtUtc ?? "";
        }

        public string FilterSummary { get; }
        public IReadOnlyList<AetheriaRuntimeTradeSurfaceGroup> Groups { get; }
        public string UpdatedAtUtc { get; }
    }

    public sealed class AetheriaRuntimeTradeRowActionSurfaceState
    {
        public AetheriaRuntimeTradeRowActionSurfaceState(
            string title,
            IReadOnlyList<AetheriaRuntimeTradeSurfaceOption> actions,
            string updatedAtUtc)
        {
            Title = title ?? "";
            Actions = actions ?? Array.Empty<AetheriaRuntimeTradeSurfaceOption>();
            UpdatedAtUtc = updatedAtUtc ?? "";
        }

        public string Title { get; }
        public IReadOnlyList<AetheriaRuntimeTradeSurfaceOption> Actions { get; }
        public string UpdatedAtUtc { get; }
    }

    public static class AetheriaRuntimeTradeInteractionSurfaceBuilder
    {
        public const string FilterSurfaceId = "aetheria.trade.filter_selector";
        public const string CloseFilter = "aetheria.trade.filter_selector.close";
        public const string RowActionSurfaceId = "aetheria.trade.row_actions";
        public const string CloseRowAction = "aetheria.trade.row_actions.close";

        public static string HardpointFilterCommand(string hardpointType)
        {
            return $"{FilterSurfaceId}.hardpoint.{hardpointType ?? ""}";
        }

        public static string SimpleCommodityFilterCommand(string commodityType)
        {
            return $"{FilterSurfaceId}.simple.{commodityType ?? ""}";
        }

        public static string CompoundCommodityFilterCommand(string commodityType)
        {
            return $"{FilterSurfaceId}.compound.{commodityType ?? ""}";
        }

        public static string BehaviorFilterCommand(string behaviorKind)
        {
            return $"{FilterSurfaceId}.behavior.{behaviorKind ?? ""}";
        }

        public static string MinimumSizeFilterCommand()
        {
            return $"{FilterSurfaceId}.size.minimum";
        }

        public static string MaximumSizeFilterCommand()
        {
            return $"{FilterSurfaceId}.size.maximum";
        }

        public static string RowActionCommand(int index)
        {
            return $"{RowActionSurfaceId}.action_{index}";
        }

        public static AetheriaRuntimeSurfaceDocument BuildFilter(
            AetheriaRuntimeTradeFilterSurfaceState state,
            long version = 1)
        {
            state ??= new AetheriaRuntimeTradeFilterSurfaceState(
                "",
                Array.Empty<AetheriaRuntimeTradeSurfaceGroup>(),
                "");

            var children = new List<AetheriaRuntimeSurfaceComponent>
            {
                Card(
                    $"{FilterSurfaceId}.summary",
                    "Trade Filters",
                    Text(
                        $"{FilterSurfaceId}.note",
                        "The observing client projects available trade filters; the shared runtime surface owns the filter selector contract."),
                    Text($"{FilterSurfaceId}.active", state.FilterSummary))
            };

            foreach (var group in state.Groups)
            {
                if (group?.Options == null || group.Options.Count == 0)
                    continue;

                children.Add(Card(
                    group.Id,
                    group.Title,
                    ButtonColumn(
                        $"{group.Id}.options",
                        group.Options
                            .Select(option => Button(option.Id, option.Label, option.Command))
                            .ToArray())));
            }

            children.Add(ButtonRow(
                $"{FilterSurfaceId}.actions",
                Button($"{FilterSurfaceId}.close", "Close", CloseFilter)));

            return new AetheriaRuntimeSurfaceDocument(
                providerId: "aetheria",
                providerKind: "trade.menu",
                title: "Trade Filter Selector",
                version: version,
                updatedAtUtc: state.UpdatedAtUtc,
                surface: new AetheriaRuntimeSurfaceTree(
                    FilterSurfaceId,
                    Node(
                        $"{FilterSurfaceId}.root",
                        "surface",
                        Array.Empty<(string Key, string Value)>(),
                        children.ToArray()),
                    Array.Empty<AetheriaRuntimeSurfaceStyleToken>()),
                commands: state.Groups
                    .Where(group => group?.Options != null)
                    .SelectMany(group => group.Options)
                    .Select(option => new AetheriaRuntimeSurfaceCommandTemplate(option.Command, option.Label, AetheriaRuntimeSurfaceCommandTemplate.CultMeshTransport))
                    .Append(new AetheriaRuntimeSurfaceCommandTemplate(CloseFilter, "Close", AetheriaRuntimeSurfaceCommandTemplate.CultMeshTransport))
                    .ToArray());
        }

        public static AetheriaRuntimeSurfaceDocument BuildRowActions(
            AetheriaRuntimeTradeRowActionSurfaceState state,
            long version = 1)
        {
            state ??= new AetheriaRuntimeTradeRowActionSurfaceState(
                "",
                Array.Empty<AetheriaRuntimeTradeSurfaceOption>(),
                "");

            return new AetheriaRuntimeSurfaceDocument(
                providerId: "aetheria",
                providerKind: "trade.menu",
                title: "Trade Row Actions",
                version: version,
                updatedAtUtc: state.UpdatedAtUtc,
                surface: new AetheriaRuntimeSurfaceTree(
                    RowActionSurfaceId,
                    Node(
                        $"{RowActionSurfaceId}.root",
                        "surface",
                        Array.Empty<(string Key, string Value)>(),
                        Card(
                            $"{RowActionSurfaceId}.card",
                            "Trade Action",
                            Text($"{RowActionSurfaceId}.title", state.Title),
                            Text(
                                $"{RowActionSurfaceId}.note",
                                "The observing client projects available row actions; the shared runtime surface owns the row action contract."),
                            ButtonColumn(
                                $"{RowActionSurfaceId}.options",
                                state.Actions
                                    .Select(action => Button(action.Id, action.Label, action.Command))
                                    .ToArray()),
                            ButtonRow(
                                $"{RowActionSurfaceId}.actions",
                                Button($"{RowActionSurfaceId}.close", "Close", CloseRowAction)))),
                    Array.Empty<AetheriaRuntimeSurfaceStyleToken>()),
                commands: state.Actions
                    .Select(action => new AetheriaRuntimeSurfaceCommandTemplate(action.Command, action.Label, AetheriaRuntimeSurfaceCommandTemplate.CultMeshTransport))
                    .Append(new AetheriaRuntimeSurfaceCommandTemplate(CloseRowAction, "Close", AetheriaRuntimeSurfaceCommandTemplate.CultMeshTransport))
                    .ToArray());
        }

        private static AetheriaRuntimeSurfaceComponent Card(
            string id,
            string title,
            params AetheriaRuntimeSurfaceComponent[] children)
        {
            return Node(id, "card", new[] { ("title", title ?? "") }, children);
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

    public enum AetheriaRuntimeTradeInteractionCommandKind
    {
        Unknown = 0,
        Close = 1,
        Select = 2
    }

    public readonly struct AetheriaRuntimeTradeInteractionCommand
    {
        public AetheriaRuntimeTradeInteractionCommand(
            AetheriaRuntimeTradeInteractionCommandKind kind,
            string command)
        {
            Kind = kind;
            Command = command ?? "";
        }

        public AetheriaRuntimeTradeInteractionCommandKind Kind { get; }
        public string Command { get; }
    }

    public static class AetheriaRuntimeTradeInteractionSurfaceCommands
    {
        public static bool TryReadFilter(
            EveSurfaceCommandRequest request,
            out AetheriaRuntimeTradeInteractionCommand command)
        {
            return TryRead(
                request,
                AetheriaRuntimeTradeInteractionSurfaceBuilder.FilterSurfaceId,
                AetheriaRuntimeTradeInteractionSurfaceBuilder.CloseFilter,
                out command);
        }

        public static bool TryReadRowAction(
            EveSurfaceCommandRequest request,
            out AetheriaRuntimeTradeInteractionCommand command)
        {
            return TryRead(
                request,
                AetheriaRuntimeTradeInteractionSurfaceBuilder.RowActionSurfaceId,
                AetheriaRuntimeTradeInteractionSurfaceBuilder.CloseRowAction,
                out command);
        }

        private static bool TryRead(
            EveSurfaceCommandRequest request,
            string surfaceId,
            string closeCommand,
            out AetheriaRuntimeTradeInteractionCommand command)
        {
            command = default;
            if (request == null ||
                !string.Equals(request.SurfaceId, surfaceId, StringComparison.Ordinal))
                return false;

            var commandText = request.Command ?? "";
            if (string.Equals(commandText, closeCommand, StringComparison.Ordinal))
            {
                command = new AetheriaRuntimeTradeInteractionCommand(
                    AetheriaRuntimeTradeInteractionCommandKind.Close,
                    commandText);
                return true;
            }

            if (string.IsNullOrWhiteSpace(commandText))
                return false;

            command = new AetheriaRuntimeTradeInteractionCommand(
                AetheriaRuntimeTradeInteractionCommandKind.Select,
                commandText);
            return true;
        }
    }
}
