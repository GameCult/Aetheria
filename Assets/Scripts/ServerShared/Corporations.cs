/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections;
using System.Collections.Generic;
using MessagePack;
using Unity.Mathematics;

[LegacyCatalogGroup("Galaxy"), Inspectable, MessagePackObject]
public class Faction : DatabaseEntry, INamedEntry
{
    [Inspectable, Key(1)]
    public string Name;

    [Inspectable, Key(2)]
    public string ShortName;

    [InspectableText, Key(3)]
    public string Description;

    [InspectableTexture, Key(4)]
    public string Logo;

    [InspectableDatabaseLink(typeof(PersonalityAttribute)), Key(5)]
    public Dictionary<Guid, float> Personality = new Dictionary<Guid, float>();

    // [Inspectable, Key(6)]
    // public bool PlayerHostile;

    [InspectableColor, Key(7)]
    public float3 PrimaryColor;

    [InspectableColor, Key(8)]
    public float3 SecondaryColor;

    [InspectableDatabaseLink(typeof(NameFile)), Key(9)]
    public Guid GeonameFile;

    [InspectableDatabaseLink(typeof(HullData)), Key(10)]
    public Guid BossHull;

    [Inspectable, Key(11)]
    public int InfluenceDistance = 4;

    [InspectableDatabaseLink(typeof(Faction)), RangedFloat(0, 1), Key(12)]
    public Dictionary<Guid, float> Allegiance = new Dictionary<Guid, float>();

    [InspectableSoundBank, Key(13)]
    public uint OverworldMusic;

    [InspectableSoundBank, Key(14)]
    public uint CombatMusic;

    [InspectableSoundBank, Key(15)]
    public uint BossMusic;

    [IgnoreMember] public string EntryName
    {
        get => Name;
        set => Name = value;
    }
}

[Inspectable, MessagePackObject]
public class NameFile : DatabaseEntry, INamedEntry
{
    [Key(1)] public string Name;
    [Key(2)] public string[] Names;

    [IgnoreMember]
    public string EntryName
    {
        get => Name;
        set => Name = value;
    }
}
