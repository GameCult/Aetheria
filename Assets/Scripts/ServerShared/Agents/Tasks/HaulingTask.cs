/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections;
using System.Collections.Generic;
using MessagePack;
using Newtonsoft.Json;

public class HaulingTask : AgentTask
{
    public override TaskType Type => TaskType.Haul;
    
    [JsonProperty("origin"), Key(4)]
    public Entity Origin;
    
    [JsonProperty("target"), Key(5)]
    public Entity Target;
    
    [JsonProperty("itemType"), Key(6)]
    public Guid ItemType;
    
    [JsonProperty("quantity"), Key(7)]
    public int Quantity;
}
