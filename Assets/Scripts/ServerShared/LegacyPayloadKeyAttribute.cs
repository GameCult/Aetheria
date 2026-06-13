using System;

[AttributeUsage(AttributeTargets.Field)]
public sealed class LegacyPayloadKeyAttribute : Attribute
{
    public LegacyPayloadKeyAttribute(int key)
    {
        Key = key;
    }

    public int Key { get; }
}
