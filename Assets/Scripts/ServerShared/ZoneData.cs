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
using UniRx;
using CultMath;
using static CultMath.math;
using float2 = CultMath.float2;
using float3 = CultMath.float3;

[MessagePackObject, JsonObject(MemberSerialization.OptIn)]
public class ZonePack
{
    [JsonProperty("planets"), Key(0)]
    public List<CultRecordRef<BodyData>> Planets = new List<CultRecordRef<BodyData>>();

    [JsonProperty("orbits"), Key(1)]
    public List<CultRecordRef<OrbitData>> Orbits = new List<CultRecordRef<OrbitData>>();

    [JsonProperty("entities"), Key(2)]
    public List<EntityPack> Entities = new List<EntityPack>();

    [JsonProperty("radius"), Key(3)]
    public float Radius = 2000;

    [JsonProperty("mass"), Key(4)]
    public float Mass = 10000;

    [JsonProperty("time"), Key(5)]
    public double Time;

    // Cut 2 (docs/mining-cut.md): chunk wear (damage taken, broken-until time), keyed by chunk. Nullable, not
    // defaulted to an empty list: an older record has no key 6 at all, and MessagePack fills a missing trailing
    // key with nil, which a reference-typed field reads back as null. ARunWithoutKeySixLoadsWhole pins that a
    // record from before this key existed loads with no wear, rather than throwing or silently reading a
    // default instance.
    [JsonProperty("chunkWear"), Key(6)]
    public List<ChunkWearPack> ChunkWear;
}

// Cut 2 (docs/mining-cut.md): one packed wear entry for one chunk. "Field" names the body that owns the chunk
// (an asteroid belt today; the operator's ruling generalizes chunks to other kinds of debris field later), not
// "belt" -- the chunk surface itself must not bake in the one live field kind.
[MessagePackObject, JsonObject(MemberSerialization.OptIn)]
public class ChunkWearPack
{
    [JsonProperty("field"), Key(0)]
    public CultRecordKey Field;

    [JsonProperty("index"), Key(1)]
    public int Index;

    [JsonProperty("damage"), Key(2)]
    public float Damage;

    [JsonProperty("brokenUntil"), Key(3)]
    public double? BrokenUntil;
}

[JsonObject(MemberSerialization.OptIn)]
public abstract class BodyData
{
    [CultName, JsonProperty("name"), Key(1)]
    public string Name = "";

    [JsonProperty("orbit"), Key(2)]
    public CultRecordRef<OrbitData> Orbit;

    [JsonProperty("mass"), Key(3)]
    public float Mass = 0;

    [CultReference(typeof(SimpleCommodityData), many: true), JsonProperty("resources"), Key(4)]
    public Dictionary<CultRecordRef<SimpleCommodityData>, float> Resources = new Dictionary<CultRecordRef<SimpleCommodityData>, float>();

    [JsonProperty("bodyRadiusMul")] [Key(5)]
    public float BodyRadiusMultiplier = 1;

    [JsonProperty("gravRadiusMul")] [Key(6)]
    public float GravityRadiusMultiplier = 1;

    [JsonProperty("depthMul")] [Key(7)]
    public float GravityDepthMultiplier = 1;

    [JsonProperty("depthExp")] [Key(8)]
    public float GravityDepthExponent = 16;
}

[CultDocument("aetheria.planetdata", "1"), MessagePackObject, JsonObject(MemberSerialization.OptIn)]
public class PlanetData : BodyData
{

}

[CultDocument("aetheria.asteroidbeltdata", "1"), MessagePackObject, JsonObject(MemberSerialization.OptIn)]
public class AsteroidBeltData : BodyData
{
    [JsonProperty("asteroids"), Key(9)]
    public Asteroid[] Asteroids;

    // Mining Cut 3 (docs/mining-cut-refresh.md, Q16 B): the belt's field kind, chosen once and saved. Unset on a
    // belt from before this key existed (MessagePack leaves a missing trailing key at its default); Zone assigns
    // it on first load through FieldKinds.Ensure, the same function generation uses.
    [JsonProperty("kind"), Key(10)]
    public CultRecordRef<FieldKindData> Kind;
}

// Mining Cut 3 (docs/mining-cut-refresh.md, Q11): a kind of debris field, an authored catalog record. A chunk's
// reflectivity comes from its field's kind; Cut 5 adds the yield table.
[CultDocument("aetheria.fieldkinddata", "1"), MessagePackObject, JsonObject(MemberSerialization.OptIn)]
public class FieldKindData
{
    [CultName, JsonProperty("name"), Key(0)]
    public string Name = "";

    // Reflectivity per schematic cell of chunk area: a chunk reflects CrossSection × its area in cells × the light
    // on it (Zone.ChunkVisibility), the rule Reflector applies to a hull.
    [JsonProperty("crossSection"), Key(1)]
    public float CrossSection;

    // How often generation picks this kind, relative to the others (FieldKinds.Assign). Zero never picks it.
    [JsonProperty("generationWeight"), Key(2)]
    public float GenerationWeight = 1;
}

// Mining Cut 3 (Q16 B): the one owner of which kind a belt is.
public static class FieldKinds
{
    // A weighted pick among the catalog's kinds, by a stable hash of the belt's key: the same belt and the same
    // catalog always give the same kind. Kinds are ordered by key so the pick does not depend on store order.
    // Unset when the catalog has no kind with positive weight.
    public static CultRecordRef<FieldKindData> Assign(CultRecordKey field, CultCache catalog)
    {
        var kinds = catalog.GetAll<FieldKindData>()
            .Where(kind => kind.GenerationWeight > 0f)
            .Select(kind => (Key: catalog.RefOf(kind).Key, Weight: kind.GenerationWeight))
            .OrderBy(kind => kind.Key.Value, StringComparer.Ordinal)
            .ToArray();
        if (kinds.Length == 0) return default;

        var pick = field.Value.StableHash() / 4294967296.0 * kinds.Sum(kind => (double) kind.Weight);
        foreach (var kind in kinds)
        {
            if (pick < kind.Weight) return new CultRecordRef<FieldKindData>(kind.Key);
            pick -= kind.Weight;
        }
        return new CultRecordRef<FieldKindData>(kinds[kinds.Length - 1].Key);
    }

    // Gives a belt with no kind its kind and saves it; a belt that has one keeps it, whatever the catalog now says.
    // Generation (ZoneGenerator) and first load (Zone) both call this, so there is one assignment rule.
    public static void Ensure(CultRecordKey field, AsteroidBeltData belt, CultCache cache)
    {
        if (belt.Kind.IsSet()) return;
        belt.Kind = Assign(field, cache);
        if (belt.Kind.IsSet()) cache.Upsert(belt);
    }
}

[CultDocument("aetheria.gasgiantdata", "1"), MessagePackObject, JsonObject(MemberSerialization.OptIn)]
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

[CultDocument("aetheria.sundata", "1"), MessagePackObject, JsonObject(MemberSerialization.OptIn)]
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

[CultDocument("aetheria.orbitdata", "1"), MessagePackObject, JsonObject(MemberSerialization.OptIn)]
public class OrbitData
{
    [JsonProperty("parent"), Key(1)]
    public CultRecordRef<OrbitData> Parent;

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
