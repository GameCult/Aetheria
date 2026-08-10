using System;

namespace JsonKnownTypes
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface)]
    public class JsonDiscriminatorAttribute : Attribute
    {
        public string Name { get; set; }

        internal bool? _autoJson;

        public bool AutoJson
        {
            get => _autoJson != null && (bool) _autoJson;
            set => _autoJson = value;
        }
    }
}
