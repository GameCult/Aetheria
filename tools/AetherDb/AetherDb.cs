using System;
using System.IO;

// Opens GameData/AetherDB.msgpack through the game's own CultCache and data types, for inspection and editing.
// Close the editor's Database Tools before saving, or its next Save will overwrite what was written here.
//
// Reads do not write. That needs saying because CultCache makes it easy to get wrong: AddBackingStore subscribes
// Add to a store's EntryAdded, MultiFileBackingStore.PullAll re-emits every entry it loads, and Add pushes to the
// store registered for that type, whose Push writes a file. So registering a second store makes merely opening the
// cache rewrite its files. Name files are therefore loaded only when a command asks for them.
public sealed class AetherDb
{
    public string Root { get; }
    public CultCache Cache { get; }
    private readonly SingleFileMessagePackBackingStore _store;

    private AetherDb(string root, bool withNameFiles)
    {
        Root = root;
        RegisterResolver.Register();
        var gameData = Path.Combine(root, "GameData");
        _store = new SingleFileMessagePackBackingStore(Path.Combine(gameData, "AetherDB.msgpack"));
        Cache = new CultCache();
        Cache.AddBackingStore(_store);
        if (withNameFiles) Cache.AddBackingStore(new MultiFileMessagePackBackingStore(gameData), typeof(NameFile));
        Cache.PullAllBackingStores();
    }

    // With no root given, walks up from the working directory to the repository
    public static AetherDb Open(string root = null) => new AetherDb(root ?? FindRoot(), false);

    // Galaxy generation needs the name files; loading them rewrites them, so only ask when they are needed,
    // and not while the editor holds them open.
    public static AetherDb OpenWithNameFiles(string root = null) => new AetherDb(root ?? FindRoot(), true);

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
