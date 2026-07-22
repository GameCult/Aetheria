using CultMath;
using NUnit.Framework;
using static CultMath.math;

public sealed class CultMathPrimitiveTests
{
    [Test]
    public void Int2SupportsAetheriaGridArithmetic()
    {
        var cell = int2(2, 3);

        Assert.AreEqual(new int2(3, 5), cell + int2(1, 2));
        Assert.AreEqual(new float2(2.0f, 3.0f), (float2)cell);
    }

    [Test]
    public void AetheriaMathRotatesItemDirectionsOnShipPlane()
    {
        var forward = float2(0, 1);

        Assert.AreEqual(float2(-1, 0), AetheriaMath.Rotate(forward, ItemRotation.CounterClockwise));
        Assert.AreEqual(float2(0, -1), AetheriaMath.Rotate(forward, ItemRotation.Reversed));
        Assert.AreEqual(float2(1, 0), AetheriaMath.Rotate(forward, ItemRotation.Clockwise));
    }

    [Test]
    public void ItemRotationDirectionsAreCultMathVectors()
    {
        Assert.AreEqual(float2(0, 1), ItemRotation.None.Direction());
        Assert.AreEqual(float2(1, 0), ItemRotation.Clockwise.Direction());
        Assert.AreEqual(float2(0, -1), ItemRotation.Reversed.Direction());
        Assert.AreEqual(float2(-1, 0), ItemRotation.CounterClockwise.Direction());
    }

    [Test]
    public void AetheriaMathProjectsUnityVectorsOntoShipPlane()
    {
        var world = new Unity.Mathematics.float3(2, 99, 3);

        Assert.AreEqual(float2(2, 3), AetheriaMath.ToCultXZ(world));
        Assert.AreEqual(new Unity.Mathematics.float3(2, 0, 3), AetheriaMath.ToUnityXZ(float2(2, 3)));
    }

    [Test]
    public void AetheriaMathCalculatesStableAngles()
    {
        Assert.That(AetheriaMath.AngleDegrees(float2(1, 0), float2(0, 1)), Is.EqualTo(90).Within(0.0001f));
        Assert.That(AetheriaMath.AngleRadians(float2(1, 0), float2(0, 1)), Is.EqualTo(PI / 2).Within(0.0001f));
        Assert.That(AetheriaMath.AngleDegrees(float3(1, 0, 0), float3(1, 0, 0)), Is.EqualTo(0).Within(0.0001f));
        Assert.That(AetheriaMath.AngleDegrees(float3(0, 0, 0), float3(1, 0, 0)), Is.EqualTo(0).Within(0.0001f));
    }

    [Test]
    public void ColorMathUsesCultMathVectorsAndSharedUnityBridge()
    {
        var rgb = ColorMath.HsvToRgb(float3(0, 1, 1));

        Assert.AreEqual(float3(1, 0, 0), rgb);
        Assert.AreEqual(new Unity.Mathematics.float3(1, 0, 0), AetheriaMath.ToUnity(rgb));
    }

    [Test]
    public void RectNormalizesBoundsAndContainsPoints()
    {
        var viewport = rect(10, 20, -5, -2);

        Assert.AreEqual(float2(-5, -2), viewport.min);
        Assert.AreEqual(float2(10, 20), viewport.max);
        Assert.AreEqual(float2(15, 22), viewport.size);
        Assert.AreEqual(float2(2.5f, 9), viewport.center);
        Assert.IsTrue(viewport.Contains(float2(0, 0)));
        Assert.IsFalse(viewport.Contains(float2(11, 0)));
    }
}
