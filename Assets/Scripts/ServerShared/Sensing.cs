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
