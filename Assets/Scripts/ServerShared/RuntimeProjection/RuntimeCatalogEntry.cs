/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using Unity.Mathematics;

public interface INamedEntry
{
    string EntryName { get; set; }
}

public abstract class RuntimeCatalogEntry
{
    public Guid ID = Guid.NewGuid();

    public override int GetHashCode()
    {
        return ID.GetHashCode();
    }

    public override bool Equals(object obj)
    {
        if (obj is RuntimeCatalogEntry entry) return entry.ID == ID;
        return false;
    }
}

public class RuntimeCatalogLink<T> : RuntimeCatalogLinkBase where T : ItemData
{
    public T Value { get; private set; }

    public RuntimeCatalogLink()
    {
    }

    public RuntimeCatalogLink(T value)
    {
        SetValue(value);
    }

    public void SetValue(T value)
    {
        Value = value;
        LinkID = value?.ID ?? Guid.Empty;
    }
}

public class RuntimeCatalogLinkBase
{
    public Guid LinkID;
}

public interface ITintInspector
{
    float3 TintColor { get; }
}
