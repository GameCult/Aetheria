namespace GameCult.Aetheria.State.Verse
{
    public static class AetheriaRuntimeCatalogCommands
    {
        public const string SurfaceId = "aetheria.catalog.operator";
        public const string Refresh = "aetheria.catalog.refresh";

        public static bool IsKnown(string command)
        {
            return command == Refresh;
        }
    }
}
