/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Linq;
public class Switch : Behavior, IActivatedBehavior
{
    public bool Activated { get; set; }

    public Switch(RuntimeBehaviorDefinition definition, EquippedItem item) : base(definition, item)
    {
    }
    public Switch(RuntimeBehaviorDefinition definition, ConsumableItemEffect item) : base(definition, item)
    {
    }

    public override bool Execute(float dt)
    {
        return Activated;
    }

    public void Activate()
    {
        Activated = true;
    }

    public void Deactivate()
    {
        Activated = false;
    }
}
