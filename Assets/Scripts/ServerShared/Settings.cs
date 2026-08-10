/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections;
using System.Collections.Generic;
using MessagePack;
using Newtonsoft.Json;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using static Unity.Mathematics.noise;

[Serializable, MessagePackObject(keyAsPropertyName:true), JsonObject]
public class PlanetSettings
{
    public float ZoneDepthExponent;
    public float ZoneDepth;
    public float ZoneBoundaryFog;
    public ExponentialCurve GravityDepth;
    public ExponentialCurve GravityRadius;
    public ExponentialCurve WaveDepth;
    public ExponentialCurve WaveRadius;
    public ExponentialCurve WaveFrequency;
    public ExponentialCurve WaveSpeed;
    public ExponentialCurve LightRadius;
    public ExponentialCurve BodyRadius;
    public float AsteroidVerticalOffset = -5f;
    public ExponentialLerp AsteroidSize;
    public ExponentialLerp AsteroidHitpoints;
    public ExponentialLerp AsteroidRespawnTime;
    public float GravityStrength;
    public float MiningDifficulty = 500f;
    public float SecureAreaRadiusMultiplier = .45f;

    public ExponentialCurve OrbitPeriod;
}

[Serializable, MessagePackObject(keyAsPropertyName:true), JsonObject]
public class GalaxyShapeSettings
{
    public int Arms = 4;
    public float Twist = 10;
    public float TwistExponent = 2;
}

[Serializable, MessagePackObject(keyAsPropertyName:true), JsonObject]
public class NameGeneratorSettings
{
    public int NameGeneratorMinLength = 5;
    public int NameGeneratorMaxLength = 10;
    public int NameGeneratorOrder = 4;
}

[Serializable, MessagePackObject(keyAsPropertyName:true), JsonObject]
public class SectorBackgroundSettings
{
    public float NoiseAmplitude;
    public float NoiseOffset;
    public float NoiseGain;
    public float NoiseLacunarity;
    public float NoiseFrequency;
    public float NoisePosition;
    public float CloudExponent;
    public float CloudAmplitude;
    
    public float fBm(float2 p, int octaves)
    {
        float freq = NoiseFrequency, amp = .5f;
        float sum = 0;	
        for(int i = 0; i < octaves; i++) 
        {
            if(i<4)
                sum += (1-abs(snoise(p * freq))) * amp;
            else sum += abs(snoise(p * freq)) * amp;
            freq *= NoiseLacunarity;
            amp *= NoiseGain;
        }
        return (sum + NoiseOffset)*NoiseAmplitude;
    }

    public float CloudDensity(float2 uv)
    {
        float noise = fBm(uv + NoisePosition, 10);
        return pow(noise, CloudExponent) * CloudAmplitude;
    }
}

[Serializable, MessagePackObject(keyAsPropertyName:true), JsonObject]
public class SectorGenerationSettings
{
    public float LinkDensity = .5f;
    public int ZoneCount = 128;
    public int MegaCount;
    public int BossCount;
}

[Serializable, MessagePackObject(keyAsPropertyName:true), JsonObject]
public class TutorialGenerationSettings
{
    public string ProtagonistFaction;
    public string AntagonistFaction;
    public string BufferFaction;
    public string[] NeutralFactions;
    public string QuestFaction;
    public float LinkDensity = .5f;
    public int ZoneCount = 32;
}

[Serializable, MessagePackObject(keyAsPropertyName:true), JsonObject]
public class ZoneGenerationSettings
{
    public ExponentialCurve PlanetSafetyRadius;
    
    public float MassFloor = 1;
    public float SunMass = 10000;
    public float GasGiantMass = 2000;
    public float PlanetMass = 100f;

    public int SatellitePasses = 4;
    public float SatelliteCreationMassFloor = 100;
    public float SatelliteCreationProbability = .25f;
    public float BinaryCreationProbability = .25f;
    public float RosetteProbability = .25f;

    public ExponentialLerp ZoneRadius;
    public ExponentialLerp ZoneMass;
    public ExponentialLerp SubZoneCount;
    public float ZoneBoundaryRadius;
    
    public float BeltProbability = .05f;
    public float BeltMassCeiling = 500f;

    public ExponentialCurve AsteroidBeltWidth;
    public ExponentialCurve AsteroidCount;
    public ExponentialLerp AsteroidRotationSpeed;

    public float ResourceDensityMinimum = .1f;
    public float ResourceDensityMaximum = 1.5f;

    public float SunColorSaturation = .75f;
    public float SunSecondaryColorDistance = .25f;
    public float SunLightSaturation = .5f;
    public float SunFogTintSaturation = .5f;
    
    public ExponentialLerp GasGiantBandCount;
    public float GasGiantBandColorSeparation = .25f;
    public float GasGiantBandAltColorChance = .25f;
    public ExponentialLerp GasGiantBandSaturation;
    public ExponentialLerp GasGiantBandBrightness;

    public string[] NameData;
}

[Serializable, MessagePackObject(keyAsPropertyName: true), JsonObject]
public class GameplaySettings
{
    public EntitySettings DefaultEntitySettings;
    public RarityTier[] Tiers;
    public ExponentialLerp QualityPriceModifier;
    public float DurabilityQualityExponent = 2;
    public float DurabilityQualityMin = 2;
    public float DurabilityQualityMax = .25f;
    public float ThermalQualityExponent = 2;
    public float ThermalQualityMin = 2;
    public float ThermalQualityMax = .25f;
    public float DefaultShutdownPerformance = .25f;
    public float SevereHeatstrokeRiskThreshold = .25f;
    public float WormholeDepth = 1000;
    public float WormholeExitVelocity = 20;
    public float WormholeExitRadius = 50;
    public float WormholeAnimationDuration = 4;
    public float WormholeExitCurveStart = .8f;
    public float ThermalWearExponent = .01f;
    public float DeltaTempWearExponent = .01f;
    public float QualityWearExponent = 2;
    public int WeaponGroupCount = 6;
    public float WarpDistance = 25;
    public float DockingDistance = 25;
    public float ProductionPersonalityLerp = .05f;
    public float MessageDuration = 4f;
    public float TargetPersistenceDuration = 3;
    public ExponentialLerp StartingGearQuality;
    public float HeatRadiationExponent = 1;
    public float HeatRadiationMultiplier = 1;
    public float HeatConductionMultiplier = 1;
    public ExponentialCurve TemperatureEmissionCurve;
    public float HeatstrokeTemperature = 330;
    public float HeatstrokeMultiplier = .00001f;
    public float HeatstrokeExponent = 2;
    public float HeatstrokeRecoverySpeed = .2f;
    public float HeatstrokeControlLimit = .75f;
    public float HypothermiaTemperature = 273;
    public float HypothermiaMultiplier = .00001f;
    public float HypothermiaExponent = 2;
    public float HypothermiaRecoverySpeed = .2f;
    public float HypothermiaControlLimit = .75f;
    public float LockIndicatorNoiseAmplitude = 50f;
    public ExponentialLerp LockIndicatorFrequency;
    public ExponentialLerp LockSpinSpeed;
    public float TorqueFloor;
    public float TorqueMultiplier;
    public float AetherTorqueMultiplier;
    public float AetherHeatMultiplier;
    public float VisibilityDecay;
    public float TargetInfoDecay;
    public float TargetDetectionInfoThreshold;
    public float TargetArmorInfoThreshold;
    public float TargetGearInfoThreshold;
    public float ConvergenceMinimumDistance;
    public float AgentRangeExponent = .25f;
    public float AgentForwardLerp = .5f;
    public float AgentMaxForwardDistance = 50;
    public float AgentFiringMinDot = .99f;
}

[Serializable, MessagePackObject(keyAsPropertyName: true), JsonObject]
public class RarityTier
{
    public string Name;
    public float Quality;
    public float3 Color;
    public float Rarity;
}