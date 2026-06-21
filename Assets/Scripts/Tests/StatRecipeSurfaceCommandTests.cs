using System.Linq;
using GameCult.Aetheria.State.Verse;
using NUnit.Framework;

public class StatRecipeSurfaceCommandTests
{
    private const string TestUpdatedAtUtc = "2026-06-18T00:00:00.0000000Z";

    [Test]
    public void ToggleConditionCreatesEnabledInfluenceForSelectedRecipe()
    {
        var state = StateWithRecipe();

        var next = AetheriaRuntimeStatRecipes.ToggleCondition(
            state,
            "weapon.damage",
            AetheriaRuntimeStatRecipeConditions.Range,
            updatedAtUtc: TestUpdatedAtUtc);

        var influence = next.SelectedRecipe.Influences.Single();
        Assert.AreEqual(AetheriaRuntimeStatRecipeConditions.Range, influence.Condition);
        Assert.AreEqual(AetheriaRuntimeStatRecipeOperations.Add, influence.Operation);
        Assert.IsTrue(influence.Enabled);
    }

    [Test]
    public void CycleInfluenceOperationAdvancesAddMultiplyOverride()
    {
        var state = AetheriaRuntimeStatRecipes.ToggleCondition(
            StateWithRecipe(),
            "weapon.damage",
            AetheriaRuntimeStatRecipeConditions.Quality,
            updatedAtUtc: TestUpdatedAtUtc);

        state = AetheriaRuntimeStatRecipes.CycleInfluenceOperation(
            state,
            "weapon.damage",
            AetheriaRuntimeStatRecipeConditions.Quality,
            updatedAtUtc: TestUpdatedAtUtc);

        Assert.AreEqual(AetheriaRuntimeStatRecipeOperations.Multiply, state.SelectedRecipe.Influences[0].Operation);

        state = AetheriaRuntimeStatRecipes.CycleInfluenceOperation(
            state,
            "weapon.damage",
            AetheriaRuntimeStatRecipeConditions.Quality,
            updatedAtUtc: TestUpdatedAtUtc);

        Assert.AreEqual(AetheriaRuntimeStatRecipeOperations.Override, state.SelectedRecipe.Influences[0].Operation);
    }

    [Test]
    public void SetInfluenceAmountAndCurveUpdateOnlyTargetInfluence()
    {
        var state = AetheriaRuntimeStatRecipes.ToggleCondition(
            StateWithRecipe(),
            "weapon.damage",
            AetheriaRuntimeStatRecipeConditions.Heat,
            updatedAtUtc: TestUpdatedAtUtc);

        state = AetheriaRuntimeStatRecipes.SetInfluenceAmount(
            state,
            "weapon.damage",
            AetheriaRuntimeStatRecipeConditions.Heat,
            3.5,
            updatedAtUtc: TestUpdatedAtUtc);

        state = AetheriaRuntimeStatRecipes.SetInfluenceCurve(
            state,
            "weapon.damage",
            AetheriaRuntimeStatRecipeConditions.Heat,
            "easeOut",
            updatedAtUtc: TestUpdatedAtUtc);

        var influence = state.SelectedRecipe.Influences.Single();
        Assert.AreEqual(3.5, influence.Amount, 0.0001);
        Assert.AreEqual("easeOut", influence.CurveLabel);
    }

    [Test]
    public void SetPreviewConditionUpdatesRequestedPreviewAxis()
    {
        var next = AetheriaRuntimeStatRecipes.SetPreviewCondition(
            StateWithRecipe(),
            AetheriaRuntimeStatRecipeConditions.Charge,
            0.25,
            TestUpdatedAtUtc);

        Assert.AreEqual(0.25, next.Preview.Charge, 0.0001);
        Assert.AreEqual(AetheriaRuntimeStatRecipePreviewState.Default.Quality, next.Preview.Quality, 0.0001);
    }

    private static AetheriaRuntimeStatRecipeSurfaceState StateWithRecipe()
    {
        return new AetheriaRuntimeStatRecipeSurfaceState(
            new[]
            {
                new AetheriaRuntimeStatRecipeState(
                    "weapon.damage",
                    "Weapon Damage",
                    10,
                    new AetheriaRuntimeStatInfluenceState[0])
            },
            "Weapon Damage",
            AetheriaRuntimeStatRecipePreviewState.Default,
            "");
    }
}
