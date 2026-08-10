using System;
using System.Collections;
using System.Collections.Generic;
using MessagePack;
using MessagePack.Formatters;
using Unity.Mathematics;
using static Unity.Mathematics.math;


public class MathResolver : IFormatterResolver
{
    public static readonly MathResolver Instance = new MathResolver();

    private MathResolver()
    {
    }

    public IMessagePackFormatter<T> GetFormatter<T>()
    {
        return FormatterCache<T>.Formatter;
    }

    private static class FormatterCache<T>
    {
        public static readonly IMessagePackFormatter<T> Formatter;

        static FormatterCache()
        {
            Formatter = (IMessagePackFormatter<T>)MathResolverResolverGetFormatterHelper.GetFormatter(typeof(T));
        }
    }
}

internal static class MathResolverResolverGetFormatterHelper
{
    private static readonly Dictionary<Type, object> FormatterMap = new Dictionary<Type, object>()
    {
        // standard
        { typeof(float2), new Float2Formatter() },
        { typeof(int2), new Int2Formatter() },
        { typeof(bool2), new Bool2Formatter() },
        { typeof(float3), new Float3Formatter() },
        { typeof(float4), new Float4Formatter() },
        { typeof(float2?), new StaticNullableFormatter<float2>(new Float2Formatter()) },
        { typeof(int2?), new StaticNullableFormatter<int2>(new Int2Formatter()) },
        { typeof(float3?), new StaticNullableFormatter<float3>(new Float3Formatter()) },
        { typeof(float4?), new StaticNullableFormatter<float4>(new Float4Formatter()) },

        // standard + array
        { typeof(float2[]), new ArrayFormatter<float2>() },
        { typeof(int2[]), new ArrayFormatter<int2>() },
        { typeof(float3[]), new ArrayFormatter<float3>() },
        { typeof(float4[]), new ArrayFormatter<float4>() },
        { typeof(float2?[]), new ArrayFormatter<float2?>() },
        { typeof(int2?[]), new ArrayFormatter<float2?>() },
        { typeof(float3?[]), new ArrayFormatter<float3?>() },
        { typeof(float4?[]), new ArrayFormatter<float4?>() },

        // standard + list
        { typeof(List<float2>), new ListFormatter<float2>() },
        { typeof(List<int2>), new ListFormatter<int2>() },
        { typeof(List<float3>), new ListFormatter<float3>() },
        { typeof(List<float4>), new ListFormatter<float4>() },
        { typeof(List<float2?>), new ListFormatter<float2?>() },
        { typeof(List<int2?>), new ListFormatter<int2?>() },
        { typeof(List<float3?>), new ListFormatter<float3?>() },
        { typeof(List<float4?>), new ListFormatter<float4?>() },
    };

    internal static object GetFormatter(Type t)
    {
        object formatter;
        if (FormatterMap.TryGetValue(t, out formatter))
        {
            return formatter;
        }

        return null;
    }
}
