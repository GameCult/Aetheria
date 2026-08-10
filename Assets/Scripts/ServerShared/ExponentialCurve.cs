/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections;
using System.Collections.Generic;
using MessagePack;
using Newtonsoft.Json;
using Unity.Mathematics;
using static Unity.Mathematics.math;

[Serializable, MessagePackObject, JsonObject]
public class ExponentialCurve
{
    [JsonProperty("exponent"), Key(0), Inspectable]
    public float Exponent;
    
    [JsonProperty("multiplier"), Key(1), Inspectable]
    public float Multiplier;
    
    [JsonProperty("constant"), Key(2), Inspectable]
    public float Constant;
    
    public float Evaluate(float value) => Multiplier * pow(value, Exponent) + Constant;
}

[Serializable, MessagePackObject, JsonObject]
public class ExponentialLerp
{
    [JsonProperty("exponent"), Key(0), Inspectable]
    public float Exponent;
    
    [JsonProperty("max"), Key(1), Inspectable]
    public float Minimum;
    
    [JsonProperty("min"), Key(2), Inspectable]
    public float Maximum;
    
    public float Evaluate(float value) => Minimum + pow(saturate(value), Exponent) * (Maximum - Minimum);
}
