using System;
using System.IO;

// What a player directory holds beside the executable's data: GameData/Aetheria.cc, GameData/Narrative/** and, when
// the repo has one, GameData/Mods/**. Nothing else under GameData ships (run.cc and player.cc are the player's own;
// SoundbanksInfo.json is read by no code). Unity-free so the headless suite can prove the rules; AetheriaBuild is the
// only caller.
internal static class PlayerStaging
{
    public const string Catalog = "Aetheria.cc";
    public const string Narrative = "Narrative";
    public const string Mods = "Mods";

    // Replaces <playerDir>/GameData with a copy of the three shipped entries of <repoRoot>/GameData.
    // Throws InvalidOperationException naming the entry when the catalog or Narrative is absent.
    public static void Stage(string repoRoot, string playerDir)
    {
        var source = Path.Combine(repoRoot, "GameData");
        var destination = Path.Combine(playerDir, "GameData");
        var catalog = Path.Combine(source, Catalog);
        var narrative = Path.Combine(source, Narrative);
        var mods = Path.Combine(source, Mods);

        if (!File.Exists(catalog)) throw new InvalidOperationException($"GameData/{Catalog} is missing; a player never ships without its catalog.");
        if (!Directory.Exists(narrative)) throw new InvalidOperationException($"GameData/{Narrative} is missing.");

        if (Directory.Exists(destination)) Directory.Delete(destination, true);
        Directory.CreateDirectory(destination);
        File.Copy(catalog, Path.Combine(destination, Catalog));
        CopyTree(narrative, Path.Combine(destination, Narrative));
        if (Directory.Exists(mods)) CopyTree(mods, Path.Combine(destination, Mods));
    }

    private static void CopyTree(string from, string to)
    {
        Directory.CreateDirectory(to);
        foreach (var directory in Directory.GetDirectories(from, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(Path.Combine(to, directory.Substring(from.Length + 1)));
        foreach (var file in Directory.GetFiles(from, "*", SearchOption.AllDirectories))
            File.Copy(file, Path.Combine(to, file.Substring(from.Length + 1)));
    }
}
