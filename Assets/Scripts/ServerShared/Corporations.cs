/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;

[Inspectable]
public class Faction : INamedEntry
{
    public Guid ID = Guid.NewGuid();
    public string FactionKey;

    [Inspectable]
    public string Name;

    [Inspectable]
    public string ShortName;

    [InspectableText]
    public string Description;

    [InspectableTexture]
    public string Logo;

    // [Inspectable]
    // public bool PlayerHostile;

    [InspectableColor]
    public float3 PrimaryColor;

    [InspectableColor]
    public float3 SecondaryColor;
    public Guid GeonameFile;
    public string GeonameFileKey;
    public Guid BossHull;

    [Inspectable]
    public int InfluenceDistance = 4;

    public Dictionary<string, float> AllegianceByKey = new Dictionary<string, float>();

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

    public override int GetHashCode()
    {
        return ID.GetHashCode();
    }

    public override bool Equals(object obj)
    {
        return obj is Faction faction && faction.ID == ID;
    }
}
