using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace GameCult.Aetheria.State.Verse
{
    public sealed class AetheriaRuntimeStatRecipeSurfaceState
    {
        public AetheriaRuntimeStatRecipeSurfaceState(
            IReadOnlyList<AetheriaRuntimeStatRecipeState> recipes,
            string selectedStatName,
            AetheriaRuntimeStatRecipePreviewState preview,
            string updatedAtUtc)
        {
            Recipes = recipes ?? Array.Empty<AetheriaRuntimeStatRecipeState>();
            SelectedStatName = selectedStatName ?? "";
            Preview = preview ?? AetheriaRuntimeStatRecipePreviewState.Default;
            UpdatedAtUtc = updatedAtUtc ?? "";
        }

        public IReadOnlyList<AetheriaRuntimeStatRecipeState> Recipes { get; }

        public string SelectedStatName { get; }

        public AetheriaRuntimeStatRecipePreviewState Preview { get; }

        public string UpdatedAtUtc { get; }

        public AetheriaRuntimeStatRecipeState SelectedRecipe =>
            Recipes.FirstOrDefault(recipe => string.Equals(recipe.StatName, SelectedStatName, StringComparison.Ordinal)) ??
            Recipes.FirstOrDefault();
    }

    public sealed class AetheriaRuntimeStatRecipeState
    {
        public AetheriaRuntimeStatRecipeState(
            string recipeKey,
            string statName,
            double baseValue,
            IReadOnlyList<AetheriaRuntimeStatInfluenceState> influences)
        {
            RecipeKey = recipeKey ?? "";
            StatName = statName ?? "";
            BaseValue = baseValue;
            Influences = influences ?? Array.Empty<AetheriaRuntimeStatInfluenceState>();
        }

        public string RecipeKey { get; }

        public string StatName { get; }

        public double BaseValue { get; }

        public IReadOnlyList<AetheriaRuntimeStatInfluenceState> Influences { get; }
    }

    public sealed class AetheriaRuntimeStatInfluenceState
    {
        public AetheriaRuntimeStatInfluenceState(
            string condition,
            string operation,
            double amount,
            string curveLabel,
            double previewSample,
            bool enabled)
        {
            Condition = condition ?? "";
            Operation = operation ?? AetheriaRuntimeStatRecipeOperations.Add;
            Amount = amount;
            CurveLabel = curveLabel ?? "";
            PreviewSample = previewSample;
            Enabled = enabled;
        }

        public string Condition { get; }

        public string Operation { get; }

        public double Amount { get; }

        public string CurveLabel { get; }

        public double PreviewSample { get; }

        public bool Enabled { get; }
    }

    public sealed class AetheriaRuntimeStatRecipePreviewState
    {
        public static readonly AetheriaRuntimeStatRecipePreviewState Default =
            new AetheriaRuntimeStatRecipePreviewState(
                quality: 1,
                durability: 1,
                heat: 0,
                charge: 1,
                ammo: 1,
                range: 1,
                integrity: 1,
                pilotSkill: 0,
                environment: 0);

        public AetheriaRuntimeStatRecipePreviewState(
            double quality,
            double durability,
            double heat,
            double charge,
            double ammo,
            double range,
            double integrity,
            double pilotSkill,
            double environment)
        {
            Quality = quality;
            Durability = durability;
            Heat = heat;
            Charge = charge;
            Ammo = ammo;
            Range = range;
            Integrity = integrity;
            PilotSkill = pilotSkill;
            Environment = environment;
        }

        public double Quality { get; }

        public double Durability { get; }

        public double Heat { get; }

        public double Charge { get; }

        public double Ammo { get; }

        public double Range { get; }

        public double Integrity { get; }

        public double PilotSkill { get; }

        public double Environment { get; }

        public double GetConditionValue(string condition)
        {
            switch (condition ?? "")
            {
                case AetheriaRuntimeStatRecipeConditions.Quality:
                    return Quality;
                case AetheriaRuntimeStatRecipeConditions.Durability:
                    return Durability;
                case AetheriaRuntimeStatRecipeConditions.Heat:
                    return Heat;
                case AetheriaRuntimeStatRecipeConditions.Charge:
                    return Charge;
                case AetheriaRuntimeStatRecipeConditions.Ammo:
                    return Ammo;
                case AetheriaRuntimeStatRecipeConditions.Range:
                    return Range;
                case AetheriaRuntimeStatRecipeConditions.Integrity:
                    return Integrity;
                case AetheriaRuntimeStatRecipeConditions.PilotSkill:
                    return PilotSkill;
                case AetheriaRuntimeStatRecipeConditions.Environment:
                    return Environment;
                default:
                    return 0;
            }
        }
    }

    public static class AetheriaRuntimeStatRecipeConditions
    {
        public const string Quality = "quality";
        public const string Durability = "durability";
        public const string Heat = "heat";
        public const string Charge = "charge";
        public const string Ammo = "ammo";
        public const string Range = "range";
        public const string Integrity = "integrity";
        public const string PilotSkill = "pilotSkill";
        public const string Environment = "environment";

        public static readonly IReadOnlyList<string> All = new[]
        {
            Quality,
            Durability,
            Heat,
            Charge,
            Ammo,
            Range,
            Integrity,
            PilotSkill,
            Environment
        };
    }

    public static class AetheriaRuntimeStatRecipeOperations
    {
        public const string Add = "add";
        public const string Multiply = "multiply";
        public const string Override = "override";
    }

    public static class AetheriaRuntimeStatRecipeSurfaceBuilder
    {
        public static AetheriaRuntimeSurfaceDocument Build(
            AetheriaRuntimeStatRecipeSurfaceState state,
            long version = 1)
        {
            state ??= new AetheriaRuntimeStatRecipeSurfaceState(
                Array.Empty<AetheriaRuntimeStatRecipeState>(),
                "",
                AetheriaRuntimeStatRecipePreviewState.Default,
                "");

            var selected = state.SelectedRecipe;
            var previewValue = selected == null ? 0 : EvaluatePreview(selected, state.Preview);
            var enabledInfluenceCount = selected?.Influences.Count(influence => influence.Enabled) ?? 0;

            return new AetheriaRuntimeSurfaceDocument(
                providerId: "aetheria",
                providerKind: "game.design",
                title: "Aetheria Stat Recipes",
                version: version,
                updatedAtUtc: state.UpdatedAtUtc,
                surface: new AetheriaRuntimeSurfaceTree(
                    AetheriaRuntimeStatRecipeCommands.SurfaceId,
                    Node(
                        "aetheria.statRecipes.root",
                        "surface",
                        Array.Empty<(string Key, string Value)>(),
                        BuildSummaryCard(selected, previewValue, enabledInfluenceCount),
                        BuildRecipeList(state, selected),
                        BuildSelectedRecipeCard(selected),
                        BuildConditionPalette(selected),
                        BuildInfluenceList(selected, state.Preview),
                        BuildPreviewCard(state.Preview)),
                    Array.Empty<AetheriaRuntimeSurfaceStyleToken>()),
                commands: BuildCommandTemplates());
        }

        private static AetheriaRuntimeSurfaceComponent BuildSummaryCard(
            AetheriaRuntimeStatRecipeState selected,
            double previewValue,
            int enabledInfluenceCount)
        {
            var statName = string.IsNullOrWhiteSpace(selected?.StatName) ? "No stat selected" : selected.StatName;

            return Node(
                "aetheria.statRecipes.summary",
                "card",
                new[] { ("title", "Stat Recipe") },
                Metric("aetheria.statRecipes.summary.stat", "Selected Stat", statName),
                Metric("aetheria.statRecipes.summary.base", "Base", Format(selected?.BaseValue ?? 0)),
                Metric("aetheria.statRecipes.summary.preview", "Preview", Format(previewValue)),
                Metric("aetheria.statRecipes.summary.influences", "Active Conditions", enabledInfluenceCount.ToString()),
                Text(
                    "aetheria.statRecipes.summary.note",
                    "Each recipe owns only the conditions it actually cares about; runtime evaluation can sample those modifiers instead of walking every global performance axis."));
        }

        private static AetheriaRuntimeSurfaceComponent BuildRecipeList(
            AetheriaRuntimeStatRecipeSurfaceState state,
            AetheriaRuntimeStatRecipeState selected)
        {
            var children = state.Recipes
                .Select((recipe, index) => Button(
                    $"aetheria.statRecipes.recipe.{index}.select",
                    string.Equals(recipe.StatName, selected?.StatName, StringComparison.Ordinal) ? $"{recipe.StatName} (Selected)" : recipe.StatName,
                    AetheriaRuntimeStatRecipeCommands.SelectStat,
                    ("recipeKey", recipe.RecipeKey),
                    ("statName", recipe.StatName)))
                .Concat(new[]
                {
                    Button(
                        "aetheria.statRecipes.recipe.add",
                        "Add Stat",
                        AetheriaRuntimeStatRecipeCommands.AddStat)
                })
                .ToArray();

            return Node(
                "aetheria.statRecipes.recipes",
                "card",
                new[] { ("title", "Recipes") },
                Node("aetheria.statRecipes.recipes.list", "grid", Array.Empty<(string Key, string Value)>(), children));
        }

        private static AetheriaRuntimeSurfaceComponent BuildSelectedRecipeCard(AetheriaRuntimeStatRecipeState selected)
        {
            if (selected == null)
            {
                return Node(
                    "aetheria.statRecipes.selected",
                    "card",
                    new[] { ("title", "Selected Recipe") },
                    Text("aetheria.statRecipes.selected.empty", "Add or select a stat recipe to start authoring modifiers."));
            }

            return Node(
                "aetheria.statRecipes.selected",
                "card",
                new[] { ("title", "Selected Recipe") },
                TextInput(
                    "aetheria.statRecipes.selected.name",
                    "Stat Name",
                    selected.StatName,
                    AetheriaRuntimeStatRecipeCommands.SetStatName,
                    ("recipeKey", selected.RecipeKey),
                    ("statName", selected.StatName)),
                NumberInput(
                    "aetheria.statRecipes.selected.base",
                    "Base Value",
                    selected.BaseValue,
                    AetheriaRuntimeStatRecipeCommands.SetBaseValue,
                    ("recipeKey", selected.RecipeKey),
                    ("statName", selected.StatName)),
                ButtonRow(
                    "aetheria.statRecipes.selected.actions",
                    Button(
                        "aetheria.statRecipes.selected.remove",
                        "Remove Stat",
                        AetheriaRuntimeStatRecipeCommands.RemoveStat,
                        ("recipeKey", selected.RecipeKey),
                        ("statName", selected.StatName)),
                    Button(
                        "aetheria.statRecipes.selected.refresh",
                        "Refresh",
                        AetheriaRuntimeStatRecipeCommands.Refresh)));
        }

        private static AetheriaRuntimeSurfaceComponent BuildConditionPalette(AetheriaRuntimeStatRecipeState selected)
        {
            var children = AetheriaRuntimeStatRecipeConditions.All
                .Select(condition =>
                {
                    var influence = selected?.Influences.FirstOrDefault(candidate => string.Equals(candidate.Condition, condition, StringComparison.Ordinal));
                    var enabled = influence?.Enabled ?? false;

                    return Toggle(
                        $"aetheria.statRecipes.conditions.{condition}",
                        ConditionLabel(condition),
                        enabled,
                        AetheriaRuntimeStatRecipeCommands.ToggleCondition,
                        ("recipeKey", selected?.RecipeKey ?? ""),
                        ("statName", selected?.StatName ?? ""),
                        ("condition", condition),
                        ("enabled", enabled ? "false" : "true"));
                })
                .ToArray();

            return Node(
                "aetheria.statRecipes.conditions",
                "card",
                new[] { ("title", "Affected By") },
                Node("aetheria.statRecipes.conditions.grid", "grid", Array.Empty<(string Key, string Value)>(), children));
        }

        private static AetheriaRuntimeSurfaceComponent BuildInfluenceList(
            AetheriaRuntimeStatRecipeState selected,
            AetheriaRuntimeStatRecipePreviewState preview)
        {
            if (selected == null || selected.Influences.Count == 0)
            {
                return Node(
                    "aetheria.statRecipes.influences",
                    "card",
                    new[] { ("title", "Modifiers") },
                    Text("aetheria.statRecipes.influences.empty", "Choose a condition above to create the first modifier for this stat."));
            }

            return Node(
                "aetheria.statRecipes.influences",
                "card",
                new[] { ("title", "Modifiers") },
                Node(
                    "aetheria.statRecipes.influences.grid",
                    "grid",
                    Array.Empty<(string Key, string Value)>(),
                    selected.Influences.Select(influence => BuildInfluenceCard(selected, influence, preview)).ToArray()));
        }

        private static AetheriaRuntimeSurfaceComponent BuildInfluenceCard(
            AetheriaRuntimeStatRecipeState recipe,
            AetheriaRuntimeStatInfluenceState influence,
            AetheriaRuntimeStatRecipePreviewState preview)
        {
            var conditionValue = preview.GetConditionValue(influence.Condition);
            var sample = ResolvePreviewSample(influence, conditionValue);

            return Node(
                $"aetheria.statRecipes.influence.{StableToken(recipe.RecipeKey)}.{influence.Condition}",
                "card",
                new[] { ("title", ConditionLabel(influence.Condition)) },
                Metric(
                    $"aetheria.statRecipes.influence.{StableToken(recipe.RecipeKey)}.{influence.Condition}.operation",
                    "Operation",
                    OperationLabel(influence.Operation)),
                NumberInput(
                    $"aetheria.statRecipes.influence.{StableToken(recipe.RecipeKey)}.{influence.Condition}.amount",
                    "Amount",
                    influence.Amount,
                    AetheriaRuntimeStatRecipeCommands.SetInfluenceAmount,
                    ("recipeKey", recipe.RecipeKey),
                    ("statName", recipe.StatName),
                    ("condition", influence.Condition)),
                TextInput(
                    $"aetheria.statRecipes.influence.{StableToken(recipe.RecipeKey)}.{influence.Condition}.curve",
                    "Curve",
                    influence.CurveLabel,
                    AetheriaRuntimeStatRecipeCommands.SetInfluenceCurve,
                    ("recipeKey", recipe.RecipeKey),
                    ("statName", recipe.StatName),
                    ("condition", influence.Condition)),
                Metric(
                    $"aetheria.statRecipes.influence.{StableToken(recipe.RecipeKey)}.{influence.Condition}.sample",
                    "Preview Sample",
                    Format(sample)),
                ButtonRow(
                    $"aetheria.statRecipes.influence.{StableToken(recipe.RecipeKey)}.{influence.Condition}.actions",
                    Button(
                        $"aetheria.statRecipes.influence.{StableToken(recipe.RecipeKey)}.{influence.Condition}.operation.cycle",
                        "Cycle Operation",
                        AetheriaRuntimeStatRecipeCommands.CycleInfluenceOperation,
                        ("recipeKey", recipe.RecipeKey),
                        ("statName", recipe.StatName),
                        ("condition", influence.Condition))));
        }

        private static AetheriaRuntimeSurfaceComponent BuildPreviewCard(AetheriaRuntimeStatRecipePreviewState preview)
        {
            return Node(
                "aetheria.statRecipes.preview",
                "card",
                new[] { ("title", "Preview Context") },
                Node(
                    "aetheria.statRecipes.preview.grid",
                    "grid",
                    Array.Empty<(string Key, string Value)>(),
                    AetheriaRuntimeStatRecipeConditions.All
                        .Select(condition => Slider(
                            $"aetheria.statRecipes.preview.{condition}",
                            ConditionLabel(condition),
                            preview.GetConditionValue(condition),
                            0,
                            1,
                            AetheriaRuntimeStatRecipeCommands.SetPreviewCondition,
                            ("condition", condition)))
                        .ToArray()));
        }

        private static IReadOnlyList<AetheriaRuntimeSurfaceCommandTemplate> BuildCommandTemplates()
        {
            return new[]
            {
                new AetheriaRuntimeSurfaceCommandTemplate(AetheriaRuntimeStatRecipeCommands.Refresh, "Refresh", AetheriaRuntimeSurfaceCommandTemplate.CultMeshTransport),
                new AetheriaRuntimeSurfaceCommandTemplate(AetheriaRuntimeStatRecipeCommands.SelectStat, "Select Stat", AetheriaRuntimeSurfaceCommandTemplate.CultMeshTransport),
                new AetheriaRuntimeSurfaceCommandTemplate(AetheriaRuntimeStatRecipeCommands.AddStat, "Add Stat", AetheriaRuntimeSurfaceCommandTemplate.CultMeshTransport),
                new AetheriaRuntimeSurfaceCommandTemplate(AetheriaRuntimeStatRecipeCommands.RemoveStat, "Remove Stat", AetheriaRuntimeSurfaceCommandTemplate.CultMeshTransport),
                new AetheriaRuntimeSurfaceCommandTemplate(AetheriaRuntimeStatRecipeCommands.SetStatName, "Set Stat Name", AetheriaRuntimeSurfaceCommandTemplate.CultMeshTransport),
                new AetheriaRuntimeSurfaceCommandTemplate(AetheriaRuntimeStatRecipeCommands.SetBaseValue, "Set Base Value", AetheriaRuntimeSurfaceCommandTemplate.CultMeshTransport),
                new AetheriaRuntimeSurfaceCommandTemplate(AetheriaRuntimeStatRecipeCommands.ToggleCondition, "Toggle Condition", AetheriaRuntimeSurfaceCommandTemplate.CultMeshTransport),
                new AetheriaRuntimeSurfaceCommandTemplate(AetheriaRuntimeStatRecipeCommands.CycleInfluenceOperation, "Cycle Operation", AetheriaRuntimeSurfaceCommandTemplate.CultMeshTransport),
                new AetheriaRuntimeSurfaceCommandTemplate(AetheriaRuntimeStatRecipeCommands.SetInfluenceAmount, "Set Influence Amount", AetheriaRuntimeSurfaceCommandTemplate.CultMeshTransport),
                new AetheriaRuntimeSurfaceCommandTemplate(AetheriaRuntimeStatRecipeCommands.SetInfluenceCurve, "Set Influence Curve", AetheriaRuntimeSurfaceCommandTemplate.CultMeshTransport),
                new AetheriaRuntimeSurfaceCommandTemplate(AetheriaRuntimeStatRecipeCommands.SetPreviewCondition, "Set Preview Condition", AetheriaRuntimeSurfaceCommandTemplate.CultMeshTransport)
            };
        }

        private static double EvaluatePreview(
            AetheriaRuntimeStatRecipeState recipe,
            AetheriaRuntimeStatRecipePreviewState preview)
        {
            var value = recipe.BaseValue;

            foreach (var influence in recipe.Influences.Where(influence => influence.Enabled))
            {
                var sample = ResolvePreviewSample(influence, preview.GetConditionValue(influence.Condition));
                switch (influence.Operation)
                {
                    case AetheriaRuntimeStatRecipeOperations.Multiply:
                        value *= 1 + ((influence.Amount - 1) * sample);
                        break;
                    case AetheriaRuntimeStatRecipeOperations.Override:
                        value = Lerp(value, influence.Amount, sample);
                        break;
                    default:
                        value += influence.Amount * sample;
                        break;
                }
            }

            return value;
        }

        private static double ResolvePreviewSample(
            AetheriaRuntimeStatInfluenceState influence,
            double conditionValue)
        {
            if (influence.PreviewSample > 0)
                return Clamp01(influence.PreviewSample);

            return Clamp01(conditionValue);
        }

        private static double Clamp01(double value)
        {
            if (value < 0)
                return 0;
            if (value > 1)
                return 1;
            return value;
        }

        private static double Lerp(double from, double to, double sample)
        {
            return from + ((to - from) * Clamp01(sample));
        }

        private static string Format(double value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static string ConditionLabel(string condition)
        {
            switch (condition ?? "")
            {
                case AetheriaRuntimeStatRecipeConditions.Quality:
                    return "Quality";
                case AetheriaRuntimeStatRecipeConditions.Durability:
                    return "Durability";
                case AetheriaRuntimeStatRecipeConditions.Heat:
                    return "Heat";
                case AetheriaRuntimeStatRecipeConditions.Charge:
                    return "Charge";
                case AetheriaRuntimeStatRecipeConditions.Ammo:
                    return "Ammo";
                case AetheriaRuntimeStatRecipeConditions.Range:
                    return "Range";
                case AetheriaRuntimeStatRecipeConditions.Integrity:
                    return "Integrity";
                case AetheriaRuntimeStatRecipeConditions.PilotSkill:
                    return "Pilot Skill";
                case AetheriaRuntimeStatRecipeConditions.Environment:
                    return "Environment";
                default:
                    return condition ?? "";
            }
        }

        private static string OperationLabel(string operation)
        {
            switch (operation ?? "")
            {
                case AetheriaRuntimeStatRecipeOperations.Multiply:
                    return "Multiply";
                case AetheriaRuntimeStatRecipeOperations.Override:
                    return "Override";
                default:
                    return "Add";
            }
        }

        private static string StableToken(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "none";

            return new string(value
                .Select(character => char.IsLetterOrDigit(character) ? character : '_')
                .ToArray());
        }

        private static AetheriaRuntimeSurfaceComponent Metric(string id, string label, string value)
        {
            return Node(id, "metric", new[] { ("label", label ?? ""), ("value", value ?? "") });
        }

        private static AetheriaRuntimeSurfaceComponent Text(string id, string value)
        {
            return Node(id, "text", new[] { ("value", value ?? "") });
        }

        private static AetheriaRuntimeSurfaceComponent Button(
            string id,
            string label,
            string command,
            params (string Key, string Value)[] extraProps)
        {
            var props = new List<(string Key, string Value)>
            {
                ("label", label ?? ""),
                ("command", command ?? "")
            };
            if (extraProps != null)
            {
                props.AddRange(extraProps);
            }

            return Node(id, "control.button", props);
        }

        private static AetheriaRuntimeSurfaceComponent Toggle(
            string id,
            string label,
            bool value,
            string command,
            params (string Key, string Value)[] extraProps)
        {
            var props = new List<(string Key, string Value)>
            {
                ("label", label ?? ""),
                ("value", value ? "true" : "false"),
                ("command", command ?? "")
            };
            if (extraProps != null)
            {
                props.AddRange(extraProps);
            }

            return Node(id, "control.toggle", props);
        }

        private static AetheriaRuntimeSurfaceComponent Slider(
            string id,
            string label,
            double value,
            double min,
            double max,
            string command,
            params (string Key, string Value)[] extraProps)
        {
            var props = new List<(string Key, string Value)>
            {
                ("label", label ?? ""),
                ("value", Format(value)),
                ("min", Format(min)),
                ("max", Format(max)),
                ("command", command ?? "")
            };
            if (extraProps != null)
            {
                props.AddRange(extraProps);
            }

            return Node(id, "control.slider", props);
        }

        private static AetheriaRuntimeSurfaceComponent NumberInput(
            string id,
            string label,
            double value,
            string command,
            params (string Key, string Value)[] extraProps)
        {
            var props = new List<(string Key, string Value)>
            {
                ("label", label ?? ""),
                ("value", Format(value)),
                ("command", command ?? ""),
                ("valueKind", "number")
            };
            if (extraProps != null)
            {
                props.AddRange(extraProps);
            }

            return Node(id, "control.text", props);
        }

        private static AetheriaRuntimeSurfaceComponent TextInput(
            string id,
            string label,
            string value,
            string command,
            params (string Key, string Value)[] extraProps)
        {
            var props = new List<(string Key, string Value)>
            {
                ("label", label ?? ""),
                ("value", value ?? ""),
                ("command", command ?? "")
            };
            if (extraProps != null)
            {
                props.AddRange(extraProps);
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
                id,
                kind,
                props.ToDictionary(prop => prop.Key, prop => prop.Value, StringComparer.Ordinal),
                children ?? Array.Empty<AetheriaRuntimeSurfaceComponent>());
        }
    }
}
