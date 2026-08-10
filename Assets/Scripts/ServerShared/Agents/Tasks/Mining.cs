/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections;
using System.Collections.Generic;
using JsonKnownTypes;
using MessagePack;
using Newtonsoft.Json;

[MessagePackObject, 
 JsonObject(MemberSerialization.OptIn), JsonConverter(typeof(JsonKnownTypesConverter<DatabaseEntry>))]
public class Mining : AgentTask
{
    [IgnoreMember] public override TaskType Type => TaskType.Mine;
    
    [JsonProperty("asteroids"), Key(4)]
    public Guid Asteroids;
}