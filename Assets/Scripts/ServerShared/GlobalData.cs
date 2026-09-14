/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections;
using System.Collections.Generic;
using MessagePack;
using Newtonsoft.Json;
using CultMath;
using static CultMath.math;
using static NoiseFbm;

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
