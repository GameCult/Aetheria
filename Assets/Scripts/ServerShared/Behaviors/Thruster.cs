/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using static CultMath.math;
using cfloat2 = CultMath.float2;

public class Thruster : Behavior, IAnalogBehavior
{
    public float Thrust { get; private set; }
    public float Torque { get; }
    public string ParticlesPrefab { get; }

    public float Axis
    {
        get => _input;
        set => _input = saturate(value);
    }

    private readonly PerformanceStat _thrust;
    private readonly PerformanceStat _visibility;
    private readonly PerformanceStat _heat;
    private readonly PerformanceStat _energyUsage;

    private float _input;

    public Thruster(RuntimeBehaviorDefinition definition, EquippedItem item) : base(definition, item)
    {
        _thrust = definition.PerformanceStat(1, new PerformanceStat());
        _visibility = definition.PerformanceStat(2, new PerformanceStat());
        _heat = definition.PerformanceStat(3, new PerformanceStat());
        _energyUsage = definition.PerformanceStat(4, new PerformanceStat());
        ParticlesPrefab = definition.String(5);
        RegisterPerformanceStat(nameof(Thrust), _thrust);
        RegisterPerformanceStat(nameof(Visibility), _visibility);
        RegisterPerformanceStat(nameof(Heat), _heat);
        RegisterPerformanceStat("EnergyUsage", _energyUsage);
        var hullShape = ItemManager.GetRuntimeShape(Entity.Hull);
        var itemShape = ItemManager.GetRuntimeShape(item.EquippableItem);
        var hullCenter = hullShape.CenterOfMass;
        var itemCenter = hullShape.Inset(itemShape, item.Position, item.EquippableItem.Rotation).CenterOfMass;
        var toCenter = hullCenter - itemCenter;
        Torque = -dot(normalize(toCenter), AetheriaMath.Rotate(new cfloat2(1, 0), item.EquippableItem.Rotation));
        Thrust = Evaluate(_thrust);
    }

    public Thruster(RuntimeBehaviorDefinition definition, ConsumableItemEffect item) : base(definition, item)
    {
        _thrust = definition.PerformanceStat(1, new PerformanceStat());
        _visibility = definition.PerformanceStat(2, new PerformanceStat());
        _heat = definition.PerformanceStat(3, new PerformanceStat());
        _energyUsage = definition.PerformanceStat(4, new PerformanceStat());
        ParticlesPrefab = definition.String(5);
        RegisterPerformanceStat(nameof(Thrust), _thrust);
        RegisterPerformanceStat(nameof(Visibility), _visibility);
        RegisterPerformanceStat(nameof(Heat), _heat);
        RegisterPerformanceStat("EnergyUsage", _energyUsage);
        Torque = 0;
        Thrust = Evaluate(_thrust);
    }

    public override bool Execute(float dt)
    {
        Item.SetAudioParameter(SpecialAudioParameter.Intensity, _input);
        if(_input > .01f && Entity.TryConsumeEnergy(_input * Evaluate(_energyUsage)))
        {
            Thrust = Evaluate(_thrust);
            Entity.CultVelocity -= Direction.xz * _input * Thrust / Entity.Mass * dt;
            Entity.CultDirection = AetheriaMath.Rotate(
                Entity.CultDirection,
                _input * Torque * Thrust * ItemManager.GameplaySettings.TorqueMultiplier / Entity.Mass * dt);
            AddHeat(_input * Evaluate(_heat) * dt);
            var vis = _input * Evaluate(_visibility);
            if (!Entity.VisibilitySources.TryGetValue(this, out var existingVisibility) || vis > existingVisibility)
                Entity.VisibilitySources[this] = vis;
            return true;
        }
        return false;
    }

    public void RestoreRuntimeState(float axis, float thrust)
    {
        Axis = axis;
        Thrust = thrust;
    }
}
