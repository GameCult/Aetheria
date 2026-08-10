using System;

namespace JsonKnownTypes
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = true)]
    public class JsonKnownTypeAttribute : Attribute
    {
        public Type Type { get; }
        public string Discriminator { get; }

        public JsonKnownTypeAttribute(Type type)
        {
            Type = type;
        }

        public JsonKnownTypeAttribute(Type type, string discriminator)
        {
            Type = type;
            Discriminator = discriminator;
        }
    }
}
