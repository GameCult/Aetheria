/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using JsonKnownTypes;
using MessagePack;
using Newtonsoft.Json;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using float2 = Unity.Mathematics.float2;
using int2 = Unity.Mathematics.int2;

[Inspectable, MessagePackObject, JsonObject(MemberSerialization.OptIn)]
public class Shape
{
    [JsonProperty("name"), Key(0)] public bool[,] Cells;
    
    private bool _dirty = true;

    public Shape()
    {
        Cells = new bool[1,1];
        Cells[0, 0] = true;
    }

    public Shape(int width, int height)
    {
        Cells = new bool[width, height];
    }
    
    [IgnoreMember]
    public int Width
    {
        get { return Cells.GetLength(0); }
        set { Resize(value, Height); }
    }

    [IgnoreMember]
    public int Height
    {
        get { return Cells.GetLength(1); }
        set { Resize(Width, value); }
    }

    public void Resize(int width, int height)
    {
        var newCells = new bool[width, height];
        
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                newCells[x, y] = x >= Width || y >= Height || Cells[x, y];
            }
        }

        Cells = newCells;
    }

    private static int mod(int x, int m) {
        int r = x%m;
        return r<0 ? r+m : r;
    }
    
    public int2 Rotate(int2 position, ItemRotation rotation)
    {
        return rotation switch
        {
            ItemRotation.Clockwise => int2(position.y, Width - 1 - position.x),
            ItemRotation.Reversed => int2(Width - 1 - position.x, Height - 1 - position.y),
            ItemRotation.CounterClockwise => int2(Height - 1 - position.y, position.x),
            _ => int2(position.x, position.y)
        };
    }

    private int2[] _cachedShapeCoordinates;
    
    [IgnoreMember]
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
        for (int y = 0; y < Height; y++)
        {
            for(int x = 0; x < Width; x++)
            {
                if(Cells[x,y]) yield return int2(x, y);
            }
        }
    }

    private int2[] _cachedAllShapeCoordinates;
    
    [IgnoreMember]
    public int2[] AllCoordinates => _cachedAllShapeCoordinates ?? (_cachedAllShapeCoordinates = EnumerateAllShapeCoordinates().ToArray());

    private IEnumerable<int2> EnumerateAllShapeCoordinates()
    {
        for (int y = 0; y < Height; y++)
        {
            for(int x = 0; x < Width; x++)
            {
                yield return int2(x, y);
            }
        }
    }

    private float2? _centerOfMass;

    [IgnoreMember]
    public float2 CenterOfMass => _centerOfMass ?? (_centerOfMass = Coordinates
        .Aggregate(float2.zero, (total, coord) => total + coord) / Coordinates.Length).Value;

    public bool this[int2 pos] {
        get { return pos.x >= 0 && pos.y >= 0 && pos.x < Width && pos.y < Height && Cells[pos.x, pos.y]; }
        set
        {
            if (pos.x < 0 || pos.y < 0 || pos.x >= Width || pos.y >= Height) return;
            _dirty = true;
            Cells[pos.x, pos.y] = value;
        }
    }

    public Shape Shrink()
    {
        var shape = new Shape(Width, Height);
        foreach(var shapeCoord in Coordinates)
            shape[shapeCoord] = (
                this[shapeCoord + int2(-1,-1)] && this[shapeCoord + int2(0,-1)] && this[shapeCoord + int2(1,-1)] &&
                this[shapeCoord + int2(-1,0)] && this[shapeCoord + int2(1,0)] && 
                this[shapeCoord + int2(-1,1)] && this[shapeCoord + int2(0,1)] && this[shapeCoord + int2(1,1)]
            );
        return shape;
    }

    public Shape Inset(Shape inset, int2 insetPosition, ItemRotation rotation = ItemRotation.None)
    {
        var shape = new Shape(max(Width,insetPosition.x + inset.Width - 1), max(Height, insetPosition.y + inset.Height - 1));
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
            shape[shapeCoord] = (
                this[shapeCoord + int2(-1,-1)] || this[shapeCoord + int2(0,-1)] || this[shapeCoord + int2(1,-1)] ||
                this[shapeCoord + int2(-1,0)] || this[shapeCoord] || this[shapeCoord + int2(1,0)] || 
                this[shapeCoord + int2(-1,1)] || this[shapeCoord + int2(0,1)] || this[shapeCoord + int2(1,1)]
            );
        return shape;
    }

    // TODO: Use the power of math to optimize this function!
    // Try to find a rotation and position with which a shape can be placed to fit within another shape
    public bool FitsWithin(Shape other, out ItemRotation rotation, out int2 position)
    {
        // Try every item orientation
        foreach(var rot in (ItemRotation[])Enum.GetValues(typeof(ItemRotation)))
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
    
    // Try to find a position with which a shape can be placed to fit within another shape
    public bool FitsWithin(Shape other, ItemRotation rotation, out int2 position)
    {
        var width = rotation == ItemRotation.Clockwise || rotation == ItemRotation.CounterClockwise ? Height : Width;
        var height = rotation == ItemRotation.Clockwise || rotation == ItemRotation.CounterClockwise ? Width : Height;
        // Try every item position that could possibly fit
        for(int x = 0; x < other.Width - width + 1; x++)
        {
            for (int y = 0; y < other.Height - height + 1; y++)
            {
                position = int2(x, y);
                var fits = true;
                foreach (var v in Coordinates)
                {
                    fits = fits && other[Rotate(v, rotation) + position];
                    if (!fits) break;
                }

                if (fits) return true;
            }
        }

        position = int2.zero;
        return false;
    }

    // Set every cell on the line from a to b to true according to Bresenham's Line Algorithm
    public void SetLine(float2 a, float2 b)
    {
        if  (a.Equals(b))            
        {            
            return;
        }
        
        // If line gradient is steep, swap x and y
        bool steep = Math.Abs(b.y - a.y) > Math.Abs(b.x - a.x);
        if (steep)
        {
            a = new float2(a.y, a.x);
            b = new float2(b.y, b.x);
        }

        // If b is closer to the origin than a, swap a and b.
        if (a.x > b.x)
        {
            var temp = a;
            a = b;
            b = temp;
        }
        
        float dx = b.x - a.x;
        float dy = b.y - a.y;
        float derr = Math.Abs(dy / dx); // dx != 0
        
        int y = (int) Math.Round(a.y);
        int xStart = (int) Math.Round(a.x);
        float error = (a.y - y) + (xStart - a.x) * derr;

        for (int x = xStart; x <= (int) Math.Round(b.x); x++)
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

[MessagePackObject, JsonObject(MemberSerialization.OptIn), JsonConverter(typeof(JsonKnownTypesConverter<ItemData>))]
public abstract class ItemData : DatabaseEntry, INamedEntry
{
    [Inspectable, JsonProperty("name"), Key(1)]
    public string Name;
    
    [InspectableText, JsonProperty("description"), Key(2)]
    public string Description;
    
    [InspectableDatabaseLink(typeof(Faction)), JsonProperty("creator"), Key(3)]
    public Guid Manufacturer;

    [Inspectable, JsonProperty("mass"), Key(4)]
    public float Mass;

    [InspectableSchematicShape, JsonProperty("shape"), Key(5)]
    public Shape Shape;

    // Heat needed to change temperature of 1 gram by 1 degree
    [Inspectable, JsonProperty("specificHeat"), Key(6)]
    public float SpecificHeat = 1;
    
    [Inspectable, JsonProperty("conductivity"), Key(7)]
    public float Conductivity = 1;
    
    [Inspectable, JsonProperty("price"), Key(8)]
    public int Price = 0;
    
    [IgnoreMember] public string EntryName
    {
        get => Name;
        set => Name = value;
    }

}

[RethinkTable("Items"), Inspectable, MessagePackObject]
public class SimpleCommodityData : ItemData
{
    // // Types of body where this resource can be found
    // [InspectableField, JsonProperty("resourceBodyType"), Key(6)]  
    // public BodyType ResourceBodyType;
    //
    // // Link to map(s) controlling density of resource, multiplied together when more than one
    // [InspectableDatabaseLink(typeof(GalaxyMapLayerData)), JsonProperty("resourceDensity"), Key(7)]  
    // public List<Guid> ResourceDensity = new List<Guid>();
    //
    // // Controls the lowest value in the resource distribution curve
    // [InspectableField, JsonProperty("distribution"), Key(8)]
    // public ExponentialLerp Distribution;
    //
    // // Minimum amount of resources needed for presence to register
    // [InspectableField, JsonProperty("floor"), Key(11)]
    // public float Floor = 5f;

    [Inspectable, JsonProperty("maxStackSize"), Key(9)]
    public int MaxStack = 10;

    [Inspectable, JsonProperty("category"), Key(10)]  
    public SimpleCommodityCategory Category;
}

[MessagePackObject, JsonObject(MemberSerialization.OptIn), JsonConverter(typeof(JsonKnownTypesConverter<CraftedItemData>))]
public abstract class CraftedItemData : ItemData
{
    // [Inspectable, JsonProperty("ingredientQualityWeight"), Key(9)]  
    // public float IngredientQualityWeight = .5f;
}

[RethinkTable("Items"), Inspectable, MessagePackObject]
public class CompoundCommodityData : CraftedItemData
{
    [InspectableDatabaseLink(typeof(PersonalityAttribute)), JsonProperty("demandProfile"), Key(10)]
    public Dictionary<Guid, float> DemandProfile = new Dictionary<Guid, float>();

    [Inspectable, JsonProperty("category"), Key(11)] 
    public CompoundCommodityCategory Category;
}

[RethinkTable("Items"), Inspectable, MessagePackObject]
public class ConsumableItemData : CraftedItemData
{
    [Inspectable, JsonProperty("behaviors"), Key(10)]
    public List<BehaviorData> Behaviors = new List<BehaviorData>();

    [Inspectable, JsonProperty("stackable"), Key(11)]
    public bool Stackable;

    [Inspectable, JsonProperty("duration"), Key(12)]
    public float Duration;

    [InspectableTexture, JsonProperty("icon"), Key(13)]
    public string Icon;

    [Inspectable, JsonProperty("effectiveness"), Key(14)]
    public BezierCurve Effectiveness;
}

[JsonObject(MemberSerialization.OptIn)]
public abstract class EquippableItemData : CraftedItemData
{
    [InspectableTexture, JsonProperty("schematic"), Key(10)]
    public string Schematic;
    
    [Inspectable, JsonProperty("behaviors"), Key(11)]  
    public List<BehaviorData> Behaviors = new List<BehaviorData>();

    [Inspectable, JsonProperty("durability"), Key(12)]
    public float Durability;
    
    [InspectableTemperature, JsonProperty("minTemp"), Key(13)]
    public float MinimumTemperature;

    [InspectableTemperature, JsonProperty("maxTemp"), Key(14)]
    public float MaximumTemperature;

    // [InspectableField, JsonProperty("durabilityExponent"), Key(15), SimplePerformanceStat]
    // public PerformanceStat DurabilityExponent = new PerformanceStat();
    //
    // [InspectableField, JsonProperty("heatExponent"), Key(16), SimplePerformanceStat]
    // public PerformanceStat HeatExponent = new PerformanceStat();
    
    [InspectableAnimationCurve, JsonProperty("heatCurve"), Key(17)]
    public BezierCurve HeatPerformanceCurve;

    [Inspectable, JsonProperty("resilience"), Key(18)]
    public float ThermalResilience = 1;
    
    // [Inspectable, JsonProperty("sfx"), Key(19)]
    // public string SoundEffectTrigger;
    
    [InspectableTexture, JsonProperty("actionIcon"), Key(20)]
    public string ActionBarIcon;

    [InspectableSoundBank, JsonProperty("soundBank"), Key(21)]
    public uint SoundBank;

    [Inspectable, JsonProperty("audioStats"), Key(22)]
    public List<AudioStat> AudioStats = new List<AudioStat>();
    
    [IgnoreMember]
    public abstract HardpointType HardpointType { get; }
    
    [IgnoreMember] private const int STEPS = 64;

    [IgnoreMember] private float? _optimum;
    
    [IgnoreMember]
    public float OptimalTemperature
    {
        get
        {
            if (_optimum==null)
            {
                var samples = Enumerable.Range(0, STEPS).Select(i => (float) i / STEPS).ToArray();
                float max = 0;
                _optimum = 0;
                foreach (var f in samples)
                {
                    var p = HeatPerformanceCurve?.Evaluate(f)??0;
                    if (p > max)
                    {
                        max = p;
                        _optimum = f;
                    }
                }
            }

            return (float) (MinimumTemperature + _optimum * (MaximumTemperature - MinimumTemperature));
        }
    }

    public float Performance(float temperature)
    {
        return saturate(HeatPerformanceCurve?.Evaluate(unlerp(MinimumTemperature, MaximumTemperature, temperature))??1);
    }
}

[Inspectable, MessagePackObject, JsonObject(MemberSerialization.OptIn)]
public class AudioStat
{
    [InspectableAudioParameter, JsonProperty("parameter"), Key(0)]
    public uint Parameter;

    [Inspectable, JsonProperty("stat"), Key(1)]
    public PerformanceStat Stat = new PerformanceStat();
}

[RethinkTable("Items"), Inspectable, MessagePackObject, JsonObject(MemberSerialization.OptIn)]
public class GearData : EquippableItemData
{
    [Inspectable, JsonProperty("hardpointType"), Key(23)]
    public HardpointType Hardpoint;

    [IgnoreMember] public override HardpointType HardpointType => Hardpoint;
}

[RethinkTable("Items"), Inspectable, MessagePackObject, JsonObject(MemberSerialization.OptIn)]
public class CargoBayData : EquippableItemData
{
    [Inspectable, JsonProperty("interiorShape"), Key(24)]
    public Shape InteriorShape;
    
    [IgnoreMember] public override HardpointType HardpointType => HardpointType.Tool;
}

[RethinkTable("Items"), Inspectable, MessagePackObject, JsonObject(MemberSerialization.OptIn)]
public class DockingBayData : CargoBayData
{
    [Inspectable, JsonProperty("maxSize"), Key(25)]
    public int2 MaxSize;
}

[RethinkTable("Items"), MessagePackObject, JsonObject(MemberSerialization.OptIn)]
public class WeaponItemData : GearData
{
    [Inspectable, JsonProperty("range"), Key(24)]
    public WeaponRange WeaponRange;
    
    [Inspectable, JsonProperty("caliber"), Key(25)]
    public WeaponCaliber WeaponCaliber;
    
    [Inspectable, JsonProperty("weaponType"), Key(26)]
    public WeaponType WeaponType;
    
    [Inspectable, JsonProperty("fireTypes"), Key(27)]
    public WeaponFireType WeaponFireTypes;
    
    [Inspectable, JsonProperty("modifiers"), Key(28)]
    public WeaponModifiers WeaponModifiers;
}

[RethinkTable("Items"), Inspectable, MessagePackObject, JsonObject(MemberSerialization.OptIn)]
public class HullData : EquippableItemData
{
    [Inspectable, JsonProperty("hardpoints"), Key(23)]  
    public List<HardpointData> Hardpoints = new List<HardpointData>();

    [InspectablePrefab, JsonProperty("prefab"), Key(24)]  
    public string Prefab;

    [Inspectable, JsonProperty("hullType"), Key(25)]
    public HullType HullType;

    [Inspectable, JsonProperty("gridOffset"), Key(26)]
    public float GridOffset;

    [Inspectable, JsonProperty("armor"), Key(27)]
    public float Armor;

    [Inspectable, JsonProperty("drag"), Key(28)]
    public float Drag;
    
    [Inspectable, JsonProperty("canTow"), Key(29)]
    public bool CanTow;

    [IgnoreMember]
    public Shape InteriorCells
    {
        get
        {
            if (_interiorCells == null)
            {
                _interiorCells = Shape.Shrink();
            }

            return _interiorCells;
        }
    }

    private Shape _interiorCells;

    [IgnoreMember] public override HardpointType HardpointType => HardpointType.Hull;
}

[Inspectable, MessagePackObject, JsonObject(MemberSerialization.OptIn)]
public class HardpointData : ITintInspector
{
    [Inspectable, JsonProperty("type"), Key(0)] public HardpointType Type;
    [Inspectable, JsonProperty("position"), Key(1)] public int2 Position;
    [Inspectable, JsonProperty("shape"), Key(2)] public Shape Shape = new Shape();
    [Inspectable, JsonProperty("transform"), Key(3)] public string Transform;
    [Inspectable, JsonProperty("rotation"), Key(4)] public ItemRotation Rotation;
    [Inspectable, JsonProperty("armor"), Key(5)] public float Armor;

    public override string ToString()
    {
        return $"{Enum.GetName(typeof(HardpointType), Type)} Hardpoint {Rotation.Arrow()}";
    }

    [IgnoreMember]
    public float3 TintColor
    {
        get
        {
            return GetColor(Type);
        }
    }

    public static float3 GetColor(HardpointType type)
    {
        if (_tintColors == null)
        {
            var hardpointTypes = (HardpointType[]) Enum.GetValues(typeof(HardpointType));
            _tintColors = hardpointTypes.ToDictionary(x => x,
                x => ColorMath.HsvToRgb(float3(frac((float)(int)x/hardpointTypes.Length + .25f), 1, 1)));
        }

        return _tintColors.ContainsKey(type) ? _tintColors[type] : _tintColors[HardpointType.Hull];
    }
    
    private static Dictionary<HardpointType, float3> _tintColors;
}

[MessagePackObject, JsonObject(MemberSerialization.OptIn)]
public class PerformanceStat
{
    [JsonProperty("min"), Key(0)]  public float Min;

    [JsonProperty("max"), Key(1)]  public float Max;

    [JsonProperty("heatExponentMultiplier"), Key(2)] 
    public float HeatExponentMultiplier;

    [JsonProperty("durabilityExponentMultiplier"), Key(3)] 
    public float DurabilityExponentMultiplier;

    [JsonProperty("qualityExponent"), Key(4)] 
    public float QualityExponent;
    
    //[JsonProperty("id"), Key(5)]  public Guid ID = Guid.NewGuid();

    // [JsonProperty("ingredient"), Key(5)]  public Guid? Ingredient;
    
    [IgnoreMember] private Dictionary<Entity,Dictionary<Behavior,float>> _scaleModifiers;
    [IgnoreMember] private Dictionary<Entity,Dictionary<Behavior,float>> _constantModifiers;

    [IgnoreMember]
    private Dictionary<Entity, Dictionary<Behavior, float>> ScaleModifiers =>
        _scaleModifiers = _scaleModifiers ?? new Dictionary<Entity, Dictionary<Behavior, float>>();

    [IgnoreMember]
    private Dictionary<Entity, Dictionary<Behavior, float>> ConstantModifiers =>
        _constantModifiers = _constantModifiers ?? new Dictionary<Entity, Dictionary<Behavior, float>>();

    public Dictionary<Behavior, float> GetScaleModifiers(Entity entity)
    {
        if(!ScaleModifiers.ContainsKey(entity))
            ScaleModifiers[entity] = new Dictionary<Behavior, float>();

        return ScaleModifiers[entity];
    }

    public Dictionary<Behavior, float> GetConstantModifiers(Entity entity)
    {
        if(!ConstantModifiers.ContainsKey(entity))
            ConstantModifiers[entity] = new Dictionary<Behavior, float>();

        return ConstantModifiers[entity];
    }
}

[RethinkTable("Items"), Inspectable, MessagePackObject, JsonObject(MemberSerialization.OptIn)]
public class PersonalityAttribute : DatabaseEntry, INamedEntry
{
    [Inspectable, JsonProperty("name"), Key(1)]
    public string Name;
    
    [Inspectable, JsonProperty("low"), Key(2)]
    public string LowName;
    
    [Inspectable, JsonProperty("high"), Key(3)]
    public string HighName;
    
    [IgnoreMember] public string EntryName
    {
        get => Name;
        set => Name = value;
    }
}