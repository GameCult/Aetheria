using System;

[AttributeUsage(AttributeTargets.Field)]
public sealed class RuntimeProjectionKeyAttribute : Attribute
{
    public RuntimeProjectionKeyAttribute(int key)
    {
        Key = key;
    }

    public int Key { get; }
}
