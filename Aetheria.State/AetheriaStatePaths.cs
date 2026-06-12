using System.IO;

namespace Aetheria.State;

public static class AetheriaStatePaths
{
    public const string DefaultStateFileName = "aetheria-world.cc";

    public static string ResolveDefaultStatePath(string? projectRoot = null)
    {
        var root = string.IsNullOrWhiteSpace(projectRoot)
            ? Directory.GetCurrentDirectory()
            : projectRoot!;
        return Path.Combine(root, "GameData", DefaultStateFileName);
    }
}
