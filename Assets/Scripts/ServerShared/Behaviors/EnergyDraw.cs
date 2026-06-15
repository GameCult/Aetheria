/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Linq;
public class EnergyDraw : Behavior
{
    private readonly PerformanceStat _energyDraw;
    private readonly bool _perSecond;

    public EnergyDraw(RuntimeBehaviorDefinition definition, EquippedItem item) : base(definition, item)
    {
        _energyDraw = definition.PerformanceStat(1, new PerformanceStat());
        _perSecond = definition.Bool(2);
        RegisterPerformanceStat(nameof(EnergyDraw), _energyDraw);
    }

    public EnergyDraw(RuntimeBehaviorDefinition definition, ConsumableItemEffect item) : base(definition, item)
    {
        _energyDraw = definition.PerformanceStat(1, new PerformanceStat());
        _perSecond = definition.Bool(2);
        RegisterPerformanceStat(nameof(EnergyDraw), _energyDraw);
    }

    public override bool Execute(float dt)
    {
        return Entity.TryConsumeEnergy(Evaluate(_energyDraw) * (_perSecond ? dt : 1));
    }
}
