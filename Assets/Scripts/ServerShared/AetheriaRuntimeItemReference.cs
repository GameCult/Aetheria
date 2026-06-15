/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using Unity.Mathematics;

public interface INamedEntry
{
    string EntryName { get; set; }
}

public class AetheriaRuntimeItemReference
{
    private const string LegacyItemDefinitionPrefix = "aetheria.item_definition:legacy:";

    public string ItemKey;

    public AetheriaRuntimeItemReference()
    {
    }

    public AetheriaRuntimeItemReference(string itemKey)
    {
        ItemKey = itemKey;
    }

    public static string FromLegacyId(Guid legacyItemId)
    {
        return legacyItemId == Guid.Empty ? "" : $"{LegacyItemDefinitionPrefix}{legacyItemId:D}";
    }
}

public interface ITintInspector
{
    float3 TintColor { get; }
}
