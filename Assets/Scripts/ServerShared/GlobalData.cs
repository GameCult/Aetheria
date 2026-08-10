/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections;
using System.Collections.Generic;
using MessagePack;
using Newtonsoft.Json;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using static Unity.Mathematics.noise;
using static NoiseFbm;

[Inspectable, Serializable, RethinkTable("Galaxy"), MessagePackObject, JsonObject(MemberSerialization.OptIn)]
public class GalaxyMapLayerData : DatabaseEntry, INamedEntry
{
    [Inspectable, JsonProperty("coreBoost"), Key(1)]  
    public float CoreBoost = 1.05f;

    [Inspectable, JsonProperty("coreBoostOffset"), Key(2)]  
    public float CoreBoostOffset = .1f;

    [Inspectable, JsonProperty("coreBoostPower"), Key(3)]  
    public float CoreBoostPower = 2.25f;

    [Inspectable, JsonProperty("spokeScale"), Key(4)]  
    public float SpokeScale = 1;

    [Inspectable, JsonProperty("spokeOffset"), Key(5)]  
    public float SpokeOffset = 0;

    [Inspectable, JsonProperty("edgeReduction"), Key(6)]  
    public float EdgeReduction = 3;

    [Inspectable, JsonProperty("noiseOffset"), Key(7)]  
    public float NoiseOffset = 0;

    [Inspectable, JsonProperty("noiseAmplitude"), Key(8)]  
    public float NoiseAmplitude = 1.5f;

    [Inspectable, JsonProperty("noiseGain"), Key(9)]  
    public float NoiseGain = .7f;

    [Inspectable, JsonProperty("noiseLacunarity"), Key(10)]  
    public float NoiseLacunarity = 2;

    [Inspectable, JsonProperty("noiseOctaves"), Key(11)]  
    public int NoiseOctaves = 7;

    [Inspectable, JsonProperty("noiseFrequency"), Key(12)]  
    public float NoiseFrequency = 1;

    [Inspectable, JsonProperty("noisePosition"), Key(13)]  
    public float NoisePosition = 1337;

    [Inspectable, JsonProperty("name"), Key(14)]  
    public string Name;

    public float Evaluate(float2 uv, GalaxyShapeSettings settings)
    {
        float2 offset = -float2(.5f, .5f)+uv;
        float circle = (.5f-length(offset))*2;
        float angle = pow(length(offset)*2,settings.TwistExponent) * settings.Twist;
        float2 t = float2(offset.x*cos(angle) - offset.y*sin(angle), offset.x*sin(angle) + offset.y*cos(angle));
        float atan = atan2(t.y,t.x);
        float spokes = (sin(atan*settings.Arms) + SpokeOffset) * SpokeScale;
        float noise = fBm(uv + float2(NoisePosition), NoiseOctaves, NoiseFrequency, NoiseOffset, NoiseAmplitude, NoiseLacunarity, NoiseGain);
        float shape = lerp(spokes - EdgeReduction * length(offset), 1, pow(circle, CoreBoostPower) * CoreBoost) + CoreBoostOffset;
        float gal = max(shape - noise * saturate(circle), 0);

        return gal;
    }

    [IgnoreMember] public string EntryName
    {
        get => Name;
        set => Name = value;
    }
}

public static class NoiseFbm
{
    public static float3 fBm3(float2 p, int octaves, float frequency, float offset, float amplitude, float lacunarity, float gain)
    {
        return float3(
            fBm(p, octaves, frequency, offset, amplitude, lacunarity, gain),
            fBm(p+10, octaves, frequency, offset, amplitude, lacunarity, gain),
            fBm(p+20, octaves, frequency, offset, amplitude, lacunarity, gain));
    }
    
    public static float fBm(float2 p, int octaves, float frequency, float offset, float amplitude, float lacunarity, float gain)
    {
        float freq = frequency, amp = .5f;
        float sum = 0;	
        for(int i = 0; i < octaves; i++) 
        {
            sum += snoise(p * freq) * amp;
            freq *= lacunarity;
            amp *= gain;
        }
        return (sum + offset)*amplitude;
    }

    public static float3 fBm3(float p, int octaves, float frequency, float offset, float amplitude, float lacunarity, float gain)
    {
        return float3(
            fBm(p, octaves, frequency, offset, amplitude, lacunarity, gain),
            fBm(p+10, octaves, frequency, offset, amplitude, lacunarity, gain),
            fBm(p+20, octaves, frequency, offset, amplitude, lacunarity, gain));
    }
    
    public static float fBm(float p, int octaves, float frequency, float offset, float amplitude, float lacunarity, float gain)
    {
        float freq = frequency, amp = .5f;
        float sum = 0;	
        for(int i = 0; i < octaves; i++) 
        {
            sum += Noise1D.noise(p * freq) * amp;
            freq *= lacunarity;
            amp *= gain;
        }
        return (sum + offset)*amplitude;
    }
}
