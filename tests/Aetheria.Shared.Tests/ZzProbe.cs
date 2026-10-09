using System;
using System.Text;
using CultMath;
using Xunit;
using Xunit.Abstractions;
namespace Aetheria.Shared.Tests;
public class ZzProbe
{
    readonly ITestOutputHelper _o; public ZzProbe(ITestOutputHelper o) { _o = o; }
    const float Deg = MathF.PI / 180f;
    static float3 Col(float a, float b, float c) => new(a, b, c * Deg);
    [Fact]
    public void Probe()
    {
        var duel = new[] { Col(0, 53.94f, 49.24f), Col(0, 61.19f, -55.86f), Col(29.08f, 0, 166.26f), Col(-21.80f, 0, -124.63f) };
        foreach (var warmFirst in new[] { false, true })
        {
            var sb = new StringBuilder();
            var al = new ThrustAllocator();
            if (warmFirst) al.Allocate(duel, new float2(0, 0), 1f, new float[4]);
            foreach (var t in new[] { 0f, .001f, .002f, .005f, .01f, .1f, .3f, .5f, .7f, .76f, .78f, .8f, 1f })
            {
                var th = new float[4]; al.Allocate(duel, new float2(0, 0), t, th);
                sb.Append($"t={t}: {th[0]:G4} {th[1]:G4} {th[2]:G4} {th[3]:G4}\n");
            }
            _o.WriteLine("warm=" + warmFirst + "\n" + sb);
        }
        Assert.Fail("PROBE");
    }
}
