using UnityEditor;
using UnityEngine;
using static UnityEditor.EditorGUILayout;

public class FloatInspector : BaseInspector<float>
{
    public override float Inspect(string label, float value, object parent, DatabaseInspector inspectorWindow)
    {
        using (new HorizontalScope())
        {
            GUILayout.Label(label, GUILayout.Width(width));
            return DelayedFloatField(value);
        }
    }
}

public class RangedFloatInspector : BaseInspector<float, InspectableRangedFloatAttribute>
{
    public override float Inspect(string label,
        float value,
        object parent,
        DatabaseInspector inspectorWindow,
        InspectableRangedFloatAttribute attribute)
    {
        using (new HorizontalScope())
        {
            GUILayout.Label(label, GUILayout.Width(width));
            return Slider(value, attribute.Min, attribute.Max);
        }
    }
}

public class TemperatureFloatInspector : BaseInspector<float, InspectableTemperatureAttribute>
{
    public override float Inspect(string label,
        float value,
        object parent,
        DatabaseInspector inspectorWindow,
        InspectableTemperatureAttribute attribute)
    {
        using (var h = new HorizontalScope())
        {
            GUILayout.Label(label, GUILayout.Width(width));
            GUILayout.Label("°K", EditorStyles.miniLabel, GUILayout.Width(labelWidth));
            value = DelayedFloatField(value);
            GUILayout.Label("°C", EditorStyles.miniLabel, GUILayout.Width(labelWidth));
            value = DelayedFloatField(value - 273.15f) + 273.15f;
            GUILayout.Label("°F", EditorStyles.miniLabel, GUILayout.Width(labelWidth));
            value = (DelayedFloatField((value - 273.15f) * 1.8f + 32) - 32) / 1.8f + 273.15f;
                
            return value;
        }
    }
}