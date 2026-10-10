/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using GameCult.Caching;
using MessagePack;
using Newtonsoft.Json;
using CultMath;
using static CultMath.math;

[Inspectable, MessagePackObject, JsonObject(MemberSerialization.OptIn), EntityTypeRestriction(HullType.Ship), RuntimeInspectable]
public class ThrusterData : BehaviorData
{
    [Inspectable, JsonProperty("thrust"), Key(1), RuntimeInspectable]  
    public PerformanceStat Thrust = new PerformanceStat();

    [Inspectable, JsonProperty("visibility"), Key(2), RuntimeInspectable]  
    public PerformanceStat Visibility = new PerformanceStat();

    [Inspectable, JsonProperty("heat"), Key(3), RuntimeInspectable]  
    public PerformanceStat Heat = new PerformanceStat();

    [Inspectable, JsonProperty("energy"), Key(4), RuntimeInspectable]  
    public PerformanceStat EnergyUsage = new PerformanceStat();

    [Inspectable, CultInspectorAssetGuid, JsonProperty("Particles"), Key(5)]
    public string ParticlesPrefab;
    
    public override Behavior CreateInstance(EquippedItem item)
    {
        return new Thruster(this, item);
    }
    
    public override Behavior CreateInstance(ConsumableItemEffect item)
    {
        return new Thruster(this, item);
    }
}

public class Thruster : Behavior, IPowerConsumer
{
    // Live: heat, quality, durability and power move this stat after construction, so it is read, never cached.
    public float Thrust => Evaluate(_data.Thrust);
    // Thrust at a full grant, the same stat read without the power-supply term: what a column promises. Live, like Thrust.
    public float NominalThrust => EvaluateNominalPower(_data.Thrust);
    public float Torque { get; }

    // This thruster's effect per unit throttle at the given thrust, in the body frame: xy is the push (x starboard, y
    // forward), z the clockwise yaw rate in rad/s. Zero when the item is absent or offline. The one geometry owner for
    // the allocator and the envelope.
    public float3 Column(float thrust)
    {
        if (Item == null || !Item.Active.Value) return default;
        var push = -float2(0, 1).Rotate(Item.EquippableItem.Rotation) * thrust / Entity.Mass;
        return float3(push.x, push.y, Torque * thrust * ItemManager.GameplaySettings.TorqueMultiplier / Entity.Mass);
    }

    public float Axis
    {
        get => _input;
        set => _input = saturate(value);
    }

    private ThrusterData _data;

    private float _input;

    // Cut 8 (operator ask 2026-09-19): presentation's read of "how healthy does this thruster actually look" --
    // Thrust is the stat that governs this behaviour's real output, so it is the equivalent quantity to condition
    // against. 1f for the ConsumableItemEffect constructor's item-less case; that path has no durability, heat or
    // power-supply state to be broken by, so it always reads perfect.
    public float Condition => Item?.ConditionRatio(_data.Thrust) ?? 1f;

    public Thruster(ThrusterData data, EquippedItem item) : base(data, item)
    {
        _data = data;
        var hullData = ItemManager.GetData(Entity.Hull) as HullData;
        var hullCenter = hullData.Shape.CenterOfMass;
        var itemData = ItemManager.GetData(item.EquippableItem);
        var itemCenter = hullData.Shape.Inset(itemData.Shape, item.Position, item.EquippableItem.Rotation).CenterOfMass;
        var toCenter = hullCenter - itemCenter;
        Torque = -dot(normalize(toCenter), float2(1, 0).Rotate(item.EquippableItem.Rotation));
    }

    public Thruster(ThrusterData data, ConsumableItemEffect item) : base(data, item)
    {
        _data = data;
        Torque = 0;
    }

    // Cut 3 (docs/stats-and-power-cut.md): the resolved stat times the behaviour-supplied throttle scalar (§1.2),
    // exactly the shape the map names. _input is set externally (the ship's controls) before Entity.Update calls
    // PowerBus.Step, so it is already current when this runs.
    //
    // Nominal-request ruling (docs/stats-and-power-cut.md, operator ruling 2026-09-19): EnergyUsage is a
    // registered request field (StatValidation.PowerRequestFields) -- read nominally, same reasoning as every
    // other IPowerConsumer in this cut, though EnergyUsage carries no PowerSupply term in Thruster's own shipped
    // catalog today; Thrust (the field Cut 7 curves) is a separate stat Execute reads with the real Evaluate.
    public float PowerRequest(float dt) => _input > 0f ? _input * EvaluateNominalPower(_data.EnergyUsage) : 0f;

    // Cut 5 (docs/stats-and-power-cut.md §1.3, PowerTiers.cs): Medium -- mobility. Losing thrust for a tick
    // under brownout is an inconvenience, not the cascading failure a starved radiator or shield causes.
    public int DefaultPowerTier => PowerTiers.Medium;

    // What this thruster adds to the ship's manoeuvre envelope: Execute's own arithmetic at full input, read live.
    // Its push is Thrust / Mass along its mount, into the matching body
    // axis, even for a flank thruster that only turns (it pushes whenever it turns); its turn is |Torque| * Thrust *
    // TorqueMultiplier / Mass into the side its Torque sign fires on, when |Torque| clears the floor. Zero when the
    // item is offline or unpowered, the same gates Execute applies.
    public ManoeuvreEnvelope Manoeuvre()
    {
        if (Item == null || !Item.Active.Value || Item.PowerSupply <= 1e-4f) return default;
        var settings = ItemManager.GameplaySettings;
        var thrust = Thrust;
        var acceleration = thrust / Entity.Mass;
        var forward = normalize(Entity.Direction);
        var right = forward.Rotate(ItemRotation.Clockwise);
        var push = -forward.Rotate(Item.EquippableItem.Rotation);
        var along = dot(push, forward);
        var across = dot(push, right);
        var turn = abs(Torque) > settings.TorqueFloor ? abs(Torque) * thrust * settings.TorqueMultiplier / Entity.Mass : 0f;
        return new ManoeuvreEnvelope(
            max(along, 0f) * acceleration, max(-along, 0f) * acceleration,
            max(-across, 0f) * acceleration, max(across, 0f) * acceleration,
            Torque > 0f ? turn : 0f, Torque < 0f ? turn : 0f);
    }

    public override bool Execute(float dt)
    {
        Item.SetAudioParameter(SpecialAudioParameter.Intensity, _input);
        // Cut 7 (docs/stats-and-power-target.md): "continuous consumers brown out ... through a power supply
        // curve on their performance stats." Thrust below is a plain Evaluate() read, so once the catalog's own
        // Thrust stat carries a PowerSupply term, a partial grant already comes back reduced -- this gate no
        // longer demands a full grant, only that the thruster is being asked to do anything (_input) and that it
        // has not been cut to true zero supply (the epsilon PowerBus itself already treats as "nothing granted").
        if(_input > 0f && Item.PowerSupply > 1e-4f)
        {
            var thrust = Thrust;
            Entity.Velocity -= Direction.xz * _input * thrust / Entity.Mass * dt;
            var turnRate = _input * Torque * thrust * ItemManager.GameplaySettings.TorqueMultiplier / Entity.Mass;
            Entity.Direction = mul(Entity.Direction, CultMath.float2x2.Rotate(turnRate * dt));
            Entity.TurnRate += turnRate;
            AddHeat(_input * Evaluate(_data.Heat) * dt);
            var vis = _input * Evaluate(_data.Visibility);
            if (!Entity.VisibilitySources.ContainsKey(this) || vis > Entity.VisibilitySources[this])
                Entity.VisibilitySources[this] = vis;
            return true;
        }
        return false;
    }
}