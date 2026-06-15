/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using Unity.Mathematics;
using static Unity.Mathematics.math;

[Inspectable, EntityTypeRestriction(HullType.Ship)]
public class AetherDriveConfig : RuntimeBehaviorConfig
{
    [Inspectable]
    public float3 RotorDiameter;

    [Inspectable]
    public float3 RotorMass;

    [Inspectable]
    public PerformanceStat MaximumRpm;

    [Inspectable]
    public float3 CouplingLambda;

    [Inspectable]
    public PerformanceStat LambdaMultiplier;

    [Inspectable]
    public PerformanceStat CouplingEfficiency;

    [Inspectable]
    public PerformanceStat Torque;

    [Inspectable]
    public BezierCurve TorqueProfile;

    [Inspectable]
    public PerformanceStat EnergyDraw;

    [Inspectable]
    public PerformanceStat PassiveCoupling;

    [InspectableAudioParameter]
    public uint RpmAudioParameter;

    [InspectableAudioParameter]
    public uint TorqueRatioAudioParameter;

    [InspectablePrefab]
    public string Particles;

    public override Behavior CreateInstance(EquippedItem item)
    {
        return new AetherDrive(this, item);
    }

    public override Behavior CreateInstance(ConsumableItemEffect item)
    {
        return new AetherDrive(this, item);
    }
}

public class AetherDrive : Behavior
{
    private readonly float3 _rotorDiameter;
    private readonly float3 _rotorMass;
    private readonly PerformanceStat _maximumRpm;
    private readonly float3 _couplingLambda;
    private readonly PerformanceStat _lambdaMultiplier;
    private readonly PerformanceStat _couplingEfficiency;
    private readonly PerformanceStat _torque;
    private readonly BezierCurve _torqueProfile;
    private readonly PerformanceStat _energyDraw;
    private readonly PerformanceStat _passiveCoupling;
    private readonly uint _rpmAudioParameter;
    private readonly uint _torqueRatioAudioParameter;
    private float3 _axis;

    public float3 Thrust { get; private set; }
    public float3 Rpm { get; private set; }
    public float MaximumRpm { get; private set; }
    public float2 ThrustDirection { get; private set; }
    public string Particles { get; }

    public float3 Axis
    {
        get => _axis;
        set => _axis = clamp(value, -1, 1);
    }

    public AetherDrive(AetherDriveConfig data, EquippedItem item) : base(data, item)
    {
        _rotorDiameter = data.RotorDiameter;
        _rotorMass = data.RotorMass;
        _maximumRpm = data.MaximumRpm;
        _couplingLambda = data.CouplingLambda;
        _lambdaMultiplier = data.LambdaMultiplier;
        _couplingEfficiency = data.CouplingEfficiency;
        _torque = data.Torque;
        _torqueProfile = data.TorqueProfile;
        _energyDraw = data.EnergyDraw;
        _passiveCoupling = data.PassiveCoupling;
        _rpmAudioParameter = data.RpmAudioParameter;
        _torqueRatioAudioParameter = data.TorqueRatioAudioParameter;
        Particles = data.Particles;
    }

    public AetherDrive(AetherDriveConfig data, ConsumableItemEffect item) : base(data, item)
    {
        _rotorDiameter = data.RotorDiameter;
        _rotorMass = data.RotorMass;
        _maximumRpm = data.MaximumRpm;
        _couplingLambda = data.CouplingLambda;
        _lambdaMultiplier = data.LambdaMultiplier;
        _couplingEfficiency = data.CouplingEfficiency;
        _torque = data.Torque;
        _torqueProfile = data.TorqueProfile;
        _energyDraw = data.EnergyDraw;
        _passiveCoupling = data.PassiveCoupling;
        _rpmAudioParameter = data.RpmAudioParameter;
        _torqueRatioAudioParameter = data.TorqueRatioAudioParameter;
        Particles = data.Particles;
    }

    public override bool Execute(float dt)
    {
        var rotorSpeed = Rpm * _rotorDiameter / 100;

        var forward = normalize(Entity.Direction);
        var right = forward.Rotate(ItemRotation.Clockwise);

        var speed = float2(dot(Entity.Velocity, forward), dot(Entity.Velocity, right));
        var couplingEfficiency = Evaluate(_couplingEfficiency);
        var efficiency = float3(saturate(1 - speed / max(rotorSpeed.xy, 1) * sign(_axis.xy)) * couplingEfficiency, 1);

        Thrust = (Rpm - AetheriaMath.Decay(Rpm, _couplingLambda, dt)) * _rotorMass * efficiency;

        var couplingLambda = _couplingLambda * Item.Evaluate(_lambdaMultiplier) * max(abs(_axis), Evaluate(_passiveCoupling));
        var previousRpm = Rpm;
        Rpm = AetheriaMath.Decay(Rpm, couplingLambda, dt);
        var rpmLoss = previousRpm - Rpm;
        var force = rpmLoss * _rotorMass * efficiency;

        var heat = rpmLoss * _rotorMass * (1 - couplingEfficiency);
        AddHeat((heat.x + heat.y + heat.z)*ItemManager.GameplaySettings.AetherHeatMultiplier);

        ThrustDirection = forward * (_axis.x * force.x / Entity.Mass) + right * (_axis.y * force.y / Entity.Mass);
        Entity.Velocity += ThrustDirection;

        Entity.Direction = mul(Entity.Direction,
            Unity.Mathematics.float2x2.Rotate(force.z * _axis.z * ItemManager.GameplaySettings.AetherTorqueMultiplier / Entity.Mass));

        if(float.IsNaN(Entity.Velocity.x))
            ItemManager.Log("FUCK FUCK FUCK FUCK");

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
}
