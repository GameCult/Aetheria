using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Unity.Mathematics;
using static Unity.Mathematics.math;

public class MathJsonConverter : JsonConverter
{
    private readonly Dictionary<Type, Action<JsonWriter, object>> _writers;
    private readonly Dictionary<Type, Func<JArray, object>> _readers;

    public MathJsonConverter()
    {
        _writers = GenerateWriters();
        _readers = GenerateReaders();
    }

    private Dictionary<Type, Action<JsonWriter, object>> GenerateWriters()
    {
        var writers = new Dictionary<Type, Action<JsonWriter, object>>();

        writers[typeof(float2)] = (writer, o) =>
        {
            if (!(o is float2 v))
                throw new JsonReaderException();

            writer.WriteStartArray();
            writer.WriteValue(v.x);
            writer.WriteValue(v.y);
            writer.WriteEndArray();
        };

        writers[typeof(int2)] = (writer, o) =>
        {
            if (!(o is int2 v))
                throw new JsonReaderException();

            writer.WriteStartArray();
            writer.WriteValue(v.x);
            writer.WriteValue(v.y);
            writer.WriteEndArray();
        };

        writers[typeof(float3)] = (writer, o) =>
        {
            if (!(o is float3 v))
                throw new JsonReaderException();

            writer.WriteStartArray();
            writer.WriteValue(v.x);
            writer.WriteValue(v.y);
            writer.WriteValue(v.z);
            writer.WriteEndArray();
        };

        writers[typeof(float4)] = (writer, o) =>
        {
            if (!(o is float4 v))
                throw new JsonReaderException();

            writer.WriteStartArray();
            writer.WriteValue(v.x);
            writer.WriteValue(v.y);
            writer.WriteValue(v.z);
            writer.WriteValue(v.w);
            writer.WriteEndArray();
        };

        return writers;
    }

    private Dictionary<Type, Func<JArray, object>> GenerateReaders()
    {
        var readers = new Dictionary<Type, Func<JArray, object>>();

        readers[typeof(float2)] = array =>
        {
            if (array.Count < 2)
            {
                throw new JsonReaderException(
                    $"Could not read {typeof(float2)} from json, expected a json array with 2 elements");
            }

            return float2(
                array[0].ToObject<float>(),
                array[1].ToObject<float>());
        };
        
        readers[typeof(int2)] = array =>
        {
            if (array.Count < 2)
            {
                throw new JsonReaderException(
                    $"Could not read {typeof(float2)} from json, expected a json array with 2 elements");
            }

            return int2(
                array[0].ToObject<int>(),
                array[1].ToObject<int>());
        };

        readers[typeof(float3)] = array =>
        {
            if (array.Count < 3)
            {
                throw new JsonReaderException(
                    $"Could not read {typeof(float3)} from json, expected a json array with 3 elements");
            }

            return float3(
                array[0].ToObject<float>(),
                array[1].ToObject<float>(),
                array[2].ToObject<float>());
        };

        readers[typeof(float4)] = array =>
        {
            if (array.Count < 4)
            {
                throw new JsonReaderException(
                    $"Could not read {typeof(float4)} from json, expected a json array with 4 elements");
            }

            return float4(
                array[0].ToObject<float>(),
                array[1].ToObject<float>(),
                array[2].ToObject<float>(),
                array[3].ToObject<float>());
        };

        return readers;
    }
    
    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
    {
        if (_writers.TryGetValue(value.GetType(), out var serializeAction))
            serializeAction(writer, value);
        else
            throw new JsonReaderException($"Invalid type for ValueConverter:{value.GetType()}");
    }

    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
    {
        if (_readers.TryGetValue(objectType, out var deserializeFunc))
        {
            // Convert token from reader to array for deserialize function
            var token = JToken.ReadFrom(reader);
            if (!(token is JArray asArray))
                throw new JsonReaderException(
                    $"Could not read {objectType} from json, expected a json array but got {token.Type}");

            return deserializeFunc(asArray);
        }

        throw new JsonReaderException($"Invalid type for ValueConverter:{objectType}");
    }

    public override bool CanConvert(Type objectType)
    {
        //If the dictionary contains the key, the type can be handled by this class
        return _writers.ContainsKey(objectType);
    }

    //Enable this JSON Converter for reading as well as writing values
    public override bool CanRead => true;
    public override bool CanWrite => true;
}
