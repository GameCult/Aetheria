using System;
using System.IO;
using GameCult.Caching;

// Opens GameData/Aetheria.cc through AetheriaStores, the game's own store composition. The catalog attaches read-only
// unless a command writes it, so a command that only reads cannot write.
public sealed class AetherDb
{
    public string Root { get; }
    public CultCache Cache { get; }

    private AetherDb(string root, bool catalogWritable, bool withRun)
    {
        Root = root;
        var gameData = Path.Combine(root, "GameData");
        Cache = AetheriaStores.Open(
            Path.Combine(gameData, "Aetheria.cc"),
            runPath: withRun ? Path.Combine(gameData, "run.cc") : null,
            catalogWritable: catalogWritable);
    }

    // With no root given, walks up from the working directory to the repository
    public static AetherDb Open(bool catalogWritable = false, bool withRun = false, string root = null) =>
        new AetherDb(root ?? FindRoot(), catalogWritable, withRun);

    public static string FindRoot()
    {
        for (var dir = new DirectoryInfo(Environment.CurrentDirectory); dir != null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "Aetheria.Shared", "Aetheria.Shared.csproj")))
                return dir.FullName;
        throw new DirectoryNotFoundException("Run from inside the Aetheria repository.");
    }
}
