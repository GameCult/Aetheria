/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

public class HeatStorage : Behavior
{
    public HeatStorage(RuntimeBehaviorDefinition definition, EquippedItem item) : base(definition, item)
    {
    }

    public HeatStorage(RuntimeBehaviorDefinition definition, ConsumableItemEffect item) : base(definition, item)
    {
    }
}
