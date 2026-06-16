/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;

public class StationTowing : AgentTask
{
    public override TaskType Type => TaskType.Tow;

    public OrbitalEntity Station;

    public string OrbitParentKey = "";

    public float OrbitDistance;
}
