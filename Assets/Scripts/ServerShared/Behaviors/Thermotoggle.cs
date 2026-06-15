/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Linq;
[Inspectable, Order(-22)]
public class ThermotoggleConfig : RuntimeBehaviorConfig
{
    [InspectableTemperature]
    public float TargetTemperature;

    [Inspectable]
    public bool HighPass;

    [Inspectable]
    public bool Adjustable;

    public override Behavior CreateInstance(EquippedItem item)
    {
        return new Thermotoggle(this, item);
    }
    public override Behavior CreateInstance(ConsumableItemEffect item)
    {
        return new Thermotoggle(this, item);
    }
}

public class Thermotoggle : Behavior
{
    public float TargetTemperature;
    public bool Adjustable { get; }
    private readonly bool _highPass;

    public Thermotoggle(ThermotoggleConfig data, EquippedItem item) : base(data, item)
    {
        TargetTemperature = data.TargetTemperature;
        _highPass = data.HighPass;
        Adjustable = data.Adjustable;
    }
    public Thermotoggle(ThermotoggleConfig data, ConsumableItemEffect item) : base(data, item)
    {
        TargetTemperature = data.TargetTemperature;
        _highPass = data.HighPass;
        Adjustable = data.Adjustable;
    }

    public override bool Execute(float dt)
    {
        return Temperature < TargetTemperature ^ _highPass;
    }
}
