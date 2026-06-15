/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System.Collections.Generic;
using Unity.Mathematics;
using static Unity.Mathematics.math;

public class Cockpit : Behavior
{
    public Cockpit(RuntimeBehaviorDefinition definition, EquippedItem item) : base(definition, item)
    {
    }

    public Cockpit(RuntimeBehaviorDefinition definition, ConsumableItemEffect item) : base(definition, item)
    {
    }
}
