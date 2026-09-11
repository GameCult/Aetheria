using System;
using System.IO;

// Opens GameData/AetherDB.msgpack through the game's own CultCache and data types, for inspection and editing scripts.
// Close the editor's Database Tools before saving from a script, or its next Save will overwrite the script's changes.
public sealed class AetherDb
{
    public string Root { get; }
    public CultCache Cache { get; }
    private readonly SingleFileMessagePackBackingStore _store;

    private AetherDb(string root)
    {
        Root = root;
        RegisterResolver.Register();
        var gameData = Path.Combine(root, "GameData");
        _store = new SingleFileMessagePackBackingStore(Path.Combine(gameData, "AetherDB.msgpack"));
        Cache = new CultCache();
        Cache.AddBackingStore(_store);
        Cache.AddBackingStore(new MultiFileMessagePackBackingStore(gameData), typeof(NameFile));
        Cache.PullAllBackingStores();
    }

    // With no root given, walks up from the working directory to the repository
    public static AetherDb Open(string root = null) => new AetherDb(root ?? FindRoot());

    // Writes every entry back to AetherDB.msgpack, as the editor's Save button does
    public void Save() => _store.PushAll();

    private static string FindRoot()
    {
        for (var dir = new DirectoryInfo(Environment.CurrentDirectory); dir != null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "GameData", "AetherDB.msgpack")))
                return dir.FullName;
        throw new DirectoryNotFoundException("Run from inside the Aetheria repository.");
    }
}
