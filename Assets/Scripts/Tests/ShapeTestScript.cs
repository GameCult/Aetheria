using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using CultMath;
using static CultMath.math;
using int2 = CultMath.int2;


public class ShapeTestScript
{
    [Test]
    public void ShapeRotation()
    {
        // Use the Assert class to test conditions
        var verticalShape = new Shape(1, 2);
        foreach (var v in verticalShape.AllCoordinates) verticalShape[v] = true;
        
        Assert.True(verticalShape.Rotate(int2.zero, ItemRotation.None).Equals(int2.zero), "verticalShape.Rotate(int2.zero, ItemRotation.None).Equals(int2.zero)");
        Assert.True(verticalShape.Rotate(int2(0,1), ItemRotation.Reversed).Equals(int2.zero), "verticalShape.Rotate(int2(0,1), ItemRotation.Reversed).Equals(int2.zero)");
        Assert.True(verticalShape.Rotate(int2(0,1), ItemRotation.CounterClockwise).Equals(int2.zero), "verticalShape.Rotate(int2(0,1), ItemRotation.CounterClockwise).Equals(int2.zero)");
        Assert.True(verticalShape.Rotate(int2(0,1), ItemRotation.Clockwise).Equals(int2(1,0)), "verticalShape.Rotate(int2(0,1), ItemRotation.Clockwise).Equals(int2(1,0))");
    }
    
    [Test]
    public void ShapeFitsWithin()
    {
        // Use the Assert class to test conditions
        var verticalShape = new Shape(1, 2);
        foreach (var v in verticalShape.AllCoordinates) verticalShape[v] = true;
        
        Assert.True(verticalShape.FitsWithin(verticalShape, ItemRotation.None, out _), "verticalShape.FitsWithin(verticalShape, ItemRotation.None, out _)");
        
        var horizontalShape = new Shape(2, 1);
        foreach (var v in horizontalShape.AllCoordinates) horizontalShape[v] = true;
        
        Assert.True(horizontalShape.FitsWithin(verticalShape, ItemRotation.Clockwise, out _), "horizontalShape.FitsWithin(verticalShape, ItemRotation.Clockwise, out _)");
        
        Assert.True(horizontalShape.FitsWithin(verticalShape, out var rotation, out _), "horizontalShape.FitsWithin(horizontalShape, out rotation, out _)");
        Assert.True(rotation == ItemRotation.Clockwise || rotation == ItemRotation.CounterClockwise, "rotation == ItemRotation.Clockwise || rotation == ItemRotation.CounterClockwise");
    }
}
