/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections;
using System.Collections.Generic;
using GameCult.Caching;
using MessagePack;
using Newtonsoft.Json;
using CultMath;

[CultDocument("aetheria.faction", "1"), Inspectable, MessagePackObject, JsonObject(MemberSerialization.OptIn)]
public class Faction
{
    [Inspectable, CultName, JsonProperty("name"), Key(1)]
    public string Name;

    [Inspectable, JsonProperty("shortName"), Key(2)]
    public string ShortName;

    [Inspectable, CultInspectorTextArea, JsonProperty("description"), Key(3)]
    public string Description;

    [Inspectable, CultInspectorAssetGuid, JsonProperty("logo"), Key(4)]
    public string Logo;

    [Inspectable, CultReference(typeof(PersonalityAttribute), many: true), JsonProperty("personality"), Key(5)]
    public Dictionary<CultRecordRef<PersonalityAttribute>, float> Personality = new Dictionary<CultRecordRef<PersonalityAttribute>, float>();

    // [Inspectable, JsonProperty("hostile"), Key(6)]
    // public bool PlayerHostile;

    [InspectableColor, JsonProperty("primaryColor"), Key(7)]
    public float3 PrimaryColor;

    [InspectableColor, JsonProperty("secondaryColor"), Key(8)]
    public float3 SecondaryColor;

    [Inspectable, JsonProperty("nameFile"), Key(9)]
    public CultRecordRef<NameFile> GeonameFile;

    [Inspectable, JsonProperty("bossHull"), Key(10)]
    public CultRecordRef<HullData> BossHull;

    [Inspectable, JsonProperty("influence"), Key(11)]
    public int InfluenceDistance = 4;

    [Inspectable, CultReference(typeof(Faction), many: true), CultInspectorRange(0, 1), JsonProperty("allegiance"), Key(12)]
    public Dictionary<CultRecordRef<Faction>, float> Allegiance = new Dictionary<CultRecordRef<Faction>, float>();

    [Inspectable, JsonProperty("overworldMusic"), Key(13)]
    public uint OverworldMusic;

    [Inspectable, JsonProperty("combatMusic"), Key(14)]
    public uint CombatMusic;

    [Inspectable, JsonProperty("bossMusic"), Key(15)]
    public uint BossMusic;
}

[CultDocument("aetheria.namefile", "1"), Inspectable, MessagePackObject]
public class NameFile
{
    [CultName, Key(1)] public string Name;
    [Key(2)] public string[] Names;
}
