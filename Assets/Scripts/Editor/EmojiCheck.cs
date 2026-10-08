using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;

// Every single-code-point emoji the pinned font set draws, and the Pirates product names, render through an
// ordinary TMP text on the game's body font with no missing code point and with ink in the emoji atlas.
// Run as -executeMethod EmojiCheck.Run (exit 1 on failure in batchmode, as EngineAssetCheck). Corpus:
// tools/emoji/emoji-singles.txt; -emojiLimit N spreads N entries evenly across it for the spike.
public static class EmojiCheck
{
    const string BodyFontPath = "Assets/Fonts/Ubuntu/Ubuntu-L Small SDF.asset";
    const int Chunk = 64;

    // The eight Pirates products (addenda "The Pirates record r2"), written as code points.
    static readonly string[] PiratesNames =
    {
        "\U0001FAF3\U0001F52B", "\U0001FAA6\U0001F920", "\U0001F9FE\U0001F6AB", "\U0001F3A8\U0001F648",
        "\U0001F525", "\U0001F440\U0001F3AF", "\U0001F511\U0001F3C3\U0001F4A8", "\U0001F4E6\U0001F92B",
    };

    [MenuItem("Aetheria/Text/Check Emoji Rendering")]
    public static void Run()
    {
        var failures = new List<string>();
        var summary = RunCheck(failures);
        foreach (var failure in failures.Take(20)) Debug.LogError(failure);
        Debug.Log($"EmojiCheck: {summary}; {failures.Count} failure(s).");
        if (Application.isBatchMode) EditorApplication.Exit(failures.Count == 0 ? 0 : 1);
    }

    static string RunCheck(List<string> failures)
    {
        var emoji = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(EmojiFont.AssetPath);
        var body = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BodyFontPath);
        if (emoji == null || body == null)
        {
            failures.Add("EmojiCheck: Emoji.asset or the body font is missing; run EmojiFont.Build first.");
            return "nothing checked";
        }

        var corpus = LoadSingles();
        var missing = new HashSet<int>();
        TMP_Text.MissingCharacterEventCallback onMissing = (unicode, index, source, asset, component) => missing.Add(unicode);
        TMP_Text.OnMissingCharacter += onMissing;

        var canvas = new GameObject("EmojiCheck", typeof(Canvas));
        var text = new GameObject("Text", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
        text.transform.SetParent(canvas.transform, false);
        text.font = body;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.rectTransform.sizeDelta = new Vector2(100000, 100000);

        int viaEmoji = 0, viaOther = 0, blank = 0;
        try
        {
            void Render(string content, string label)
            {
                text.text = content;
                text.ForceMeshUpdate();
                for (var i = 0; i < text.textInfo.characterCount; i++)
                {
                    var info = text.textInfo.characterInfo[i];
                    if (info.character == ' ' || info.elementType != TMP_TextElementType.Character) continue;
                    if (info.fontAsset == emoji) viaEmoji++; else viaOther++;
                    if (info.fontAsset == emoji && info.textElement.glyph.glyphRect.width <= 0)
                    {
                        blank++;
                        failures.Add($"blank glyph U+{(int)info.character:X4} in {label}");
                    }
                }
            }

            for (var i = 0; i < corpus.Count; i += Chunk)
            {
                var slice = corpus.Skip(i).Take(Chunk).ToList();
                Render(string.Join(" ", slice.Select(char.ConvertFromUtf32)), $"chunk {i}");
            }
            foreach (var name in PiratesNames) Render(name, "Pirates name");
        }
        finally
        {
            TMP_Text.OnMissingCharacter -= onMissing;
            UnityEngine.Object.DestroyImmediate(canvas);
        }

        foreach (var cp in missing.OrderBy(c => c)) failures.Add($"missing code point U+{cp:X4}");

        long ink = 0, colour = 0;
        int pages = 0, nullPages = 0;
        foreach (var page in emoji.atlasTextures)
        {
            if (page == null) { nullPages++; continue; }
            pages++;
            var pixels = page.GetPixels32();
            ink += pixels.LongCount(p => p.a != 0);
            colour += pixels.LongCount(p => p.a != 0 && (p.r != p.g || p.g != p.b));
        }
        if (ink == 0) failures.Add("the emoji atlas holds no ink after rendering");
        else if (colour == 0) failures.Add("the emoji atlas holds no coloured pixel: the glyphs rendered monochrome");

        // Dynamic atlas content is cache: drop it so this check leaves the asset as Build wrote it.
        emoji.ClearFontAssetData(true);
        return $"checked {corpus.Count} single code points + {PiratesNames.Length} Pirates names, {missing.Count} missing, " +
               $"{viaEmoji} glyphs via the emoji asset, {viaOther} via other fonts, {blank} blank, {ink} atlas ink pixels of which {colour} coloured, {pages} atlas pages ({nullPages} null)";
    }

    static List<int> LoadSingles()
    {
        var path = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "tools", "emoji", "emoji-singles.txt");
        var all = File.ReadAllLines(path).Where(l => l.Length > 0).Select(l => Convert.ToInt32(l, 16)).ToList();
        var args = Environment.GetCommandLineArgs();
        var at = Array.IndexOf(args, "-emojiLimit");
        if (at < 0 || at + 1 >= args.Length) return all;
        var limit = int.Parse(args[at + 1]);
        return Enumerable.Range(0, limit).Select(i => all[i * all.Count / limit]).ToList();
    }
}
