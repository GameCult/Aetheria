namespace GameCult.Aetheria.State.Unity
{
    public static class AetheriaRuntimePlayerSettingsCommands
    {
        public const string SurfaceId = "aetheria.player_settings";
        public const string Refresh = "aetheria.player_settings.refresh";
        public const string SetPlayerName = "aetheria.player_settings.player_name.set";
        public const string CycleTemperatureUnit = "aetheria.player_settings.gameplay.temperature_unit.cycle";
        public const string DecrementSignificantDigits = "aetheria.player_settings.gameplay.significant_digits.decrement";
        public const string IncrementSignificantDigits = "aetheria.player_settings.gameplay.significant_digits.increment";
        public const string CycleNebulaQuality = "aetheria.player_settings.graphics.nebula_quality.cycle";
        public const string ToggleShowAsteroidsInMinimap = "aetheria.player_settings.graphics.show_asteroids.toggle";

        public static bool IsKnown(string command)
        {
            return command == Refresh ||
                command == SetPlayerName ||
                command == CycleTemperatureUnit ||
                command == DecrementSignificantDigits ||
                command == IncrementSignificantDigits ||
                command == CycleNebulaQuality ||
                command == ToggleShowAsteroidsInMinimap;
        }
    }
}
