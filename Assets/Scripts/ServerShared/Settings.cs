/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections;
using System.Collections.Generic;
using MessagePack;
using Newtonsoft.Json;
using CultMath;
using static CultMath.math;

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

    // Non-hostile ships for hand-testing combat/targeting: belong to a galaxy faction that is
    // neither the zone owner nor the zone's nearest faction, so derived hostility rules leave
    // them non-hostile to the player. Zero eligible factions means none are spawned.
    public int NeutralWandererCount = 2;
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
    public float FiringArc = 120;
    // Cut 2 (docs/fire-control-cut.md, Q4): what an entity with no working targeting system fires with --
    // the ceiling FireControl.Accuracy falls back to (Resolution 1, Precision 0 alongside it). Authored low
    // on purpose ("really, really bad," operator 2026-09-19): .05 against the two catalog designs' own
    // Accuracy ranges (the authoring spec this cut produces proposes .45-.6 for the 1-cell design and
    // .65-.85 for the 2-cell one) leaves unaided fire capable of hitting something close, slow and
    // unaware, and little else. One authored setting, easy to find and turn -- the operator rules the real
    // figure in play.
    public float UnaidedAccuracy = .05f;
    // Cut 5 (docs/fire-control-cut.md, 5.1, Soul finding 3): the unaided fallback for Tracking, alongside
    // UnaidedAccuracy above -- an entity with no working targeting system used to fall back to Tracking 0,
    // which Commit turned into a hard wall (any nonzero deviation was an automatic miss). Ten world units of
    // fire-time-projection deviation forgiveness is the first guess and the operator's knob, the same shape
    // as UnaidedAccuracy: authored deliberately bad, not authored broken.
    public float UnaidedTracking = 10f;
    // Cut 6d (docs/fire-control-cut.md): the unaided fallback for Precision, alongside UnaidedAccuracy and
    // UnaidedTracking above -- FireControl.Precision falls back to this the same way it already falls back to
    // UnaidedAccuracy/UnaidedTracking. Precision is now a grouping tightness (FireControl.Sigma = 1/Precision,
    // in hull-schematic cell units), not the old coin-flip probability, so the unaided floor has to be
    // authored on that scale. .3 -> sigma 3.33 cells: against the live catalog's own hulls (LonginusX 6x17,
    // Zenith 12x12, Turret 8x8) that puts pOnHull at the hull's own centre of mass around .5-.84 -- broadly
    // sprayed across the whole silhouette (sigma is a large fraction of the hull's own width) while still
    // landing on the ship more often than not. A tighter floor (e.g. .1, sigma 10) was tried first and
    // rejected: it drove pOnHull for the same hulls down to .08-.18, reading as "can't hit the broad side of a
    // barn" rather than "sprays the silhouette." First guess, the same deliberately-bad shape as its two
    // siblings; the operator rules the real figure in play.
    public float UnaidedPrecision = .3f;
    public float AgentRangeExponent = .25f;
    public float AgentForwardLerp = .5f;
    public float AgentMaxForwardDistance = 50;

    // Cut 3 (docs/fire-control-cut.md): first guesses: the headless fixture is the tuning harness, the
    // operator smoke is the arbiter (§ Risks).
    //
    // How long before impact a shot's outcome commits (R4). Deviation counts up to this horizon; after it
    // the result is frozen and the rest of the flight is pure choreography.
    public float CommitHorizon = .5f;

    // World units per hull-schematic cell, used only by FireControl.HitProbability's angular-size term
    // (pSpread) to turn a hull's cell footprint into a real-world silhouette size at range.
    public float SchematicCellSize = 2f;

    // The floor an AI's own predicted hit probability (FireControl.HitProbability) must clear before it counts
    // a firing solution worth taking (Combat.cs, TurretController.cs). Player fire is gated on arc alone
    // (Weapon.ArcAllowsFire, Q2) -- this threshold is an AI fire-discipline heuristic, not part of the roll.
    public float AgentMinHitProbability = .2f;

    // Cut 4 (docs/fire-control-cut.md): how often a continuous weapon (ConstantWeapon) rolls a discrete
    // outcome for Damage * this interval, through the same FireControl.Fire/Step pair a discrete shot uses --
    // a beam is a sequence of rolls, not a continuous truth. First guess; the headless fixture is the tuning
    // harness, the operator smoke is the arbiter, same as Cut 3's other first guesses.
    public float BeamResolveInterval = .25f;
}

[Serializable, MessagePackObject(keyAsPropertyName: true), JsonObject]
public class RarityTier
{
    public string Name;
    public float Quality;
    public float3 Color;
    public float Rarity;
}