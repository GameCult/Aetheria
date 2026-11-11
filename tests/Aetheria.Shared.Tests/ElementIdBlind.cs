using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using GameCult.Caching;
using GameCult.Caching.MessagePack;
using MessagePack;

// Element ids are minted at random when a record is written, so two writes of the same content differ in exactly those slots.
// A test that asks "is this the same content" compares the bytes with every element id blank.
public static class ElementIdBlind
{
    private static readonly MessagePackSerializerOptions Options = CultDocumentMessagePackSerialization.OptionsFor(typeof(ItemData).Assembly);

    // The document's bytes with every [CultElementId] member blank, on a copy: the document is untouched.
    public static byte[] Pack(object document, Type type = null)
    {
        type ??= document.GetType();
        var copy = MessagePackSerializer.Deserialize(type, MessagePackSerializer.Serialize(type, document, Options), Options);
        Blank(copy);
        return MessagePackSerializer.Serialize(type, copy, Options);
    }

    // A stored payload of the schema, re-encoded with every element id blank.
    public static byte[] Payload(CultDocumentRegistry registry, string schemaId, byte[] payload)
    {
        var type = registry.GetRequiredBySchemaId(schemaId).DocumentType;
        var value = MessagePackSerializer.Deserialize(type, payload, Options);
        Blank(value);
        return MessagePackSerializer.Serialize(type, value, Options);
    }

    private static void Blank(object value)
    {
        if (value == null || value is string || value.GetType().IsPrimitive || value.GetType().IsEnum) return;
        if (value is IEnumerable items && !(value is string))
        {
            foreach (var item in items) Blank(item);
            return;
        }
        for (var type = value.GetType(); type != null && type != typeof(object); type = type.BaseType)
            foreach (var member in type.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                if (member is FieldInfo field)
                {
                    if (field.GetCustomAttribute<CultElementIdAttribute>() != null) field.SetValue(value, "");
                    else if (!field.FieldType.IsPrimitive && field.GetCustomAttribute<IgnoreMemberAttribute>() == null && field.GetCustomAttribute<KeyAttribute>() != null)
                        Blank(field.GetValue(value));
                }
                else if (member is PropertyInfo property && property.GetIndexParameters().Length == 0 && property.GetCustomAttribute<KeyAttribute>() != null)
                {
                    if (property.GetCustomAttribute<CultElementIdAttribute>() != null) property.SetValue(value, "");
                    else Blank(property.GetValue(value));
                }
            }
    }
}
