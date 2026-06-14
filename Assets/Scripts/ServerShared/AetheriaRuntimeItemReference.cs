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
    private const string ItemDefinitionPrefix = "aetheria.item_definition:";
    private const string LegacyItemDefinitionPrefix = "aetheria.item_definition:legacy:";

    public string ItemKey;

    public AetheriaRuntimeItemReference()
    {
    }

    public AetheriaRuntimeItemReference(string itemKey)
    {
        ItemKey = itemKey;
    }

    public AetheriaRuntimeItemReference(Guid legacyItemId)
        : this(FromLegacyId(legacyItemId))
    {
    }

    public Guid LegacyItemId => TryParseLegacyId(ItemKey, out var itemId) ? itemId : Guid.Empty;

    public static string FromLegacyId(Guid legacyItemId)
    {
        return legacyItemId == Guid.Empty ? "" : $"{LegacyItemDefinitionPrefix}{legacyItemId:D}";
    }

    public static Guid ToLegacyId(string itemKey)
    {
        return TryParseLegacyId(itemKey, out var itemId) ? itemId : Guid.Empty;
    }

    private static bool TryParseLegacyId(string itemKey, out Guid itemId)
    {
        itemId = Guid.Empty;
        if (string.IsNullOrWhiteSpace(itemKey))
            return false;

        var legacyId = itemKey.StartsWith(LegacyItemDefinitionPrefix, StringComparison.OrdinalIgnoreCase)
            ? itemKey.Substring(LegacyItemDefinitionPrefix.Length)
            : itemKey.StartsWith(ItemDefinitionPrefix, StringComparison.OrdinalIgnoreCase)
                ? itemKey.Substring(ItemDefinitionPrefix.Length)
            : itemKey;

        return Guid.TryParse(legacyId, out itemId);
    }
}

public interface ITintInspector
{
    float3 TintColor { get; }
}
