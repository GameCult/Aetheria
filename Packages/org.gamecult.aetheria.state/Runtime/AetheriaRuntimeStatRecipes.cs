using System;
using System.Collections.Generic;
using System.Linq;

#nullable enable

namespace GameCult.Aetheria.State.Verse
{
    public static class AetheriaRuntimeStatRecipes
    {
        public static AetheriaRuntimeStatRecipeSurfaceState Refresh(
            AetheriaRuntimeStatRecipeSurfaceState? state,
            string? updatedAtUtc = null)
        {
            state ??= EmptyState();
            return With(state, state.Recipes, state.SelectedStatName, state.Preview, Stamp(updatedAtUtc));
        }

        public static AetheriaRuntimeStatRecipeSurfaceState SelectStat(
            AetheriaRuntimeStatRecipeSurfaceState? state,
            string recipeKey,
            string statName = "",
            string? updatedAtUtc = null)
        {
            state ??= EmptyState();
            var recipe = FindRecipe(state, recipeKey, statName);
            return recipe == null
                ? state
                : With(state, state.Recipes, recipe.StatName, state.Preview, Stamp(updatedAtUtc));
        }

        public static AetheriaRuntimeStatRecipeSurfaceState AddStat(
            AetheriaRuntimeStatRecipeSurfaceState? state,
            string statName,
            string recipeKey = "",
            double baseValue = 0,
            string? updatedAtUtc = null)
        {
            state ??= EmptyState();
            recipeKey = string.IsNullOrWhiteSpace(recipeKey) ? $"design:{Guid.NewGuid():N}" : recipeKey;
            var recipe = new AetheriaRuntimeStatRecipeState(
                recipeKey,
                string.IsNullOrWhiteSpace(statName) ? "New Stat Recipe" : statName,
                baseValue,
                Array.Empty<AetheriaRuntimeStatInfluenceState>());

            return With(
                state,
                state.Recipes.Concat(new[] { recipe }).ToArray(),
                recipe.StatName,
                state.Preview,
                Stamp(updatedAtUtc));
        }

        public static AetheriaRuntimeStatRecipeSurfaceState RemoveStat(
            AetheriaRuntimeStatRecipeSurfaceState? state,
            string recipeKey,
            string statName = "",
            string? updatedAtUtc = null)
        {
            state ??= EmptyState();
            var recipe = FindRecipe(state, recipeKey, statName);
            if (recipe == null)
                return state;

            var remaining = state.Recipes
                .Where(candidate => !SameRecipe(candidate, recipe))
                .ToArray();
            return With(
                state,
                remaining,
                remaining.FirstOrDefault()?.StatName ?? "",
                state.Preview,
                Stamp(updatedAtUtc));
        }

        public static AetheriaRuntimeStatRecipeSurfaceState SetStatName(
            AetheriaRuntimeStatRecipeSurfaceState? state,
            string recipeKey,
            string statName,
            string? currentStatName = null,
            string? updatedAtUtc = null)
        {
            return UpdateRecipe(state, recipeKey, currentStatName ?? "", Stamp(updatedAtUtc), recipe =>
                new AetheriaRuntimeStatRecipeState(
                    recipe.RecipeKey,
                    statName ?? "",
                    recipe.BaseValue,
                    recipe.Influences));
        }

        public static AetheriaRuntimeStatRecipeSurfaceState SetBaseValue(
            AetheriaRuntimeStatRecipeSurfaceState? state,
            string recipeKey,
            double baseValue,
            string statName = "",
            string? updatedAtUtc = null)
        {
            return UpdateRecipe(state, recipeKey, statName, Stamp(updatedAtUtc), recipe =>
                new AetheriaRuntimeStatRecipeState(
                    recipe.RecipeKey,
                    recipe.StatName,
                    baseValue,
                    recipe.Influences));
        }

        public static AetheriaRuntimeStatRecipeSurfaceState SetConditionEnabled(
            AetheriaRuntimeStatRecipeSurfaceState? state,
            string recipeKey,
            string condition,
            bool enabled,
            string statName = "",
            string? updatedAtUtc = null)
        {
            return UpsertInfluence(
                state,
                recipeKey,
                statName,
                condition,
                Stamp(updatedAtUtc),
                influence => new AetheriaRuntimeStatInfluenceState(
                    influence.Condition,
                    influence.Operation,
                    influence.Amount,
                    influence.CurveLabel,
                    influence.PreviewSample,
                    enabled),
                enabled);
        }

        public static AetheriaRuntimeStatRecipeSurfaceState ToggleCondition(
            AetheriaRuntimeStatRecipeSurfaceState? state,
            string recipeKey,
            string condition,
            string statName = "",
            string? updatedAtUtc = null)
        {
            state ??= EmptyState();
            var recipe = FindRecipe(state, recipeKey, statName);
            var current = recipe?.Influences.FirstOrDefault(influence =>
                string.Equals(influence.Condition, NormalizeCondition(condition), StringComparison.Ordinal));
            return SetConditionEnabled(
                state,
                recipeKey,
                condition,
                current == null || !current.Enabled,
                statName,
                updatedAtUtc);
        }

        public static AetheriaRuntimeStatRecipeSurfaceState CycleInfluenceOperation(
            AetheriaRuntimeStatRecipeSurfaceState? state,
            string recipeKey,
            string condition,
            string statName = "",
            string? updatedAtUtc = null)
        {
            return UpsertInfluence(state, recipeKey, statName, condition, Stamp(updatedAtUtc), influence =>
                new AetheriaRuntimeStatInfluenceState(
                    influence.Condition,
                    NextOperation(influence.Operation),
                    influence.Amount,
                    influence.CurveLabel,
                    influence.PreviewSample,
                    influence.Enabled));
        }

        public static AetheriaRuntimeStatRecipeSurfaceState SetInfluenceAmount(
            AetheriaRuntimeStatRecipeSurfaceState? state,
            string recipeKey,
            string condition,
            double amount,
            string statName = "",
            string? updatedAtUtc = null)
        {
            return UpsertInfluence(state, recipeKey, statName, condition, Stamp(updatedAtUtc), influence =>
                new AetheriaRuntimeStatInfluenceState(
                    influence.Condition,
                    influence.Operation,
                    amount,
                    influence.CurveLabel,
                    influence.PreviewSample,
                    influence.Enabled));
        }

        public static AetheriaRuntimeStatRecipeSurfaceState SetInfluenceCurve(
            AetheriaRuntimeStatRecipeSurfaceState? state,
            string recipeKey,
            string condition,
            string curveLabel,
            string statName = "",
            string? updatedAtUtc = null)
        {
            return UpsertInfluence(state, recipeKey, statName, condition, Stamp(updatedAtUtc), influence =>
                new AetheriaRuntimeStatInfluenceState(
                    influence.Condition,
                    influence.Operation,
                    influence.Amount,
                    curveLabel ?? "",
                    influence.PreviewSample,
                    influence.Enabled));
        }

        public static AetheriaRuntimeStatRecipeSurfaceState SetPreviewCondition(
            AetheriaRuntimeStatRecipeSurfaceState? state,
            string condition,
            double value,
            string? updatedAtUtc = null)
        {
            state ??= EmptyState();
            condition = NormalizeCondition(condition);
            if (string.IsNullOrWhiteSpace(condition))
                return state;

            var preview = state.Preview;
            var next = condition switch
            {
                AetheriaRuntimeStatRecipeConditions.Quality => new AetheriaRuntimeStatRecipePreviewState(value, preview.Durability, preview.Heat, preview.Charge, preview.Ammo, preview.Range, preview.Integrity, preview.PilotSkill, preview.Environment),
                AetheriaRuntimeStatRecipeConditions.Durability => new AetheriaRuntimeStatRecipePreviewState(preview.Quality, value, preview.Heat, preview.Charge, preview.Ammo, preview.Range, preview.Integrity, preview.PilotSkill, preview.Environment),
                AetheriaRuntimeStatRecipeConditions.Heat => new AetheriaRuntimeStatRecipePreviewState(preview.Quality, preview.Durability, value, preview.Charge, preview.Ammo, preview.Range, preview.Integrity, preview.PilotSkill, preview.Environment),
                AetheriaRuntimeStatRecipeConditions.Charge => new AetheriaRuntimeStatRecipePreviewState(preview.Quality, preview.Durability, preview.Heat, value, preview.Ammo, preview.Range, preview.Integrity, preview.PilotSkill, preview.Environment),
                AetheriaRuntimeStatRecipeConditions.Ammo => new AetheriaRuntimeStatRecipePreviewState(preview.Quality, preview.Durability, preview.Heat, preview.Charge, value, preview.Range, preview.Integrity, preview.PilotSkill, preview.Environment),
                AetheriaRuntimeStatRecipeConditions.Range => new AetheriaRuntimeStatRecipePreviewState(preview.Quality, preview.Durability, preview.Heat, preview.Charge, preview.Ammo, value, preview.Integrity, preview.PilotSkill, preview.Environment),
                AetheriaRuntimeStatRecipeConditions.Integrity => new AetheriaRuntimeStatRecipePreviewState(preview.Quality, preview.Durability, preview.Heat, preview.Charge, preview.Ammo, preview.Range, value, preview.PilotSkill, preview.Environment),
                AetheriaRuntimeStatRecipeConditions.PilotSkill => new AetheriaRuntimeStatRecipePreviewState(preview.Quality, preview.Durability, preview.Heat, preview.Charge, preview.Ammo, preview.Range, preview.Integrity, value, preview.Environment),
                AetheriaRuntimeStatRecipeConditions.Environment => new AetheriaRuntimeStatRecipePreviewState(preview.Quality, preview.Durability, preview.Heat, preview.Charge, preview.Ammo, preview.Range, preview.Integrity, preview.PilotSkill, value),
                _ => preview
            };

            return With(state, state.Recipes, state.SelectedStatName, next, Stamp(updatedAtUtc));
        }

        private static AetheriaRuntimeStatRecipeSurfaceState UpsertInfluence(
            AetheriaRuntimeStatRecipeSurfaceState? state,
            string recipeKey,
            string statName,
            string condition,
            string updatedAtUtc,
            Func<AetheriaRuntimeStatInfluenceState, AetheriaRuntimeStatInfluenceState> update,
            bool defaultEnabled = true)
        {
            condition = NormalizeCondition(condition);
            if (string.IsNullOrWhiteSpace(condition))
                return state ?? EmptyState();

            return UpdateRecipe(state, recipeKey, statName, updatedAtUtc, recipe =>
            {
                var influences = recipe.Influences.ToList();
                var index = influences.FindIndex(influence => string.Equals(influence.Condition, condition, StringComparison.Ordinal));
                var current = index >= 0
                    ? influences[index]
                    : new AetheriaRuntimeStatInfluenceState(
                        condition,
                        AetheriaRuntimeStatRecipeOperations.Add,
                        0,
                        "linear",
                        0,
                        defaultEnabled);
                var next = update(current);

                if (index >= 0)
                    influences[index] = next;
                else
                    influences.Add(next);

                return new AetheriaRuntimeStatRecipeState(recipe.RecipeKey, recipe.StatName, recipe.BaseValue, influences);
            });
        }

        private static AetheriaRuntimeStatRecipeSurfaceState UpdateRecipe(
            AetheriaRuntimeStatRecipeSurfaceState? state,
            string recipeKey,
            string statName,
            string updatedAtUtc,
            Func<AetheriaRuntimeStatRecipeState, AetheriaRuntimeStatRecipeState> update)
        {
            state ??= EmptyState();
            var target = FindRecipe(state, recipeKey, statName);
            if (target == null)
                return state;

            var recipes = state.Recipes
                .Select(recipe => SameRecipe(recipe, target) ? update(recipe) : recipe)
                .ToArray();
            var selected = recipes.FirstOrDefault(recipe => string.Equals(recipe.RecipeKey, target.RecipeKey, StringComparison.Ordinal))
                ?? recipes.FirstOrDefault(recipe => string.Equals(recipe.StatName, target.StatName, StringComparison.Ordinal));

            return With(state, recipes, selected?.StatName ?? state.SelectedStatName, state.Preview, updatedAtUtc);
        }

        private static AetheriaRuntimeStatRecipeSurfaceState With(
            AetheriaRuntimeStatRecipeSurfaceState state,
            IReadOnlyList<AetheriaRuntimeStatRecipeState> recipes,
            string selectedStatName,
            AetheriaRuntimeStatRecipePreviewState preview,
            string updatedAtUtc)
        {
            return new AetheriaRuntimeStatRecipeSurfaceState(
                recipes,
                selectedStatName,
                preview ?? state.Preview,
                updatedAtUtc);
        }

        private static AetheriaRuntimeStatRecipeState? FindRecipe(
            AetheriaRuntimeStatRecipeSurfaceState state,
            string recipeKey,
            string statName)
        {
            if (!string.IsNullOrWhiteSpace(recipeKey))
            {
                var byKey = state.Recipes.FirstOrDefault(recipe => string.Equals(recipe.RecipeKey, recipeKey, StringComparison.Ordinal));
                if (byKey != null)
                    return byKey;
            }

            statName = string.IsNullOrWhiteSpace(statName) ? state.SelectedStatName : statName;
            return state.Recipes.FirstOrDefault(recipe => string.Equals(recipe.StatName, statName, StringComparison.Ordinal))
                   ?? state.SelectedRecipe;
        }

        private static bool SameRecipe(AetheriaRuntimeStatRecipeState left, AetheriaRuntimeStatRecipeState right)
        {
            if (!string.IsNullOrWhiteSpace(left.RecipeKey) || !string.IsNullOrWhiteSpace(right.RecipeKey))
                return string.Equals(left.RecipeKey, right.RecipeKey, StringComparison.Ordinal);

            return string.Equals(left.StatName, right.StatName, StringComparison.Ordinal);
        }

        private static string NormalizeCondition(string condition)
        {
            return AetheriaRuntimeStatRecipeConditions.All.FirstOrDefault(candidate =>
                       string.Equals(candidate, condition, StringComparison.OrdinalIgnoreCase)) ?? "";
        }

        private static string NextOperation(string operation)
        {
            switch (operation ?? "")
            {
                case AetheriaRuntimeStatRecipeOperations.Add:
                    return AetheriaRuntimeStatRecipeOperations.Multiply;
                case AetheriaRuntimeStatRecipeOperations.Multiply:
                    return AetheriaRuntimeStatRecipeOperations.Override;
                default:
                    return AetheriaRuntimeStatRecipeOperations.Add;
            }
        }

        private static AetheriaRuntimeStatRecipeSurfaceState EmptyState()
        {
            return new AetheriaRuntimeStatRecipeSurfaceState(
                Array.Empty<AetheriaRuntimeStatRecipeState>(),
                "",
                AetheriaRuntimeStatRecipePreviewState.Default,
                "");
        }

        private static string Stamp(string? updatedAtUtc)
        {
            return string.IsNullOrWhiteSpace(updatedAtUtc) ? DateTime.UtcNow.ToString("O") : updatedAtUtc;
        }
    }
}
