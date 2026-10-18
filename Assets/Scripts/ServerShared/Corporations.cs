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

    // Key 9 was the faction's name-file link, retired by ruling faction-curated-place-names; never reuse it.

    // Key 10 was the faction's boss hull reference, retired by ruling faction-boss-is-a-loadout-reference; never reuse it.

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

    // Key 16 is reserved for Relations (operator ruling faction-relations-field).

    // The faction's rules of engagement; null fights as FactionDoctrine.Default.
    [Inspectable, JsonProperty("doctrine"), Key(17)]
    public FactionDoctrine Doctrine;

    // The maker's final assembly: the distribution every lot of theirs draws its own quality from, and the hard ceiling on each
    // of its roles (quality-lean-assembly-hard-ceiling). Null is not authored: a lot of theirs assembles at AssemblyDistribution's
    // defaults. assembly-from-maker derives it once at the part mean plus a tolerance margin (quality-min-tolerance-binned-assembly);
    // tune it in Studio after that. Key 16 is reserved for Relations and 18 for Livery.
    [Inspectable, JsonProperty("assembly"), Key(19)]
    public AssemblyDistribution Assembly;

    // Curated place names in authored order; the home zone takes the first (ruling faction-curated-place-names).
    // Authored by AetherDb place-names, which restores the table on every apply.
    [Inspectable, JsonProperty("places"), Key(21)]
    public FactionPlace[] Places = Array.Empty<FactionPlace>();
}

// Historical, narrative and strategic intent join as keys 2+ when economics-driven placement lands (ruling faction-curated-place-names).
[MessagePackObject, JsonObject(MemberSerialization.OptIn)]
public class FactionPlace
{
    [Inspectable, JsonProperty("name"), Key(1)]
    public string Name;

    // The one rule for a valid place name: trimmed, non-empty and not a catalogue designation (EAC-, any case). Galaxy.NamePlaces
    // treats an invalid entry as absent; AetherDb place-names refuses a table that holds one.
    public static bool IsValidName(string name) =>
        !string.IsNullOrEmpty(name) && name == name.Trim() && !name.StartsWith("EAC-", StringComparison.OrdinalIgnoreCase);
}

