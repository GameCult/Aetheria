namespace JsonKnownTypes.Utils
{
    internal static class Mapper
    {
        public static JsonDiscriminatorSettings Map(JsonDiscriminatorAttribute entity)
        {
            var settings = new JsonDiscriminatorSettings();

            settings.Name = entity.Name ?? settings.Name;
            settings.AutoJsonKnown = entity._autoJson ?? settings.AutoJsonKnown;
            
            return settings;
        }
    }
}
