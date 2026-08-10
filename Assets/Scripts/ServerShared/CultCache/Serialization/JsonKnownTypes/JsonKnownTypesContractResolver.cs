using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace JsonKnownTypes
{
    public class JsonKnownTypesContractResolver<T> : DefaultContractResolver
    {
        protected override JsonConverter ResolveContractConverter(Type objectType)
        {
            if (typeof(T).IsAssignableFrom(objectType) && !objectType.IsAbstract)
                return null;
            return base.ResolveContractConverter(objectType);
        }
    }
}
