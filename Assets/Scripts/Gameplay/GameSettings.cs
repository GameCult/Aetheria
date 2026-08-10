/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Sector Display Properties", menuName = "Aetheria/Sector Display Properties")]
public class GameSettings : ScriptableObject
{
    public string DefaultOverworldSoundbank = "Overworld_Generic";
    public string DefaultCombatSoundbank = "Combat_Generic";
    public string DefaultBossSoundbank = "Boss_Metal";
    public string AmbienceSoundBank = "Ambience";
    public string StartingHullName = "Longinus";
    public float PickupLifetime = 30;
    public float LootDropProbability = .25f;
    public float LootDropVelocity = 25;
    public float HeatstrokePhasingFloor = 0;
    public float HeatstrokePhasingFrequency = 5;
    public float WormholeDistanceRatio;
    public float DefaultViewDistance;
    public float MinimapZoneGravityRange;
    public float[] MinimapZoomLevels;
    public int DefaultMinimapZoom;
    public ExponentialCurve IconSize;
    public int AsteroidMeshCount = 5;
    public float MinimapAsteroidSize = 3;
    public float MinimapIconSize = 1f / 32;
    public float PlanetRotationSpeed = 1;
    public NameGeneratorSettings NameGeneratorSettings;
    public TutorialGenerationSettings TutorialGenerationSettings;
    public SectorBackgroundSettings TutorialBackgroundSettings;
    public SectorBackgroundSettings SectorBackgroundSettings;
    public SectorGenerationSettings SectorGenerationSettings;
    public BodySettingsCollection[] BodySettingsCollections;
    public PlanetSettings PlanetSettings;
    public ZoneGenerationSettings ZoneSettings;
    public GameplaySettings GameplaySettings;
    public Color HullHitColor;
    public Color ArmorHitColor;
    public Color HardpointHitColor;
    public Color GearHitColor;
    public Gradient ArmorGradient;
    public Gradient DurabilityGradient;
    public ZoneEnvironment DefaultEnvironment;
    [HideInInspector] public Sprite[] ItemIcons;
    [HideInInspector] public Sprite[] WeaponTypeIcons;
    [HideInInspector] public Sprite[] WeaponCaliberIcons;
    [HideInInspector] public Sprite[] WeaponRangeIcons;
    [HideInInspector] public Sprite[] WeaponFireTypeIcons;
    [HideInInspector] public Sprite[] WeaponModifierIcons;

    public Sprite GetIcon(HardpointType type) => ItemIcons[(int) type];
    public Sprite GetIcon(WeaponType type) => WeaponTypeIcons[(int) type];
    public Sprite GetIcon(WeaponRange range) => WeaponRangeIcons[(int) range];
    public Sprite GetIcon(WeaponCaliber caliber) => WeaponCaliberIcons[(int) caliber];

    public IEnumerable<Sprite> GetIcons(WeaponFireType type)
    {
        var count = Enum.GetValues(typeof(WeaponFireType)).Length;
        var t = (int) type;
        for (int i = 0; i < count - 1; i++)
        {
            if (1 << i == (1 << i & t))
                yield return WeaponFireTypeIcons[i + 1];
        }
    }

    public IEnumerable<Sprite> GetIcons(WeaponModifiers type)
    {
        var count = Enum.GetValues(typeof(WeaponModifiers)).Length;
        var t = (int) type;
        for (int i = 0; i < count - 1; i++)
        {
            if (1 << i == (1 << i & t))
                yield return WeaponModifierIcons[i + 1];
        }
    }
}

[Serializable]
public class BodySettingsCollection
{
    public float MinimumMass;
    public CelestialBodySettings[] BodySettings;
}