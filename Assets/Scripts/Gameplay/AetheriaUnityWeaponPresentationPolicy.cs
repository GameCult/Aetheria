/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using GameCult.Aetheria.State.Verse;

public static class AetheriaUnityWeaponPresentationPolicy
{
    private static readonly HashSet<string> ArticulatedWeaponBehaviorKinds = new HashSet<string>(StringComparer.Ordinal)
    {
        "GuidedWeapon",
        "InstantWeapon",
        "ConstantWeapon",
        "ChargedWeapon",
        "AutoWeapon"
    };

    public static bool HasArticulatedWeaponBehavior(
        AetheriaRuntimeCatalogSnapshot runtimeCatalog,
        EquippedItem item)
    {
        var typedItem = runtimeCatalog?.FindItem(item?.EquippableItem, x => x.ItemKey);
        if (typedItem?.BehaviorKinds == null)
            return false;

        foreach (var behaviorKind in typedItem.BehaviorKinds)
        {
            if (ArticulatedWeaponBehaviorKinds.Contains(behaviorKind))
                return true;
        }

        return false;
    }
}
