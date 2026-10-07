/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using GameCult.Caching;
using MessagePack;
using Newtonsoft.Json;
using CultMath;
using static CultMath.math;

[Inspectable, MessagePackObject, JsonObject(MemberSerialization.OptIn), EntityTypeRestriction(HullType.Ship), RuntimeInspectable]
public class AetherDriveData : BehaviorData
{
    [Inspectable, JsonProperty("diameters"), Key(1)]
    public float3 RotorDiameter;
    
    [Inspectable, JsonProperty("masses"), Key(2)]
    public float3 RotorMass;
    
    [Inspectable, JsonProperty("rpm"), Key(3), RuntimeInspectable]
    public PerformanceStat MaximumRpm;
    
    [Inspectable, JsonProperty("couplingLambdas"), Key(4)]
    public float3 CouplingLambda;
    
    [Inspectable, JsonProperty("lambdaMultiplier"), Key(5)]
    public PerformanceStat LambdaMultiplier;
    
    [Inspectable, JsonProperty("couplingEfficiency"), Key(6), RuntimeInspectable]
    public PerformanceStat CouplingEfficiency;
    
    [Inspectable, JsonProperty("torque"), Key(7), RuntimeInspectable]
    public PerformanceStat Torque;
    
    [Inspectable, JsonProperty("torqueProfile"), Key(8), RuntimeInspectable]
    public BezierCurve TorqueProfile;
    
    [Inspectable, JsonProperty("draw"), Key(9), RuntimeInspectable]
    public PerformanceStat EnergyDraw;
    
    [Inspectable, JsonProperty("passiveCoupling"), Key(10), RuntimeInspectable]
    public PerformanceStat PassiveCoupling;

    [Inspectable, JsonProperty("rpmAudio"), Key(11), RuntimeInspectable]
    public uint RpmAudioParameter;

    [Inspectable, JsonProperty("torqueAudio"), Key(12), RuntimeInspectable]
    public uint TorqueRatioAudioParameter;

    [Inspectable, CultInspectorAssetGuid, JsonProperty("particles"), Key(13)]
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

public class AetherDrive : Behavior, IPowerConsumer
{
    private AetherDriveData _data;
    private float3 _axis;

    public float3 Thrust { get; private set; }
    public float3 Rpm { get; private set; }
    public float MaximumRpm { get; private set; }
    public float2 ThrustDirection { get; private set; }

    public AetherDriveData DriveData => _data;

    // Cut 8 (operator ask 2026-09-19): the equivalent quantity to Thruster.Condition -- Torque is the stat that
    // actually governs this behaviour's rotor spin-up (PowerRequest/Execute above both read it), so it is what
    // "broken" means for a drive. 1f for the item-less ConsumableItemEffect case, same reasoning as Thruster.
    public float Condition => Item?.ConditionRatio(_data.Torque) ?? 1f;

    public float3 Axis
    {
        get => _axis;
        set => _axis = clamp(value, -1, 1);
    }

    public AetherDrive(AetherDriveData data, EquippedItem item) : base(data, item)
    {
        _data = data;
    }

    public AetherDrive(AetherDriveData data, ConsumableItemEffect item) : base(data, item)
    {
        _data = data;
    }

    // Cut 3 (docs/stats-and-power-cut.md): the request PowerBus needs before Execute runs. Only the rotor
    // spin-up (torque accelerating Rpm toward MaximumRpm) costs power -- spending existing spin into thrust
    // (Execute's decay-to-thrust arithmetic below) is free, same as before. Pure and side-effect-free: it reads
    // Rpm but does not write it, so Execute's own identical arithmetic a few lines later -- which does perform
    // the real decay -- produces the same numbers Rpm actually moves by. Accepts the recompute (Q3's ruling).
    //
    // Nominal-request ruling (docs/stats-and-power-cut.md, operator ruling 2026-09-19): Torque, LambdaMultiplier,
    // MaximumRpm, PassiveCoupling and EnergyDraw are all registered request fields (StatValidation.
    // PowerRequestFields) -- read nominally (what full power would produce) so Torque carrying its own
    // PowerSupply term (Cut 7's brownout curve) no longer makes this tick's request depend on this tick's own
    // grant. Execute below is unchanged: it calls the real, curved Evaluate, so the actual spin-up still
    // degrades with whatever the bus actually grants.
    public float PowerRequest(float dt)
    {
        var couplingLambda = _data.CouplingLambda * Item.EvaluateNominalPower(_data.LambdaMultiplier) * max(abs(_axis), EvaluateNominalPower(_data.PassiveCoupling));
        var rpmAfterDecay = decay(Rpm, couplingLambda, dt);
        var maximumRpm = EvaluateNominalPower(_data.MaximumRpm);
        var torqueProfile = float3(
            _data.TorqueProfile.Evaluate(rpmAfterDecay.x / maximumRpm),
            _data.TorqueProfile.Evaluate(rpmAfterDecay.y / maximumRpm),
            _data.TorqueProfile.Evaluate(rpmAfterDecay.z / maximumRpm));
        var potentialTorque = EvaluateNominalPower(_data.Torque) * torqueProfile;
        var potentialRpmDelta = potentialTorque / length(_data.RotorMass) * dt;
        var actualRpmDelta = min(maximumRpm - rpmAfterDecay, potentialRpmDelta);
        var torqueRatio = actualRpmDelta / potentialRpmDelta;
        var draw = torqueRatio * EvaluateNominalPower(_data.EnergyDraw) / 3;
        return (draw.x + draw.y + draw.z) * dt;
    }

    // Cut 5 (docs/stats-and-power-cut.md §1.3, PowerTiers.cs): Medium -- mobility, same as Thruster.
    public int DefaultPowerTier => PowerTiers.Medium;

    public override bool Execute(float dt)
    {
        var rotorSpeed = Rpm * _data.RotorDiameter / 100;
        
        var forward = normalize(Entity.Direction);
        var right = forward.Rotate(ItemRotation.Clockwise);
            
        var speed = float2(dot(Entity.Velocity, forward), dot(Entity.Velocity, right));
        var couplingEfficiency = Evaluate(_data.CouplingEfficiency);
        var efficiency = float3(saturate(1 - speed / max(rotorSpeed.xy, 1) * sign(_axis.xy)) * couplingEfficiency, 1);

        Thrust = (Rpm - decay(Rpm, _data.CouplingLambda, dt)) * _data.RotorMass * efficiency;

        var couplingLambda = _data.CouplingLambda * Item.Evaluate(_data.LambdaMultiplier) * max(abs(_axis), Evaluate(_data.PassiveCoupling));
        var previousRpm = Rpm;
        Rpm = decay(Rpm, couplingLambda, dt);
        var rpmLoss = previousRpm - Rpm;
        var force = rpmLoss * _data.RotorMass * efficiency;

        var heat = rpmLoss * _data.RotorMass * (1 - couplingEfficiency);
        AddHeat((heat.x + heat.y + heat.z)*ItemManager.GameplaySettings.AetherHeatMultiplier);

        ThrustDirection = forward * (_axis.x * force.x / Entity.Mass) + right * (_axis.y * force.y / Entity.Mass);
        Entity.Velocity += ThrustDirection;
        
        Entity.Direction = mul(Entity.Direction,
            CultMath.float2x2.Rotate(force.z * _axis.z * ItemManager.GameplaySettings.AetherTorqueMultiplier / Entity.Mass));

        if(float.IsNaN(Entity.Velocity.x))
            ItemManager.Log("FUCK FUCK FUCK FUCK");
        
        MaximumRpm = Evaluate(_data.MaximumRpm);
        var torqueProfile = float3(
            _data.TorqueProfile.Evaluate(Rpm.x / MaximumRpm),
            _data.TorqueProfile.Evaluate(Rpm.y / MaximumRpm),
            _data.TorqueProfile.Evaluate(Rpm.z / MaximumRpm));
        var potentialTorque = Evaluate(_data.Torque) * torqueProfile;
        var potentialRpmDelta = potentialTorque / length(_data.RotorMass) * dt;
        var actualRpmDelta = min(MaximumRpm - Rpm, potentialRpmDelta);
        var torqueRatio = actualRpmDelta / potentialRpmDelta;

        Item.SetAudioParameter(SpecialAudioParameter.Intensity, max(max(abs(_axis.x), abs(_axis.y)), abs(_axis.z)));
        Item.SetAudioParameter(_data.RpmAudioParameter, (Rpm.x + Rpm.y + Rpm.z) / 3 / MaximumRpm);
        Item.SetAudioParameter(_data.TorqueRatioAudioParameter, max(max(torqueRatio.x, torqueRatio.y), torqueRatio.z));
        
        // Cut 7 (docs/stats-and-power-target.md): actualRpmDelta already derives from Evaluate(_data.Torque)
        // above, so a Torque stat carrying a PowerSupply term already shrinks the rotor's spin-up under a
        // partial grant -- this no longer demands a full one, only that supply has not been cut to true zero.
        if (Item.PowerSupply > 1e-4f)
        {
            Rpm += actualRpmDelta;
            return true;
        }

        return false;
    }
}