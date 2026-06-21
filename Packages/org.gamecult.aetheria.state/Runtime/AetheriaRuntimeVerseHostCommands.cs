namespace GameCult.Aetheria.State.Verse
{
    public static class AetheriaRuntimeVerseHostCommands
    {
        public const string SurfaceId = "aetheria.verse_host_settings";
        public const string Refresh = "aetheria.verse_host_settings.refresh";
        public const string CycleVisibility = "aetheria.verse_host_settings.visibility.cycle";

        public static bool IsKnown(string command)
        {
            return command == Refresh ||
                command == CycleVisibility;
        }
    }
}
