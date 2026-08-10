using System.Linq;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using static UnityEditor.EditorGUILayout;
using static Unity.Mathematics.math;

public class AnimationCurveFloat4Inspector : BaseInspector<float4[]>
{
    public override float4[] Inspect(string label, float4[] value, object parent, DatabaseInspector inspectorWindow)
    {
        var val = value != null && value.Length > 0
            ? value.ToCurve()
            : new AnimationCurve();
        using (new HorizontalScope())
        {
            GUILayout.Label(label, GUILayout.Width(width));
            val = CurveField(val, Color.yellow, new Rect(0, 0, 1, 1));
        }

        return val.keys.Select(k => float4(k.time, k.value, k.inTangent, k.outTangent)).ToArray();
    }
}

public class BezierCurveInspector : BaseInspector<BezierCurve>
{
    public override BezierCurve Inspect(string label, BezierCurve value, object parent, DatabaseInspector inspectorWindow)
    {
        var val = value != null && value.Keys != null && value.Keys.Length > 0
            ? value.ToCurve()
            : new AnimationCurve();
        using (new HorizontalScope())
        {
            GUILayout.Label(label, GUILayout.Width(width));
            val = CurveField(val, Color.yellow, new Rect(0, 0, 1, 1));
        }

        value.Keys = val.keys.Select(k => float4(k.time, k.value, k.inTangent, k.outTangent)).ToArray();

        return value;
    }
}

public class AnimationCurveInspect : BaseInspector<AnimationCurve>
{
    public override AnimationCurve Inspect(string label, AnimationCurve value, object parent, DatabaseInspector inspectorWindow)
    {
        using (var h = new HorizontalScope())
        {
            GUILayout.Label(label, GUILayout.Width(width));
            if (value == null)
                value = new AnimationCurve();
            value = CurveField(value, Color.yellow, new Rect(0, 0, 1, 1));
        }

        return value;
    }
}