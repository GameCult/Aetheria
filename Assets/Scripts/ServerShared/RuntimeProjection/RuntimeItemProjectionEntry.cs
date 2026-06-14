/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using Unity.Mathematics;

public interface INamedEntry
{
    string EntryName { get; set; }
}

public class RuntimeItemReference
{
    public Guid ItemId;

    public RuntimeItemReference()
    {
    }

    public RuntimeItemReference(Guid itemId)
    {
        ItemId = itemId;
    }
}

public interface ITintInspector
{
    float3 TintColor { get; }
}
