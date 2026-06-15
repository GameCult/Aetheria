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

public class ZoneConstructionBlueprint
{
    public List<BodyConstructionData> Bodies = new List<BodyConstructionData>();
    public List<OrbitConstructionData> Orbits = new List<OrbitConstructionData>();
    public List<EntityConstructionBlueprint> Entities = new List<EntityConstructionBlueprint>();
    public float Radius = 2000;
    public float Mass = 10000;
    public double Time;
}

public abstract class BodyConstructionData : INamedEntry
{
    public Guid ID = Guid.NewGuid();

    public string Name = "";

    public Guid Orbit;

    public float Mass = 0;

    public Dictionary<string, float> Resources = new Dictionary<string, float>();

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

public class PlanetConstructionData : BodyConstructionData
{

}

public class AsteroidBeltConstructionData : BodyConstructionData
{
    public Asteroid[] Asteroids;
}

public class GasGiantConstructionData : BodyConstructionData
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

public class SunConstructionData : GasGiantConstructionData
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

public class OrbitConstructionData
{
    public Guid ID = Guid.NewGuid();

    public Guid Parent;

    public float Distance;

    public float Phase;

    public float2 FixedPosition = float2.zero;

}
