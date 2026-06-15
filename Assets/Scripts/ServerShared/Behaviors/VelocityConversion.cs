/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using Unity.Mathematics;
using static Unity.Mathematics.math;
public class VelocityConversionConfig : RuntimeBehaviorConfig
{
    [Inspectable]
    public PerformanceStat Lambda = new PerformanceStat();

    public override Behavior CreateInstance(EquippedItem item)
    {
        return new VelocityConversion(this, item);
    }
    public override Behavior CreateInstance(ConsumableItemEffect item)
    {
        return new VelocityConversion(this, item);
    }
}

public class VelocityConversion : Behavior
{
    private readonly PerformanceStat _lambda;

    public VelocityConversion(VelocityConversionConfig data, EquippedItem item) : base(data, item)
    {
        _lambda = data.Lambda;
    }
    public VelocityConversion(VelocityConversionConfig data, ConsumableItemEffect item) : base(data, item)
    {
        _lambda = data.Lambda;
    }

    public override bool Execute(float dt)
    {
        Entity.Velocity = AetheriaMath.Damp(Entity.Velocity, Entity.Direction * length(Entity.Velocity), Evaluate(_lambda), dt);
        return true;
    }
}
