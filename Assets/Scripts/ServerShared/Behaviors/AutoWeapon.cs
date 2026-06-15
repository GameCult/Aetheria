using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using static Unity.Mathematics.math;

public class AutoWeapon : InstantWeapon
{

    public AutoWeapon(RuntimeBehaviorDefinition definition, EquippedItem item) : base(definition, item) { }
    public AutoWeapon(RuntimeBehaviorDefinition definition, ConsumableItemEffect item) : base(definition, item) { }

    public override bool Execute(float dt)
    {
        if(_firing && _burstRemaining == 0 && _cooldown < 0)
            Trigger();
        return base.Execute(dt);
    }
}

