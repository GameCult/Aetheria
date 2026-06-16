/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using static Unity.Mathematics.math;

[Serializable]
public class ExponentialCurve
{
    public float Exponent;

    public float Multiplier;

    public float Constant;

    public float Evaluate(float value) => Multiplier * pow(value, Exponent) + Constant;
}

[Serializable]
public class ExponentialLerp
{
    public float Exponent;

    public float Minimum;

    public float Maximum;

    public float Evaluate(float value) => Minimum + pow(saturate(value), Exponent) * (Maximum - Minimum);
}
