/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

public class Reflector : Behavior
{
    private readonly PerformanceStat _crossSection;

    public Reflector(RuntimeBehaviorDefinition definition, EquippedItem item) : base(definition, item)
    {
        _crossSection = definition.PerformanceStat(1, new PerformanceStat());
        RegisterPerformanceStat("CrossSection", _crossSection);
    }

    public Reflector(RuntimeBehaviorDefinition definition, ConsumableItemEffect item) : base(definition, item)
    {
        _crossSection = definition.PerformanceStat(1, new PerformanceStat());
        RegisterPerformanceStat("CrossSection", _crossSection);
    }

    public override bool Execute(float dt)
    {
        Entity.VisibilitySources[this] = Evaluate(_crossSection) * Entity.Zone.GetLight(Entity.CultPositionXZ);

        return true;
    }
}
