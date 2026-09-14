using System;
using System.Collections.Generic;
using NUnit.Framework;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CultMath;



class BresenhamTest
{ 
    [Test]
    public void LowPositiveGradientTest()
    {
        Shape shape = new Shape(7, 7);
        shape.SetLine(new float2(1, 1f), new float2(6f, 3f));

        var coords = new[] { (1, 1), (2, 1), (3, 2), (4, 2), (5, 3), (6, 3)};
        foreach(var coord in coords)
        {
            Assert.True(shape.Cells[coord.Item1, coord.Item2], $"{coord.Item1}, {coord.Item2}");
        }
    }

    [Test]
    public void LowNegativeGradientTest()
    {
        Shape shape = new Shape(5, 5);
        shape.SetLine(new float2(-2f, 4f), new float2(3f, 2f));

        var coords = new[] { (0, 3), (1, 3), (2, 2), (3, 2)};
        foreach (var coord in coords)
        {
            Assert.True(shape.Cells[coord.Item1, coord.Item2], $"{coord.Item1}, {coord.Item2}");
        }
    }

    [Test]
    public void HighPositiveGradientTest()
    {
        Shape shape = new Shape(4, 4);
        shape.SetLine(new float2(1f, 3f), new float2(0f, 0f));

        var coords = new[] { (0, 0), (0, 1), (1, 2), (1, 3) };
        foreach (var coord in coords)
        {
            Assert.True(shape.Cells[coord.Item1, coord.Item2], $"{coord.Item1}, {coord.Item2}");
        }
    }

    [Test]
    public void FloatingPointYTest()
    {
        Shape shape = new Shape(7, 7);
        shape.SetLine(new float2(0.6f, 1.4f), new float2(6f, 2.6f));

        var coords = new[] { (1, 1), (2, 2), (3, 2), (4, 2), (5, 2), (6, 3) };
        foreach (var coord in coords)
        {
            Assert.True(shape.Cells[coord.Item1, coord.Item2], $"{coord.Item1}, {coord.Item2}");
        }
    }


    [Test]
    public void FloatingPointXTest()
    {
        Shape shape = new Shape(7, 7);
        shape.SetLine(new float2(0.6f, 1f), new float2(5.6f, 3f));

        var coords = new[] { (1, 1), (2, 2), (3, 2), (4, 2), (5, 3), (6, 3) };
        foreach (var coord in coords)
        {
            Assert.True(shape.Cells[coord.Item1, coord.Item2], $"{coord.Item1}, {coord.Item2}");
        }
    }
}


