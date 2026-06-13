/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;

public class HaulingTask : AgentTask
{
    public override TaskType Type => TaskType.Haul;

    public Entity Origin;

    public Entity Target;

    public Guid ItemType;

    public int Quantity;
}
