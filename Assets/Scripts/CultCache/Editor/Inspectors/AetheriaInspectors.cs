using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using static UnityEditor.EditorGUILayout;
using static Unity.Mathematics.math;

public class StatReferenceInspector : BaseInspector<StatReference>
{
    public override StatReference Inspect(string label, StatReference value, object parent, DatabaseInspector inspectorWindow)
    {
        using (new HorizontalScope())
        {
            GUILayout.Label(label, GUILayout.Width(width));
            
            using (new VerticalScope())
            {
                var targetObjects = typeof(BehaviorData).GetAllChildClasses()
                    .Concat(typeof(EquippableItemData).GetAllChildClasses()).ToArray();
                var objectNames = targetObjects.Select(b => b.Name).ToArray();
                var selectedIndex = Array.IndexOf(objectNames, value.Target);
                if (selectedIndex == -1)
                {
                    selectedIndex = 0;
                    GUI.changed = true;
                }
                using (new HorizontalScope())
                {
                    GUILayout.Label("Target", GUILayout.Width(width));
                    var newSelection = Popup(selectedIndex, objectNames);
                    if (newSelection != selectedIndex)
                        GUI.changed = true;
                    value.Target = objectNames[newSelection];
                }

                using (new HorizontalScope())
                {
                    GUILayout.Label("Stat", GUILayout.Width(width));
                    var stats = targetObjects[selectedIndex].GetFields()
                        .Where(f => f.FieldType == typeof(PerformanceStat)).ToArray();
                    if(stats.Length==0)
                        GUILayout.Label("No Stats!");
                    else
                    {
                        var statNames = stats.Select(s => s.Name).ToArray();
                        var selectedStatIndex = Array.IndexOf(statNames, value.Stat);
                        if (selectedStatIndex == -1)
                        {
                            selectedStatIndex = 0;
                            GUI.changed = true;
                        }
                        var newSelection = Popup(selectedStatIndex, statNames);
                        if (newSelection != selectedStatIndex)
                            GUI.changed = true;
                        value.Stat = statNames[newSelection];
                    }
                }
            }
        }

        return value;
    }
}

public class PerformanceStatInspector : BaseInspector<PerformanceStat>
{
    public override PerformanceStat Inspect(string label, PerformanceStat value, object parent, DatabaseInspector inspectorWindow)
    {
        if(value == null)
            value = new PerformanceStat();
        using (new VerticalScope())
        {
            using (new HorizontalScope())
            {
                GUILayout.Label(label, GUILayout.Width(width));
                GUILayout.Label("Min", EditorStyles.miniLabel, GUILayout.Width(labelWidth));
                value.Min = DelayedFloatField(value.Min);
                GUILayout.Label("Max", EditorStyles.miniLabel, GUILayout.Width(labelWidth + 5));
                value.Max = DelayedFloatField(value.Max);
            }

            // Only display heat, durability and quality effects if the stat actually varies
            if (Math.Abs(value.Min - value.Max) > .0001f)
            {
                using (new HorizontalScope())
                {
                    GUILayout.Label("", GUILayout.Width(width));
                    GUILayout.Label("H", EditorStyles.miniLabel, GUILayout.Width(labelWidth));
                    value.HeatExponentMultiplier = DelayedFloatField(value.HeatExponentMultiplier);
                    
                    GUILayout.Label("D", EditorStyles.miniLabel, GUILayout.Width(labelWidth));
                    value.DurabilityExponentMultiplier = DelayedFloatField(value.DurabilityExponentMultiplier);
                    
                    GUILayout.Label("Q", EditorStyles.miniLabel, GUILayout.Width(labelWidth));
                    value.QualityExponent = DelayedFloatField(value.QualityExponent);
                }
            }

            Space();
        }

        return value;
    }
}

public class ShapeInspector : BaseInspector<Shape>
{
    private Material _schematicMat = new Material(Shader.Find("Legacy Shaders/Particles/Additive"));
    
    public override Shape Inspect(string label, Shape value, object parent, DatabaseInspector inspectorWindow)
    {
        if(value == null)
        {
            value = new Shape(1, 1);
            value[int2(0)] = true;
        }
        using (new VerticalScope())
        {
            using (new HorizontalScope())
            {
                GUILayout.Label(label, GUILayout.Width(width));
                GUILayout.Label("Size", GUILayout.ExpandWidth(false));
                GUILayout.Label("X", GUILayout.Width(labelWidth));
                value.Width = DelayedIntField(value.Width);
                GUILayout.Label("Y", GUILayout.Width(labelWidth));
                value.Height = DelayedIntField(value.Height);
            }

            using (new HorizontalScope())
            {
                GUILayout.Label("", GUILayout.Width(width));
                using (new VerticalScope())
                {
                    for (var y = value.Height - 1; y >= 0; y--)
                    {
                        using (new HorizontalScope())
                        {
                            for (var x = 0; x < value.Width; x++)
                            {
                                var oldColor = GUI.backgroundColor;
                                value[int2(x, y)] = Toggle(value[int2(x, y)], GUILayout.Width(toggleWidth));
                                GUI.backgroundColor = oldColor;
                            }
                        }
                    }
                }
                GUILayout.FlexibleSpace();
            }
        }

        return value;
    }
}

public class ShapeSchematicInspector : BaseInspector<Shape, InspectableSchematicShapeAttribute>
{
    private Material _schematicMat = new Material(Shader.Find("Legacy Shaders/Particles/Additive"));
    
    public override Shape Inspect(string label, Shape value, object parent, DatabaseInspector inspectorWindow, InspectableSchematicShapeAttribute attribute)
    {
        var item = parent as EquippableItemData;
        if(value == null)
        {
            value = new Shape(1, 1);
            value[int2(0)] = true;
        }
        using (new VerticalScope())
        {
            Texture2D schematic = null;
            if(item != null && !string.IsNullOrEmpty(item.Schematic))
                schematic = AssetDatabase.LoadAssetAtPath<Texture2D>(item.Schematic);
            using (new HorizontalScope())
            {
                GUILayout.Label(label, GUILayout.Width(width));
                GUILayout.Label("Size", GUILayout.ExpandWidth(false));
                if(schematic == null)
                {
                    GUILayout.Label("X", GUILayout.Width(labelWidth));
                    value.Width = DelayedIntField(value.Width);
                    GUILayout.Label("Y", GUILayout.Width(labelWidth));
                    value.Height = DelayedIntField(value.Height);
                }
                else
                {
                    GUILayout.Label("X", GUILayout.Width(labelWidth));
                    value.Width = DelayedIntField(value.Width);
                    value.Height = Mathf.RoundToInt(value.Width * ((float)schematic.height / schematic.width));
                    GUILayout.Label("Y", GUILayout.Width(labelWidth));
                    LabelField($"{value.Height}");
                }
            }

            using (new HorizontalScope())
            {
                GUILayout.Label("", GUILayout.Width(width));
                using (var a = new VerticalScope())
                {
                    if(schematic != null)
                    {
                        EditorGUI.DrawPreviewTexture(a.rect, schematic, _schematicMat);
                    }
                    for (var y = value.Height - 1; y >= 0; y--)
                    {
                        using (new HorizontalScope())
                        {
                            for (var x = 0; x < value.Width; x++)
                            {
                                var oldColor = GUI.backgroundColor;
                                if(item is HullData hull)
                                {
                                    var hardpoint = hull.Hardpoints
                                        .FirstOrDefault(hp =>
                                            hp.Shape.Coordinates
                                                .Select(v => v + hp.Position)
                                                .Any(v => v.x == x && v.y == y));
                                    if (hardpoint != null)
                                        GUI.backgroundColor = hardpoint.TintColor.ToColor();
                                }
                                value[int2(x, y)] = Toggle(value[int2(x, y)], GUILayout.Width(toggleWidth));
                                GUI.backgroundColor = oldColor;
                            }
                        }
                    }
                }
                GUILayout.FlexibleSpace();
            }
        }

        return value;
    }
}