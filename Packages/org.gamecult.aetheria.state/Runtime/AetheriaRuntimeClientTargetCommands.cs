namespace GameCult.Aetheria.State.Unity
{
    public static class AetheriaRuntimeClientTargetCommands
    {
        public const string SurfaceId = "aetheria.client_target";
        public const string Refresh = "aetheria.client_target.refresh";
        public const string CycleTargetKind = "aetheria.client_target.kind.cycle";
        public const string SetTitle = "aetheria.client_target.title.set";
        public const string SetVerseId = "aetheria.client_target.verse_id.set";
        public const string SetCultMeshAddress = "aetheria.client_target.cultmesh_address.set";
        public const string SetStateFilePath = "aetheria.client_target.state_file_path.set";

        public static bool IsKnown(string command)
        {
            return command == Refresh ||
                command == CycleTargetKind ||
                command == SetTitle ||
                command == SetVerseId ||
                command == SetCultMeshAddress ||
                command == SetStateFilePath;
        }
    }
}
