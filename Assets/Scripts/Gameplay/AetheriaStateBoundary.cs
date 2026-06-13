using System.IO;

public static class AetheriaStateBoundary
{
    public const string RuntimeStateFileName = "aetheria-world.cc";

    public static string GetStateFilePath(DirectoryInfo gameDataDirectory)
    {
        return Path.Combine(gameDataDirectory.FullName, RuntimeStateFileName);
    }
}
