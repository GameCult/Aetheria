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
// public class CategoryAttribute : Attribute
// {
//     public readonly Type Type;
//
//     public CategoryAttribute(Type type)
//     {
//         Type = type;
//     }
// }