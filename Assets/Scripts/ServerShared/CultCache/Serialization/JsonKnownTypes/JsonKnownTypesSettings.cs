using System;
using System.Collections.Generic;

namespace JsonKnownTypes
{
    public class JsonKnownTypesSettings
    {
        public string Name { get; set; }
        public Dictionary<string, Type> DiscriminatorToType { get; set; } = new Dictionary<string, Type>();
        public Dictionary<Type, string> TypeToDiscriminator { get; set; } = new Dictionary<Type, string>();
    }
}
