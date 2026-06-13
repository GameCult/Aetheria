/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;

[Inspectable]
public class Faction : RuntimeCatalogEntry, INamedEntry
{
    [Inspectable]
    public string Name;

    [Inspectable]
    public string ShortName;

    [InspectableText]
    public string Description;

    [InspectableTexture]
    public string Logo;

    [InspectableRuntimeCatalogLink(typeof(PersonalityAttribute))]
    public Dictionary<Guid, float> Personality = new Dictionary<Guid, float>();

    // [Inspectable]
    // public bool PlayerHostile;

    [InspectableColor]
    public float3 PrimaryColor;

    [InspectableColor]
    public float3 SecondaryColor;

    [InspectableRuntimeCatalogLink(typeof(NameFile))]
    public Guid GeonameFile;

    [InspectableRuntimeCatalogLink(typeof(HullData))]
    public Guid BossHull;

    [Inspectable]
    public int InfluenceDistance = 4;

    [InspectableRuntimeCatalogLink(typeof(Faction)), RangedFloat(0, 1)]
    public Dictionary<Guid, float> Allegiance = new Dictionary<Guid, float>();

    [InspectableSoundBank]
    public uint OverworldMusic;

    [InspectableSoundBank]
    public uint CombatMusic;

    [InspectableSoundBank]
    public uint BossMusic;

    public string EntryName
    {
        get => Name;
        set => Name = value;
    }
}

[Inspectable]
public class NameFile : RuntimeCatalogEntry, INamedEntry
{
    public string Name;
    public string[] Names;

    public string EntryName
    {
        get => Name;
        set => Name = value;
    }
}
