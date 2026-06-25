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

    public enum AetheriaRuntimeTradeFilterSelectionKind
    {
        Unknown = 0,
        Hardpoint = 1,
        SimpleCommodity = 2,
        CompoundCommodity = 3,
        Behavior = 4,
        MinimumSize = 5,
        MaximumSize = 6
    }

    public sealed class AetheriaRuntimeTradeFilterOption
    {
        public AetheriaRuntimeTradeFilterOption(
            AetheriaRuntimeTradeFilterSelectionKind kind,
            string token,
            string label)
        {
            Kind = kind;
            Token = token ?? "";
            Label = label ?? "";
        }

        public AetheriaRuntimeTradeFilterSelectionKind Kind { get; }
        public string Token { get; }
        public string Label { get; }
    }

    public readonly struct AetheriaRuntimeTradeFilterSelection
    {
        public AetheriaRuntimeTradeFilterSelection(
            AetheriaRuntimeTradeFilterSelectionKind kind,
            string command,
            string token)
        {
            Kind = kind;
            Command = command ?? "";
            Token = token ?? "";
        }

        public AetheriaRuntimeTradeFilterSelectionKind Kind { get; }
        public string Command { get; }
        public string Token { get; }
    }

    public sealed class AetheriaRuntimeTradeFilterSurfaceProjection
    {
        public AetheriaRuntimeTradeFilterSurfaceProjection(
            AetheriaRuntimeTradeFilterSurfaceState state,
            IReadOnlyDictionary<string, AetheriaRuntimeTradeFilterSelection> selections)
        {
            State = state ?? new AetheriaRuntimeTradeFilterSurfaceState(
                "",
                Array.Empty<AetheriaRuntimeTradeSurfaceGroup>(),
                "");
            Selections = selections ?? new Dictionary<string, AetheriaRuntimeTradeFilterSelection>(StringComparer.Ordinal);
        }

        public AetheriaRuntimeTradeFilterSurfaceState State { get; }
        public IReadOnlyDictionary<string, AetheriaRuntimeTradeFilterSelection> Selections { get; }

        public bool TryResolve(string command, out AetheriaRuntimeTradeFilterSelection selection)
        {
            return Selections.TryGetValue(command ?? "", out selection);
        }
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

    public sealed class AetheriaRuntimeTradeRowActionOption
    {
        public AetheriaRuntimeTradeRowActionOption(int index, string label)
        {
            Index = index;
            Label = label ?? "";
        }

        public int Index { get; }
        public string Label { get; }
    }

    public readonly struct AetheriaRuntimeTradeRowActionSelection
    {
        public AetheriaRuntimeTradeRowActionSelection(string command, int index)
        {
            Command = command ?? "";
            Index = index;
        }

        public string Command { get; }
        public int Index { get; }
    }

    public sealed class AetheriaRuntimeTradeRowActionSurfaceProjection
    {
        public AetheriaRuntimeTradeRowActionSurfaceProjection(
            AetheriaRuntimeTradeRowActionSurfaceState state,
            IReadOnlyDictionary<string, AetheriaRuntimeTradeRowActionSelection> selections)
        {
            State = state ?? new AetheriaRuntimeTradeRowActionSurfaceState(
                "",
                Array.Empty<AetheriaRuntimeTradeSurfaceOption>(),
                "");
            Selections = selections ?? new Dictionary<string, AetheriaRuntimeTradeRowActionSelection>(StringComparer.Ordinal);
        }

        public AetheriaRuntimeTradeRowActionSurfaceState State { get; }
        public IReadOnlyDictionary<string, AetheriaRuntimeTradeRowActionSelection> Selections { get; }

        public bool TryResolve(string command, out AetheriaRuntimeTradeRowActionSelection selection)
        {
            return Selections.TryGetValue(command ?? "", out selection);
        }
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

        public static AetheriaRuntimeTradeFilterSurfaceProjection ProjectFilters(
            string filterSummary,
            IEnumerable<AetheriaRuntimeTradeFilterOption> options,
            string updatedAtUtc)
        {
            var selections = new Dictionary<string, AetheriaRuntimeTradeFilterSelection>(StringComparer.Ordinal);
            var grouped = new Dictionary<string, (AetheriaRuntimeTradeFilterSelectionKind Kind, List<AetheriaRuntimeTradeSurfaceOption> Options)>(StringComparer.Ordinal);

            foreach (var option in options ?? Array.Empty<AetheriaRuntimeTradeFilterOption>())
            {
                if (option == null || option.Kind == AetheriaRuntimeTradeFilterSelectionKind.Unknown)
                    continue;

                var command = FilterCommand(option.Kind, option.Token);
                if (string.IsNullOrWhiteSpace(command))
                    continue;

                var groupKey = FilterGroupKey(option.Kind);
                if (!grouped.TryGetValue(groupKey, out var group))
                {
                    group = (option.Kind, new List<AetheriaRuntimeTradeSurfaceOption>());
                    grouped[groupKey] = group;
                }

                group.Options.Add(new AetheriaRuntimeTradeSurfaceOption(
                    $"{FilterSurfaceId}.{groupKey}.{StableToken(option.Token)}",
                    string.IsNullOrWhiteSpace(option.Label) ? option.Token : option.Label,
                    command));
                selections[command] = new AetheriaRuntimeTradeFilterSelection(
                    option.Kind,
                    command,
                    option.Token);
            }

            var groups = grouped
                .OrderBy(entry => FilterGroupOrder(entry.Value.Kind))
                .Select(entry => new AetheriaRuntimeTradeSurfaceGroup(
                    $"{FilterSurfaceId}.{entry.Key}.card",
                    FilterGroupTitle(entry.Value.Kind),
                    entry.Value.Options
                        .OrderBy(option => option.Label, StringComparer.Ordinal)
                        .ToArray()))
                .Where(group => group.Options.Count > 0)
                .ToArray();

            return new AetheriaRuntimeTradeFilterSurfaceProjection(
                new AetheriaRuntimeTradeFilterSurfaceState(
                    filterSummary,
                    groups,
                    updatedAtUtc),
                selections);
        }

        public static AetheriaRuntimeTradeRowActionSurfaceProjection ProjectRowActions(
            string title,
            IEnumerable<AetheriaRuntimeTradeRowActionOption> actions,
            string updatedAtUtc)
        {
            var options = new List<AetheriaRuntimeTradeSurfaceOption>();
            var selections = new Dictionary<string, AetheriaRuntimeTradeRowActionSelection>(StringComparer.Ordinal);

            foreach (var action in actions ?? Array.Empty<AetheriaRuntimeTradeRowActionOption>())
            {
                if (action == null || action.Index < 0)
                    continue;

                var command = RowActionCommand(action.Index);
                var label = string.IsNullOrWhiteSpace(action.Label)
                    ? $"Action {action.Index + 1}"
                    : action.Label;
                options.Add(new AetheriaRuntimeTradeSurfaceOption(
                    $"{RowActionSurfaceId}.action_{action.Index}",
                    label,
                    command));
                selections[command] = new AetheriaRuntimeTradeRowActionSelection(command, action.Index);
            }

            return new AetheriaRuntimeTradeRowActionSurfaceProjection(
                new AetheriaRuntimeTradeRowActionSurfaceState(
                    title,
                    options,
                    updatedAtUtc),
                selections);
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

        private static string FilterCommand(AetheriaRuntimeTradeFilterSelectionKind kind, string token)
        {
            switch (kind)
            {
                case AetheriaRuntimeTradeFilterSelectionKind.Hardpoint:
                    return HardpointFilterCommand(token);
                case AetheriaRuntimeTradeFilterSelectionKind.SimpleCommodity:
                    return SimpleCommodityFilterCommand(token);
                case AetheriaRuntimeTradeFilterSelectionKind.CompoundCommodity:
                    return CompoundCommodityFilterCommand(token);
                case AetheriaRuntimeTradeFilterSelectionKind.Behavior:
                    return BehaviorFilterCommand(token);
                case AetheriaRuntimeTradeFilterSelectionKind.MinimumSize:
                    return MinimumSizeFilterCommand();
                case AetheriaRuntimeTradeFilterSelectionKind.MaximumSize:
                    return MaximumSizeFilterCommand();
                default:
                    return "";
            }
        }

        private static string FilterGroupKey(AetheriaRuntimeTradeFilterSelectionKind kind)
        {
            switch (kind)
            {
                case AetheriaRuntimeTradeFilterSelectionKind.Hardpoint:
                    return "hardpoint";
                case AetheriaRuntimeTradeFilterSelectionKind.SimpleCommodity:
                    return "simple";
                case AetheriaRuntimeTradeFilterSelectionKind.CompoundCommodity:
                    return "compound";
                case AetheriaRuntimeTradeFilterSelectionKind.Behavior:
                    return "behavior";
                case AetheriaRuntimeTradeFilterSelectionKind.MinimumSize:
                case AetheriaRuntimeTradeFilterSelectionKind.MaximumSize:
                    return "size";
                default:
                    return "unknown";
            }
        }

        private static string FilterGroupTitle(AetheriaRuntimeTradeFilterSelectionKind kind)
        {
            switch (kind)
            {
                case AetheriaRuntimeTradeFilterSelectionKind.Hardpoint:
                    return "Gear Type";
                case AetheriaRuntimeTradeFilterSelectionKind.SimpleCommodity:
                    return "Simple Commodity";
                case AetheriaRuntimeTradeFilterSelectionKind.CompoundCommodity:
                    return "Compound Commodity";
                case AetheriaRuntimeTradeFilterSelectionKind.Behavior:
                    return "Item Behavior";
                case AetheriaRuntimeTradeFilterSelectionKind.MinimumSize:
                case AetheriaRuntimeTradeFilterSelectionKind.MaximumSize:
                    return "Size";
                default:
                    return "Filters";
            }
        }

        private static int FilterGroupOrder(AetheriaRuntimeTradeFilterSelectionKind kind)
        {
            switch (kind)
            {
                case AetheriaRuntimeTradeFilterSelectionKind.Hardpoint:
                    return 0;
                case AetheriaRuntimeTradeFilterSelectionKind.SimpleCommodity:
                    return 1;
                case AetheriaRuntimeTradeFilterSelectionKind.CompoundCommodity:
                    return 2;
                case AetheriaRuntimeTradeFilterSelectionKind.Behavior:
                    return 3;
                case AetheriaRuntimeTradeFilterSelectionKind.MinimumSize:
                case AetheriaRuntimeTradeFilterSelectionKind.MaximumSize:
                    return 4;
                default:
                    return 99;
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

            var commandText = request.Operation?.OperationId ?? "";
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
