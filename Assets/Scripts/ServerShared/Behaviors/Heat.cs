/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Linq;
public class Heat : Behavior
{
    private readonly PerformanceStat _heat;
    private readonly bool _perSecond;

    public Heat(RuntimeBehaviorDefinition definition, EquippedItem item) : base(definition, item)
    {
        _heat = definition.PerformanceStat(1, new PerformanceStat());
        _perSecond = definition.Bool(2);
        RegisterPerformanceStat(nameof(Heat), _heat);
    }

    public Heat(RuntimeBehaviorDefinition definition, ConsumableItemEffect item) : base(definition, item)
    {
        _heat = definition.PerformanceStat(1, new PerformanceStat());
        _perSecond = definition.Bool(2);
        RegisterPerformanceStat(nameof(Heat), _heat);
    }

    public override bool Execute(float dt)
    {
        AddHeat(Evaluate(_heat) * (_perSecond ? dt : 1));

        return true;
    }
}
