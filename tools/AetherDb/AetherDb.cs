using System;
using System.IO;
using GameCult.Caching;

// Opens GameData/Aetheria.cc through AetheriaStores, the game's own store composition. The catalog attaches read-only
// unless a command writes it, so a command that only reads cannot write. A station hull with no kind does not lock the
// catalog: it is named to warn (stderr by default) and the commands that repair it stay reachable.
public sealed class AetherDb : IDisposable
{
    public string Root { get; }
    public CultCache Cache { get; }

    public void Dispose() => Cache.Dispose();

    private AetherDb(string root, bool catalogWritable, bool withRun, Action<string> warn)
    {
        Root = root;
        var gameData = Path.Combine(root, "GameData");
        Cache = AetheriaStores.Open(
            Path.Combine(gameData, "Aetheria.cc"),
            runPath: withRun ? Path.Combine(gameData, "run.cc") : null,
            catalogWritable: catalogWritable,
            kindlessStation: warn ?? Console.Error.WriteLine);
    }

    // With no root given, walks up from the working directory to the game folder: the first folder whose GameData holds Aetheria.cc
    public static AetherDb Open(bool catalogWritable = false, bool withRun = false, string root = null, Action<string> warn = null) =>
        new AetherDb(root ?? FindRoot(), catalogWritable, withRun, warn);

    public static string FindRoot()
    {
        for (var dir = new DirectoryInfo(Environment.CurrentDirectory); dir != null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "GameData", "Aetheria.cc")))
                return dir.FullName;
        throw new DirectoryNotFoundException("Run from a folder whose GameData holds Aetheria.cc.");
    }
}
