using System.IO;

namespace GameCult.Aetheria.State.Unity
{
    public readonly struct AetheriaRuntimeStateBootReport
    {
        public AetheriaRuntimeStateBootReport(string stateFilePath, bool stateFileExists)
        {
            StateFilePath = stateFilePath;
            StateFileExists = stateFileExists;
        }

        public string StateFilePath { get; }

        public bool StateFileExists { get; }
    }

    public static class AetheriaRuntimeStateBoot
    {
        public static AetheriaRuntimeStateBootReport Inspect(DirectoryInfo gameDataDirectory)
        {
            var stateFilePath = AetheriaRuntimeStateBoundary.GetStateFilePath(gameDataDirectory);
            return new AetheriaRuntimeStateBootReport(
                stateFilePath,
                File.Exists(stateFilePath));
        }
    }
}
