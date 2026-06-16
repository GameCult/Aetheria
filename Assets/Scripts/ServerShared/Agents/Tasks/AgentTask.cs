/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

public abstract class AgentTask
{
    public int Priority;

    public string ZoneKey = "";

    public bool Reserved;

    public abstract TaskType Type { get; }
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

