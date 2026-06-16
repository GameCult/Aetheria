/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Linq;
public class Trigger : Behavior, IActivatedBehavior
{
    public bool _pulled;
    public bool Pulled => _pulled;

    public Trigger(RuntimeBehaviorDefinition definition, EquippedItem item) : base(definition, item) { }
    public Trigger(RuntimeBehaviorDefinition definition, ConsumableItemEffect item) : base(definition, item) { }

    public override bool Execute(float dt)
    {
        if (_pulled)
        {
            _pulled = false;
            return true;
        }

        return false;
    }

    public void Activate()
    {
        _pulled = true;
    }

    public void Deactivate()
    {
    }

    public void RestoreRuntimeState(bool pulled)
    {
        _pulled = pulled;
    }
}
