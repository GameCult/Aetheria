/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using Unity.Mathematics;
using static Unity.Mathematics.math;

[Inspectable]
public class ReflectorConfig : RuntimeBehaviorConfig
{
    [Inspectable]
    public PerformanceStat CrossSection = new PerformanceStat();

    // [InspectableAnimationCurve]
    // public float4[] VisibilityCurve;

    public override Behavior CreateInstance(EquippedItem item)
    {
        return new Reflector(this, item);
    }

    public override Behavior CreateInstance(ConsumableItemEffect item)
    {
        return new Reflector(this, item);
    }
}

public class Reflector : Behavior
{
    private readonly PerformanceStat _crossSection;

    public Reflector(ReflectorConfig data, EquippedItem item) : base(data, item)
    {
        _crossSection = data.CrossSection;
    }

    public Reflector(ReflectorConfig data, ConsumableItemEffect item) : base(data, item)
    {
        _crossSection = data.CrossSection;
    }

    public override bool Execute(float dt)
    {
        Entity.VisibilitySources[this] = Evaluate(_crossSection) * Entity.Zone.GetLight(Entity.Position.xz);

        return true;
    }
}
