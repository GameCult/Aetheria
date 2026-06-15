/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Linq;
[Inspectable]
public class VisibilityConfig : RuntimeBehaviorConfig
{
    [Inspectable]
    public PerformanceStat Visibility = new PerformanceStat();

    [Inspectable]
    public PerformanceStat VisibilityDecay = new PerformanceStat();

    public override Behavior CreateInstance(EquippedItem item)
    {
        return new Visibility(this, item);
    }
    public override Behavior CreateInstance(ConsumableItemEffect item)
    {
        return new Visibility(this, item);
    }
}

public class Visibility : Behavior
{
    private readonly PerformanceStat _visibility;

    public Visibility(VisibilityConfig data, EquippedItem item) : base(data, item)
    {
        _visibility = data.Visibility;
    }
    public Visibility(VisibilityConfig data, ConsumableItemEffect item) : base(data, item)
    {
        _visibility = data.Visibility;
    }

    public override bool Execute(float dt)
    {
        Entity.VisibilitySources[this] = Evaluate(_visibility);
        return true;
    }
}
