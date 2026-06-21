/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.Linq;
using cfloat2 = CultMath.float2;
using cfloat3 = CultMath.float3;
using int2 = Unity.Mathematics.int2;
using unityfloat2 = Unity.Mathematics.float2;

public class Shape
{
    public bool[,] Cells;

    private bool _dirty = true;

    public Shape()
    {
        Cells = new bool[1, 1];
        Cells[0, 0] = true;
    }

    public Shape(int width, int height)
    {
        Cells = new bool[width, height];
    }

    public int Width
    {
        get { return Cells.GetLength(0); }
        set { Resize(value, Height); }
    }

    public int Height
    {
        get { return Cells.GetLength(1); }
        set { Resize(Width, value); }
    }

    public void Resize(int width, int height)
    {
        var newCells = new bool[width, height];

        for (var x = 0; x < width; x++)
        {
            for (var y = 0; y < height; y++)
            {
                newCells[x, y] = x >= Width || y >= Height || Cells[x, y];
            }
        }

        Cells = newCells;
    }

    public int2 Rotate(int2 position, ItemRotation rotation)
    {
        return rotation switch
        {
            ItemRotation.Clockwise => new int2(position.y, Width - 1 - position.x),
            ItemRotation.Reversed => new int2(Width - 1 - position.x, Height - 1 - position.y),
            ItemRotation.CounterClockwise => new int2(Height - 1 - position.y, position.x),
            _ => new int2(position.x, position.y)
        };
    }

    private int2[] _cachedShapeCoordinates;

    public int2[] Coordinates
    {
        get
        {
            if (_dirty)
            {
                _cachedShapeCoordinates = EnumerateShapeCoordinates().ToArray();
                _dirty = false;
            }

            return _cachedShapeCoordinates;
        }
    }

    private IEnumerable<int2> EnumerateShapeCoordinates()
    {
        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                if (Cells[x, y])
                    yield return new int2(x, y);
            }
        }
    }

    private int2[] _cachedAllShapeCoordinates;

    public int2[] AllCoordinates => _cachedAllShapeCoordinates ??= EnumerateAllShapeCoordinates().ToArray();

    private IEnumerable<int2> EnumerateAllShapeCoordinates()
    {
        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                yield return new int2(x, y);
            }
        }
    }

    private cfloat2? _centerOfMass;

    public cfloat2 CenterOfMass => _centerOfMass ??= Coordinates
        .Aggregate(new cfloat2(0, 0), (total, coord) => total + new cfloat2(coord.x, coord.y)) / Coordinates.Length;

    public bool GetCell(int x, int y)
    {
        return x >= 0 && y >= 0 && x < Width && y < Height && Cells[x, y];
    }

    public void SetCell(int x, int y, bool value)
    {
        if (x < 0 || y < 0 || x >= Width || y >= Height)
            return;

        _dirty = true;
        _centerOfMass = null;
        Cells[x, y] = value;
    }

    public bool this[int2 pos]
    {
        get { return GetCell(pos.x, pos.y); }
        set { SetCell(pos.x, pos.y, value); }
    }

    public Shape Shrink()
    {
        var shape = new Shape(Width, Height);
        foreach (var shapeCoord in Coordinates)
        {
            shape[shapeCoord] = (
                this[shapeCoord + new int2(-1, -1)] && this[shapeCoord + new int2(0, -1)] && this[shapeCoord + new int2(1, -1)] &&
                this[shapeCoord + new int2(-1, 0)] && this[shapeCoord + new int2(1, 0)] &&
                this[shapeCoord + new int2(-1, 1)] && this[shapeCoord + new int2(0, 1)] && this[shapeCoord + new int2(1, 1)]
            );
        }

        return shape;
    }

    public Shape Inset(Shape inset, int2 insetPosition, ItemRotation rotation = ItemRotation.None)
    {
        var shape = new Shape(Math.Max(Width, insetPosition.x + inset.Width - 1), Math.Max(Height, insetPosition.y + inset.Height - 1));
        foreach (var v in inset.Coordinates)
        {
            var insetCoord = inset.Rotate(v, rotation) + insetPosition;
            shape[insetCoord] = true;
        }

        return shape;
    }

    public Shape Expand()
    {
        var shape = new Shape(Width, Height);
        foreach (var shapeCoord in AllCoordinates)
        {
            shape[shapeCoord] = (
                this[shapeCoord + new int2(-1, -1)] || this[shapeCoord + new int2(0, -1)] || this[shapeCoord + new int2(1, -1)] ||
                this[shapeCoord + new int2(-1, 0)] || this[shapeCoord] || this[shapeCoord + new int2(1, 0)] ||
                this[shapeCoord + new int2(-1, 1)] || this[shapeCoord + new int2(0, 1)] || this[shapeCoord + new int2(1, 1)]
            );
        }

        return shape;
    }

    public bool FitsWithin(Shape other, out ItemRotation rotation, out int2 position)
    {
        foreach (var rot in (ItemRotation[])Enum.GetValues(typeof(ItemRotation)))
        {
            rotation = rot;
            if (FitsWithin(other, rot, out var pos))
            {
                position = pos;
                return true;
            }
        }

        rotation = ItemRotation.None;
        position = int2.zero;
        return false;
    }

    public bool FitsWithin(Shape other, ItemRotation rotation, out int2 position)
    {
        var width = rotation == ItemRotation.Clockwise || rotation == ItemRotation.CounterClockwise ? Height : Width;
        var height = rotation == ItemRotation.Clockwise || rotation == ItemRotation.CounterClockwise ? Width : Height;

        for (var x = 0; x < other.Width - width + 1; x++)
        {
            for (var y = 0; y < other.Height - height + 1; y++)
            {
                position = new int2(x, y);
                var fits = true;
                foreach (var v in Coordinates)
                {
                    fits = fits && other[Rotate(v, rotation) + position];
                    if (!fits)
                        break;
                }

                if (fits)
                    return true;
            }
        }

        position = int2.zero;
        return false;
    }

    public void SetLine(unityfloat2 a, unityfloat2 b) =>
        SetLine(AetheriaMath.ToCult(a), AetheriaMath.ToCult(b));

    public void SetLine(cfloat2 a, cfloat2 b)
    {
        if (a.Equals(b))
            return;

        var steep = Math.Abs(b.y - a.y) > Math.Abs(b.x - a.x);
        if (steep)
        {
            a = new cfloat2(a.y, a.x);
            b = new cfloat2(b.y, b.x);
        }

        if (a.x > b.x)
        {
            var temp = a;
            a = b;
            b = temp;
        }

        var dx = b.x - a.x;
        var dy = b.y - a.y;
        var derr = Math.Abs(dy / dx);

        var y = (int)Math.Round(a.y);
        var xStart = (int)Math.Round(a.x);
        var error = (a.y - y) + (xStart - a.x) * derr;

        for (var x = xStart; x <= (int)Math.Round(b.x); x++)
        {
            if (x >= 0 && x < Width && y >= 0 && y < Height)
            {
                Cells[steep ? y : x, steep ? x : y] = true;
            }

            error += derr;
            if (error >= 0.5f)
            {
                y += Math.Sign(dy);
                error -= 1f;
            }
        }
    }
}

public class HardpointData : ITintInspector
{
    public HardpointType Type;
    public int2 Position;
    public Shape Shape = new Shape();
    public string Transform;
    public ItemRotation Rotation;
    public float Armor;

    public override string ToString()
    {
        return $"{Enum.GetName(typeof(HardpointType), Type)} Hardpoint {Rotation.Arrow()}";
    }

    public cfloat3 TintColor
    {
        get { return GetColor(Type); }
    }

    public static cfloat3 GetColor(HardpointType type)
    {
        if (_tintColors == null)
        {
            var hardpointTypes = (HardpointType[])Enum.GetValues(typeof(HardpointType));
            _tintColors = hardpointTypes.ToDictionary(
                x => x,
                x => ColorMath.HsvToRgb(new cfloat3(Fraction((float)(int)x / hardpointTypes.Length + .25f), 1, 1)));
        }

        return _tintColors.TryGetValue(type, out var color) ? color : _tintColors[HardpointType.Hull];
    }

    private static float Fraction(float value)
    {
        return value - MathF.Floor(value);
    }

    private static Dictionary<HardpointType, cfloat3> _tintColors;
}
