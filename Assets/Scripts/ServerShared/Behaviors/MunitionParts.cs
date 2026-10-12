/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Linq;
using MessagePack;
using Newtonsoft.Json;

// A missile type's stats, as five dedicated gear-part behaviours on the launcher's own design, one per gear
// category the entity cut will derive them from (ruling drones-and-munitions-are-entities: "dedicated gear so a
// missile thruster is not the same as a ship thruster"). Ruling missile-stats-now-gear-later, in the operator's
// words: "For now these stats will belong to the missile, later they will be derived from gear." Each part becomes
// a derivation from (or moves onto) that munition gear when munitions are entities.
//
// Each part is its own BehaviorData because every stat walker (role validation, StatModifier targeting, AetherDb
// roles, the trade panel) reads PerformanceStat fields one level deep. The parts are inert: nothing here executes,
// and no ship-wide reader (thrust allocator, sensing, signatures) reads them, because a missile thruster is not a
// ship thruster and a seeker is not a ship sensor. MunitionParts.Of is the one read path.
public abstract class MunitionPartData : BehaviorData
{
    public override Behavior CreateInstance(EquippedItem item) => new MunitionPart(this, item);
    public override Behavior CreateInstance(ConsumableItemEffect consumable) => new MunitionPart(this, consumable);
}

// What a part instantiates to: no state and no override, so Execute stays the default true.
public sealed class MunitionPart : Behavior
{
    public MunitionPart(MunitionPartData data, EquippedItem item) : base(data, item) { }
    public MunitionPart(MunitionPartData data, ConsumableItemEffect consumable) : base(data, consumable) { }
}

public static class MunitionParts
{
    // The design's one part of that type. Catalog load and Upsert refuse a munition without exactly one of each.
    public static T Of<T>(EquippedItem item) where T : MunitionPartData =>
        item.Behaviors.Select(b => b.Data).OfType<T>().Single();
}

// The missile hull.
[Inspectable, MessagePackObject, JsonObject(MemberSerialization.OptIn)]
public sealed class MunitionHullData : MunitionPartData
{
    [Inspectable, JsonProperty("durability"), Key(1), RuntimeInspectable]
    public PerformanceStat Durability = new PerformanceStat();

    // Metres.
    [Inspectable, JsonProperty("radius"), Key(2)]
    public PerformanceStat Radius = new PerformanceStat();
}

// The missile thruster.
[Inspectable, MessagePackObject, JsonObject(MemberSerialization.OptIn)]
public sealed class MunitionThrusterData : MunitionPartData
{
    // The main motor, m/s^2.
    [Inspectable, JsonProperty("acceleration"), Key(1), RuntimeInspectable]
    public PerformanceStat Acceleration = new PerformanceStat();

    // The sideways acceleration cap, m/s^2.
    [Inspectable, JsonProperty("lateral"), Key(2)]
    public PerformanceStat Lateral = new PerformanceStat();

    // m/s.
    [Inspectable, JsonProperty("maxSpeed"), Key(3), RuntimeInspectable]
    public PerformanceStat MaxSpeed = new PerformanceStat();

    // The strength of this motor's DrivePlume at full throttle, as ThrusterData.Visibility. A datum only: it is
    // never registered as the launching ship's signature.
    [Inspectable, JsonProperty("visibility"), Key(4)]
    public PerformanceStat Visibility = new PerformanceStat();
}

// The seeker. Passive here: it reads a target and emits nothing.
[Inspectable, MessagePackObject, JsonObject(MemberSerialization.OptIn)]
public sealed class MunitionSeekerData : MunitionPartData
{
    // Detection power, as SensorData.Sensitivity.
    [Inspectable, JsonProperty("sensitivity"), Key(1)]
    public PerformanceStat Sensitivity = new PerformanceStat();

    // The full cone, in degrees (ruling sensor-stat-set's name and unit).
    [Inspectable, JsonProperty("beamWidth"), Key(2)]
    public PerformanceStat BeamWidth = new PerformanceStat();

    // Metres.
    [Inspectable, JsonProperty("range"), Key(3)]
    public PerformanceStat Range = new PerformanceStat();
}

// Guidance and its cognition.
[Inspectable, MessagePackObject, JsonObject(MemberSerialization.OptIn)]
public sealed class MunitionControlData : MunitionPartData
{
    // The proportional-navigation constant.
    [Inspectable, JsonProperty("navigation"), Key(1)]
    public PerformanceStat Navigation = new PerformanceStat();

    // The share of Lateral spent jinking.
    [Inspectable, JsonProperty("evasion"), Key(2)]
    public PerformanceStat Evasion = new PerformanceStat();

    // Hz.
    [Inspectable, JsonProperty("evasionFrequency"), Key(3), RuntimeInspectable]
    public PerformanceStat EvasionFrequency = new PerformanceStat();

    // Seconds.
    [Inspectable, JsonProperty("lifetime"), Key(4)]
    public PerformanceStat Lifetime = new PerformanceStat();

    // The missile's own skill, 0 to 1 (ruling missile-cognition: "a smarter missile can better spend its delta v").
    // Four uses: guidance efficiency, when to jink against point defense, how much fuel it keeps for the terminal,
    // and the price of the hit.
    [Inspectable, JsonProperty("cognition"), Key(5)]
    public PerformanceStat Cognition = new PerformanceStat();
}

// The tank.
[Inspectable, MessagePackObject, JsonObject(MemberSerialization.OptIn)]
public sealed class MunitionFuelData : MunitionPartData
{
    // Total manoeuvre budget, m/s.
    [Inspectable, JsonProperty("deltaV"), Key(1)]
    public PerformanceStat DeltaV = new PerformanceStat();
}
