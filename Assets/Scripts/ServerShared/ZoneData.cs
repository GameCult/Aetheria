/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections;
using System.Collections.Generic;
using MessagePack;
using UniRx;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using float2 = Unity.Mathematics.float2;
using float3 = Unity.Mathematics.float3;

public class ZonePack
{
    public List<BodyData> Planets = new List<BodyData>();
    public List<OrbitData> Orbits = new List<OrbitData>();
    public List<EntityPack> Entities = new List<EntityPack>();
    public float Radius = 2000;
    public float Mass = 10000;
    public double Time;
}

// [LegacyCatalogGroup("Galaxy"), MessagePackObject]
// public class StationData : DatabaseEntry, INamedEntry
// {
//     [Key(1)]
//     public string Name;
//
//     // Can be Planet or Orbit
//     [Key(2)]
//     public Guid Parent;
//
//     [Key(3)]
//     public Guid Owner;
//
//     [Key(4)]
//     public List<ItemInstance> Inventory = new List<ItemInstance>();
//
//     [Key(5)]
//     public Dictionary<Guid, float> BuyPrices = new Dictionary<Guid, float>();
//
//     [Key(6)]
//     public Dictionary<Guid, float> SellPrices = new Dictionary<Guid, float>();
//
//     [Key(7)]
//     public Guid Zone;
//
//     [IgnoreMember] public string EntryName
//     {
//         get => Name;
//         set => Name = value;
//     }
// }

[MessagePackObject,
 Union(0, typeof(PlanetData)),
 Union(1, typeof(AsteroidBeltData)),
 Union(2, typeof(GasGiantData)),
 Union(3, typeof(SunData))]
public abstract class BodyData : DatabaseEntry, INamedEntry
{
    [Key(1)]
    public string Name = "";

    [Key(2)]
    public Guid Orbit;

    [Key(3)]
    public float Mass = 0;

    [Key(4)]
    public Dictionary<Guid, float> Resources = new Dictionary<Guid, float>();

    [Key(5)]
    public float BodyRadiusMultiplier = 1;

    [Key(6)]
    public float GravityRadiusMultiplier = 1;

    [Key(7)]
    public float GravityDepthMultiplier = 1;

    [Key(8)]
    public float GravityDepthExponent = 16;

    [IgnoreMember] public string EntryName
    {
        get => Name;
        set => Name = value;
    }
}

[MessagePackObject]
public class PlanetData : BodyData
{

}

[MessagePackObject]
public class AsteroidBeltData : BodyData
{
    [Key(9)]
    public Asteroid[] Asteroids;
}

[MessagePackObject]
public class GasGiantData : BodyData
{
    [Key(9)]
    public float FirstOffsetDomainRotationSpeed = 1;

    [Key(10)]
    public float FirstOffsetRotationSpeed = 1;

    [Key(11)]
    public float SecondOffsetDomainRotationSpeed = 1;

    [Key(12)]
    public float SecondOffsetRotationSpeed = 1;

    [Key(13)]
    public float AlbedoRotationSpeed = 1;

    [Key(14)]
    public float WaveRadiusMultiplier = 1;

    [Key(15)]
    public float WaveDepthMultiplier = 1;

    [Key(16)]
    public float WaveDepthExponent = 8;

    [Key(17)]
    public float WaveSpeedMultiplier = 8;

    [Key(18)]
    public List<string> MaterialOverrides = new List<string>();

    [Key(19)]
    public float4[] Colors = new float4[0];
}

public class SunData : GasGiantData
{
    [Key(20)]
    public float3 LightColor = float3.zero;

    [Key(21)]
    public float3 FogTintColor = float3.zero;

    [Key(22)]
    public float LightRadiusMultiplier = 1;
}

[MessagePackObject]
public class Asteroid
{
    [Key(0)]
    public float Distance;

    [Key(1)]
    public float Phase;

    [Key(2)]
    public float Size;

    [Key(3)]
    public float RotationSpeed;
}

[MessagePackObject]
public class OrbitData : DatabaseEntry
{
    [Key(1)]
    public Guid Parent;

    [Key(2)]
    public float Distance;

    [Key(3)]
    public float Phase;

    [Key(4)]
    public float2 FixedPosition = float2.zero;

    // [Key(4)]
    // public float Period;

    public static float2 Evaluate(float phase)
    {
        phase *= PI * 2;
        return new float2(cos(phase), sin(phase));
    }
}
