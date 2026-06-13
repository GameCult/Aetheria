/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections;
using System.Collections.Generic;
using MessagePack;
public class HaulingTask : AgentTask
{
    public override TaskType Type => TaskType.Haul;

    [Key(4)]
    public Entity Origin;

    [Key(5)]
    public Entity Target;

    [Key(6)]
    public Guid ItemType;

    [Key(7)]
    public int Quantity;
}
