/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using GameCult.Caching;
using System;
using System.Collections;
using System.Collections.Generic;
using MessagePack;
using Newtonsoft.Json;

[MessagePackObject, 
 JsonObject(MemberSerialization.OptIn)]
public class StationTowing : AgentTask
{
    [IgnoreMember] public override TaskType Type => TaskType.Tow;
    
    [JsonProperty("station"), Key(4)]
    public OrbitalEntity Station;
    
    [JsonProperty("orbitParent"), Key(5)]
    public CultRecordKey OrbitParent;
    
    [JsonProperty("orbitDistance"), Key(6)]
    public float OrbitDistance;
}
