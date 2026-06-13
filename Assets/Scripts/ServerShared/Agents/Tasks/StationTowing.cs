/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections;
using System.Collections.Generic;
using MessagePack;
[MessagePackObject]
public class StationTowing : AgentTask
{
    [IgnoreMember] public override TaskType Type => TaskType.Tow;

    [Key(4)]
    public OrbitalEntity Station;

    [Key(5)]
    public Guid OrbitParent;

    [Key(6)]
    public float OrbitDistance;
}
