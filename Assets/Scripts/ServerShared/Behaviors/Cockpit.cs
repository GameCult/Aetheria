/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System.Collections.Generic;
using Unity.Mathematics;
using static Unity.Mathematics.math;

[Inspectable]
public class CockpitConfig : RuntimeBehaviorConfig
{
    public override Behavior CreateInstance(EquippedItem item)
    {
        return new Cockpit(this, item);
    }
    public override Behavior CreateInstance(ConsumableItemEffect item)
    {
        return new Cockpit(this, item);
    }
}

public class Cockpit : Behavior
{
    public Cockpit(CockpitConfig data, EquippedItem item) : base(data, item)
    {
    }

    public Cockpit(CockpitConfig data, ConsumableItemEffect item) : base(data, item)
    {
    }
}
