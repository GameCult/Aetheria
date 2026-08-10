using System;
using MessagePack;

[MessagePackObject, Serializable]
public class EntitySettings
{
    [Key(0)]
    public float ShutdownPerformance = .1f;
}