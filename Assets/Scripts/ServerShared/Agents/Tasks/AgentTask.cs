/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections;
using System.Collections.Generic;
using MessagePack;
using Newtonsoft.Json;

// Agent tasks are owned by their Agent and not persisted.
[JsonObject(MemberSerialization.OptIn)]
public abstract class AgentTask
{
    [JsonProperty("priority"), Key(1)]
    public int Priority;

    [JsonProperty("reserved"), Key(3)]
    public bool Reserved;

    [IgnoreMember] public abstract TaskType Type { get; }
}

public enum TaskType
{
    None,
    Mine,
    Haul,
    Tow,
    Defend,
    Attack,
    Explore
}

