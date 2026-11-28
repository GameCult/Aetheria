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
    public string StartingHullName = "Longinus";
    public float HeatstrokePhasingFloor = 0;
    public float HeatstrokePhasingFrequency = 5;
    public float WormholeDistanceRatio;
    // The follow camera's distance from the hull's centre of mass, as a multiple of the hull's reach
    // (Contacts.Radius); 1.5 keeps the Longinus at about 30 m.
    public float FollowCameraDistancePerReach = 1.5f;
    public float DefaultViewDistance;
    public float[] MinimapZoomLevels;
    public int DefaultMinimapZoom;
    public ExponentialCurve IconSize;
    public int AsteroidMeshCount = 5;
    public float MinimapAsteroidSize = 3;
    public float MinimapIconSize = 1f / 32;
    // Tactical view (rulings tactical-view-distinct-mode, tactical-view-in-runs-slow-time): one wheel notch multiplies the
    // camera distance by this, in either view. Every feel constant here (step, band, detent, ease, pitch) is a placeholder
    // the operator tunes by eye.
    public float TacticalZoomStep = 1.25f;
    // The camera distance (m) where the scene starts to fade to the schematic: the chase view's farthest distance and the
    // detent band's near edge; and where it is fully faded: the tactical view's nearest distance and the band's far edge.
    public float TacticalBlendStart = 250;
    public float TacticalBlendEnd = 1000;
    // Inside the detent band a notch moves the view this fraction as far as outside it (lower holds harder).
    public float DetentResistance = .6f;
    // Seconds, e-folding, for the band to push the view back to the edge it came from.
    public float DetentRelaxTime = .4f;
    // Seconds the camera takes to settle on where the wheel put it (critically damped).
    public float ViewEaseTime = .15f;
    // The tactical camera's pitch down from the horizon, degrees.
    public float TacticalPitch = 55;
    // Sim seconds per real second at full fade (about 100x slower).
    public float TacticalTimeScale = .01f;
    // The farthest zoom, as a multiple of the zone radius.
    public float TacticalFitPerRadius = 2;
    // Schematic icon size as a fraction of the camera distance.
    public float TacticalIconSize = 1f / 40;
    // The colour the scene fades toward.
    public Color TacticalBackground = new Color(.02f, .03f, .05f, 1);
    public float PlanetRotationSpeed = 1;
    public SectorBackgroundSettings CastBackgroundSettings;
    public SectorBackgroundSettings SectorBackgroundSettings;
    public SectorGenerationSettings SectorGenerationSettings;
    public BodySettingsCollection[] BodySettingsCollections;
    public PlanetSettings PlanetSettings;
    public ZoneGenerationSettings ZoneSettings;
    public GameplaySettings GameplaySettings;
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

    // Every HardpointType has an entry in the asset's ItemIcons; a missing one is named, never an IndexOutOfRange.
    public Sprite GetIcon(HardpointType type) => IconOf(ItemIcons, type);

    public static Sprite IconOf(Sprite[] icons, HardpointType type)
    {
        var index = (int) type;
        if (icons == null || index < 0 || index >= icons.Length || icons[index] == null)
            throw new InvalidOperationException($"GameSettings.ItemIcons has no sprite for HardpointType.{type} (index {index}, {icons?.Length ?? 0} entries); set it in the Settings asset.");
        return icons[index];
    }
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