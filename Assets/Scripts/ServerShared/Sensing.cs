/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using CultMath;

// Light signatures split by channel (ruling visibility-per-capability-reveal): an observer learns what a ship
// is by which channel carries the light. Append-only: values are serialized in VisibilityData.
public enum SignatureChannel
{
    Thermal,
    Reflection,
    DrivePlume,
    LaunchFlash,
    Emission
}

public readonly struct Signature
{
    public readonly SignatureChannel Channel;
    public readonly float Strength;
    public readonly EquippedItem Item;

    public Signature(SignatureChannel channel, float strength, EquippedItem item)
    {
        Channel = channel;
        Strength = strength;
        Item = item;
    }

    public Signature WithStrength(float strength) => new Signature(Channel, strength, Item);
}

// What a painted ship learns of a beam on it (ruling sensor-stat-set item 4): a Search paint is a ping sweeping
// it (Strength 0); a Track paint is a lock (Strength is the emitter's own lock progress, 0..1, ruling
// lock-warning-strength). Never summed.
public enum PaintMode
{
    Search,
    Track
}

// An ally's ping crossed `Subject` at `IlluminatedRange` through `IlluminatedObscuration`; `MaxRange` is that
// sensor's PingRange. Posted on the crossing tick and read for one tick by the receiving ship's own sensors.
public readonly struct PingReturn
{
    public readonly Entity Subject;
    public readonly float IlluminatedRange;
    public readonly float IlluminatedObscuration;
    public readonly float MaxRange;

    public PingReturn(Entity subject, float illuminatedRange, float illuminatedObscuration, float maxRange)
    {
        Subject = subject;
        IlluminatedRange = illuminatedRange;
        IlluminatedObscuration = illuminatedObscuration;
        MaxRange = maxRange;
    }
}

public readonly struct PaintWarning
{
    public readonly float2 Bearing;
    public readonly EquippableItemData EmitterClass;
    public readonly PaintMode Mode;
    public readonly float Strength;

    public PaintWarning(float2 bearing, EquippableItemData emitterClass, PaintMode mode, float strength)
    {
        Bearing = bearing;
        EmitterClass = emitterClass;
        Mode = mode;
        Strength = strength;
    }
}

// What an observer has seen `Item` do (ruling visibility-per-capability-reveal): the channel it showed on, the
// bearing at that act and the zone clock then. Held per (subject, item) in the observer's Entity; the latest act
// replaces the earlier one. Position is never carried: only the track gives it.
public readonly struct Reveal
{
    public readonly EquippedItem Item;
    public readonly SignatureChannel Channel;
    public readonly float2 Bearing;
    public readonly float Time;

    public Reveal(EquippedItem item, SignatureChannel channel, float2 bearing, float time)
    {
        Item = item;
        Channel = channel;
        Bearing = bearing;
        Time = time;
    }
}

// A subject's removal from a zone as the zone recorded it (death, dock, warp, any removal): where it was and how
// bright its light was (its signature outside the Emission channel). Judged by each observer's Sense under the
// light-act gate; notification only, one tick long, it decides nothing.
public readonly struct Departure
{
    public readonly Entity Subject;
    public readonly float2 Position;
    public readonly float Signature;

    public Departure(Entity subject, float2 position, float signature)
    {
        Subject = subject;
        Position = position;
        Signature = signature;
    }
}

// How much an observer holds of one subject, derived on read by Entity.ContactOf from its track, its reveals and
// the zone clock. Detected: tracked, or a reveal younger than GameplaySettings.BearingMemory. Tracked: exactly
// membership of VisibleEntities. Classified: detected, with any reveal in the book. Identified: the track has reached
// TargetGearInfoThreshold. Bearing is the true bearing while tracked, else the freshest reveal's.
public readonly struct Contact
{
    public readonly bool Detected;
    public readonly bool Tracked;
    public readonly bool Classified;
    public readonly bool Identified;
    public readonly float2 Bearing;
    public readonly float Info;

    public Contact(bool detected, bool tracked, bool classified, bool identified, float2 bearing, float info)
    {
        Detected = detected;
        Tracked = tracked;
        Classified = classified;
        Identified = identified;
        Bearing = bearing;
        Info = info;
    }
}
