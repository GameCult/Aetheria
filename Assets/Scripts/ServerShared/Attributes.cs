/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;

[AttributeUsage(AttributeTargets.Class)]
public class EntityTypeRestrictionAttribute : Attribute
{
    public readonly HullType Type;

    public EntityTypeRestrictionAttribute(HullType type)
    {
        Type = type;
    }
}

public class RuntimeInspectable : Attribute { }

// The runtime PropertiesPanel's opt-in; the Studio does not read it.
[AttributeUsage(AttributeTargets.All, Inherited = false)]
public class InspectableAttribute : Attribute { }

// Inspection meaning CultLib's CultInspector attributes do not carry, drawn by Aetheria's Studio drawers.
[AttributeUsage(AttributeTargets.Field)]
public abstract class PreferredInspectorAttribute : InspectableAttribute { }

public class InspectableTemperatureAttribute : PreferredInspectorAttribute { }
public class InspectableAnimationCurveAttribute : PreferredInspectorAttribute { }
public class InspectableColorAttribute : PreferredInspectorAttribute { }
public class InspectableSchematicShapeAttribute : PreferredInspectorAttribute { }

public class InspectableTypeAttribute : PreferredInspectorAttribute
{
    public readonly Type Type;

    public InspectableTypeAttribute(Type type)
    {
        Type = type;
    }
}