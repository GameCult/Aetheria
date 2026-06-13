using System;

[AttributeUsage(AttributeTargets.Field)]
public sealed class RuntimeCatalogKeyAttribute : Attribute
{
    public RuntimeCatalogKeyAttribute(int key)
    {
        Key = key;
    }

    public int Key { get; }
}
