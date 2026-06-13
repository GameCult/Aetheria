/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections;
using System.Collections.Generic;
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

public abstract class BodyData : DatabaseEntry, INamedEntry
{
    public string Name = "";

    public Guid Orbit;

    public float Mass = 0;

    public Dictionary<Guid, float> Resources = new Dictionary<Guid, float>();

    public float BodyRadiusMultiplier = 1;

    public float GravityRadiusMultiplier = 1;

    public float GravityDepthMultiplier = 1;

    public float GravityDepthExponent = 16;

    public string EntryName
    {
        get => Name;
        set => Name = value;
    }
}

public class PlanetData : BodyData
{

}

public class AsteroidBeltData : BodyData
{
    public Asteroid[] Asteroids;
}

public class GasGiantData : BodyData
{
    public float FirstOffsetDomainRotationSpeed = 1;

    public float FirstOffsetRotationSpeed = 1;

    public float SecondOffsetDomainRotationSpeed = 1;

    public float SecondOffsetRotationSpeed = 1;

    public float AlbedoRotationSpeed = 1;

    public float WaveRadiusMultiplier = 1;

    public float WaveDepthMultiplier = 1;

    public float WaveDepthExponent = 8;

    public float WaveSpeedMultiplier = 8;

    public List<string> MaterialOverrides = new List<string>();

    public float4[] Colors = new float4[0];
}

public class SunData : GasGiantData
{
    public float3 LightColor = float3.zero;

    public float3 FogTintColor = float3.zero;

    public float LightRadiusMultiplier = 1;
}

public class Asteroid
{
    public float Distance;

    public float Phase;

    public float Size;

    public float RotationSpeed;
}

public class OrbitData : DatabaseEntry
{
    public Guid Parent;

    public float Distance;

    public float Phase;

    public float2 FixedPosition = float2.zero;

    public static float2 Evaluate(float phase)
    {
        phase *= PI * 2;
        return new float2(cos(phase), sin(phase));
    }
}
