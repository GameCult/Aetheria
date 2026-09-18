/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GameCult.Caching;
using MessagePack;
using Newtonsoft.Json;
using CultMath;
using static CultMath.math;
using float2 = CultMath.float2;
using int2 = CultMath.int2;

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

[JsonObject(MemberSerialization.OptIn)]
public abstract class ItemData
{
    [Inspectable, CultName, JsonProperty("name"), Key(1)]
    public string Name;
    
    [Inspectable, CultInspectorTextArea, JsonProperty("description"), Key(2)]
    public string Description;

    // MessagePack key 3 belonged to a removed creator/faction reference field; do not reuse it. That provenance
    // now lives on FactionProductData, and branding is derived, never stored on the design.

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
}

[CultDocument("aetheria.simplecommoditydata", "1"), Inspectable, MessagePackObject]
public class SimpleCommodityData : ItemData
{
    // MessagePack keys 6, 7, 8 and 11 belonged to removed resource-distribution fields; do not reuse them.

    [Inspectable, JsonProperty("maxStackSize"), Key(9)]
    public int MaxStack = 10;

    [Inspectable, JsonProperty("category"), Key(10)]  
    public SimpleCommodityCategory Category;
}

[JsonObject(MemberSerialization.OptIn)]
public abstract class CraftedItemData : ItemData
{
    // The named slots this design is assembled from. A stat may read the quality of the part filling one.
    [Inspectable, JsonProperty("roles"), Key(9)]  
    public List<ItemRole> Roles = new List<ItemRole>();
}

// One slot in a design: every laser has a focusing array. A role is a name and nothing else. What fills it is
// a quality, authored per role by each manufacturer's product; the part itself earns a record when crafting
// needs one to exist.
[MessagePackObject, JsonObject(MemberSerialization.OptIn)]
public class ItemRole
{
    [Inspectable, JsonProperty("name"), Key(0)]
    public string Name;
}

[CultDocument("aetheria.compoundcommoditydata", "1"), Inspectable, MessagePackObject]
public class CompoundCommodityData : CraftedItemData
{
    [Inspectable, CultReference(typeof(PersonalityAttribute), many: true), JsonProperty("demandProfile"), Key(10)]
    public Dictionary<CultRecordRef<PersonalityAttribute>, float> DemandProfile = new Dictionary<CultRecordRef<PersonalityAttribute>, float>();

    [Inspectable, JsonProperty("category"), Key(11)] 
    public CompoundCommodityCategory Category;
}

[CultDocument("aetheria.consumableitemdata", "1"), Inspectable, MessagePackObject]
public class ConsumableItemData : CraftedItemData
{
    [Inspectable, JsonProperty("behaviors"), Key(10)]
    public List<BehaviorData> Behaviors = new List<BehaviorData>();

    [Inspectable, JsonProperty("stackable"), Key(11)]
    public bool Stackable;

    [Inspectable, JsonProperty("duration"), Key(12)]
    public float Duration;

    [Inspectable, CultInspectorAssetGuid, JsonProperty("icon"), Key(13)]
    public string Icon;

    [Inspectable, JsonProperty("effectiveness"), Key(14)]
    public BezierCurve Effectiveness;
}

[JsonObject(MemberSerialization.OptIn)]
public abstract class EquippableItemData : CraftedItemData
{
    [Inspectable, CultInspectorAssetGuid, JsonProperty("schematic"), Key(10)]
    public string Schematic;
    
    [Inspectable, JsonProperty("behaviors"), Key(11)]  
    public List<BehaviorData> Behaviors = new List<BehaviorData>();

    [Inspectable, JsonProperty("durability"), Key(12)]
    public float Durability;
    
    // R-heat requires every equippable to carry a coherent heat response (StatValidation.ValidateHeatResponse
    // refuses a zero-span range), so these four fields default to a legal, non-degenerate placeholder rather
    // than to a zero-span trap -- the same shape HeatResponseTests' own fixture authors by hand. A design that
    // cares about its thermal behaviour overrides all four; one that does not (most test fixtures, most gear
    // that predates R-heat and has not been reviewed) still opens and evaluates sanely instead of refusing to
    // load or resolving to a permanently-dead Performance().
    [InspectableTemperature, JsonProperty("minTemp"), Key(13)]
    public float MinimumTemperature = 0;

    [InspectableTemperature, JsonProperty("maxTemp"), Key(14)]
    public float MaximumTemperature = 100;

    // [InspectableField, JsonProperty("durabilityExponent"), Key(15), SimplePerformanceStat]
    // public PerformanceStat DurabilityExponent = new PerformanceStat();
    //
    // [InspectableField, JsonProperty("heatExponent"), Key(16), SimplePerformanceStat]
    // public PerformanceStat HeatExponent = new PerformanceStat();
    
    // MessagePack key 17 belonged to HeatPerformanceCurve, a BezierCurve; R-heat (docs/stats-and-power-cut.md)
    // replaced the authored shape with minimum, maximum, optimum and plateau width. Do not reuse.

    [Inspectable, JsonProperty("resilience"), Key(18)]
    public float ThermalResilience = 1;
    
    // [Inspectable, JsonProperty("sfx"), Key(19)]
    // public string SoundEffectTrigger;
    
    [Inspectable, CultInspectorAssetGuid, JsonProperty("actionIcon"), Key(20)]
    public string ActionBarIcon;

    [Inspectable, JsonProperty("soundBank"), Key(21)]
    public uint SoundBank;

    [Inspectable, JsonProperty("audioStats"), Key(22)]
    public List<AudioStat> AudioStats = new List<AudioStat>();
    
    [IgnoreMember]
    public abstract HardpointType HardpointType { get; }

    // R-heat (docs/stats-and-power-cut.md): the authored shape is minimum, maximum (both above), optimum and
    // plateau width. Where the part likes to be, and how forgiving it is. Authored, not derived -- the 100-sample
    // bezier scan that used to infer this from HeatPerformanceCurve is gone, and so is its cache.
    [InspectableTemperature, JsonProperty("optimalTemperature"), Key(30)]
    public float OptimalTemperature = 50;

    // The band, centered on OptimalTemperature, across which performance is 1. Plateau width is the
    // operational-lifespan lever: inside it, Wear's thermal term is zero (Performance() returns exactly 1), so a
    // well-managed item takes wear only from deltaTemp (Entity.cs UpdatePerformance).
    [Inspectable, JsonProperty("plateauWidth"), Key(31)]
    public float PlateauWidth = 20;

    // Performance is 1 across the plateau and falls linearly to 0 at each bound -- asymmetric whenever the
    // optimum is off-centre, which is how most gear behaves and how negent gear inverts. The plateau clamps to
    // the bounds rather than poking past them, defensively, though StatValidation already refuses an authored
    // overshoot at load -- this is redundant, not load-bearing.
    public float Performance(float temperature)
    {
        if (temperature <= MinimumTemperature || temperature >= MaximumTemperature) return 0f;
        var halfPlateau = PlateauWidth * 0.5f;
        var plateauLow = max(MinimumTemperature, OptimalTemperature - halfPlateau);
        var plateauHigh = min(MaximumTemperature, OptimalTemperature + halfPlateau);
        if (temperature >= plateauLow && temperature <= plateauHigh) return 1f;
        if (temperature < plateauLow)
            return saturate(unlerp(MinimumTemperature, plateauLow, temperature));
        return saturate(unlerp(MaximumTemperature, plateauHigh, temperature));
    }
}

[Inspectable, MessagePackObject, JsonObject(MemberSerialization.OptIn)]
public class AudioStat
{
    [Inspectable, JsonProperty("parameter"), Key(0)]
    public uint Parameter;

    [Inspectable, JsonProperty("stat"), Key(1)]
    public PerformanceStat Stat = new PerformanceStat();
}

[CultDocument("aetheria.geardata", "1"), Inspectable, MessagePackObject, JsonObject(MemberSerialization.OptIn)]
public class GearData : EquippableItemData
{
    [Inspectable, JsonProperty("hardpointType"), Key(23)]
    public HardpointType Hardpoint;

    [IgnoreMember] public override HardpointType HardpointType => Hardpoint;
}

[CultDocument("aetheria.cargobaydata", "1"), Inspectable, MessagePackObject, JsonObject(MemberSerialization.OptIn)]
public class CargoBayData : EquippableItemData
{
    [Inspectable, JsonProperty("interiorShape"), Key(24)]
    public Shape InteriorShape;
    
    [IgnoreMember] public override HardpointType HardpointType => HardpointType.Tool;
}

[CultDocument("aetheria.dockingbaydata", "1"), Inspectable, MessagePackObject, JsonObject(MemberSerialization.OptIn)]
public class DockingBayData : CargoBayData
{
    [Inspectable, JsonProperty("maxSize"), Key(25)]
    public int2 MaxSize;
}

[CultDocument("aetheria.weaponitemdata", "1"), MessagePackObject, JsonObject(MemberSerialization.OptIn)]
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

[CultDocument("aetheria.hulldata", "1"), Inspectable, MessagePackObject, JsonObject(MemberSerialization.OptIn)]
public class HullData : EquippableItemData
{
    [Inspectable, JsonProperty("hardpoints"), Key(23)]  
    public List<HardpointData> Hardpoints = new List<HardpointData>();

    [Inspectable, CultInspectorAssetGuid, JsonProperty("prefab"), Key(24)]  
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
public class HardpointData
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

// What a PerformanceStat reads to resolve one number: the lot it prices quality against, and the per-context
// factors that used to be three separate Evaluate bodies. EquippedItem, ConsumableItemEffect and the
// unequipped case (ItemManager's own private context) are the three implementations; each supplies only the
// sources it has, and says so through what it returns rather than through a second, quietly different formula.
// Cut 1 (docs/stats-and-power-cut.md): each factor now takes the declaring term's own exponent rather than
// reading a fixed field off the stat, because the stat no longer carries one fixed field per source.
public interface IStatContext
{
    Lot Lot { get; }
    float HeatFactor(float exponent);
    float DurabilityFactor(float exponent);
    // Progress through a consumable effect's duration, standing in for "condition" on a consumable the way heat
    // does for equipped gear. Only ConsumableItemEffect has one; every other context is the identity (1).
    float ConsumableProgressFactor(float exponent);
    // Cut 6 (the power bus) wires this to a real brownout curve. Until then no catalog stat declares a
    // PowerSupply term, and every context answers the identity so the enum member can exist now without a
    // resolver to back it.
    float PowerSupplyFactor(float exponent);
    float ScaleModifier(PerformanceStat stat);
    float ConstantModifier(PerformanceStat stat);
}

// The declared sources a stat term can read. Closed: adding a member is a schema change with a census
// (docs/stats-and-power-cut.md §2).
public enum StatSource
{
    Heat,
    Durability,
    Quality,
    PowerSupply,
    ConsumableProgress
}

// One declared dependency of a stat: a source and the exponent it is raised to inside the stat's Min/Max
// interpolation. Role is meaningful only for Quality; PerformanceStat.Evaluate refuses it elsewhere.
[MessagePackObject, JsonObject(MemberSerialization.OptIn)]
public class StatTerm
{
    [JsonProperty("source"), Key(0)] public StatSource Source;
    [JsonProperty("exponent"), Key(1)] public float Exponent;
    // The design role whose part quality this term reads; unset reads the item's own workmanship quality.
    // Njordr states the same rule as a derivation over dimensions; this is that rule with one dimension.
    [Inspectable, JsonProperty("role"), Key(2)] public string Role;
}

[MessagePackObject, JsonObject(MemberSerialization.OptIn)]
public class PerformanceStat
{
    [JsonProperty("min"), Key(0)]  public float Min;

    [JsonProperty("max"), Key(1)]  public float Max;

    // MessagePack keys 2, 3, 4 and 5 belonged to fixed exponent fields (HeatExponentMultiplier,
    // DurabilityExponentMultiplier, QualityExponent, FromRole) now expressed as declared Terms below; do not
    // reuse them.

    [Inspectable, JsonProperty("terms"), Key(6)]
    public List<StatTerm> Terms = new List<StatTerm>();

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

    // The one evaluation path. A stat with no terms resolves to Max (the identity factor, 1, times Min/Max
    // interpolation lands on the top) -- that is the same number a stat with all-zero exponents produced before
    // this cut, because pow(x, 0) == 1 regardless of x. Callers keep their own NaN handling: an unequipped read
    // throws with diagnostic detail, an equipped or consumable read falls back to Min. That disagreement is not
    // named as a defect, so it is not touched here.
    public float Evaluate(IStatContext context)
    {
        var factor = 1f;
        foreach (var term in Terms)
        {
            factor *= term.Source switch
            {
                StatSource.Quality => pow(context.Lot.QualityForRole(term.Role), term.Exponent),
                StatSource.Heat => context.HeatFactor(term.Exponent),
                StatSource.Durability => context.DurabilityFactor(term.Exponent),
                StatSource.ConsumableProgress => context.ConsumableProgressFactor(term.Exponent),
                StatSource.PowerSupply => context.PowerSupplyFactor(term.Exponent),
                _ => throw new ArgumentOutOfRangeException(nameof(term.Source), term.Source, $"Unknown StatSource on {Min}-{Max} stat")
            };
        }
        return lerp(Min, Max, factor) * context.ScaleModifier(this) + context.ConstantModifier(this);
    }
}

// Cut 1's loud refusal: the heat-response shape (min, max, optimum, plateau width) must describe a coherent
// range. Runs at catalog load (AetheriaStores.Open) and at every catalog write that goes through
// CultRecordRefs.Upsert (AetheriaStores.cs) -- every tool, test and migration script in this repository writes
// through that one path, so an authoring error is refused before it reaches disk, not just the next time
// someone reopens the file. The one hole this does not close: CultCache Studio's generic document editor
// writes straight through CultCache, bypassing Upsert, and Studio exposes no per-document validation hook to
// attach to yet. That gap is named here rather than silently assumed closed.
public static class StatValidation
{
    public static void ValidateHeatResponse(EquippableItemData data)
    {
        if (float.IsNaN(data.MinimumTemperature) || float.IsNaN(data.MaximumTemperature) ||
            float.IsNaN(data.OptimalTemperature) || float.IsNaN(data.PlateauWidth))
            throw new InvalidOperationException(
                $"{data.Name}: heat response carries NaN (min {data.MinimumTemperature}, max {data.MaximumTemperature}, " +
                $"optimum {data.OptimalTemperature}, plateau {data.PlateauWidth})");
        if (data.MinimumTemperature == data.MaximumTemperature)
            throw new InvalidOperationException(
                $"{data.Name}: MinimumTemperature and MaximumTemperature are both {data.MinimumTemperature} -- a " +
                "zero-span range is dead at every temperature, which is an authoring error, not an authored immunity");
        if (data.OptimalTemperature < data.MinimumTemperature || data.OptimalTemperature > data.MaximumTemperature)
            throw new InvalidOperationException(
                $"{data.Name}: OptimalTemperature {data.OptimalTemperature} lies outside its bounds " +
                $"[{data.MinimumTemperature}, {data.MaximumTemperature}]");
        if (data.PlateauWidth < 0)
            throw new InvalidOperationException($"{data.Name}: PlateauWidth {data.PlateauWidth} is negative");
        var halfPlateau = data.PlateauWidth * 0.5f;
        var plateauLow = data.OptimalTemperature - halfPlateau;
        var plateauHigh = data.OptimalTemperature + halfPlateau;
        if (plateauLow < data.MinimumTemperature || plateauHigh > data.MaximumTemperature)
            throw new InvalidOperationException(
                $"{data.Name}: plateau [{plateauLow}, {plateauHigh}] pokes past its bounds " +
                $"[{data.MinimumTemperature}, {data.MaximumTemperature}] -- the plateau must clamp to the bounds, " +
                "not exceed them");
    }
}

[CultDocument("aetheria.personalityattribute", "1"), Inspectable, MessagePackObject, JsonObject(MemberSerialization.OptIn)]
public class PersonalityAttribute
{
    [Inspectable, CultName, JsonProperty("name"), Key(1)]
    public string Name;
    
    [Inspectable, JsonProperty("low"), Key(2)]
    public string LowName;
    
    [Inspectable, JsonProperty("high"), Key(3)]
    public string HighName;
    
}