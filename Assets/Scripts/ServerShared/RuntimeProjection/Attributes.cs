using System;

[AttributeUsage(AttributeTargets.All, Inherited = false)]
public class InspectableAttribute : Attribute { }

[AttributeUsage(AttributeTargets.Field)]
public abstract class PreferredInspectorAttribute : InspectableAttribute { }

public class InspectableTextAttribute : PreferredInspectorAttribute { }
public class InspectablePrefabAttribute : PreferredInspectorAttribute { }
public class InspectableTextureAttribute : PreferredInspectorAttribute { }
public class InspectableTextAssetAttribute : PreferredInspectorAttribute { }
public class InspectableTemperatureAttribute : PreferredInspectorAttribute { }
public class InspectableAnimationCurveAttribute : PreferredInspectorAttribute { }
public class InspectableColorAttribute : PreferredInspectorAttribute { }
public class InspectableSoundBankAttribute : PreferredInspectorAttribute { }
public class InspectableAudioParameterAttribute : PreferredInspectorAttribute { }
public class InspectableSchematicShapeAttribute : PreferredInspectorAttribute { }

public class InspectableEnumValuesAttribute : PreferredInspectorAttribute
{
    public Type EnumType;

    public InspectableEnumValuesAttribute(Type enumType)
    {
        EnumType = enumType;
    }
}

public class InspectableRangedFloatAttribute : PreferredInspectorAttribute
{
    public readonly float Min, Max;

    public InspectableRangedFloatAttribute(float min, float max)
    {
        Min = min;
        Max = max;
    }
}

public class InspectableRangedIntAttribute : PreferredInspectorAttribute
{
    public readonly int Min, Max;

    public InspectableRangedIntAttribute(int min, int max)
    {
        Min = min;
        Max = max;
    }
}

public class OrderAttribute : Attribute
{
    public int Order;

    public OrderAttribute(int order)
    {
        Order = order;
    }
}

[AttributeUsage(AttributeTargets.Field, AllowMultiple = true, Inherited = true)]
public class InspectorHeaderAttribute : Attribute
{
    public readonly string header;

    public InspectorHeaderAttribute(string header) => this.header = header;
}
