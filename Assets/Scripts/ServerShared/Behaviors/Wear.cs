/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

public class Wear : Behavior
{
    private readonly bool _perSecond;

    public Wear(RuntimeBehaviorDefinition definition, EquippedItem item) : base(definition, item)
    {
        _perSecond = definition.Bool(1, true);
    }

    public Wear(RuntimeBehaviorDefinition definition, ConsumableItemEffect item) : base(definition, item)
    {
        _perSecond = definition.Bool(1, true);
    }

    public override bool Execute(float dt)
    {
        CauseWearDamage(_perSecond ? dt : 1);
        return true;
    }
}
