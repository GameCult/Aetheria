using System;
using System.Collections;
using System.Collections.Generic;
using MessagePack;
using MessagePack.Formatters;

public class TypeFormatter : MessagePack.Formatters.IMessagePackFormatter<Type>
{
    public static readonly IMessagePackFormatter<Type> Instance = new TypeFormatter();

    private TypeFormatter()
    {
    }
    
    public void Serialize(ref MessagePackWriter writer, Type value, MessagePackSerializerOptions options)
    {
        writer.Write(value == null ? "" : value.AssemblyQualifiedName);
    }

    public Type Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        if (reader.IsNil)
        {
            return null; //throw new InvalidOperationException("typecode is null, struct not supported");
        }

        var assemblyQualifiedName = reader.ReadString();
        if (string.IsNullOrEmpty(assemblyQualifiedName)) return null;
        
        return Type.GetType(assemblyQualifiedName);
    }
}

public class TypeResolver : IFormatterResolver
{
    public static readonly TypeResolver Instance = new TypeResolver();

    private TypeResolver()
    {
    }

    public IMessagePackFormatter<T> GetFormatter<T>()
    {
        return FormatterCache<T>.Formatter;
    }

    private static object GetFormatterHelper(Type t)
    {
        if (t == typeof(Type))
        {
            return TypeFormatter.Instance;
        }

        return null;
    }

    private static class FormatterCache<T>
    {
        public static readonly IMessagePackFormatter<T> Formatter;

        static FormatterCache()
        {
            Formatter = (IMessagePackFormatter<T>)GetFormatterHelper(typeof(T));
        }
    }
}