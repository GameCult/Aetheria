using System;

namespace JsonKnownTypes
{
    public class JsonKnownThisTypeAttribute : Attribute
    {
        public string Discriminator { get; }

        public JsonKnownThisTypeAttribute()
        { }

        public JsonKnownThisTypeAttribute(string discriminator)
        {
            Discriminator = discriminator;
        }
    }
}
