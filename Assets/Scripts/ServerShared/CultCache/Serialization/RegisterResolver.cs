using System.Collections;
using System.Collections.Generic;
using MessagePack;
using MessagePack.ReactivePropertyExtension;
using MessagePack.Resolvers;
using Newtonsoft.Json;
using RethinkDb.Driver.Net;

public static class RegisterResolver
{
    private static bool registered = false;
    public static void Register()
    {
        if (registered) return;
        
        // Set extensions to default resolver.
        var resolver = CompositeResolver.Create(
            MathResolver.Instance,
            TypeResolver.Instance,
            NativeGuidResolver.Instance,
            StandardResolver.Instance,
            ReactivePropertyResolver.Instance
        );
        var options = MessagePackSerializerOptions.Standard.WithResolver(resolver);
        MessagePackSerializer.DefaultOptions = options;

        // Add Unity.Mathematics serialization support to Newtonsoft JSON
        JsonConvert.DefaultSettings = () => new JsonSerializerSettings
        {
            Converters = new List<JsonConverter>
            {
                new MathJsonConverter(),
                Converter.DateTimeConverter,
                Converter.BinaryConverter,
                Converter.GroupingConverter,
                Converter.PocoExprConverter
            }
        };

        registered = true;
    }
}
