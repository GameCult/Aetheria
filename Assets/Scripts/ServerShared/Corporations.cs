/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections;
using System.Collections.Generic;
using MessagePack;
using Newtonsoft.Json;
using Unity.Mathematics;

[RethinkTable("Galaxy"), Inspectable, MessagePackObject, JsonObject(MemberSerialization.OptIn)]
public class Faction : DatabaseEntry, INamedEntry
{
    [Inspectable, JsonProperty("name"), Key(1)]
    public string Name;
    
    [Inspectable, JsonProperty("shortName"), Key(2)]
    public string ShortName;
    
    [InspectableText, JsonProperty("description"), Key(3)]
    public string Description;
    
    [InspectableTexture, JsonProperty("logo"), Key(4)]
    public string Logo;
    
    [InspectableDatabaseLink(typeof(PersonalityAttribute)), JsonProperty("personality"), Key(5)]  
    public Dictionary<Guid, float> Personality = new Dictionary<Guid, float>();

    // [Inspectable, JsonProperty("hostile"), Key(6)]
    // public bool PlayerHostile;

    [InspectableColor, JsonProperty("primaryColor"), Key(7)]
    public float3 PrimaryColor;
    
    [InspectableColor, JsonProperty("secondaryColor"), Key(8)]
    public float3 SecondaryColor;

    [InspectableDatabaseLink(typeof(NameFile)), JsonProperty("nameFile"), Key(9)]  
    public Guid GeonameFile;

    [InspectableDatabaseLink(typeof(HullData)), JsonProperty("bossHull"), Key(10)]  
    public Guid BossHull;

    [Inspectable, JsonProperty("influence"), Key(11)]
    public int InfluenceDistance = 4;
    
    [InspectableDatabaseLink(typeof(Faction)), RangedFloat(0, 1), JsonProperty("allegiance"), Key(12)]  
    public Dictionary<Guid, float> Allegiance = new Dictionary<Guid, float>();

    [InspectableSoundBank, JsonProperty("overworldMusic"), Key(13)]
    public uint OverworldMusic;

    [InspectableSoundBank, JsonProperty("combatMusic"), Key(14)]
    public uint CombatMusic;

    [InspectableSoundBank, JsonProperty("bossMusic"), Key(15)]
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
