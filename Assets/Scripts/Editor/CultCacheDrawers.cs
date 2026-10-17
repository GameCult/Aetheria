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

// A size and a toggle per cell. When the owning item has a schematic texture, the texture lies under the grid and the
// height follows its aspect; a hull's hardpoint cells are tinted. Both read the owning record through
// inspector.Record, which is a read-only copy: nothing here writes to it.
[CultInspectorDrawer(typeof(InspectableSchematicShapeAttribute))]
public sealed class InspectableSchematicShapeDrawer : ICultInspectorDrawer
{
    private static Material _schematicMaterial;

    public object Draw(CultInspector inspector, string label, Type type, object value, MemberInfo member)
    {
        if (type != typeof(Shape)) return inspector.DrawDefault(label, type, value, member);
        var shape = value as Shape ?? new Shape();
        var item = inspector.Record as EquippableItemData;
        var hull = item as HullData;
        var schematic = string.IsNullOrEmpty(item?.Schematic) ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(item.Schematic));

        EditorGUILayout.LabelField(label);
        using (new EditorGUILayout.HorizontalScope())
        {
            var width = max(EditorGUILayout.DelayedIntField("Width", shape.Width), 1);
            int height;
            if (schematic == null) height = max(EditorGUILayout.DelayedIntField("Height", shape.Height), 1);
            else
            {
                height = max(Mathf.RoundToInt(width * ((float) schematic.height / schematic.width)), 1);
                EditorGUILayout.LabelField("Height", height.ToString());
            }
            if (width != shape.Width || height != shape.Height) shape.Resize(width, height);
        }

        // The flexible space shrinks the grid group to its toggles, so the texture fits the grid, not the inspector width.
        using (new EditorGUILayout.HorizontalScope())
        {
            using (var grid = new EditorGUILayout.VerticalScope())
            {
                if (schematic != null && Event.current.type == EventType.Repaint)
                {
                    _schematicMaterial ??= new Material(Shader.Find("Legacy Shaders/Particles/Additive"));
                    EditorGUI.DrawPreviewTexture(grid.rect, schematic, _schematicMaterial);
                }

                for (var y = shape.Height - 1; y >= 0; y--)
                    using (new EditorGUILayout.HorizontalScope())
                        for (var x = 0; x < shape.Width; x++)
                        {
                            var hardpoint = hull?.Hardpoints.FirstOrDefault(hp => HullFootprint.Cells(hp).Contains(int2(x, y)));
                            var previous = GUI.backgroundColor;
                            if (hardpoint != null) GUI.backgroundColor = hardpoint.TintColor.ToColor();
                            shape[int2(x, y)] = GUILayout.Toggle(shape[int2(x, y)], GUIContent.none, GUILayout.Width(16));
                            GUI.backgroundColor = previous;
                        }
            }
            GUILayout.FlexibleSpace();
        }
        return shape;
    }
}

// A hardpoint's schematic footprint over its hull's cells: the hull's occupied cells shaded, this hardpoint's cells lit
// in its tint, other hardpoints' cells tinted and disabled. A click edits Position and Shape together through
// HullFootprint.Toggle, which owns the rule so a Thing host reads the same one. The footprint is authored for gameplay,
// never derived from the model. Read-only on inspector.Record; only the HardpointData it is given changes.
[CultInspectorDrawer(typeof(HardpointData))]
public sealed class HardpointFootprintDrawer : ICultInspectorDrawer
{
    public object Draw(CultInspector inspector, string label, Type type, object value, MemberInfo member)
    {
        var hardpoint = value as HardpointData;
        if (type != typeof(HardpointData) || hardpoint == null) return inspector.DrawDefault(label, type, value, member);
        var hull = inspector.Record as HullData;
        var own = hull?.Hardpoints.FirstOrDefault(hp => hp.Type == hardpoint.Type && hp.Position.Equals(hardpoint.Position)
            && HullFootprint.Cells(hp).SequenceEqual(HullFootprint.Cells(hardpoint)));
        var drawn = (HardpointData) inspector.DrawDefault(label, type, value, member);
        if (hull?.Shape == null) return drawn;

        var others = hull.Hardpoints.Where(hp => hp != own).ToArray();
        EditorGUILayout.LabelField("Footprint");
        for (var y = hull.Shape.Height - 1; y >= 0; y--)
            using (new EditorGUILayout.HorizontalScope())
                for (var x = 0; x < hull.Shape.Width; x++)
                {
                    var cell = int2(x, y);
                    var other = others.FirstOrDefault(hp => HullFootprint.Cells(hp).Contains(cell));
                    var on = HullFootprint.Cells(drawn).Contains(cell);
                    var previous = GUI.backgroundColor;
                    GUI.backgroundColor = on ? drawn.TintColor.ToColor() : other != null ? other.TintColor.ToColor()
                        : hull.Shape[cell] ? Color.gray : previous;
                    EditorGUI.BeginChangeCheck();
                    using (new EditorGUI.DisabledScope(other != null && !on))
                        GUILayout.Toggle(on || other != null, GUIContent.none, GUILayout.Width(16));
                    if (EditorGUI.EndChangeCheck() && HullFootprint.Toggle(drawn.Shape, drawn.Position, cell, out var shape, out var position))
                    {
                        drawn.Shape = shape;
                        drawn.Position = position;
                        GUI.changed = true;
                    }
                    GUI.backgroundColor = previous;
                }
        return drawn;
    }
}
