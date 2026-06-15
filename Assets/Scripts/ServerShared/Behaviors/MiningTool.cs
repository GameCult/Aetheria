/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Linq;
using Unity.Mathematics;
using static Unity.Mathematics.math;

public class MiningTool : Behavior
{
    public Guid AsteroidBelt;
    public int Asteroid;

    private readonly PerformanceStat _damagePerSecond;
    private readonly PerformanceStat _efficiency;
    private readonly PerformanceStat _penetration;
    private readonly PerformanceStat _range;
    public float Range { get; private set; }

    public MiningTool(RuntimeBehaviorDefinition definition, EquippedItem item) : base(definition, item)
    {
        _damagePerSecond = definition.PerformanceStat(1, new PerformanceStat());
        _efficiency = definition.PerformanceStat(2, new PerformanceStat());
        _penetration = definition.PerformanceStat(3, new PerformanceStat());
        _range = definition.PerformanceStat(4, new PerformanceStat());
        RegisterPerformanceStat("DamagePerSecond", _damagePerSecond);
        RegisterPerformanceStat("Efficiency", _efficiency);
        RegisterPerformanceStat("Penetration", _penetration);
        RegisterPerformanceStat(nameof(Range), _range);
    }

    public MiningTool(RuntimeBehaviorDefinition definition, ConsumableItemEffect item) : base(definition, item)
    {
        _damagePerSecond = definition.PerformanceStat(1, new PerformanceStat());
        _efficiency = definition.PerformanceStat(2, new PerformanceStat());
        _penetration = definition.PerformanceStat(3, new PerformanceStat());
        _range = definition.PerformanceStat(4, new PerformanceStat());
        RegisterPerformanceStat("DamagePerSecond", _damagePerSecond);
        RegisterPerformanceStat("Efficiency", _efficiency);
        RegisterPerformanceStat("Penetration", _penetration);
        RegisterPerformanceStat(nameof(Range), _range);
    }

    public override bool Execute(float dt)
    {
        Range = Evaluate(_range);
        var belt = Entity.Zone.AsteroidBelts[AsteroidBelt];
        if (AsteroidBelt != Guid.Empty &&
            Entity.Zone.AsteroidExists(AsteroidBelt, Asteroid) &&
            length(Entity.Position.xz - belt.Transforms[Asteroid].xy) - belt.Transforms[Asteroid].w < Range)
        {
            Entity.Zone.MineAsteroid(
                Entity,
                AsteroidBelt,
                Asteroid,
                Evaluate(_damagePerSecond) * dt,
                Evaluate(_efficiency),
                Evaluate(_penetration));
            return true;
        }

        return false;
    }
}
