using System;

namespace JsonKnownTypes
{
    public static class AttributesManager
    {
        public static JsonKnownTypeAttribute[] GetJsonKnownAttributes(Type type) =>
            (JsonKnownTypeAttribute[])Attribute.GetCustomAttributes(type, typeof(JsonKnownTypeAttribute));

        public static JsonKnownThisTypeAttribute GetJsonKnownThisAttribute(Type type) =>
            (JsonKnownThisTypeAttribute)Attribute.GetCustomAttribute(type, typeof(JsonKnownThisTypeAttribute));

        public static JsonDiscriminatorAttribute GetJsonDiscriminatorAttribute(Type type) =>
            (JsonDiscriminatorAttribute)Attribute.GetCustomAttribute(type, typeof(JsonDiscriminatorAttribute));
    }
}
