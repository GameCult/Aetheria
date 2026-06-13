/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using Unity.Mathematics;

public interface INamedEntry
{
    string EntryName { get; set; }
}

public abstract class RuntimeItemProjectionEntry
{
    public Guid ID = Guid.NewGuid();

    public override int GetHashCode()
    {
        return ID.GetHashCode();
    }

    public override bool Equals(object obj)
    {
        if (obj is RuntimeItemProjectionEntry entry) return entry.ID == ID;
        return false;
    }
}

public class RuntimeItemReference
{
    public ItemData Value { get; private set; }
    public Guid ItemId;

    public RuntimeItemReference()
    {
    }

    public RuntimeItemReference(ItemData value)
    {
        SetValue(value);
    }

    public void SetValue(ItemData value)
    {
        Value = value;
        ItemId = value?.ID ?? Guid.Empty;
    }
}

public interface ITintInspector
{
    float3 TintColor { get; }
}
