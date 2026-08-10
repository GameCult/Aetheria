/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections;
using System.Collections.Generic;
using MessagePack;
using Newtonsoft.Json;
using UniRx;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using float2 = Unity.Mathematics.float2;
using float3 = Unity.Mathematics.float3;

[MessagePackObject, JsonObject(MemberSerialization.OptIn)]
public class ZonePack
{
    [JsonProperty("planets"), Key(0)]
    public List<BodyData> Planets = new List<BodyData>();
    
    [JsonProperty("orbits"), Key(1)]
    public List<OrbitData> Orbits = new List<OrbitData>();
    
    [JsonProperty("entities"), Key(2)]
    public List<EntityPack> Entities = new List<EntityPack>();
    
    [JsonProperty("radius"), Key(3)]
    public float Radius = 2000;

    [JsonProperty("mass"), Key(4)]
    public float Mass = 10000;

    [JsonProperty("time"), Key(5)]
    public double Time;
    
    
}

// [RethinkTable("Galaxy"), MessagePackObject, JsonObject(MemberSerialization.OptIn)]
// public class StationData : DatabaseEntry, INamedEntry
// {
//     [JsonProperty("name"), Key(1)]
//     public string Name;
//     
//     // Can be Planet or Orbit
//     [JsonProperty("parent"), Key(2)]
//     public Guid Parent;
//
//     [JsonProperty("owner"), Key(3)]
//     public Guid Owner;
//
//     [JsonProperty("inventory"), Key(4)]
//     public List<ItemInstance> Inventory = new List<ItemInstance>();
//
//     [JsonProperty("buying"), Key(5)]
//     public Dictionary<Guid, float> BuyPrices = new Dictionary<Guid, float>();
//
//     [JsonProperty("selling"), Key(6)]
//     public Dictionary<Guid, float> SellPrices = new Dictionary<Guid, float>();
//
//     [JsonProperty("zone"), Key(7)]
//     public Guid Zone;
//     
//     [IgnoreMember] public string EntryName
//     {
//         get => Name;
//         set => Name = value;
//     }
// }

[MessagePackObject, 
 JsonObject(MemberSerialization.OptIn),
 Union(0, typeof(PlanetData)),
 Union(1, typeof(AsteroidBeltData)),
 Union(2, typeof(GasGiantData)),
 Union(3, typeof(SunData))]
public abstract class BodyData : DatabaseEntry, INamedEntry
{
    [JsonProperty("name"), Key(1)]
    public string Name = "";

    [JsonProperty("orbit"), Key(2)]
    public Guid Orbit;

    [JsonProperty("mass"), Key(3)]
    public float Mass = 0;

    [JsonProperty("resources"), Key(4)]
    public Dictionary<Guid, float> Resources = new Dictionary<Guid, float>();

    [JsonProperty("bodyRadiusMul")] [Key(5)]
    public float BodyRadiusMultiplier = 1;

    [JsonProperty("gravRadiusMul")] [Key(6)]
    public float GravityRadiusMultiplier = 1;

    [JsonProperty("depthMul")] [Key(7)]
    public float GravityDepthMultiplier = 1;

    [JsonProperty("depthExp")] [Key(8)]
    public float GravityDepthExponent = 16;
    
    [IgnoreMember] public string EntryName
    {
        get => Name;
        set => Name = value;
    }
}

[MessagePackObject, JsonObject(MemberSerialization.OptIn)]
public class PlanetData : BodyData
{
    
}

[MessagePackObject, JsonObject(MemberSerialization.OptIn)]
public class AsteroidBeltData : BodyData
{
    [JsonProperty("asteroids"), Key(9)]
    public Asteroid[] Asteroids;
}

[MessagePackObject, JsonObject(MemberSerialization.OptIn)]
public class GasGiantData : BodyData
{
    [JsonProperty("firstOffsetDomainRotationSpeed"), Key(9)]
    public float FirstOffsetDomainRotationSpeed = 1;
    
    [JsonProperty("firstOffsetRotationSpeed"), Key(10)]
    public float FirstOffsetRotationSpeed = 1;
    
    [JsonProperty("secondOffsetDomainRotationSpeed"), Key(11)]
    public float SecondOffsetDomainRotationSpeed = 1;
    
    [JsonProperty("secondOffsetRotationSpeed"), Key(12)]
    public float SecondOffsetRotationSpeed = 1;
    
    [JsonProperty("albedoRotationSpeed"), Key(13)]
    public float AlbedoRotationSpeed = 1;

    [JsonProperty("gravRadiusMul")] [Key(14)]
    public float WaveRadiusMultiplier = 1;

    [JsonProperty("depthMul")] [Key(15)]
    public float WaveDepthMultiplier = 1;

    [JsonProperty("depthExp")] [Key(16)]
    public float WaveDepthExponent = 8;

    [JsonProperty("depthExp")] [Key(17)]
    public float WaveSpeedMultiplier = 8;
    
    [JsonProperty("materialOverrides"), Key(18)]
    public List<string> MaterialOverrides = new List<string>();
    
    [JsonProperty("colors"), Key(19)]
    public float4[] Colors = new float4[0];
}

public class SunData : GasGiantData
{
    [JsonProperty("lightColor"), Key(20)]
    public float3 LightColor = float3.zero;
    
    [JsonProperty("fogTintColor"), Key(21)]
    public float3 FogTintColor = float3.zero;

    [JsonProperty("lightRadiusMul")] [Key(22)]
    public float LightRadiusMultiplier = 1;
}

[MessagePackObject, JsonObject(MemberSerialization.OptIn)]
public class Asteroid
{
    [JsonProperty("distance"), Key(0)]
    public float Distance;
    
    [JsonProperty("phase"), Key(1)]
    public float Phase;
    
    [JsonProperty("size"), Key(2)]
    public float Size;
    
    [JsonProperty("rotationSpeed"), Key(3)]
    public float RotationSpeed;
}

[MessagePackObject, JsonObject(MemberSerialization.OptIn)]
public class OrbitData : DatabaseEntry
{
    [JsonProperty("parent"), Key(1)]
    public Guid Parent;

    [JsonProperty("distance"), Key(2)]
    public float Distance;

    [JsonProperty("phase"), Key(3)]
    public float Phase;
    
    [JsonProperty("phase"), Key(4)]
    public float2 FixedPosition = float2.zero;
    
    // [JsonProperty("period"), Key(4)]
    // public float Period;

    public static float2 Evaluate(float phase)
    {
        phase *= PI * 2;
        return new float2(cos(phase), sin(phase));
    }
}