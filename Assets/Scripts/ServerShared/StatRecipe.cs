/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.Linq;
using GameCult.Aetheria.State.Verse;

public class PerformanceStat
{
    public float Min;
    public float Max;
    public float HeatExponentMultiplier;
    public float DurabilityExponentMultiplier;
    public float QualityExponent;
    public StatRecipe Recipe;

    private Dictionary<Entity, Dictionary<Behavior, float>> _scaleModifiers;
    private Dictionary<Entity, Dictionary<Behavior, float>> _constantModifiers;

    private Dictionary<Entity, Dictionary<Behavior, float>> ScaleModifiers =>
        _scaleModifiers ??= new Dictionary<Entity, Dictionary<Behavior, float>>();

    private Dictionary<Entity, Dictionary<Behavior, float>> ConstantModifiers =>
        _constantModifiers ??= new Dictionary<Entity, Dictionary<Behavior, float>>();

    public Dictionary<Behavior, float> GetScaleModifiers(Entity entity)
    {
        if (!ScaleModifiers.ContainsKey(entity))
            ScaleModifiers[entity] = new Dictionary<Behavior, float>();

        return ScaleModifiers[entity];
    }

    public bool TryGetScaleModifiers(Entity entity, out Dictionary<Behavior, float> modifiers)
    {
        modifiers = null;
        return entity != null &&
               _scaleModifiers != null &&
               _scaleModifiers.TryGetValue(entity, out modifiers) &&
               modifiers.Count > 0;
    }

    public Dictionary<Behavior, float> GetConstantModifiers(Entity entity)
    {
        if (!ConstantModifiers.ContainsKey(entity))
            ConstantModifiers[entity] = new Dictionary<Behavior, float>();

        return ConstantModifiers[entity];
    }

    public bool TryGetConstantModifiers(Entity entity, out Dictionary<Behavior, float> modifiers)
    {
        modifiers = null;
        return entity != null &&
               _constantModifiers != null &&
               _constantModifiers.TryGetValue(entity, out modifiers) &&
               modifiers.Count > 0;
    }

    public float ApplyEntityModifiers(Entity entity, float baseValue)
    {
        var scaleModifier = 1.0f;
        if (TryGetScaleModifiers(entity, out var scaleModifiers))
        {
            foreach (var modifier in scaleModifiers.Values)
                scaleModifier *= modifier;
        }

        float constantModifier = 0;
        if (TryGetConstantModifiers(entity, out var constantModifiers))
        {
            foreach (var modifier in constantModifiers.Values)
                constantModifier += modifier;
        }

        return baseValue * scaleModifier + constantModifier;
    }

    public StatConditionMask GetDependencyMask(bool includeHeat)
    {
        if (Recipe != null)
            return Recipe.DependencyMask;

        var mask = StatConditionMask.None;
        if (QualityExponent != 0)
            mask |= StatConditionMask.Quality;
        if (DurabilityExponentMultiplier != 0)
            mask |= StatConditionMask.Durability;
        if (includeHeat && HeatExponentMultiplier != 0)
            mask |= StatConditionMask.Heat;
        return mask;
    }

    public float EvaluateRecipeOrLegacy(
        in StatEvaluationContext context,
        float durabilityExponent,
        float thermalExponent,
        bool includeHeat)
    {
        if (Recipe != null)
            return Recipe.Evaluate(context);

        var heat = !includeHeat || HeatExponentMultiplier == 0
            ? 1
            : CultMath.math.pow(context.Heat, thermalExponent * HeatExponentMultiplier);
        var durability = DurabilityExponentMultiplier == 0
            ? 1
            : CultMath.math.pow(context.Durability, durabilityExponent * DurabilityExponentMultiplier);
        var quality = QualityExponent == 0
            ? 1
            : CultMath.math.pow(context.Quality, QualityExponent);

        return CultMath.math.lerp(Min, Max, durability * quality * heat);
    }

    public float EvaluateConsumableBaseline(in StatEvaluationContext context)
    {
        if (Recipe != null)
            return Recipe.Evaluate(context);

        return CultMath.math.lerp(Min, Max, context.Durability * CultMath.math.pow(context.Quality, QualityExponent));
    }
}

[Flags]
public enum StatConditionMask
{
    None = 0,
    Quality = 1 << 0,
    Durability = 1 << 1,
    Heat = 1 << 2,
    Charge = 1 << 3,
    Ammo = 1 << 4,
    Range = 1 << 5,
    Integrity = 1 << 6,
    PilotSkill = 1 << 7,
    Environment = 1 << 8
}

public enum StatModifierOperation
{
    Add,
    Multiply,
    Override
}

public static class StatRecipeTokens
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

    public const string Add = "add";
    public const string Multiply = "multiply";
    public const string Override = "override";

    public static StatConditionMask ToConditionMask(string condition)
    {
        switch (condition ?? "")
        {
            case Quality:
                return StatConditionMask.Quality;
            case Durability:
                return StatConditionMask.Durability;
            case Heat:
                return StatConditionMask.Heat;
            case Charge:
                return StatConditionMask.Charge;
            case Ammo:
                return StatConditionMask.Ammo;
            case Range:
                return StatConditionMask.Range;
            case Integrity:
                return StatConditionMask.Integrity;
            case PilotSkill:
                return StatConditionMask.PilotSkill;
            case Environment:
                return StatConditionMask.Environment;
            default:
                return StatConditionMask.None;
        }
    }

    public static StatModifierOperation ToModifierOperation(string operation)
    {
        switch (operation ?? "")
        {
            case Multiply:
                return StatModifierOperation.Multiply;
            case Override:
                return StatModifierOperation.Override;
            default:
                return StatModifierOperation.Add;
        }
    }
}

public static class AetheriaRuntimeBehaviorValueReader
{
    public static PerformanceStat ReadPerformanceStat(AetheriaRuntimeBehaviorValue value)
    {
        return new PerformanceStat
        {
            Min = ChildFloat(value, 0),
            Max = ChildFloat(value, 1),
            HeatExponentMultiplier = ChildFloat(value, 2),
            DurabilityExponentMultiplier = ChildFloat(value, 3),
            QualityExponent = ChildFloat(value, 4),
            Recipe = ReadStatRecipe(ChildValue(value, 5))
        };
    }

    public static StatRecipe ReadStatRecipe(AetheriaRuntimeBehaviorValue value)
    {
        if (value == null || value.Children.Count == 0)
            return null;

        return new StatRecipe
        {
            BaseValue = ChildFloat(value, 0),
            Modifiers = ChildValue(value, 1)?.Children
                .Select(ReadStatRecipeModifier)
                .Where(modifier => modifier != null)
                .ToArray() ?? Array.Empty<StatRecipeModifier>()
        };
    }

    public static StatRecipeModifier ReadStatRecipeModifier(AetheriaRuntimeBehaviorValue value)
    {
        if (value == null)
            return null;

        return new StatRecipeModifier
        {
            Condition = StatRecipeTokens.ToConditionMask(ChildString(value, 0)),
            Operation = StatRecipeTokens.ToModifierOperation(ChildString(value, 1)),
            Amount = ChildFloat(value, 2),
            Curve = ReadBezierCurve(ChildValue(value, 3)),
            Enabled = value.Children.Count <= 4 || ChildBool(value, 4, fallback: true)
        };
    }

    public static BezierCurve ReadBezierCurve(AetheriaRuntimeBehaviorValue value)
    {
        if (value == null || value.Children.Count == 0)
            return null;

        return BezierCurve.FromKeys(
            value.Children
                .Where(key => key.Children.Count >= 4)
                .Select(key => (
                    ChildFloat(key, 0),
                    ChildFloat(key, 1),
                    ChildFloat(key, 2),
                    ChildFloat(key, 3))));
    }

    public static StatReference ReadStatReference(AetheriaRuntimeBehaviorValue value)
    {
        return new StatReference
        {
            Target = ChildString(value, 1),
            Stat = ChildString(value, 2)
        };
    }

    public static T ReadEnum<T>(AetheriaRuntimeBehaviorValue value, T fallback) where T : struct
    {
        if (!string.IsNullOrWhiteSpace(value?.StringValue) && Enum.TryParse(value.StringValue, true, out T parsed))
            return parsed;

        return value != null && Enum.IsDefined(typeof(T), (int)value.NumberValue)
            ? (T)Enum.ToObject(typeof(T), (int)value.NumberValue)
            : fallback;
    }

    private static float ChildFloat(AetheriaRuntimeBehaviorValue value, int index)
    {
        return value != null && value.Children.Count > index ? (float)value.Children[index].NumberValue : 0;
    }

    private static bool ChildBool(AetheriaRuntimeBehaviorValue value, int index, bool fallback = false)
    {
        return value != null && value.Children.Count > index ? value.Children[index].BoolValue : fallback;
    }

    private static string ChildString(AetheriaRuntimeBehaviorValue value, int index)
    {
        return value != null && value.Children.Count > index ? value.Children[index].StringValue ?? "" : "";
    }

    private static AetheriaRuntimeBehaviorValue ChildValue(AetheriaRuntimeBehaviorValue value, int index)
    {
        return value != null && value.Children.Count > index ? value.Children[index] : null;
    }
}

public sealed class StatRecipe
{
    public float BaseValue;
    public StatRecipeModifier[] Modifiers = Array.Empty<StatRecipeModifier>();

    private StatConditionMask? _dependencyMask;
    private StatRecipeModifier[] _enabledModifiers;

    public StatConditionMask DependencyMask
    {
        get
        {
            if (_dependencyMask.HasValue)
                return _dependencyMask.Value;

            var mask = StatConditionMask.None;
            foreach (var modifier in EnabledModifiers)
                mask |= modifier.Condition;

            _dependencyMask = mask;
            return mask;
        }
    }

    private StatRecipeModifier[] EnabledModifiers
    {
        get
        {
            if (_enabledModifiers != null)
                return _enabledModifiers;

            var modifiers = Modifiers ?? Array.Empty<StatRecipeModifier>();
            var enabledCount = 0;
            for (var i = 0; i < modifiers.Length; i++)
            {
                if (modifiers[i] != null && modifiers[i].Enabled && modifiers[i].Condition != StatConditionMask.None)
                    enabledCount++;
            }

            if (enabledCount == 0)
            {
                _enabledModifiers = Array.Empty<StatRecipeModifier>();
                return _enabledModifiers;
            }

            _enabledModifiers = new StatRecipeModifier[enabledCount];
            var next = 0;
            for (var i = 0; i < modifiers.Length; i++)
            {
                var modifier = modifiers[i];
                if (modifier != null && modifier.Enabled && modifier.Condition != StatConditionMask.None)
                    _enabledModifiers[next++] = modifier;
            }

            return _enabledModifiers;
        }
    }

    public void InvalidateCache()
    {
        _dependencyMask = null;
        _enabledModifiers = null;
    }

    public float Evaluate(in StatEvaluationContext context)
    {
        var value = BaseValue;
        foreach (var modifier in EnabledModifiers)
        {
            var sample = modifier.Sample(context.GetConditionValue(modifier.Condition));
            switch (modifier.Operation)
            {
                case StatModifierOperation.Multiply:
                    value *= 1 + ((modifier.Amount - 1) * sample);
                    break;
                case StatModifierOperation.Override:
                    value = CultMath.math.lerp(value, modifier.Amount, sample);
                    break;
                default:
                    value += modifier.Amount * sample;
                    break;
            }
        }

        return value;
    }
}

public sealed class StatRecipeModifier
{
    public StatConditionMask Condition;
    public StatModifierOperation Operation;
    public float Amount;
    public BezierCurve Curve;
    public bool Enabled = true;

    public float Sample(float conditionValue)
    {
        var value = CultMath.math.saturate(conditionValue);
        return Curve == null ? value : CultMath.math.saturate(Curve.Evaluate(value));
    }
}

public readonly struct StatEvaluationContext
{
    public StatEvaluationContext(
        float quality,
        float durability,
        float heat,
        float charge = 0,
        float ammo = 0,
        float range = 0,
        float integrity = 0,
        float pilotSkill = 0,
        float environment = 0)
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

    public float Quality { get; }
    public float Durability { get; }
    public float Heat { get; }
    public float Charge { get; }
    public float Ammo { get; }
    public float Range { get; }
    public float Integrity { get; }
    public float PilotSkill { get; }
    public float Environment { get; }

    public float GetConditionValue(StatConditionMask condition)
    {
        switch (condition)
        {
            case StatConditionMask.Quality:
                return Quality;
            case StatConditionMask.Durability:
                return Durability;
            case StatConditionMask.Heat:
                return Heat;
            case StatConditionMask.Charge:
                return Charge;
            case StatConditionMask.Ammo:
                return Ammo;
            case StatConditionMask.Range:
                return Range;
            case StatConditionMask.Integrity:
                return Integrity;
            case StatConditionMask.PilotSkill:
                return PilotSkill;
            case StatConditionMask.Environment:
                return Environment;
            default:
                return 0;
        }
    }

    public StatEvaluationContext WithCondition(StatConditionMask condition, float value)
    {
        switch (condition)
        {
            case StatConditionMask.Quality:
                return new StatEvaluationContext(value, Durability, Heat, Charge, Ammo, Range, Integrity, PilotSkill, Environment);
            case StatConditionMask.Durability:
                return new StatEvaluationContext(Quality, value, Heat, Charge, Ammo, Range, Integrity, PilotSkill, Environment);
            case StatConditionMask.Heat:
                return new StatEvaluationContext(Quality, Durability, value, Charge, Ammo, Range, Integrity, PilotSkill, Environment);
            case StatConditionMask.Charge:
                return new StatEvaluationContext(Quality, Durability, Heat, value, Ammo, Range, Integrity, PilotSkill, Environment);
            case StatConditionMask.Ammo:
                return new StatEvaluationContext(Quality, Durability, Heat, Charge, value, Range, Integrity, PilotSkill, Environment);
            case StatConditionMask.Range:
                return new StatEvaluationContext(Quality, Durability, Heat, Charge, Ammo, value, Integrity, PilotSkill, Environment);
            case StatConditionMask.Integrity:
                return new StatEvaluationContext(Quality, Durability, Heat, Charge, Ammo, Range, value, PilotSkill, Environment);
            case StatConditionMask.PilotSkill:
                return new StatEvaluationContext(Quality, Durability, Heat, Charge, Ammo, Range, Integrity, value, Environment);
            case StatConditionMask.Environment:
                return new StatEvaluationContext(Quality, Durability, Heat, Charge, Ammo, Range, Integrity, PilotSkill, value);
            default:
                return this;
        }
    }
}
