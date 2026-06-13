using System;

[Serializable]
public class EntitySettings
{
    public float ShutdownPerformance = .1f;

    public EntitySettings Copy()
    {
        return new EntitySettings
        {
            ShutdownPerformance = ShutdownPerformance
        };
    }
}
