using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GameCult.Eve.Surface;

namespace GameCult.Aetheria.State.Verse
{
    public sealed class AetheriaRuntimeEquippedItemMetric
    {
        public AetheriaRuntimeEquippedItemMetric(string id, string label, string value, string valueRef = "")
        {
            Id = id ?? "";
            Label = label ?? "";
            Value = value ?? "";
            ValueRef = valueRef ?? "";
        }

        public string Id { get; }
        public string Label { get; }
        public string Value { get; }
        public string ValueRef { get; }
    }

    public sealed class AetheriaRuntimeEquippedItemSection
    {
        public AetheriaRuntimeEquippedItemSection(
            string id,
            string title,
            IReadOnlyList<AetheriaRuntimeEquippedItemMetric> metrics)
        {
            Id = id ?? "";
            Title = title ?? "";
            Metrics = metrics ?? Array.Empty<AetheriaRuntimeEquippedItemMetric>();
        }

        public string Id { get; }
        public string Title { get; }
        public IReadOnlyList<AetheriaRuntimeEquippedItemMetric> Metrics { get; }
    }

    public sealed class AetheriaRuntimeEquippedItemControl
    {
        public AetheriaRuntimeEquippedItemControl(
            string id,
            string label,
            string command,
            IReadOnlyDictionary<string, string> payload)
        {
            Id = id ?? "";
            Label = label ?? "";
            Command = command ?? "";
            Payload = payload ?? EmptyPayload;
        }

        public string Id { get; }
        public string Label { get; }
        public string Command { get; }
        public IReadOnlyDictionary<string, string> Payload { get; }

        private static readonly IReadOnlyDictionary<string, string> EmptyPayload =
            new Dictionary<string, string>(StringComparer.Ordinal);
    }

    public sealed class AetheriaRuntimeEquippedItemTemperatureControl
    {
        public AetheriaRuntimeEquippedItemTemperatureControl(
            string id,
            string label,
            string value,
            IReadOnlyDictionary<string, string> payload)
        {
            Id = id ?? "";
            Label = label ?? "";
            Value = value ?? "";
            Payload = payload ?? EmptyPayload;
        }

        public string Id { get; }
        public string Label { get; }
        public string Value { get; }
        public IReadOnlyDictionary<string, string> Payload { get; }

        private static readonly IReadOnlyDictionary<string, string> EmptyPayload =
            new Dictionary<string, string>(StringComparer.Ordinal);
    }

    public sealed class AetheriaRuntimeEquippedItemActionBarSlot
    {
        public AetheriaRuntimeEquippedItemActionBarSlot(
            string id,
            string title,
            string currentBinding,
            IReadOnlyList<AetheriaRuntimeEquippedItemControl> controls)
        {
            Id = id ?? "";
            Title = title ?? "";
            CurrentBinding = currentBinding ?? "";
            Controls = controls ?? Array.Empty<AetheriaRuntimeEquippedItemControl>();
        }

        public string Id { get; }
        public string Title { get; }
        public string CurrentBinding { get; }
        public IReadOnlyList<AetheriaRuntimeEquippedItemControl> Controls { get; }
    }

    public sealed class AetheriaRuntimeEquippedItemDetailsSurfaceState
    {
        public AetheriaRuntimeEquippedItemDetailsSurfaceState(
            string itemName,
            string description,
            string manufacturer,
            string mass,
            string durability,
            string temperature,
            string thermalRange,
            string overrideShutdown,
            string overrideShutdownLabel,
            IReadOnlyList<AetheriaRuntimeEquippedItemTemperatureControl> temperatureControls,
            IReadOnlyList<AetheriaRuntimeEquippedItemSection> behaviorSections,
            IReadOnlyList<AetheriaRuntimeEquippedItemControl> weaponGroupControls,
            IReadOnlyList<AetheriaRuntimeEquippedItemActionBarSlot> actionBarSlots,
            string updatedAtUtc)
        {
            ItemName = itemName ?? "";
            Description = description ?? "";
            Manufacturer = manufacturer ?? "";
            Mass = mass ?? "";
            Durability = durability ?? "";
            Temperature = temperature ?? "";
            ThermalRange = thermalRange ?? "";
            OverrideShutdown = overrideShutdown ?? "";
            OverrideShutdownLabel = overrideShutdownLabel ?? "";
            TemperatureControls = temperatureControls ?? Array.Empty<AetheriaRuntimeEquippedItemTemperatureControl>();
            BehaviorSections = behaviorSections ?? Array.Empty<AetheriaRuntimeEquippedItemSection>();
            WeaponGroupControls = weaponGroupControls ?? Array.Empty<AetheriaRuntimeEquippedItemControl>();
            ActionBarSlots = actionBarSlots ?? Array.Empty<AetheriaRuntimeEquippedItemActionBarSlot>();
            UpdatedAtUtc = updatedAtUtc ?? "";
        }

        public string ItemName { get; }
        public string Description { get; }
        public string Manufacturer { get; }
        public string Mass { get; }
        public string Durability { get; }
        public string Temperature { get; }
        public string ThermalRange { get; }
        public string OverrideShutdown { get; }
        public string OverrideShutdownLabel { get; }
        public IReadOnlyList<AetheriaRuntimeEquippedItemTemperatureControl> TemperatureControls { get; }
        public IReadOnlyList<AetheriaRuntimeEquippedItemSection> BehaviorSections { get; }
        public IReadOnlyList<AetheriaRuntimeEquippedItemControl> WeaponGroupControls { get; }
        public IReadOnlyList<AetheriaRuntimeEquippedItemActionBarSlot> ActionBarSlots { get; }
        public string UpdatedAtUtc { get; }
        public bool HasWeaponControls => WeaponGroupControls.Count > 0;
    }

    public sealed class AetheriaRuntimeEquippedItemObservation
    {
        public AetheriaRuntimeEquippedItemObservation(
            string itemKey,
            double quality,
            double durability,
            double temperature,
            bool overrideShutdown)
        {
            ItemKey = itemKey ?? "";
            Quality = quality;
            Durability = durability;
            Temperature = temperature;
            OverrideShutdown = overrideShutdown;
        }

        public string ItemKey { get; }
        public double Quality { get; }
        public double Durability { get; }
        public double Temperature { get; }
        public bool OverrideShutdown { get; }
    }

    public static class AetheriaRuntimeEquippedItemDetailsSurfaceBuilder
    {
        public const string SurfaceId = "aetheria.inventory.equipped_item_details";
        public const string Close = "aetheria.inventory.equipped_item_details.close";
        public const string ToggleOverrideShutdown = "aetheria.inventory.equipped_item_details.override_shutdown.toggle";
        public const string SetTargetTemperature = "aetheria.inventory.equipped_item_details.target_temperature.set";
        public const string ToggleWeaponGroup = "aetheria.inventory.equipped_item_details.weapon_group.toggle";
        public const string BindWeaponGroup = "aetheria.inventory.equipped_item_details.weapon_group.bind";
        public const string ClearActionBarBinding = "aetheria.inventory.equipped_item_details.action_bar.clear";

        public static AetheriaRuntimeEquippedItemDetailsSurfaceState Project(
            AetheriaRuntimeCatalogItem typedItem,
            AetheriaRuntimeEquippedItemObservation item,
            string title,
            string manufacturer,
            Func<float, string> formatValue,
            Func<float, string> formatTemperature,
            IReadOnlyList<AetheriaRuntimeEquippedItemTemperatureControl> temperatureControls,
            IReadOnlyList<AetheriaRuntimeEquippedItemControl> weaponGroupControls,
            IReadOnlyList<AetheriaRuntimeEquippedItemActionBarSlot> actionBarSlots,
            DateTime updatedAtUtc = default(DateTime))
        {
            if (updatedAtUtc == default(DateTime))
                updatedAtUtc = DateTime.UtcNow;

            if (typedItem == null)
            {
                return new AetheriaRuntimeEquippedItemDetailsSurfaceState(
                    title ?? "",
                    "",
                    manufacturer ?? "",
                    "",
                    "",
                    "",
                    "",
                    "",
                    "",
                    temperatureControls ?? Array.Empty<AetheriaRuntimeEquippedItemTemperatureControl>(),
                    Array.Empty<AetheriaRuntimeEquippedItemSection>(),
                    weaponGroupControls ?? Array.Empty<AetheriaRuntimeEquippedItemControl>(),
                    actionBarSlots ?? Array.Empty<AetheriaRuntimeEquippedItemActionBarSlot>(),
                    updatedAtUtc.ToString("O", CultureInfo.InvariantCulture));
            }

            var durability = item != null && item.Durability < .01
                ? "Item Destroyed!"
                : $"{(int)((item?.Durability ?? 1) / MaxDurability(typedItem, item) * 100)}%";

            return new AetheriaRuntimeEquippedItemDetailsSurfaceState(
                title ?? typedItem.Name,
                typedItem.Description ?? "",
                manufacturer ?? "",
                FormatValue(typedItem.Mass, formatValue),
                durability,
                FormatTemperature(item?.Temperature ?? 0, formatTemperature),
                FormatTemperatureRange(typedItem, formatTemperature),
                item != null && item.OverrideShutdown ? "Enabled" : "Disabled",
                item != null && item.OverrideShutdown ? "Disable Override" : "Enable Override",
                temperatureControls ?? Array.Empty<AetheriaRuntimeEquippedItemTemperatureControl>(),
                ProjectBehaviorSections(typedItem, item, formatValue, formatTemperature).ToArray(),
                weaponGroupControls ?? Array.Empty<AetheriaRuntimeEquippedItemControl>(),
                actionBarSlots ?? Array.Empty<AetheriaRuntimeEquippedItemActionBarSlot>(),
                updatedAtUtc.ToString("O", CultureInfo.InvariantCulture));
        }

        public static AetheriaRuntimeSurfaceDocument Build(
            AetheriaRuntimeEquippedItemDetailsSurfaceState state,
            long version = 1)
        {
            state ??= new AetheriaRuntimeEquippedItemDetailsSurfaceState(
                "",
                "",
                "",
                "",
                "",
                "",
                "",
                "",
                "",
                Array.Empty<AetheriaRuntimeEquippedItemTemperatureControl>(),
                Array.Empty<AetheriaRuntimeEquippedItemSection>(),
                Array.Empty<AetheriaRuntimeEquippedItemControl>(),
                Array.Empty<AetheriaRuntimeEquippedItemActionBarSlot>(),
                "");

            var children = new List<AetheriaRuntimeSurfaceComponent>
            {
                Card(
                    $"{SurfaceId}.summary",
                    state.ItemName,
                    Text(
                        $"{SurfaceId}.description",
                        string.IsNullOrWhiteSpace(state.Description)
                            ? "No typed item description is available."
                            : state.Description),
                    Text(
                        $"{SurfaceId}.note",
                        "The observing client projects the selected equipment; the shared runtime surface owns equipped-item inspection layout and commands."),
                    Metric($"{SurfaceId}.manufacturer", "Manufacturer", state.Manufacturer),
                    Metric($"{SurfaceId}.mass", "Mass", state.Mass)),
                Card(
                    $"{SurfaceId}.status.card",
                    "Status",
                    Metric($"{SurfaceId}.durability", "Durability", state.Durability),
                    Metric($"{SurfaceId}.temperature", "Temperature", state.Temperature),
                    Metric($"{SurfaceId}.thermal_range", "Thermal Range", state.ThermalRange))
            };

            foreach (var section in state.BehaviorSections)
            {
                if (section?.Metrics == null || section.Metrics.Count == 0)
                    continue;

                children.Add(Card(
                    string.IsNullOrWhiteSpace(section.Id)
                        ? $"{SurfaceId}.behavior.{SafeId(section.Title)}"
                        : section.Id,
                    section.Title,
                    section.Metrics
                        .Select(metric => Metric(metric.Id, metric.Label, metric.Value, metric.ValueRef))
                        .ToArray()));
            }

            children.Add(BuildControlsCard(state));

            if (state.HasWeaponControls)
            {
                children.Add(Card(
                    $"{SurfaceId}.weapon_groups.card",
                    "Weapon Groups",
                    Text(
                        $"{SurfaceId}.weapon_groups.note",
                        "Toggle membership directly; action-bar group binding is handled below."),
                    ButtonRow(
                        $"{SurfaceId}.weapon_groups.actions",
                        state.WeaponGroupControls.Select(Button).ToArray())));

                children.AddRange(state.ActionBarSlots.Select(BuildActionBarCard));
            }

            children.Add(ButtonRow(
                $"{SurfaceId}.actions",
                Button($"{SurfaceId}.close", "Close", Close)));

            return new AetheriaRuntimeSurfaceDocument(
                providerId: "aetheria",
                providerKind: "inventory.menu",
                title: "Inventory Equipped Item Details",
                version: version,
                updatedAtUtc: state.UpdatedAtUtc,
                surface: new AetheriaRuntimeSurfaceTree(
                    SurfaceId,
                    Node(
                        $"{SurfaceId}.root",
                        "surface",
                        Array.Empty<(string Key, string Value)>(),
                        children.ToArray()),
                    Array.Empty<AetheriaRuntimeSurfaceStyleToken>()),
                commands: new[]
                {
                    new AetheriaRuntimeSurfaceCommandTemplate(Close, "Close", AetheriaRuntimeSurfaceCommandTemplate.CultMeshTransport),
                    new AetheriaRuntimeSurfaceCommandTemplate(ToggleOverrideShutdown, "Toggle Override Shutdown", AetheriaRuntimeSurfaceCommandTemplate.CultMeshTransport),
                    new AetheriaRuntimeSurfaceCommandTemplate(SetTargetTemperature, "Set Target Temperature", AetheriaRuntimeSurfaceCommandTemplate.CultMeshTransport),
                    new AetheriaRuntimeSurfaceCommandTemplate(ToggleWeaponGroup, "Toggle Weapon Group", AetheriaRuntimeSurfaceCommandTemplate.CultMeshTransport),
                    new AetheriaRuntimeSurfaceCommandTemplate(BindWeaponGroup, "Bind Weapon Group", AetheriaRuntimeSurfaceCommandTemplate.CultMeshTransport),
                    new AetheriaRuntimeSurfaceCommandTemplate(ClearActionBarBinding, "Clear Action Bar Binding", AetheriaRuntimeSurfaceCommandTemplate.CultMeshTransport)
                });
        }

        public static IReadOnlyDictionary<string, string> Payload(params (string Key, string Value)[] values)
        {
            return (values ?? Array.Empty<(string Key, string Value)>())
                .ToDictionary(value => value.Key ?? "", value => value.Value ?? "", StringComparer.Ordinal);
        }

        private static AetheriaRuntimeSurfaceComponent BuildControlsCard(
            AetheriaRuntimeEquippedItemDetailsSurfaceState state)
        {
            var children = new List<AetheriaRuntimeSurfaceComponent>
            {
                Metric(
                    $"{SurfaceId}.controls.override_shutdown.metric",
                    "Override Shutdown",
                    state.OverrideShutdown),
                ButtonRow(
                    $"{SurfaceId}.controls.override_shutdown.actions",
                    Button(
                        $"{SurfaceId}.controls.override_shutdown.toggle",
                        string.IsNullOrWhiteSpace(state.OverrideShutdownLabel)
                            ? "Toggle Override"
                            : state.OverrideShutdownLabel,
                        ToggleOverrideShutdown))
            };

            children.AddRange(state.TemperatureControls.Select(control => Metric(
                $"{control.Id}.metric",
                control.Label,
                control.Value)));
            children.AddRange(state.TemperatureControls.Select(control => TextField(
                control.Id,
                control.Label,
                SetTargetTemperature,
                control.Value,
                control.Payload)));

            return Card($"{SurfaceId}.controls.card", "Controls", children.ToArray());
        }

        private static AetheriaRuntimeSurfaceComponent BuildActionBarCard(
            AetheriaRuntimeEquippedItemActionBarSlot slot)
        {
            return Card(
                slot.Id,
                slot.Title,
                Metric($"{slot.Id}.binding", "Current", slot.CurrentBinding),
                ButtonRow($"{slot.Id}.actions", slot.Controls.Select(Button).ToArray()));
        }

        private static string SafeId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "empty";

            return new string(value
                .Select(character => char.IsLetterOrDigit(character) ? char.ToLowerInvariant(character) : '-')
                .ToArray()).Trim('-');
        }

        private static IEnumerable<AetheriaRuntimeEquippedItemSection> ProjectBehaviorSections(
            AetheriaRuntimeCatalogItem typedItem,
            AetheriaRuntimeEquippedItemObservation item,
            Func<float, string> formatValue,
            Func<float, string> formatTemperature)
        {
            foreach (var behavior in typedItem.BehaviorPayloads ?? Array.Empty<AetheriaRuntimeBehaviorPayload>())
            {
                if (string.Equals(behavior.Kind, AetheriaRuntimeBehaviorKinds.StatModifier, StringComparison.Ordinal))
                {
                    var statReference = ReadStatReference(FindTypedBehaviorField(behavior, 1)?.Value);
                    var modifier = ReadPerformanceStat(FindTypedBehaviorField(behavior, 2)?.Value);
                    var modifierType = ReadEnum(
                        FindTypedBehaviorField(behavior, 3)?.Value,
                        AetheriaRuntimeEquippedItemStatModifierType.Constant);
                    yield return new AetheriaRuntimeEquippedItemSection(
                        $"{SurfaceId}.behavior.{behavior.Kind}.stat_modifier",
                        "Stat Modifier",
                        new[]
                        {
                            new AetheriaRuntimeEquippedItemMetric(
                                $"{SurfaceId}.behavior.{behavior.Kind}.target",
                                $"{SplitCamelCase(statReference.Target)}:{SplitCamelCase(statReference.Stat)}",
                                $"{(modifierType == AetheriaRuntimeEquippedItemStatModifierType.Constant ? "+" : "x")}{FormatCurrentItemStat(modifier, item, formatValue)}",
                                AetheriaRuntimeDaemonItemStatQueries.ItemStatRef(
                                    item?.ItemKey ?? "",
                                    behavior.Kind,
                                    behavior.Group,
                                    2))
                        });
                    continue;
                }

                var metadata = AetheriaRuntimeBehaviorMetadataCatalog.Get(behavior.Kind);
                if (metadata == null)
                    continue;

                var fields = metadata.DisplayFields
                    .Select(field => ProjectBehaviorMetric(behavior, field, item, formatValue, formatTemperature))
                    .Where(metric => metric != null)
                    .ToArray();

                if (fields.Length == 0)
                    continue;

                yield return new AetheriaRuntimeEquippedItemSection(
                    $"{SurfaceId}.behavior.{behavior.Kind}",
                    FormatTypeName(behavior.Kind),
                    fields);
            }
        }

        private static AetheriaRuntimeEquippedItemMetric ProjectBehaviorMetric(
            AetheriaRuntimeBehaviorPayload behavior,
            AetheriaRuntimeBehaviorFieldMetadata field,
            AetheriaRuntimeEquippedItemObservation item,
            Func<float, string> formatValue,
            Func<float, string> formatTemperature)
        {
            var payloadField = FindTypedBehaviorField(behavior, field.Key);
            if (payloadField == null)
                return null;

            string value;
            switch (field.ValueKind)
            {
                case AetheriaRuntimeBehaviorFieldValueKind.Number:
                    value = FormatValue(payloadField.Value.NumberValue, formatValue);
                    break;
                case AetheriaRuntimeBehaviorFieldValueKind.Temperature:
                    value = FormatTemperature(payloadField.Value.NumberValue, formatTemperature);
                    break;
                case AetheriaRuntimeBehaviorFieldValueKind.Integer:
                    value = ((int)payloadField.Value.NumberValue).ToString(CultureInfo.InvariantCulture);
                    break;
                case AetheriaRuntimeBehaviorFieldValueKind.PerformanceStat:
                    value = FormatCurrentItemStat(payloadField.Value, item, formatValue);
                    break;
                default:
                    return null;
            }

            return new AetheriaRuntimeEquippedItemMetric(
                $"{SurfaceId}.behavior.{behavior.Kind}.{field.Key}",
                SplitCamelCase(field.Name),
                value,
                field.ValueKind == AetheriaRuntimeBehaviorFieldValueKind.PerformanceStat
                    ? AetheriaRuntimeDaemonItemStatQueries.ItemStatRef(
                        item?.ItemKey ?? "",
                        behavior.Kind,
                        behavior.Group,
                        field.Key)
                    : "");
        }

        private static string FormatCurrentItemStat(
            AetheriaRuntimePerformanceStat stat,
            AetheriaRuntimeEquippedItemObservation item,
            Func<float, string> formatValue)
        {
            return FormatValue(
                AetheriaRuntimeDaemonItemStatQueries.EvaluatePerformanceStat(
                    ToBehaviorValue(stat),
                    ToLoadoutItem(item),
                    item?.Temperature ?? 0),
                formatValue);
        }

        private static string FormatCurrentItemStat(
            AetheriaRuntimeBehaviorValue stat,
            AetheriaRuntimeEquippedItemObservation item,
            Func<float, string> formatValue)
        {
            return FormatValue(
                AetheriaRuntimeDaemonItemStatQueries.EvaluatePerformanceStat(
                    stat,
                    ToLoadoutItem(item),
                    item?.Temperature ?? 0),
                formatValue);
        }

        private static AetheriaRuntimeLoadoutItemCommit ToLoadoutItem(AetheriaRuntimeEquippedItemObservation item)
        {
            return AetheriaRuntimeDaemonItemStatQueries.ItemCommit(
                item?.ItemKey ?? "",
                item?.Quality ?? 1,
                item?.Durability ?? 1,
                enabled: true,
                overrideShutdown: item != null && item.OverrideShutdown);
        }

        private static double MaxDurability(
            AetheriaRuntimeCatalogItem typedItem,
            AetheriaRuntimeEquippedItemObservation item)
        {
            if (typedItem != null && typedItem.Durability > 0)
                return typedItem.Durability;

            return Math.Max(item?.Durability ?? 1, 1);
        }

        private static string FormatTemperatureRange(
            AetheriaRuntimeCatalogItem item,
            Func<float, string> formatTemperature)
        {
            if (item.MaximumTemperature > item.MinimumTemperature)
            {
                return
                    $"{FormatTemperature(item.MinimumTemperature, formatTemperature)} to " +
                    $"{FormatTemperature(item.MaximumTemperature, formatTemperature)}";
            }

            return "No typed thermal range";
        }

        private static AetheriaRuntimeBehaviorField FindTypedBehaviorField(
            AetheriaRuntimeBehaviorPayload behavior,
            int? key)
        {
            return key == null
                ? null
                : behavior?.Fields?.FirstOrDefault(field => field.Key == key.Value);
        }

        private static AetheriaRuntimePerformanceStat ReadPerformanceStat(AetheriaRuntimeBehaviorValue value)
        {
            return new AetheriaRuntimePerformanceStat(
                ChildNumber(value, 0),
                ChildNumber(value, 1),
                ChildNumber(value, 2),
                ChildNumber(value, 3),
                ChildNumber(value, 4),
                ReadStatRecipe(ChildValue(value, 5)));
        }

        private static AetheriaRuntimeStatRecipe ReadStatRecipe(AetheriaRuntimeBehaviorValue value)
        {
            if (value == null || value.Children.Count == 0)
                return null;

            var modifiers = ChildValue(value, 1)?.Children
                .Select(ReadStatRecipeModifier)
                .Where(modifier => modifier != null)
                .ToArray() ?? Array.Empty<AetheriaRuntimeStatRecipeModifier>();

            return new AetheriaRuntimeStatRecipe(ChildNumber(value, 0), modifiers);
        }

        private static AetheriaRuntimeStatRecipeModifier ReadStatRecipeModifier(AetheriaRuntimeBehaviorValue value)
        {
            if (value == null)
                return null;

            return new AetheriaRuntimeStatRecipeModifier(
                ChildString(value, 0),
                ChildString(value, 1),
                ChildNumber(value, 2),
                ReadCurveKeys(ChildValue(value, 3)),
                value.Children.Count <= 4 || ChildValue(value, 4)?.BoolValue == true);
        }

        private static IReadOnlyList<AetheriaRuntimeCurveKey> ReadCurveKeys(AetheriaRuntimeBehaviorValue value)
        {
            if (value?.Children == null || value.Children.Count == 0)
                return Array.Empty<AetheriaRuntimeCurveKey>();

            return value.Children
                .Where(key => key.Children.Count >= 4)
                .Select(key => new AetheriaRuntimeCurveKey(
                    ChildNumber(key, 0),
                    ChildNumber(key, 1),
                    ChildNumber(key, 2),
                    ChildNumber(key, 3)))
                .ToArray();
        }

        private static AetheriaRuntimeEquippedItemStatReference ReadStatReference(AetheriaRuntimeBehaviorValue value)
        {
            return new AetheriaRuntimeEquippedItemStatReference(
                ChildString(value, 1),
                ChildString(value, 2));
        }

        private static T ReadEnum<T>(AetheriaRuntimeBehaviorValue value, T fallback) where T : struct
        {
            if (!string.IsNullOrWhiteSpace(value?.StringValue) && Enum.TryParse(value.StringValue, true, out T parsed))
                return parsed;

            return value != null && Enum.IsDefined(typeof(T), (int)value.NumberValue)
                ? (T)Enum.ToObject(typeof(T), (int)value.NumberValue)
                : fallback;
        }

        private static AetheriaRuntimeBehaviorValue ToBehaviorValue(AetheriaRuntimePerformanceStat stat)
        {
            return new AetheriaRuntimeBehaviorValue(
                "performance-stat",
                "",
                0,
                false,
                "",
                "",
                new[]
                {
                    Number(stat?.Min ?? 0),
                    Number(stat?.Max ?? 0),
                    Number(stat?.HeatExponentMultiplier ?? 0),
                    Number(stat?.DurabilityExponentMultiplier ?? 0),
                    Number(stat?.QualityExponent ?? 0),
                    StatRecipeValue(stat?.Recipe)
                },
                EmptyMapEntries());
        }

        private static AetheriaRuntimeBehaviorValue StatRecipeValue(AetheriaRuntimeStatRecipe recipe)
        {
            if (recipe == null)
                return EmptyValue("stat-recipe");

            return new AetheriaRuntimeBehaviorValue(
                "stat-recipe",
                "",
                0,
                false,
                "",
                "",
                new[]
                {
                    Number(recipe.BaseValue),
                    new AetheriaRuntimeBehaviorValue(
                        "stat-recipe-modifiers",
                        "",
                        0,
                        false,
                        "",
                        "",
                        (recipe.Modifiers ?? Array.Empty<AetheriaRuntimeStatRecipeModifier>())
                            .Select(StatRecipeModifierValue)
                            .ToArray(),
                        EmptyMapEntries())
                },
                EmptyMapEntries());
        }

        private static AetheriaRuntimeBehaviorValue StatRecipeModifierValue(AetheriaRuntimeStatRecipeModifier modifier)
        {
            if (modifier == null)
                return EmptyValue("stat-recipe-modifier");

            return new AetheriaRuntimeBehaviorValue(
                "stat-recipe-modifier",
                "",
                0,
                false,
                "",
                "",
                new[]
                {
                    TextValue(modifier.Condition),
                    TextValue(modifier.Operation),
                    Number(modifier.Amount),
                    CurveValue(modifier.CurveKeys),
                    BoolValue(modifier.Enabled)
                },
                EmptyMapEntries());
        }

        private static AetheriaRuntimeBehaviorValue CurveValue(IReadOnlyList<AetheriaRuntimeCurveKey> keys)
        {
            return new AetheriaRuntimeBehaviorValue(
                "curve",
                "",
                0,
                false,
                "",
                "",
                (keys ?? Array.Empty<AetheriaRuntimeCurveKey>())
                    .Select(key => new AetheriaRuntimeBehaviorValue(
                        "curve-key",
                        "",
                        0,
                        false,
                        "",
                        "",
                        new[]
                        {
                            Number(key.Time),
                            Number(key.Value),
                            Number(key.InTangent),
                            Number(key.OutTangent)
                        },
                        EmptyMapEntries()))
                    .ToArray(),
                EmptyMapEntries());
        }

        private static AetheriaRuntimeBehaviorValue Number(double value)
        {
            return new AetheriaRuntimeBehaviorValue("", "", value, false, "", "", Array.Empty<AetheriaRuntimeBehaviorValue>(), EmptyMapEntries());
        }

        private static AetheriaRuntimeBehaviorValue TextValue(string value)
        {
            return new AetheriaRuntimeBehaviorValue("", value ?? "", 0, false, "", "", Array.Empty<AetheriaRuntimeBehaviorValue>(), EmptyMapEntries());
        }

        private static AetheriaRuntimeBehaviorValue BoolValue(bool value)
        {
            return new AetheriaRuntimeBehaviorValue("", "", 0, value, "", "", Array.Empty<AetheriaRuntimeBehaviorValue>(), EmptyMapEntries());
        }

        private static AetheriaRuntimeBehaviorValue EmptyValue(string kind)
        {
            return new AetheriaRuntimeBehaviorValue(kind ?? "", "", 0, false, "", "", Array.Empty<AetheriaRuntimeBehaviorValue>(), EmptyMapEntries());
        }

        private static IReadOnlyList<AetheriaRuntimeBehaviorMapEntry> EmptyMapEntries()
        {
            return Array.Empty<AetheriaRuntimeBehaviorMapEntry>();
        }

        private static double ChildNumber(AetheriaRuntimeBehaviorValue value, int index)
        {
            return value != null && value.Children.Count > index ? value.Children[index].NumberValue : 0;
        }

        private static string ChildString(AetheriaRuntimeBehaviorValue value, int index)
        {
            return value != null && value.Children.Count > index ? value.Children[index].StringValue ?? "" : "";
        }

        private static AetheriaRuntimeBehaviorValue ChildValue(AetheriaRuntimeBehaviorValue value, int index)
        {
            return value != null && value.Children.Count > index ? value.Children[index] : null;
        }

        private static string FormatValue(
            double value,
            Func<float, string> formatValue)
        {
            return formatValue == null
                ? value.ToString("0.###", CultureInfo.InvariantCulture)
                : formatValue((float)value);
        }

        private static string FormatTemperature(
            double value,
            Func<float, string> formatTemperature)
        {
            return formatTemperature == null
                ? value.ToString("0.###", CultureInfo.InvariantCulture)
                : formatTemperature((float)value);
        }

        private static string FormatTypeName(string typeName)
        {
            if (string.IsNullOrEmpty(typeName))
                return "";

            return SplitCamelCase(typeName.StartsWith("I", StringComparison.Ordinal) && typeName.Length > 1
                ? typeName.Substring(1)
                : typeName);
        }

        private static string SplitCamelCase(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "";

            return string.Concat(value.Select((character, index) =>
                index > 0 && char.IsUpper(character) ? " " + character : character.ToString()));
        }

        private static AetheriaRuntimeSurfaceComponent Card(
            string id,
            string title,
            params AetheriaRuntimeSurfaceComponent[] children)
        {
            return Node(id, "card", new[] { ("title", title ?? "") }, children);
        }

        private static AetheriaRuntimeSurfaceComponent Metric(string id, string label, string value, string valueRef = "")
        {
            var props = new List<(string Key, string Value)>
            {
                ("label", label ?? ""),
                ("value", value ?? "")
            };

            if (!string.IsNullOrWhiteSpace(valueRef))
                props.Add(AetheriaRuntimeSurfaceStateRefs.ValueRef(valueRef));

            return Node(id, "metric", props);
        }

        private static AetheriaRuntimeSurfaceComponent Text(string id, string value)
        {
            return Node(id, "text", new[] { ("value", value ?? "") });
        }

        private static AetheriaRuntimeSurfaceComponent Button(AetheriaRuntimeEquippedItemControl control)
        {
            return CommandButton(control.Id, control.Label, control.Command, control.Payload);
        }

        private static AetheriaRuntimeSurfaceComponent Button(string id, string label, string command)
        {
            return Node(id, "control.button", new[] { ("label", label ?? ""), ("command", command ?? "") });
        }

        private static AetheriaRuntimeSurfaceComponent CommandButton(
            string id,
            string label,
            string command,
            IReadOnlyDictionary<string, string> payload)
        {
            var props = new List<(string Key, string Value)>
            {
                ("label", label ?? ""),
                ("command", command ?? "")
            };

            if (payload != null)
            {
                props.AddRange(payload.Select(entry => (entry.Key, entry.Value)));
            }

            return Node(id, "control.button", props);
        }

        private static AetheriaRuntimeSurfaceComponent TextField(
            string id,
            string label,
            string command,
            string value,
            IReadOnlyDictionary<string, string> payload)
        {
            var props = new List<(string Key, string Value)>
            {
                ("label", label ?? ""),
                ("command", command ?? ""),
                ("value", value ?? "")
            };

            if (payload != null)
            {
                props.AddRange(payload.Select(entry => (entry.Key, entry.Value)));
            }

            return Node(id, "control.text", props);
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
                id ?? "",
                kind ?? "",
                (props ?? Array.Empty<(string Key, string Value)>())
                    .ToDictionary(prop => prop.Key, prop => prop.Value ?? "", StringComparer.Ordinal),
                children ?? Array.Empty<AetheriaRuntimeSurfaceComponent>());
        }
    }

    public enum AetheriaRuntimeEquippedItemDetailsCommandKind
    {
        Unknown = 0,
        Close = 1,
        ToggleOverrideShutdown = 2,
        SetTargetTemperature = 3,
        ToggleWeaponGroup = 4,
        BindWeaponGroup = 5,
        ClearActionBarBinding = 6
    }

    internal enum AetheriaRuntimeEquippedItemStatModifierType
    {
        Constant = 0,
        Multiplier = 1
    }

    internal readonly struct AetheriaRuntimeEquippedItemStatReference
    {
        public AetheriaRuntimeEquippedItemStatReference(string target, string stat)
        {
            Target = target ?? "";
            Stat = stat ?? "";
        }

        public string Target { get; }
        public string Stat { get; }
    }

    public readonly struct AetheriaRuntimeEquippedItemDetailsCommand
    {
        public AetheriaRuntimeEquippedItemDetailsCommand(
            AetheriaRuntimeEquippedItemDetailsCommandKind kind,
            int behaviorIndex = -1,
            float targetTemperature = 0f,
            int groupIndex = -1,
            int slotIndex = -1)
        {
            Kind = kind;
            BehaviorIndex = behaviorIndex;
            TargetTemperature = targetTemperature;
            GroupIndex = groupIndex;
            SlotIndex = slotIndex;
        }

        public AetheriaRuntimeEquippedItemDetailsCommandKind Kind { get; }
        public int BehaviorIndex { get; }
        public float TargetTemperature { get; }
        public int GroupIndex { get; }
        public int SlotIndex { get; }
    }

    public static class AetheriaRuntimeEquippedItemDetailsSurfaceCommands
    {
        public static bool TryRead(
            EveSurfaceCommandRequest request,
            out AetheriaRuntimeEquippedItemDetailsCommand command)
        {
            command = default;
            if (request == null ||
                !string.Equals(request.SurfaceId, AetheriaRuntimeEquippedItemDetailsSurfaceBuilder.SurfaceId, StringComparison.Ordinal))
                return false;

            switch (request.Command ?? "")
            {
                case AetheriaRuntimeEquippedItemDetailsSurfaceBuilder.Close:
                    command = new AetheriaRuntimeEquippedItemDetailsCommand(
                        AetheriaRuntimeEquippedItemDetailsCommandKind.Close);
                    return true;
                case AetheriaRuntimeEquippedItemDetailsSurfaceBuilder.ToggleOverrideShutdown:
                    command = new AetheriaRuntimeEquippedItemDetailsCommand(
                        AetheriaRuntimeEquippedItemDetailsCommandKind.ToggleOverrideShutdown);
                    return true;
                case AetheriaRuntimeEquippedItemDetailsSurfaceBuilder.SetTargetTemperature:
                    command = new AetheriaRuntimeEquippedItemDetailsCommand(
                        AetheriaRuntimeEquippedItemDetailsCommandKind.SetTargetTemperature,
                        behaviorIndex: ReadInt(request, "behaviorIndex", -1),
                        targetTemperature: ReadFloat(request, "value", 0f));
                    return true;
                case AetheriaRuntimeEquippedItemDetailsSurfaceBuilder.ToggleWeaponGroup:
                    command = new AetheriaRuntimeEquippedItemDetailsCommand(
                        AetheriaRuntimeEquippedItemDetailsCommandKind.ToggleWeaponGroup,
                        groupIndex: ReadInt(request, "group", -1));
                    return true;
                case AetheriaRuntimeEquippedItemDetailsSurfaceBuilder.BindWeaponGroup:
                    command = new AetheriaRuntimeEquippedItemDetailsCommand(
                        AetheriaRuntimeEquippedItemDetailsCommandKind.BindWeaponGroup,
                        groupIndex: ReadInt(request, "group", -1),
                        slotIndex: ReadInt(request, "slot", -1));
                    return true;
                case AetheriaRuntimeEquippedItemDetailsSurfaceBuilder.ClearActionBarBinding:
                    command = new AetheriaRuntimeEquippedItemDetailsCommand(
                        AetheriaRuntimeEquippedItemDetailsCommandKind.ClearActionBarBinding,
                        slotIndex: ReadInt(request, "slot", -1));
                    return true;
                default:
                    return false;
            }
        }

        private static int ReadInt(EveSurfaceCommandRequest request, string key, int defaultValue)
        {
            return request.Payload != null &&
                   request.Payload.TryGetValue(key, out var raw) &&
                   int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
                ? value
                : defaultValue;
        }

        private static float ReadFloat(EveSurfaceCommandRequest request, string key, float defaultValue)
        {
            return request.Payload != null &&
                   request.Payload.TryGetValue(key, out var raw) &&
                   float.TryParse(raw, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var value)
                ? value
                : defaultValue;
        }
    }
}
