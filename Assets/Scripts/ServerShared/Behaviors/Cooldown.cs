/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using static CultMath.math;

public class Cooldown : Behavior, IAlwaysUpdatedBehavior, IProgressBehavior
{
    private readonly PerformanceStat _cooldownDuration;

    private float _cooldown; // Normalized

    public float Progress => saturate(_cooldown);

    public Cooldown(RuntimeBehaviorDefinition definition, EquippedItem item) : base(definition, item)
    {
        _cooldownDuration = definition.PerformanceStat(1, new PerformanceStat());
        RegisterPerformanceStat(nameof(Cooldown), _cooldownDuration);
    }

    public Cooldown(RuntimeBehaviorDefinition definition, ConsumableItemEffect item) : base(definition, item)
    {
        _cooldownDuration = definition.PerformanceStat(1, new PerformanceStat());
        RegisterPerformanceStat(nameof(Cooldown), _cooldownDuration);
    }

    public override bool Execute(float dt)
    {
        if (_cooldown < 0)
        {
            _cooldown = 1;
            return true;
        }

        return false;
    }

    public void Update(float delta)
    {
        _cooldown -= delta / Evaluate(_cooldownDuration);
    }
}
