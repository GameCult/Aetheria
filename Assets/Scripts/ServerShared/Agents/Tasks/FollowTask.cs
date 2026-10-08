using System;
using MessagePack;

// Keep station on an entity at a standoff: the flight hailing a track holds its members here (Flight.cs).
// Runtime only, like every agent task.
public class FollowTask : AgentTask
{
    [IgnoreMember] public Entity Anchor;
    [IgnoreMember] public float Standoff;
    [IgnoreMember] public override TaskType Type => TaskType.Follow;
}
