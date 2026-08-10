using System;
using MessagePack;
using Newtonsoft.Json;

[MessagePackObject, JsonObject(MemberSerialization.OptIn)]
public class EnumDictionary<E, T> where E : Enum
{
    [JsonProperty("values"), Key(0)] public T[] Values;

    public EnumDictionary()
    {
        Values = new T[Enum.GetNames(typeof(E)).Length];
    }

    public T this[E key] => Values[Convert.ToInt32(key)];
}