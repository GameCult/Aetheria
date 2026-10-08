using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;

// Every single-code-point emoji of the pinned Unicode Emoji version that Twemoji draws, and the Pirates product
// names read from the shipped catalog, render through an ordinary TMP text on the game's body font with no
// missing code point, no blank glyph, ink in the emoji atlas, and each glyph served by the emoji asset or by the
// body font itself, never by a third font. TMP Settings must route emoji by exactly one entry: the global
// fallback (the emoji fallback list stays empty), so dropping that entry or re-adding the second route fails.
// Run as -executeMethod EmojiCheck.Run (exit 1 on failure in batchmode, as EngineAssetCheck). Corpus:
// tools/emoji/emoji-singles.txt (from emoji-test.txt); the emoji Twemoji lacks are listed in
// tools/emoji/emoji-unsupported.txt and reported, not rendered; -emojiLimit N spreads N entries evenly for a spike.
public static class EmojiCheck
{
    const string BodyFontPath = "Assets/Fonts/Ubuntu/Ubuntu-L Small SDF.asset";
    const string PiratesFaction = "Pirates";
    const int Chunk = 64;

    [MenuItem("Aetheria/Text/Check Emoji Rendering")]
    public static void Run()
    {
        var failures = new List<string>();
        var summary = RunCheck(failures);
        foreach (var failure in failures.Take(20)) Debug.LogError(failure);
        Debug.Log($"EmojiCheck: {summary}; {failures.Count} failure(s).");
        if (Application.isBatchMode) EditorApplication.Exit(failures.Count == 0 ? 0 : 1);
    }

    // Public so a scratch driver can mutate TMP Settings in memory and watch this fail; adds to failures.
    public static string RunCheck(List<string> failures)
    {
        var emoji = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(EmojiFont.AssetPath);
        var body = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BodyFontPath);
        if (emoji == null || body == null)
        {
            failures.Add("EmojiCheck: Emoji.asset or the body font is missing; run EmojiFont.Build first.");
            return "nothing checked";
        }

        // One route: the global fallback names the emoji asset, and the emoji fallback list is empty.
        if (TMP_Settings.emojiFallbackTextAssets != null && TMP_Settings.emojiFallbackTextAssets.Count > 0)
            failures.Add("TMP Settings' emoji fallback list is not empty: a second emoji route exists (EmojiFont.Build leaves it empty).");
        if (TMP_Settings.fallbackFontAssets == null || !TMP_Settings.fallbackFontAssets.Contains(emoji))
            failures.Add("TMP Settings' global fallback list does not name Emoji.asset: the only emoji route is gone.");

        var unsupported = new HashSet<int>(LoadCodePoints("emoji-unsupported.txt"));
        var corpus = LoadSingles().Where(cp => !unsupported.Contains(cp)).ToList();
        var missing = new HashSet<int>();
        TMP_Text.MissingCharacterEventCallback onMissing = (unicode, index, source, asset, component) => missing.Add(unicode);
        TMP_Text.OnMissingCharacter += onMissing;

        var canvas = new GameObject("EmojiCheck", typeof(Canvas));
        var text = new GameObject("Text", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
        text.transform.SetParent(canvas.transform, false);
        text.font = body;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.rectTransform.sizeDelta = new Vector2(100000, 100000);

        int viaEmoji = 0, viaBody = 0, blank = 0, pirateNames = 0;
        var strays = new HashSet<string>();
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
                    if (info.fontAsset == emoji) viaEmoji++;
                    else if (info.fontAsset == body) viaBody++;
                    else if (strays.Add($"{info.fontAsset.name}:{(int)info.character:X}"))
                        failures.Add($"U+{(int)info.character:X4} in {label} was served by {info.fontAsset.name}, not Emoji.asset or the body font");
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

            // The Pirates names as the shipped catalog stores them, through the game's own catalog reader.
            var catalogPath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "GameData", "Aetheria.cc");
            using (var catalog = AetheriaStores.Open(catalogPath))
            {
                var pirates = catalog.GetAll<Faction>().SingleOrDefault(faction => faction.Name == PiratesFaction);
                if (pirates == null) failures.Add($"the catalog has no {PiratesFaction} faction");
                else
                {
                    var key = catalog.RefOf(pirates);
                    foreach (var product in catalog.GetAll<FactionProductData>().Where(product => product.Manufacturer.Equals(key)))
                    {
                        pirateNames++;
                        Render(product.Name, $"Pirates product name \"{product.Name}\"");
                    }
                    if (pirateNames == 0) failures.Add($"the catalog has no {PiratesFaction} products");
                }
            }
        }
        finally
        {
            TMP_Text.OnMissingCharacter -= onMissing;
            UnityEngine.Object.DestroyImmediate(canvas);
        }

        foreach (var cp in missing.OrderBy(c => c)) failures.Add($"missing code point U+{cp:X4}");

        // TMP over-allocates atlasTextures by doubling its capacity when a page is added, so entries past
        // atlasTextureCount are null by design (3 pages in a 4-slot array). Only the used pages count.
        long ink = 0, colour = 0;
        for (var i = 0; i < emoji.atlasTextureCount; i++)
        {
            var page = emoji.atlasTextures[i];
            if (page == null) { failures.Add($"emoji atlas page {i} of {emoji.atlasTextureCount} is null"); continue; }
            var pixels = page.GetPixels32();
            ink += pixels.LongCount(p => p.a != 0);
            colour += pixels.LongCount(p => p.a != 0 && (p.r != p.g || p.g != p.b));
        }
        if (ink == 0) failures.Add("the emoji atlas holds no ink after rendering");
        else if (colour == 0) failures.Add("the emoji atlas holds no coloured pixel: the glyphs rendered monochrome");

        var pages = emoji.atlasTextureCount;
        // Dynamic atlas content is cache: drop it so this check leaves the asset as Build wrote it.
        emoji.ClearFontAssetData(true);
        if (unsupported.Count > 0)
            Debug.LogWarning($"EmojiCheck: Twemoji lacks {unsupported.Count} single-code-point emoji of the Emoji version: " +
                             string.Join(" ", unsupported.OrderBy(c => c).Select(c => $"U+{c:X4}")));
        return $"checked {corpus.Count} single code points ({unsupported.Count} not drawn by Twemoji, listed in the warning) + {pirateNames} Pirates product names, " +
               $"{missing.Count} missing, {viaEmoji} glyphs via the emoji asset, {viaBody} via the body font, {strays.Count} via other fonts, {blank} blank, " +
               $"{ink} atlas ink pixels of which {colour} coloured, {pages} atlas pages";
    }

    static List<int> LoadSingles()
    {
        var all = LoadCodePoints("emoji-singles.txt");
        var args = Environment.GetCommandLineArgs();
        var at = Array.IndexOf(args, "-emojiLimit");
        if (at < 0 || at + 1 >= args.Length) return all;
        var limit = int.Parse(args[at + 1]);
        return Enumerable.Range(0, limit).Select(i => all[i * all.Count / limit]).ToList();
    }

    static List<int> LoadCodePoints(string file)
    {
        var path = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "tools", "emoji", file);
        return File.ReadAllLines(path).Where(l => l.Length > 0).Select(l => Convert.ToInt32(l, 16)).ToList();
    }
}
