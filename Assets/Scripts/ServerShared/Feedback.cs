/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using CultMath;
using static CultMath.math;

// What an impact presenter renders, derived once. Engine-free and never called by ServerShared: FireControl
// publishes the fact (AbsorbEvent), this derives how it reads, and every presenter (visual, audio, camera) renders
// the same cue instead of computing its own.
public readonly struct ImpactCue
{
    public readonly AbsorbLayer Layer;
    public readonly float3 Position;
    public readonly float3 Direction;
    public readonly float Intensity;
    public readonly float Shake;

    public ImpactCue(AbsorbLayer layer, float3 position, float3 direction, float intensity, float shake)
    {
        Layer = layer;
        Position = position;
        Direction = direction;
        Intensity = intensity;
        Shake = shake;
    }
}

public static class Feedback
{
    // Intensity is the hit's share of what the receiver's hull can take, scaled by the presenter and saturated to
    // 0..1; a receiver with no hull to measure against reads 0. Shake follows intensity for the player's own ship
    // only, scaled by the player's ScreenShake preference.
    public static ImpactCue Cue(AbsorbEvent e, float receiverMaxDurability, bool receiverIsPlayer, float shakePreference, float intensityScale)
    {
        var intensity = receiverMaxDurability > 0f ? saturate(e.Magnitude / receiverMaxDurability * intensityScale) : 0f;
        var shake = receiverIsPlayer ? intensity * saturate(shakePreference) : 0f;
        return new ImpactCue(e.Layer, e.Position, e.Direction, intensity, shake);
    }
}
