using System.IO;

namespace GameCult.Aetheria.State.Unity
{
    public static class AetheriaRuntimeStateBoundary
    {
        public const string RuntimeStateFileName = "aetheria-world.cc";

        public static string GetStateFilePath(DirectoryInfo gameDataDirectory)
        {
            return Path.Combine(gameDataDirectory.FullName, RuntimeStateFileName);
        }
    }
}
