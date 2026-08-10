using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace JsonKnownTypes
{
    public class JsonKnownTypesConverter<T> : JsonConverter
    {
        private readonly JsonKnownTypesSettings _typesSettings 
            = JsonKnownTypesSettingsManager.GetSettings<T>();

        private JsonSerializerSettings SpecifiedSubclassConversion
        {
            get =>
                new JsonSerializerSettings
                {
                    ContractResolver = new JsonKnownTypesContractResolver<T>()
                };
        }
        
        public override bool CanConvert(Type objectType)
            => _typesSettings.TypeToDiscriminator.ContainsKey(objectType);

        public override bool CanWrite { get => true; }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null) return null;
            
            var jo = JObject.Load(reader);

            var discriminator = jo[_typesSettings.Name].ToString();

            if(_typesSettings.DiscriminatorToType.TryGetValue(discriminator, out var typeForObject))
                return JsonConvert.DeserializeObject(jo.ToString(), typeForObject, SpecifiedSubclassConversion);

            return null;
            //throw new NotImplementedException();
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var objectType = value.GetType();
            if (_typesSettings.TypeToDiscriminator.TryGetValue(objectType, out var discriminator))
            {
                var json = JsonConvert.SerializeObject(value, SpecifiedSubclassConversion);
                var jo = JObject.Parse(json);
                jo.Add(_typesSettings.Name, new JValue(discriminator));
                jo.WriteTo(writer);
            }
            else
            {
                throw new ArgumentException($"Json Known Types: Type Unknown {objectType.FullName}!");
            }
        }
    }
}
