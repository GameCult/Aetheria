/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Linq;
public class Thermotoggle : Behavior
{
    public float TargetTemperature;
    public bool Adjustable { get; }
    private readonly bool _highPass;

    public Thermotoggle(RuntimeBehaviorDefinition definition, EquippedItem item) : base(definition, item)
    {
        TargetTemperature = definition.Float(1);
        _highPass = definition.Bool(2);
        Adjustable = definition.Bool(3);
    }

    public Thermotoggle(RuntimeBehaviorDefinition definition, ConsumableItemEffect item) : base(definition, item)
    {
        TargetTemperature = definition.Float(1);
        _highPass = definition.Bool(2);
        Adjustable = definition.Bool(3);
    }

    public override bool Execute(float dt)
    {
        return Temperature < TargetTemperature ^ _highPass;
    }
}
