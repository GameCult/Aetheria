using System;
using System.Linq;
using System.Reflection;
using CultMath;
using CultMath.UnityBridge;
using GameCult.Caching;
using GameCult.Unity.Caching.Editor;
using MessagePack;
using UnityEditor;
using UnityEngine;
using static CultMath.math;

// Aetheria's Studio drawers: the inspection meaning CultLib's CultInspector attributes do not carry. Each draws only
// the value types its attribute sits on and hands anything else back to built-in drawing.

// The simple name of one of the attribute type's [Union] members, as a string
[CultInspectorDrawer(typeof(InspectableTypeAttribute))]
public sealed class InspectableTypeDrawer : ICultInspectorDrawer
{
    public object Draw(CultInspector inspector, string label, Type type, object value, MemberInfo member)
    {
        var attribute = member?.GetCustomAttribute<InspectableTypeAttribute>();
        if (type != typeof(string) || attribute == null) return inspector.DrawDefault(label, type, value, member);
        var names = attribute.Type.GetCustomAttributes<UnionAttribute>(false).Select(union => union.SubType.Name).ToArray();
        var index = Array.IndexOf(names, value as string) + 1;
        var picked = EditorGUILayout.Popup(label, index, names.Prepend("None").ToArray());
        return picked == index ? value : picked == 0 ? null : names[picked - 1];
    }
}

[CultInspectorDrawer(typeof(InspectableColorAttribute))]
public sealed class InspectableColorDrawer : ICultInspectorDrawer
{
    public object Draw(CultInspector inspector, string label, Type type, object value, MemberInfo member)
    {
        if (type == typeof(float3)) return EditorGUILayout.ColorField(label, ((float3) value).ToColor()).ToFloat3();
        if (type == typeof(float4)) return EditorGUILayout.ColorField(label, ((float4) value).ToColor()).ToCultMath();
        return inspector.DrawDefault(label, type, value, member);
    }
}

// Keyframes as (time, value, in tangent, out tangent), edited as a curve over the unit square
[CultInspectorDrawer(typeof(InspectableAnimationCurveAttribute))]
public sealed class InspectableAnimationCurveDrawer : ICultInspectorDrawer
{
    public object Draw(CultInspector inspector, string label, Type type, object value, MemberInfo member)
    {
        float4[] keys;
        if (type == typeof(float4[])) keys = value as float4[];
        else if (type == typeof(BezierCurve)) keys = (value as BezierCurve)?.Keys;
        else return inspector.DrawDefault(label, type, value, member);

        EditorGUI.BeginChangeCheck();
        var curve = EditorGUILayout.CurveField(label, keys?.Length > 0 ? keys.ToCurve() : new AnimationCurve(), Color.yellow, new Rect(0, 0, 1, 1));
        if (!EditorGUI.EndChangeCheck()) return value;
        var edited = curve.keys.Select(k => float4(k.time, k.value, k.inTangent, k.outTangent)).ToArray();
        if (type == typeof(float4[])) return edited;
        var bezier = value as BezierCurve ?? new BezierCurve();
        bezier.Keys = edited;
        return bezier;
    }
}

// One Kelvin value shown and edited as Kelvin, Celsius and Fahrenheit
[CultInspectorDrawer(typeof(InspectableTemperatureAttribute))]
public sealed class InspectableTemperatureDrawer : ICultInspectorDrawer
{
    public object Draw(CultInspector inspector, string label, Type type, object value, MemberInfo member)
    {
        if (type != typeof(float)) return inspector.DrawDefault(label, type, value, member);
        var kelvin = (float) value;
        EditorGUI.BeginChangeCheck();
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.PrefixLabel(label);
            var indent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;
            GUILayout.Label("°K", EditorStyles.miniLabel, GUILayout.Width(20));
            kelvin = EditorGUILayout.DelayedFloatField(kelvin);
            GUILayout.Label("°C", EditorStyles.miniLabel, GUILayout.Width(20));
            kelvin = EditorGUILayout.DelayedFloatField(kelvin - 273.15f) + 273.15f;
            GUILayout.Label("°F", EditorStyles.miniLabel, GUILayout.Width(20));
            kelvin = (EditorGUILayout.DelayedFloatField((kelvin - 273.15f) * 1.8f + 32) - 32) / 1.8f + 273.15f;
            EditorGUI.indentLevel = indent;
        }
        return EditorGUI.EndChangeCheck() ? kelvin : value;
    }
}

// A size and a toggle per cell. The Database Tools drawer also laid the item's schematic texture under the grid,
// derived the height from its aspect, and tinted hull hardpoints. Those read the document the shape belongs to, and a
// Studio drawer is handed only the value, so they wait on the Studio exposing the open record.
[CultInspectorDrawer(typeof(InspectableSchematicShapeAttribute))]
public sealed class InspectableSchematicShapeDrawer : ICultInspectorDrawer
{
    public object Draw(CultInspector inspector, string label, Type type, object value, MemberInfo member)
    {
        if (type != typeof(Shape)) return inspector.DrawDefault(label, type, value, member);
        var shape = value as Shape ?? new Shape();
        EditorGUILayout.LabelField(label);
        using (new EditorGUILayout.HorizontalScope())
        {
            var width = max(EditorGUILayout.DelayedIntField("Width", shape.Width), 1);
            var height = max(EditorGUILayout.DelayedIntField("Height", shape.Height), 1);
            if (width != shape.Width || height != shape.Height) shape.Resize(width, height);
        }

        for (var y = shape.Height - 1; y >= 0; y--)
            using (new EditorGUILayout.HorizontalScope())
                for (var x = 0; x < shape.Width; x++)
                    shape[int2(x, y)] = GUILayout.Toggle(shape[int2(x, y)], GUIContent.none, GUILayout.Width(16));
        return shape;
    }
}
