/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Linq;
public class Visibility : Behavior
{
    private readonly PerformanceStat _visibility;

    public Visibility(RuntimeBehaviorDefinition definition, EquippedItem item) : base(definition, item)
    {
        _visibility = definition.PerformanceStat(1, new PerformanceStat());
        RegisterPerformanceStat(nameof(Visibility), _visibility);
        RegisterPerformanceStat("VisibilityDecay", definition.PerformanceStat(2, new PerformanceStat()));
    }

    public Visibility(RuntimeBehaviorDefinition definition, ConsumableItemEffect item) : base(definition, item)
    {
        _visibility = definition.PerformanceStat(1, new PerformanceStat());
        RegisterPerformanceStat(nameof(Visibility), _visibility);
        RegisterPerformanceStat("VisibilityDecay", definition.PerformanceStat(2, new PerformanceStat()));
    }

    public override bool Execute(float dt)
    {
        Entity.VisibilitySources[this] = Evaluate(_visibility);
        return true;
    }
}
