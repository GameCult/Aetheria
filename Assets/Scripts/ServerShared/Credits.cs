using System;
using System.Collections.Generic;
using System.Linq;

// The third-party attributions the shipped game must show the player. One list, owned here: the credits screen only
// renders it, and CreditsTests holds it against the licence files in Assets. Unity-free so the headless build checks it.
public class Credit
{
    public string Name;
    public string Licence;
    // What the licence requires shown: copyright holders, and any change notice.
    public string Notice;
    // The licence file in the repo that carries the full text and that this entry answers for.
    public string LicenceFile;
}

public static class Credits
{
    public const string TwemojiFile = "Assets/Fonts/Emoji/LICENSE.txt";
    public const string PlaydeadFile = "Assets/Plugins/Playdead/LICENSE.txt";
    public const string NounProjectFile = "Assets/Resources/Sprites/Attribution.txt";
    // Where the player build loads the Noun Project credit lines from (Resources path, no extension).
    public const string NounProjectResource = "Sprites/Attribution";

    // The full list. The Noun Project icon credits are the lines of its attribution file, whose text the caller supplies
    // (Unity reads it as a Resources TextAsset; tests read it from disk), so the file stays the one copy of those names.
    public static IReadOnlyList<Credit> All(string nounProjectAttribution)
    {
        var credits = new List<Credit>
        {
            new Credit
            {
                Name = "Twemoji",
                Licence = "CC BY 4.0",
                Notice = "Copyright (c) 2022-present Jason Sofonia & Justine De Caires; copyright (c) 2014-2021 Twitter. " +
                         "Graphics licensed under CC BY 4.0 (https://creativecommons.org/licenses/by/4.0/). " +
                         "Converted to a colour font; the artwork itself is not edited.",
                LicenceFile = TwemojiFile
            },
            new Credit
            {
                Name = "Playdead temporal anti-aliasing",
                Licence = "MIT",
                Notice = "Copyright (c) 2015 Playdead",
                LicenceFile = PlaydeadFile
            },
            new Credit
            {
                Name = "Noun Project icons",
                Licence = "Noun Project attribution licence",
                Notice = string.Join("\n", NounProjectLines(nounProjectAttribution)),
                LicenceFile = NounProjectFile
            }
        };
        Validate(credits);
        return credits;
    }

    // The icon credit lines of the attribution file: everything after its "Noun Project" heading.
    public static IEnumerable<string> NounProjectLines(string attribution) =>
        (attribution ?? "").Split('\n').Select(line => line.Trim()).Where(line => line.Length > 0).Skip(1);

    // Throws on an entry a player-facing screen could not honestly show.
    public static void Validate(IReadOnlyList<Credit> credits)
    {
        foreach (var credit in credits)
            if (string.IsNullOrWhiteSpace(credit.Name) || string.IsNullOrWhiteSpace(credit.Licence) ||
                string.IsNullOrWhiteSpace(credit.Notice) || string.IsNullOrWhiteSpace(credit.LicenceFile))
                throw new InvalidOperationException($"Credit '{credit.Name}' is missing its name, licence, notice or licence file");
        var duplicate = credits.GroupBy(credit => credit.LicenceFile).FirstOrDefault(group => group.Count() > 1);
        if (duplicate != null) throw new InvalidOperationException($"Licence file {duplicate.Key} is credited twice");
    }
}
