using System;
using System.IO;
using System.Linq;
using GameCult.Caching;

// Opens GameData/Aetheria.cc through AetheriaStores, the game's own store composition. The catalog attaches read-only
// unless a command writes it, so a command that only reads cannot write. A station hull with no kind does not lock the
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

    private AetherDb(string root, bool catalogWritable, bool withRun, bool withMods, Action<string> warn)
    {
        Root = root;
        var gameData = Path.Combine(root, "GameData");
        var catalog = Path.Combine(gameData, "Aetheria.cc");
        if (withMods)
        {
            // The catalog the game boots: ShipModCatalog.ResolveCatalog over GameData/Mods. It is derived and read-only, so a write
            // through it would land in a temp file, never in Aetheria.cc; the one writable open stays the shipped file alone.
            if (catalogWritable) throw new ArgumentException("A catalog composed with packages is read-only; a write opens the shipped file alone.", nameof(withMods));
            _composed = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "aetherdb-mods-" + Guid.NewGuid().ToString("N"))).FullName;
            try
            {
                var (resolved, excluded) = ShipModCatalog.ResolveCatalog(catalog, Path.Combine(_composed, "derived.cc"), Path.Combine(gameData, "Mods"));
                // The game skips a broken package so a player still boots; a census of the shipped game does not.
                if (excluded.Length > 0)
                    throw new InvalidOperationException("First-party packages were excluded from the catalog: " +
                        string.Join("; ", excluded.Select(entry => entry.Package + ": " + entry.Reason)));
                catalog = resolved;
            }
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

    // With no root given, walks up from the working directory to the game folder: the first folder whose GameData holds Aetheria.cc
    // withMods composes the packages in GameData/Mods as the game boots them, read-only (see the constructor).
    public static AetherDb Open(bool catalogWritable = false, bool withRun = false, string root = null, Action<string> warn = null, bool withMods = false) =>
        new AetherDb(root ?? FindRoot(), catalogWritable, withRun, withMods, warn);

    public static string FindRoot()
    {
        for (var dir = new DirectoryInfo(Environment.CurrentDirectory); dir != null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "GameData", "Aetheria.cc")))
                return dir.FullName;
        throw new DirectoryNotFoundException("Run from a folder whose GameData holds Aetheria.cc.");
    }
}
