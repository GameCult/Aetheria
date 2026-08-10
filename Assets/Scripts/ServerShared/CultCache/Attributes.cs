using System;

[AttributeUsage(AttributeTargets.Class)]
public class GlobalSettingsAttribute : Attribute { }

// [AttributeUsage(AttributeTargets.Class)]
// public class ExternalEntryAttribute : Attribute { }

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

public class InspectableDatabaseLinkAttribute : PreferredInspectorAttribute
{
    public readonly Type EntryType;

    public InspectableDatabaseLinkAttribute(Type entryType)
    {
        EntryType = entryType;
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

public class InspectableTypeAttribute : PreferredInspectorAttribute
{
    public readonly Type Type;

    public InspectableTypeAttribute(Type type)
    {
        Type = type;
    }
}

[AttributeUsage(AttributeTargets.Field)]
public class RangedFloatAttribute : Attribute
{
    public readonly float Min, Max;

    public RangedFloatAttribute(float min, float max)
    {
        Min = min;
        Max = max;
    }
}


/// <summary>
///   <para>Specify a tooltip for a field in the Inspector window.</para>
/// </summary>
[AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
public class TooltipAttribute : Attribute
{
    /// <summary>
    ///   <para>The tooltip text.</para>
    /// </summary>
    public readonly string tooltip;

    /// <summary>
    ///   <para>Specify a tooltip for a field.</para>
    /// </summary>
    /// <param name="tooltip">The tooltip text.</param>
    public TooltipAttribute(string tooltip) => this.tooltip = tooltip;
}

[AttributeUsage(AttributeTargets.Class)]
public class RethinkTableAttribute : Attribute
{
    public string TableName;

    public RethinkTableAttribute(string tableName)
    {
        TableName = tableName;
    }
}

public class NameAttribute : Attribute
{
    public string Name;

    public NameAttribute(string name)
    {
        Name = name;
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