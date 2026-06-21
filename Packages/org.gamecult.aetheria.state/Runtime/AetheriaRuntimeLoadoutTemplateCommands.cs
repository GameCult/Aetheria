namespace GameCult.Aetheria.State.Unity
{
    public static class AetheriaRuntimeLoadoutTemplateCommands
    {
        public const string SurfaceId = "aetheria.loadout_templates";
        public const string Save = "aetheria.loadout_templates.save";

        public static bool IsKnown(string command)
        {
            return command == Save;
        }
    }
}
