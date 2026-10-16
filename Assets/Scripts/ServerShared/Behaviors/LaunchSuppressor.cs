/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using MessagePack;
using Newtonsoft.Json;
using static CultMath.math;

// Electronic warfare, hide-launch (campaign aetheria-ew, invariants lie-is-own-emission and lies-cost-real-heat): a
// gear that thins its own ship's launch flash. It never stores a factor and never writes a signature: Entity.SetSignature
// asks each equipped suppressor at write time, so the order in which a weapon and a suppressor execute decides nothing.
[Inspectable, MessagePackObject, JsonObject(MemberSerialization.OptIn), RuntimeInspectable]
public class LaunchSuppressorData : BehaviorData
{
    // The multiplier on the flash, clamped to 0..1: a suppressor never brightens a launch.
    [Inspectable, JsonProperty("factor"), Key(1), RuntimeInspectable]
    public PerformanceStat Factor = new PerformanceStat();

    // Heat paid to the suppressor's own cell per unit of strength it removed.
    [Inspectable, JsonProperty("heatPerUnit"), Key(2)]
    public PerformanceStat HeatPerUnit = new PerformanceStat();

    // Above this item temperature the suppressor scales nothing and pays nothing.
    [Inspectable, JsonProperty("ratedTemperature"), Key(3)]
    public PerformanceStat RatedTemperature = new PerformanceStat();

    public override Behavior CreateInstance(EquippedItem item)
    {
        return new LaunchSuppressor(this, item);
    }
    public override Behavior CreateInstance(ConsumableItemEffect item)
    {
        return new LaunchSuppressor(this, item);
    }
}

public class LaunchSuppressor : Behavior
{
    private readonly LaunchSuppressorData _data;

    public LaunchSuppressor(LaunchSuppressorData data, EquippedItem item) : base(data, item)
    {
        _data = data;
    }
    public LaunchSuppressor(LaunchSuppressorData data, ConsumableItemEffect item) : base(data, item)
    {
        _data = data;
    }

    // Called by Entity.SetSignature for every light signature the ship writes; the strength passes through unchanged
    // unless this is a launch flash and the suppressor is running below its rated temperature.
    internal float Suppress(SignatureChannel channel, float strength)
    {
        if (channel != SignatureChannel.LaunchFlash || Item == null || !Item.Active.Value || Temperature > Evaluate(_data.RatedTemperature))
            return strength;
        var scaled = strength * clamp(Evaluate(_data.Factor), 0f, 1f);
        AddHeat((strength - scaled) * Evaluate(_data.HeatPerUnit));
        return scaled;
    }

    public override bool Execute(float dt) => true;
}
