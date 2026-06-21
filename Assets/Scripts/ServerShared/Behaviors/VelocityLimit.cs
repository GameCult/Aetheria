/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using static CultMath.math;

public class VelocityLimit : Behavior
{
    public float Limit { get; private set; }

    private readonly PerformanceStat _topSpeed;

    public VelocityLimit(RuntimeBehaviorDefinition definition, EquippedItem item) : base(definition, item)
    {
        _topSpeed = definition.PerformanceStat(1, new PerformanceStat());
        RegisterPerformanceStat("TopSpeed", _topSpeed);
        Limit = Evaluate(_topSpeed);
    }

    public VelocityLimit(RuntimeBehaviorDefinition definition, ConsumableItemEffect item) : base(definition, item)
    {
        _topSpeed = definition.PerformanceStat(1, new PerformanceStat());
        RegisterPerformanceStat("TopSpeed", _topSpeed);
        Limit = Evaluate(_topSpeed);
    }

    public override bool Execute(float dt)
    {
        Limit = Evaluate(_topSpeed);
        var velocity = Entity.CultVelocity;
        if (length(velocity) > Limit)
            Entity.CultVelocity = normalize(velocity) * Limit;
        return true;
    }

    public void RestoreRuntimeState(float limit)
    {
        Limit = limit;
    }
}
