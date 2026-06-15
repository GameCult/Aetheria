/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using Unity.Mathematics;
using static Unity.Mathematics.math;

[Inspectable]
public class VelocityLimitConfig : RuntimeBehaviorConfig
{
    [Inspectable]
    public PerformanceStat TopSpeed = new PerformanceStat();

    public override Behavior CreateInstance(EquippedItem item)
    {
        return new VelocityLimit(this, item);
    }
    public override Behavior CreateInstance(ConsumableItemEffect item)
    {
        return new VelocityLimit(this, item);
    }
}

[Order(100)]
public class VelocityLimit : Behavior
{
    public float Limit { get; private set; }

    private readonly PerformanceStat _topSpeed;

    public VelocityLimit(VelocityLimitConfig data, EquippedItem item) : base(data, item)
    {
        _topSpeed = data.TopSpeed;
        Limit = Evaluate(_topSpeed);
    }

    public VelocityLimit(VelocityLimitConfig data, ConsumableItemEffect item) : base(data, item)
    {
        _topSpeed = data.TopSpeed;
        Limit = Evaluate(_topSpeed);
    }

    public override bool Execute(float dt)
    {
        Limit = Evaluate(_topSpeed);
        if (length(Entity.Velocity) > Limit)
            Entity.Velocity = normalize(Entity.Velocity) * Limit;
        return true;
    }
}
