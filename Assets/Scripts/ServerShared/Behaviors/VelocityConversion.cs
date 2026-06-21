/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using static CultMath.math;

public class VelocityConversion : Behavior
{
    private readonly PerformanceStat _lambda;

    public VelocityConversion(RuntimeBehaviorDefinition definition, EquippedItem item) : base(definition, item)
    {
        _lambda = definition.PerformanceStat(1, new PerformanceStat());
        RegisterPerformanceStat("Lambda", _lambda);
    }

    public VelocityConversion(RuntimeBehaviorDefinition definition, ConsumableItemEffect item) : base(definition, item)
    {
        _lambda = definition.PerformanceStat(1, new PerformanceStat());
        RegisterPerformanceStat("Lambda", _lambda);
    }

    public override bool Execute(float dt)
    {
        var velocity = Entity.CultVelocity;
        Entity.CultVelocity = AetheriaMath.Damp(velocity, Entity.CultDirection * length(velocity), Evaluate(_lambda), dt);
        return true;
    }
}
