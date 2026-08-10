using System;

namespace JsonKnownTypes.Exceptions
{
    public class JsonKnownTypesException : Exception
    {
        public JsonKnownTypesException(string message) 
            : base(message)
        { }

        public JsonKnownTypesException(string message, Exception innerException) 
            : base(message, innerException)
        { }
    }
}
