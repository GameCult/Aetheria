/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections;
using System.Collections.Generic;
using MessagePack;
[MessagePackObject,
 Union(0, typeof(StationTowing)),
 Union(1, typeof(Mining)),
 Union(2, typeof(Survey)),
 Union(3, typeof(HaulingTask))]
public abstract class AgentTask : DatabaseEntry
{
    [Key(1)]
    public int Priority;

    [Key(2)]
    public Guid Zone;

    [Key(3)]
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

