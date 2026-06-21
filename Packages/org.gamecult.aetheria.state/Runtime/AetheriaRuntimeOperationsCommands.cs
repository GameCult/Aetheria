namespace GameCult.Aetheria.State.Verse
{
    public static class AetheriaRuntimeOperationsCommands
    {
        public const string SurfaceId = "aetheria.operations";
        public const string Refresh = "aetheria.operations.refresh";

        public static bool IsKnown(string command)
        {
            return command == Refresh;
        }
    }
}
