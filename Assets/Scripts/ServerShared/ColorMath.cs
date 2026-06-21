/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using static CultMath.math;
using cfloat3 = CultMath.float3;
using cfloat4 = CultMath.float4;

// Thanks to Ian Taylor at https://www.chilliant.com/rgb2hsv.html
public static class ColorMath
{
    public static cfloat3 HueToRgb(in float h)
    {
        float r = abs(h * 6 - 3) - 1;
        float g = 2 - abs(h * 6 - 2);
        float b = 2 - abs(h * 6 - 4);
        return saturate(float3(r,g,b));
    }
    
    public static cfloat3 HsvToRgb(in cfloat3 hsv)
    {
        cfloat3 rgb = HueToRgb(hsv.x);
        return ((rgb - 1) * hsv.y + 1) * hsv.z;
    }

    public static cfloat3 HslToRgb(in cfloat3 hsl)
    {
        cfloat3 rgb = HueToRgb(hsl.x);
        float c = (1 - abs(2 * hsl.z - 1)) * hsl.y;
        return (rgb - 0.5f) * c + hsl.z;
    }

    const float Epsilon = 1e-10f;
    public static cfloat3 RgbToHcv(in cfloat3 rgb)
    {
        // Based on work by Sam Hocevar and Emil Persson
        cfloat4 p = (rgb.y < rgb.z) ? float4(rgb.zy, -1.0f, 2.0f/3.0f) : float4(rgb.zy, 0.0f, -1.0f/3.0f);
        cfloat4 q = (rgb.x < p.x) ? float4(p.xyw, rgb.x) : float4(rgb.x, p.yzx);
        float c = q.x - min(q.w, q.y);
        float h = abs((q.w - q.y) / (6 * c + Epsilon) + q.z);
        return float3(h, c, q.x);
    }

    public static cfloat3 RgbToHsv(in cfloat3 rgb)
    {
        cfloat3 hcv = RgbToHcv(rgb);
        float s = hcv.y / (hcv.z + Epsilon);
        return float3(hcv.x, s, hcv.z);
    }

    public static cfloat3 RgbToHsl(in cfloat3 rgb)
    {
        cfloat3 hcv = RgbToHcv(rgb);
        float l = hcv.z - hcv.y * 0.5f;
        float s = hcv.y / (1 - abs(l * 2 - 1) + Epsilon);
        return float3(hcv.x, s, l);
    }
}
