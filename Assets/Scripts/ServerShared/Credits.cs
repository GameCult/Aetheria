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
    public const string LiberationFile = "Assets/TextMesh Pro/Fonts/LiberationSans - OFL.txt";
    // Fonts state their holder and licence in their own name tables; these are the font files the credit answers for.
    public const string UbuntuFontFile = "Assets/Fonts/Ubuntu/Ubuntu-R.ttf";
    public const string MontserratFontFile = "Assets/Fonts/Ubuntu/Montserrat-Thin.ttf";
    public const string UniRxFile = "Assets/Plugins/UniRx/ReadMe.txt";
    public const string AmbientCgFile = "Assets/Materials/PBR/SOURCES.md";
    public const string KdTreeFile = "Assets/Scripts/ServerShared/NIH/KDTree/KDTree.cs";
    public const string SimplexNoiseFile = "Assets/Plugins/GPU Noise/SimplexNoise2D.cginc";
    // Works whose notice is only a source-file header; each answers through the file whose header states it.
    public const string UnityShaderFile = "Assets/Shaders/Particle Standard Unlit.shader";
    public const string AdditiveUiFile = "Assets/Shaders/AdditiveUI.shader";
    public const string AcesFile = "Assets/Shaders/CustomDoF/ACES.hlsl";
    public const string ClassicNoiseFile = "Assets/Plugins/Celestial Body/Scripts/Shaders/Includes/SimplexNoise2.cginc";
    // The first line of the attribution file; the icon credits are the lines after it.
    public const string NounProjectHeading = "Noun Project";
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
                Name = "Liberation Sans",
                Licence = "SIL Open Font License 1.1",
                Notice = "Digitized data copyright (c) 2010 Google Corporation with Reserved Font Arimo, Tinos and Cousine. " +
                         "Copyright (c) 2012 Red Hat, Inc. with Reserved Font Name Liberation.",
                LicenceFile = LiberationFile
            },
            new Credit
            {
                Name = "Ubuntu and Ubuntu Mono fonts",
                Licence = "Ubuntu Font Licence 1.0",
                Notice = "Copyright 2011 Canonical Ltd.",
                LicenceFile = UbuntuFontFile
            },
            new Credit
            {
                Name = "Montserrat",
                Licence = "SIL Open Font License 1.1",
                Notice = "Copyright(c) 2014 by Julieta Ulanovsky",
                LicenceFile = MontserratFontFile
            },
            new Credit
            {
                Name = "UniRx",
                Licence = "MIT",
                Notice = "Created by Yoshifumi Kawai (neuecc)",
                LicenceFile = UniRxFile
            },
            new Credit
            {
                Name = "KDTree",
                Licence = "MIT",
                Notice = "Copyright (c) 2018 Vili Volčini / viliwonka",
                LicenceFile = KdTreeFile
            },
            new Credit
            {
                Name = "webgl-noise simplex noise",
                Licence = "MIT",
                Notice = "Copyright (C) 2011 Ashima Arts. Author: Ian McEwan, Ashima Arts " +
                         "(https://github.com/ashima/webgl-noise). Translated to Unity shaders and modified by Keijiro Takahashi " +
                         "(https://github.com/keijiro/NoiseShader).",
                LicenceFile = SimplexNoiseFile
            },
            new Credit
            {
                Name = "Celestial Body noise shaders",
                Licence = "MIT",
                Notice = "Copyright (C) 2011 Ashima Arts. Copyright (C) 2011 by Ashima Arts (Simplex noise). " +
                         "Copyright (C) 2011 by Stefan Gustavson (Classic noise).",
                LicenceFile = ClassicNoiseFile
            },
            new Credit
            {
                Name = "Unity built-in shaders",
                Licence = "MIT",
                Notice = "Copyright (c) 2016 Unity Technologies. Shader source adapted from Unity's built-in shaders " +
                         "(Particle Standard Unlit, StretchedSkybox, MaskedWorldSpaceUI, MaskedWorldSpaceAdditiveUI).",
                LicenceFile = UnityShaderFile
            },
            new Credit
            {
                Name = "AdditiveUI shaders",
                Licence = "MIT for the Unity Technologies portion; the Supyrb portion states no licence",
                Notice = "Copyright (c) 2017 Supyrb. All rights reserved. Author: Johannes Deml. " +
                         "Built on Unity's built-in UI shader, copyright (c) 2016 Unity Technologies, MIT.",
                LicenceFile = AdditiveUiFile
            },
            new Credit
            {
                Name = "ACES colour transforms",
                Licence = "Academy Color Encoding System licence",
                Notice = "Copyright 2015 Academy of Motion Picture Arts and Sciences (A.M.P.A.S.). " +
                         "Portions contributed by others as indicated. All rights reserved. " +
                         "Source: https://github.com/ampas/aces-dev",
                LicenceFile = AcesFile
            },
            new Credit
            {
                Name = "ambientCG PBR materials",
                Licence = "CC0 1.0 Universal",
                Notice = "Texture sets from ambientCG (https://ambientcg.com), public domain",
                LicenceFile = AmbientCgFile
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

    // The icon credit lines of the attribution file: everything after its "Noun Project" heading, one line per icon (the
    // file's "Other" sub-heading and its lines follow in the same list). A file that does not open with the heading is
    // refused rather than having its first line dropped.
    public static IEnumerable<string> NounProjectLines(string attribution)
    {
        var lines = (attribution ?? "").Split('\n').Select(line => line.Trim()).Where(line => line.Length > 0).ToArray();
        if (lines.Length < 2 || lines[0] != NounProjectHeading)
            throw new InvalidOperationException($"{NounProjectFile} must open with the heading '{NounProjectHeading}' followed by icon credits");
        return lines.Skip(1);
    }

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
