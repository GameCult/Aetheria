using System;
using MessagePack;
[MessagePackObject]
public class EnumDictionary<E, T> where E : Enum
{
    [Key(0)] public T[] Values;

    public EnumDictionary()
    {
        Values = new T[Enum.GetNames(typeof(E)).Length];
    }

    public T this[E key] => Values[Convert.ToInt32(key)];
}