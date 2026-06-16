/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;

public class Faction : INamedEntry
{
    public string FactionKey;

    public string Name;

    public string ShortName;

    public string Description;

    public string Logo;

    // public bool PlayerHostile;

    public float3 PrimaryColor;

    public float3 SecondaryColor;
    public string GeonameFileKey;
    public string BossHullItemKey;

    public int InfluenceDistance = 4;

    public Dictionary<string, float> AllegianceByKey = new Dictionary<string, float>();

    public uint OverworldMusic;

    public uint CombatMusic;

    public uint BossMusic;

    public string EntryName
    {
        get => Name;
        set => Name = value;
    }

    public bool HasSameKey(Faction other)
    {
        return other != null &&
               !string.IsNullOrWhiteSpace(FactionKey) &&
               string.Equals(FactionKey, other.FactionKey, StringComparison.OrdinalIgnoreCase);
    }

    public override int GetHashCode()
    {
        return StringComparer.OrdinalIgnoreCase.GetHashCode(FactionKey ?? "");
    }

    public override bool Equals(object obj)
    {
        return obj is Faction faction && HasSameKey(faction);
    }
}
