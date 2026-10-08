using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

// The one writer of the emoji font asset and of TMP Settings' emoji routing. The font file is the
// output of tools/emoji/fetch_emoji_font.py; the asset is a dynamic COLOR font asset whose atlas pages
// are cache, filled on first use at runtime and cleared here. Every TMP text reaches emoji through
// TMP Settings' emoji fallback and global fallback list; no component names an emoji font.
public static class EmojiFont
{
    public const string FontPath = "Assets/Fonts/Emoji/Twemoji.ttf";
    public const string AssetPath = "Assets/Fonts/Emoji/Emoji.asset";

    // Colour outlines (COLRv0) are rasterised at one size; 109 is the strike size a CBDT set needs.
    const int SamplingPointSize = 64;
    const int AtlasPadding = 4;
    const int AtlasSize = 2048;

    // Menu entry for interactive use; -executeMethod EmojiFont.Build for batchmode, exit 1 on failure.
    [MenuItem("Aetheria/Text/Build Emoji Font")]
    public static void Build()
    {
        var ok = TryBuild(out var message);
        if (ok) Debug.Log(message); else Debug.LogError(message);
        if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
    }

    static bool TryBuild(out string message)
    {
        var font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
        if (font == null)
        {
            message = $"EmojiFont: {FontPath} is missing; run tools/emoji/fetch_emoji_font.py.";
            return false;
        }

        // An existing asset keeps its GUID, so a second Build changes nothing the settings point at.
        var asset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetPath);
        if (asset == null)
        {
            asset = TMP_FontAsset.CreateFontAsset(font, SamplingPointSize, AtlasPadding, GlyphRenderMode.COLOR,
                AtlasSize, AtlasSize, AtlasPopulationMode.Dynamic, enableMultiAtlasSupport: true);
            if (asset == null)
            {
                message = $"EmojiFont: TMP could not create a font asset from {FontPath}.";
                return false;
            }
            asset.name = "Emoji";
            AssetDatabase.CreateAsset(asset, AssetPath);
            foreach (var page in asset.atlasTextures)
            {
                page.name = "Emoji Atlas";
                AssetDatabase.AddObjectToAsset(page, asset);
            }
            asset.material.name = "Emoji Atlas Material";
            AssetDatabase.AddObjectToAsset(asset.material, asset);
        }
        else asset.ClearFontAssetData(true);

        // Text-presentation emoji (U+263A without FE0F) are not routed through the emoji list, so the
        // asset is also the global fallback.
        TMP_Settings.emojiFallbackTextAssets = new List<TMP_Asset> { asset };
        TMP_Settings.fallbackFontAssets = new List<TMP_FontAsset> { asset };
        TMP_Settings.defaultSpriteAsset = null;

        EditorUtility.SetDirty(asset);
        EditorUtility.SetDirty(TMP_Settings.instance);
        AssetDatabase.SaveAssets();
        message = "EmojiFont: built " + AssetPath + " and routed TMP Settings' emoji fallback to it.";
        return true;
    }
}
