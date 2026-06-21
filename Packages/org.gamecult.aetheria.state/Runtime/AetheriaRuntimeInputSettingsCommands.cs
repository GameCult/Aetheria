namespace GameCult.Aetheria.State.Verse
{
    public static class AetheriaRuntimeInputSettingsCommands
    {
        public const string SurfaceId = "aetheria.input_settings";
        public const string Refresh = "aetheria.input_settings.refresh";
        public const string BeginCapture = "aetheria.input_settings.binding.capture";
        public const string CancelCapture = "aetheria.input_settings.binding.cancel";
        public const string SetBindingOverride = "aetheria.input_settings.binding.override";
        public const string ToggleActionBar = "aetheria.input_settings.action_bar.toggle";
        public const string SetActionBarEnabled = "aetheria.input_settings.action_bar.set_enabled";

        public static bool IsKnown(string command)
        {
            return command == Refresh ||
                command == BeginCapture ||
                command == CancelCapture ||
                command == SetBindingOverride ||
                command == ToggleActionBar ||
                command == SetActionBarEnabled;
        }
    }
}
