/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using static CultMath.math;
using cfloat2 = CultMath.float2;
using cfloat3 = CultMath.float3;

public class AetherDrive : Behavior
{
    private readonly cfloat3 _rotorDiameter;
    private readonly cfloat3 _rotorMass;
    private readonly PerformanceStat _maximumRpm;
    private readonly cfloat3 _couplingLambda;
    private readonly PerformanceStat _lambdaMultiplier;
    private readonly PerformanceStat _couplingEfficiency;
    private readonly PerformanceStat _torque;
    private readonly BezierCurve _torqueProfile;
    private readonly PerformanceStat _energyDraw;
    private readonly PerformanceStat _passiveCoupling;
    private readonly uint _rpmAudioParameter;
    private readonly uint _torqueRatioAudioParameter;
    private cfloat3 _axis;

    public cfloat3 Thrust { get; private set; }
    public cfloat3 Rpm { get; private set; }
    public float MaximumRpm { get; private set; }
    public cfloat2 ThrustDirection { get; private set; }
    public string Particles { get; }

    public cfloat3 Axis
    {
        get => _axis;
        set => _axis = clamp(value, -1, 1);
    }

    public AetherDrive(RuntimeBehaviorDefinition definition, EquippedItem item) : base(definition, item)
    {
        _rotorDiameter = definition.Float3(1);
        _rotorMass = definition.Float3(2);
        _maximumRpm = definition.PerformanceStat(3, null);
        _couplingLambda = definition.Float3(4);
        _lambdaMultiplier = definition.PerformanceStat(5, null);
        _couplingEfficiency = definition.PerformanceStat(6, null);
        _torque = definition.PerformanceStat(7, null);
        _torqueProfile = definition.BezierCurve(8, null);
        _energyDraw = definition.PerformanceStat(9, null);
        _passiveCoupling = definition.PerformanceStat(10, null);
        _rpmAudioParameter = definition.UInt(11);
        _torqueRatioAudioParameter = definition.UInt(12);
        Particles = definition.String(13);
        RegisterAetherDriveStats();
    }

    public AetherDrive(RuntimeBehaviorDefinition definition, ConsumableItemEffect item) : base(definition, item)
    {
        _rotorDiameter = definition.Float3(1);
        _rotorMass = definition.Float3(2);
        _maximumRpm = definition.PerformanceStat(3, null);
        _couplingLambda = definition.Float3(4);
        _lambdaMultiplier = definition.PerformanceStat(5, null);
        _couplingEfficiency = definition.PerformanceStat(6, null);
        _torque = definition.PerformanceStat(7, null);
        _torqueProfile = definition.BezierCurve(8, null);
        _energyDraw = definition.PerformanceStat(9, null);
        _passiveCoupling = definition.PerformanceStat(10, null);
        _rpmAudioParameter = definition.UInt(11);
        _torqueRatioAudioParameter = definition.UInt(12);
        Particles = definition.String(13);
        RegisterAetherDriveStats();
    }

    private void RegisterAetherDriveStats()
    {
        RegisterPerformanceStat(nameof(MaximumRpm), _maximumRpm);
        RegisterPerformanceStat("LambdaMultiplier", _lambdaMultiplier);
        RegisterPerformanceStat("CouplingEfficiency", _couplingEfficiency);
        RegisterPerformanceStat("Torque", _torque);
        RegisterPerformanceStat("EnergyDraw", _energyDraw);
        RegisterPerformanceStat("PassiveCoupling", _passiveCoupling);
    }

    public override bool Execute(float dt)
    {
        var rotorSpeed = Rpm * _rotorDiameter / 100;

        var forward = normalize(Entity.CultDirection);
        var right = AetheriaMath.Rotate(Entity.CultDirection, ItemRotation.Clockwise);
        var velocity = Entity.CultVelocity;

        var speed = float2(dot(velocity, forward), dot(velocity, right));
        var couplingEfficiency = Evaluate(_couplingEfficiency);
        var efficiency = float3(saturate(1 - speed / max(rotorSpeed.xy, 1) * sign(_axis.xy)) * couplingEfficiency, 1);

        Thrust = (Rpm - AetheriaMath.Decay(Rpm, _couplingLambda, dt)) * _rotorMass * efficiency;

        var couplingLambda = _couplingLambda * Evaluate(_lambdaMultiplier) * max(abs(_axis), Evaluate(_passiveCoupling));
        var previousRpm = Rpm;
        Rpm = AetheriaMath.Decay(Rpm, couplingLambda, dt);
        var rpmLoss = previousRpm - Rpm;
        var force = rpmLoss * _rotorMass * efficiency;

        var heat = rpmLoss * _rotorMass * (1 - couplingEfficiency);
        AddHeat((heat.x + heat.y + heat.z)*ItemManager.GameplaySettings.AetherHeatMultiplier);

        ThrustDirection = forward * (_axis.x * force.x / Entity.Mass) + right * (_axis.y * force.y / Entity.Mass);
        Entity.CultVelocity += ThrustDirection;

        Entity.CultDirection = AetheriaMath.Rotate(
            Entity.CultDirection,
            force.z * _axis.z * ItemManager.GameplaySettings.AetherTorqueMultiplier / Entity.Mass);

        if(float.IsNaN(Entity.CultVelocity.x))
            ItemManager.Log("AetherDrive produced NaN velocity.");

        MaximumRpm = Evaluate(_maximumRpm);
        var torqueProfile = float3(
            _torqueProfile.Evaluate(Rpm.x / MaximumRpm),
            _torqueProfile.Evaluate(Rpm.y / MaximumRpm),
            _torqueProfile.Evaluate(Rpm.z / MaximumRpm));
        var potentialTorque = Evaluate(_torque) * torqueProfile;
        var potentialRpmDelta = potentialTorque / length(_rotorMass) * dt;
        var actualRpmDelta = min(MaximumRpm - Rpm, potentialRpmDelta);
        var torqueRatio = actualRpmDelta / potentialRpmDelta;
        var draw = torqueRatio * Evaluate(_energyDraw) / 3;

        Item.SetAudioParameter(SpecialAudioParameter.Intensity, max(max(abs(_axis.x), abs(_axis.y)), abs(_axis.z)));
        Item.SetAudioParameter(_rpmAudioParameter, (Rpm.x + Rpm.y + Rpm.z) / 3 / MaximumRpm);
        Item.SetAudioParameter(_torqueRatioAudioParameter, max(max(torqueRatio.x, torqueRatio.y), torqueRatio.z));

        if (Entity.TryConsumeEnergy((draw.x + draw.y + draw.z)*dt))
        {
            Rpm += actualRpmDelta;
            return true;
        }


        return false;
    }

    public void RestoreRuntimeState(
        cfloat3 axis,
        cfloat3 thrust,
        cfloat3 rpm,
        float maximumRpm,
        cfloat2 thrustDirection)
    {
        Axis = axis;
        Thrust = thrust;
        Rpm = rpm;
        MaximumRpm = maximumRpm;
        ThrustDirection = thrustDirection;
    }
}
