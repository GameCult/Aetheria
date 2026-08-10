using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using static UnityEditor.EditorGUILayout;

public class Float2Inspector : BaseInspector<float2>
{
    public override float2 Inspect(string label, float2 value, object parent, DatabaseInspector inspectorWindow)
    {
        using (new HorizontalScope())
        {
            GUILayout.Label(label, GUILayout.Width(width));
            GUILayout.Label("X", GUILayout.Width(labelWidth));
            value.x = DelayedFloatField(value.x);
            GUILayout.Label("Y", GUILayout.Width(labelWidth));
            value.y = DelayedFloatField(value.y);
            return value;
        }
    }
}

public class Float3Inspector : BaseInspector<float3>
{
    public override float3 Inspect(string label, float3 value, object parent, DatabaseInspector inspectorWindow)
    {
        using (new HorizontalScope())
        {
            GUILayout.Label(label, GUILayout.Width(width));
            GUILayout.Label("X", GUILayout.Width(labelWidth));
            value.x = DelayedFloatField(value.x);
            GUILayout.Label("Y", GUILayout.Width(labelWidth));
            value.y = DelayedFloatField(value.y);
            GUILayout.Label("Z", GUILayout.Width(labelWidth));
            value.z = DelayedFloatField(value.z);
            return value;
        }
    }
}

public class Float3ColorInspector : BaseInspector<float3, InspectableColorAttribute>
{
    public override float3 Inspect(string label, float3 value, object parent, DatabaseInspector inspectorWindow, InspectableColorAttribute attribute)
    {
        using (new HorizontalScope())
        {
            GUILayout.Label(label, GUILayout.Width(width));
            return ColorField(value.ToColor()).ToFloat3();
        }
    }
}

public class Float4Inspector : BaseInspector<float4>
{
    public override float4 Inspect(string label, float4 value, object parent, DatabaseInspector inspectorWindow)
    {
        using (new HorizontalScope())
        {
            GUILayout.Label(label, GUILayout.Width(width));
            GUILayout.Label("X", GUILayout.Width(labelWidth));
            value.x = DelayedFloatField(value.x);
            GUILayout.Label("Y", GUILayout.Width(labelWidth));
            value.y = DelayedFloatField(value.y);
            GUILayout.Label("Z", GUILayout.Width(labelWidth));
            value.z = DelayedFloatField(value.z);
            GUILayout.Label("W", GUILayout.Width(labelWidth));
            value.w = DelayedFloatField(value.w);
            return value;
        }
    }
}

public class Float4ColorInspector : BaseInspector<float4, InspectableColorAttribute>
{
    public override float4 Inspect(string label, float4 value, object parent, DatabaseInspector inspectorWindow, InspectableColorAttribute attribute)
    {
        using (new HorizontalScope())
        {
            GUILayout.Label(label, GUILayout.Width(width));
            return ColorField(value.ToColor()).ToFloat4();
        }
    }
}

public class Int2Inspector : BaseInspector<int2>
{
    public override int2 Inspect(string label, int2 value, object parent, DatabaseInspector inspectorWindow)
    {
        using (new HorizontalScope())
        {
            GUILayout.Label(label, GUILayout.Width(width));
            GUILayout.Label("X", GUILayout.Width(labelWidth));
            value.x = DelayedIntField(value.x);
            GUILayout.Label("Y", GUILayout.Width(labelWidth));
            value.y = DelayedIntField(value.y);
            if (GUILayout.Button("\u2190", GUILayout.Width(arrowWidth)))
                value.x--;
            if (GUILayout.Button("\u2192", GUILayout.Width(arrowWidth)))
                value.x++;
            if (GUILayout.Button("\u2191", GUILayout.Width(arrowWidth)))
                value.y++;
            if (GUILayout.Button("\u2193", GUILayout.Width(arrowWidth)))
                value.y--;
            return value;
        }
    }
}

public class Int3Inspector : BaseInspector<int3>
{
    public override int3 Inspect(string label, int3 value, object parent, DatabaseInspector inspectorWindow)
    {
        using (new HorizontalScope())
        {
            GUILayout.Label(label, GUILayout.Width(width));
            GUILayout.Label("X", GUILayout.Width(labelWidth));
            value.x = DelayedIntField(value.x);
            GUILayout.Label("Y", GUILayout.Width(labelWidth));
            value.y = DelayedIntField(value.y);
            GUILayout.Label("Z", GUILayout.Width(labelWidth));
            value.z = DelayedIntField(value.z);
            return value;
        }
    }
}

public class Int4Inspector : BaseInspector<int4>
{
    public override int4 Inspect(string label, int4 value, object parent, DatabaseInspector inspectorWindow)
    {
        using (new HorizontalScope())
        {
            GUILayout.Label(label, GUILayout.Width(width));
            GUILayout.Label("X", GUILayout.Width(labelWidth));
            value.x = DelayedIntField(value.x);
            GUILayout.Label("Y", GUILayout.Width(labelWidth));
            value.y = DelayedIntField(value.y);
            GUILayout.Label("Z", GUILayout.Width(labelWidth));
            value.z = DelayedIntField(value.z);
            GUILayout.Label("W", GUILayout.Width(labelWidth));
            value.w = DelayedIntField(value.w);
            return value;
        }
    }
}