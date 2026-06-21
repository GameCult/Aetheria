using System;
using GameCult.Aetheria.State.Verse;
using NUnit.Framework;

public class StatRecipeTests
{
    [Test]
    public void AddModifierScalesByConditionSample()
    {
        var recipe = new StatRecipe
        {
            BaseValue = 10,
            Modifiers = new[]
            {
                new StatRecipeModifier
                {
                    Condition = StatConditionMask.Quality,
                    Operation = StatModifierOperation.Add,
                    Amount = 6
                }
            }
        };

        var value = recipe.Evaluate(new StatEvaluationContext(quality: 0.5f, durability: 1, heat: 1));

        Assert.AreEqual(13, value, 0.0001f);
    }

    [Test]
    public void MultiplyModifierInterpolatesFromIdentity()
    {
        var recipe = new StatRecipe
        {
            BaseValue = 10,
            Modifiers = new[]
            {
                new StatRecipeModifier
                {
                    Condition = StatConditionMask.Durability,
                    Operation = StatModifierOperation.Multiply,
                    Amount = 2
                }
            }
        };

        var value = recipe.Evaluate(new StatEvaluationContext(quality: 1, durability: 0.25f, heat: 1));

        Assert.AreEqual(12.5f, value, 0.0001f);
    }

    [Test]
    public void OverrideModifierInterpolatesTowardTargetValue()
    {
        var recipe = new StatRecipe
        {
            BaseValue = 10,
            Modifiers = new[]
            {
                new StatRecipeModifier
                {
                    Condition = StatConditionMask.Heat,
                    Operation = StatModifierOperation.Override,
                    Amount = 20
                }
            }
        };

        var value = recipe.Evaluate(new StatEvaluationContext(quality: 1, durability: 1, heat: 0.25f));

        Assert.AreEqual(12.5f, value, 0.0001f);
    }

    [Test]
    public void DependencyMaskOnlyIncludesEnabledConditions()
    {
        var recipe = new StatRecipe
        {
            Modifiers = new[]
            {
                new StatRecipeModifier
                {
                    Condition = StatConditionMask.Quality,
                    Enabled = false
                },
                new StatRecipeModifier
                {
                    Condition = StatConditionMask.Range,
                    Enabled = true
                }
            }
        };

        Assert.AreEqual(StatConditionMask.Range, recipe.DependencyMask);
    }

    [Test]
    public void DisabledAndConditionlessModifiersDoNotAffectEvaluation()
    {
        var recipe = new StatRecipe
        {
            BaseValue = 10,
            Modifiers = new[]
            {
                new StatRecipeModifier
                {
                    Condition = StatConditionMask.Quality,
                    Operation = StatModifierOperation.Add,
                    Amount = 100,
                    Enabled = false
                },
                new StatRecipeModifier
                {
                    Condition = StatConditionMask.None,
                    Operation = StatModifierOperation.Add,
                    Amount = 100,
                    Enabled = true
                },
                new StatRecipeModifier
                {
                    Condition = StatConditionMask.Heat,
                    Operation = StatModifierOperation.Add,
                    Amount = 4,
                    Enabled = true
                }
            }
        };

        var value = recipe.Evaluate(new StatEvaluationContext(quality: 1, durability: 1, heat: 0.25f));

        Assert.AreEqual(11, value, 0.0001f);
        Assert.AreEqual(StatConditionMask.Heat, recipe.DependencyMask);
    }

    [Test]
    public void ContextOverrideReplacesSingleCondition()
    {
        var context = new StatEvaluationContext(quality: 0.2f, durability: 0.3f, heat: 0.4f, range: 0.5f);

        var next = context.WithCondition(StatConditionMask.Range, 0.75f);

        Assert.AreEqual(0.2f, next.Quality, 0.0001f);
        Assert.AreEqual(0.3f, next.Durability, 0.0001f);
        Assert.AreEqual(0.4f, next.Heat, 0.0001f);
        Assert.AreEqual(0.75f, next.Range, 0.0001f);
    }

    [Test]
    public void RecipeTokensMapCatalogStringsToRuntimeEnums()
    {
        Assert.AreEqual(StatConditionMask.Quality, StatRecipeTokens.ToConditionMask("quality"));
        Assert.AreEqual(StatConditionMask.PilotSkill, StatRecipeTokens.ToConditionMask("pilotSkill"));
        Assert.AreEqual(StatConditionMask.None, StatRecipeTokens.ToConditionMask("unknown"));
        Assert.AreEqual(StatModifierOperation.Multiply, StatRecipeTokens.ToModifierOperation("multiply"));
        Assert.AreEqual(StatModifierOperation.Override, StatRecipeTokens.ToModifierOperation("override"));
        Assert.AreEqual(StatModifierOperation.Add, StatRecipeTokens.ToModifierOperation("unknown"));
    }

    [Test]
    public void LegacyStatDependencyMaskIncludesOnlyActiveLegacyInputs()
    {
        var stat = new PerformanceStat
        {
            QualityExponent = 2,
            DurabilityExponentMultiplier = 0,
            HeatExponentMultiplier = 3
        };

        Assert.AreEqual(StatConditionMask.Quality, stat.GetDependencyMask(includeHeat: false));
        Assert.AreEqual(StatConditionMask.Quality | StatConditionMask.Heat, stat.GetDependencyMask(includeHeat: true));
    }

    [Test]
    public void LegacyStatEvaluationUsesExplicitContextExponents()
    {
        var stat = new PerformanceStat
        {
            Min = 0,
            Max = 100,
            QualityExponent = 2,
            DurabilityExponentMultiplier = 1,
            HeatExponentMultiplier = 1
        };

        var value = stat.EvaluateRecipeOrLegacy(
            new StatEvaluationContext(quality: 0.5f, durability: 0.5f, heat: 0.5f),
            durabilityExponent: 2,
            thermalExponent: 2,
            includeHeat: true);

        Assert.AreEqual(3.125f, value, 0.0001f);
    }

    [Test]
    public void ConsumableBaselinePreservesLinearEffectivenessCompatibility()
    {
        var stat = new PerformanceStat
        {
            Min = 10,
            Max = 30,
            QualityExponent = 2,
            DurabilityExponentMultiplier = 10
        };

        var value = stat.EvaluateConsumableBaseline(
            new StatEvaluationContext(quality: 0.5f, durability: 0.5f, heat: 1));

        Assert.AreEqual(12.5f, value, 0.0001f);
    }

    [Test]
    public void EntityModifierApplicationLeavesBaseValueWhenNoEntityModifiersExist()
    {
        var stat = new PerformanceStat();

        Assert.AreEqual(42f, stat.ApplyEntityModifiers(null, 42f), 0.0001f);
    }

    [Test]
    public void BehaviorValueReaderReadsEnumFromString()
    {
        var value = BehaviorValue("multiply", 0);

        var operation = AetheriaRuntimeBehaviorValueReader.ReadEnum(value, StatModifierOperation.Add);

        Assert.AreEqual(StatModifierOperation.Multiply, operation);
    }

    [Test]
    public void BehaviorValueReaderReadsEnumFromNumberOrFallback()
    {
        var value = BehaviorValue("", (int)StatModifierOperation.Override);
        var unknown = BehaviorValue("", 999);

        Assert.AreEqual(
            StatModifierOperation.Override,
            AetheriaRuntimeBehaviorValueReader.ReadEnum(value, StatModifierOperation.Add));
        Assert.AreEqual(
            StatModifierOperation.Add,
            AetheriaRuntimeBehaviorValueReader.ReadEnum(unknown, StatModifierOperation.Add));
    }

    private static AetheriaRuntimeBehaviorValue BehaviorValue(string stringValue, double numberValue)
    {
        return new AetheriaRuntimeBehaviorValue(
            "",
            stringValue,
            numberValue,
            false,
            "",
            "",
            Array.Empty<AetheriaRuntimeBehaviorValue>(),
            Array.Empty<AetheriaRuntimeBehaviorMapEntry>());
    }
}
