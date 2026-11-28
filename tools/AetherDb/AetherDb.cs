using System;
using System.IO;
using System.Linq;
using GameCult.Caching;

// Opens the catalog through AetheriaStores, the game's own store composition: OpenBooted for a read tool (the catalog the game boots),
// Open for the shipped GameData/Aetheria.cc alone, which attaches read-only unless a command writes it, so a command that only reads cannot write. A station hull with no kind does not lock the
// catalog: it is named to warn (stderr by default) and the commands that repair it stay reachable.
public sealed class AetherDb : IDisposable
{
    public string Root { get; }
    public CultCache Cache { get; }

    // The compose directory withMods made, owned by this instance: the derived catalog is read from it until Dispose.
    private readonly string _composed;

    public void Dispose()
    {
        Cache.Dispose();
        if (_composed != null) try { Directory.Delete(_composed, true); } catch (IOException) { }
    }

    private AetherDb(string root, bool booted, bool withRun, Action<string> warn, bool catalogWritable)
    {
        Root = root;
        var gameData = Path.Combine(root, "GameData");
        var catalog = Path.Combine(gameData, "Aetheria.cc");
        if (booted)
        {
            // Derived and read-only, so a write through it would land in a temp file, never in Aetheria.cc: a write opens the shipped file alone.
            _composed = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "aetherdb-mods-" + Guid.NewGuid().ToString("N"))).FullName;
            try { catalog = ComposeBooted(root, _composed); }
            catch { try { Directory.Delete(_composed, true); } catch (IOException) { } throw; }
        }
        try
        {
            Cache = AetheriaStores.Open(
                catalog,
                runPath: withRun ? Path.Combine(gameData, "run.cc") : null,
                catalogWritable: catalogWritable,
                kindlessStation: warn ?? Console.Error.WriteLine);
        }
        catch { if (_composed != null) try { Directory.Delete(_composed, true); } catch (IOException) { } throw; }
    }

    // The one owner of "the catalog the game boots": the first-party packages in GameData/Mods composed (ShipModCatalog.ResolveCatalog)
    // over a scratch copy of GameData/Aetheria.cc, inside the caller's scratch directory, which the caller owns and removes. Returns
    // the path of the composed catalog to open read-only; the shipped file is never written. The game skips a broken package so a
    // player still boots; a tool reading the catalog does not, so a package left out throws, naming it.
    public static string ComposeBooted(string root, string scratch)
    {
        var copy = Path.Combine(scratch, "Aetheria.cc");
        File.Copy(Path.Combine(root, "GameData", "Aetheria.cc"), copy);
        new FileInfo(copy).IsReadOnly = false;
        var (composed, excluded) = ShipModCatalog.ResolveCatalog(copy, Path.Combine(scratch, "derived.cc"), Path.Combine(root, "GameData", "Mods"));
        if (excluded.Length > 0)
            throw new InvalidOperationException("First-party packages were excluded from the catalog: " +
                string.Join("; ", excluded.Select(entry => entry.Package + ": " + entry.Reason)));
        return composed;
    }

    // The shipped GameData/Aetheria.cc alone, which is what an apply path (and its dry run) reads and writes. With no root given, walks up
    // from the working directory to the game folder: the first folder whose GameData holds Aetheria.cc.
    public static AetherDb Open(bool catalogWritable = false, bool withRun = false, string root = null, Action<string> warn = null) =>
        new AetherDb(root ?? FindRoot(), false, withRun, warn, catalogWritable);

    // The catalog the game boots (see ComposeBooted), read-only: every read tool opens this.
    public static AetherDb OpenBooted(bool withRun = false, string root = null, Action<string> warn = null) =>
        new AetherDb(root ?? FindRoot(), true, withRun, warn, false);

    public static string FindRoot()
    {
        for (var dir = new DirectoryInfo(Environment.CurrentDirectory); dir != null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "GameData", "Aetheria.cc")))
                return dir.FullName;
        throw new DirectoryNotFoundException("Run from a folder whose GameData holds Aetheria.cc.");
    }
}
