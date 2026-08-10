/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using static Unity.Mathematics.math;

// Thanks to Ian Taylor at https://www.chilliant.com/rgb2hsv.html
public static class ColorMath
{
    public static float3 HueToRgb(in float h)
    {
        float r = abs(h * 6 - 3) - 1;
        float g = 2 - abs(h * 6 - 2);
        float b = 2 - abs(h * 6 - 4);
        return saturate(float3(r,g,b));
    }
    
    public static float3 HsvToRgb(in float3 hsv)
    {
        float3 rgb = HueToRgb(hsv.x);
        return ((rgb - 1) * hsv.y + 1) * hsv.z;
    }
    
    public static float3 HslToRgb(in float3 hsl)
    {
        float3 rgb = HueToRgb(hsl.x);
        float c = (1 - abs(2 * hsl.z - 1)) * hsl.y;
        return (rgb - 0.5f) * c + hsl.z;
    }
    
    const float Epsilon = 1e-10f;
    public static float3 RgbToHcv(in float3 rgb)
    {
        // Based on work by Sam Hocevar and Emil Persson
        float4 p = (rgb.y < rgb.z) ? float4(rgb.zy, -1.0f, 2.0f/3.0f) : float4(rgb.zy, 0.0f, -1.0f/3.0f);
        float4 q = (rgb.x < p.x) ? float4(p.xyw, rgb.x) : float4(rgb.x, p.yzx);
        float c = q.x - min(q.w, q.y);
        float h = abs((q.w - q.y) / (6 * c + Epsilon) + q.z);
        return float3(h, c, q.x);
    }
    
    public static float3 RgbToHsv(in float3 rgb)
    {
        float3 hcv = RgbToHcv(rgb);
        float s = hcv.y / (hcv.z + Epsilon);
        return float3(hcv.x, s, hcv.z);
    }
    
    public static float3 RgbToHsl(in float3 rgb)
    {
        float3 hcv = RgbToHcv(rgb);
        float l = hcv.z - hcv.y * 0.5f;
        float s = hcv.y / (1 - abs(l * 2 - 1) + Epsilon);
        return float3(hcv.x, s, l);
    }
}
