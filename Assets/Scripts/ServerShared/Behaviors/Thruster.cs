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

public class Thruster : Behavior, IAnalogBehavior, IPowerConsumer
{
    public float Thrust { get; private set; }
    public float Torque { get; }

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
        Thrust = Evaluate(_data.Thrust);
    }

    public Thruster(ThrusterData data, ConsumableItemEffect item) : base(data, item)
    {
        _data = data;
        Torque = 0;
        Thrust = Evaluate(_data.Thrust);
    }

    // Cut 3 (docs/stats-and-power-cut.md): the resolved stat times the behaviour-supplied throttle scalar (§1.2),
    // exactly the shape the map names. _input is set externally (the ship's controls) before Entity.Update calls
    // PowerBus.Step, so it is already current when this runs.
    //
    // Nominal-request ruling (docs/stats-and-power-cut.md, operator ruling 2026-09-19): EnergyUsage is a
    // registered request field (StatValidation.PowerRequestFields) -- read nominally, same reasoning as every
    // other IPowerConsumer in this cut, though EnergyUsage carries no PowerSupply term in Thruster's own shipped
    // catalog today; Thrust (the field Cut 7 curves) is a separate stat Execute reads with the real Evaluate.
    public float PowerRequest(float dt) => _input > .01f ? _input * EvaluateNominalPower(_data.EnergyUsage) : 0f;

    // Cut 5 (docs/stats-and-power-cut.md §1.3, PowerTiers.cs): Medium -- mobility. Losing thrust for a tick
    // under brownout is an inconvenience, not the cascading failure a starved radiator or shield causes.
    public int DefaultPowerTier => PowerTiers.Medium;

    public override bool Execute(float dt)
    {
        Item.SetAudioParameter(SpecialAudioParameter.Intensity, _input);
        // Cut 7 (docs/stats-and-power-target.md): "continuous consumers brown out ... through a power supply
        // curve on their performance stats." Thrust below is a plain Evaluate() read, so once the catalog's own
        // Thrust stat carries a PowerSupply term, a partial grant already comes back reduced -- this gate no
        // longer demands a full grant, only that the thruster is being asked to do anything (_input) and that it
        // has not been cut to true zero supply (the epsilon PowerBus itself already treats as "nothing granted").
        if(_input > .01f && Item.PowerSupply > 1e-4f)
        {
            Thrust = Evaluate(_data.Thrust);
            Entity.Velocity -= Direction.xz * _input * Thrust / Entity.Mass * dt;
            Entity.Direction = mul(Entity.Direction,
                CultMath.float2x2.Rotate(_input * Torque * Thrust * ItemManager.GameplaySettings.TorqueMultiplier / Entity.Mass * dt));
            AddHeat(_input * Evaluate(_data.Heat) * dt);
            var vis = _input * Evaluate(_data.Visibility);
            if (!Entity.VisibilitySources.ContainsKey(this) || vis > Entity.VisibilitySources[this])
                Entity.VisibilitySources[this] = vis;
            return true;
        }
        return false;
    }
}